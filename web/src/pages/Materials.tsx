import { useCallback, useEffect, useMemo, useState } from "react";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { useAuth } from "@/store/auth";
import {
  Button, Card, Chip, Confirm, EmptyState, ErrorText, Field, FormSheet, Input, PageHeader,
  Select, SkeletonList,
} from "@/components/ui";
import type {
  Category, Material, MaterialDetail, Paged, SaveMaterialBody, Subcategory, Unit,
} from "@/lib/types";

const PAGE_SIZE = 50;

/**
 * The catalogue, as a site person would describe an item out loud: what it is, what kind of thing
 * it is, and who makes it.
 *
 * It used to ask for a code, two units, a conversion factor, min stock, reorder level, a default
 * rate, GST and a free-text note before it would accept "Cement". Every one of those is either
 * derivable, set elsewhere, or nobody's job at the point of adding a material — purchases carry the
 * real rate and tax, inventory carries the real stock. So they are gone from this screen.
 */
export default function Materials() {
  const canManage = useAuth((s) => s.can("masters.manage"));

  const [q, setQ] = useState("");
  const [debouncedQ, setDebouncedQ] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);

  useEffect(() => {
    const t = setTimeout(() => { setDebouncedQ(q); setPage(1); }, 250);
    return () => clearTimeout(t);
  }, [q]);

  const filtersOn = Boolean(q || categoryId || status);

  const { data: categories } = useAsync(() => api<Category[]>("/material-categories"), []);
  const activeCategories = useMemo(() => (categories ?? []).filter((c) => c.isActive), [categories]);

  const { data, loading, error, reload } = useAsync(
    () => api<Paged<Material>>("/materials", {
      query: {
        q: debouncedQ, categoryId,
        active: status === "" ? undefined : status === "active",
        page, pageSize: PAGE_SIZE,
      },
    }),
    [debouncedQ, categoryId, status, page],
  );

  const [editing, setEditing] = useState<MaterialDetail | null>(null);
  const [creating, setCreating] = useState(false);
  const [confirm, setConfirm] = useState<{ material: Material; activate: boolean } | null>(null);
  const [rowError, setRowError] = useState<ApiError | null>(null);

  const refreshAll = useCallback(() => reload(), [reload]);

  const openEdit = async (m: Material) => {
    setRowError(null);
    try { setEditing(await api<MaterialDetail>(`/materials/${m.id}`)); }
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
        title="Materials"
        action={canManage && (
          <Button onClick={() => { setRowError(null); setCreating(true); }}>+ Add</Button>
        )}
      />

      <Input placeholder="Search materials…" value={q} onChange={(e) => setQ(e.target.value)} />
      <div className="grid grid-cols-2 gap-2">
        <Select value={categoryId} onChange={(e) => { setCategoryId(e.target.value); setPage(1); }}>
          <option value="">All categories</option>
          {activeCategories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </Select>
        <Select value={status} onChange={(e) => { setStatus(e.target.value); setPage(1); }}>
          <option value="">Any status</option>
          <option value="active">Active</option>
          <option value="inactive">Inactive</option>
        </Select>
      </div>

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
            <div className="space-y-2">
              {data!.items.map((m) => (
                <Card key={m.id}>
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <div className="flex items-center gap-2">
                        <span className="truncate text-sm font-semibold">{m.name}</span>
                        {!m.isActive && <Chip tone="danger">Inactive</Chip>}
                      </div>
                      {m.brand && <div className="truncate text-xs font-medium text-brand-ink">{m.brand}</div>}
                      <div className="mt-0.5 truncate text-xs text-text-dim">
                        {m.categoryName} / {m.subcategoryName}
                      </div>
                    </div>
                    <span className="shrink-0 text-xs text-text-dim">{m.unitCode}</span>
                  </div>
                  {canManage && (
                    <div className="mt-3 flex gap-2 border-t border-border pt-3">
                      <Button variant="ghost" className="min-h-11 flex-1 py-2 text-xs"
                        onClick={() => void openEdit(m)}>Edit</Button>
                      <Button variant="ghost" className="min-h-11 flex-1 py-2 text-xs"
                        onClick={() => setConfirm({ material: m, activate: !m.isActive })}>
                        {m.isActive ? "Deactivate" : "Reactivate"}
                      </Button>
                    </div>
                  )}
                </Card>
              ))}
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
          onClose={() => { setCreating(false); setEditing(null); }}
          onSaved={() => { setCreating(false); setEditing(null); refreshAll(); }}
        />
      )}

      <Confirm
        open={confirm !== null}
        title={confirm?.activate ? "Reactivate material?" : "Deactivate material?"}
        body={confirm?.activate
          ? "The material becomes available again for new purchases and requests. Past records are unchanged."
          : "This material will no longer appear when recording purchases or requests. Past records are unchanged."}
        confirmLabel={confirm?.activate ? "Reactivate" : "Deactivate"}
        danger={!confirm?.activate}
        onConfirm={() => void runLifecycle()}
        onCancel={() => setConfirm(null)}
      />
    </div>
  );
}

