import { useState } from "react";
import { Link } from "react-router-dom";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import { money } from "@/lib/format";
import { Button, Card, Chip, EmptyState, ErrorText, Field, Input, PageHeader, Sheet, Spinner } from "@/components/ui";
import type { Customer, CustomerLedger, Paged } from "@/lib/types";

export default function Customers() {
  const canManage = useAuth((s) => s.can("masters.manage"));
  const [q, setQ] = useState("");
  const [open, setOpen] = useState(false);
  const { data, loading, error, reload } = useAsync(
    () => api<Paged<Customer>>("/customers", { query: { q, pageSize: 100 } }),
    [q],
  );

  return (
    <div className="space-y-3">
      <Link to="/more" className="text-xs text-text-dim">← More</Link>
      <PageHeader title="Customers" action={canManage && <Button onClick={() => setOpen(true)}>+ New</Button>} />
      <Input placeholder="Search…" value={q} onChange={(e) => setQ(e.target.value)} />

      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
        (data?.items.length ?? 0) === 0 ? <EmptyState title="No customers" /> :
          data!.items.map((c) => <CustomerRow key={c.id} c={c} />)
      )}

      <NewCustomerSheet open={open} onClose={() => setOpen(false)} onSaved={() => { setOpen(false); reload(); }} />
    </div>
  );
}

function CustomerRow({ c }: { c: Customer }) {
  const [expanded, setExpanded] = useState(false);
  const { data } = useAsync(
    () => (expanded ? api<CustomerLedger>(`/customer-payments/ledger/${c.id}`) : Promise.resolve(null)),
    [expanded, c.id],
  );
  return (
    <Card onClick={() => setExpanded(!expanded)}>
      <div className="flex items-center justify-between">
        <div>
          <div className="flex items-center gap-2">
            <span className="text-sm font-semibold">{c.name}</span>
            {!c.isActive && <Chip tone="danger">Inactive</Chip>}
          </div>
          <div className="text-xs text-text-dim">{c.code}{c.mobile ? ` · ${c.mobile}` : ""}</div>
        </div>
        <span className="text-text-dim">{expanded ? "▾" : "▸"}</span>
      </div>
      {expanded && data && (
        <div className="mt-2 space-y-0.5 border-t border-border pt-2 text-xs">
          <div className="flex justify-between"><span className="text-text-dim">Sale value</span><span className="tabular-nums">{money(data.totalSaleValue)}</span></div>
          <div className="flex justify-between"><span className="text-text-dim">Received</span><span className="tabular-nums">{money(data.totalReceived)}</span></div>
          <div className="flex justify-between font-semibold"><span>Outstanding</span><span className="tabular-nums text-warn">{money(data.outstanding)}</span></div>
        </div>
      )}
    </Card>
  );
}

function NewCustomerSheet({ open, onClose, onSaved }: { open: boolean; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState({ code: "", name: "", mobile: "", email: "" });
  const [err, setErr] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);
  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) => setForm({ ...form, [k]: e.target.value });

  async function save() {
    setBusy(true); setErr(null);
    try {
      await api("/customers", { method: "POST", body: { ...form, email: form.email || null, isActive: true } });
      onSaved();
    } catch (e) { setErr(e as ApiError); } finally { setBusy(false); }
  }

  return (
    <Sheet open={open} onClose={onClose} title="New customer">
      <div className="space-y-3">
        <Field label="Code"><Input value={form.code} onChange={set("code")} placeholder="CUST-01" /></Field>
        <Field label="Name"><Input value={form.name} onChange={set("name")} /></Field>
        <Field label="Mobile"><Input value={form.mobile} onChange={set("mobile")} inputMode="tel" /></Field>
        <Field label="Email"><Input value={form.email} onChange={set("email")} inputMode="email" /></Field>
        <ErrorText error={err} />
        <Button className="w-full" onClick={save} disabled={busy || !form.code || !form.name}>Create</Button>
      </div>
    </Sheet>
  );
}
