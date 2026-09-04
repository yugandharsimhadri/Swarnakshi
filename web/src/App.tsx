import { useEffect } from "react";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { useAuth } from "@/store/auth";
import { Spinner } from "@/components/ui";
import AppShell from "@/components/AppShell";
import Login from "@/pages/Login";
import Dashboard from "@/pages/Dashboard";
import Sites from "@/pages/Sites";
import Projects from "@/pages/Projects";
import ProjectDetail from "@/pages/project/ProjectDetail";
import Materials from "@/pages/Materials";
import More from "@/pages/More";
import { InventoryList, MaterialInventory } from "@/pages/Inventory";
import { MaterialRequestList, NewMaterialRequest, MaterialRequestDetail } from "@/pages/MaterialRequests";
import { PurchaseList, NewPurchase, PurchaseDetail } from "@/pages/Purchases";
import Approvals from "@/pages/Approvals";
import Contractors from "@/pages/Contractors";
import Suppliers from "@/pages/Suppliers";
import Customers from "@/pages/Customers";
import { ReportsHub, ReportView } from "@/pages/Reports";
import Users from "@/pages/Users";
import Register from "@/pages/Register";
import PlatformConsole from "@/pages/PlatformConsole";
import Employees from "@/pages/Employees";

export default function App() {
  const { user, platformUser, loading, bootstrap } = useAuth();
  const canDashboard = useAuth((s) => s.can("dashboard.view"));
  const canReports = useAuth((s) => s.can("reports.view"));

  useEffect(() => { void bootstrap(); }, [bootstrap]);

  if (loading) return <div className="grid min-h-full place-items-center"><Spinner /></div>;

  return (
    <BrowserRouter>
      {platformUser ? (
        // A platform operator gets its own console and nothing else — there is no company shell
        // for it to render, and no company route it is allowed to reach.
        <Routes>
          <Route path="*" element={<PlatformConsole />} />
        </Routes>
      ) : !user ? (
        <Routes>
          <Route path="/register" element={<Register />} />
          <Route path="*" element={<Login />} />
        </Routes>
      ) : (
        <Routes>
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
            <Route path="more" element={<More />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Route>
        </Routes>
      )}
    </BrowserRouter>
  );
}
