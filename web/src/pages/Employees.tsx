import { useState } from "react";
import { Link } from "react-router-dom";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { money, dateStr } from "@/lib/format";
import {
  Button, Card, Chip, EmptyState, ErrorText, Field, Input, PageHeader, Select, Sheet, SkeletonList,
} from "@/components/ui";
import {
  type EmployeePayment,
  type Employee, type EmployeeLedger, type Lookup, type Paged, type Project, type Site,
} from "@/lib/types";

const today = () => new Date().toISOString().slice(0, 10);

export default function Employees() {
  const canManage = useAuth((s) => s.can("masters.manage"));
  const canPay = useAuth((s) => s.can("labour.create"));
  const [q, setQ] = useState("");
  const [showInactive, setShowInactive] = useState(false);
  const [editing, setEditing] = useState<Employee | null>(null);
  const [creating, setCreating] = useState(false);
  const [payFor, setPayFor] = useState<Employee | null>(null);
  const [ledgerFor, setLedgerFor] = useState<Employee | null>(null);

  const { data, loading, error, reload } = useAsync(
    () => api<Paged<Employee>>("/employees", { query: { q, active: showInactive ? undefined : true, pageSize: 200 } }),
    [q, showInactive],
  );

  const totalAdvance = (data?.items ?? []).reduce((s, e) => s + e.advanceOutstanding, 0);

  return (
    <div className="space-y-3">
      <Link to="/more" className="text-xs text-text-dim">← More</Link>
      <PageHeader title="Employees" action={canManage && <Button onClick={() => setCreating(true)}>+ New</Button>} />

      <div className="grid grid-cols-2 gap-3">
        <Card className="py-3">
          <div className="text-xs text-text-dim">On payroll</div>
          <div className="text-xl font-semibold">{data?.total ?? "—"}</div>
        </Card>
        <Card className="py-3">
          <div className="text-xs text-text-dim">Advances outstanding</div>
          <div className={`text-xl font-semibold ${totalAdvance > 0 ? "text-warn" : ""}`}>{money(totalAdvance)}</div>
        </Card>
      </div>

      <Input placeholder="Search name, phone or designation…" value={q} onChange={(e) => setQ(e.target.value)} />
      <label className="flex items-center gap-2 px-1 text-xs text-text-dim">
        <input type="checkbox" checked={showInactive} onChange={(e) => setShowInactive(e.target.checked)} />
        Include people who have left
      </label>

      {loading ? <SkeletonList /> : error ? <ErrorText error={error} /> : (
        (data?.items.length ?? 0) === 0 ? <EmptyState title="No employees" hint={canManage ? "Tap + New to add one." : undefined} /> : (
          <div className="space-y-2">
            {data!.items.map((e) => (
              <Card key={e.id} className="space-y-2">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="truncate text-sm font-semibold">{e.name}</span>
                      {!e.isActive && <Chip tone="danger">Left</Chip>}
                      {e.advanceOutstanding > 0 && <Chip tone="warn">Adv {money(e.advanceOutstanding)}</Chip>}
                    </div>
                    <div className="truncate text-xs text-text-dim">
                      {e.designation ? `${e.designation} · ` : ""}{e.phone}
                    </div>
                    <div className="truncate text-xs text-text-dim">
                      {money(e.monthlySalary)}/month · joined {dateStr(e.joinDate)}
                      {e.siteName ? ` · ${e.siteName}` : ""}
                    </div>
                  </div>
                </div>
                <div className="flex gap-2">
                  {canPay && e.isActive && <Button className="flex-1" onClick={() => setPayFor(e)}>Pay</Button>}
                  <Button variant="ghost" className="flex-1" onClick={() => setLedgerFor(e)}>Ledger</Button>
                  {canManage && <Button variant="ghost" onClick={() => setEditing(e)}>Edit</Button>}
                </div>
              </Card>
            ))}
          </div>
        )
      )}

      {(creating || editing) && (
        <EmployeeSheet
          employee={editing}
          onClose={() => { setCreating(false); setEditing(null); }}
          onSaved={() => { setCreating(false); setEditing(null); reload(); }}
        />
      )}
      {payFor && <PaymentSheet employee={payFor} onClose={() => setPayFor(null)} onSaved={() => { setPayFor(null); reload(); }} />}
      {ledgerFor && <LedgerSheet employee={ledgerFor} onClose={() => setLedgerFor(null)} />}
    </div>
  );
}

