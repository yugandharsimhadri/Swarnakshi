import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { money, moneyShort, num, dateStr } from "@/lib/format";
import {
  Button, Card, Chip, EmptyState, ErrorText, Field, Input, PageHeader, ProgressBar, Select, Sheet,
  Spinner, StatCard,
} from "@/components/ui";
import { IconDelivery, IconIssue } from "@/components/icons";
import {
  ContractStatusName, ExpenseTypeName, InvTxnTypeName, ProjectStatusName, TxnStatusName,
  type CostByHead, type Contractor, type ContractWork, type ContractorPayment, type Customer,
  type CustomerPayment, type InventoryTxn, type LabourEntry, type Lookup, type Paged, type Project,
  type ProjectExpense, type ProjectSummary,
} from "@/lib/types";

/**
 * A villa has exactly three things people record against it, and the tabs say so out loud:
 * material that arrived, money spent, and work given to a contractor. Overview and Customer
 * are for reading, not entering.
 */
type Tab = "overview" | "material" | "expenses" | "contractors" | "customer";

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
    ["overview", "Overview"], ["material", "Material"], ["expenses", "Expenses"],
    ["contractors", "Contractors"], ["customer", "Customer"],
  ];

  return (
    <div className="space-y-4">
      <PageHeader
        title={p.name}
        back="/projects"
        subtitle={`${p.siteName}${p.customerName ? ` · ${p.customerName}` : ""}`}
        action={
          <div className="flex shrink-0 items-center gap-2">
            <Chip tone={p.status === 1 ? "ok" : "neutral"}>{ProjectStatusName[p.status] ?? p.status}</Chip>
            {canManage && <Button variant="ghost" onClick={() => setEditing(true)}>Edit</Button>}
          </div>
        }
      />
      {(p.status === 1 || p.status === 2) && (
        <Card><ProgressBar percent={p.completionPercent} /></Card>
      )}

      <Alerts s={s} />

      <EditProjectSheet project={p} open={editing} onClose={() => setEditing(false)} onSaved={() => { setEditing(false); reload(); }} />

      <div className="grid grid-cols-2 gap-3">
        <StatCard label="Spent so far" value={moneyShort(s.totalCost)}
          sub={s.committedContractorCost > 0 ? `+ ${moneyShort(s.committedContractorCost)} committed` : undefined} />
        <StatCard label="Estimated" value={moneyShort(s.estimatedCost)} />
        {/* Burn beats budget variance on an unfinished villa: variance shows a large positive that
            reads as money saved when it is really a house that is not finished. */}
        <StatCard
          label="Spend vs progress"
          value={s.burnPercent == null ? "—" : `${s.burnPercent}%`}
          sub={s.burnPercent == null ? "not started yet" : `of ${moneyShort(s.estimatedCost * s.completionPercent / 100)} expected`}
          tone={s.burnPercent == null ? undefined : s.burnPercent > 110 ? "danger" : s.burnPercent > 100 ? "warn" : "ok"}
        />
        <StatCard
          label="Earned margin"
          value={s.earnedMargin == null ? "unsold" : moneyShort(s.earnedMargin)}
          sub={s.earnedRevenue == null ? undefined : `on ${moneyShort(s.earnedRevenue)} earned`}
          tone={s.earnedMargin == null ? undefined : s.earnedMargin < 0 ? "danger" : "ok"}
        />
      </div>

      <div className="flex gap-1 overflow-x-auto rounded-xl bg-surface-2 p-1">
        {tabs.map(([t, label]) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`min-h-11 whitespace-nowrap rounded-lg px-3 py-1.5 text-xs font-semibold ${tab === t ? "bg-surface text-text" : "text-text-dim"}`}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === "overview" && <Overview projectId={p.id} s={s} />}
      {tab === "material" && <MaterialTab projectId={p.id} />}
      {tab === "expenses" && <Expenses projectId={p.id} />}
      {tab === "contractors" && <ContractorsTab projectId={p.id} />}
      {tab === "customer" && <CustomerTab projectId={p.id} hasCustomer={!!p.customerId} summary={s} />}
    </div>
  );
}

