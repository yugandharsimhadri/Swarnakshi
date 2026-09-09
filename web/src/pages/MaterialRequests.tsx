import { useEffect, useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { num, money, dateStr } from "@/lib/format";
import { Button, Card, Chip, Confirm, EmptyState, ErrorText, Field, Input, PageHeader, Select, Sheet, SkeletonList, Spinner } from "@/components/ui";
import { AttachmentPanel } from "@/components/AttachmentPanel";
import { MaterialPicker } from "@/components/MaterialPicker";
import { MatReqStatusName, type InventoryBalance, type Material, type MaterialRequest, type Paged, type Project } from "@/lib/types";

const statusTone = (s: number) =>
  s === 4 || s === 7 ? "danger" : s === 5 ? "ok" : s === 3 || s === 6 ? "brand" : "neutral";

export function MaterialRequestList() {
  const canCreate = useAuth((s) => s.can("material_request.create"));
  const { data, loading, error } = useAsync(
    () => api<Paged<MaterialRequest>>("/material-requests", { query: { pageSize: 100 } }),
    [],
  );

  return (
    <div className="space-y-3">
      <PageHeader
        title="Material Requests"
        action={canCreate && <Link to="/inventory/requests/new"><Button>+ New</Button></Link>}
      />
      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
        (data?.items.length ?? 0) === 0 ? <EmptyState title="No requests" /> : (
          <div className="space-y-2">
            {data!.items.map((r) => (
              <Link key={r.id} to={`/inventory/requests/${r.id}`}>
                <Card className="flex items-center justify-between">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="truncate text-sm font-semibold">{r.projectName}</span>
                      <Chip tone={statusTone(r.requestStatus)}>{MatReqStatusName[r.requestStatus]}</Chip>
                    </div>
                    <div className="truncate text-xs text-text-dim">
                      {r.txnNumber} · {dateStr(r.date)} · {r.items.length} item(s)
                    </div>
                  </div>
                </Card>
              </Link>
            ))}
          </div>
        )
      )}
    </div>
  );
}

