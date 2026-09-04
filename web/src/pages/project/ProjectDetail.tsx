import { useState } from "react";
import { useParams } from "react-router-dom";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { money, moneyShort } from "@/lib/format";
import {
  Button, Card, Chip, ErrorText, Field, Input, PageHeader, ProgressBar, Select, Sheet, Spinner,
  StatCard
} from "@/components/ui";
import {
  ProjectStatusName, type Customer, type Lookup, type Paged, type Project,
  type ProjectSummary
} from "@/lib/types";
import CustomerTab from "@/pages/project/CustomerTab";
import Overview from "@/pages/project/OverviewTab";
import MaterialTab from "@/pages/project/MaterialTab";
import Expenses from "@/pages/project/ExpensesTab";
import ContractorsTab from "@/pages/project/ContractorsTab";

/**
 * The villa screen: who it is for, what it has cost, and five tabs.
 *
 * This file is the shell only — the header, the figures across the top, the alerts and the tab
 * strip. Each tab lives in its own module beside this one and owns its own data fetching and its
 * own sheets, so a change to expenses cannot break contractors.
 */

/**
 * A villa has exactly three things people record against it, and the tabs say so out loud:
 * material that arrived, money spent, and work given to a contractor. Overview and Customer
 * are for reading, not entering.
 */
type Tab = "overview" | "material" | "expenses" | "contractors" | "customer";

export default function ProjectDetail() {
  const { id } = useParams<{ id: string }>();
  const [tab, setTab] = useState<Tab>("overview");
  const [editing, setEditing] = useState(false);
  const canManage = useAuth((st) => st.can("projects.manage"));
  const { data, loading, error, reload } = useAsync(async () => {
    const [project, summary] = await Promise.all([
      api<Project>(`/projects/${id}`),
      api<ProjectSummary>(`/projects/${id}/summary`),
    ]);
    return { project, summary };
  }, [id]);

  if (loading) return <Spinner />;
  if (error || !data) return <ErrorText error={error} />;
  const { project: p, summary: s } = data;

  const tabs: [Tab, string][] = [
    ["overview", "Overview"], ["material", "Material"], ["expenses", "Expenses"],
    ["contractors", "Contractors"], ["customer", "Customer"],
  ];

  return (
    <div className="space-y-4">
      <PageHeader
        title={p.name}
        back="/projects"
        subtitle={`${p.siteName}${p.customerName ? ` · ${p.customerName}` : ""}`}
        action={
          <div className="flex shrink-0 items-center gap-2">
            <Chip tone={p.status === 1 ? "ok" : "neutral"}>{ProjectStatusName[p.status] ?? p.status}</Chip>
            {canManage && <Button variant="ghost" onClick={() => setEditing(true)}>Edit</Button>}
          </div>
        }
      />
      {(p.status === 1 || p.status === 2) && (
        <Card><ProgressBar percent={p.completionPercent} /></Card>
      )}

      <Alerts s={s} />

      <EditProjectSheet project={p} open={editing} onClose={() => setEditing(false)} onSaved={() => { setEditing(false); reload(); }} />

      <div className="grid grid-cols-2 gap-3">
        <StatCard label="Spent so far" value={moneyShort(s.totalCost)}
          sub={s.committedContractorCost > 0 ? `+ ${moneyShort(s.committedContractorCost)} committed` : undefined} />
        <StatCard label="Estimated" value={moneyShort(s.estimatedCost)} />
        {/* Burn beats budget variance on an unfinished villa: variance shows a large positive that
            reads as money saved when it is really a house that is not finished. */}
        <StatCard
          label="Spend vs progress"
          value={s.burnPercent == null ? "—" : `${s.burnPercent}%`}
          sub={s.burnPercent == null ? "not started yet" : `of ${moneyShort(s.expectedCostToDate)} expected`}
          tone={s.burnPercent == null ? undefined : s.burnPercent > 110 ? "danger" : s.burnPercent > 100 ? "warn" : "ok"}
        />
        <StatCard
          label="Earned margin"
          value={s.earnedMargin == null ? "unsold" : moneyShort(s.earnedMargin)}
          sub={s.earnedRevenue == null ? undefined : `on ${moneyShort(s.earnedRevenue)} earned`}
          tone={s.earnedMargin == null ? undefined : s.earnedMargin < 0 ? "danger" : "ok"}
        />
      </div>

      <div className="flex gap-1 overflow-x-auto rounded-xl bg-surface-2 p-1">
        {tabs.map(([t, label]) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`min-h-11 whitespace-nowrap rounded-lg px-3 py-1.5 text-xs font-semibold ${tab === t ? "bg-surface text-text" : "text-text-dim"}`}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === "overview" && <Overview projectId={p.id} s={s} />}
      {tab === "material" && <MaterialTab projectId={p.id} />}
      {tab === "expenses" && <Expenses projectId={p.id} />}
      {tab === "contractors" && <ContractorsTab projectId={p.id} />}
      {tab === "customer" && <CustomerTab projectId={p.id} hasCustomer={!!p.customerId} summary={s} />}
    </div>
  );
}

/**
 * The two things about a villa that need somebody to do something today. Both were derivable from
 * numbers already on the screen and neither was ever said out loud, which is the same as not being
 * there — an owner scanning ten villas reads chips, not arithmetic.
 */
