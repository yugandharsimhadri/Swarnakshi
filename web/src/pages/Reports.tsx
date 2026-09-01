import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api } from "@/lib/api";
import { tokens } from "@/lib/api";
import { useAsync } from "@/lib/useAsync";
import { Card, ErrorText, PageHeader, Spinner } from "@/components/ui";
import type { ReportTable } from "@/lib/types";

const REPORTS: { slug: string; path: string; label: string; group: string }[] = [
  { slug: "company-summary", path: "company/summary", label: "Company Summary", group: "Company" },
  { slug: "project-cost-summary", path: "project/cost-summary", label: "Project Cost Summary", group: "Company" },
  { slug: "contractor-outstanding", path: "contractor/outstanding", label: "Contractor Outstanding", group: "Money" },
  { slug: "customer-outstanding", path: "customer/outstanding", label: "Customer Outstanding", group: "Money" },
  { slug: "inventory-stock", path: "inventory/stock", label: "Inventory Stock", group: "Inventory" },
  { slug: "low-stock", path: "inventory/low-stock", label: "Low Stock", group: "Inventory" },
  { slug: "purchase-register", path: "inventory/purchase-register", label: "Purchase Register", group: "Inventory" },
  { slug: "consumption", path: "inventory/consumption", label: "Consumption Register", group: "Inventory" },
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
                <Card className="flex items-center justify-between">
                  <span className="text-sm font-semibold">{r.label}</span>
                  <span className="text-text-dim">▸</span>
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
      <Link to="/reports" className="-ml-1 inline-flex min-h-11 items-center px-1 text-xs text-text-dim">← Reports</Link>
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-bold">{report?.label ?? "Report"}</h1>
        <button onClick={downloadCsv} disabled={downloading} className="min-h-11 rounded-lg bg-surface-2 px-3 py-1.5 text-xs font-semibold">
          {downloading ? "…" : "Export CSV"}
        </button>
      </div>

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
                      <td key={j} className={`whitespace-nowrap px-3 py-2 ${isNum(cell) ? "text-right tabular-nums" : ""}`}>
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
