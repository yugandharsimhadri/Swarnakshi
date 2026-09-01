import { NavLink, Outlet } from "react-router-dom";
import { useAuth } from "@/store/auth";

// Ordered by daily use, not by data hierarchy: the dashboard, then where material and money move,
// then the store. Sites, masters and reports are set-up-or-review work and live under More.
const tabs = [
  { to: "/", label: "Home", icon: "⌂", end: true },
  { to: "/movement", label: "Movement", icon: "⇄" },
  { to: "/stock/inventory", label: "Inventory", icon: "▦" },
  { to: "/projects", label: "Projects", icon: "▤" },
  { to: "/more", label: "More", icon: "☰" },
];

export default function AppShell() {
  const company = useAuth((s) => s.company);

  return (
    <div className="mx-auto flex min-h-full max-w-md flex-col">
      <LicenceBanner company={company} />
      <main className="flex-1 px-3 pb-24 pt-3">
        <Outlet />
      </main>

      <nav className="safe-b fixed inset-x-0 bottom-0 z-40 mx-auto max-w-md border-t border-border bg-surface/95 backdrop-blur">
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
