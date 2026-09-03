import { useEffect, useMemo, useRef, useState } from "react";
import { api } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { Select } from "@/components/ui";
import type { Category, Material, Paged } from "@/lib/types";

/**
 * Choosing a material in two steps: the trade, then the item.
 *
 * Two short dropdowns beat one long one for the person this app is for. Nine categories is a list
 * you read in a glance, and picking one cuts the second list to the few dozen things that trade
 * actually buys — so neither control ever asks someone to scroll past four hundred rows. On a phone
 * these open as the native wheel, which needs no typing at all.
 *
 * Typing is still there for whoever already knows the name: "Search by name" swaps both dropdowns
 * for one box that matches on name, brand, category and type.
 */
export function MaterialPicker({ value, onChange, autoFocus }: {
  /** The chosen material, or null. Held by the caller so a row can be re-rendered. */
  value: Material | null;
  onChange: (material: Material | null) => void;
  autoFocus?: boolean;
}) {
  const [categoryId, setCategoryId] = useState("");
  const [searching, setSearching] = useState(false);

  const { data: categories } = useAsync(() => api<Category[]>("/material-categories"), []);
  const activeCategories = useMemo(
    () => (categories ?? []).filter((c) => c.isActive).sort((a, b) => a.sortOrder - b.sortOrder),
    [categories],
  );

  // Only the chosen trade's materials — the whole point of asking for the category first.
  const { data: inCategory, loading } = useAsync(
    () => (categoryId
      ? api<Paged<Material>>("/materials", { query: { categoryId, active: true, pageSize: 500 } })
      : Promise.resolve(null)),
    [categoryId],
  );

  // ---- chosen: a settled row, not a control still asking a question ----
  if (value) {
    return (
      <button
        type="button"
        onClick={() => onChange(null)}
        className="flex w-full items-center justify-between gap-3 rounded-xl border border-border bg-surface-2 px-3 py-2.5 text-left"
      >
        <span className="min-w-0">
          <span className="block truncate text-sm font-semibold">{value.name}</span>
          <span className="block truncate text-xs text-text-dim">
            {value.brand ? `${value.brand} · ` : ""}{value.categoryName} / {value.subcategoryName} · {value.unitCode}
          </span>
        </span>
        <span className="shrink-0 text-xs text-brand-ink">Change</span>
      </button>
    );
  }

  if (searching) {
    return (
      <div className="space-y-1">
        <MaterialSearch onPick={onChange} autoFocus />
        <button
          type="button"
          onClick={() => setSearching(false)}
          className="min-h-9 px-1 text-xs text-brand-ink underline-offset-2 hover:underline"
        >
          Pick from the lists instead
        </button>
      </div>
    );
  }

  const materials = inCategory?.items ?? [];

  return (
    <div className="space-y-2">
      <Select
        value={categoryId}
        autoFocus={autoFocus}
        onChange={(e) => setCategoryId(e.target.value)}
      >
        <option value="">Material category…</option>
        {activeCategories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
      </Select>

      <Select
        value=""
        disabled={!categoryId || loading}
        onChange={(e) => {
          const picked = materials.find((m) => m.id === e.target.value);
          if (picked) onChange(picked);
        }}
      >
        <option value="">
          {!categoryId ? "Choose a category first"
            : loading ? "Loading…"
            : materials.length === 0 ? "Nothing in this category yet"
            : `Material name… (${materials.length})`}
        </option>
        {materials.map((m) => (
          <option key={m.id} value={m.id}>
            {m.name}{m.brand ? ` · ${m.brand}` : ""} ({m.unitCode})
          </option>
        ))}
      </Select>

      <button
        type="button"
        onClick={() => setSearching(true)}
        className="min-h-9 px-1 text-xs text-brand-ink underline-offset-2 hover:underline"
      >
        Search by name instead
      </button>
    </div>
  );
}

/** One box that matches on name, brand, category and type. For people who know what they want. */
function MaterialSearch({ onPick, autoFocus }: {
  onPick: (m: Material) => void; autoFocus?: boolean;
}) {
  const [term, setTerm] = useState("");
  const [debounced, setDebounced] = useState("");
  const [open, setOpen] = useState(true);
  const box = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const t = setTimeout(() => setDebounced(term.trim()), 200);
    return () => clearTimeout(t);
  }, [term]);

  useEffect(() => {
    if (!open) return;
    const away = (e: PointerEvent) => {
      if (box.current && !box.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("pointerdown", away);
    return () => document.removeEventListener("pointerdown", away);
  }, [open]);

  const { data, loading } = useAsync(
    () => (debounced
      ? api<Paged<Material>>("/materials", { query: { q: debounced, active: true, pageSize: 30 } })
      : Promise.resolve(null)),
    [debounced],
  );
  const items = data?.items ?? [];

  return (
    <div ref={box} className="relative">
      <input
        className="w-full rounded-xl border border-border bg-surface-2 px-3 py-2.5 text-base outline-none focus:border-brand sm:text-sm"
        value={term}
        autoFocus={autoFocus}
        autoCapitalize="none"
        autoCorrect="off"
        placeholder="Type a material — cement, tile, wire…"
        onChange={(e) => { setTerm(e.target.value); setOpen(true); }}
        onFocus={() => setOpen(true)}
      />
      {open && debounced.length > 0 && (
        <div className="absolute inset-x-0 top-full z-30 mt-1 max-h-72 overflow-y-auto rounded-xl border border-border bg-surface shadow-lg">
          {loading ? (
            <div className="px-3 py-4 text-sm text-text-dim">Searching…</div>
          ) : items.length === 0 ? (
            <div className="px-3 py-4 text-sm text-text-dim">
              Nothing matches “{debounced}”. Try a shorter word, or add it under More → Materials.
            </div>
          ) : (
            <ul>
              {items.map((m) => (
                <li key={m.id}>
                  <button
                    type="button"
                    onClick={() => { onPick(m); setOpen(false); }}
                    className="flex w-full items-center justify-between gap-3 border-b border-border/60 px-3 py-2.5 text-left last:border-0 hover:bg-surface-2"
                  >
                    <span className="min-w-0">
                      <span className="block truncate text-sm font-medium">{m.name}</span>
                      <span className="block truncate text-xs text-text-dim">
                        {m.brand ? `${m.brand} · ` : ""}{m.categoryName} / {m.subcategoryName}
                      </span>
                    </span>
                    <span className="shrink-0 text-xs text-text-dim">{m.unitCode}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}
