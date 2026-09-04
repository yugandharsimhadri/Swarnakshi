import { Link } from "react-router-dom";
import { api } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { money, num, dateStr } from "@/lib/format";
import { Card, Chip, EmptyState, ErrorText, Spinner } from "@/components/ui";
import { IconDelivery, IconIssue } from "@/components/icons";
import { InvTxnTypeName, type InventoryTxn, type Paged } from "@/lib/types";

/** Every movement of material into this villa, and what each trade has cost so far. */
export default function MaterialTab({ projectId }: { projectId: string }) {
  const canRequest = useAuth((s) => s.can("material_request.create"));
  const canBuy = useAuth((s) => s.can("purchase.create"));
  const { data, loading, error } = useAsync(
    () => api<Paged<InventoryTxn>>("/inventory/transactions", { query: { projectId, pageSize: 100 } }),
    [projectId],
  );

  const rows = data?.items ?? [];
  const totalValue = rows.reduce((sum, t) => sum + Math.abs(t.quantity) * t.rate, 0);

  // What the villa has swallowed, by trade. The categories are few enough now that this is a
  // readable breakdown rather than a second list — and it is the question an owner actually asks
  // of a material list ("where did the money go"), which a chronological ledger never answers.
  const byCategory = Object.entries(
    rows.reduce<Record<string, number>>((acc, t) => {
      acc[t.categoryName] = (acc[t.categoryName] ?? 0) + Math.abs(t.quantity) * t.rate;
      return acc;
    }, {}),
  ).sort((a, b) => b[1] - a[1]);

  return (
    <div className="space-y-2">
      <div className="grid grid-cols-2 gap-2">
        {canRequest && (
          <Link to={`/inventory/requests/new?projectId=${projectId}`}>
            <Card className="flex flex-col items-center gap-1 text-center transition-colors hover:border-brand/40">
              <IconIssue size={26} className="text-brand" />
              <div className="text-sm font-semibold">Take from store</div>
              <div className="text-xs text-text-dim">Issue site stock here</div>
            </Card>
          </Link>
        )}
        {canBuy && (
          <Link to={`/inventory/purchases/new?projectId=${projectId}`}>
            <Card className="flex flex-col items-center gap-1 text-center transition-colors hover:border-brand/40">
              <IconDelivery size={26} className="text-brand" />
              <div className="text-sm font-semibold">Bought for this villa</div>
              <div className="text-xs text-text-dim">Straight from the supplier</div>
            </Card>
          </Link>
        )}
      </div>

      {totalValue > 0 && (
        <Card>
          <div className="flex justify-between text-sm">
            <span className="text-text-dim">Material charged to this villa</span>
            <span className="font-semibold tabular-nums">{money(totalValue)}</span>
          </div>
          <div className="mt-2 space-y-0.5 border-t border-border pt-2">
            {byCategory.map(([category, value]) => (
              <div key={category} className="flex justify-between text-xs">
                <span className="truncate text-text-dim">{category}</span>
                <span className="shrink-0 tabular-nums">{money(value)}</span>
              </div>
            ))}
          </div>
        </Card>
      )}

      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
        (data?.items.length ?? 0) === 0
          ? <EmptyState title="No material yet" hint="Take it from the store, or record a purchase for this villa." />
          : rows.map((t) => (
            <Card key={t.id} className="flex items-center justify-between gap-3">
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <span className="truncate text-sm font-semibold">{t.materialName}</span>
                  <Chip>{t.categoryName}</Chip>
                </div>
                <div className="truncate text-xs text-text-dim">
                  {t.materialTypeName} · {dateStr(t.date)} · {InvTxnTypeName[t.type]} · {t.txnNumber}
                </div>
              </div>
              <div className="shrink-0 text-right text-sm tabular-nums">
                <div>{num(Math.abs(t.quantity))} {t.unitCode}</div>
                <div className="text-xs text-text-dim">{money(Math.abs(t.quantity) * t.rate)}</div>
              </div>
            </Card>
          ))
      )}
    </div>
  );
}

// ---- Expenses tab -----------------------------------------------------
/**
 * Entry type two: money that left for this villa and is not a contractor's bill — a day's labour,
 * a tip to the lorry driver, tea for the crew. Labour used to be a tab of its own; it is an
 * expense with a category, so it is one option in the same form now.
 */
