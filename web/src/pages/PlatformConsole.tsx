import { useState } from "react";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { dateStr } from "@/lib/format";
import {
  Button, Card, Chip, EmptyState, ErrorText, Field, Input, Select, Sheet, SkeletonList,
} from "@/components/ui";
import type { CompanyAdmin, CompanyOverview } from "@/lib/types";

/**
 * The EnterpriseAdmin console. Deliberately its own screen with its own shell — no bottom tab bar,
 * no company navigation — because a platform operator has no company to navigate.
 */
export default function PlatformConsole() {
  const platformUser = useAuth((s) => s.platformUser);
  const logout = useAuth((s) => s.logout);
  const [q, setQ] = useState("");
  const [licenceFor, setLicenceFor] = useState<CompanyOverview | null>(null);
  const [resetFor, setResetFor] = useState<{ company: CompanyOverview; admin: CompanyAdmin } | null>(null);

  const { data, loading, error, reload } = useAsync(
    () => api<CompanyOverview[]>("/platform/companies", { query: { q } }),
    [q],
  );

  const expiring = (data ?? []).filter((c) => !c.isExpired && c.daysToExpiry <= 30).length;
  const expired = (data ?? []).filter((c) => c.isExpired).length;

  return (
    <div className="mx-auto min-h-full max-w-3xl px-3 pb-16 pt-3">
      <div className="mb-3 flex items-start justify-between gap-3">
        <div>
          <h1 className="text-lg font-bold">Enterprise Console</h1>
          <div className="text-xs text-text-dim">
            Signed in as {platformUser?.displayName ?? "EnterpriseAdmin"} · licences and admin passwords only
          </div>
        </div>
        <Button variant="ghost" onClick={logout}>Sign out</Button>
      </div>

      <div className="mb-3 grid grid-cols-3 gap-2">
        <Card className="py-3"><div className="text-xs text-text-dim">Companies</div><div className="text-xl font-semibold">{data?.length ?? "—"}</div></Card>
        <Card className="py-3"><div className="text-xs text-text-dim">Expiring ≤30d</div><div className={`text-xl font-semibold ${expiring ? "text-warn" : ""}`}>{expiring}</div></Card>
        <Card className="py-3"><div className="text-xs text-text-dim">Expired</div><div className={`text-xl font-semibold ${expired ? "text-danger" : ""}`}>{expired}</div></Card>
      </div>

      <Input placeholder="Search by company name or code…" value={q} onChange={(e) => setQ(e.target.value)} />

      <div className="mt-3 space-y-2">
        {loading ? <SkeletonList /> : error ? <ErrorText error={error} /> : (
          (data?.length ?? 0) === 0 ? <EmptyState title="No companies" /> :
            data!.map((c) => (
              <Card key={c.id} className="space-y-2">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="truncate font-semibold">{c.name}</span>
                      <Chip tone="brand">{c.code}</Chip>
                      {!c.isActive && <Chip tone="danger">Suspended</Chip>}
                      {c.isExpired
                        ? <Chip tone="danger">Expired</Chip>
                        : c.daysToExpiry <= 30 && <Chip tone="warn">{c.daysToExpiry}d left</Chip>}
                    </div>
                    <div className="mt-0.5 text-xs text-text-dim">
                      Licence to {dateStr(c.licenseExpiresOn)} · {c.userCount} users · {c.siteCount} sites · {c.projectCount} projects
                    </div>
                    {c.contactEmail && <div className="text-xs text-text-dim">{c.contactEmail}</div>}
                  </div>
                </div>

                <div className="space-y-1 border-t border-border pt-2">
                  {c.admins.length === 0
                    ? <div className="text-xs text-text-dim">No admin on record.</div>
                    : c.admins.map((a) => (
                      <div key={a.userId} className="flex items-center justify-between gap-2">
                        <div className="min-w-0 text-xs">
                          <span className="font-medium">{a.name}</span>
                          <span className="text-text-dim"> · {a.login}</span>
                          {!a.isActive && <Chip tone="danger">Inactive</Chip>}
                        </div>
                        <Button variant="ghost" onClick={() => setResetFor({ company: c, admin: a })}>
                          Reset password
                        </Button>
                      </div>
                    ))}
                </div>

                <div className="flex gap-2">
                  <Button className="flex-1" onClick={() => setLicenceFor(c)}>Licence</Button>
                  <Button
                    variant={c.isActive ? "danger" : "ghost"}
                    onClick={async () => {
                      await api(`/platform/companies/${c.id}/active`, { method: "PUT", body: { isActive: !c.isActive } });
                      reload();
                    }}
                  >
                    {c.isActive ? "Suspend" : "Reactivate"}
                  </Button>
                </div>
              </Card>
            ))
        )}
      </div>

      {licenceFor && (
        <LicenceSheet company={licenceFor} onClose={() => setLicenceFor(null)} onSaved={() => { setLicenceFor(null); reload(); }} />
      )}
      {resetFor && (
        <ResetPasswordSheet
          company={resetFor.company}
          admin={resetFor.admin}
          onClose={() => setResetFor(null)}
          onSaved={() => { setResetFor(null); reload(); }}
        />
      )}
    </div>
  );
}

