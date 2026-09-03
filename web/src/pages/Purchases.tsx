import { useState } from "react";
import { useEffect } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { money, dateStr } from "@/lib/format";
import { Button, Card, Chip, Confirm, EmptyState, ErrorText, Field, Input, PageHeader, Select, SkeletonList, Spinner } from "@/components/ui";
import { AttachmentPanel } from "@/components/AttachmentPanel";
import { TxnStatusName, type Material, type Paged, type Project, type Purchase, type Site } from "@/lib/types";

interface Supplier { id: string; name: string; code: string }

const stTone = (s: number) => (s === 6 ? "ok" : s === 4 || s === 5 ? "danger" : s === 2 ? "brand" : "neutral");

export function PurchaseList() {
  const canCreate = useAuth((s) => s.can("purchase.create"));
  const { data, loading, error } = useAsync(() => api<Paged<Purchase>>("/purchases", { query: { pageSize: 100 } }), []);

  return (
    <div className="space-y-3">
      <PageHeader title="Purchases" action={canCreate && <Link to="/inventory/purchases/new"><Button>+ New</Button></Link>} />
      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
        (data?.items.length ?? 0) === 0 ? <EmptyState title="No purchases" /> : (
          <div className="space-y-2">
            {data!.items.map((p) => (
              <Link key={p.id} to={`/inventory/purchases/${p.id}`}>
                <Card className="flex items-center justify-between">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="truncate text-sm font-semibold">{p.supplierName}</span>
                      <Chip tone={stTone(p.status)}>{TxnStatusName[p.status]}</Chip>
                    </div>
                    <div className="truncate text-xs text-text-dim">{p.txnNumber} · {p.siteName} · {dateStr(p.date)}</div>
                  </div>
                  <div className="text-right text-xs text-text-dim">{money(p.totalAmount)}</div>
                </Card>
              </Link>
            ))}
          </div>
        )
      )}
    </div>
  );
}