function EmployeeSheet({ employee, onClose, onSaved }: { employee: Employee | null; onClose: () => void; onSaved: () => void }) {
  const { data: sites } = useAsync(() => api<Paged<Site>>("/sites", { query: { pageSize: 100 } }), []);
  const [form, setForm] = useState({
    name: employee?.name ?? "", phone: employee?.phone ?? "",
    monthlySalary: employee ? String(employee.monthlySalary) : "",
    joinDate: employee?.joinDate?.slice(0, 10) ?? today(),
    leaveDate: employee?.leaveDate?.slice(0, 10) ?? "",
    designation: employee?.designation ?? "", address: employee?.address ?? "",
    notes: employee?.notes ?? "", siteId: employee?.siteId ?? "", isActive: employee?.isActive ?? true,
  });
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);
  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
    setForm({ ...form, [k]: e.target.value });

  // The four the business insists on: who, how to reach them, what they earn, since when.
  const ready = form.name.trim() && form.phone.trim()
    && Number(form.monthlySalary) > 0 && form.joinDate;

  async function save() {
    setBusy(true); setErr(null);
    try {
      const body = {
        name: form.name.trim(), phone: form.phone.trim(),
        monthlySalary: Number(form.monthlySalary), joinDate: form.joinDate,
        leaveDate: form.leaveDate || null, designation: form.designation || null,
        address: form.address || null, notes: form.notes || null,
        siteId: form.siteId || null, isActive: form.isActive,
      };
      if (employee) await api(`/employees/${employee.id}`, { method: "PUT", body });
      else await api("/employees", { method: "POST", body });
      onSaved();
    } catch (e) { setErr(e as ApiError); } finally { setBusy(false); }
  }

  return (
    <Sheet open onClose={onClose} title={employee ? `Edit — ${employee.name}` : "New employee"}>
      <div className="space-y-3">
        <Field label="Designation"><Input value={form.designation} onChange={set("designation")} placeholder="Supervisor" /></Field>
        <Field label="Name *"><Input value={form.name} onChange={set("name")} /></Field>
        <Field label="Phone *"><Input inputMode="tel" value={form.phone} onChange={set("phone")} /></Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Monthly salary *"><Input inputMode="decimal" value={form.monthlySalary} onChange={set("monthlySalary")} /></Field>
          <Field label="Join date *"><Input type="date" value={form.joinDate} onChange={set("joinDate")} /></Field>
        </div>
        <Field label="Home site">
          <Select value={form.siteId} onChange={set("siteId")}>
            <option value="">— none —</option>
            {sites?.items.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </Select>
        </Field>
        <Field label="Address"><Input value={form.address} onChange={set("address")} /></Field>
        {employee && (
          <div className="grid grid-cols-2 gap-3">
            <Field label="Leave date"><Input type="date" value={form.leaveDate} onChange={set("leaveDate")} /></Field>
            <label className="mt-6 flex items-center gap-2 text-sm">
              <input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />
              Active
            </label>
          </div>
        )}
        <ErrorText error={err} />
        <Button className="w-full" onClick={save} disabled={busy || !ready}>
          {busy ? "Saving…" : employee ? "Save" : "Add employee"}
        </Button>
      </div>
    </Sheet>
  );
}

