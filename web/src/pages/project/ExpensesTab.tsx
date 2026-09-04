import { useState } from "react";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { money, dateStr } from "@/lib/format";
import {
  Button, Card, Chip, EmptyState, ErrorText, Field, Input, Select, Sheet,
  Spinner
} from "@/components/ui";
import {
  ExpenseTypeName, TxnStatusName, type LabourEntry, type Lookup, type Paged,
  type ProjectExpense
} from "@/lib/types";

/** Money spent on the villa that is not material and not a contractor: day labour and sundries. */
export default function Expenses({ projectId }: { projectId: string }) {
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
