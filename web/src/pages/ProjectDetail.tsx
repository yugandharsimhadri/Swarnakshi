import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { money, moneyShort, dateStr } from "@/lib/format";
import {
  Button, Card, Chip, EmptyState, ErrorText, Field, Input, Select, Sheet, Spinner, StatCard,
} from "@/components/ui";
import {
  ContractStatusName, ExpenseTypeName, ProjectStatusName, TxnStatusName,
  type CostByHead, type Contractor, type ContractWork, type ContractorPayment, type Customer,
  type CustomerPayment, type LabourEntry, type Lookup, type Paged, type Project, type ProjectExpense,
  type ProjectSummary,
} from "@/lib/types";

type Tab = "overview" | "expenses" | "labour" | "contracts" | "payments" | "customer";

export default function ProjectDetail() {
  const { id } = useParams<{ id: string }>();
  const [tab, setTab] = useState<Tab>("overview");
  const [editing, setEditing] = useState(false);
  const canManage = useAuth((st) => st.can("projects.manage"));
  const { data, loading, error, reload } = useAsync(async () => {
    const [project, summary] = await Promise.all([
      api<Project>(`/projects/${id}`),
      api<ProjectSummary>(`/projects/${id}/summary`),
    ]);
    return { project, summary };
  }, [id]);

  if (loading) return <Spinner />;
  if (error || !data) return <ErrorText error={error} />;
  const { project: p, summary: s } = data;

  const tabs: [Tab, string][] = [
    ["overview", "Overview"], ["expenses", "Expenses"], ["labour", "Labour"],
    ["contracts", "Contracts"], ["payments", "Payments"], ["customer", "Customer"],
  ];

  return (
    <div className="space-y-4">
      <Link to="/projects" className="text-xs text-text-dim">← Projects</Link>
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-lg font-bold">{p.name}</h1>
            <Chip tone={p.status === 1 ? "ok" : "neutral"}>{ProjectStatusName[p.status] ?? p.status}</Chip>
          </div>
          <div className="text-xs text-text-dim">{p.code} · {p.siteName}{p.customerName ? ` · ${p.customerName}` : ""}</div>
        </div>
        {canManage && <Button variant="ghost" onClick={() => setEditing(true)}>Edit</Button>}
      </div>
      <EditProjectSheet project={p} open={editing} onClose={() => setEditing(false)} onSaved={() => { setEditing(false); reload(); }} />

      <div className="grid grid-cols-2 gap-3">
        <StatCard label="Total cost" value={moneyShort(s.totalCost)} />
        <StatCard label="Estimated" value={moneyShort(s.estimatedCost)} />
        <StatCard label="Budget variance" value={moneyShort(s.budgetVariance)} tone={s.budgetVariance < 0 ? "danger" : "ok"} />
        <StatCard label="Margin" value={s.margin == null ? "—" : moneyShort(s.margin)} tone={s.margin != null && s.margin < 0 ? "danger" : "ok"} />
      </div>

      <div className="flex gap-1 overflow-x-auto rounded-xl bg-surface-2 p-1">
        {tabs.map(([t, label]) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`whitespace-nowrap rounded-lg px-3 py-1.5 text-xs font-semibold ${tab === t ? "bg-surface text-text" : "text-text-dim"}`}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === "overview" && <Overview projectId={p.id} s={s} />}
      {tab === "expenses" && <Expenses projectId={p.id} />}
      {tab === "labour" && <Labour projectId={p.id} />}
      {tab === "contracts" && <Contracts projectId={p.id} />}
      {tab === "payments" && <Payments projectId={p.id} />}
      {tab === "customer" && <CustomerTab projectId={p.id} hasCustomer={!!p.customerId} summary={s} />}
    </div>
  );
}