function Alerts({ s }: { s: ProjectSummary }) {
  const overBudget = s.burnPercent != null && s.burnPercent > 100;
  if (!s.duesOnHandover && !overBudget) return null;

  return (
    <div className="space-y-2">
      {s.duesOnHandover && (
        <Card className="border-danger/40 bg-danger/5">
          <div className="text-sm font-semibold text-danger">Handed over, still owed {money(s.customerOutstanding)}</div>
          <div className="mt-0.5 text-xs text-text-dim">
            The villa is complete and the customer has not paid in full.
          </div>
        </Card>
      )}
      {overBudget && (
        <Card className={s.burnPercent! > 110 ? "border-danger/40 bg-danger/5" : "border-warn/40 bg-warn/5"}>
          <div className={`text-sm font-semibold ${s.burnPercent! > 110 ? "text-danger" : "text-warn"}`}>
            {/* Not "% of budget spent": the figure compares spend against the spend expected by
                this stage, so a villa 10% built that has used 11% of its budget reads 110%. The
                old wording said 110% of the whole budget was gone, which is ten times the truth. */}
            Spending {s.burnPercent}% of what {s.completionPercent}% built should have cost
          </div>
          <div className="mt-0.5 text-xs text-text-dim">
            {moneyShort(s.totalCost)} spent against {moneyShort(s.expectedCostToDate)} expected by this stage.
          </div>
        </Card>
      )}
    </div>
  );
}

// ---- Customer tab -------------------------------------------------

function EditProjectSheet({ project, open, onClose, onSaved }: { project: Project; open: boolean; onClose: () => void; onSaved: () => void }) {
  const { data: customers } = useAsync(() => api<Paged<Customer>>("/customers", { query: { pageSize: 200, active: true } }), []);
  const { data: types } = useAsync(() => api<Lookup[]>("/project-types"), []);
  const [form, setForm] = useState({
    name: project.name, villaNumber: project.villaNumber ?? "", customerId: project.customerId ?? "",
    projectTypeId: project.projectTypeId ?? "", estimatedCost: String(project.estimatedCost),
    contractSaleValue: project.contractSaleValue != null ? String(project.contractSaleValue) : "",
    status: String(project.status),
    completionPercent: String(project.completionPercent ?? 0),
  });
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  async function save() {
    setBusy(true); setErr(null);
    try {
      await api(`/projects/${project.id}`, {
        method: "PUT",
        body: {
          name: form.name, villaNumber: form.villaNumber || null, siteId: project.siteId,
          customerId: form.customerId || null, projectTypeId: form.projectTypeId || null,
          estimatedCost: Number(form.estimatedCost || 0),
          contractSaleValue: form.contractSaleValue ? Number(form.contractSaleValue) : null,
          status: Number(form.status),
          completionPercent: Number(form.completionPercent || 0),
        },
      });
      onSaved();
    } catch (e) { setErr(e as ApiError); } finally { setBusy(false); }
  }

  return (
    <Sheet open={open} onClose={onClose} title="Edit project">
      <div className="space-y-3">
        <Field label="Name"><Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></Field>
        <Field label="Villa number"><Input value={form.villaNumber} onChange={(e) => setForm({ ...form, villaNumber: e.target.value })} /></Field>
        <Field label="Customer">
          <Select value={form.customerId} onChange={(e) => setForm({ ...form, customerId: e.target.value })}>
            <option value="">— none (self-owned) —</option>
            {customers?.items.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </Select>
        </Field>
        <Field label="Project type">
          <Select value={form.projectTypeId} onChange={(e) => setForm({ ...form, projectTypeId: e.target.value })}>
            <option value="">—</option>
            {types?.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
          </Select>
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Estimated cost"><Input inputMode="decimal" value={form.estimatedCost} onChange={(e) => setForm({ ...form, estimatedCost: e.target.value })} /></Field>
          <Field label="Sale value"><Input inputMode="decimal" value={form.contractSaleValue} onChange={(e) => setForm({ ...form, contractSaleValue: e.target.value })} /></Field>
        </div>
        <Field label="Status">
          <Select
            value={form.status}
            onChange={(e) => {
              const status = e.target.value;
              // Keep the two honest as they are edited, rather than letting the server correct it
              // after the fact: finishing a project means 100%, and one not started means 0.
              const completionPercent =
                status === "3" ? "100" : status === "0" ? "0" : form.completionPercent;
              setForm({ ...form, status, completionPercent });
            }}
          >
            <option value="0">Planned</option><option value="1">Active</option><option value="2">On Hold</option>
            <option value="3">Completed</option><option value="4">Cancelled</option>
          </Select>
        </Field>
        <Field label="Completion %">
          <Input
            inputMode="numeric"
            value={form.completionPercent}
            disabled={form.status === "0" || form.status === "3"}
            onChange={(e) => setForm({ ...form, completionPercent: e.target.value.replace(/[^0-9]/g, "") })}
          />
        </Field>
        <ProgressBar percent={Number(form.completionPercent || 0)} />
        <ErrorText error={err} />
        <Button className="w-full" onClick={save} disabled={busy || !form.name}>Save</Button>
      </div>
    </Sheet>
  );
}

// ---- Material tab ----------------------------------------------------
/**
 * Entry type one: material that reached this villa. Two ways in, and the screen says which is
 * which in the words a supervisor uses — "take from the store" or "bought for this villa".
 * Both land here afterwards, so the villa's material history is one list regardless of route.
 */
