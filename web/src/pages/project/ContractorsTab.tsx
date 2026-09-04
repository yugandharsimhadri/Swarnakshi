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
  ContractStatusName, TxnStatusName, type Contractor, type ContractWork, type ContractorPayment,
  type Lookup,
  type Paged
} from "@/lib/types";

/** Work orders given to contractors, what has been paid against each, and what is still owed. */
export default function ContractorsTab({ projectId }: { projectId: string }) {
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