// ---- Customer tab -------------------------------------------------
function CustomerTab({ projectId, hasCustomer, summary }: { projectId: string; hasCustomer: boolean; summary: ProjectSummary }) {
  const canCreate = useAuth((s) => s.can("customer_payment.create"));
  const [open, setOpen] = useState(false);
  const { data, loading, error, reload } = useAsync(
    () => api<Paged<CustomerPayment>>("/customer-payments", { query: { projectId, pageSize: 100 } }),
    [projectId],
  );

  if (!hasCustomer) return <EmptyState title="No customer assigned" hint="Edit the project to link a customer." />;

  return (
    <div className="space-y-2">
      <Card>
        <Row label="Sale value" value={money(summary.contractSaleValue)} />
        <Row label="Received" value={money(summary.customerReceived)} />
        <div className="flex justify-between border-t border-border pt-1 text-sm font-semibold">
          <span>Outstanding</span><span className="tabular-nums text-warn">{money(summary.customerOutstanding)}</span>
        </div>
      </Card>
      {canCreate && <Button className="w-full" onClick={() => setOpen(true)}>+ Record receipt</Button>}
      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
        (data?.items.length ?? 0) === 0 ? <EmptyState title="No receipts yet" /> : data!.items.map((r) => (
          <Card key={r.id} className="flex items-center justify-between">
            <div>
              <div className="text-sm font-semibold">{r.paymentMethodName}{r.reference ? ` · ${r.reference}` : ""}</div>
              <div className="text-xs text-text-dim">{dateStr(r.date)} · {r.txnNumber}</div>
            </div>
            <span className={`text-sm tabular-nums ${r.status === 5 ? "text-text-dim line-through" : "text-ok"}`}>{money(r.amount)}</span>
          </Card>
        ))
      )}
      <RecordReceiptSheet projectId={projectId} open={open} onClose={() => setOpen(false)} onSaved={() => { setOpen(false); reload(); }} />
    </div>
  );
}

function RecordReceiptSheet({ projectId, open, onClose, onSaved }: { projectId: string; open: boolean; onClose: () => void; onSaved: () => void }) {
  const { data: methods } = useAsync(() => api<Lookup[]>("/payment-methods"), []);
  const [form, setForm] = useState({ amount: "", methodId: "", reference: "" });
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  async function save() {
    setBusy(true); setErr(null);
    try {
      await api("/customer-payments", {
        method: "POST",
        body: {
          projectId, date: new Date().toISOString().slice(0, 10), amount: Number(form.amount),
          paymentMethodId: form.methodId, reference: form.reference || null,
        },
      });
      onSaved();
    } catch (e) { setErr(e as ApiError); } finally { setBusy(false); }
  }

  return (
    <Sheet open={open} onClose={onClose} title="Record customer receipt">
      <div className="space-y-3">
        <Field label="Amount"><Input inputMode="decimal" value={form.amount} onChange={(e) => setForm({ ...form, amount: e.target.value })} /></Field>
        <Field label="Received via">
          <Select value={form.methodId} onChange={(e) => setForm({ ...form, methodId: e.target.value })}>
            <option value="">Select…</option>
            {methods?.map((m) => <option key={m.id} value={m.id}>{m.name}</option>)}
          </Select>
        </Field>
        <Field label="Reference"><Input value={form.reference} onChange={(e) => setForm({ ...form, reference: e.target.value })} /></Field>
        <ErrorText error={err} />
        <Button className="w-full" onClick={save} disabled={busy || !form.methodId || !Number(form.amount)}>Save receipt</Button>
      </div>
    </Sheet>
  );
}

