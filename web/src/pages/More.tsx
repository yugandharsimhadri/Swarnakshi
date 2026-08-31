import { Link } from "react-router-dom";
import { useAuth } from "@/store/auth";
import { useTheme } from "@/store/theme";
import { RoleName } from "@/lib/types";
import { Button, Card, PageHeader } from "@/components/ui";

export default function More() {
  const user = useAuth((s) => s.user);
  const logout = useAuth((s) => s.logout);
  const canApprove = useAuth((s) => s.can("approvals.decide"));
  const canManageUsers = useAuth((s) => s.can("users.manage"));
  const canManageMasters = useAuth((s) => s.can("masters.manage"));
  const { theme, toggle } = useTheme();

  return (
    <div className="space-y-4">
      <PageHeader title="More" />

      <Card>
        <div className="text-sm font-semibold">{user?.name}</div>
        <div className="text-xs text-text-dim">{user?.email}</div>
        <div className="mt-1 text-xs text-text-dim">Role: {user ? RoleName[user.role] : "—"}</div>
      </Card>

      {([
        ["/contractors", "Contractors", true],
        ["/customers", "Customers", true],
        ["/reports", "Reports", true],
        ["/approvals", "Approval Center", canApprove],
        ["/users", "Users", canManageUsers],
      ] as [string, string, boolean][])
        .filter(([, , show]) => show)
        .map(([to, label]) => (
          <Link key={to} to={to}>
            <Card className="flex items-center justify-between">
              <span className="text-sm">{label}</span>
              <span className="text-text-dim">▸</span>
            </Card>
          </Link>
        ))}
      {canManageMasters && (
        <p className="px-1 text-xs text-text-dim">
          Tip: manage units, categories, expense heads &amp; other lists via the API
          (<code>/api/simple-masters</code>) — a UI screen for these is on the backlog.
        </p>
      )}

      <Card className="flex items-center justify-between">
        <span className="text-sm">Appearance</span>
        <Button variant="ghost" onClick={toggle}>{theme === "dark" ? "🌙 Dark" : "☀️ Light"}</Button>
      </Card>

      <Button variant="danger" className="w-full" onClick={logout}>Sign out</Button>

      <p className="px-1 text-center text-xs text-text-dim">Swarnakshi · P5 build</p>
    </div>
  );
}
