import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api } from "@/lib/api";
import { tokens } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { Card, ErrorText, PageHeader, Spinner } from "@/components/ui";
import { IconChevron } from "@/components/icons";
import type { ReportTable } from "@/lib/types";

type Report = { slug: string; path: string; label: string; group: string; hint?: string };

const REPORTS: Report[] = [
  { slug: "villa-profitability", path: "project/profitability", label: "Villa Profit & Loss", group: "Profit",
    hint: "Margin against the part of each villa actually built" },
  { slug: "budget-burn", path: "project/budget-burn", label: "Budget vs Progress", group: "Profit",
    hint: "Spend measured against how far along the work is" },
  { slug: "site-summary", path: "site/summary", label: "Site Summary", group: "Profit",
    hint: "Capital tied up in each site" },
  { slug: "company-summary", path: "company/summary", label: "Company Summary", group: "Profit" },

  { slug: "customer-outstanding", path: "customer/outstanding", label: "Customer Outstanding", group: "Money" },
  { slug: "supplier-outstanding", path: "supplier/outstanding", label: "Supplier Outstanding", group: "Money" },
  { slug: "contractor-outstanding", path: "contractor/outstanding", label: "Contractor Outstanding", group: "Money" },
  { slug: "contractor-commitment", path: "contractor/commitment", label: "Contractor Commitment", group: "Money",
    hint: "Promised under work orders but not yet paid" },

  { slug: "inventory-stock", path: "inventory/stock", label: "Inventory Stock", group: "Inventory" },
  { slug: "low-stock", path: "inventory/low-stock", label: "Low Stock", group: "Inventory" },
  { slug: "purchase-register", path: "inventory/purchase-register", label: "Purchase Register", group: "Inventory" },
  { slug: "consumption", path: "inventory/consumption", label: "Consumption Register", group: "Inventory" },
  { slug: "project-cost-summary", path: "project/cost-summary", label: "Project Cost Summary", group: "Inventory" },
];

export function ReportsHub() {
  const groups = [...new Set(REPORTS.map((r) => r.group))];
  return (
    <div className="space-y-4">
      <PageHeader title="Reports" />
      {groups.map((g) => (
        <div key={g}>
          <div className="mb-2 px-1 text-xs font-semibold uppercase tracking-wide text-text-dim">{g}</div>
          <div className="space-y-2">
            {REPORTS.filter((r) => r.group === g).map((r) => (
              <Link key={r.slug} to={`/reports/${r.slug}`}>
                <Card className="flex items-center justify-between gap-3 transition-colors hover:border-brand/40">
                  <span className="min-w-0">
                    <span className="block text-sm font-semibold">{r.label}</span>
                    {r.hint && <span className="block text-xs text-text-dim">{r.hint}</span>}
                  </span>
                  <IconChevron size={16} className="shrink-0 text-text-dim" />
                </Card>
              </Link>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}

const isNum = (v: unknown): v is number => typeof v === "number";
const fmt = (v: string | number | null) =>
  v === null ? "—" : isNum(v) ? new Intl.NumberFormat("en-IN", { maximumFractionDigits: 2 }).format(v) : v;

/**
 * A report is read to find the one row that needs doing something about. "OVER BUDGET" set in the
 * same grey as every other cell is not a warning, it is a word — so the words the server uses to
 * flag a row, and any margin that has gone negative, get colour.
 */
const FLAG_TONE: Record<string, string> = {
  "OVER BUDGET": "text-danger font-semibold",
  "DUES ON HANDOVER": "text-danger font-semibold",
  LOW: "text-warn font-semibold",
  watch: "text-warn font-semibold",
  unsold: "text-text-dim",
  "not started": "text-text-dim",
};

function cellClass(value: string | number | null, column: string) {
  if (typeof value === "string") return FLAG_TONE[value] ?? "";
  if (!isNum(value)) return "";
  // Money that has gone the wrong way, on the columns where direction means something.
  if (/margin|profit|p ?\/ ?l|left in budget/i.test(column)) {
    return value < 0 ? "text-danger" : value > 0 ? "text-ok" : "";
  }
  if (/outstanding|committed unpaid|balance/i.test(column) && value > 0) return "text-warn";
  if (/burn/i.test(column)) return value > 110 ? "text-danger font-semibold" : value > 100 ? "text-warn" : "";
  return "";
}

export function ReportView() {
  const { slug } = useParams<{ slug: string }>();
  const report = REPORTS.find((r) => r.slug === slug);
  const { data, loading, error } = useAsync(
    () => (report ? api<ReportTable>(`/reports/${report.path}`) : Promise.reject({ message: "Unknown report", errors: [], status: 404 })),
    [slug],
  );
  const [downloading, setDownloading] = useState(false);

  async function downloadCsv() {
    if (!report) return;
    setDownloading(true);
    try {
      const res = await fetch(`/api/reports/${report.path}?format=csv`, {
        headers: { Authorization: `Bearer ${tokens.access}` },
      });
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `${report.slug}.csv`;
      a.click();
      URL.revokeObjectURL(url);
    } finally {
      setDownloading(false);
    }
  }

  return (
    <div className="space-y-3">
      <PageHeader
        title={report?.label ?? "Report"}
        back="/reports"
        action={
          <button onClick={downloadCsv} disabled={downloading} className="min-h-11 shrink-0 rounded-lg bg-surface-2 px-3 py-1.5 text-xs font-semibold">
            {downloading ? "…" : "Export CSV"}
          </button>
        }
      />

      {loading ? <Spinner /> : error ? <ErrorText error={error} /> : !data ? null : (
        data.rows.length === 0 ? (
          <Card><div className="text-sm text-text-dim">No data.</div></Card>
        ) : (
          <div className="overflow-x-auto rounded-2xl border border-border">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border bg-surface-2 text-left text-xs text-text-dim">
                  {data.columns.map((c) => <th key={c} className="whitespace-nowrap px-3 py-2 font-medium">{c}</th>)}
                </tr>
              </thead>
              <tbody>
                {data.rows.map((row, i) => (
                  <tr key={i} className="border-b border-border/60 last:border-0">
                    {row.map((cell, j) => (
                      <td
                        key={j}
                        className={`whitespace-nowrap px-3 py-2 ${isNum(cell) ? "text-right tabular-nums" : ""} ${cellClass(cell, data.columns[j] ?? "")}`}
                      >
                        {fmt(cell)}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )
      )}
    </div>
  );
}