function Overview({ projectId, s }: { projectId: string; s: ProjectSummary }) {
  const { data } = useAsync(() => api<CostByHead[]>("/expenses/cost-by-head", { query: { projectId } }), [projectId]);
  const rows: [string, number][] = [
    ["Material", s.materialCost], ["Labour", s.labourCost], ["Contractor", s.contractorCost], ["Other", s.otherCost],
  ];
  return (
    <div className="space-y-3">
      <Card>
        <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-text-dim">Cost by type</div>
        {rows.map(([l, v]) => (
          <div key={l} className="flex justify-between py-0.5 text-sm">
            <span className="text-text-dim">{l}</span><span className="tabular-nums">{money(v)}</span>
          </div>
        ))}
        <div className="mt-1 flex justify-between border-t border-border pt-1.5 text-sm font-semibold">
          <span>Total</span><span className="tabular-nums">{money(s.totalCost)}</span>
        </div>
      </Card>

      {data && data.length > 0 && (
        <Card>
          <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-text-dim">Cost by head</div>
          {data.map((r) => (
            <div key={r.expenseHeadId} className="flex justify-between py-0.5 text-sm">
              <span className="text-text-dim">{r.expenseHeadName}</span><span className="tabular-nums">{money(r.amount)}</span>
            </div>
          ))}
        </Card>
      )}

      <Card>
        <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-text-dim">Customer</div>
        <Row label="Sale value" value={money(s.contractSaleValue)} />
        <Row label="Received" value={money(s.customerReceived)} />
        <Row label="Outstanding" value={money(s.customerOutstanding)} />
      </Card>
    </div>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return <div className="flex justify-between py-0.5 text-sm"><span className="text-text-dim">{label}</span><span className="tabular-nums">{value}</span></div>;
}

function EditProjectSheet({ project, open, onClose, onSaved }: { project: Project; open: boolean; onClose: () => void; onSaved: () => void }) {
  const { data: customers } = useAsync(() => api<Paged<Customer>>("/customers", { query: { pageSize: 200, active: true } }), []);
  const { data: types } = useAsync(() => api<Lookup[]>("/project-types"), []);
  const [form, setForm] = useState({
    name: project.name, villaNumber: project.villaNumber ?? "", customerId: project.customerId ?? "",
    projectTypeId: project.projectTypeId ?? "", estimatedCost: String(project.estimatedCost),
    contractSaleValue: project.contractSaleValue != null ? String(project.contractSaleValue) : "",
    status: String(project.status),
  });
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  async function save() {
    setBusy(true); setErr(null);
    try {
      await api(`/projects/${project.id}`, {
        method: "PUT",
        body: {
          code: project.code, name: form.name, villaNumber: form.villaNumber || null, siteId: project.siteId,
          customerId: form.customerId || null, projectTypeId: form.projectTypeId || null,
          estimatedCost: Number(form.estimatedCost || 0),
          contractSaleValue: form.contractSaleValue ? Number(form.contractSaleValue) : null,
          status: Number(form.status),
        },
      });
      onSaved();
    } catch (e) { setErr(e as ApiError); } finally { setBusy(false); }
  }

  return (
    <Sheet open={open} onClose={onClose} title="Edit project">
      <div className="space-y-3">
        <Field label="Name"><Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></Field>
        <Field label="Villa number"><Input value={form.villaNumber} onChange={(e) => setForm({ ...form, villaNumber: e.target.value })} /></Field>
        <Field label="Customer">
          <Select value={form.customerId} onChange={(e) => setForm({ ...form, customerId: e.target.value })}>
            <option value="">— none (self-owned) —</option>
            {customers?.items.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </Select>
        </Field>
        <Field label="Project type">
          <Select value={form.projectTypeId} onChange={(e) => setForm({ ...form, projectTypeId: e.target.value })}>
            <option value="">—</option>
            {types?.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
          </Select>
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Estimated cost"><Input inputMode="decimal" value={form.estimatedCost} onChange={(e) => setForm({ ...form, estimatedCost: e.target.value })} /></Field>
          <Field label="Sale value"><Input inputMode="decimal" value={form.contractSaleValue} onChange={(e) => setForm({ ...form, contractSaleValue: e.target.value })} /></Field>
        </div>
        <Field label="Status">
          <Select value={form.status} onChange={(e) => setForm({ ...form, status: e.target.value })}>
            <option value="0">Planned</option><option value="1">Active</option><option value="2">On Hold</option>
            <option value="3">Completed</option><option value="4">Cancelled</option>
          </Select>
        </Field>
        <ErrorText error={err} />
        <Button className="w-full" onClick={save} disabled={busy || !form.name}>Save</Button>
      </div>
    </Sheet>
  );
}

// ---- Expenses tab -----------------------------------------------------
function Expenses({ projectId }: { projectId: string }) {
  const canCreate = useAuth((s) => s.can("expense.create"));
  const [open, setOpen] = useState(false);
  const { data, loading, error, reload } = useAsync(
    () => api<Paged<ProjectExpense>>("/expenses", { query: { projectId, pageSize: 100 } }),
    [projectId],
  );

  return (
    <div className="space-y-2">
      {canCreate && <Button className="w-full" onClick={() => setOpen(true)}>+ Add expense</Button>}
      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
        (data?.items.length ?? 0) === 0 ? <EmptyState title="No expenses" /> : data!.items.map((e) => (
          <Card key={e.id} className="flex items-center justify-between">
            <div className="min-w-0">
              <div className="truncate text-sm font-semibold">{e.expenseHeadName}{e.expenseSubheadName ? ` · ${e.expenseSubheadName}` : ""}</div>
              <div className="truncate text-xs text-text-dim">
                {dateStr(e.date)} · {ExpenseTypeName[e.expenseType]} · {e.description || e.txnNumber}
              </div>
            </div>
            <div className={`text-right text-sm tabular-nums ${e.status === 5 ? "text-text-dim line-through" : ""}`}>{money(e.amount)}</div>
          </Card>
        ))
      )}
      <AddExpenseSheet projectId={projectId} open={open} onClose={() => setOpen(false)} onSaved={() => { setOpen(false); reload(); }} />
    </div>
  );
}

function AddExpenseSheet({ projectId, open, onClose, onSaved }: { projectId: string; open: boolean; onClose: () => void; onSaved: () => void }) {
  const { data: heads } = useAsync(() => api<Lookup[]>("/expense-heads"), []);
  const { data: methods } = useAsync(() => api<Lookup[]>("/payment-methods"), []);
  const [form, setForm] = useState({ headId: "", amount: "", description: "", type: "4", methodId: "" });
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  async function save() {
    setBusy(true); setErr(null);
    try {
      await api("/expenses", {
        method: "POST",
        body: {
          projectId, date: new Date().toISOString().slice(0, 10), expenseHeadId: form.headId,
          description: form.description || null, amount: Number(form.amount), expenseType: Number(form.type),
          paymentStatus: 2, paymentMethodId: form.methodId || null,
        },
      });
      onSaved();
    } catch (e) { setErr(e as ApiError); } finally { setBusy(false); }
  }

  return (
    <Sheet open={open} onClose={onClose} title="Add expense">
      <div className="space-y-3">
        <Field label="Expense head">
          <Select value={form.headId} onChange={(e) => setForm({ ...form, headId: e.target.value })}>
            <option value="">Select…</option>
            {heads?.map((h) => <option key={h.id} value={h.id}>{h.name}</option>)}
          </Select>
        </Field>
        <Field label="Type">
          <Select value={form.type} onChange={(e) => setForm({ ...form, type: e.target.value })}>
            <option value="4">Direct</option><option value="5">Transport</option>
            <option value="6">Machinery</option><option value="7">Other</option><option value="1">Material (direct)</option>
          </Select>
        </Field>
        <Field label="Amount"><Input inputMode="decimal" value={form.amount} onChange={(e) => setForm({ ...form, amount: e.target.value })} /></Field>
        <Field label="Description"><Input value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></Field>
        <Field label="Paid via">
          <Select value={form.methodId} onChange={(e) => setForm({ ...form, methodId: e.target.value })}>
            <option value="">—</option>
            {methods?.map((m) => <option key={m.id} value={m.id}>{m.name}</option>)}
          </Select>
        </Field>
        <ErrorText error={err} />
        <Button className="w-full" onClick={save} disabled={busy || !form.headId || !Number(form.amount)}>Save expense</Button>
      </div>
    </Sheet>
  );
}

// ---- Labour tab -----------------------------------------------------
function Labour({ projectId }: { projectId: string }) {
  const canCreate = useAuth((s) => s.can("labour.create"));
  const [open, setOpen] = useState(false);
  const { data, loading, error, reload } = useAsync(
    () => api<Paged<LabourEntry>>("/labour", { query: { projectId, pageSize: 100 } }),
    [projectId],
  );

  async function submit(lid: string) {
    await api(`/labour/${lid}/submit`, { method: "POST" });
    reload();
  }

  return (
    <div className="space-y-2">
      {canCreate && <Button className="w-full" onClick={() => setOpen(true)}>+ Add labour cost</Button>}
      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
        (data?.items.length ?? 0) === 0 ? <EmptyState title="No labour entries" /> : data!.items.map((l) => (
          <Card key={l.id} className="flex items-center justify-between">
            <div className="min-w-0">
              <div className="flex items-center gap-2">
                <span className="truncate text-sm font-semibold">{l.labourCategoryName}</span>
                <Chip tone={l.status === 6 ? "ok" : l.status === 4 ? "danger" : l.status === 2 ? "brand" : "neutral"}>
                  {TxnStatusName[l.status]}
                </Chip>
              </div>
              <div className="text-xs text-text-dim">{dateStr(l.periodEnd)} · {l.txnNumber}</div>
            </div>
            <div className="flex items-center gap-2">
              <span className="text-sm tabular-nums">{money(l.amount)}</span>
              {l.status === 0 && canCreate && <Button variant="ghost" onClick={() => submit(l.id)}>Submit</Button>}
            </div>
          </Card>
        ))
      )}
      <AddLabourSheet projectId={projectId} open={open} onClose={() => setOpen(false)} onSaved={() => { setOpen(false); reload(); }} />
    </div>
  );
}

