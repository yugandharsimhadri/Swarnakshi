import { useEffect, useMemo, useRef, useState } from "react";
import { api } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import type { Category, Material, Paged } from "@/lib/types";

/**
 * Choosing a material, the way someone standing at a gate with a delivery note would.
 *
 * They type. "cem" finds the cements, "16" finds 16mm TMT, "ultratech" finds the brand. The
 * category chips are there for the other case — you know it is something plumbing but not what it
 * is called — and they narrow the same search rather than replacing it. There is no long dropdown
 * anywhere in this flow, which is the point: a `<select>` of four hundred materials is a wall.
 */
export function MaterialPicker({ value, onChange, placeholder, autoFocus }: {
  /** The chosen material, or null. Kept by the caller so a row can be re-rendered. */
  value: Material | null;
  onChange: (material: Material | null) => void;
  placeholder?: string;
  autoFocus?: boolean;
}) {
  const [term, setTerm] = useState("");
  const [debounced, setDebounced] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [open, setOpen] = useState(false);
  const box = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const t = setTimeout(() => setDebounced(term.trim()), 200);
    return () => clearTimeout(t);
  }, [term]);

  // Close when the tap lands elsewhere — on a phone there is no Escape key.
  useEffect(() => {
    if (!open) return;
    const away = (e: PointerEvent) => {
      if (box.current && !box.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("pointerdown", away);
    return () => document.removeEventListener("pointerdown", away);
  }, [open]);

  const { data: categories } = useAsync(() => api<Category[]>("/material-categories"), []);
  const activeCategories = useMemo(
    () => (categories ?? []).filter((c) => c.isActive).sort((a, b) => a.sortOrder - b.sortOrder),
    [categories],
  );

  // Only ask the server once there is something to narrow by; an unfiltered fetch of everything is
  // both slow and useless to look at.
  const { data: results, loading } = useAsync(
    () => (debounced || categoryId
      ? api<Paged<Material>>("/materials", {
          query: { q: debounced, categoryId, active: true, pageSize: 40 },
        })
      : Promise.resolve(null)),
    [debounced, categoryId],
  );

  const items = results?.items ?? [];
  const showPanel = open && (debounced.length > 0 || categoryId !== "" || items.length > 0);

  function choose(m: Material) {
    onChange(m);
    setTerm("");
    setOpen(false);
  }

  // ---- chosen: show it as a settled row, not a text box still asking a question ----
  if (value) {
    return (
      <button
        type="button"
        onClick={() => { onChange(null); setOpen(true); }}
        className="flex w-full items-center justify-between gap-3 rounded-xl border border-border bg-surface-2 px-3 py-2.5 text-left"
      >
        <span className="min-w-0">
          <span className="block truncate text-sm font-semibold">{value.name}</span>
          <span className="block truncate text-xs text-text-dim">
            {value.brand ? `${value.brand} · ` : ""}{value.subcategoryName} · {value.unitCode}
          </span>
        </span>
        <span className="shrink-0 text-xs text-brand-ink">Change</span>
      </button>
    );
  }

  return (
    <div ref={box} className="relative">
      <input
        className="w-full rounded-xl border border-border bg-surface-2 px-3 py-2.5 text-base outline-none focus:border-brand sm:text-sm"
        value={term}
        autoFocus={autoFocus}
        autoCapitalize="none"
        autoCorrect="off"
        placeholder={placeholder ?? "Type a material — cement, 16mm, tile…"}
        onChange={(e) => { setTerm(e.target.value); setOpen(true); }}
        onFocus={() => setOpen(true)}
      />

      {showPanel && (
        <div className="absolute inset-x-0 top-full z-30 mt-1 max-h-80 overflow-y-auto rounded-xl border border-border bg-surface shadow-lg">
          {/* Chips first: on a narrow screen they are what a thumb reaches. */}
          <div className="flex flex-wrap gap-1 border-b border-border p-2">
            <Chip active={categoryId === ""} onClick={() => setCategoryId("")}>All</Chip>
            {activeCategories.map((c) => (
              <Chip key={c.id} active={categoryId === c.id} onClick={() => setCategoryId(c.id)}>
                {c.name}
              </Chip>
            ))}
          </div>

          {loading ? (
            <div className="px-3 py-4 text-sm text-text-dim">Searching…</div>
          ) : items.length === 0 ? (
            <div className="px-3 py-4 text-sm text-text-dim">
              Nothing matches{debounced ? ` “${debounced}”` : ""}. Try a shorter word, or add it under
              More → Materials.
            </div>
          ) : (
            <ul>
              {items.map((m) => (
                <li key={m.id}>
                  <button
                    type="button"
                    onClick={() => choose(m)}
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

function Chip({ active, onClick, children }: {
  active: boolean; onClick: () => void; children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`rounded-full px-2.5 py-1 text-xs font-medium transition-colors ${
        active ? "bg-brand text-white" : "bg-surface-2 text-text-dim hover:text-text"
      }`}
    >
      {children}
    </button>
  );
}
