import { api } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { moneyShort } from "@/lib/format";
import { Card, Chip, PageHeader, Spinner, StatCard, EmptyState } from "@/components/ui";
import type { Paged, Project, Site } from "@/lib/types";
import { Link } from "react-router-dom";

export default function Dashboard() {
  const user = useAuth((s) => s.user);
  const canApprove = useAuth((s) => s.can("approvals.decide"));

  const { data, loading } = useAsync(async () => {
    const [sites, projects, approvals] = await Promise.all([
      api<Paged<Site>>("/sites", { query: { pageSize: 100 } }),
      api<Paged<Project>>("/projects", { query: { pageSize: 100 } }),
      canApprove ? api<{ pending: number }>("/approvals/count") : Promise.resolve({ pending: 0 }),
    ]);
    return { sites, projects, approvals };
  }, [canApprove]);

  if (loading) return <Spinner />;

  const sites = data?.sites.items ?? [];
  const projects = data?.projects.items ?? [];
  const pending = data?.approvals.pending ?? 0;
  const activeProjects = projects.filter((p) => p.status === 1).length;
  const inventoryValue = sites.reduce((s, x) => s + x.inventoryValue, 0);
  const estimatedTotal = projects.reduce((s, p) => s + p.estimatedCost, 0);

  return (
    <div className="space-y-4">
      <PageHeader title={`Hi, ${user?.name ?? ""}`} />

      {canApprove && pending > 0 && (
        <Link to="/approvals">
          <Card className="flex items-center justify-between border-brand/40 bg-brand/10">
            <div>
              <div className="text-sm font-semibold text-brand-ink">Approvals waiting</div>
              <div className="text-xs text-text-dim">Review material requests, purchases &amp; payments</div>
            </div>
            <Chip tone="brand">{pending}</Chip>
          </Card>
        </Link>
      )}

      <div className="grid grid-cols-2 gap-3">
        <StatCard label="Active sites" value={String(sites.filter((s) => s.status === 1).length)} sub={`${sites.length} total`} />
        <StatCard label="Active projects" value={String(activeProjects)} sub={`${projects.length} total`} />
        <StatCard label="Inventory value" value={moneyShort(inventoryValue)} />
        <StatCard label="Estimated cost" value={moneyShort(estimatedTotal)} sub="all projects" />
      </div>

      <div>
        <div className="mb-2 px-1 text-xs font-semibold uppercase tracking-wide text-text-dim">Recent projects</div>
        {projects.length === 0 ? (
          <EmptyState title="No projects yet" hint="Add a site, then create projects under it." />
        ) : (
          <div className="space-y-2">
            {projects.slice(0, 5).map((p) => (
              <Link key={p.id} to={`/projects/${p.id}`}>
                <Card className="flex items-center justify-between">
                  <div className="min-w-0">
                    <div className="truncate text-sm font-semibold">{p.name}</div>
                    <div className="truncate text-xs text-text-dim">{p.siteName}</div>
                  </div>
                  <div className="text-right text-xs text-text-dim">{moneyShort(p.estimatedCost)}</div>
                </Card>
              </Link>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
