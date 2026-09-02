import { NavLink, Outlet } from "react-router-dom";
import { useAuth } from "@/store/auth";

// The four things a site person opens every day, plus More. Everything that is set-up or review
// work — sites, masters, people, reports — sits behind More, where it is looked at once a month.
const tabs = [
  { to: "/", label: "Home", icon: "⌂", end: true },
  { to: "/projects", label: "Projects", icon: "▤" },
  { to: "/inventory", label: "Inventory", icon: "▦" },
  { to: "/approvals", label: "Approvals", icon: "✓" },
  { to: "/more", label: "More", icon: "☰" },
];

export default function AppShell() {
  const company = useAuth((s) => s.company);

  return (
    // Phone-width column by default. On a desktop the column simply gets wider — the same screens,
    // more room for the tables and reports the office does its reconciliation in.
    <div className="mx-auto flex min-h-full max-w-md flex-col lg:max-w-5xl">
      <LicenceBanner company={company} />
      <main className="flex-1 px-3 pb-24 pt-3 lg:px-6 lg:pb-8">
        <Outlet />
      </main>

      <nav className="safe-b fixed inset-x-0 bottom-0 z-40 mx-auto max-w-md border-t border-border bg-surface/95 backdrop-blur lg:max-w-5xl">
        <div className="grid grid-cols-5">
          {tabs.map((t) => (
            <NavLink
              key={t.to}
              to={t.to}
              end={t.end}
              className={({ isActive }) =>
                `flex flex-col items-center gap-0.5 py-2.5 text-[11px] font-medium ${
                  isActive ? "text-brand" : "text-text-dim"
                }`
              }
            >
              <span className="text-lg leading-none">{t.icon}</span>
              {t.label}
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
