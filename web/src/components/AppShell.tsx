import type { ComponentType, SVGProps } from "react";
import { NavLink, Outlet } from "react-router-dom";
import { useAuth } from "@/store/auth";
import { IconApprovals, IconHome, IconInventory, IconMore, IconProjects } from "@/components/icons";

// The things a site person opens every day, plus More. Everything that is set-up or review work —
// sites, masters, people, reports — sits behind More, where it is looked at once a month. Home is
// the company dashboard; a Supervisor does not have it, so their bar starts at Projects.
type Tab = {
  to: string;
  label: string;
  Icon: ComponentType<SVGProps<SVGSVGElement> & { size?: number }>;
  end?: boolean;
  needs?: string;
};

const ALL_TABS: Tab[] = [
  { to: "/", label: "Home", Icon: IconHome, end: true, needs: "dashboard.view" },
  { to: "/projects", label: "Projects", Icon: IconProjects },
  { to: "/inventory", label: "Inventory", Icon: IconInventory },
  { to: "/approvals", label: "Approvals", Icon: IconApprovals },
  { to: "/more", label: "More", Icon: IconMore },
];

export default function AppShell() {
  const company = useAuth((s) => s.company);
  const can = useAuth((s) => s.can);
  const tabs = ALL_TABS.filter((t) => !t.needs || can(t.needs));

  return (
    // Phone-width column by default. On a desktop the column simply gets wider — the same screens,
    // more room for the tables and reports the office does its reconciliation in.
    <div className="mx-auto flex min-h-full max-w-md flex-col lg:max-w-5xl">
      <LicenceBanner company={company} />
      <main className="flex-1 px-3 pb-24 pt-3 lg:px-6 lg:pb-8">
        <Outlet />
      </main>

      <nav className="safe-b fixed inset-x-0 bottom-0 z-40 mx-auto max-w-md border-t border-border bg-surface/95 backdrop-blur lg:max-w-5xl">
        <div className={tabs.length === 4 ? "grid grid-cols-4" : "grid grid-cols-5"}>
          {tabs.map(({ to, end, label, Icon }) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              className={({ isActive }) =>
                `relative flex flex-col items-center gap-1 py-2.5 text-[11px] font-medium transition-colors ${
                  isActive ? "text-brand" : "text-text-dim"
                }`
              }
            >
              {({ isActive }) => (
                <>
                  {/* A rule above the active tab rather than a filled pill — the same way a
                      drawing marks the sheet you are on. */}
                  <span
                    aria-hidden
                    className={`absolute inset-x-4 top-0 h-0.5 rounded-full transition-opacity ${
                      isActive ? "bg-brand opacity-100" : "opacity-0"
                    }`}
                  />
                  <Icon size={21} strokeWidth={isActive ? 2 : 1.7} />
                  {label}
                </>
              )}
            </NavLink>
          ))}
        </div>
      </nav>
    </div>
  );
}

/**
 * Warns before a licence lapses rather than after. A builder who loses access mid-month has to
 * chase somebody; a fortnight of notice lets them renew on their own schedule.
 */
function LicenceBanner({ company }: { company: { name: string; daysToExpiry: number } | null }) {
  if (!company || company.daysToExpiry > 14) return null;

  const urgent = company.daysToExpiry <= 3;
  const days = company.daysToExpiry;

  return (
    <div role="status" className={`px-3 pt-3 text-xs ${urgent ? "text-danger" : "text-warn"}`}>
      <div className={`rounded-xl px-3 py-2 ${urgent ? "bg-danger/10" : "bg-warn/10"}`}>
        {days <= 0
          ? "Your licence has expired. Ask your Swarnakshi administrator to renew it."
          : `Your licence expires in ${days} day${days === 1 ? "" : "s"}. Ask your Swarnakshi administrator to renew it.`}
      </div>
    </div>
  );
}