function AddLabourSheet({ projectId, open, onClose, onSaved }: { projectId: string; open: boolean; onClose: () => void; onSaved: () => void }) {
  const { data: cats } = useAsync(() => api<Lookup[]>("/labour-categories"), []);
  const [form, setForm] = useState({ categoryId: "", amount: "", remarks: "" });
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);
  const today = new Date().toISOString().slice(0, 10);

  async function save(submit: boolean) {
    setBusy(true); setErr(null);
    try {
      const created = await api<LabourEntry>("/labour", {
        method: "POST",
        body: {
          projectId, labourCategoryId: form.categoryId, periodType: 1,
          periodStart: today, periodEnd: today, amount: Number(form.amount),
          paymentType: "Daily", remarks: form.remarks || null,
        },
      });
      if (submit) await api(`/labour/${created.id}/submit`, { method: "POST" });
      onSaved();
    } catch (e) { setErr(e as ApiError); } finally { setBusy(false); }
  }

  return (
    <Sheet open={open} onClose={onClose} title="Add labour cost">
      <div className="space-y-3">
        <Field label="Labour category">
          <Select value={form.categoryId} onChange={(e) => setForm({ ...form, categoryId: e.target.value })}>
            <option value="">Select…</option>
            {cats?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </Select>
        </Field>
        <Field label="Amount (for the day)"><Input inputMode="decimal" value={form.amount} onChange={(e) => setForm({ ...form, amount: e.target.value })} /></Field>
        <Field label="Remarks"><Input value={form.remarks} onChange={(e) => setForm({ ...form, remarks: e.target.value })} /></Field>
        <ErrorText error={err} />
        <div className="flex gap-2">
          <Button variant="ghost" className="flex-1" onClick={() => save(false)} disabled={busy || !form.categoryId || !Number(form.amount)}>Draft</Button>
          <Button className="flex-1" onClick={() => save(true)} disabled={busy || !form.categoryId || !Number(form.amount)}>Submit</Button>
        </div>
      </div>
    </Sheet>
  );
}

