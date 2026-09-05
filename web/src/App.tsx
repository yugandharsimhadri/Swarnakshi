import { lazy, Suspense, useEffect } from "react";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { useAuth } from "@/store/auth";
import { Spinner } from "@/components/ui";
import AppShell from "@/components/AppShell";
import Login from "@/pages/Login";

/**
 * Everything past the sign-in screen is loaded on demand.
 *
 * Importing all twenty screens at the top of this file put every one of them — every form, every
 * report, the platform console a company user can never open — into a single file the browser had
 * to download and parse before it could draw anything at all. On the site office's connection that
 * is the whole of the wait. Login stays eagerly imported because it is what most first loads are
 * for, and making it a second round trip would trade one wait for another.
 *
 * Named exports are wrapped rather than re-exported so that both halves of a screen still travel in
 * the same chunk: opening the purchase list downloads the purchase detail with it, which is where
 * the user is going next anyway.
 */
const named = <T extends Record<string, unknown>, K extends keyof T>(
  load: () => Promise<T>, key: K,
) => lazy(() => load().then((m) => ({ default: m[key] as React.ComponentType })));

const Register = lazy(() => import("@/pages/Register"));
const PlatformConsole = lazy(() => import("@/pages/PlatformConsole"));

const Dashboard = lazy(() => import("@/pages/Dashboard"));
const Sites = lazy(() => import("@/pages/Sites"));
const Projects = lazy(() => import("@/pages/Projects"));
const ProjectDetail = lazy(() => import("@/pages/project/ProjectDetail"));
const Materials = lazy(() => import("@/pages/Materials"));
const More = lazy(() => import("@/pages/More"));
const Settings = lazy(() => import("@/pages/Settings"));
const Approvals = lazy(() => import("@/pages/Approvals"));
const Contractors = lazy(() => import("@/pages/Contractors"));
const Suppliers = lazy(() => import("@/pages/Suppliers"));
const Customers = lazy(() => import("@/pages/Customers"));
const Users = lazy(() => import("@/pages/Users"));
const Employees = lazy(() => import("@/pages/Employees"));

const InventoryList = named(() => import("@/pages/Inventory"), "InventoryList");
const MaterialInventory = named(() => import("@/pages/Inventory"), "MaterialInventory");
const MaterialRequestList = named(() => import("@/pages/MaterialRequests"), "MaterialRequestList");
const NewMaterialRequest = named(() => import("@/pages/MaterialRequests"), "NewMaterialRequest");
const MaterialRequestDetail = named(() => import("@/pages/MaterialRequests"), "MaterialRequestDetail");
const PurchaseList = named(() => import("@/pages/Purchases"), "PurchaseList");
const NewPurchase = named(() => import("@/pages/Purchases"), "NewPurchase");
const PurchaseDetail = named(() => import("@/pages/Purchases"), "PurchaseDetail");
const ReportsHub = named(() => import("@/pages/Reports"), "ReportsHub");
const ReportView = named(() => import("@/pages/Reports"), "ReportView");

const Loading = () => <div className="grid min-h-40 place-items-center"><Spinner /></div>;

export default function App() {
  const { user, platformUser, loading, bootstrap } = useAuth();
  const canDashboard = useAuth((s) => s.can("dashboard.view"));
  const canReports = useAuth((s) => s.can("reports.view"));
  const canSettings = useAuth((s) => s.can("settings.manage"));

  useEffect(() => { void bootstrap(); }, [bootstrap]);

  if (loading) return <div className="grid min-h-full place-items-center"><Spinner /></div>;

  return (
    <BrowserRouter>
      {platformUser ? (
        // A platform operator gets its own console and nothing else — there is no company shell
        // for it to render, and no company route it is allowed to reach.
        <Suspense fallback={<Loading />}>
          <Routes>
            <Route path="*" element={<PlatformConsole />} />
          </Routes>
        </Suspense>
      ) : !user ? (
        <Routes>
          <Route path="/register" element={<Suspense fallback={<Loading />}><Register /></Suspense>} />
          <Route path="*" element={<Login />} />
        </Routes>
      ) : (
        <Routes>
          {/* AppShell is eager and holds the Suspense boundary around its own Outlet, so moving
              between screens swaps the page and leaves the navigation bar where it was. */}
          <Route element={<AppShell />}>
            {/* A site Supervisor has no company dashboard — their landing screen is the work. */}
            <Route index element={canDashboard ? <Dashboard /> : <Navigate to="/projects" replace />} />
            <Route path="sites" element={<Sites />} />
            <Route path="projects" element={<Projects />} />
            <Route path="projects/:id" element={<ProjectDetail />} />

            <Route path="inventory" element={<InventoryList />} />
            <Route path="inventory/purchases" element={<PurchaseList />} />
            <Route path="inventory/purchases/new" element={<NewPurchase />} />
            <Route path="inventory/purchases/:id" element={<PurchaseDetail />} />
            <Route path="inventory/requests" element={<MaterialRequestList />} />
            <Route path="inventory/requests/new" element={<NewMaterialRequest />} />
            <Route path="inventory/requests/:id" element={<MaterialRequestDetail />} />
            <Route path="inventory/:siteId/:materialId" element={<MaterialInventory />} />
            <Route path="materials" element={<Materials />} />

            {/* Links people have bookmarked, or that live in an old approval email. */}
            <Route path="movement" element={<Navigate to="/inventory" replace />} />
            <Route path="stock" element={<Navigate to="/inventory" replace />} />
            <Route path="stock/inventory" element={<Navigate to="/inventory" replace />} />
            <Route path="stock/requests/*" element={<Navigate to="/inventory/requests" replace />} />
            <Route path="stock/purchases/*" element={<Navigate to="/inventory/purchases" replace />} />

            <Route path="approvals" element={<Approvals />} />
            <Route path="contractors" element={<Contractors />} />
            <Route path="suppliers" element={<Suppliers />} />
            <Route path="customers" element={<Customers />} />
            <Route path="reports" element={canReports ? <ReportsHub /> : <Navigate to="/" replace />} />
            <Route path="reports/:slug" element={canReports ? <ReportView /> : <Navigate to="/" replace />} />
            <Route path="users" element={<Users />} />
            <Route path="employees" element={<Employees />} />
            <Route path="settings" element={canSettings ? <Settings /> : <Navigate to="/more" replace />} />
            <Route path="more" element={<More />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Route>
        </Routes>
      )}
    </BrowserRouter>
  );
}
