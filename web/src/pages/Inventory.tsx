import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { money, moneyShort, num, dateStr } from "@/lib/format";
import { useAuth } from "@/store/auth";
import {
  Button, Card, Chip, EmptyState, ErrorText, Field, Input, PageHeader, Select, Sheet, Spinner, StatCard,
} from "@/components/ui";
import { SitePicker, useSites, lastSite } from "@/components/SitePicker";
import { IconPurchase, IconRequest } from "@/components/icons";
import {
  InvTxnTypeName, type InventoryBalance, type InventoryTxn, type Material,
  type MaterialInventoryDetail, type Paged,
} from "@/lib/types";

const today = () => new Date().toISOString().slice(0, 10);

/**
 * The store, for one site. Stock on the screen, and one button to put more of it there.
 *
 * Adding stock used to mean raising a purchase — supplier, invoice, tax, discount, submit, approve.
 * That is the right path for a real invoice, and it is still one tap away. But "we have 40 bags in
 * the shed, put them in the system" is a quantity and a cost, and now that is all it asks for.
 */
export function InventoryList() {
  const { data: sites } = useSites();
  const [siteId, setSiteId] = useState(lastSite());
  const [q, setQ] = useState("");
  const [low, setLow] = useState(false);
  const [adding, setAdding] = useState(false);
  const canAdjust = useAuth((s) => s.can("inventory.adjust"));

  useEffect(() => {
    if (!siteId && sites?.items.length) setSiteId(sites.items[0].id);
  }, [sites, siteId]);

  const { data, loading, error, reload } = useAsync(
    () => (siteId ? api<InventoryBalance[]>("/inventory", { query: { siteId, q, lowStock: low } }) : Promise.resolve([])),
    [siteId, q, low],
  );

  const totalValue = (data ?? []).reduce((s, r) => s + r.value, 0);

  return (
    <div className="space-y-3">
      <PageHeader
        title="Inventory"
        action={canAdjust && siteId && <Button onClick={() => setAdding(true)}>+ Add stock</Button>}
      />
      <SitePicker value={siteId} onChange={setSiteId} sites={sites?.items ?? []} />

      {siteId && (
        <>
          <div className="grid grid-cols-2 gap-3">
            <StatCard label="Stock value" value={moneyShort(totalValue)} />
            <StatCard label="Materials" value={String(data?.length ?? 0)}
              sub={`${(data ?? []).filter((r) => r.lowStock).length} low`} />
          </div>

          <div className="grid grid-cols-2 gap-2">
            <Link to="/inventory/purchases">
              <Card className="flex items-center justify-center gap-2 text-sm font-semibold transition-colors hover:border-brand/40">
                <IconPurchase size={18} className="text-brand" /> Purchases
              </Card>
            </Link>
            <Link to="/inventory/requests">
              <Card className="flex items-center justify-center gap-2 text-sm font-semibold transition-colors hover:border-brand/40">
                <IconRequest size={18} className="text-brand" /> Requests
              </Card>
            </Link>
          </div>

          <Input placeholder="Search materials…" value={q} onChange={(e) => setQ(e.target.value)} />
          <label className="flex min-h-11 items-center gap-2 px-1 text-xs text-text-dim">
            <input type="checkbox" className="h-5 w-5 accent-brand" checked={low}
              onChange={(e) => setLow(e.target.checked)} /> Low stock only
          </label>

          {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
            (data?.length ?? 0) === 0
              ? <EmptyState title="No stock yet" hint="Tap “Add stock” to record what is on site." />
              : (
                <div className="space-y-2">
                  {data!.map((r) => (
                    <Link key={r.materialId} to={`/inventory/${siteId}/${r.materialId}`}>
                      <Card className="flex items-center justify-between">
                        <div className="min-w-0">
                          <div className="flex items-center gap-2">
                            <span className="truncate text-sm font-semibold">{r.materialName}</span>
                            {r.lowStock && <Chip tone="warn">Low</Chip>}
                          </div>
                          <div className="truncate text-xs text-text-dim">{r.categoryName}</div>
                        </div>
                        <div className="text-right">
                          <div className="text-sm font-semibold tabular-nums">{num(r.quantity)} {r.unitCode}</div>
                          <div className="text-xs text-text-dim">{money(r.value)}</div>
                        </div>
                      </Card>
                    </Link>
                  ))}
                </div>
              )
          )}
        </>
      )}

      <AddStockSheet
        open={adding}
        siteId={siteId}
        onClose={() => setAdding(false)}
        onSaved={() => { setAdding(false); reload(); }}
      />
    </div>
  );
}