// ---- Contracts tab -------------------------------------------------
function Contracts({ projectId }: { projectId: string }) {
  const canManage = useAuth((s) => s.can("contract.manage"));
  const [open, setOpen] = useState(false);
  const { data, loading, error, reload } = useAsync(
    () => api<Paged<ContractWork>>("/contracts", { query: { projectId, pageSize: 100 } }),
    [projectId],
  );

  return (
    <div className="space-y-2">
      {canManage && <Button className="w-full" onClick={() => setOpen(true)}>+ New contract</Button>}
      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
        (data?.items.length ?? 0) === 0 ? <EmptyState title="No contracts" /> : data!.items.map((c) => (
          <Card key={c.id} className="space-y-1">
            <div className="flex items-center justify-between">
              <span className="text-sm font-semibold">{c.workCategory}</span>
              <Chip tone={c.workStatus === 2 ? "ok" : c.workStatus === 1 ? "brand" : "neutral"}>{ContractStatusName[c.workStatus]}</Chip>
            </div>
            <div className="text-xs text-text-dim">{c.contractorName}</div>
            <div className="flex justify-between text-xs">
              <span className="text-text-dim">Contract {money(c.contractAmount)}</span>
              <span>Paid {money(c.totalPaid)} · <span className="text-warn">Bal {money(c.balance)}</span></span>
            </div>
          </Card>
        ))
      )}
      <NewContractSheet projectId={projectId} open={open} onClose={() => setOpen(false)} onSaved={() => { setOpen(false); reload(); }} />
    </div>
  );
}

