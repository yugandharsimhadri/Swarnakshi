import { useState } from "react";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { money, dateStr } from "@/lib/format";
import {
  Button, Card, Chip, EmptyState, ErrorText, Field, Input, Select, Sheet,
  Spinner
} from "@/components/ui";
import { AttachmentPanel } from "@/components/AttachmentPanel";
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
  const [sentForApproval, setSentForApproval] = useState(false);
  const [savedDraft, setSavedDraft] = useState(false);
  const { data, loading, error, reload } = useAsync(
    () => api<Paged<ProjectExpense>>("/expenses", { query: { projectId, pageSize: 100 } }),
    [projectId],
  );
  const { data: labour, reload: reloadLabour } = useAsync(
    () => api<Paged<LabourEntry>>("/labour", { query: { projectId, pageSize: 100 } }),
    [projectId],
  );

  // 0 is Draft, and the only drafts here are own-pocket claims waiting for their bill. They are
  // pulled out of the list and shown in their own card below, so each appears exactly once.
  const isUnsentClaim = (e: ProjectExpense) => e.status === 0 && !!e.paidByUserId;
  const drafts = (data?.items ?? []).filter(isUnsentClaim);
  const entries = (data?.items ?? []).filter((e) => !isUnsentClaim(e));

  return (
    <div className="space-y-2">
      <div className="flex gap-2">
        {canCreate && <Button className="flex-1" onClick={() => setOpen(true)}>+ Expense</Button>}
        {canLabour && <Button variant="ghost" className="flex-1" onClick={() => setLabourOpen(true)}>+ Labour</Button>}
      </div>

      {/* Said once, after saving, rather than as standing text on the form: whether an expense
          waits depends on its amount, and warning about approval on every entry would be wrong
          for every entry under the limit. */}
      {sentForApproval && (
        <div role="status" className="rounded-xl bg-warn/10 px-3 py-2 text-xs text-warn">
          Sent to the owner for approval. It counts towards this villa's cost once approved.
        </div>
      )}
      {savedDraft && (
        <div role="status" className="rounded-xl bg-brand/10 px-3 py-2 text-xs text-brand-ink">
          Saved. Attach the bill photo below, then send it to the owner.
        </div>
      )}

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
        entries.length === 0 && drafts.length === 0 ? <EmptyState title="No expenses" /> : entries.map((e) => (
          <Card key={e.id} className="flex items-center justify-between gap-2">
            <div className="min-w-0">
              <div className="flex items-center gap-2">
                <span className="truncate text-sm font-semibold">{e.expenseHeadName}{e.expenseSubheadName ? ` · ${e.expenseSubheadName}` : ""}</span>
                {/* Only the states that mean "this is not a cost yet" get a chip. Posted is the
                    normal case and labelling every row with it would bury the two that matter. */}
                {e.status !== 6 && (
                  <Chip tone={e.status === 2 ? "warn" : e.status === 4 ? "danger" : "neutral"}>
                    {TxnStatusName[e.status]}
                  </Chip>
                )}
                {/* Whether the company still owes somebody for this is worth a chip of its own:
                    the row is a cost either way, but only one of them is also a debt. */}
                {e.reimbursementStatus === 1 && <Chip tone="brand">Owed</Chip>}
                {e.reimbursementStatus === 2 && <Chip tone="ok">Repaid</Chip>}
              </div>
              <div className="truncate text-xs text-text-dim">
                {dateStr(e.date)} · {ExpenseTypeName[e.expenseType]} · {e.description || e.txnNumber}
                {e.paidByUserName ? ` · ${e.paidByUserName} paid` : ""}
              </div>
            </div>
            <div className={`text-right text-sm tabular-nums ${e.status === 5 || e.status === 4 ? "text-text-dim line-through" : e.status === 2 ? "text-text-dim" : ""}`}>{money(e.amount)}</div>
          </Card>
        ))
      )}

      {/* A claim the engineer saved but has not sent up yet. It sits apart from the list above
          because it is the one row on this screen that still needs something doing to it, and
          what it needs — the bill photo — only fits in a card of its own. */}
      {drafts.map((e) => (
        <ClaimDraftCard key={e.id} expense={e} onSent={() => { setSentForApproval(true); reload(); }} />
      ))}
      <AddExpenseSheet
        projectId={projectId}
        open={open}
        onClose={() => setOpen(false)}
        onSaved={(status) => {
          setOpen(false);
          setSentForApproval(status === 2);
          setSavedDraft(status === 0);
          reload();
        }}
      />
    </div>
  );
}

