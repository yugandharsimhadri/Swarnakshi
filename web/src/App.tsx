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
            <Route path="materials" element={<Materials />} />
            <Route path="more" element={<More />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Route>
        </Routes>
      )}
    </BrowserRouter>
  );
}
