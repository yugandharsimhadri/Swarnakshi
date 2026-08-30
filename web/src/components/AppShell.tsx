import { NavLink, Outlet } from "react-router-dom";

const tabs = [
  { to: "/", label: "Home", icon: "⌂", end: true },
  { to: "/sites", label: "Sites", icon: "⌾" },
  { to: "/projects", label: "Projects", icon: "▤" },
  { to: "/materials", label: "Materials", icon: "▦" },
  { to: "/more", label: "More", icon: "☰" },
];

export default function AppShell() {
  return (
    <div className="mx-auto flex min-h-full max-w-md flex-col">
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
