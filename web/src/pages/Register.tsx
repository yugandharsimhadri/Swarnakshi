import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api, type ApiError } from "@/lib/api";
import { useAuth } from "@/store/auth";
import { Button, Card, ErrorText, Field, Input } from "@/components/ui";
import { dateStr } from "@/lib/format";
import { Logomark } from "@/components/icons";

interface RegisterResponse {
  companyId: string;
  companyCode: string;
  companyName: string;
  login: string;
  licenseExpiresOn: string;
}

/** Mirrors the server's rule so the form can say why before it submits. */
const codeOk = (c: string) => /^[a-z0-9][a-z0-9-]*[a-z0-9]$/.test(c) && c.length >= 2 && c.length <= 30;
const userOk = (u: string) => /^[a-z0-9][a-z0-9._-]*$/.test(u) && u.length >= 3 && u.length <= 60;

export default function Register() {
  const nav = useNavigate();
  const doLogin = useAuth((s) => s.login);

  const [form, setForm] = useState({
    companyName: "", companyCode: "", username: "", password: "", confirmPassword: "",
    contactEmail: "", contactMobile: "",
  });
  const [codeState, setCodeState] = useState<"idle" | "checking" | "free" | "taken" | "invalid">("idle");
  const [error, setError] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState<RegisterResponse | null>(null);

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm({ ...form, [k]: k === "companyCode" || k === "username" ? e.target.value.toLowerCase() : e.target.value });

  // Tell the person the code is taken while they type, not after they fill the whole form in.
  useEffect(() => {
    const code = form.companyCode.trim();
    if (!code) { setCodeState("idle"); return; }
    if (!codeOk(code)) { setCodeState("invalid"); return; }

    setCodeState("checking");
    const timer = setTimeout(async () => {
      try {
        const res = await api<{ available: boolean }>("/register/code-available", { query: { code } });
        setCodeState(res.available ? "free" : "taken");
      } catch { setCodeState("idle"); }
    }, 350);
    return () => clearTimeout(timer);
  }, [form.companyCode]);

  const passwordsMatch = form.password.length > 0 && form.password === form.confirmPassword;
  const canSubmit =
    form.companyName.trim().length >= 2 &&
    codeState === "free" &&
    userOk(form.username) &&
    form.password.length >= 8 &&
    passwordsMatch;

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const res = await api<RegisterResponse>("/register", {
        method: "POST",
        body: {
          companyName: form.companyName.trim(),
          companyCode: form.companyCode.trim(),
          username: form.username.trim(),
          password: form.password,
          confirmPassword: form.confirmPassword,
          contactEmail: form.contactEmail.trim() || null,
          contactMobile: form.contactMobile.trim() || null,
        },
      });
      setDone(res);
    } catch (err) {
      setError(err as ApiError);
    } finally {
      setBusy(false);
    }
  }

  async function signInNow() {
    if (!done) return;
    setBusy(true);
    try {
      await doLogin(done.login, form.password);
      nav("/");
    } catch (err) {
      setError(err as ApiError);
      setBusy(false);
    }
  }

  if (done) {
    return (
      <div className="mx-auto flex min-h-full max-w-sm flex-col justify-center gap-4 px-6 py-10">
        <div className="text-center">
          <Logomark size={52} className="mx-auto text-brand" />
          <h1 className="mt-3 text-xl font-bold">{done.companyName} is registered</h1>
        </div>
        <Card className="space-y-2">
          <div>
            <div className="text-xs text-text-dim">Your login</div>
            <div className="text-lg font-semibold tabular-nums">{done.login}</div>
          </div>
          <p className="text-xs text-text-dim">
            Everyone in your company signs in as <code>username@{done.companyCode}</code>
            {form.contactMobile.trim() && <> — or you can use your mobile number</>}.
            Your licence runs to <strong>{dateStr(done.licenseExpiresOn)}</strong>.
          </p>
        </Card>
        <ErrorText error={error} />
        <Button className="w-full" onClick={signInNow} disabled={busy}>
          {busy ? "Signing in…" : "Sign in and get started"}
        </Button>
      </div>
    );
  }

  return (
    <div className="mx-auto flex min-h-full max-w-sm flex-col justify-center px-6 py-10">
      <div className="mb-6 text-center">
        <div className="text-2xl font-bold tracking-tight">Register your company</div>
        <div className="mt-1 text-sm text-text-dim">Your own sites, stock and books — separate from everyone else's.</div>
      </div>

      <form onSubmit={submit} className="space-y-3">
        <Field label="Company name">
          <Input value={form.companyName} onChange={set("companyName")} placeholder="Acme Builders" required />
        </Field>

        <Field
          label="Company code"
          error={
            codeState === "taken" ? "Already taken — please choose another."
              : codeState === "invalid" ? "2–30 characters: lowercase letters, digits or hyphens."
              : undefined
          }
        >
          <Input
            value={form.companyCode}
            onChange={set("companyCode")}
            placeholder="acme"
            autoCapitalize="none"
            autoCorrect="off"
            required
          />
          <span className="mt-1 block text-xs text-text-dim">
            {codeState === "checking" && "Checking…"}
            {codeState === "free" && `✓ Available — you will sign in as ${form.username || "yourname"}@${form.companyCode}`}
            {codeState === "idle" && "Used in every login. Cannot be changed later."}
          </span>
        </Field>

        <Field
          label="Your username"
          error={form.username && !userOk(form.username) ? "3–60 characters: lowercase letters, digits, dot, underscore or hyphen." : undefined}
        >
          <Input
            value={form.username}
            onChange={set("username")}
            placeholder="ravi"
            autoCapitalize="none"
            autoCorrect="off"
            autoComplete="username"
            required
          />
        </Field>

        <Field label="Password" error={form.password && form.password.length < 8 ? "At least 8 characters." : undefined}>
          <Input type="password" autoComplete="new-password" value={form.password} onChange={set("password")} required />
        </Field>

        <Field
          label="Retype password"
          error={form.confirmPassword && !passwordsMatch ? "The two passwords do not match." : undefined}
        >
          <Input type="password" autoComplete="new-password" value={form.confirmPassword} onChange={set("confirmPassword")} required />
        </Field>

        <Field label="Contact email (optional)">
          <Input inputMode="email" value={form.contactEmail} onChange={set("contactEmail")} />
        </Field>
        <Field label="Your mobile number (optional)">
          <Input inputMode="tel" value={form.contactMobile} onChange={set("contactMobile")} placeholder="9876543210" />
          <span className="mt-1 block text-xs text-text-dim">
            Add it and you can sign in with just your number — no @company needed.
          </span>
        </Field>

        <ErrorText error={error} />
        <Button type="submit" className="w-full" disabled={busy || !canSubmit}>
          {busy ? "Creating…" : "Create company"}
        </Button>
      </form>

      <p className="mt-6 text-center text-xs text-text-dim">
        Already registered?{" "}
        <Link to="/login" className="font-semibold text-brand-ink underline">Sign in</Link>
      </p>
    </div>
  );
}
