import { Link } from "react-router-dom";
import { api } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { moneyShort, dateStr } from "@/lib/format";
import { Card, Chip, EmptyState, PageHeader, SkeletonList } from "@/components/ui";
import { MatReqStatusName, type MaterialRequest, type Paged, type ProjectExpense } from "@/lib/types";

const statusTone = (s: number) =>
  s === 4 || s === 7 ? "danger" : s === 5 ? "ok" : s === 3 || s === 6 ? "brand" : "neutral";

/**
 * Where material and money actually move day to day: request stock for a villa, get it approved,
 * issue it, and record what a villa spent. Second only to the dashboard in how often it is opened,
 * which is why it sits on the tab bar rather than three taps into a menu.
 */
export default function Movement() {
  const canRequest = useAuth((s) => s.can("material_request.create"));
  const canSpend = useAuth((s) => s.can("expense.create"));

  const { data, loading } = useAsync(async () => {
    const [requests, expenses] = await Promise.all([
      api<Paged<MaterialRequest>>("/material-requests", { query: { pageSize: 8 } }),
      api<Paged<ProjectExpense>>("/expenses", { query: { pageSize: 8 } }),
    ]);
    return { requests, expenses };
  }, []);

  const awaiting = (data?.requests.items ?? []).filter((r) => r.requestStatus === 2).length;
  const readyToIssue = (data?.requests.items ?? []).filter((r) => r.requestStatus === 3 || r.requestStatus === 6).length;

  const actions: { to: string; label: string; hint: string; show: boolean }[] = [
    { to: "/stock/requests/new", label: "Request material", hint: "Ask for stock for a villa", show: canRequest },
    { to: "/stock/requests", label: "Material requests", hint: "Approve, issue, track", show: true },
    { to: "/stock/purchases/new", label: "Record a purchase", hint: "Into stock, or straight to a villa", show: canSpend || canRequest },
    { to: "/projects", label: "Project expenses", hint: "Labour, contractors, direct costs", show: true },
  ];

  return (
    <div className="space-y-4">
      <PageHeader title="Movement" />

      <div className="grid grid-cols-2 gap-3">
        <Card className="py-3">
          <div className="text-xs text-text-dim">Awaiting approval</div>
          <div className={`text-xl font-semibold ${awaiting ? "text-warn" : ""}`}>{awaiting}</div>
        </Card>
        <Card className="py-3">
          <div className="text-xs text-text-dim">Ready to issue</div>
          <div className={`text-xl font-semibold ${readyToIssue ? "text-brand-ink" : ""}`}>{readyToIssue}</div>
        </Card>
      </div>

      <div className="space-y-2">
        {actions.filter((a) => a.show).map((a) => (
          <Link key={a.to} to={a.to}>
            <Card className="flex items-center justify-between">
              <div>
                <div className="text-sm font-semibold">{a.label}</div>
                <div className="text-xs text-text-dim">{a.hint}</div>
              </div>
              <span className="text-text-dim">▸</span>
            </Card>
          </Link>
        ))}
      </div>

      <div>
        <div className="mb-2 px-1 text-xs font-semibold uppercase tracking-wide text-text-dim">Recent requests</div>
        {loading ? <SkeletonList rows={3} /> : (data?.requests.items.length ?? 0) === 0 ? (
          <EmptyState title="No material requests yet" hint={canRequest ? "Tap Request material to start one." : undefined} />
        ) : (
          <div className="space-y-2">
            {data!.requests.items.slice(0, 5).map((r) => (
              <Link key={r.id} to={`/stock/requests/${r.id}`}>
                <Card className="flex items-center justify-between">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="truncate text-sm font-semibold">{r.projectName}</span>
                      <Chip tone={statusTone(r.requestStatus)}>{MatReqStatusName[r.requestStatus]}</Chip>
                    </div>
                    <div className="truncate text-xs text-text-dim">{r.txnNumber} · {dateStr(r.date)}</div>
                  </div>
                </Card>
              </Link>
            ))}
          </div>
        )}
      </div>

      <div>
        <div className="mb-2 px-1 text-xs font-semibold uppercase tracking-wide text-text-dim">Recent project spend</div>
        {loading ? <SkeletonList rows={3} /> : (data?.expenses.items.length ?? 0) === 0 ? (
          <EmptyState title="Nothing spent yet" />
        ) : (
          <div className="space-y-2">
            {data!.expenses.items.slice(0, 5).map((e) => (
              <Card key={e.id} className="flex items-center justify-between">
                <div className="min-w-0">
                  <div className="truncate text-sm font-semibold">{e.expenseHeadName}</div>
                  <div className="truncate text-xs text-text-dim">{dateStr(e.date)} · {e.description || e.txnNumber}</div>
                </div>
                <div className="text-right text-sm tabular-nums">{moneyShort(e.amount)}</div>
              </Card>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