export function NewPurchase() {
  const nav = useNavigate();
  const { data: sites } = useAsync(() => api<Paged<Site>>("/sites", { query: { pageSize: 100 } }), []);
  const { data: suppliers } = useAsync(() => api<Paged<Supplier>>("/suppliers", { query: { pageSize: 200, active: true } }), []);
  const { data: materials } = useAsync(() => api<Paged<Material>>("/materials", { query: { pageSize: 200, active: true } }), []);
  const { data: projects } = useAsync(() => api<Paged<Project>>("/projects", { query: { pageSize: 200 } }), []);

  // Arrived from a villa's Material tab: its site and its name are already known, so the form
  // opens with the destination filled in and every line pointed at that villa.
  const [params] = useSearchParams();
  const forProjectId = params.get("projectId") ?? "";

  const [siteId, setSiteId] = useState("");
  // Typed, not picked: the name resolves on save — an exact (case-insensitive) match reuses that
  // supplier, anything else is created. A builder logging a delivery shouldn't stop to add a master.
  const [supplierName, setSupplierName] = useState("");
  const [invoiceNumber, setInvoice] = useState("");
  const [remarks, setRemarks] = useState("");
  const [rows, setRows] = useState<{ materialId: string; qty: string; rate: string; deliverTo: string }[]>(
    [{ materialId: "", qty: "", rate: "", deliverTo: forProjectId }]);
  const [error, setError] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  const forProject = projects?.items.find((p) => p.id === forProjectId);
  useEffect(() => {
    if (forProject && !siteId) setSiteId(forProject.siteId);
  }, [forProject, siteId]);

  const setRow = (i: number, k: "materialId" | "qty" | "rate" | "deliverTo", v: string) =>
    setRows(rows.map((r, idx) => (idx === i ? { ...r, [k]: v } : r)));
  const total = rows.reduce((s, r) => s + (Number(r.qty) || 0) * (Number(r.rate) || 0), 0);

  function pickMaterial(i: number, id: string) {
    const m = materials?.items.find((x) => x.id === id);
    setRows(rows.map((r, idx) => (idx === i ? { ...r, materialId: id, rate: r.rate || String(m?.defaultPurchaseRate ?? "") } : r)));
  }

  async function save(submit: boolean) {
    setBusy(true);
    setError(null);
    try {
      const items = rows
        .filter((r) => r.materialId && Number(r.qty) > 0)
        .map((r) => {
          const m = materials!.items.find((x) => x.id === r.materialId)!;
          return {
            materialId: r.materialId, unitId: m.unitId, quantity: Number(r.qty),
            rate: Number(r.rate) || 0, discount: 0, taxAmount: 0,
            deliverToProjectId: r.deliverTo || null,
          };
        });
      const typed = supplierName.trim();
      if (!siteId || !typed || items.length === 0) throw { message: "Site, supplier and at least one line are required.", errors: [], status: 400 };
      const matched = suppliers?.items.find((x) => x.name.toLowerCase() === typed.toLowerCase());
      const created = await api<Purchase>("/purchases", {
        method: "POST",
        body: {
          supplierId: matched?.id ?? null,
          supplierName: matched ? null : typed,
          siteId, invoiceNumber: invoiceNumber || null,
          date: new Date().toISOString().slice(0, 10), otherCharges: 0,
          remarks: remarks.trim() || null, items,
        },
      });
      if (submit) await api(`/purchases/${created.id}/submit`, { method: "POST" });
      nav(`/inventory/purchases/${created.id}`);
    } catch (e) {
      setError(e as ApiError);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-3">
      <PageHeader
        title={forProject ? `Buy for ${forProject.name}` : "New purchase"}
        back={forProjectId ? `/projects/${forProjectId}` : "/inventory/purchases"}
      />

      <Field label="Site (stock goes here)">
        <Select value={siteId} onChange={(e) => setSiteId(e.target.value)}>
          <option value="">Select…</option>
          {sites?.items.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
        </Select>
      </Field>
      <Field label="Supplier">
        <Input
          list="supplier-names"
          value={supplierName}
          onChange={(e) => setSupplierName(e.target.value)}
          placeholder="Type a name — new or existing"
          autoCapitalize="words"
        />
        <datalist id="supplier-names">
          {suppliers?.items.map((sup) => <option key={sup.id} value={sup.name} />)}
        </datalist>
        {supplierName.trim() && !suppliers?.items.some((x) => x.name.toLowerCase() === supplierName.trim().toLowerCase()) && (
          <span className="mt-1 block text-xs text-text-dim">New supplier — &ldquo;{supplierName.trim()}&rdquo; will be added.</span>
        )}
      </Field>
      <Field label="Invoice no. (optional)"><Input value={invoiceNumber} onChange={(e) => setInvoice(e.target.value)} /></Field>
      <Field label="Remarks"><Input value={remarks} onChange={(e) => setRemarks(e.target.value)} placeholder="Vehicle number, who received it…" /></Field>

      <div className="space-y-2">
        {rows.map((r, i) => (
          <Card key={i} className="space-y-2">
            <Select value={r.materialId} onChange={(e) => pickMaterial(i, e.target.value)}>
              <option value="">Select material…</option>
              {materials?.items.map((m) => <option key={m.id} value={m.id}>{m.name} ({m.unitCode})</option>)}
            </Select>
            <div className="flex gap-2">
              <Input placeholder="Qty" inputMode="decimal" value={r.qty} onChange={(e) => setRow(i, "qty", e.target.value)} />
              <Input placeholder="Rate" inputMode="decimal" value={r.rate} onChange={(e) => setRow(i, "rate", e.target.value)} />

              {rows.length > 1 && <Button variant="ghost" onClick={() => setRows(rows.filter((_, idx) => idx !== i))}>✕</Button>}
            </div>
            <Select
              value={r.deliverTo}
              onChange={(e) => setRow(i, "deliverTo", e.target.value)}
              disabled={!siteId}
            >
              <option value="">Into site stock</option>
              {(projects?.items ?? [])
                .filter((pr) => pr.siteId === siteId)
                .map((pr) => <option key={pr.id} value={pr.id}>Straight to {pr.name}</option>)}
            </Select>
            {r.deliverTo && (
              <p className="text-xs text-text-dim">
                Booked into this site's stock and issued to the villa in one step, so the ledger shows
                both moves and the villa is charged {money((Number(r.qty) || 0) * (Number(r.rate) || 0))}.
              </p>
            )}
          </Card>
        ))}
        <Button variant="ghost" className="w-full" onClick={() => setRows([...rows, { materialId: "", qty: "", rate: "", deliverTo: forProjectId }])}>
          + Add line
        </Button>
      </div>

      <div className="flex justify-between px-1 text-sm font-semibold">
        <span>Total</span><span className="tabular-nums">{money(total)}</span>
      </div>
      <ErrorText error={error} />
      <div className="flex gap-2">
        <Button variant="ghost" className="flex-1" onClick={() => save(false)} disabled={busy}>Save draft</Button>
        <Button className="flex-1" onClick={() => save(true)} disabled={busy}>Save &amp; post</Button>
      </div>
    </div>
  );
}

export function PurchaseDetail() {
  const { id } = useParams<{ id: string }>();
  const canCreate = useAuth((s) => s.can("purchase.create"));
  const { data, loading, error, reload } = useAsync(() => api<Purchase>(`/purchases/${id}`), [id]);
  const [busy, setBusy] = useState(false);
  const [pay, setPay] = useState("");
  const [actErr, setActErr] = useState<ApiError | null>(null);
  const [confirmPost, setConfirmPost] = useState(false);

  if (loading) return <SkeletonList />;
  if (error || !data) return <ErrorText error={error} />;

  async function submit() {
    setConfirmPost(false);
    setBusy(true); setActErr(null);
    try { await api(`/purchases/${id}/submit`, { method: "POST" }); reload(); }
    catch (e) { setActErr(e as ApiError); } finally { setBusy(false); }
  }
  async function addPayment() {
    setBusy(true); setActErr(null);
    try {
      await api(`/purchases/${id}/payments`, { method: "POST", body: { amount: Number(pay), date: new Date().toISOString().slice(0, 10) } });
      setPay(""); reload();
    } catch (e) { setActErr(e as ApiError); } finally { setBusy(false); }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title={data.supplierName}
        back="/inventory/purchases"
        subtitle={`${data.txnNumber} · ${data.siteName} · ${dateStr(data.date)}`}
        action={<Chip tone={stTone(data.status)}>{TxnStatusName[data.status]}</Chip>}
      />

      <div className="space-y-2">
        {data.items.map((it) => (
          <Card key={it.id} className="flex items-center justify-between text-sm">
            <span>
              {it.materialName}
              {it.deliverToProjectName && (
                <span className="block text-xs text-brand-ink">→ {it.deliverToProjectName}</span>
              )}
            </span>
            <span className="text-text-dim tabular-nums">{it.quantity} {it.unitCode} × {money(it.rate, true)} = {money(it.lineTotal)}</span>
          </Card>
        ))}
      </div>

      <Card className="space-y-1.5 text-sm">
        <Row label="Total" value={money(data.totalAmount)} />
        <Row label="Paid" value={money(data.paidAmount)} />
        <Row label="Balance" value={money(data.balanceAmount)} />
      </Card>

      <ErrorText error={actErr} />
      {data.status === 0 && canCreate && (
        <Button className="w-full" onClick={() => setConfirmPost(true)} disabled={busy}>Submit / post</Button>
      )}
      {data.status === 6 && data.balanceAmount > 0 && canCreate && (
        <div className="flex gap-2">
          <Input placeholder="Payment amount" inputMode="decimal" value={pay} onChange={(e) => setPay(e.target.value)} />
          <Button onClick={addPayment} disabled={busy || !Number(pay)}>Pay</Button>
        </div>
      )}

      <div className="pt-2"><AttachmentPanel entityType="Purchase" entityId={data.id} canEdit={canCreate} /></div>

      <Confirm
        open={confirmPost}
        title="Post this purchase?"
        body={`${data.items.length} line(s), ${money(data.totalAmount)} — stock enters ${data.siteName} inventory at the landed rate and cannot be un-posted.`}
        confirmLabel="Post"
        onConfirm={submit}
        onCancel={() => setConfirmPost(false)}
      />
    </div>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return <div className="flex justify-between"><span className="text-text-dim">{label}</span><span className="tabular-nums">{value}</span></div>;
}