// ---------------------------------------------------------------------------
// Add / Edit — five fields, plus a unit only if you care.
// ---------------------------------------------------------------------------

function MaterialForm({ material, categories, onClose, onSaved }: {
  material: MaterialDetail | null;
  categories: Category[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const isEdit = material !== null;

  const [categoryId, setCategoryId] = useState(material?.materialCategoryId ?? "");
  const [subcategoryId, setSubcategoryId] = useState(material?.materialSubcategoryId ?? "");
  const [name, setName] = useState(material?.name ?? "");
  const [brand, setBrand] = useState(material?.brand ?? "");
  const [description, setDescription] = useState(material?.description ?? "");
  const [unitId, setUnitId] = useState(material?.unitId ?? "");
  const [showUnit, setShowUnit] = useState(false);

  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  const { data: units } = useAsync(() => api<Unit[]>("/units"), []);
  const { data: subs } = useAsync(
    () => categoryId
      ? api<Subcategory[]>("/material-subcategories", { query: { categoryId } })
      : Promise.resolve([] as Subcategory[]),
    [categoryId],
  );

  const submit = async () => {
    setSaving(true);
    setError(null);
    // Everything the old form asked for and this one does not is sent as its neutral value: the
    // server keeps the columns, nobody has to fill them in.
    const body: SaveMaterialBody = {
      name: name.trim(),
      materialSubcategoryId: subcategoryId,
      brand: brand.trim() || null,
      unitId: unitId || null,
      secondaryUnitId: null,
      conversionFactor: null,
      genericMeasurement: null,
      minStockLevel: 0,
      reorderLevel: 0,
      defaultPurchaseRate: 0,
      gstRate: null,
      description: description.trim() || null,
      notes: null,
      specifications: {},
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

  const canSubmit = Boolean(name.trim() && subcategoryId);

  return (
    <FormSheet
      open
      onClose={onClose}
      title={isEdit ? "Edit material" : "Add material"}
      subtitle={isEdit ? material!.name : undefined}
      footer={
        <div className="flex gap-2">
          <Button variant="ghost" className="flex-1" onClick={onClose} disabled={saving}>Cancel</Button>
          <Button className="flex-1" onClick={() => void submit()} disabled={saving || !canSubmit}>
            {saving ? "Saving…" : "Save"}
          </Button>
        </div>
      }
    >
      <ErrorText error={error && { message: error.message, errors: error.errors }} />

      <div className="space-y-3 sm:col-span-2">
        <Field label="Name">
          <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Cement" autoFocus />
        </Field>
        <Field label="Category">
          <Select value={categoryId} onChange={(e) => { setCategoryId(e.target.value); setSubcategoryId(""); }}>
            <option value="">Select category…</option>
            {categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </Select>
        </Field>
        <Field label="Subcategory">
          <Select value={subcategoryId} disabled={!categoryId} onChange={(e) => setSubcategoryId(e.target.value)}>
            <option value="">{categoryId ? "Select subcategory…" : "Pick a category first"}</option>
            {(subs ?? []).filter((s) => s.isActive).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </Select>
        </Field>
        <Field label="Company / brand">
          <Input value={brand} onChange={(e) => setBrand(e.target.value)} placeholder="Ultratech" />
        </Field>
        <Field label="Description">
          <Input value={description} onChange={(e) => setDescription(e.target.value)} placeholder="OPC 53 grade" />
        </Field>

        {/* Off by default: a unit is only worth a decision when the obvious one is wrong. */}
        {showUnit || unitId ? (
          <Field label="Unit of measure">
            <Select value={unitId} onChange={(e) => setUnitId(e.target.value)}>
              <option value="">Default</option>
              {(units ?? []).map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
            </Select>
          </Field>
        ) : (
          <button type="button" onClick={() => setShowUnit(true)}
            className="min-h-11 px-1 text-xs text-brand-ink underline-offset-2 hover:underline">
            Set a unit of measure
          </button>
        )}
      </div>
    </FormSheet>
  );
}
