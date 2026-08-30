import { useState } from "react";
import { api } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { money } from "@/lib/format";
import { Card, Chip, EmptyState, ErrorText, Input, PageHeader, Spinner } from "@/components/ui";
import type { Material, Paged } from "@/lib/types";

export default function Materials() {
  const [q, setQ] = useState("");
  const { data, loading, error } = useAsync(
    () => api<Paged<Material>>("/materials", { query: { q, pageSize: 100 } }),
    [q],
  );

  return (
    <div className="space-y-3">
      <PageHeader title="Materials" />
      <Input placeholder="Search materials…" value={q} onChange={(e) => setQ(e.target.value)} />

      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
        (data?.items.length ?? 0) === 0 ? <EmptyState title="No materials" /> : (
          <div className="space-y-2">
            {data!.items.map((m) => (
              <Card key={m.id} className="flex items-center justify-between">
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="truncate text-sm font-semibold">{m.name}</span>
                    {!m.isActive && <Chip tone="danger">Inactive</Chip>}
                  </div>
                  <div className="truncate text-xs text-text-dim">{m.categoryName} · {m.subcategoryName}</div>
                </div>
                <div className="text-right text-xs text-text-dim">
                  {money(m.defaultPurchaseRate)}<span className="text-text-dim">/{m.unitCode}</span>
                </div>
              </Card>
            ))}
          </div>
        )
      )}
    </div>
  );
}
