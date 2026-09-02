import { useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "@/store/auth";
import { Button, ErrorText, Field, Input } from "@/components/ui";
import { Logomark } from "@/components/icons";
import type { ApiError } from "@/lib/api";

export default function Login() {
  const login = useAuth((s) => s.login);
  const [identifier, setIdentifier] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await login(identifier.trim(), password);
    } catch (err) {
      setError(err as ApiError);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="mx-auto flex min-h-full max-w-sm flex-col justify-center px-6 py-10">
      <div className="mb-8 flex flex-col items-center text-center">
        <Logomark size={56} className="text-brand" />
        <div className="mt-3 text-2xl font-bold tracking-tight">Swarnakshi</div>
        <div className="mt-1 text-sm text-text-dim">Construction Expense &amp; Inventory</div>
      </div>

      <form onSubmit={submit} className="space-y-3">
        <Field label="Username">
          <Input
            autoComplete="username"
            autoCapitalize="none"
            autoCorrect="off"
            placeholder="yourname@companycode"
            value={identifier}
            onChange={(e) => setIdentifier(e.target.value)}
            required
          />
        </Field>
        <Field label="Password">
          <Input
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
        </Field>
        <ErrorText error={error} />
        <Button type="submit" className="w-full" disabled={busy}>
          {busy ? "Signing in…" : "Sign in"}
        </Button>
      </form>

      <p className="mt-6 text-center text-xs text-text-dim">
        New company?{" "}
        <Link to="/register" className="font-semibold text-brand-ink underline">
          Register here
        </Link>
      </p>
    </div>
  );
}
