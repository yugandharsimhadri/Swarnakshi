import { useState } from "react";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { dateStr } from "@/lib/format";
import {
  Button, Card, Chip, EmptyState, ErrorText, Field, Input, Select, Sheet, SkeletonList,
} from "@/components/ui";
import { EngagementName, type CompanyAdmin, type CompanyOverview, type CompanyUsage, type PlatformUsage } from "@/lib/types";

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

  // Loaded apart from the company list, and not re-run while somebody types in the search box:
  // adoption is a property of the whole book of customers, not of the three rows being searched.
  const { data: usage } = useAsync(() => api<PlatformUsage>("/platform/usage"), []);
  const byId = new Map((usage?.items ?? []).map((u) => [u.id, u]));

  const expiring = usage?.expiringSoon ?? 0;
  const expired = usage?.expired ?? 0;

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

      {/* Adoption first, licences second. A renewal is easy to see coming; a customer who
          registered in March and has never entered a villa is not, and is the one worth a call. */}
      <div className="mb-2 grid grid-cols-4 gap-2">
        <Figure label="Companies" value={usage?.companies} />
        <Figure label="Active" value={usage?.active} tone={usage && usage.active ? "ok" : undefined} hint="entered something in 30 days" />
        <Figure label="Dormant" value={usage?.dormant} tone={usage && usage.dormant ? "warn" : undefined} hint="has data, nothing in 30 days" />
        <Figure label="Never started" value={usage?.neverStarted} tone={usage && usage.neverStarted ? "danger" : undefined} hint="registered, never used" />
      </div>
      <div className="mb-3 grid grid-cols-4 gap-2">
        <Figure label="New" value={usage?.justRegistered} hint="registered within a fortnight" />
        <Figure label="Expiring ≤30d" value={expiring} tone={expiring ? "warn" : undefined} />
        <Figure label="Expired" value={expired} tone={expired ? "danger" : undefined} />
        <Figure label="Entries, 30d" value={usage?.writes30} hint={usage ? `${usage.totalProjects} projects, ${usage.totalRecords} records all time` : undefined} />
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
                      <EngagementChip usage={byId.get(c.id)} />
                    </div>
                    <div className="mt-0.5 text-xs text-text-dim">
                      Licence to {dateStr(c.licenseExpiresOn)} · {c.userCount} users · {c.siteCount} sites · {c.projectCount} projects
                    </div>
                    <UsageLine usage={byId.get(c.id)} />
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

/** One number on the console's header. Undefined while the usage call is still in flight. */
function Figure({ label, value, tone, hint }: {
  label: string; value?: number; tone?: "ok" | "warn" | "danger"; hint?: string;
}) {
  const colour = tone === "ok" ? "text-ok" : tone === "warn" ? "text-warn" : tone === "danger" ? "text-danger" : "";
  return (
    <Card className="py-3">
      <div className="text-xs text-text-dim">{label}</div>
      <div className={`text-xl font-semibold ${colour}`}>{value ?? "—"}</div>
      {hint && <div className="mt-0.5 text-[11px] leading-tight text-text-dim">{hint}</div>}
    </Card>
  );
}

function EngagementChip({ usage }: { usage?: CompanyUsage }) {
  if (!usage) return null;
  const tone = usage.engagement === 3 ? "ok" : usage.engagement === 2 ? "warn" : usage.engagement === 1 ? "danger" : "neutral";
  return <Chip tone={tone}>{EngagementName[usage.engagement]}</Chip>;
}

/**
 * What this customer has actually done. Written as a sentence rather than a row of figures,
 * because the operator is reading it to decide whether to pick up the phone.
 */
function UsageLine({ usage }: { usage?: CompanyUsage }) {
  if (!usage) return null;
  const last = usage.daysSinceActivity == null
    ? "nothing entered yet"
    : usage.daysSinceActivity === 0
      ? "last entry today"
      : `last entry ${usage.daysSinceActivity}d ago`;
  return (
    <div className="text-xs text-text-dim">
      {usage.records} records · {usage.writes30} entries in 30d · {last} · registered {usage.daysSinceRegistered}d ago
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