/** Material, how many, what it cost. Nothing else is asked for. */
function AddStockSheet({ open, siteId, onClose, onSaved }: {
  open: boolean; siteId: string; onClose: () => void; onSaved: () => void;
}) {
  const { data: materials } = useAsync(
    () => api<Paged<Material>>("/materials", { query: { pageSize: 500, active: true } }), []);
  const [materialId, setMaterialId] = useState("");
  const [quantity, setQuantity] = useState("");
  const [rate, setRate] = useState("");
  const [date, setDate] = useState(today());
  const [remarks, setRemarks] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  const material = useMemo(
    () => materials?.items.find((m) => m.id === materialId),
    [materials, materialId],
  );
  const total = (Number(quantity) || 0) * (Number(rate) || 0);
  const canSave = Boolean(siteId && materialId && Number(quantity) > 0 && Number(rate) >= 0);

  async function save() {
    setBusy(true);
    setError(null);
    try {
      await api("/inventory/opening-stock", {
        method: "POST",
        body: {
          siteId, materialId,
          quantity: Number(quantity),
          rate: Number(rate),
          date,
          remarks: remarks.trim() || null,
        },
      });
      setMaterialId(""); setQuantity(""); setRate(""); setRemarks("");
      onSaved();
    } catch (e) {
      setError(e as ApiError);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Sheet open={open} onClose={onClose} title="Add stock">
      <div className="space-y-3">
        <Field label="Material">
          <Select value={materialId} onChange={(e) => setMaterialId(e.target.value)}>
            <option value="">Select a material…</option>
            {materials?.items.map((m) => (
              <option key={m.id} value={m.id}>{m.name}{m.brand ? ` · ${m.brand}` : ""}</option>
            ))}
          </Select>
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label={`Quantity${material ? ` (${material.unitCode})` : ""}`}>
            <Input inputMode="decimal" value={quantity} onChange={(e) => setQuantity(e.target.value)} placeholder="100" />
          </Field>
          <Field label="Cost per unit">
            <Input inputMode="decimal" value={rate} onChange={(e) => setRate(e.target.value)} placeholder="450" />
          </Field>
        </div>
        {total > 0 && (
          <div className="rounded-xl bg-surface-2 px-3 py-2 text-sm">
            Total value <span className="float-right font-semibold tabular-nums">{money(total)}</span>
          </div>
        )}
        <Field label="Date"><Input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></Field>
        <Field label="Remarks">
          <Input value={remarks} onChange={(e) => setRemarks(e.target.value)} placeholder="Counted in the shed" />
        </Field>
        <ErrorText error={error} />
        <Button className="w-full" onClick={() => void save()} disabled={busy || !canSave}>
          {busy ? "Saving…" : "Add to stock"}
        </Button>
      </div>
    </Sheet>
  );
}

export function MaterialInventory() {
  const { siteId, materialId } = useParams<{ siteId: string; materialId: string }>();
  const { data, loading, error } = useAsync(async () => {
    const [detail, ledger] = await Promise.all([
      api<MaterialInventoryDetail>(`/inventory/${siteId}/${materialId}`),
      api<Paged<InventoryTxn>>("/inventory/transactions", { query: { siteId, materialId, pageSize: 50 } }),
    ]);
    return { detail, ledger };
  }, [siteId, materialId]);

  if (loading) return <Spinner />;
  if (error || !data) return <ErrorText error={error} />;
  const d = data.detail;

  return (
    <div className="space-y-4">
      <PageHeader title={d.materialName} back="/inventory" />

      <div className="grid grid-cols-2 gap-3">
        <StatCard label="In stock" value={`${num(d.quantity)} ${d.unitCode}`} />
        <StatCard label="Avg rate" value={money(d.averageRate, true)} />
        <StatCard label="Stock value" value={moneyShort(d.value)} />
        <StatCard label="Last purchase" value={d.lastPurchaseRate ? money(d.lastPurchaseRate, true) : "—"} />
        <StatCard label="Purchased" value={`${num(d.totalPurchasedQty)} ${d.unitCode}`} />
        <StatCard label="Consumed" value={`${num(d.totalConsumedQty)} ${d.unitCode}`} />
      </div>

      <div>
        <div className="mb-2 px-1 text-xs font-semibold uppercase tracking-wide text-text-dim">Ledger</div>
        <div className="space-y-2">
          {data.ledger.items.map((t) => (
            <Card key={t.id} className="flex items-center justify-between">
              <div className="min-w-0">
                <div className="flex items-center gap-2 text-sm">
                  <Chip tone={t.quantity >= 0 ? "ok" : "warn"}>{InvTxnTypeName[t.type]}</Chip>
                  <span className="truncate text-text-dim">{t.projectName ?? t.sourceRef ?? t.txnNumber}</span>
                </div>
                <div className="text-xs text-text-dim">{dateStr(t.date)} · {t.txnNumber}</div>
              </div>
              <div className="text-right text-sm tabular-nums">
                <div className={t.quantity >= 0 ? "text-ok" : "text-warn"}>{t.quantity >= 0 ? "+" : ""}{num(t.quantity)}</div>
                <div className="text-xs text-text-dim">@{money(t.rate, true)}</div>
              </div>
            </Card>
          ))}
        </div>
      </div>
    </div>
  );
}