function PaymentSheet({ employee, onClose, onSaved }: { employee: Employee; onClose: () => void; onSaved: () => void }) {
  const { data: methods } = useAsync(() => api<Lookup[]>("/payment-methods"), []);
  const { data: projects } = useAsync(() => api<Paged<Project>>("/projects", { query: { pageSize: 100 } }), []);
  const [form, setForm] = useState({
    kind: "1", amount: String(employee.monthlySalary), advanceRecovered: "",
    periodStart: "", periodEnd: "", methodId: "", reference: "", projectId: "", remarks: "",
  });
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  const isAdvance = form.kind === "2";
  const recovered = isAdvance ? 0 : Number(form.advanceRecovered) || 0;
  const net = (Number(form.amount) || 0) - recovered;

  async function save(submit: boolean) {
    setBusy(true); setErr(null);
    try {
      const created = await api<EmployeePayment>("/employee-payments", {
        method: "POST",
        body: {
          employeeId: employee.id, date: today(), kind: Number(form.kind),
          amount: Number(form.amount), advanceRecovered: recovered,
          periodStart: form.periodStart || null, periodEnd: form.periodEnd || null,
          paymentMethodId: form.methodId || null, reference: form.reference || null,
          projectId: form.projectId || null, remarks: form.remarks || null,
        },
      });
      if (submit) await api(`/employee-payments/${created.id}/submit`, { method: "POST" });
      onSaved();
    } catch (e) { setErr(e as ApiError); } finally { setBusy(false); }
  }

  return (
    <Sheet open onClose={onClose} title={`Pay — ${employee.name}`}>
      <div className="space-y-3">
        <Card className="py-2 text-xs text-text-dim">
          {money(employee.monthlySalary)}/month
          {employee.advanceOutstanding > 0 && (
            <span className="text-warn"> · advance outstanding {money(employee.advanceOutstanding)}</span>
          )}
        </Card>

        <Field label="Payment for">
          <Select value={form.kind} onChange={(e) => setForm({ ...form, kind: e.target.value, advanceRecovered: "" })}>
            <option value="1">Salary</option>
            <option value="2">Advance</option>
            <option value="3">Bonus</option>
            <option value="4">Reimbursement</option>
          </Select>
        </Field>

        <Field label="Amount"><Input inputMode="decimal" value={form.amount} onChange={(e) => setForm({ ...form, amount: e.target.value })} /></Field>

        {!isAdvance && employee.advanceOutstanding > 0 && (
          <Field
            label="Recover advance"
            error={recovered > employee.advanceOutstanding ? `Only ${money(employee.advanceOutstanding)} is outstanding.` : undefined}
          >
            <Input inputMode="decimal" value={form.advanceRecovered} onChange={(e) => setForm({ ...form, advanceRecovered: e.target.value })} />
            <span className="mt-1 block text-xs text-text-dim">Net handed over: {money(net)}</span>
          </Field>
        )}

        {form.kind === "1" && (
          <div className="grid grid-cols-2 gap-3">
            <Field label="Period from"><Input type="date" value={form.periodStart} onChange={(e) => setForm({ ...form, periodStart: e.target.value })} /></Field>
            <Field label="Period to"><Input type="date" value={form.periodEnd} onChange={(e) => setForm({ ...form, periodEnd: e.target.value })} /></Field>
          </div>
        )}

        <Field label="Charge to project">
          <Select value={form.projectId} onChange={(e) => setForm({ ...form, projectId: e.target.value })}>
            <option value="">— company overhead —</option>
            {projects?.items.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
          </Select>
          <span className="mt-1 block text-xs text-text-dim">
            Left as overhead this never reaches any project's cost.
          </span>
        </Field>

        <Field label="Paid via">
          <Select value={form.methodId} onChange={(e) => setForm({ ...form, methodId: e.target.value })}>
            <option value="">—</option>
            {methods?.map((m) => <option key={m.id} value={m.id}>{m.name}</option>)}
          </Select>
        </Field>
        <Field label="Reference"><Input value={form.reference} onChange={(e) => setForm({ ...form, reference: e.target.value })} /></Field>

        <ErrorText error={err} />
        <p className="px-1 text-xs text-text-dim">Submitting sends this to the Owner for approval before any money is recorded.</p>
        <div className="flex gap-2">
          <Button variant="ghost" className="flex-1" onClick={() => save(false)} disabled={busy || !Number(form.amount)}>Draft</Button>
          <Button className="flex-1" onClick={() => save(true)} disabled={busy || !Number(form.amount)}>Submit</Button>
        </div>
      </div>
    </Sheet>
  );
}

function LedgerSheet({ employee, onClose }: { employee: Employee; onClose: () => void }) {
  const { data, loading, error } = useAsync(() => api<EmployeeLedger>(`/employees/${employee.id}/ledger`), [employee.id]);

  return (
    <Sheet open onClose={onClose} title={`Ledger — ${employee.name}`}>
      {loading ? <SkeletonList rows={3} /> : error ? <ErrorText error={error} /> : !data ? null : (
        <div className="space-y-3">
          <Card className="space-y-1 py-3 text-sm">
            <Row label="Salary" value={`${money(data.monthlySalary)}/month`} />
            <Row label="Total paid" value={money(data.totalPaid)} />
            <Row label="Advances given" value={money(data.advancesGiven)} />
            <Row label="Advances recovered" value={money(data.advancesRecovered)} />
            <div className="flex justify-between border-t border-border pt-1 font-semibold">
              <span>Advance outstanding</span>
              <span className={`tabular-nums ${data.advanceOutstanding > 0 ? "text-warn" : ""}`}>{money(data.advanceOutstanding)}</span>
            </div>
          </Card>

          {data.rows.length === 0 ? <EmptyState title="No payments yet" /> : (
            <div className="space-y-2">
              {data.rows.map((r) => (
                <Card key={r.ref} className="flex items-center justify-between py-2">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2 text-sm">
                      <Chip tone={r.kind === "Advance" ? "warn" : "neutral"}>{r.kind}</Chip>
                      <span className="text-xs text-text-dim">{r.status}</span>
                    </div>
                    <div className="text-xs text-text-dim">{dateStr(r.date)} · {r.ref}</div>
                  </div>
                  <div className="text-right text-sm tabular-nums">
                    <div>{money(r.netPaid)}</div>
                    {r.advanceRecovered > 0 && (
                      <div className="text-xs text-text-dim">{money(r.amount)} − {money(r.advanceRecovered)} adv</div>
                    )}
                  </div>
                </Card>
              ))}
            </div>
          )}
        </div>
      )}
    </Sheet>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return <div className="flex justify-between"><span className="text-text-dim">{label}</span><span className="tabular-nums">{value}</span></div>;
}

