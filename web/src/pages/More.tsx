import type { ComponentType, ReactNode, SVGProps } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "@/store/auth";
import { useTheme } from "@/store/theme";
import { RoleName } from "@/lib/types";
import { Button, Card, PageHeader } from "@/components/ui";
import {
  IconAccess, IconChevron, IconContractor, IconCustomer, IconEmployees, IconMaterials,
  IconMoon, IconReports, IconSite, IconSun,
} from "@/components/icons";

type IconComponent = ComponentType<SVGProps<SVGSVGElement> & { size?: number }>;

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

  const setup: [string, IconComponent, string, boolean][] = [
    ["/sites", IconSite, "Sites", true],
    ["/materials", IconMaterials, "Materials", true],
    ["/contractors", IconContractor, "Contractors", true],
    ["/customers", IconCustomer, "Customers", true],
    ["/employees", IconEmployees, "Employees", true],
    ["/users", IconAccess, "Users & access", canManageUsers],
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
        {setup.filter(([, , , show]) => show).map(([to, Icon, label]) => (
          <Row key={to} to={to} Icon={Icon} label={label} />
        ))}
      </Section>

      <Section title="Review">
        <Row to="/reports" Icon={IconReports} label="Reports" />
      </Section>

      <Card className="flex items-center justify-between">
        <span className="text-sm">Appearance</span>
        <Button variant="ghost" onClick={toggle} className="inline-flex items-center gap-2">
          {theme === "dark" ? <IconMoon size={16} /> : <IconSun size={16} />}
          {theme === "dark" ? "Dark" : "Light"}
        </Button>
      </Card>

      {/* Outlined, not a solid red slab. Signing out is routine and reversible; the loud red
          treatment belongs to the things that cancel a transaction. */}
      <button
        onClick={logout}
        className="min-h-11 w-full rounded-xl border border-danger/40 py-2.5 text-sm font-semibold text-danger transition-colors hover:bg-danger/10"
      >
        Sign out
      </button>
    </div>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="space-y-2">
      <div className="px-1 text-xs font-semibold uppercase tracking-wide text-text-dim">{title}</div>
      {children}
    </div>
  );
}

function Row({ to, Icon, label }: { to: string; Icon: IconComponent; label: string }) {
  return (
    <Link to={to}>
      <Card className="flex items-center gap-3 transition-colors hover:border-brand/40">
        <span className="text-brand"><Icon size={20} /></span>
        <span className="flex-1 text-sm font-medium">{label}</span>
        <IconChevron size={16} className="text-text-dim" />
      </Card>
    </Link>
  );
}
