import { useMemo, useState } from "react";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { money, dateStr, today } from "@/lib/format";
import {
  Button, Card, Chip, EmptyState, ErrorText, Field, Input, PageHeader, Select, Sheet, Spinner,
} from "@/components/ui";
import type { Claim, ClaimsOwed, Lookup, Paged, Settlement } from "@/lib/types";

/**
 * Money the company owes its own people, and the payments that clear it.
 *
 * One screen for two readers. Somebody with expense.settle sees everyone's claims and can pay them;
 * anybody else sees only their own and can do nothing but read — which is the engineer's "what am I
 * owed?" answered without a second page to build or a second place for the figure to disagree.
 */
export default function Reimbursements() {
  const canSettle = useAuth((s) => s.can("expense.settle"));
  const [settling, setSettling] = useState<ClaimsOwed | null>(null);

  const { data: owed, loading: owedLoading, reload: reloadOwed } = useAsync(
    () => (canSettle ? api<ClaimsOwed[]>("/reimbursements/owed") : Promise.resolve([])),
    [canSettle],
  );
  const { data: claims, loading, error, reload } = useAsync(
    () => api<Claim[]>("/reimbursements/claims"),
    [],
  );
  const { data: settlements, reload: reloadSettlements } = useAsync(
    () => api<Paged<Settlement>>("/reimbursements", { query: { pageSize: 20 } }),
    [],
  );

  const mine = useMemo(() => (claims ?? []).reduce((sum, c) => c.reimbursementStatus === 1 ? sum + c.amount : sum, 0), [claims]);

  function refreshAll() {
    reload(); reloadOwed(); reloadSettlements();
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title={canSettle ? "Reimbursements" : "My claims"}
        subtitle={canSettle ? "What the company owes its people" : "Money you spent and what you are owed"}
      />

      {!canSettle && (
        <Card>
          <div className="text-xs text-text-dim">Owed to you</div>
          <div className="text-2xl tabular-nums">{money(mine)}</div>
        </Card>
      )}

      {canSettle && (
        <div className="space-y-2">
          {owedLoading ? <Spinner /> : (owed?.length ?? 0) === 0
            ? <Card><div className="text-sm text-text-dim">Nothing owed to anybody.</div></Card>
            : owed!.map((o) => (
              <Card key={o.userId} className="flex items-center justify-between gap-2">
                <div className="min-w-0">
                  <div className="truncate text-sm font-semibold">{o.userName}</div>
                  <div className="text-xs text-text-dim">{o.claims} claim{o.claims === 1 ? "" : "s"}</div>
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-sm tabular-nums">{money(o.due)}</span>
                  <Button onClick={() => setSettling(o)}>Settle</Button>
                </div>
              </Card>
            ))}
        </div>
      )}

      <div className="space-y-2">
        <div className="text-xs font-semibold uppercase tracking-wide text-text-dim">Claims</div>
        {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
          (claims?.length ?? 0) === 0 ? <EmptyState title="No claims" hint="Expenses paid out of pocket appear here once the owner approves them." /> :
            claims!.map((c) => (
              <Card key={c.id} className="flex items-center justify-between gap-2">
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="truncate text-sm font-semibold">{c.expenseHeadName}</span>
                    <Chip tone={c.reimbursementStatus === 1 ? "warn" : "ok"}>
                      {c.reimbursementStatus === 1 ? "Due" : "Settled"}
                    </Chip>
                  </div>
                  <div className="truncate text-xs text-text-dim">
                    {c.projectName} · {dateStr(c.date)} · {c.description || c.txnNumber}
                    {canSettle ? ` · ${c.paidByUserName}` : ""}
                  </div>
                </div>
                <span className="text-sm tabular-nums">{money(c.amount)}</span>
              </Card>
            ))
        )}
      </div>

      {(settlements?.items.length ?? 0) > 0 && (
        <div className="space-y-2">
          <div className="text-xs font-semibold uppercase tracking-wide text-text-dim">Payments</div>
          {settlements!.items.map((s) => (
            <SettlementCard key={s.id} settlement={s} canSettle={canSettle} onChanged={refreshAll} />
          ))}
        </div>
      )}

      {settling && (
        <SettleSheet
          person={settling}
          onClose={() => setSettling(null)}
          onSaved={() => { setSettling(null); refreshAll(); }}
        />
      )}
    </div>
  );
}

