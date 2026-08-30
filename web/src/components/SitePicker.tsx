import { api } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { Select } from "@/components/ui";
import type { Paged, Site } from "@/lib/types";

/** Remembers the last-chosen site in localStorage so stock screens open where you left off. */
export function useSites() {
  return useAsync(() => api<Paged<Site>>("/sites", { query: { pageSize: 100 } }), []);
}

export function SitePicker({ value, onChange, sites }: { value: string; onChange: (id: string) => void; sites: Site[] }) {
  return (
    <Select
      value={value}
      onChange={(e) => {
        onChange(e.target.value);
        try { localStorage.setItem("swk.site", e.target.value); } catch { /* ignore */ }
      }}
    >
      <option value="">Select a site…</option>
      {sites.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
    </Select>
  );
}

export function lastSite(): string {
  try { return localStorage.getItem("swk.site") ?? ""; } catch { return ""; }
}
