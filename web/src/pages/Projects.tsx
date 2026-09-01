import { useState } from "react";
import { Link } from "react-router-dom";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { moneyShort } from "@/lib/format";
import { Button, Card, Chip, EmptyState, ErrorText, Field, Input, PageHeader, ProgressBar, Select, Sheet, Spinner, StatCard } from "@/components/ui";
import { ProjectStatusName, type Paged, type Project, type ProjectProgress, type Site } from "@/lib/types";

export default function Projects() {
  const canManage = useAuth((s) => s.can("projects.manage"));
  const [q, setQ] = useState("");
  const [open, setOpen] = useState(false);
  const { data, loading, error, reload } = useAsync(
    () => api<Paged<Project>>("/projects", { query: { q, pageSize: 100 } }),
    [q],
  );
  const { data: progress, reload: reloadProgress } = useAsync(
    () => api<ProjectProgress>("/projects/progress-summary"), []);

  const refreshAll = () => { reload(); reloadProgress(); };

  return (
    <div className="space-y-3">
      <PageHeader title="Projects" action={canManage && <Button onClick={() => setOpen(true)}>+ New</Button>} />
      {/* Where the work stands, before the list of it. Counts come from the whole book of work, not
          the filtered page, so searching does not silently change what they mean. */}
      <div className="grid grid-cols-3 gap-2">
        <StatCard label="Not started" value={String(progress?.notStarted ?? 0)} />
        <StatCard label="In progress" value={String(progress?.inProgress ?? 0)}
          sub={progress?.onHold ? `${progress.onHold} on hold` : undefined} />
        <StatCard label="Completed" value={String(progress?.completed ?? 0)} tone="ok" />
      </div>

      {progress && progress.inProgress > 0 && (
        <Card>
          <ProgressBar percent={progress.averageCompletionOfInProgress}
            label={`Average across ${progress.inProgress} project${progress.inProgress === 1 ? "" : "s"} under way`} />
        </Card>
      )}

      <Input placeholder="Search projects…" value={q} onChange={(e) => setQ(e.target.value)} />

      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
        (data?.items.length ?? 0) === 0 ? <EmptyState title="No projects" /> : (
          <div className="space-y-2">
            {data!.items.map((p) => (
              <Link key={p.id} to={`/projects/${p.id}`}>
                <Card>
                  <div className="flex items-center justify-between gap-3">
                    <div className="min-w-0">
                      <div className="flex items-center gap-2">
                        <span className="truncate text-sm font-semibold">{p.name}</span>
                        <Chip tone={p.status === 1 ? "ok" : p.status === 3 ? "ok" : "neutral"}>
                          {ProjectStatusName[p.status]}
                        </Chip>
                      </div>
                      <div className="truncate text-xs text-text-dim">{p.code} · {p.siteName}</div>
                    </div>
                    <div className="shrink-0 text-right text-xs text-text-dim">{moneyShort(p.estimatedCost)}</div>
                  </div>
                  {/* Only for work that has actually begun: a bar reading 0% on every planned villa
                      is noise, and a completed one is already said by its chip. */}
                  {(p.status === 1 || p.status === 2) && (
                    <div className="mt-3"><ProgressBar percent={p.completionPercent} /></div>
                  )}
                </Card>
              </Link>
            ))}
          </div>
        )
      )}

      <NewProjectSheet open={open} onClose={() => setOpen(false)} onSaved={() => { setOpen(false); refreshAll(); }} />
    </div>
  );
}

function NewProjectSheet({ open, onClose, onSaved }: { open: boolean; onClose: () => void; onSaved: () => void }) {
  const { data: sites } = useAsync(() => api<Paged<Site>>("/sites", { query: { pageSize: 100 } }), []);
  const [form, setForm] = useState({
    code: "", name: "", villaNumber: "", siteId: "", estimatedCost: "", contractSaleValue: "",
    status: "0", completionPercent: "0",
  });
  const [error, setError] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);
  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
    setForm({ ...form, [k]: e.target.value });

  async function save() {
    setBusy(true);
    setError(null);
    try {
      await api("/projects", {
        method: "POST",
        body: {
          code: form.code,
          name: form.name,
          villaNumber: form.villaNumber || null,
          siteId: form.siteId,
          estimatedCost: Number(form.estimatedCost || 0),
          contractSaleValue: form.contractSaleValue ? Number(form.contractSaleValue) : null,
          status: Number(form.status),
          completionPercent: Number(form.completionPercent || 0),
        },
      });
      onSaved();
    } catch (e) {
      setError(e as ApiError);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Sheet open={open} onClose={onClose} title="New project">
      <div className="space-y-3">
        <Field label="Site">
          <Select value={form.siteId} onChange={set("siteId")}>
            <option value="">Select a site…</option>
            {sites?.items.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </Select>
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Code"><Input value={form.code} onChange={set("code")} placeholder="GV-101" /></Field>
          <Field label="Villa no."><Input value={form.villaNumber} onChange={set("villaNumber")} placeholder="101" /></Field>
        </div>
        <Field label="Name"><Input value={form.name} onChange={set("name")} placeholder="Villa 101" /></Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Estimated cost"><Input value={form.estimatedCost} onChange={set("estimatedCost")} inputMode="numeric" /></Field>
          <Field label="Sale value"><Input value={form.contractSaleValue} onChange={set("contractSaleValue")} inputMode="numeric" /></Field>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Status">
            <Select
              value={form.status}
              onChange={(e) => {
                const status = e.target.value;
                const completionPercent =
                  status === "3" ? "100" : status === "0" ? "0" : form.completionPercent;
                setForm({ ...form, status, completionPercent });
              }}
            >
              <option value="0">Planned</option><option value="1">Active</option><option value="2">On Hold</option>
              <option value="3">Completed</option>
            </Select>
          </Field>
          {/* A villa handed over part-built, or taken on mid-way, starts somewhere other than zero. */}
          <Field label="Completion %">
            <Input
              inputMode="numeric"
              value={form.completionPercent}
              disabled={form.status === "0" || form.status === "3"}
              onChange={(e) => setForm({ ...form, completionPercent: e.target.value.replace(/[^0-9]/g, "") })}
            />
          </Field>
        </div>
        <ErrorText error={error} />
        <Button className="w-full" onClick={save} disabled={busy}>{busy ? "Saving…" : "Create project"}</Button>
      </div>
    </Sheet>
  );
}