/**
 * The two things about a villa that need somebody to do something today. Both were derivable from
 * numbers already on the screen and neither was ever said out loud, which is the same as not being
 * there — an owner scanning ten villas reads chips, not arithmetic.
 */
function Alerts({ s }: { s: ProjectSummary }) {
  const overBudget = s.burnPercent != null && s.burnPercent > 100;
  if (!s.duesOnHandover && !overBudget) return null;

  return (
    <div className="space-y-2">
      {s.duesOnHandover && (
        <Card className="border-danger/40 bg-danger/5">
          <div className="text-sm font-semibold text-danger">Handed over, still owed {money(s.customerOutstanding)}</div>
          <div className="mt-0.5 text-xs text-text-dim">
            The villa is complete and the customer has not paid in full.
          </div>
        </Card>
      )}
      {overBudget && (
        <Card className={s.burnPercent! > 110 ? "border-danger/40 bg-danger/5" : "border-warn/40 bg-warn/5"}>
          <div className={`text-sm font-semibold ${s.burnPercent! > 110 ? "text-danger" : "text-warn"}`}>
            {/* Not "% of budget spent": the figure compares spend against the spend expected by
                this stage, so a villa 10% built that has used 11% of its budget reads 110%. The
                old wording said 110% of the whole budget was gone, which is ten times the truth. */}
            Spending {s.burnPercent}% of what {s.completionPercent}% built should have cost
          </div>
          <div className="mt-0.5 text-xs text-text-dim">
            {moneyShort(s.totalCost)} spent against {moneyShort(s.estimatedCost * s.completionPercent / 100)} expected by this stage.
          </div>
        </Card>
      )}
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
  const [form, setForm] = useState({ amount: "", methodId: "", reference: "", description: "" });
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
          description: form.description || null,
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
        <Field label="Remarks"><Input value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} placeholder="Part payment, cheque handed to…" /></Field>
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
    completionPercent: String(project.completionPercent ?? 0),
  });
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  async function save() {
    setBusy(true); setErr(null);
    try {
      await api(`/projects/${project.id}`, {
        method: "PUT",
        body: {
          name: form.name, villaNumber: form.villaNumber || null, siteId: project.siteId,
          customerId: form.customerId || null, projectTypeId: form.projectTypeId || null,
          estimatedCost: Number(form.estimatedCost || 0),
          contractSaleValue: form.contractSaleValue ? Number(form.contractSaleValue) : null,
          status: Number(form.status),
          completionPercent: Number(form.completionPercent || 0),
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
          <Select
            value={form.status}
            onChange={(e) => {
              const status = e.target.value;
              // Keep the two honest as they are edited, rather than letting the server correct it
              // after the fact: finishing a project means 100%, and one not started means 0.
              const completionPercent =
                status === "3" ? "100" : status === "0" ? "0" : form.completionPercent;
              setForm({ ...form, status, completionPercent });
            }}
          >
            <option value="0">Planned</option><option value="1">Active</option><option value="2">On Hold</option>
            <option value="3">Completed</option><option value="4">Cancelled</option>
          </Select>
        </Field>
        <Field label="Completion %">
          <Input
            inputMode="numeric"
            value={form.completionPercent}
            disabled={form.status === "0" || form.status === "3"}
            onChange={(e) => setForm({ ...form, completionPercent: e.target.value.replace(/[^0-9]/g, "") })}
          />
        </Field>
        <ProgressBar percent={Number(form.completionPercent || 0)} />
        <ErrorText error={err} />
        <Button className="w-full" onClick={save} disabled={busy || !form.name}>Save</Button>
      </div>
    </Sheet>
  );
}

// ---- Material tab ----------------------------------------------------
/**
 * Entry type one: material that reached this villa. Two ways in, and the screen says which is
 * which in the words a supervisor uses — "take from the store" or "bought for this villa".
 * Both land here afterwards, so the villa's material history is one list regardless of route.
 */
function MaterialTab({ projectId }: { projectId: string }) {
  const canRequest = useAuth((s) => s.can("material_request.create"));
  const canBuy = useAuth((s) => s.can("purchase.create"));
  const { data, loading, error } = useAsync(
    () => api<Paged<InventoryTxn>>("/inventory/transactions", { query: { projectId, pageSize: 100 } }),
    [projectId],
  );

  const rows = data?.items ?? [];
  const totalValue = rows.reduce((sum, t) => sum + Math.abs(t.quantity) * t.rate, 0);

  // What the villa has swallowed, by trade. The categories are few enough now that this is a
  // readable breakdown rather than a second list — and it is the question an owner actually asks
  // of a material list ("where did the money go"), which a chronological ledger never answers.
  const byCategory = Object.entries(
    rows.reduce<Record<string, number>>((acc, t) => {
      acc[t.categoryName] = (acc[t.categoryName] ?? 0) + Math.abs(t.quantity) * t.rate;
      return acc;
    }, {}),
  ).sort((a, b) => b[1] - a[1]);

  return (
    <div className="space-y-2">
      <div className="grid grid-cols-2 gap-2">
        {canRequest && (
          <Link to={`/inventory/requests/new?projectId=${projectId}`}>
            <Card className="flex flex-col items-center gap-1 text-center transition-colors hover:border-brand/40">
              <IconIssue size={26} className="text-brand" />
              <div className="text-sm font-semibold">Take from store</div>
              <div className="text-xs text-text-dim">Issue site stock here</div>
            </Card>
          </Link>
        )}
        {canBuy && (
          <Link to={`/inventory/purchases/new?projectId=${projectId}`}>
            <Card className="flex flex-col items-center gap-1 text-center transition-colors hover:border-brand/40">
              <IconDelivery size={26} className="text-brand" />
              <div className="text-sm font-semibold">Bought for this villa</div>
              <div className="text-xs text-text-dim">Straight from the supplier</div>
            </Card>
          </Link>
        )}
      </div>

      {totalValue > 0 && (
        <Card>
          <div className="flex justify-between text-sm">
            <span className="text-text-dim">Material charged to this villa</span>
            <span className="font-semibold tabular-nums">{money(totalValue)}</span>
          </div>
          <div className="mt-2 space-y-0.5 border-t border-border pt-2">
            {byCategory.map(([category, value]) => (
              <div key={category} className="flex justify-between text-xs">
                <span className="truncate text-text-dim">{category}</span>
                <span className="shrink-0 tabular-nums">{money(value)}</span>
              </div>
            ))}
          </div>
        </Card>
      )}

      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
        (data?.items.length ?? 0) === 0
          ? <EmptyState title="No material yet" hint="Take it from the store, or record a purchase for this villa." />
          : rows.map((t) => (
            <Card key={t.id} className="flex items-center justify-between gap-3">
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <span className="truncate text-sm font-semibold">{t.materialName}</span>
                  <Chip>{t.categoryName}</Chip>
                </div>
                <div className="truncate text-xs text-text-dim">
                  {t.materialTypeName} · {dateStr(t.date)} · {InvTxnTypeName[t.type]} · {t.txnNumber}
                </div>
              </div>
              <div className="shrink-0 text-right text-sm tabular-nums">
                <div>{num(Math.abs(t.quantity))} {t.unitCode}</div>
                <div className="text-xs text-text-dim">{money(Math.abs(t.quantity) * t.rate)}</div>
              </div>
            </Card>
          ))
      )}
    </div>
  );
}

// ---- Expenses tab -----------------------------------------------------
/**
 * Entry type two: money that left for this villa and is not a contractor's bill — a day's labour,
 * a tip to the lorry driver, tea for the crew. Labour used to be a tab of its own; it is an
 * expense with a category, so it is one option in the same form now.
 */
function Expenses({ projectId }: { projectId: string }) {
  const canCreate = useAuth((s) => s.can("expense.create"));
  const canLabour = useAuth((s) => s.can("labour.create"));
  const [open, setOpen] = useState(false);
  const [labourOpen, setLabourOpen] = useState(false);
  const { data, loading, error, reload } = useAsync(
    () => api<Paged<ProjectExpense>>("/expenses", { query: { projectId, pageSize: 100 } }),
    [projectId],
  );
  const { data: labour, reload: reloadLabour } = useAsync(
    () => api<Paged<LabourEntry>>("/labour", { query: { projectId, pageSize: 100 } }),
    [projectId],
  );

  return (
    <div className="space-y-2">
      <div className="flex gap-2">
        {canCreate && <Button className="flex-1" onClick={() => setOpen(true)}>+ Expense</Button>}
        {canLabour && <Button variant="ghost" className="flex-1" onClick={() => setLabourOpen(true)}>+ Labour</Button>}
      </div>

      {(labour?.items.length ?? 0) > 0 && (
        <div className="space-y-2">
          {labour!.items.map((l) => (
            <Card key={l.id} className="flex items-center justify-between">
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <span className="truncate text-sm font-semibold">{l.labourCategoryName}</span>
                  <Chip tone={l.status === 6 ? "ok" : l.status === 4 ? "danger" : l.status === 2 ? "brand" : "neutral"}>
                    {TxnStatusName[l.status]}
                  </Chip>
                </div>
                <div className="text-xs text-text-dim">Labour · {dateStr(l.periodEnd)} · {l.txnNumber}</div>
              </div>
              <div className="flex items-center gap-2">
                <span className="text-sm tabular-nums">{money(l.amount)}</span>
                {l.status === 0 && canLabour && (
                  <Button variant="ghost"
                    onClick={() => api(`/labour/${l.id}/submit`, { method: "POST" }).then(reloadLabour)}>
                    Submit
                  </Button>
                )}
              </div>
            </Card>
          ))}
        </div>
      )}

      <AddLabourSheet projectId={projectId} open={labourOpen} onClose={() => setLabourOpen(false)}
        onSaved={() => { setLabourOpen(false); reloadLabour(); }} />
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

// ---- Contractors tab -------------------------------------------------
/**
 * Entry type three: work handed to a contractor. The work order and what has been paid against it
 * are one thing to the person on site — "we gave Ramesh the plumbing, he's had 40 of the 90" —
 * so they are one tab rather than a Contracts tab and a Payments tab that never line up.
 */
function ContractorsTab({ projectId }: { projectId: string }) {
  const canManage = useAuth((s) => s.can("contract.manage"));
  const canPay = useAuth((s) => s.can("contractor_payment.create"));
  const [workOpen, setWorkOpen] = useState(false);
  const [payOpen, setPayOpen] = useState(false);

  const { data, loading, error, reload } = useAsync(
    () => api<Paged<ContractWork>>("/contracts", { query: { projectId, pageSize: 100 } }),
    [projectId],
  );
  const { data: payments, reload: reloadPayments } = useAsync(
    () => api<Paged<ContractorPayment>>("/contractor-payments", { query: { projectId, pageSize: 100 } }),
    [projectId],
  );

  const refresh = () => { reload(); reloadPayments(); };

  return (
    <div className="space-y-2">
      <div className="flex gap-2">
        {canManage && <Button className="flex-1" onClick={() => setWorkOpen(true)}>+ Work order</Button>}
        {canPay && <Button variant="ghost" className="flex-1" onClick={() => setPayOpen(true)}>+ Payment</Button>}
      </div>

      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
        (data?.items.length ?? 0) === 0 ? <EmptyState title="No work orders" hint="Assign work to a contractor to start tracking it." /> : data!.items.map((c) => (
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

      {(payments?.items.length ?? 0) > 0 && (
        <>
          <div className="px-1 pt-2 text-xs font-semibold uppercase tracking-wide text-text-dim">Payments</div>
          {payments!.items.map((pay) => (
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
                {pay.status === 0 && canPay && (
                  <Button variant="ghost"
                    onClick={() => api(`/contractor-payments/${pay.id}/submit`, { method: "POST" }).then(reloadPayments)}>
                    Submit
                  </Button>
                )}
              </div>
            </Card>
          ))}
        </>
      )}

      <NewContractSheet projectId={projectId} open={workOpen} onClose={() => setWorkOpen(false)}
        onSaved={() => { setWorkOpen(false); refresh(); }} />
      <NewPaymentSheet projectId={projectId} open={payOpen} onClose={() => setPayOpen(false)}
        onSaved={() => { setPayOpen(false); refresh(); }} />
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
