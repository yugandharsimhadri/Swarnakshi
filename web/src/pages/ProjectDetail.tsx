import { Link, useParams } from "react-router-dom";
import { api } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { money, moneyShort } from "@/lib/format";
import { Card, Chip, ErrorText, Spinner, StatCard } from "@/components/ui";
import { ProjectStatusName, type Project, type ProjectSummary } from "@/lib/types";

export default function ProjectDetail() {
  const { id } = useParams<{ id: string }>();
  const { data, loading, error } = useAsync(async () => {
    const [project, summary] = await Promise.all([
      api<Project>(`/projects/${id}`),
      api<ProjectSummary>(`/projects/${id}/summary`),
    ]);
    return { project, summary };
  }, [id]);

  if (loading) return <Spinner />;
  if (error || !data) return <ErrorText error={error} />;

  const { project: p, summary: s } = data;
  const costRows: [string, number][] = [
    ["Material", s.materialCost],
    ["Labour", s.labourCost],
    ["Contractor", s.contractorCost],
    ["Other", s.otherCost],
  ];

  return (
    <div className="space-y-4">
      <Link to="/projects" className="text-xs text-text-dim">← Projects</Link>

      <div>
        <div className="flex items-center gap-2">
          <h1 className="text-lg font-bold">{p.name}</h1>
          <Chip tone={p.status === 1 ? "ok" : "neutral"}>{ProjectStatusName[p.status]}</Chip>
        </div>
        <div className="text-xs text-text-dim">{p.code} · {p.siteName}{p.customerName ? ` · ${p.customerName}` : ""}</div>
      </div>

      <div className="grid grid-cols-2 gap-3">
        <StatCard label="Total cost" value={moneyShort(s.totalCost)} />
        <StatCard label="Estimated" value={moneyShort(s.estimatedCost)} />
        <StatCard
          label="Budget variance"
          value={moneyShort(s.budgetVariance)}
          tone={s.budgetVariance < 0 ? "danger" : "ok"}
        />
        <StatCard
          label="Margin"
          value={s.margin === null ? "—" : moneyShort(s.margin)}
          tone={s.margin != null && s.margin < 0 ? "danger" : "ok"}
        />
      </div>

      <Card>
        <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-text-dim">Cost by type</div>
        <div className="space-y-1.5">
          {costRows.map(([label, val]) => (
            <div key={label} className="flex justify-between text-sm">
              <span className="text-text-dim">{label}</span>
              <span className="tabular-nums">{money(val)}</span>
            </div>
          ))}
          <div className="mt-1 flex justify-between border-t border-border pt-1.5 text-sm font-semibold">
            <span>Total</span><span className="tabular-nums">{money(s.totalCost)}</span>
          </div>
        </div>
      </Card>

      <Card>
        <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-text-dim">Customer</div>
        <div className="space-y-1.5 text-sm">
          <Row label="Sale value" value={money(s.contractSaleValue)} />
          <Row label="Received" value={money(s.customerReceived)} />
          <Row label="Outstanding" value={money(s.customerOutstanding)} />
        </div>
      </Card>

      <p className="px-1 text-xs text-text-dim">
        Expenses, materials, labour, contracts &amp; payments tabs arrive with P1–P3.
      </p>
    </div>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between">
      <span className="text-text-dim">{label}</span>
      <span className="tabular-nums">{value}</span>
    </div>
  );
}
