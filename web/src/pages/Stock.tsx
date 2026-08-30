import { Link } from "react-router-dom";
import { Card, PageHeader } from "@/components/ui";
import { useAuth } from "@/store/auth";

const items = [
  { to: "/stock/inventory", icon: "▦", label: "Site Inventory", hint: "Stock balances & ledger" },
  { to: "/stock/requests", icon: "⇄", label: "Material Requests", hint: "Request stock for a project" },
  { to: "/stock/purchases", icon: "🧾", label: "Purchases", hint: "Buy material into a site" },
  { to: "/materials", icon: "▤", label: "Material Master", hint: "Catalogue & rates" },
];

export default function Stock() {
  const canApprove = useAuth((s) => s.can("approvals.decide"));
  return (
    <div className="space-y-3">
      <PageHeader title="Stock" />
      {items.map((i) => (
        <Link key={i.to} to={i.to}>
          <Card className="flex items-center gap-3">
            <span className="text-xl">{i.icon}</span>
            <div>
              <div className="text-sm font-semibold">{i.label}</div>
              <div className="text-xs text-text-dim">{i.hint}</div>
            </div>
          </Card>
        </Link>
      ))}
      {canApprove && (
        <Link to="/approvals">
          <Card className="flex items-center gap-3">
            <span className="text-xl">✓</span>
            <div>
              <div className="text-sm font-semibold">Approval Center</div>
              <div className="text-xs text-text-dim">Pending material requests, purchases &amp; payments</div>
            </div>
          </Card>
        </Link>
      )}
    </div>
  );
}
