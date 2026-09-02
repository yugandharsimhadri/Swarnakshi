import { Link } from "react-router-dom";
import { useAuth } from "@/store/auth";
import { useTheme } from "@/store/theme";
import { RoleName } from "@/lib/types";
import { Button, Card, PageHeader } from "@/components/ui";

/**
 * Everything that is not daily work. Grouped so a non-technical user reads two short lists
 * instead of one long one: the things you set up once, and the things you look at monthly.
 */
export default function More() {
  const user = useAuth((s) => s.user);
  const company = useAuth((s) => s.company);
  const logout = useAuth((s) => s.logout);
  const canManageUsers = useAuth((s) => s.can("users.manage"));
  const { theme, toggle } = useTheme();

  const setup: [string, string, string, boolean][] = [
    ["/sites", "🏗", "Sites", true],
    ["/materials", "▤", "Materials", true],
    ["/contractors", "👷", "Contractors", true],
    ["/customers", "🤝", "Customers", true],
    ["/employees", "👤", "Employees", true],
    ["/users", "🔑", "Users & access", canManageUsers],
  ];

  return (
    <div className="space-y-4">
      <PageHeader title="More" />

      <Card>
        <div className="text-sm font-semibold">{user?.name}</div>
        <div className="text-xs text-text-dim">{user?.login}</div>
        <div className="mt-1 text-xs text-text-dim">
          {user ? RoleName[user.role] : "—"}{company ? ` · ${company.name}` : ""}
        </div>
      </Card>

      <Section title="Set up">
        {setup.filter(([, , , show]) => show).map(([to, icon, label]) => (
          <Row key={to} to={to} icon={icon} label={label} />
        ))}
      </Section>

      <Section title="Review">
        <Row to="/reports" icon="📊" label="Reports" />
      </Section>

      <Card className="flex items-center justify-between">
        <span className="text-sm">Appearance</span>
        <Button variant="ghost" onClick={toggle}>{theme === "dark" ? "🌙 Dark" : "☀️ Light"}</Button>
      </Card>

      <Button variant="danger" className="w-full" onClick={logout}>Sign out</Button>
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="space-y-2">
      <div className="px-1 text-xs font-semibold uppercase tracking-wide text-text-dim">{title}</div>
      {children}
    </div>
  );
}

function Row({ to, icon, label }: { to: string; icon: string; label: string }) {
  return (
    <Link to={to}>
      <Card className="flex items-center gap-3">
        <span className="text-lg">{icon}</span>
        <span className="flex-1 text-sm font-medium">{label}</span>
        <span className="text-text-dim">▸</span>
      </Card>
    </Link>
  );
}
