import { Link } from "react-router-dom";
import { useAuth } from "@/store/auth";
import { useTheme } from "@/store/theme";
import { RoleName } from "@/lib/types";
import { Button, Card, PageHeader } from "@/components/ui";

export default function More() {
  const user = useAuth((s) => s.user);
  const logout = useAuth((s) => s.logout);
  const canApprove = useAuth((s) => s.can("approvals.decide"));
  const { theme, toggle } = useTheme();

  return (
    <div className="space-y-4">
      <PageHeader title="More" />

      <Card>
        <div className="text-sm font-semibold">{user?.name}</div>
        <div className="text-xs text-text-dim">{user?.email}</div>
        <div className="mt-1 text-xs text-text-dim">Role: {user ? RoleName[user.role] : "—"}</div>
      </Card>

      <Link to="/contractors">
        <Card className="flex items-center justify-between">
          <span className="text-sm">Contractors</span>
          <span className="text-text-dim">▸</span>
        </Card>
      </Link>
      {canApprove && (
        <Link to="/approvals">
          <Card className="flex items-center justify-between">
            <span className="text-sm">Approval Center</span>
            <span className="text-text-dim">▸</span>
          </Card>
        </Link>
      )}

      <Card className="flex items-center justify-between">
        <span className="text-sm">Appearance</span>
        <Button variant="ghost" onClick={toggle}>{theme === "dark" ? "🌙 Dark" : "☀️ Light"}</Button>
      </Card>

      <Button variant="danger" className="w-full" onClick={logout}>Sign out</Button>

      <p className="px-1 text-center text-xs text-text-dim">Swarnakshi · P2 build</p>
    </div>
  );
}
