import { useEffect, useState } from "react";
import { api, apiUrl, tokens, type ApiError } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { money, dateStr } from "@/lib/format";
import { Button, Card, Chip, Confirm, EmptyState, ErrorText, PageHeader, Spinner } from "@/components/ui";
import type { ApprovalItem, ContractorPaymentChange, Paged, PaymentFacts } from "@/lib/types";

const label: Record<string, string> = {
  MaterialRequest: "Material Request",
  Purchase: "Purchase",
  SupplierPayment: "Supplier Payment",
  ContractorPayment: "Contractor Payment",
  ContractorPaymentChange: "Contractor Payment Change",
  LabourEntry: "Labour Payment",
  EmployeePayment: "Employee Payment",
  CustomerPayment: "Customer Receipt",
  ProjectExpense: "Villa Expense",
  SiteExpense: "Site Expense",
  InventoryAdjustment: "Inventory Adjustment",
};

export default function Approvals() {
  const { data, loading, error, reload } = useAsync(
    () => api<Paged<ApprovalItem>>("/approvals", { query: { pendingOnly: true, pageSize: 100 } }),
    [],
  );
  const [pending, setPending] = useState<{ id: string; approve: boolean; ref: string; change: boolean } | null>(null);
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
                {a.entityType === "ContractorPaymentChange"
                  ? <PaymentChangeDetail id={a.entityId} />
                  : a.amount != null && <div className="text-sm tabular-nums">{money(a.amount)}</div>}
                {/* An expense may carry the photographed bill, and when it does the photo is the
                    decision — nobody can approve paying a man back for a bill they cannot see. */}
                {(a.entityType === "ProjectExpense" || a.entityType === "SiteExpense") && (
                  <BillPhotos entityType={a.entityType} entityId={a.entityId} />
                )}
                <div className="flex gap-2 pt-1">
                  <Button className="flex-1" onClick={() => setPending({ id: a.id, approve: true, ref: a.entityRef ?? "", change: a.entityType === "ContractorPaymentChange" })}>Approve</Button>
                  <Button variant="danger" className="flex-1" onClick={() => setPending({ id: a.id, approve: false, ref: a.entityRef ?? "", change: a.entityType === "ContractorPaymentChange" })}>Reject</Button>
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
          pending?.change
            ? pending.approve
              ? `${pending.ref} will be applied: the payment, the contract balance and the villa's cost update immediately.`
              : `${pending.ref} will be rejected. The payment stays exactly as it is.`
            : pending?.approve
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

/**
 * The bills attached to one expense, shown rather than listed.
 *
 * Fetched as a blob with the access token and turned into an object URL, because the download
 * endpoint is authenticated — an `<img src>` pointing at it would arrive without the Authorization
 * header and render as a broken image. Nothing is shown at all when there is no attachment, which
 * is the ordinary case for a company-paid expense.
 */
function BillPhotos({ entityType, entityId }: { entityType: string; entityId: string }) {
  const { data } = useAsync(
    () => api<{ id: string; fileName: string; contentType: string }[]>("/attachments", { query: { entityType, entityId } }),
    [entityType, entityId],
  );
  const images = (data ?? []).filter((a) => a.contentType.startsWith("image/"));
  if (images.length === 0) return null;
  return (
    <div className="flex gap-2 overflow-x-auto">
      {images.map((a) => <BillThumb key={a.id} id={a.id} name={a.fileName} />)}
    </div>
  );
}

function BillThumb({ id, name }: { id: string; name: string }) {
  const [url, setUrl] = useState<string | null>(null);

  useEffect(() => {
    let revoke: string | null = null;
    let cancelled = false;
    fetch(apiUrl(`/attachments/${id}/download`), { headers: { Authorization: `Bearer ${tokens.access}` } })
      .then((r) => (r.ok ? r.blob() : Promise.reject(new Error("failed"))))
      .then((b) => {
        if (cancelled) return;
        revoke = URL.createObjectURL(b);
        setUrl(revoke);
      })
      .catch(() => { /* a missing file must not take the approval queue down with it */ });
    return () => {
      cancelled = true;
      if (revoke) URL.revokeObjectURL(revoke);
    };
  }, [id]);

  if (!url) return <div className="h-24 w-24 shrink-0 animate-pulse rounded-xl bg-surface-2" />;
  return (
    <a href={url} target="_blank" rel="noreferrer" className="shrink-0">
      <img src={url} alt={name} className="h-24 w-24 rounded-xl border border-border object-cover" />
    </a>
  );
}

/**
 * What a change to a posted payment would actually do. The queue's one line of reference and amount
 * cannot say that, and "approve a change" with no sight of the change is not a decision.
 */
function PaymentChangeDetail({ id }: { id: string }) {
  const { data: c } = useAsync(() => api<ContractorPaymentChange>(`/contractor-payments/changes/${id}`), [id]);
  if (!c) return null;

  const fields: [string, (f: PaymentFacts) => string][] = [
    ["Amount", (f) => money(f.amount)],
    ["Date", (f) => dateStr(f.date)],
    ["Work order", (f) => f.work ?? "—"],
    ["Paid via", (f) => f.method],
    ["Reference", (f) => f.reference ?? "—"],
  ];

  return (
    <div className="space-y-1 text-sm">
      <div className="text-text-dim">{c.contractorName} · {c.projectName}</div>
      {c.kind === 2 ? (
        <div className="font-semibold text-danger">Remove {money(c.before.amount)} paid on {dateStr(c.before.date)}</div>
      ) : (
        fields
          .filter(([, read]) => read(c.before) !== read(c.after))
          .map(([name, read]) => (
            <div key={name}>
              <span className="text-text-dim">{name}: </span>
              <span className="line-through">{read(c.before)}</span> → <span className="font-semibold">{read(c.after)}</span>
            </div>
          ))
      )}
      <div><span className="text-text-dim">Why: </span>{c.reason}</div>
      {c.requestedBy && <div className="text-xs text-text-dim">Asked by {c.requestedBy}</div>}
    </div>
  );
}
