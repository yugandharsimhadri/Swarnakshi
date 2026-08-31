import { useEffect } from "react";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { useAuth } from "@/store/auth";
import { Spinner } from "@/components/ui";
import AppShell from "@/components/AppShell";
import Login from "@/pages/Login";
import Dashboard from "@/pages/Dashboard";
import Sites from "@/pages/Sites";
import Projects from "@/pages/Projects";
import ProjectDetail from "@/pages/ProjectDetail";
import Materials from "@/pages/Materials";
import More from "@/pages/More";
import Stock from "@/pages/Stock";
import { InventoryList, MaterialInventory } from "@/pages/Inventory";
import { MaterialRequestList, NewMaterialRequest, MaterialRequestDetail } from "@/pages/MaterialRequests";
import { PurchaseList, NewPurchase, PurchaseDetail } from "@/pages/Purchases";
import Approvals from "@/pages/Approvals";
import Contractors from "@/pages/Contractors";
import Customers from "@/pages/Customers";
import { ReportsHub, ReportView } from "@/pages/Reports";
import Users from "@/pages/Users";

export default function App() {
  const { user, loading, bootstrap } = useAuth();

  useEffect(() => { void bootstrap(); }, [bootstrap]);

  if (loading) return <div className="grid min-h-full place-items-center"><Spinner /></div>;

  return (
    <BrowserRouter>
      {!user ? (
        <Routes>
          <Route path="*" element={<Login />} />
        </Routes>
      ) : (
        <Routes>
          <Route element={<AppShell />}>
            <Route index element={<Dashboard />} />
            <Route path="sites" element={<Sites />} />
            <Route path="projects" element={<Projects />} />
            <Route path="projects/:id" element={<ProjectDetail />} />

            <Route path="stock" element={<Stock />} />
            <Route path="stock/inventory" element={<InventoryList />} />
            <Route path="stock/inventory/:siteId/:materialId" element={<MaterialInventory />} />
            <Route path="stock/requests" element={<MaterialRequestList />} />
            <Route path="stock/requests/new" element={<NewMaterialRequest />} />
            <Route path="stock/requests/:id" element={<MaterialRequestDetail />} />
            <Route path="stock/purchases" element={<PurchaseList />} />
            <Route path="stock/purchases/new" element={<NewPurchase />} />
            <Route path="stock/purchases/:id" element={<PurchaseDetail />} />
            <Route path="materials" element={<Materials />} />

            <Route path="approvals" element={<Approvals />} />
            <Route path="contractors" element={<Contractors />} />
            <Route path="customers" element={<Customers />} />
            <Route path="reports" element={<ReportsHub />} />
            <Route path="reports/:slug" element={<ReportView />} />
            <Route path="users" element={<Users />} />
            <Route path="more" element={<More />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Route>
        </Routes>
      )}
    </BrowserRouter>
  );
}
