import { Link } from "react-router-dom";
import { api } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { moneyShort, num, dateStr } from "@/lib/format";
import { Card, Chip, EmptyState, PageHeader, Spinner, StatCard } from "@/components/ui";
import { IconChevron } from "@/components/icons";
import type { DashboardPayload } from "@/lib/types";

export default function Dashboard() {
  const user = useAuth((s) => s.user);
  const { data, loading } = useAsync(() => api<DashboardPayload>("/dashboard"), []);

  if (loading || !data) return <Spinner />;

  return (
    <div className="space-y-4">
      <PageHeader title={`Hi, ${user?.name ?? ""}`} />

      {data.pendingApprovals > 0 && (
        <Link to="/approvals">
          <Card className="flex items-center justify-between border-brand/40 bg-brand/10">
            <div>
              <div className="text-sm font-semibold text-brand-ink">Approvals waiting</div>
              <div className="text-xs text-text-dim">Review material requests, purchases &amp; payments</div>
            </div>
            <Chip tone="brand">{data.pendingApprovals}</Chip>
          </Card>
        </Link>
      )}

      <div className="grid grid-cols-2 gap-3">
        {data.kpis.map((k) => (
          <StatCard
            key={k.label}
            label={k.label}
            value={k.format === "money" ? moneyShort(k.value) : num(k.value)}
          />
        ))}
      </div>

      <Link to="/reports" className="block">
        <Card className="flex items-center justify-between">
          <span className="text-sm font-semibold">Reports</span>
          <IconChevron size={16} className="text-text-dim" />
        </Card>
      </Link>

      <div>
        <div className="mb-2 px-1 text-xs font-semibold uppercase tracking-wide text-text-dim">Recent activity</div>
        {data.recent.length === 0 ? (
          <EmptyState title="Nothing yet" hint="Transactions will show here as they're posted." />
        ) : (
          <div className="space-y-2">
            {data.recent.map((r, i) => (
              <Card key={i} className="flex items-center justify-between">
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <Chip>{r.type}</Chip>
                    <span className="truncate text-xs text-text-dim">{r.context ?? r.ref}</span>
                  </div>
                  <div className="text-xs text-text-dim">{dateStr(r.date)} · {r.ref}</div>
                </div>
                <div className="text-right text-sm tabular-nums">{moneyShort(r.amount)}</div>
              </Card>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
