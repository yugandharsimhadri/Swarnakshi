import { useCallback, useEffect, useMemo, useState } from "react";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { num } from "@/lib/format";
import { useAuth } from "@/store/auth";
import {
  Button, Card, Chip, Confirm, DetailRow, EmptyState, ErrorText, Field, FormSection, FormSheet,
  Input, PageHeader, Select, SkeletonList, StatCard, TableWrap, Td, Th,
} from "@/components/ui";
import type { Paged, Party, PartyDetail, PartySummary, SavePartyBody } from "@/lib/types";

const PAGE_SIZE = 50;

/** What differs between the contractor and customer masters — everything else is shared. */
export interface PartyMasterConfig {
  /** API segment: "contractors" | "customers". */
  resource: string;
  title: string;
  subtitle: string;
  addLabel: string;
  searchPlaceholder: string;
  singular: string;
  /** Contractors carry Company / Contractor Type / Bank Details; customers do not. */
  hasCompany: boolean;
  hasType: boolean;
  hasBankDetails: boolean;
  deactivateBody: string;
  reactivateBody: string;
}

type Draft = {
  name: string; companyName: string; type: string;
  mobile: string; email: string; address: string;
  pan: string; gstin: string; bankDetails: string; notes: string;
};

const emptyDraft: Draft = {
  name: "", companyName: "", type: "",
  mobile: "", email: "", address: "", pan: "", gstin: "", bankDetails: "", notes: "",
};

