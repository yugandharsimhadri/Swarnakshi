import { useState } from "react";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { money, dateStr } from "@/lib/format";
import {
  Button, Card, Chip, EmptyState, ErrorText, Field, Input, Select, Sheet, Spinner,
} from "@/components/ui";
import { AddOnStatusName, type AddOnRequest, type AddOnSummary, type Lookup } from "@/lib/types";

/**
 * Changes the customer asked for on this villa — an extra point in the kitchen, a different floor,
 * a wall moved.
 *
 * The amounts here are estimates and are shown apart from every cost figure on the screen above,
 * which is deliberate: what a change was expected to come to and what it actually cost are two
 * different numbers, and the second one arrives as ordinary expenses on the Expenses tab. Nothing
 * on this tab is added to the villa's cost.
 */
export default function AddOnsTab({ projectId }: { projectId: string }) {
  const canManage = useAuth((s) => s.can("addon.manage"));
  const [editing, setEditing] = useState<AddOnRequest | null>(null);
  const [open, setOpen] = useState(false);

  const { data, loading, error, reload } = useAsync(
    () => api<AddOnRequest[]>("/add-ons", { query: { projectId } }),
    [projectId],
  );
  const { data: summary, reload: reloadSummary } = useAsync(
    () => api<AddOnSummary>("/add-ons/summary", { query: { projectId } }),
    [projectId],
  );

  const refresh = () => { reload(); reloadSummary(); };

  async function setStatus(a: AddOnRequest, status: number) {
    const remarks = status === 2 ? window.prompt("Why is this change not going ahead?") : null;
    if (status === 2 && !remarks) return;
    await api(`/add-ons/${a.id}/status`, { method: "POST", body: { status, remarks } });
    refresh();
  }

  return (
    <div className="space-y-2">
      {canManage && <Button className="w-full" onClick={() => { setEditing(null); setOpen(true); }}>+ Add-on request</Button>}

      {summary && (summary.requested + summary.agreed + summary.done + summary.declined) > 0 && (
        <Card className="space-y-1">
          <div className="text-xs font-semibold uppercase tracking-wide text-text-dim">Expected, not spent</div>
          <div className="grid grid-cols-3 gap-2 text-center">
            <Figure label="Requested" count={summary.requested} amount={summary.requestedAmount} />
            <Figure label="Agreed" count={summary.agreed} amount={summary.agreedAmount} />
            <Figure label="Done" count={summary.done} amount={summary.doneAmount} />
          </div>
          <div className="pt-1 text-xs text-text-dim">
            Estimates of what the customer asked for. What the work actually costs is on the
            Expenses tab, and only that counts towards this villa.
          </div>
        </Card>
      )}

      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
        (data?.length ?? 0) === 0
          ? <EmptyState title="No changes requested" hint="Extras and modifications the customer asks for are recorded here." />
          : data!.map((a) => (
            <Card key={a.id} className="space-y-2">
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="truncate text-sm font-semibold">{a.expenseHeadName}</span>
                    <Chip tone={a.status === 1 ? "brand" : a.status === 3 ? "ok" : a.status === 2 ? "danger" : "neutral"}>
                      {AddOnStatusName[a.status]}
                    </Chip>
                  </div>
                  <div className="text-xs text-text-dim">{dateStr(a.date)} · {a.txnNumber}</div>
                </div>
                <div className="shrink-0 text-right">
                  <div className="text-sm tabular-nums">{money(a.expectedAmount)}</div>
                  <div className="text-[11px] text-text-dim">expected</div>
                </div>
              </div>

              <div className="whitespace-pre-wrap text-sm">{a.description}</div>
              {a.remarks && <div className="text-xs text-text-dim">{a.remarks}</div>}

              {canManage && (
                <div className="flex flex-wrap gap-2 pt-1">
                  {a.status === 0 && <Button variant="ghost" onClick={() => setStatus(a, 1)}>Agreed</Button>}
                  {a.status === 0 && <Button variant="ghost" onClick={() => setStatus(a, 2)}>Not going ahead</Button>}
                  {a.status === 1 && <Button variant="ghost" onClick={() => setStatus(a, 3)}>Done</Button>}
                  <Button variant="ghost" onClick={() => { setEditing(a); setOpen(true); }}>Edit</Button>
                </div>
              )}
            </Card>
          ))
      )}

      {open && (
        <AddOnSheet
          projectId={projectId}
          existing={editing}
          onClose={() => { setOpen(false); setEditing(null); }}
          onSaved={() => { setOpen(false); setEditing(null); refresh(); }}
        />
      )}
    </div>
  );
}

function Figure({ label, count, amount }: { label: string; count: number; amount: number }) {
  return (
    <div>
      <div className="text-xs text-text-dim">{label}</div>
      <div className="text-sm tabular-nums">{money(amount)}</div>
      <div className="text-[11px] text-text-dim">{count} request{count === 1 ? "" : "s"}</div>
    </div>
  );
}

function AddOnSheet({ projectId, existing, onClose, onSaved }: {
  projectId: string; existing: AddOnRequest | null; onClose: () => void; onSaved: () => void;
}) {
  const { data: heads } = useAsync(() => api<Lookup[]>("/expense-heads"), []);
  const [form, setForm] = useState({
    headId: existing?.expenseHeadId ?? "",
    description: existing?.description ?? "",
    amount: existing ? String(existing.expectedAmount) : "",
    remarks: existing?.remarks ?? "",
  });
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  async function save() {
    setBusy(true); setErr(null);
    try {
      const body = {
        projectId,
        date: existing?.date ?? new Date().toISOString().slice(0, 10),
        expenseHeadId: form.headId,
        description: form.description,
        expectedAmount: Number(form.amount || 0),
        remarks: form.remarks || null,
      };
      if (existing) await api(`/add-ons/${existing.id}`, { method: "PUT", body });
      else await api("/add-ons", { method: "POST", body });
      onSaved();
    } catch (e) { setErr(e as ApiError); } finally { setBusy(false); }
  }

  return (
    <Sheet open onClose={onClose} title={existing ? "Edit request" : "Customer's request"}>
      <div className="space-y-3">
        <Field label="Type of work">
          <Select value={form.headId} onChange={(e) => setForm({ ...form, headId: e.target.value })}>
            <option value="">Select…</option>
            {heads?.map((h) => <option key={h.id} value={h.id}>{h.name}</option>)}
          </Select>
        </Field>
        <Field label="What the customer asked for">
          <textarea
            rows={3}
            value={form.description}
            onChange={(e) => setForm({ ...form, description: e.target.value })}
            placeholder="Two extra power points in the kitchen, one above the counter"
            className="w-full rounded-xl border border-border bg-surface px-3 py-2 text-sm"
          />
        </Field>
        <Field label="Expected amount">
          <Input inputMode="decimal" value={form.amount}
            onChange={(e) => setForm({ ...form, amount: e.target.value })} />
        </Field>
        <Field label="Notes">
          <Input value={form.remarks} onChange={(e) => setForm({ ...form, remarks: e.target.value })} />
        </Field>

        <div className="rounded-xl bg-surface-2 px-3 py-2 text-xs text-text-dim">
          An estimate for the record. The villa is charged by the expenses the work actually takes,
          not by this figure.
        </div>

        <ErrorText error={err} />
        <Button className="w-full" onClick={save} disabled={busy || !form.headId || !form.description.trim()}>
          {busy ? "Saving…" : existing ? "Save changes" : "Record request"}
        </Button>
      </div>
    </Sheet>
  );
}
