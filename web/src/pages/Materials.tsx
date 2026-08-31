import { useCallback, useEffect, useMemo, useState } from "react";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { money, num } from "@/lib/format";
import { useAuth } from "@/store/auth";
import {
  Button, Card, Chip, Confirm, DetailRow, EmptyState, ErrorText, Field, FormSection, FormSheet,
  Input, PageHeader, Select, SkeletonList, StatCard, TableWrap, Td, Th,
} from "@/components/ui";
import type {
  Category, Material, MaterialDetail, MaterialSiteStock, MaterialSummary, Paged,
  SaveMaterialBody, SpecDefinition, Subcategory, Unit,
} from "@/lib/types";

const PAGE_SIZE = 50;

type Draft = {
  code: string; name: string; materialSubcategoryId: string; brand: string;
  unitId: string; secondaryUnitId: string; conversionFactor: string; genericMeasurement: string;
  minStockLevel: string; reorderLevel: string; defaultPurchaseRate: string; gstRate: string;
  description: string; notes: string; specifications: Record<string, string>;
};

const emptyDraft: Draft = {
  code: "", name: "", materialSubcategoryId: "", brand: "",
  unitId: "", secondaryUnitId: "", conversionFactor: "", genericMeasurement: "",
  minStockLevel: "", reorderLevel: "", defaultPurchaseRate: "", gstRate: "",
  description: "", notes: "", specifications: {},
};

const numOrNull = (s: string) => (s.trim() === "" ? null : Number(s));
const numOrZero = (s: string) => (s.trim() === "" ? 0 : Number(s));