/**
 * An own-pocket claim that still needs its bill. Two steps rather than one because a photo cannot
 * be attached to a row that does not exist yet — and the Send button stays out of reach until
 * there is something for the owner to look at, which is the same rule the server enforces.
 */
function ClaimDraftCard({ expense, onSent }: { expense: ProjectExpense; onSent: () => void }) {
  const [bills, setBills] = useState(0);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<ApiError | null>(null);

  async function send() {
    setBusy(true); setErr(null);
    try {
      await api(`/expenses/${expense.id}/submit`, { method: "POST" });
      onSent();
    } catch (e) { setErr(e as ApiError); } finally { setBusy(false); }
  }

  return (
    <Card className="space-y-3 border-brand/40">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <span className="truncate text-sm font-semibold">{expense.expenseHeadName}</span>
            <Chip tone="neutral">Draft</Chip>
          </div>
          <div className="truncate text-xs text-text-dim">
            You paid · {dateStr(expense.date)} · {expense.description || expense.txnNumber}
          </div>
        </div>
        <span className="text-sm tabular-nums">{money(expense.amount)}</span>
      </div>

      <AttachmentPanel
        entityType="ProjectExpense"
        entityId={expense.id}
        canEdit
        photosOnly
        onChange={setBills}
      />

      <ErrorText error={err} />
      <Button className="w-full" disabled={busy || bills === 0} onClick={send}>
        {bills === 0 ? "Attach the bill first" : busy ? "Sending…" : "Send for approval"}
      </Button>
      <div className="text-xs text-text-dim">
        Claims you paid for always go to the owner, whatever the auto-approve limit is.
      </div>
    </Card>
  );
}

function AddExpenseSheet({ projectId, open, onClose, onSaved }: { projectId: string; open: boolean; onClose: () => void; onSaved: (status: number) => void }) {
  const { data: heads } = useAsync(() => api<Lookup[]>("/expense-heads"), []);
  const { data: methods } = useAsync(() => api<Lookup[]>("/payment-methods"), []);
  const [form, setForm] = useState({ headId: "", amount: "", description: "", type: "4", methodId: "" });
  const [paidByMe, setPaidByMe] = useState(false);
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  async function save() {
    setBusy(true); setErr(null);
    try {
      const created = await api<ProjectExpense>("/expenses", {
        method: "POST",
        body: {
          projectId, date: new Date().toISOString().slice(0, 10), expenseHeadId: form.headId,
          description: form.description || null, amount: Number(form.amount), expenseType: Number(form.type),
          paymentStatus: 2, paymentMethodId: paidByMe ? null : form.methodId || null,
          paidByMe,
        },
      });
      // The saved row says what happened: 0 Draft (a claim awaiting its bill), 2 Pending Approval,
      // or 6 Posted when the owner's limit approved it on the spot. Reading it off the response
      // rather than guessing is what keeps the message on screen true.
      onSaved(created.status);
    } catch (e) { setErr(e as ApiError); } finally { setBusy(false); }
  }

  return (
    <Sheet open={open} onClose={onClose} title="Add expense">
      <div className="space-y-3">
        {/* First question on the form, because it changes what the rest of it means: the payment
            method is the company's if the company paid, and irrelevant if he paid himself. */}
        <Field label="Paid by">
          <div className="grid grid-cols-2 gap-2">
            <button
              type="button"
              onClick={() => setPaidByMe(false)}
              className={`rounded-xl border px-3 py-2 text-xs ${!paidByMe ? "border-brand text-brand-ink" : "border-border text-text-dim"}`}
            >
              Company
            </button>
            <button
              type="button"
              onClick={() => setPaidByMe(true)}
              className={`rounded-xl border px-3 py-2 text-xs ${paidByMe ? "border-brand text-brand-ink" : "border-border text-text-dim"}`}
            >
              I paid — reimburse me
            </button>
          </div>
        </Field>
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
        {!paidByMe && (
          <Field label="Paid via">
            <Select value={form.methodId} onChange={(e) => setForm({ ...form, methodId: e.target.value })}>
              <option value="">—</option>
              {methods?.map((m) => <option key={m.id} value={m.id}>{m.name}</option>)}
            </Select>
          </Field>
        )}
        <ErrorText error={err} />
        <Button className="w-full" onClick={save} disabled={busy || !form.headId || !Number(form.amount)}>
          {paidByMe ? "Save — then attach the bill" : "Save expense"}
        </Button>
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