export default function PartyMaster({ config }: { config: PartyMasterConfig }) {
  const canManage = useAuth((s) => s.can("masters.manage"));
  const { resource } = config;

  // ---- filters (default Status = Active, per the master-data convention) ----
  const [q, setQ] = useState("");
  const [debouncedQ, setDebouncedQ] = useState("");
  const [status, setStatus] = useState("active");
  const [type, setType] = useState("");
  const [page, setPage] = useState(1);

  useEffect(() => {
    const t = setTimeout(() => { setDebouncedQ(q); setPage(1); }, 250);
    return () => clearTimeout(t);
  }, [q]);

  const filtersOn = Boolean(q || type || status !== "active");
  const clearFilters = () => {
    setQ(""); setDebouncedQ(""); setStatus("active"); setType(""); setPage(1);
  };

  const { data: summary, reload: reloadSummary } = useAsync(
    () => api<PartySummary>(`/${resource}/summary`), [resource]);

  const { data: types } = useAsync(
    () => config.hasType ? api<string[]>(`/${resource}/types`) : Promise.resolve([] as string[]),
    [resource],
  );

  const { data, loading, error, reload } = useAsync(
    () => api<Paged<Party>>(`/${resource}`, {
      query: {
        q: debouncedQ, type,
        active: status === "" ? undefined : status === "active",
        page, pageSize: PAGE_SIZE,
      },
    }),
    [resource, debouncedQ, status, type, page],
  );

  const refreshAll = useCallback(() => { reload(); reloadSummary(); }, [reload, reloadSummary]);

  // ---- dialogs ----
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<PartyDetail | null>(null);
  const [viewing, setViewing] = useState<PartyDetail | null>(null);
  const [confirm, setConfirm] = useState<{ party: Party; activate: boolean } | null>(null);
  const [rowError, setRowError] = useState<ApiError | null>(null);

  const load = async (p: Party, into: (d: PartyDetail) => void) => {
    setRowError(null);
    try { into(await api<PartyDetail>(`/${resource}/${p.id}`)); }
    catch (e) { setRowError(e as ApiError); }
  };

  const runLifecycle = async () => {
    if (!confirm) return;
    const { party, activate } = confirm;
    setConfirm(null);
    setRowError(null);
    try {
      await api(`/${resource}/${party.id}/${activate ? "reactivate" : "deactivate"}`, { method: "POST" });
      refreshAll();
    } catch (e) { setRowError(e as ApiError); }
  };

  const total = data?.total ?? 0;
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <div className="space-y-3">

      <PageHeader
        title={config.title}
        action={canManage && (
          <Button onClick={() => { setRowError(null); setCreating(true); }}>{config.addLabel}</Button>
        )}
      />
      <p className="-mt-2 px-1 text-xs text-text-dim">{config.subtitle}</p>

      <div className="grid grid-cols-3 gap-2">
        <StatCard label={`Total ${config.title}`} value={num(summary?.total)} />
        <StatCard label="Active" value={num(summary?.active)} tone="ok" />
        <StatCard label="Inactive" value={num(summary?.inactive)} tone={summary?.inactive ? "warn" : undefined} />
      </div>

      <Card className="space-y-3">
        <Input placeholder={config.searchPlaceholder} value={q} onChange={(e) => setQ(e.target.value)} />
        <div className="grid grid-cols-2 gap-2">
          <Select value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }}>
            <option value="active">Active</option>
            <option value="inactive">Inactive</option>
            <option value="">All</option>
          </Select>
          {config.hasType && (
            <Select value={type} onChange={(e) => { setType(e.target.value); setPage(1); }}>
              <option value="">All types</option>
              {(types ?? []).map((t) => <option key={t} value={t}>{t}</option>)}
            </Select>
          )}
        </div>
        {filtersOn && (
          <div className="flex items-center justify-between">
            <span className="text-xs text-text-dim">{num(total)} matching</span>
            <Button variant="ghost" onClick={clearFilters}>Clear Filters</Button>
          </div>
        )}
      </Card>

      <ErrorText error={rowError && { message: rowError.message, errors: rowError.errors }} />

      {loading ? <SkeletonList rows={5} />
        : error ? <ErrorText error={{ message: error.message, errors: error.errors }} />
        : total === 0 ? (
          <EmptyState
            title={`No ${config.title.toLowerCase()} found`}
            hint={filtersOn ? "Try clearing the filters." : `Add the first ${config.singular} to get started.`}
          />
        ) : (
          <>
            {/* Mobile: compact cards */}
            <div className="space-y-2 lg:hidden">
              {data!.items.map((p) => (
                <Card key={p.id} onClick={() => void load(p, setViewing)}>
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <div className="flex items-center gap-2">
                        <span className="truncate text-sm font-semibold">{p.name}</span>
                        {!p.isActive && <Chip tone="danger">Inactive</Chip>}
                      </div>
                      {config.hasCompany && p.companyName && (
                        <div className="truncate text-xs text-text-dim">{p.companyName}</div>
                      )}
                      <div className="truncate text-xs text-text-dim">
                        {p.mobile ?? "—"}
                      </div>
                      {!config.hasCompany && p.email && (
                        <div className="truncate text-xs text-text-dim">{p.email}</div>
                      )}
                    </div>
                    {config.hasType && p.type && <Chip tone="brand">{p.type}</Chip>}
                  </div>
                  {canManage && (
                    <div className="mt-3 flex gap-2 border-t border-border pt-3">
                      <Button variant="ghost" className="min-h-11 flex-1 py-2 text-xs"
                        onClick={(e) => { e.stopPropagation(); void load(p, setEditing); }}>Edit</Button>
                      <Button variant="ghost" className="min-h-11 flex-1 py-2 text-xs"
                        onClick={(e) => { e.stopPropagation(); setConfirm({ party: p, activate: !p.isActive }); }}>
                        {p.isActive ? "Deactivate" : "Reactivate"}
                      </Button>
                    </div>
                  )}
                </Card>
              ))}
            </div>

            {/* Desktop: table */}
            <div className="hidden lg:block">
              <TableWrap>
                <thead>
                  <tr>
                    <Th>{config.singular === "contractor" ? "Contractor" : "Customer"}</Th>
                    {config.hasCompany && <Th>Company</Th>}
                    <Th>Mobile</Th>
                    {config.hasType ? <Th>Contractor Type</Th> : <Th>Email</Th>}
                    {!config.hasCompany && <Th>GSTIN</Th>}
                    <Th>Status</Th>
                    <Th className="text-right">Actions</Th>
                  </tr>
                </thead>
                <tbody>
                  {data!.items.map((p) => (
                    <tr key={p.id} className={p.isActive ? "" : "opacity-60"}>
                      <Td className="font-medium">{p.name}</Td>
                      {config.hasCompany && <Td>{p.companyName ?? "—"}</Td>}
                      <Td className="text-xs">{p.mobile ?? "—"}</Td>
                      {config.hasType
                        ? <Td className="text-xs">{p.type ?? "—"}</Td>
                        : <Td className="text-xs">{p.email ?? "—"}</Td>}
                      {!config.hasCompany && <Td className="font-mono text-xs">{p.gstin ?? "—"}</Td>}
                      <Td>{p.isActive ? <Chip tone="ok">Active</Chip> : <Chip tone="danger">Inactive</Chip>}</Td>
                      <Td className="whitespace-nowrap text-right">
                        <button className="px-1.5 text-xs text-brand-ink underline-offset-2 hover:underline"
                          onClick={() => void load(p, setViewing)}>View</button>
                        {canManage && (
                          <>
                            <button className="px-1.5 text-xs text-brand-ink underline-offset-2 hover:underline"
                              onClick={() => void load(p, setEditing)}>Edit</button>
                            <button className="px-1.5 text-xs text-text-dim underline-offset-2 hover:underline"
                              onClick={() => setConfirm({ party: p, activate: !p.isActive })}>
                              {p.isActive ? "Deactivate" : "Reactivate"}
                            </button>
                          </>
                        )}
                      </Td>
                    </tr>
                  ))}
                </tbody>
              </TableWrap>
            </div>

            {pages > 1 && (
              <div className="flex items-center justify-between px-1">
                <Button variant="ghost" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Previous</Button>
                <span className="text-xs text-text-dim">Page {page} of {pages}</span>
                <Button variant="ghost" disabled={page >= pages} onClick={() => setPage((p) => p + 1)}>Next</Button>
              </div>
            )}
          </>
        )}

      {(creating || editing) && (
        <PartyForm
          config={config}
          party={editing}
          onClose={() => { setCreating(false); setEditing(null); }}
          onSaved={() => { setCreating(false); setEditing(null); refreshAll(); }}
        />
      )}

      {viewing && (
        <PartyView
          config={config}
          party={viewing}
          onClose={() => setViewing(null)}
          onEdit={canManage ? () => { const p = viewing; setViewing(null); void load(p, setEditing); } : undefined}
        />
      )}

      <Confirm
        open={confirm !== null}
        title={confirm?.activate ? `Reactivate ${config.singular}?` : `Deactivate ${config.singular}?`}
        body={confirm?.activate ? config.reactivateBody : config.deactivateBody}
        confirmLabel={confirm?.activate ? "Reactivate" : "Deactivate"}
        danger={!confirm?.activate}
        onConfirm={() => void runLifecycle()}
        onCancel={() => setConfirm(null)}
      />
    </div>
  );
}

