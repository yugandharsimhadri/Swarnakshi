import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { money, moneyShort, num, dateStr } from "@/lib/format";
import { Card, Chip, EmptyState, ErrorText, Input, PageHeader, Spinner, StatCard } from "@/components/ui";
import { SitePicker, useSites, lastSite } from "@/components/SitePicker";
import {
  InvTxnTypeName, type InventoryBalance, type InventoryTxn, type MaterialInventoryDetail, type Paged,
} from "@/lib/types";

export function InventoryList() {
  const { data: sites } = useSites();
  const [siteId, setSiteId] = useState(lastSite());
  const [q, setQ] = useState("");
  const [low, setLow] = useState(false);

  useEffect(() => {
    if (!siteId && sites?.items.length) setSiteId(sites.items[0].id);
  }, [sites, siteId]);

  const { data, loading, error } = useAsync(
    () => (siteId ? api<InventoryBalance[]>("/inventory", { query: { siteId, q, lowStock: low } }) : Promise.resolve([])),
    [siteId, q, low],
  );

  const totalValue = (data ?? []).reduce((s, r) => s + r.value, 0);

  return (
    <div className="space-y-3">
      <PageHeader title="Site Inventory" />
      <SitePicker value={siteId} onChange={setSiteId} sites={sites?.items ?? []} />

      {siteId && (
        <>
          <div className="grid grid-cols-2 gap-3">
            <StatCard label="Stock value" value={moneyShort(totalValue)} />
            <StatCard label="Materials" value={String(data?.length ?? 0)} sub={`${(data ?? []).filter((r) => r.lowStock).length} low`} />
          </div>
          <Input placeholder="Search materials…" value={q} onChange={(e) => setQ(e.target.value)} />
          <label className="flex items-center gap-2 px-1 text-xs text-text-dim">
            <input type="checkbox" checked={low} onChange={(e) => setLow(e.target.checked)} /> Low stock only
          </label>

          {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
            (data?.length ?? 0) === 0 ? <EmptyState title="No stock yet" hint="Record opening stock or a purchase." /> : (
              <div className="space-y-2">
                {data!.map((r) => (
                  <Link key={r.materialId} to={`/stock/inventory/${siteId}/${r.materialId}`}>
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
    </div>
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
      <Link to="/stock/inventory" className="text-xs text-text-dim">← Inventory</Link>
      <h1 className="text-lg font-bold">{d.materialName}</h1>

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
