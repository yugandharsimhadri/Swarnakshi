import { useState } from "react";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { money, dateStr } from "@/lib/format";
import {
  Button, Card, EmptyState, ErrorText, Field, Input, LabelRow, Select, Sheet,
  Spinner
} from "@/components/ui";
import { type CustomerPayment, type Lookup, type Paged, type ProjectSummary } from "@/lib/types";

/**
 * What the customer has been billed and has paid, and the one action that changes it.
 *
 * A tab per feature, each owning the sheets it opens: the five tabs used to share one 800-line
 * file, which meant a change to how a receipt is recorded sat beside the code for issuing cement
 * and every edit risked the wrong screen.
 */
export default function CustomerTab({ projectId, hasCustomer, summary }: { projectId: string; hasCustomer: boolean; summary: ProjectSummary }) {
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
        <LabelRow label="Sale value" value={money(summary.contractSaleValue)} />
        <LabelRow label="Received" value={money(summary.customerReceived)} />
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