// ---------------------------------------------------------------------------

function PartyForm({ config, party, onClose, onSaved }: {
  config: PartyMasterConfig;
  party: PartyDetail | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const isEdit = party !== null;
  const [draft, setDraft] = useState<Draft>(() => party
    ? {
        name: party.name, companyName: party.companyName ?? "",
        type: party.type ?? "", mobile: party.mobile ?? "", email: party.email ?? "",
        address: party.address ?? "", pan: party.pan ?? "", gstin: party.gstin ?? "",
        bankDetails: party.bankDetails ?? "", notes: party.notes ?? "",
      }
    : emptyDraft);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  const set = <K extends keyof Draft>(k: K, v: Draft[K]) => setDraft((d) => ({ ...d, [k]: v }));
  const blank = (s: string) => (s.trim() === "" ? null : s.trim());

  const submit = async () => {
    setSaving(true);
    setError(null);
    const body: SavePartyBody = {
      name: draft.name.trim(),
      companyName: config.hasCompany ? blank(draft.companyName) : null,
      type: config.hasType ? blank(draft.type) : null,
      mobile: blank(draft.mobile),
      email: blank(draft.email),
      address: blank(draft.address),
      pan: blank(draft.pan),
      gstin: blank(draft.gstin),
      bankDetails: config.hasBankDetails ? blank(draft.bankDetails) : null,
      notes: blank(draft.notes),
    };
    try {
      if (isEdit) await api(`/${config.resource}/${party!.id}`, { method: "PUT", body });
      else await api(`/${config.resource}`, { method: "POST", body });
      onSaved();
    } catch (e) {
      setError(e as ApiError);
    } finally {
      setSaving(false);
    }
  };

  const canSubmit = draft.name.trim();
  const label = config.singular.charAt(0).toUpperCase() + config.singular.slice(1);

  return (
    <FormSheet
      open
      onClose={onClose}
      title={isEdit ? `Edit ${label}` : `Add ${label}`}
      subtitle={isEdit ? party!.name : `New `}
      footer={
        <div className="flex gap-2">
          <Button variant="ghost" className="flex-1" onClick={onClose} disabled={saving}>Cancel</Button>
          <Button className="flex-1" onClick={() => void submit()} disabled={saving || !canSubmit}>
            {saving ? "Saving…" : isEdit ? "Save Changes" : `Create ${label}`}
          </Button>
        </div>
      }
    >
      <ErrorText error={error && { message: error.message, errors: error.errors }} />

      <FormSection title="Basic Information">
        <Field label="Name *">
          <Input value={draft.name} onChange={(e) => set("name", e.target.value)} />
        </Field>
        {config.hasCompany && (
          <Field label="Company Name">
            <Input value={draft.companyName} onChange={(e) => set("companyName", e.target.value)} />
          </Field>
        )}
        {config.hasType && (
          <Field label="Contractor Type">
            <Input value={draft.type} onChange={(e) => set("type", e.target.value)}
              placeholder="Plumbing, Electrical, Civil…" />
          </Field>
        )}
      </FormSection>

      <FormSection title="Contact Information">
        <Field label="Mobile">
          <Input value={draft.mobile} onChange={(e) => set("mobile", e.target.value)} inputMode="tel" />
        </Field>
        <Field label="Email">
          <Input value={draft.email} onChange={(e) => set("email", e.target.value)} inputMode="email" />
        </Field>
        <Field label="Address">
          <Input value={draft.address} onChange={(e) => set("address", e.target.value)} />
        </Field>
      </FormSection>

      <FormSection title="Tax Information">
        <Field label="PAN">
          <Input value={draft.pan} onChange={(e) => set("pan", e.target.value)} placeholder="ABCDE1234F" />
        </Field>
        <Field label="GSTIN">
          <Input value={draft.gstin} onChange={(e) => set("gstin", e.target.value)} placeholder="29ABCDE1234F1Z5" />
        </Field>
      </FormSection>

      {config.hasBankDetails && (
        <FormSection title="Bank Information">
          <div className="sm:col-span-2">
            <Field label="Bank Details">
              <Input value={draft.bankDetails} onChange={(e) => set("bankDetails", e.target.value)}
                placeholder="Account number · IFSC · Bank" />
            </Field>
          </div>
        </FormSection>
      )}

      <FormSection title="Additional Information">
        <div className="sm:col-span-2">
          <Field label="Notes">
            <Input value={draft.notes} onChange={(e) => set("notes", e.target.value)} />
          </Field>
        </div>
      </FormSection>
    </FormSheet>
  );
}

// ---------------------------------------------------------------------------

function PartyView({ config, party, onClose, onEdit }: {
  config: PartyMasterConfig; party: PartyDetail; onClose: () => void; onEdit?: () => void;
}) {
  const usage = party.usage;
  const label = config.singular.charAt(0).toUpperCase() + config.singular.slice(1);

  // Read-only counts from the existing relationships — no accounting is computed here.
  const usageRows = useMemo(() => config.hasCompany
    ? [["Contracts", usage.contracts], ["Contractor payments", usage.contractorPayments]] as const
    : [["Projects", usage.projects], ["Customer payments", usage.customerPayments]] as const,
    [config.hasCompany, usage]);

  return (
    <FormSheet
      open
      onClose={onClose}
      title={party.name}
      subtitle={party.companyName ?? undefined}
      footer={onEdit && <Button className="w-full" onClick={onEdit}>Edit {label}</Button>}
    >
      <div className="mb-4 flex flex-wrap items-center gap-2">
        {party.isActive ? <Chip tone="ok">Active</Chip> : <Chip tone="danger">Inactive</Chip>}
        {party.type && <Chip tone="brand">{party.type}</Chip>}
      </div>

      <FormSection title="Identity">
        <div className="sm:col-span-2">
          <DetailRow label="Name" value={party.name} />
          {config.hasCompany && <DetailRow label="Company Name" value={party.companyName} />}
          {config.hasType && <DetailRow label="Contractor Type" value={party.type} />}
        </div>
      </FormSection>

      <FormSection title="Contact">
        <div className="sm:col-span-2">
          <DetailRow label="Mobile" value={party.mobile} />
          <DetailRow label="Email" value={party.email} />
          <DetailRow label="Address" value={party.address} />
        </div>
      </FormSection>

      <FormSection title="Tax">
        <div className="sm:col-span-2">
          <DetailRow label="PAN" value={party.pan} />
          <DetailRow label="GSTIN" value={party.gstin} />
        </div>
      </FormSection>

      {config.hasBankDetails && (
        <FormSection title="Bank">
          <div className="sm:col-span-2">
            <DetailRow label="Bank Details" value={party.bankDetails} />
          </div>
        </FormSection>
      )}

      <FormSection title="Usage" hint="Counts of existing records — deactivation never changes these.">
        <div className="sm:col-span-2">
          {usageRows.map(([label_, count]) => (
            <DetailRow key={label_} label={label_} value={num(count)} />
          ))}
        </div>
      </FormSection>

      {party.notes && (
        <FormSection title="Additional Information">
          <div className="sm:col-span-2">
            <DetailRow label="Notes" value={party.notes} />
          </div>
        </FormSection>
      )}
    </FormSheet>
  );
}
