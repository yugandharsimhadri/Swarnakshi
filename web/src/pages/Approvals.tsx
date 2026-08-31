import { useState } from "react";
import { api, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { money, dateStr } from "@/lib/format";
import { Button, Card, Chip, Confirm, EmptyState, ErrorText, PageHeader, Spinner } from "@/components/ui";
import type { ApprovalItem, Paged } from "@/lib/types";

const label: Record<string, string> = {
  MaterialRequest: "Material Request",
  Purchase: "Purchase",
  ContractorPayment: "Contractor Payment",
  LabourEntry: "Labour Payment",
  CustomerPayment: "Customer Payment",
  InventoryAdjustment: "Inventory Adjustment",
};

export default function Approvals() {
  const { data, loading, error, reload } = useAsync(
    () => api<Paged<ApprovalItem>>("/approvals", { query: { pendingOnly: true, pageSize: 100 } }),
    [],
  );
  const [pending, setPending] = useState<{ id: string; approve: boolean; ref: string } | null>(null);
  const [busy, setBusy] = useState(false);
  const [actErr, setActErr] = useState<ApiError | null>(null);

  async function confirmDecision() {
    if (!pending) return;
    setBusy(true);
    setActErr(null);
    try {
      await api(`/approvals/${pending.id}/${pending.approve ? "approve" : "reject"}`, {
        method: "POST",
        body: { remarks: pending.approve ? "Approved" : "Rejected", allowOverride: false },
      });
      setPending(null);
      reload();
    } catch (e) {
      setActErr(e as ApiError);
      setPending(null);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-3">
      <PageHeader title="Approval Center" />
      <ErrorText error={actErr} />
      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : (
        (data?.items.length ?? 0) === 0 ? <EmptyState title="Nothing pending" hint="You're all caught up." /> : (
          <div className="space-y-2">
            {data!.items.map((a) => (
              <Card key={a.id} className="space-y-2">
                <div className="flex items-center justify-between">
                  <Chip tone="brand">{label[a.entityType] ?? a.entityType}</Chip>
                  <span className="text-xs text-text-dim">{dateStr(a.requestedAt)}</span>
                </div>
                <div className="text-sm font-semibold">{a.entityRef ?? a.entityId.slice(0, 8)}</div>
                {a.amount != null && <div className="text-sm tabular-nums">{money(a.amount)}</div>}
                <div className="flex gap-2 pt-1">
                  <Button className="flex-1" onClick={() => setPending({ id: a.id, approve: true, ref: a.entityRef ?? "" })}>Approve</Button>
                  <Button variant="danger" className="flex-1" onClick={() => setPending({ id: a.id, approve: false, ref: a.entityRef ?? "" })}>Reject</Button>
                </div>
              </Card>
            ))}
          </div>
        )
      )}

      <Confirm
        open={pending !== null}
        title={pending?.approve ? "Approve this item?" : "Reject this item?"}
        body={
          pending?.approve
            ? `${pending.ref} will be posted: inventory / ledgers / project cost update immediately.`
            : `${pending?.ref} will be rejected and cannot be posted.`
        }
        confirmLabel={pending?.approve ? "Approve" : "Reject"}
        danger={!pending?.approve}
        onConfirm={confirmDecision}
        onCancel={() => setPending(null)}
      />
      {busy && <Spinner />}
    </div>
  );
}
