import { useState } from "react";
import { Link } from "react-router-dom";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { RoleName, type AdminUser, type Paged, type Role, type Site } from "@/lib/types";
import {
  Button, Card, Chip, ErrorText, Field, Input, PageHeader, Select, Sheet, SkeletonList,
} from "@/components/ui";

const ROLES: Role[] = [1, 2, 3, 4];

export default function Users() {
  const { data, loading, error, reload } = useAsync(() => api<AdminUser[]>("/users"), []);
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<AdminUser | null>(null);

  return (
    <div className="space-y-3">
      <Link to="/more" className="-ml-1 inline-flex min-h-11 items-center px-1 text-xs text-text-dim">← More</Link>
      <PageHeader title="Users" action={<Button onClick={() => setCreating(true)}>+ New</Button>} />

      {loading ? <SkeletonList /> : error ? <ErrorText error={error} /> : (
        <div className="space-y-2">
          {data!.map((u) => (
            <Card key={u.id} onClick={() => setEditing(u)}>
              <div className="flex items-center justify-between">
                <div>
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-semibold">{u.name}</span>
                    {!u.isActive && <Chip tone="danger">Inactive</Chip>}
                  </div>
                  <div className="text-xs text-text-dim">{u.email}</div>
                </div>
                <Chip tone="brand">{RoleName[u.role]}</Chip>
              </div>
              {(u.extraPermissions.length > 0 || u.siteIds.length > 0) && (
                <div className="mt-1 text-xs text-text-dim">
                  {u.extraPermissions.length > 0 && `${u.extraPermissions.length} extra permission(s)`}
                  {u.extraPermissions.length > 0 && u.siteIds.length > 0 && " · "}
                  {u.siteIds.length > 0 && `${u.siteIds.length} site(s)`}
                </div>
              )}
            </Card>
          ))}
        </div>
      )}

      <CreateUserSheet open={creating} onClose={() => setCreating(false)} onSaved={() => { setCreating(false); reload(); }} />
      {editing && (
        <EditUserSheet
          user={editing}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); reload(); }}
        />
      )}
    </div>
  );
}

function CreateUserSheet({ open, onClose, onSaved }: { open: boolean; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState({ name: "", email: "", password: "", role: "3" });
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  async function save() {
    setBusy(true); setErr(null);
    try {
      await api("/users", { method: "POST", body: { ...form, role: Number(form.role) } });
      onSaved();
    } catch (e) { setErr(e as ApiError); } finally { setBusy(false); }
  }

  return (
    <Sheet open={open} onClose={onClose} title="New user">
      <div className="space-y-3">
        <Field label="Name"><Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></Field>
        <Field label="Email"><Input inputMode="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} /></Field>
        <Field label="Temporary password"><Input value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} /></Field>
        <Field label="Role">
          <Select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })}>
            {ROLES.map((r) => <option key={r} value={r}>{RoleName[r]}</option>)}
          </Select>
        </Field>
        <ErrorText error={err} />
        <Button className="w-full" onClick={save} disabled={busy || !form.name || !form.email || form.password.length < 8}>
          Create user
        </Button>
      </div>
    </Sheet>
  );
}

function EditUserSheet({ user, onClose, onSaved }: { user: AdminUser; onClose: () => void; onSaved: () => void }) {
  const { data: permKeys } = useAsync(() => api<string[]>("/users/permission-keys"), []);
  const { data: sites } = useAsync(() => api<Paged<Site>>("/sites", { query: { pageSize: 100 } }), []);
  const [form, setForm] = useState({ name: user.name, role: String(user.role), isActive: user.isActive });
  const [perms, setPerms] = useState<string[]>(user.extraPermissions);
  const [siteIds, setSiteIds] = useState<string[]>(user.siteIds);
  const [pwd, setPwd] = useState("");
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  const toggle = (list: string[], set: (v: string[]) => void, val: string) =>
    set(list.includes(val) ? list.filter((x) => x !== val) : [...list, val]);

  async function saveAll() {
    setBusy(true); setErr(null);
    try {
      await api(`/users/${user.id}`, { method: "PUT", body: { name: form.name, role: Number(form.role), isActive: form.isActive } });
      if (Number(form.role) === 2) await api(`/users/${user.id}/permissions`, { method: "PUT", body: { permissions: perms } });
      if (Number(form.role) === 3) await api(`/users/${user.id}/sites`, { method: "PUT", body: { siteIds } });
      if (pwd.length >= 8) await api(`/users/${user.id}/password`, { method: "POST", body: { password: pwd } });
      onSaved();
    } catch (e) { setErr(e as ApiError); } finally { setBusy(false); }
  }

  return (
    <Sheet open onClose={onClose} title={user.name}>
      <div className="space-y-3">
        <Field label="Name"><Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></Field>
        <Field label="Role">
          <Select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })}>
            {ROLES.map((r) => <option key={r} value={r}>{RoleName[r]}</option>)}
          </Select>
        </Field>
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />
          Active
        </label>

        {Number(form.role) === 2 && (
          <Field label="Extra permissions (Sub-Owner)">
            <div className="max-h-44 space-y-1 overflow-y-auto rounded-xl border border-border p-2">
              {permKeys?.map((k) => (
                <label key={k} className="flex items-center gap-2 text-xs">
                  <input type="checkbox" checked={perms.includes(k)} onChange={() => toggle(perms, setPerms, k)} />
                  {k}
                </label>
              ))}
            </div>
          </Field>
        )}

        {Number(form.role) === 3 && (
          <Field label="Assigned sites (Supervisor)">
            <div className="max-h-44 space-y-1 overflow-y-auto rounded-xl border border-border p-2">
              {sites?.items.map((s) => (
                <label key={s.id} className="flex items-center gap-2 text-xs">
                  <input type="checkbox" checked={siteIds.includes(s.id)} onChange={() => toggle(siteIds, setSiteIds, s.id)} />
                  {s.name}
                </label>
              ))}
            </div>
          </Field>
        )}

        <Field label="Reset password (optional)"><Input value={pwd} onChange={(e) => setPwd(e.target.value)} placeholder="min 8 chars" /></Field>
        <ErrorText error={err} />
        <Button className="w-full" onClick={saveAll} disabled={busy || !form.name}>Save</Button>
      </div>
    </Sheet>
  );
}