function SettlementCard({ settlement, canSettle, onChanged }: { settlement: Settlement; canSettle: boolean; onChanged: () => void }) {
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState<ApiError | null>(null);
  const voided = settlement.status === 5;

  async function voidIt() {
    const reason = window.prompt("Why is this payment being voided?");
    if (!reason) return;
    setBusy(true); setErr(null);
    try {
      await api(`/reimbursements/${settlement.id}/void`, { method: "POST", body: { reason } });
      onChanged();
    } catch (e) { setErr(e as ApiError); } finally { setBusy(false); }
  }

  return (
    <Card className="space-y-1">
      <div className="flex items-center justify-between gap-2">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <span className="truncate text-sm font-semibold">{settlement.paidToUserName}</span>
            {voided && <Chip tone="danger">Voided</Chip>}
          </div>
          <div className="truncate text-xs text-text-dim">
            {dateStr(settlement.date)} · {settlement.lines.length} claim{settlement.lines.length === 1 ? "" : "s"}
            {settlement.paymentMethodName ? ` · ${settlement.paymentMethodName}` : ""}
            {settlement.reference ? ` · ${settlement.reference}` : ""}
          </div>
        </div>
        <span className={`text-sm tabular-nums ${voided ? "text-text-dim line-through" : ""}`}>{money(settlement.amount)}</span>
      </div>
      {canSettle && !voided && (
        <Button variant="ghost" className="w-full" disabled={busy} onClick={voidIt}>Void this payment</Button>
      )}
      <ErrorText error={err} />
    </Card>
  );
}

/**
 * Pay one person for the claims picked here. Every Due claim starts ticked, because paying somebody
 * everything they are owed is the normal case and unticking one is the exception.
 */
function SettleSheet({ person, onClose, onSaved }: { person: ClaimsOwed; onClose: () => void; onSaved: () => void }) {
  const { data: methods } = useAsync(() => api<Lookup[]>("/payment-methods"), []);
  const { data: claims, loading } = useAsync(
    () => api<Claim[]>("/reimbursements/claims", { query: { status: 1, userId: person.userId } }),
    [person.userId],
  );
  const [picked, setPicked] = useState<Record<string, boolean>>({});
  const [form, setForm] = useState({ methodId: "", reference: "", date: today() });
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  // Unticked only if the user says so: anything not in `picked` is treated as picked.
  const isPicked = (id: string) => picked[id] !== false;
  const chosen = (claims ?? []).filter((c) => isPicked(c.id));
  const total = chosen.reduce((sum, c) => sum + c.amount, 0);

  async function save() {
    setBusy(true); setErr(null);
    try {
      await api("/reimbursements", {
        method: "POST",
        body: {
          paidToUserId: person.userId,
          date: form.date,
          paymentMethodId: form.methodId || null,
          reference: form.reference || null,
          projectExpenseIds: chosen.map((c) => c.id),
        },
      });
      onSaved();
    } catch (e) { setErr(e as ApiError); } finally { setBusy(false); }
  }

  return (
    <Sheet open onClose={onClose} title={`Pay ${person.userName}`}>
      <div className="space-y-3">
        {loading ? <Spinner /> : (claims ?? []).map((c) => (
          <label key={c.id} className="flex items-center justify-between gap-3 rounded-xl border border-border px-3 py-2">
            <div className="flex min-w-0 items-center gap-2">
              <input
                type="checkbox"
                checked={isPicked(c.id)}
                onChange={(e) => setPicked({ ...picked, [c.id]: e.target.checked })}
              />
              <div className="min-w-0">
                <div className="truncate text-sm">{c.expenseHeadName}</div>
                <div className="truncate text-xs text-text-dim">{c.projectName} · {dateStr(c.date)}</div>
              </div>
            </div>
            <span className="text-sm tabular-nums">{money(c.amount)}</span>
          </label>
        ))}

        <Field label="Paid via">
          <Select value={form.methodId} onChange={(e) => setForm({ ...form, methodId: e.target.value })}>
            <option value="">—</option>
            {methods?.map((m) => <option key={m.id} value={m.id}>{m.name}</option>)}
          </Select>
        </Field>
        <Field label="Reference">
          <Input
            value={form.reference}
            placeholder="UPI reference or cheque number"
            onChange={(e) => setForm({ ...form, reference: e.target.value })}
          />
        </Field>
        <Field label="Date paid">
          <Input type="date" value={form.date} onChange={(e) => setForm({ ...form, date: e.target.value })} />
        </Field>

        <div className="flex items-center justify-between rounded-xl bg-surface-2 px-3 py-2">
          <span className="text-sm">Paying</span>
          <span className="text-base tabular-nums">{money(total)}</span>
        </div>

        <ErrorText error={err} />
        <Button className="w-full" disabled={busy || chosen.length === 0} onClick={save}>
          {busy ? "Saving…" : `Pay ${money(total)}`}
        </Button>
      </div>
    </Sheet>
  );
}
