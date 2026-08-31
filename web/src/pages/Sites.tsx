import { useState } from "react";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { moneyShort } from "@/lib/format";
import { Button, Card, Chip, EmptyState, ErrorText, Field, Input, PageHeader, Select, Sheet, SkeletonList } from "@/components/ui";
import { SiteStatusName, type Paged, type Site } from "@/lib/types";

export default function Sites() {
  const canManage = useAuth((s) => s.can("sites.manage"));
  const [q, setQ] = useState("");
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<Site | null>(null);
  const { data, loading, error, reload } = useAsync(() => api<Paged<Site>>("/sites", { query: { q, pageSize: 100 } }), [q]);

  return (
    <div className="space-y-3">
      <PageHeader title="Sites" action={canManage && <Button onClick={() => setCreating(true)}>+ New</Button>} />
      <Input placeholder="Search sites…" value={q} onChange={(e) => setQ(e.target.value)} />

      {loading ? <SkeletonList /> : error ? <ErrorText error={error} /> : (
        (data?.items.length ?? 0) === 0 ? <EmptyState title="No sites" hint={canManage ? "Tap + New to add one." : undefined} /> : (
          <div className="space-y-2">
            {data!.items.map((s) => (
              <Card key={s.id} onClick={canManage ? () => setEditing(s) : undefined} className="flex items-center justify-between">
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="truncate text-sm font-semibold">{s.name}</span>
                    <Chip tone={s.status === 1 ? "ok" : "neutral"}>{SiteStatusName[s.status]}</Chip>
                  </div>
                  <div className="truncate text-xs text-text-dim">
                    {s.code} · {[s.city, s.state].filter(Boolean).join(", ") || "—"} · {s.projectCount} projects
                  </div>
                </div>
                <div className="text-right text-xs text-text-dim">{moneyShort(s.inventoryValue)}</div>
              </Card>
            ))}
          </div>
        )
      )}

      <SiteSheet open={creating} onClose={() => setCreating(false)} onSaved={() => { setCreating(false); reload(); }} />
      {editing && <SiteSheet open site={editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); reload(); }} />}
    </div>
  );
}

function SiteSheet({ open, site, onClose, onSaved }: { open: boolean; site?: Site; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState({
    code: site?.code ?? "", name: site?.name ?? "", city: site?.city ?? "", state: site?.state ?? "",
    pin: site?.pin ?? "", status: String(site?.status ?? 1),
  });
  const [error, setError] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);
  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
    setForm({ ...form, [k]: e.target.value });

  async function save() {
    setBusy(true);
    setError(null);
    try {
      const body = { ...form, pin: form.pin || null, status: Number(form.status) };
      if (site) await api(`/sites/${site.id}`, { method: "PUT", body });
      else await api("/sites", { method: "POST", body });
      onSaved();
    } catch (e) {
      setError(e as ApiError);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Sheet open={open} onClose={onClose} title={site ? "Edit site" : "New site"}>
      <div className="space-y-3">
        <Field label="Code"><Input value={form.code} onChange={set("code")} placeholder="GV" /></Field>
        <Field label="Name"><Input value={form.name} onChange={set("name")} placeholder="Green Valley" /></Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="City"><Input value={form.city} onChange={set("city")} /></Field>
          <Field label="State"><Input value={form.state} onChange={set("state")} /></Field>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <Field label="PIN"><Input value={form.pin} onChange={set("pin")} inputMode="numeric" /></Field>
          <Field label="Status">
            <Select value={form.status} onChange={set("status")}>
              <option value="0">Planned</option><option value="1">Active</option><option value="2">On Hold</option>
              <option value="3">Completed</option><option value="4">Cancelled</option>
            </Select>
          </Field>
        </div>
        <ErrorText error={error} />
        <Button className="w-full" onClick={save} disabled={busy || !form.code || !form.name}>
          {busy ? "Saving…" : site ? "Save" : "Create site"}
        </Button>
      </div>
    </Sheet>
  );
}