function NewContractSheet({ projectId, open, onClose, onSaved }: { projectId: string; open: boolean; onClose: () => void; onSaved: () => void }) {
  const { data: contractors } = useAsync(() => api<Paged<Contractor>>("/contractors", { query: { pageSize: 200, active: true } }), []);
  const [form, setForm] = useState({ contractorId: "", workCategory: "", contractAmount: "", estimatedCost: "" });
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  async function save() {
    setBusy(true); setErr(null);
    try {
      await api("/contracts", {
        method: "POST",
        body: {
          projectId, contractorId: form.contractorId, workCategory: form.workCategory,
          contractAmount: Number(form.contractAmount), estimatedCost: Number(form.estimatedCost || 0), workStatus: 1,
        },
      });
      onSaved();
    } catch (e) { setErr(e as ApiError); } finally { setBusy(false); }
  }

  return (
    <Sheet open={open} onClose={onClose} title="New contract">
      <div className="space-y-3">
        <Field label="Contractor">
          <Select value={form.contractorId} onChange={(e) => setForm({ ...form, contractorId: e.target.value })}>
            <option value="">Select…</option>
            {contractors?.items.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </Select>
        </Field>
        <Field label="Work category"><Input value={form.workCategory} onChange={(e) => setForm({ ...form, workCategory: e.target.value })} placeholder="Plumbing" /></Field>
        <Field label="Contract amount"><Input inputMode="decimal" value={form.contractAmount} onChange={(e) => setForm({ ...form, contractAmount: e.target.value })} /></Field>
        <Field label="Estimated cost"><Input inputMode="decimal" value={form.estimatedCost} onChange={(e) => setForm({ ...form, estimatedCost: e.target.value })} /></Field>
        <ErrorText error={err} />
        <Button className="w-full" onClick={save} disabled={busy || !form.contractorId || !form.workCategory || !Number(form.contractAmount)}>Create</Button>
      </div>
    </Sheet>
  );
}

// ---- Payments tab (contractor) ------------------------------------
function Payments({ projectId }: { projectId: string }) {
  const canCreate = useAuth((s) => s.can("contractor_payment.create"));
  const [open, setOpen] = useState(false);
  const { data, loading, error, reload } = useAsync(
    () => api<Paged<ContractorPayment>>("/contractor-payments", { query: { projectId, pageSize: 100 } }),
    [projectId],
  );

  async function submit(pid: string) {
    await api(`/contractor-payments/${pid}/submit`, { method: "POST" });
    reload();
  }

  return (
    <div className="space-y-2">
      {canCreate && <Button className="w-full" onClick={() => setOpen(true)}>+ Contractor payment</Button>}
      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
        (data?.items.length ?? 0) === 0 ? <EmptyState title="No payments" /> : data!.items.map((pay) => (
          <Card key={pay.id} className="flex items-center justify-between">
            <div className="min-w-0">
              <div className="flex items-center gap-2">
                <span className="truncate text-sm font-semibold">{pay.contractorName}</span>
                <Chip tone={pay.status === 6 ? "ok" : pay.status === 4 ? "danger" : pay.status === 2 ? "brand" : "neutral"}>
                  {TxnStatusName[pay.status]}
                </Chip>
              </div>
              <div className="text-xs text-text-dim">{dateStr(pay.date)} · {pay.txnNumber}</div>
            </div>
            <div className="flex items-center gap-2">
              <span className="text-sm tabular-nums">{money(pay.amount)}</span>
              {pay.status === 0 && canCreate && <Button variant="ghost" onClick={() => submit(pay.id)}>Submit</Button>}
            </div>
          </Card>
        ))
      )}
      <NewPaymentSheet projectId={projectId} open={open} onClose={() => setOpen(false)} onSaved={() => { setOpen(false); reload(); }} />
    </div>
  );
}