export function NewMaterialRequest() {
  const nav = useNavigate();
  const { data: projects } = useAsync(() => api<Paged<Project>>("/projects", { query: { pageSize: 100 } }), []);
  // Arrived from a villa's Material tab? Then the villa is already decided — don't ask again.
  const [params] = useSearchParams();
  const [projectId, setProjectId] = useState(params.get("projectId") ?? "");
  const [rows, setRows] = useState<{ material: Material | null; qty: string }[]>([{ material: null, qty: "" }]);
  const [notes, setNotes] = useState("");
  const [error, setError] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  const setRow = (i: number, k: "qty", v: string) =>
    setRows(rows.map((r, idx) => (idx === i ? { ...r, [k]: v } : r)));
  const setMaterial = (i: number, m: Material | null) =>
    setRows(rows.map((r, idx) => (idx === i ? { ...r, material: m } : r)));

  // What the chosen villa's site actually holds. Fetched once for the whole site rather than per
  // material: the list is small, and one request keeps every row's answer consistent with the rest.
  const siteId = projects?.items.find((p) => p.id === projectId)?.siteId ?? "";
  const { data: stock } = useAsync(
    () => (siteId ? api<InventoryBalance[]>("/inventory", { query: { siteId } }) : Promise.resolve([])),
    [siteId],
  );
  const inStore = (materialId: string) => stock?.find((b) => b.materialId === materialId);

  // Summed per material, because two rows for the same material each fit inside the balance while
  // together they do not — and that is what a long request written stage by stage looks like.
  const askedFor = (materialId: string) =>
    rows.filter((r) => r.material?.id === materialId).reduce((sum, r) => sum + (Number(r.qty) || 0), 0);

  const shortOf = (r: { material: Material | null; qty: string }) => {
    if (!r.material || !siteId || !stock) return null;
    const have = inStore(r.material.id)?.quantity ?? 0;
    const want = askedFor(r.material.id);
    return want > have ? { have, want } : null;
  };

  const anyShort = rows.some((r) => shortOf(r) !== null);

  async function save(submit: boolean) {
    setBusy(true);
    setError(null);
    try {
      const items = rows
        .filter((r) => r.material && Number(r.qty) > 0)
        .map((r) => ({ materialId: r.material!.id, unitId: r.material!.unitId, requestedQty: Number(r.qty) }));
      if (!projectId || items.length === 0) throw { message: "Pick a project and at least one material.", errors: [], status: 400 };
      const created = await api<MaterialRequest>("/material-requests", {
        method: "POST",
        body: { projectId, requestType: 1, date: new Date().toISOString().slice(0, 10), notes: notes.trim() || null, items },
      });
      if (submit) await api(`/material-requests/${created.id}/submit`, { method: "POST" });
      nav(`/inventory/requests/${created.id}`);
    } catch (e) {
      setError(e as ApiError);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-3">
      <PageHeader
        title="Take from store"
        back={params.get("projectId") ? `/projects/${params.get("projectId")}` : "/inventory/requests"}
      />

      <Field label="Project">
        <Select value={projectId} onChange={(e) => setProjectId(e.target.value)}>
          <option value="">Select a project…</option>
          {projects?.items.map((p) => <option key={p.id} value={p.id}>{p.name} · {p.siteName}</option>)}
        </Select>
      </Field>

      <div className="space-y-2">
        {rows.map((r, i) => {
          const balance = r.material ? inStore(r.material.id) : undefined;
          const short = shortOf(r);
          return (
            <Card key={i} className="space-y-2">
              <MaterialPicker value={r.material} onChange={(m) => setMaterial(i, m)} />
              <div className="flex gap-2">
                <Input placeholder="Quantity" inputMode="decimal" value={r.qty} onChange={(e) => setRow(i, "qty", e.target.value)} />
                {rows.length > 1 && (
                  <Button variant="ghost" onClick={() => setRows(rows.filter((_, idx) => idx !== i))}>✕</Button>
                )}
              </div>

              {/* What the store holds, shown as soon as a material is picked rather than after the
                  quantity is typed — the number you need in order to type the right quantity is no
                  use as an error afterwards. */}
              {r.material && siteId && (
                short ? (
                  <div className="text-xs font-medium text-danger">
                    Only {num(short.have)} {r.material.unitCode} in store, and this request asks for {num(short.want)}.
                    Reduce it, or buy the material instead of taking it from the store.
                  </div>
                ) : (
                  <div className="text-xs text-text-dim">
                    In store: <span className="tabular-nums text-text">{num(balance?.quantity ?? 0)} {r.material.unitCode}</span>
                    {balance && balance.quantity > 0 && ` · ${money(balance.averageRate, true)} each`}
                  </div>
                )
              )}
            </Card>
          );
        })}
        <Button variant="ghost" className="w-full" onClick={() => setRows([...rows, { material: null, qty: "" }])}>
          + Add material
        </Button>
      </div>

      <Field label="Remarks">
        <Input value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="What it is for — slab, plastering…" />
      </Field>

      <ErrorText error={error} />
      {anyShort && (
        <div role="status" className="rounded-xl bg-danger/10 px-3 py-2 text-xs text-danger">
          The store cannot cover this request. A villa can only be charged for material that
          actually left the shelf, so this has to come down to what is there — or be bought.
        </div>
      )}
      <div className="flex gap-2">
        <Button variant="ghost" className="flex-1" onClick={() => save(false)} disabled={busy || anyShort}>Save draft</Button>
        <Button className="flex-1" onClick={() => save(true)} disabled={busy || anyShort}>Submit for approval</Button>
      </div>
    </div>
  );
}

export function MaterialRequestDetail() {
  const { id } = useParams<{ id: string }>();
  const canIssue = useAuth((s) => s.can("material_request.create"));
  const { data, loading, error, reload } = useAsync(() => api<MaterialRequest>(`/material-requests/${id}`), [id]);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<ApiError | null>(null);
  const [pendingAct, setPendingAct] = useState<"issue" | "cancel" | null>(null);

  if (loading) return <SkeletonList />;
  if (error || !data) return <ErrorText error={error} />;

  const canSubmit = data.requestStatus === 0;
  const canDoIssue = (data.requestStatus === 3 || data.requestStatus === 6) && canIssue;

  async function act(path: string, body?: unknown) {
    setPendingAct(null);
    setBusy(true);
    setActionError(null);
    try {
      await api(`/material-requests/${id}/${path}`, { method: "POST", body: body ?? {} });
      reload();
    } catch (e) {
      setActionError(e as ApiError);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title={data.projectName}
        back="/inventory/requests"
        subtitle={`${data.txnNumber} · ${data.siteName} · ${dateStr(data.date)}`}
        action={<Chip tone={statusTone(data.requestStatus)}>{MatReqStatusName[data.requestStatus]}</Chip>}
      />
      {data.notes && <div className="-mt-2 px-1 text-xs text-text-dim">“{data.notes}”</div>}

      <div className="space-y-2">
        {data.items.map((it) => (
          <Card key={it.id} className="flex items-center justify-between">
            <div className="text-sm">{it.materialName}</div>
            <div className="text-right text-xs text-text-dim">
              <div>req {num(it.requestedQty)} {it.unitCode}</div>
              {it.approvedQty != null && <div>appr {num(it.approvedQty)}</div>}
              {it.issuedQty > 0 && <div className="text-ok">issued {num(it.issuedQty)}</div>}
            </div>
          </Card>
        ))}
      </div>

      <ErrorText error={actionError} />
      <div className="flex gap-2">
        {canSubmit && <Button className="flex-1" onClick={() => act("submit")} disabled={busy}>Submit for approval</Button>}
        {canDoIssue && <Button className="flex-1" onClick={() => setPendingAct("issue")} disabled={busy}>Issue from stock</Button>}
        {data.requestStatus < 3 && (
          <Button variant="ghost" onClick={() => setPendingAct("cancel")} disabled={busy}>Cancel</Button>
        )}
      </div>
      {data.requestStatus === 2 && (
        <p className="px-1 text-xs text-text-dim">Waiting for Owner approval in the Approval Center.</p>
      )}

      <div className="pt-2"><AttachmentPanel entityType="MaterialRequest" entityId={data.id} canEdit={canIssue} /></div>

      <IssueSheet
        open={pendingAct === "issue"}
        defaultDate={data.date.slice(0, 10)}
        busy={busy}
        onClose={() => setPendingAct(null)}
        onIssue={(date) => act("issue", { items: null, date })}
      />

      <Confirm
        open={pendingAct === "cancel"}
        title="Cancel this request?"
        body="The request will be cancelled and cannot be reopened."
        confirmLabel="Cancel request"
        danger
        onConfirm={() => act("cancel")}
        onCancel={() => setPendingAct(null)}
      />
    </div>
  );
}

/**
 * Issuing asks for a date, because the store and the office are rarely in step. A supervisor types
 * up Tuesday's issue on Saturday; without a date the cost lands on Saturday, and material issued on
 * the 31st but entered on the 2nd falls into the wrong month. Defaults to the request's own date,
 * which is right far more often than "today" is.
 */
function IssueSheet({ open, defaultDate, busy, onClose, onIssue }: {
  open: boolean;
  defaultDate: string;
  busy: boolean;
  onClose: () => void;
  onIssue: (date: string) => void;
}) {
  const [date, setDate] = useState(defaultDate);
  useEffect(() => { if (open) setDate(defaultDate); }, [open, defaultDate]);

  return (
    <Sheet open={open} onClose={onClose} title="Issue material from stock">
      <div className="space-y-3">
        <p className="text-sm text-text-dim">
          Stock leaves the site inventory and is booked to the villa at the current weighted-average rate.
        </p>
        <Field label="Date the material left the store">
          <Input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
        </Field>
        <div className="flex gap-2">
          <Button variant="ghost" className="flex-1" onClick={onClose} disabled={busy}>Cancel</Button>
          <Button className="flex-1" onClick={() => onIssue(date)} disabled={busy || !date}>
            {busy ? "Issuing…" : "Issue"}
          </Button>
        </div>
      </div>
    </Sheet>
  );
}