function LicenceSheet({ company, onClose, onSaved }: { company: CompanyOverview; onClose: () => void; onSaved: () => void }) {
  const [expiresOn, setExpiresOn] = useState(company.licenseExpiresOn.slice(0, 10));
  const [extendDays, setExtendDays] = useState("365");
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  async function run(fn: () => Promise<unknown>) {
    setBusy(true); setErr(null);
    try { await fn(); onSaved(); }
    catch (e) { setErr(e as ApiError); }
    finally { setBusy(false); }
  }

  return (
    <Sheet open onClose={onClose} title={`Licence — ${company.name}`}>
      <div className="space-y-4">
        <Card className="py-3 text-sm">
          Currently expires <strong>{dateStr(company.licenseExpiresOn)}</strong>
          {company.isExpired
            ? <span className="text-danger"> · expired</span>
            : <span className="text-text-dim"> · {company.daysToExpiry} days left</span>}
        </Card>

        <div>
          <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-text-dim">Extend</div>
          <div className="flex gap-2">
            <Select value={extendDays} onChange={(e) => setExtendDays(e.target.value)}>
              <option value="30">30 days</option>
              <option value="90">90 days</option>
              <option value="180">180 days</option>
              <option value="365">1 year</option>
              <option value="730">2 years</option>
            </Select>
            <Button
              disabled={busy}
              onClick={() => run(() => api(`/platform/companies/${company.id}/license/extend`, {
                method: "POST", body: { days: Number(extendDays) },
              }))}
            >
              Extend
            </Button>
          </div>
          <p className="mt-1 text-xs text-text-dim">
            An expired licence extends from today, so a renewal always buys the full period.
          </p>
        </div>

        <div>
          <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-text-dim">Or set an exact date</div>
          <div className="flex gap-2">
            <Input type="date" value={expiresOn} onChange={(e) => setExpiresOn(e.target.value)} />
            <Button
              variant="ghost"
              disabled={busy || !expiresOn}
              onClick={() => run(() => api(`/platform/companies/${company.id}/license`, {
                method: "PUT", body: { expiresOn, notes: null },
              }))}
            >
              Set
            </Button>
          </div>
        </div>

        <ErrorText error={err} />
      </div>
    </Sheet>
  );
}

function ResetPasswordSheet({ company, admin, onClose, onSaved }: {
  company: CompanyOverview; admin: CompanyAdmin; onClose: () => void; onSaved: () => void;
}) {
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);
  const [doneMsg, setDoneMsg] = useState<string | null>(null);

  const match = password.length >= 8 && password === confirm;

  async function save() {
    setBusy(true); setErr(null);
    try {
      await api(`/platform/companies/${company.id}/reset-password`, {
        method: "POST",
        body: { userId: admin.userId, newPassword: password, confirmPassword: confirm },
      });
      setDoneMsg(`Password reset for ${admin.login}. Share it with them securely — their old sessions are signed out.`);
    } catch (e) { setErr(e as ApiError); }
    finally { setBusy(false); }
  }

  return (
    <Sheet open onClose={doneMsg ? onSaved : onClose} title={`Reset password — ${admin.name}`}>
      {doneMsg ? (
        <div className="space-y-3">
          <Card className="py-3 text-sm">{doneMsg}</Card>
          <Button className="w-full" onClick={onSaved}>Done</Button>
        </div>
      ) : (
        <div className="space-y-3">
          <Card className="py-3 text-xs text-text-dim">
            {admin.login} · {company.name}
          </Card>
          <Field label="New password" error={password && password.length < 8 ? "At least 8 characters." : undefined}>
            <Input type="password" autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} />
          </Field>
          <Field label="Retype password" error={confirm && password !== confirm ? "The two passwords do not match." : undefined}>
            <Input type="password" autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} />
          </Field>
          <ErrorText error={err} />
          <Button className="w-full" onClick={save} disabled={busy || !match}>
            {busy ? "Resetting…" : "Reset password"}
          </Button>
        </div>
      )}
    </Sheet>
  );
}
