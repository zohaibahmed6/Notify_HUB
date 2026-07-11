import { Navigate, Route, Routes } from "react-router-dom";

import ProtectedRoute from "@/routes/ProtectedRoute";
import AppShell from "@/components/layout/AppShell";
import LoginPage from "@/pages/LoginPage";
import InboxPage from "@/pages/InboxPage";
import TaskBoardPage from "@/pages/TaskBoardPage";
import TemplatesPage from "@/pages/TemplatesPage";
import AuditLogPage from "@/pages/AuditLogPage";

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
        <Route element={<AppShell />}>
          <Route path="/" element={<Navigate to="/inbox" replace />} />
          <Route path="/inbox" element={<InboxPage />} />
          <Route path="/tasks" element={<TaskBoardPage />} />
          <Route path="/templates" element={<TemplatesPage />} />
          <Route path="/audit" element={<AuditLogPage />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
