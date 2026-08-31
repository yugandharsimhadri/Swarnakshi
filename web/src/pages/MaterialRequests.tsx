import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { num, dateStr } from "@/lib/format";
import { Button, Card, Chip, Confirm, EmptyState, ErrorText, Field, Input, PageHeader, Select, SkeletonList, Spinner } from "@/components/ui";
import { AttachmentPanel } from "@/components/AttachmentPanel";
import { MatReqStatusName, type Material, type MaterialRequest, type Paged, type Project } from "@/lib/types";

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
        action={canCreate && <Link to="/stock/requests/new"><Button>+ New</Button></Link>}
      />
      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
        (data?.items.length ?? 0) === 0 ? <EmptyState title="No requests" /> : (
          <div className="space-y-2">
            {data!.items.map((r) => (
              <Link key={r.id} to={`/stock/requests/${r.id}`}>
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
  const { data: materials } = useAsync(() => api<Paged<Material>>("/materials", { query: { pageSize: 200, active: true } }), []);
  const [projectId, setProjectId] = useState("");
  const [rows, setRows] = useState<{ materialId: string; qty: string }[]>([{ materialId: "", qty: "" }]);
  const [error, setError] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  const setRow = (i: number, k: "materialId" | "qty", v: string) =>
    setRows(rows.map((r, idx) => (idx === i ? { ...r, [k]: v } : r)));

  async function save(submit: boolean) {
    setBusy(true);
    setError(null);
    try {
      const items = rows
        .filter((r) => r.materialId && Number(r.qty) > 0)
        .map((r) => {
          const m = materials!.items.find((x) => x.id === r.materialId)!;
          return { materialId: r.materialId, unitId: m.unitId, requestedQty: Number(r.qty) };
        });
      if (!projectId || items.length === 0) throw { message: "Pick a project and at least one material.", errors: [], status: 400 };
      const created = await api<MaterialRequest>("/material-requests", {
        method: "POST",
        body: { projectId, requestType: 1, date: new Date().toISOString().slice(0, 10), items },
      });
      if (submit) await api(`/material-requests/${created.id}/submit`, { method: "POST" });
      nav(`/stock/requests/${created.id}`);
    } catch (e) {
      setError(e as ApiError);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-3">
      <Link to="/stock/requests" className="text-xs text-text-dim">← Requests</Link>
      <PageHeader title="New request" />

      <Field label="Project">
        <Select value={projectId} onChange={(e) => setProjectId(e.target.value)}>
          <option value="">Select a project…</option>
          {projects?.items.map((p) => <option key={p.id} value={p.id}>{p.name} · {p.siteName}</option>)}
        </Select>
      </Field>

      <div className="space-y-2">
        {rows.map((r, i) => (
          <Card key={i} className="space-y-2">
            <Select value={r.materialId} onChange={(e) => setRow(i, "materialId", e.target.value)}>
              <option value="">Select material…</option>
              {materials?.items.map((m) => <option key={m.id} value={m.id}>{m.name} ({m.unitCode})</option>)}
            </Select>
            <div className="flex gap-2">
              <Input placeholder="Quantity" inputMode="decimal" value={r.qty} onChange={(e) => setRow(i, "qty", e.target.value)} />
              {rows.length > 1 && (
                <Button variant="ghost" onClick={() => setRows(rows.filter((_, idx) => idx !== i))}>✕</Button>
              )}
            </div>
          </Card>
        ))}
        <Button variant="ghost" className="w-full" onClick={() => setRows([...rows, { materialId: "", qty: "" }])}>
          + Add material
        </Button>
      </div>

      <ErrorText error={error} />
      <div className="flex gap-2">
        <Button variant="ghost" className="flex-1" onClick={() => save(false)} disabled={busy}>Save draft</Button>
        <Button className="flex-1" onClick={() => save(true)} disabled={busy}>Submit for approval</Button>
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
      <Link to="/stock/requests" className="text-xs text-text-dim">← Requests</Link>
      <div>
        <div className="flex items-center gap-2">
          <h1 className="text-lg font-bold">{data.projectName}</h1>
          <Chip tone={statusTone(data.requestStatus)}>{MatReqStatusName[data.requestStatus]}</Chip>
        </div>
        <div className="text-xs text-text-dim">{data.txnNumber} · {data.siteName} · {dateStr(data.date)}</div>
      </div>

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

      <Confirm
        open={pendingAct !== null}
        title={pendingAct === "issue" ? "Issue material from stock?" : "Cancel this request?"}
        body={
          pendingAct === "issue"
            ? "Stock leaves the site inventory now and is booked as project consumption at the current weighted-average rate."
            : "The request will be cancelled and cannot be reopened."
        }
        confirmLabel={pendingAct === "issue" ? "Issue" : "Cancel request"}
        danger={pendingAct === "cancel"}
        onConfirm={() => act(pendingAct === "issue" ? "issue" : "cancel", pendingAct === "issue" ? { items: null } : undefined)}
        onCancel={() => setPendingAct(null)}
      />
    </div>
  );
}
