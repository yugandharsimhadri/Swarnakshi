import { useState } from "react";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { money, dateStr, today } from "@/lib/format";
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
  const [editing, setEditing] = useState<ContractorPayment | null>(null);
  const [deleting, setDeleting] = useState<ContractorPayment | null>(null);

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
          {payments!.items.map((pay) => {
            const unposted = pay.status === 0 || pay.status === 4;
            const canChange = canPay && (unposted || (pay.status === 6 && pay.pendingChange == null));
            return (
              <Card key={pay.id} className="space-y-2">
                <div className="flex items-center justify-between gap-2">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="truncate text-sm font-semibold">{pay.contractorName}</span>
                      <Chip tone={pay.status === 6 ? "ok" : pay.status === 4 ? "danger" : pay.status === 2 ? "brand" : "neutral"}>
                        {TxnStatusName[pay.status]}
                      </Chip>
                    </div>
                    <div className="text-xs text-text-dim">{dateStr(pay.date)} · {pay.txnNumber}</div>
                  </div>
                  <span className="shrink-0 text-sm tabular-nums">{money(pay.amount)}</span>
                </div>
                {pay.pendingChange != null && (
                  <div className="text-xs text-warn">
                    {pay.pendingChange === 2 ? "Delete" : "Change"} waiting for approval. The figures stay as they are until then.
                  </div>
                )}
                {canChange && (
                  <div className="flex gap-2">
                    <Button variant="ghost" className="flex-1" onClick={() => setEditing(pay)}>Edit</Button>
                    <Button variant="ghost" className="flex-1" onClick={() => setDeleting(pay)}>Delete</Button>
                    {unposted && (
                      <Button variant="ghost" className="flex-1"
                        onClick={() => api(`/contractor-payments/${pay.id}/submit`, { method: "POST" }).then(refresh)}>
                        Submit
                      </Button>
                    )}
                  </div>
                )}
              </Card>
            );
          })}
        </>
      )}

      <NewContractSheet projectId={projectId} open={workOpen} onClose={() => setWorkOpen(false)}
        onSaved={() => { setWorkOpen(false); refresh(); }} />
      {(payOpen || editing) && (
        <PaymentSheet projectId={projectId} existing={editing ?? undefined}
          onClose={() => { setPayOpen(false); setEditing(null); }}
          onSaved={() => { setPayOpen(false); setEditing(null); refresh(); }} />
      )}
      {deleting && (
        <DeletePaymentSheet payment={deleting} onClose={() => setDeleting(null)}
          onSaved={() => { setDeleting(null); refresh(); }} />
      )}
    </div>
  );
}