export default function Materials() {
  const canManage = useAuth((s) => s.can("masters.manage"));

  // ---- filters ---------------------------------------------------------
  const [q, setQ] = useState("");
  const [debouncedQ, setDebouncedQ] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [subcategoryId, setSubcategoryId] = useState("");
  const [brand, setBrand] = useState("");
  const [unitId, setUnitId] = useState("");
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);

  useEffect(() => {
    const t = setTimeout(() => { setDebouncedQ(q); setPage(1); }, 250);
    return () => clearTimeout(t);
  }, [q]);

  const filtersOn = Boolean(q || categoryId || subcategoryId || brand || unitId || status);
  const clearFilters = () => {
    setQ(""); setDebouncedQ(""); setCategoryId(""); setSubcategoryId("");
    setBrand(""); setUnitId(""); setStatus(""); setPage(1);
  };

  // ---- reference data --------------------------------------------------
  const { data: categories } = useAsync(() => api<Category[]>("/material-categories"), []);
  const { data: units } = useAsync(() => api<Unit[]>("/units"), []);
  const { data: brands, reload: reloadBrands } = useAsync(() => api<string[]>("/materials/brands"), []);
  const { data: allSubs } = useAsync(() => api<Subcategory[]>("/material-subcategories"), []);

  const activeCategories = useMemo(() => (categories ?? []).filter((c) => c.isActive), [categories]);
  const filterSubs = useMemo(
    () => (allSubs ?? []).filter((s) => s.isActive && (!categoryId || s.parentId === categoryId)),
    [allSubs, categoryId],
  );

  // ---- list ------------------------------------------------------------
  const { data: summary, reload: reloadSummary } = useAsync(
    () => api<MaterialSummary>("/materials/summary"), []);

  const { data, loading, error, reload } = useAsync(
    () => api<Paged<Material>>("/materials", {
      query: {
        q: debouncedQ, categoryId, subcategoryId, brand, unitId,
        active: status === "" ? undefined : status === "active",
        page, pageSize: PAGE_SIZE,
      },
    }),
    [debouncedQ, categoryId, subcategoryId, brand, unitId, status, page],
  );

  const refreshAll = useCallback(() => {
    reload(); reloadSummary(); reloadBrands();
  }, [reload, reloadSummary, reloadBrands]);

  // ---- dialogs ---------------------------------------------------------
  const [editing, setEditing] = useState<MaterialDetail | null>(null);
  const [creating, setCreating] = useState(false);
  const [viewing, setViewing] = useState<MaterialDetail | null>(null);
  const [confirm, setConfirm] = useState<{ material: Material; activate: boolean } | null>(null);
  const [rowError, setRowError] = useState<ApiError | null>(null);

  const openEdit = async (m: Material) => {
    setRowError(null);
    try { setEditing(await api<MaterialDetail>(`/materials/${m.id}`)); }
    catch (e) { setRowError(e as ApiError); }
  };
  const openView = async (m: Material) => {
    setRowError(null);
    try { setViewing(await api<MaterialDetail>(`/materials/${m.id}`)); }
    catch (e) { setRowError(e as ApiError); }
  };

  const runLifecycle = async () => {
    if (!confirm) return;
    const { material, activate } = confirm;
    setConfirm(null);
    setRowError(null);
    try {
      await api(`/materials/${material.id}/${activate ? "reactivate" : "deactivate"}`, { method: "POST" });
      refreshAll();
    } catch (e) { setRowError(e as ApiError); }
  };

  const total = data?.total ?? 0;
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <div className="space-y-3">
      <PageHeader
        title="Material Master"
        action={canManage && (
          <Button onClick={() => { setRowError(null); setCreating(true); }}>+ Add Material</Button>
        )}
      />
      <p className="-mt-2 px-1 text-xs text-text-dim">Construction materials catalogue</p>

      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        <StatCard label="Total Materials" value={num(summary?.total)} />
        <StatCard label="Active" value={num(summary?.active)} tone="ok" />
        <StatCard label="Inactive" value={num(summary?.inactive)} tone={summary?.inactive ? "warn" : undefined} />
        <StatCard label="Categories" value={num(summary?.categories)} />
      </div>

      <Card className="space-y-3">
        <Input
          placeholder="Search code, name, company, category or specification…"
          value={q}
          onChange={(e) => setQ(e.target.value)}
        />
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-5">
          <Select value={categoryId}
            onChange={(e) => { setCategoryId(e.target.value); setSubcategoryId(""); setPage(1); }}>
            <option value="">All categories</option>
            {activeCategories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </Select>
          <Select value={subcategoryId}
            onChange={(e) => { setSubcategoryId(e.target.value); setPage(1); }}>
            <option value="">All subcategories</option>
            {filterSubs.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </Select>
          <Select value={brand} onChange={(e) => { setBrand(e.target.value); setPage(1); }}>
            <option value="">All companies</option>
            {(brands ?? []).map((b) => <option key={b} value={b}>{b}</option>)}
          </Select>
          <Select value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }}>
            <option value="">Any status</option>
            <option value="active">Active</option>
            <option value="inactive">Inactive</option>
          </Select>
          <Select value={unitId} onChange={(e) => { setUnitId(e.target.value); setPage(1); }}>
            <option value="">Any unit</option>
            {(units ?? []).map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
          </Select>
        </div>
        {filtersOn && (
          <div className="flex items-center justify-between">
            <span className="text-xs text-text-dim">{num(total)} matching</span>
            <Button variant="ghost" onClick={clearFilters}>Clear Filters</Button>
          </div>
        )}
      </Card>

      <ErrorText error={rowError && { message: rowError.message, errors: rowError.errors }} />

      {loading ? <SkeletonList rows={6} />
        : error ? <ErrorText error={{ message: error.message, errors: error.errors }} />
        : total === 0 ? (
          <EmptyState
            title="No materials found"
            hint={filtersOn ? "Try clearing the filters." : "Add the first material to get started."}
          />
        ) : (
          <>
            {/* Mobile: compact cards */}
            <div className="space-y-2 lg:hidden">
              {data!.items.map((m) => (
                <Card key={m.id} onClick={() => void openView(m)}>
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <div className="flex items-center gap-2">
                        <span className="truncate text-sm font-semibold">{m.name}</span>
                        {!m.isActive && <Chip tone="danger">Inactive</Chip>}
                      </div>
                      {m.brand && <div className="truncate text-xs font-medium text-brand-ink">{m.brand}</div>}
                      {m.specSummary && <div className="truncate text-xs text-text-dim">{m.specSummary}</div>}
                      <div className="mt-0.5 truncate text-xs text-text-dim">
                        {m.code} · {m.categoryName} / {m.subcategoryName}
                      </div>
                    </div>
                    <div className="shrink-0 text-right text-xs">
                      <div className="font-medium">{money(m.defaultPurchaseRate)}</div>
                      <div className="text-text-dim">/{m.unitCode}</div>
                    </div>
                  </div>
                  {canManage && (
                    <div className="mt-3 flex gap-2 border-t border-border pt-3">
                      <Button variant="ghost" className="min-h-11 flex-1 py-2 text-xs"
                        onClick={(e) => { e.stopPropagation(); void openEdit(m); }}>Edit</Button>
                      <Button variant="ghost" className="min-h-11 flex-1 py-2 text-xs"
                        onClick={(e) => { e.stopPropagation(); setConfirm({ material: m, activate: !m.isActive }); }}>
                        {m.isActive ? "Deactivate" : "Reactivate"}
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
                    <Th>Code</Th><Th>Material</Th><Th>Company</Th><Th>Category</Th>
                    <Th>Specification</Th><Th>Unit</Th>
                    <Th className="text-right">Default Rate</Th><Th className="text-right">GST</Th>
                    <Th>Status</Th><Th className="text-right">Actions</Th>
                  </tr>
                </thead>
                <tbody>
                  {data!.items.map((m) => (
                    <tr key={m.id} className={m.isActive ? "" : "opacity-60"}>
                      <Td className="font-mono text-xs">{m.code}</Td>
                      <Td className="font-medium">{m.name}</Td>
                      <Td>{m.brand ?? "—"}</Td>
                      <Td className="text-xs text-text-dim">
                        {m.categoryName}
                        <div>{m.subcategoryName}</div>
                      </Td>
                      <Td className="text-xs">{m.specSummary ?? "—"}</Td>
                      <Td className="text-xs">{m.unitCode}</Td>
                      <Td className="text-right tabular-nums">{money(m.defaultPurchaseRate)}</Td>
                      <Td className="text-right tabular-nums text-xs">
                        {m.gstRate == null ? "—" : `${m.gstRate}%`}
                      </Td>
                      <Td>{m.isActive ? <Chip tone="ok">Active</Chip> : <Chip tone="danger">Inactive</Chip>}</Td>
                      <Td className="text-right whitespace-nowrap">
                        <button className="px-1.5 text-xs text-brand-ink underline-offset-2 hover:underline"
                          onClick={() => void openView(m)}>View</button>
                        {canManage && (
                          <>
                            <button className="px-1.5 text-xs text-brand-ink underline-offset-2 hover:underline"
                              onClick={() => void openEdit(m)}>Edit</button>
                            <button className="px-1.5 text-xs text-text-dim underline-offset-2 hover:underline"
                              onClick={() => setConfirm({ material: m, activate: !m.isActive })}>
                              {m.isActive ? "Deactivate" : "Reactivate"}
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
        <MaterialForm
          material={editing}
          categories={activeCategories}
          units={units ?? []}
          onClose={() => { setCreating(false); setEditing(null); }}
          onSaved={() => { setCreating(false); setEditing(null); refreshAll(); }}
        />
      )}

      {viewing && (
        <MaterialView
          material={viewing}
          onClose={() => setViewing(null)}
          onEdit={canManage ? () => { const m = viewing; setViewing(null); void openEdit(m); } : undefined}
        />
      )}

      <Confirm
        open={confirm !== null}
        title={confirm?.activate ? "Reactivate material?" : "Deactivate material?"}
        body={confirm?.activate
          ? "The material becomes available again for new purchases and material requests. Historical data is unchanged."
          : "This material will no longer be available for new material purchases or requests. Existing transaction history will remain unchanged."}
        confirmLabel={confirm?.activate ? "Reactivate" : "Deactivate"}
        danger={!confirm?.activate}
        onConfirm={() => void runLifecycle()}
        onCancel={() => setConfirm(null)}
      />
    </div>
  );
}

// ---------------------------------------------------------------------------
// Add / Edit
// ---------------------------------------------------------------------------

function MaterialForm({ material, categories, units, onClose, onSaved }: {
  material: MaterialDetail | null;
  categories: Category[];
  units: Unit[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const isEdit = material !== null;

  const [categoryId, setCategoryId] = useState(material?.materialCategoryId ?? "");
  const [draft, setDraft] = useState<Draft>(() => material
    ? {
        code: material.code, name: material.name,
        materialSubcategoryId: material.materialSubcategoryId, brand: material.brand ?? "",
        unitId: material.unitId, secondaryUnitId: material.secondaryUnitId ?? "",
        conversionFactor: material.conversionFactor?.toString() ?? "",
        genericMeasurement: material.genericMeasurement ?? "",
        minStockLevel: material.minStockLevel.toString(),
        reorderLevel: material.reorderLevel.toString(),
        defaultPurchaseRate: material.defaultPurchaseRate.toString(),
        gstRate: material.gstRate?.toString() ?? "",
        description: material.description ?? "", notes: material.notes ?? "",
        specifications: Object.fromEntries(material.specifications.map((s) => [s.key, s.value])),
      }
    : emptyDraft);

  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  const set = <K extends keyof Draft>(k: K, v: Draft[K]) => setDraft((d) => ({ ...d, [k]: v }));

  const { data: subs } = useAsync(
    () => categoryId ? api<Subcategory[]>("/material-subcategories", { query: { categoryId } })
                     : Promise.resolve([] as Subcategory[]),
    [categoryId],
  );

  // Specification fields are declared by the subcategory — nothing irrelevant is ever shown.
  const { data: specDefs } = useAsync(
    () => draft.materialSubcategoryId
      ? api<SpecDefinition[]>("/materials/spec-definitions", { query: { subcategoryId: draft.materialSubcategoryId } })
      : Promise.resolve([] as SpecDefinition[]),
    [draft.materialSubcategoryId],
  );

  const submit = async () => {
    setSaving(true);
    setError(null);
    const body: SaveMaterialBody = {
      code: draft.code.trim(),
      name: draft.name.trim(),
      materialSubcategoryId: draft.materialSubcategoryId,
      brand: draft.brand.trim() || null,
      unitId: draft.unitId,
      secondaryUnitId: draft.secondaryUnitId || null,
      conversionFactor: draft.secondaryUnitId ? numOrNull(draft.conversionFactor) : null,
      genericMeasurement: draft.genericMeasurement.trim() || null,
      minStockLevel: numOrZero(draft.minStockLevel),
      reorderLevel: numOrZero(draft.reorderLevel),
      defaultPurchaseRate: numOrZero(draft.defaultPurchaseRate),
      gstRate: numOrNull(draft.gstRate),
      description: draft.description.trim() || null,
      notes: draft.notes.trim() || null,
      specifications: Object.fromEntries(
        Object.entries(draft.specifications).map(([k, v]) => [k, v.trim() === "" ? null : v.trim()]),
      ),
    };
    try {
      if (isEdit) await api(`/materials/${material!.id}`, { method: "PUT", body });
      else await api("/materials", { method: "POST", body });
      onSaved();
    } catch (e) {
      setError(e as ApiError);
    } finally {
      setSaving(false);
    }
  };

  const canSubmit = draft.code.trim() && draft.name.trim() && draft.materialSubcategoryId && draft.unitId;

  return (
    <FormSheet
      open
      onClose={onClose}
      title={isEdit ? "Edit Material" : "Add Material"}
      subtitle={isEdit ? `${material!.code} · ${material!.name}` : "Define an exact purchasable material"}
      footer={
        <div className="flex gap-2">
          <Button variant="ghost" className="flex-1" onClick={onClose} disabled={saving}>Cancel</Button>
          <Button className="flex-1" onClick={() => void submit()} disabled={saving || !canSubmit}>
            {saving ? "Saving…" : isEdit ? "Save Changes" : "Create Material"}
          </Button>
        </div>
      }
    >
      <ErrorText error={error && { message: error.message, errors: error.errors }} />

      <FormSection title="Basic Information">
        <Field label="Material Code *"
          error={isEdit && material!.codeLocked ? "Locked — this material has transaction history." : undefined}>
          <Input value={draft.code} onChange={(e) => set("code", e.target.value)}
            disabled={isEdit && material!.codeLocked} placeholder="ELE-WIR-POL-025" />
        </Field>
        <Field label="Material Name *">
          <Input value={draft.name} onChange={(e) => set("name", e.target.value)} placeholder="Electrical Wire" />
        </Field>
        <Field label="Category *">
          <Select value={categoryId}
            onChange={(e) => { setCategoryId(e.target.value); set("materialSubcategoryId", ""); set("specifications", {}); }}>
            <option value="">Select category…</option>
            {categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </Select>
        </Field>
        <Field label="Subcategory *">
          <Select value={draft.materialSubcategoryId} disabled={!categoryId}
            onChange={(e) => { set("materialSubcategoryId", e.target.value); set("specifications", {}); }}>
            <option value="">{categoryId ? "Select subcategory…" : "Select a category first"}</option>
            {(subs ?? []).filter((s) => s.isActive).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </Select>
        </Field>
        <Field label="Company / Brand">
          <Input value={draft.brand} onChange={(e) => set("brand", e.target.value)} placeholder="Polycab" />
        </Field>
      </FormSection>

      <FormSection
        title="Specifications"
        hint={!draft.materialSubcategoryId
          ? "Choose a subcategory to see its specification fields."
          : (specDefs ?? []).length === 0
            ? "This subcategory does not define any specification fields."
            : undefined}
      >
        {(specDefs ?? []).map((d) => (
          <Field key={d.id} label={d.isRequired ? `${d.label} *` : d.label}>
            {d.kind === 3 ? (
              <Select value={draft.specifications[d.key] ?? ""}
                onChange={(e) => set("specifications", { ...draft.specifications, [d.key]: e.target.value })}>
                <option value="">—</option>
                {d.options.map((o) => <option key={o} value={o}>{o}</option>)}
              </Select>
            ) : (
              <Input
                type={d.kind === 2 ? "number" : "text"}
                inputMode={d.kind === 2 ? "decimal" : undefined}
                value={draft.specifications[d.key] ?? ""}
                onChange={(e) => set("specifications", { ...draft.specifications, [d.key]: e.target.value })}
              />
            )}
          </Field>
        ))}
      </FormSection>

      <FormSection title="Measurement">
        <Field label="Primary Unit *">
          <Select value={draft.unitId} onChange={(e) => set("unitId", e.target.value)}>
            <option value="">Select unit…</option>
            {units.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
          </Select>
        </Field>
        <Field label="Secondary Unit">
          <Select value={draft.secondaryUnitId} onChange={(e) => set("secondaryUnitId", e.target.value)}>
            <option value="">None</option>
            {units.filter((u) => u.id !== draft.unitId).map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
          </Select>
        </Field>
        <Field label="Conversion Factor"
          error={draft.secondaryUnitId && !draft.conversionFactor ? "Required with a secondary unit." : undefined}>
          <Input type="number" inputMode="decimal" disabled={!draft.secondaryUnitId}
            value={draft.conversionFactor} onChange={(e) => set("conversionFactor", e.target.value)}
            placeholder="90" />
        </Field>
        <Field label="Generic Measurement">
          <Input value={draft.genericMeasurement} onChange={(e) => set("genericMeasurement", e.target.value)}
            placeholder="90 Meter / Coil" />
        </Field>
      </FormSection>

      <FormSection title="Commercial Information" hint="Default rate is a reference only — inventory is valued from actual purchase rates.">
        <Field label="Default Purchase Rate">
          <Input type="number" inputMode="decimal" value={draft.defaultPurchaseRate}
            onChange={(e) => set("defaultPurchaseRate", e.target.value)} placeholder="55" />
        </Field>
        <Field label="GST %">
          <Input type="number" inputMode="decimal" value={draft.gstRate}
            onChange={(e) => set("gstRate", e.target.value)} placeholder="18" />
        </Field>
      </FormSection>

      <FormSection title="Inventory Controls" hint="Reference levels only. Current stock is held per site.">
        <Field label="Minimum Stock Level">
          <Input type="number" inputMode="decimal" value={draft.minStockLevel}
            onChange={(e) => set("minStockLevel", e.target.value)} placeholder="0" />
        </Field>
        <Field label="Reorder Level">
          <Input type="number" inputMode="decimal" value={draft.reorderLevel}
            onChange={(e) => set("reorderLevel", e.target.value)} placeholder="0" />
        </Field>
      </FormSection>

      <FormSection title="Additional Information">
        <Field label="Description">
          <Input value={draft.description} onChange={(e) => set("description", e.target.value)} />
        </Field>
        <Field label="Notes">
          <Input value={draft.notes} onChange={(e) => set("notes", e.target.value)} />
        </Field>
      </FormSection>
    </FormSheet>
  );
}

// ---------------------------------------------------------------------------
// View
// ---------------------------------------------------------------------------

function MaterialView({ material, onClose, onEdit }: {
  material: MaterialDetail; onClose: () => void; onEdit?: () => void;
}) {
  // Stock comes from the inventory service — never stored on the material.
  const { data: stock, loading } = useAsync(
    () => api<MaterialSiteStock[]>(`/materials/${material.id}/stock`), [material.id]);

  return (
    <FormSheet
      open
      onClose={onClose}
      title={material.name}
      subtitle={`${material.code} · ${material.categoryName} / ${material.subcategoryName}`}
      footer={onEdit && <Button className="w-full" onClick={onEdit}>Edit Material</Button>}
    >
      <div className="mb-4 flex flex-wrap items-center gap-2">
        {material.isActive ? <Chip tone="ok">Active</Chip> : <Chip tone="danger">Inactive</Chip>}
        {material.brand && <Chip tone="brand">{material.brand}</Chip>}
        {material.specSummary && <Chip>{material.specSummary}</Chip>}
      </div>

      <FormSection title="Identity">
        <div className="sm:col-span-2">
          <DetailRow label="Material Code" value={<span className="font-mono">{material.code}</span>} />
          <DetailRow label="Material Name" value={material.name} />
          <DetailRow label="Company / Brand" value={material.brand} />
          <DetailRow label="Category" value={material.categoryName} />
          <DetailRow label="Subcategory" value={material.subcategoryName} />
        </div>
      </FormSection>

      {material.specifications.length > 0 && (
        <FormSection title="Specifications">
          <div className="sm:col-span-2">
            {material.specifications.map((s) => <DetailRow key={s.definitionId} label={s.label} value={s.value} />)}
          </div>
        </FormSection>
      )}

      <FormSection title="Measurement">
        <div className="sm:col-span-2">
          <DetailRow label="Primary Unit" value={material.unitCode} />
          <DetailRow label="Secondary Unit" value={material.secondaryUnitCode} />
          <DetailRow label="Conversion Factor" value={material.conversionFactor} />
          <DetailRow label="Generic Measurement" value={material.genericMeasurement} />
        </div>
      </FormSection>

      <FormSection title="Commercial & Inventory">
        <div className="sm:col-span-2">
          <DetailRow label="Default Purchase Rate" value={money(material.defaultPurchaseRate)} />
          <DetailRow label="GST" value={material.gstRate == null ? null : `${material.gstRate}%`} />
          <DetailRow label="Minimum Stock Level" value={num(material.minStockLevel)} />
          <DetailRow label="Reorder Level" value={num(material.reorderLevel)} />
        </div>
      </FormSection>

      <FormSection title="Stock by Site" hint="Live from inventory — never stored on the material.">
        <div className="sm:col-span-2">
          {loading ? <SkeletonList rows={2} />
            : (stock ?? []).length === 0
              ? <p className="py-2 text-sm text-text-dim">No stock recorded at any site.</p>
              : (stock ?? []).map((s) => (
                  <DetailRow key={s.siteId} label={s.siteName}
                    value={`${num(s.quantity)} ${material.unitCode} · ${money(s.value)}`} />
                ))}
        </div>
      </FormSection>

      {(material.description || material.notes) && (
        <FormSection title="Additional Information">
          <div className="sm:col-span-2">
            <DetailRow label="Description" value={material.description} />
            <DetailRow label="Notes" value={material.notes} />
          </div>
        </FormSection>
      )}
    </FormSheet>
  );
}