function NewPaymentSheet({ projectId, open, onClose, onSaved }: { projectId: string; open: boolean; onClose: () => void; onSaved: () => void }) {
  const { data: contractors } = useAsync(() => api<Paged<Contractor>>("/contractors", { query: { pageSize: 200, active: true } }), []);
  const { data: contracts } = useAsync(() => api<Paged<ContractWork>>("/contracts", { query: { projectId, pageSize: 100 } }), [projectId]);
  const { data: methods } = useAsync(() => api<Lookup[]>("/payment-methods"), []);
  const [form, setForm] = useState({ contractorId: "", contractWorkId: "", amount: "", methodId: "", kind: "2", reference: "" });
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  async function save(submit: boolean) {
    setBusy(true); setErr(null);
    try {
      const created = await api<ContractorPayment>("/contractor-payments", {
        method: "POST",
        body: {
          contractorId: form.contractorId, projectId,
          contractWorkId: form.contractWorkId || null, date: new Date().toISOString().slice(0, 10),
          amount: Number(form.amount), paymentMethodId: form.methodId,
          referenceNumber: form.reference || null, paymentKind: Number(form.kind),
        },
      });
      if (submit) await api(`/contractor-payments/${created.id}/submit`, { method: "POST" });
      onSaved();
    } catch (e) { setErr(e as ApiError); } finally { setBusy(false); }
  }

  const workOptions = (contracts?.items ?? []).filter((w) => !form.contractorId || w.contractorId === form.contractorId);

  return (
    <Sheet open={open} onClose={onClose} title="Contractor payment">
      <div className="space-y-3">
        <Field label="Contractor">
          <Select value={form.contractorId} onChange={(e) => setForm({ ...form, contractorId: e.target.value, contractWorkId: "" })}>
            <option value="">Select…</option>
            {contractors?.items.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </Select>
        </Field>
        <Field label="Against contract (optional)">
          <Select value={form.contractWorkId} onChange={(e) => setForm({ ...form, contractWorkId: e.target.value })}>
            <option value="">— none —</option>
            {workOptions.map((w) => <option key={w.id} value={w.id}>{w.workCategory} · bal {money(w.balance)}</option>)}
          </Select>
        </Field>
        <Field label="Amount"><Input inputMode="decimal" value={form.amount} onChange={(e) => setForm({ ...form, amount: e.target.value })} /></Field>
        <Field label="Kind">
          <Select value={form.kind} onChange={(e) => setForm({ ...form, kind: e.target.value })}>
            <option value="1">Advance</option><option value="2">Partial</option>
            <option value="3">Final</option><option value="4">Adjustment</option>
          </Select>
        </Field>
        <Field label="Paid via">
          <Select value={form.methodId} onChange={(e) => setForm({ ...form, methodId: e.target.value })}>
            <option value="">Select…</option>
            {methods?.map((m) => <option key={m.id} value={m.id}>{m.name}</option>)}
          </Select>
        </Field>
        <Field label="Reference"><Input value={form.reference} onChange={(e) => setForm({ ...form, reference: e.target.value })} /></Field>
        <ErrorText error={err} />
        <div className="flex gap-2">
          <Button variant="ghost" className="flex-1" onClick={() => save(false)} disabled={busy || !form.contractorId || !form.methodId || !Number(form.amount)}>Draft</Button>
          <Button className="flex-1" onClick={() => save(true)} disabled={busy || !form.contractorId || !form.methodId || !Number(form.amount)}>Submit</Button>
        </div>
      </div>
    </Sheet>
  );
}