function NewContractSheet({ projectId, open, onClose, onSaved }: { projectId: string; open: boolean; onClose: () => void; onSaved: () => void }) {
  const { data: contractors } = useAsync(() => api<Paged<Contractor>>("/contractors", { query: { pageSize: 200, active: true } }), []);
  const [form, setForm] = useState({ contractorId: "", workCategory: "", contractAmount: "", startDate: today() });
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  async function save() {
    setBusy(true); setErr(null);
    try {
      await api("/contracts", {
        method: "POST",
        body: {
          projectId, contractorId: form.contractorId, workCategory: form.workCategory,
          contractAmount: Number(form.contractAmount), workStatus: 1,
          startDate: form.startDate || null,
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
        <Field label="Start date"><Input type="date" value={form.startDate} onChange={(e) => setForm({ ...form, startDate: e.target.value })} /></Field>
        <ErrorText error={err} />
        <Button className="w-full" onClick={save} disabled={busy || !form.contractorId || !form.workCategory || !Number(form.contractAmount)}>Create</Button>
      </div>
    </Sheet>
  );
}

/**
 * Records a payment, or corrects one.
 *
 * One form for three jobs, and only the buttons differ. New: save or send. A draft or one the Owner
 * sent back: nothing has moved yet, so it is corrected directly. Posted: money has moved, so the form
 * asks why and sends the correction for approval instead of saving it.
 */
function PaymentSheet({ projectId, existing, onClose, onSaved }: {
  projectId: string; existing?: ContractorPayment; onClose: () => void; onSaved: () => void;
}) {
  const posted = existing?.status === 6;
  const { data: contractors } = useAsync(() => api<Paged<Contractor>>("/contractors", { query: { pageSize: 200, active: true } }), []);
  const { data: contracts } = useAsync(() => api<Paged<ContractWork>>("/contracts", { query: { projectId, pageSize: 100 } }), [projectId]);
  const { data: methods } = useAsync(() => api<Lookup[]>("/payment-methods"), []);
  // Today, but editable. A payment is often entered the morning after it was handed over, and a
  // date nobody can correct quietly turns every late entry into a wrong one.
  const [form, setForm] = useState({
    contractorId: existing?.contractorId ?? "",
    contractWorkId: existing?.contractWorkId ?? "",
    amount: existing ? String(existing.amount) : "",
    methodId: existing?.paymentMethodId ?? "",
    kind: String(existing?.paymentKind ?? 2),
    reference: existing?.referenceNumber ?? "",
    date: existing?.date?.slice(0, 10) ?? today(),
    reason: "",
  });
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  async function save(submit: boolean) {
    setBusy(true); setErr(null);
    try {
      const body = {
        contractorId: form.contractorId, projectId,
        contractWorkId: form.contractWorkId || null, date: form.date,
        amount: Number(form.amount), paymentMethodId: form.methodId,
        referenceNumber: form.reference || null, description: existing?.description ?? null,
        paymentKind: Number(form.kind),
      };
      if (posted) {
        await api(`/contractor-payments/${existing!.id}/change-request`, {
          method: "POST", body: { payment: body, reason: form.reason },
        });
      } else {
        const saved = existing
          ? await api<ContractorPayment>(`/contractor-payments/${existing.id}`, { method: "PUT", body })
          : await api<ContractorPayment>("/contractor-payments", { method: "POST", body });
        if (submit) await api(`/contractor-payments/${saved.id}/submit`, { method: "POST" });
      }
      onSaved();
    } catch (e) { setErr(e as ApiError); } finally { setBusy(false); }
  }

  const workOptions = (contracts?.items ?? []).filter((w) => !form.contractorId || w.contractorId === form.contractorId);
  const incomplete = !form.contractorId || !form.methodId || !Number(form.amount) || !form.date;

  return (
    <Sheet open onClose={onClose} title={existing ? `Edit ${existing.txnNumber}` : "Contractor payment"}>
      <div className="space-y-3">
        <Field label="Contractor">
          {existing ? <Input value={existing.contractorName} disabled /> : (
            <Select value={form.contractorId} onChange={(e) => setForm({ ...form, contractorId: e.target.value, contractWorkId: "" })}>
              <option value="">Select…</option>
              {contractors?.items.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </Select>
          )}
        </Field>
        <Field label="Against contract (optional)">
          <Select value={form.contractWorkId} onChange={(e) => setForm({ ...form, contractWorkId: e.target.value })}>
            <option value="">— none —</option>
            {workOptions.map((w) => <option key={w.id} value={w.id}>{w.workCategory} · bal {money(w.balance)}</option>)}
          </Select>
        </Field>
        <Field label="Amount"><Input inputMode="decimal" value={form.amount} onChange={(e) => setForm({ ...form, amount: e.target.value })} /></Field>
        <Field label="Date paid">
          <Input type="date" value={form.date} onChange={(e) => setForm({ ...form, date: e.target.value })} />
        </Field>
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
        {posted && (
          <>
            <Field label="Why is this changing?">
              <Input value={form.reason} maxLength={500} onChange={(e) => setForm({ ...form, reason: e.target.value })} placeholder="Typed 30,000, paid 25,000" />
            </Field>
            <div className="rounded-xl bg-surface-2 px-3 py-2 text-xs text-text-dim">
              The Owner approves this first. Until then the payment, the contract balance and the
              villa's cost stay as they are.
            </div>
          </>
        )}
        <ErrorText error={err} />
        {posted ? (
          <Button className="w-full" onClick={() => save(false)} disabled={busy || incomplete || !form.reason.trim()}>Send for approval</Button>
        ) : (
          <div className="flex gap-2">
            <Button variant="ghost" className="flex-1" onClick={() => save(false)} disabled={busy || incomplete}>{existing ? "Save" : "Draft"}</Button>
            <Button className="flex-1" onClick={() => save(true)} disabled={busy || incomplete}>Submit</Button>
          </div>
        )}
      </div>
    </Sheet>
  );
}

/**
 * Removes a payment. A posted one only goes once the Owner agrees; anything earlier never moved any
 * money, so it is simply withdrawn. Neither is erased: the row stays, marked Cancelled.
 */
function DeletePaymentSheet({ payment, onClose, onSaved }: { payment: ContractorPayment; onClose: () => void; onSaved: () => void }) {
  const posted = payment.status === 6;
  const [reason, setReason] = useState("");
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  async function confirm() {
    setBusy(true); setErr(null);
    try {
      await api(`/contractor-payments/${payment.id}/${posted ? "delete-request" : "cancel"}`, { method: "POST", body: { reason } });
      onSaved();
    } catch (e) { setErr(e as ApiError); } finally { setBusy(false); }
  }

  return (
    <Sheet open onClose={onClose} title={`Delete ${payment.txnNumber}`}>
      <div className="space-y-3">
        <div className="text-sm">{payment.contractorName} · {money(payment.amount)} · {dateStr(payment.date)}</div>
        <Field label="Why?">
          <Input value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} placeholder="Entered twice" />
        </Field>
        {posted && (
          <div className="rounded-xl bg-surface-2 px-3 py-2 text-xs text-text-dim">
            The Owner approves this first. Until then the payment still counts towards the contract
            and the villa's cost.
          </div>
        )}
        <ErrorText error={err} />
        <Button variant="danger" className="w-full" onClick={confirm} disabled={busy || !reason.trim()}>
          {posted ? "Send for approval" : "Delete"}
        </Button>
      </div>
    </Sheet>
  );
}
