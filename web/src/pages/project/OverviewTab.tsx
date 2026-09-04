import { api } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { money } from "@/lib/format";
import { Card, LabelRow } from "@/components/ui";
import { type CostByHead, type ProjectSummary } from "@/lib/types";

/** The villa's cost broken down by type and by head, and where the customer stands. Read only. */
export default function Overview({ projectId, s }: { projectId: string; s: ProjectSummary }) {
  const { data } = useAsync(() => api<CostByHead[]>("/expenses/cost-by-head", { query: { projectId } }), [projectId]);
  const rows: [string, number][] = [
    ["Material", s.materialCost], ["Labour", s.labourCost], ["Contractor", s.contractorCost], ["Other", s.otherCost],
  ];
  return (
    <div className="space-y-3">
      <Card>
        <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-text-dim">Cost by type</div>
        {rows.map(([l, v]) => (
          <div key={l} className="flex justify-between py-0.5 text-sm">
            <span className="text-text-dim">{l}</span><span className="tabular-nums">{money(v)}</span>
          </div>
        ))}
        <div className="mt-1 flex justify-between border-t border-border pt-1.5 text-sm font-semibold">
          <span>Total</span><span className="tabular-nums">{money(s.totalCost)}</span>
        </div>
      </Card>

      {data && data.length > 0 && (
        <Card>
          <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-text-dim">Cost by head</div>
          {data.map((r) => (
            <div key={r.expenseHeadId} className="flex justify-between py-0.5 text-sm">
              <span className="text-text-dim">{r.expenseHeadName}</span><span className="tabular-nums">{money(r.amount)}</span>
            </div>
          ))}
        </Card>
      )}

      <Card>
        <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-text-dim">Customer</div>
        <LabelRow label="Sale value" value={money(s.contractSaleValue)} />
        <LabelRow label="Received" value={money(s.customerReceived)} />
        <LabelRow label="Outstanding" value={money(s.customerOutstanding)} />
      </Card>
    </div>
  );
}

