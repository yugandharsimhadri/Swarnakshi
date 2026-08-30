import { useState } from "react";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { moneyShort } from "@/lib/format";
import { Button, Card, Chip, EmptyState, ErrorText, Field, Input, PageHeader, Sheet, Spinner } from "@/components/ui";
import { SiteStatusName, type Paged, type Site } from "@/lib/types";

export default function Sites() {
  const canManage = useAuth((s) => s.can("sites.manage"));
  const [q, setQ] = useState("");
  const [open, setOpen] = useState(false);
  const { data, loading, error, reload } = useAsync(() => api<Paged<Site>>("/sites", { query: { q, pageSize: 100 } }), [q]);

  return (
    <div className="space-y-3">
      <PageHeader
        title="Sites"
        action={canManage && <Button onClick={() => setOpen(true)}>+ New</Button>}
      />
      <Input placeholder="Search sites…" value={q} onChange={(e) => setQ(e.target.value)} />

      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
        (data?.items.length ?? 0) === 0 ? <EmptyState title="No sites" hint={canManage ? "Tap + New to add one." : undefined} /> : (
          <div className="space-y-2">
            {data!.items.map((s) => (
              <Card key={s.id} className="flex items-center justify-between">
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

      <NewSiteSheet open={open} onClose={() => setOpen(false)} onSaved={() => { setOpen(false); reload(); }} />
    </div>
  );
}

function NewSiteSheet({ open, onClose, onSaved }: { open: boolean; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState({ code: "", name: "", city: "", state: "", pin: "" });
  const [error, setError] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);
  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) => setForm({ ...form, [k]: e.target.value });

  async function save() {
    setBusy(true);
    setError(null);
    try {
      await api("/sites", { method: "POST", body: { ...form, pin: form.pin || null, status: 1 } });
      setForm({ code: "", name: "", city: "", state: "", pin: "" });
      onSaved();
    } catch (e) {
      setError(e as ApiError);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Sheet open={open} onClose={onClose} title="New site">
      <div className="space-y-3">
        <Field label="Code"><Input value={form.code} onChange={set("code")} placeholder="GV" /></Field>
        <Field label="Name"><Input value={form.name} onChange={set("name")} placeholder="Green Valley" /></Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="City"><Input value={form.city} onChange={set("city")} /></Field>
          <Field label="State"><Input value={form.state} onChange={set("state")} /></Field>
        </div>
        <Field label="PIN"><Input value={form.pin} onChange={set("pin")} inputMode="numeric" /></Field>
        <ErrorText error={error} />
        <Button className="w-full" onClick={save} disabled={busy}>{busy ? "Saving…" : "Create site"}</Button>
      </div>
    </Sheet>
  );
}
