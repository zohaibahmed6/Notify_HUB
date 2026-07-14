import { Navigate, Route, Routes } from "react-router-dom";

import ProtectedRoute from "@/routes/ProtectedRoute";
import { VersionedRoute } from "@/routes/VersionedRoute";
import AppShell from "@/components/layout/AppShell";
import LoginPage from "@/pages/LoginPage";
import InboxPage from "@/pages/InboxPage";
import TaskBoardPage from "@/pages/TaskBoardPage";
import TemplatesPage from "@/pages/TemplatesPage";
import AuditLogPage from "@/pages/AuditLogPage";
import SettingsPage from "@/pages/SettingsPage";
import DashboardPage from "@/pages/DashboardPage";
import SmsHistoryPage from "@/pages/SmsHistoryPage";
import LoginPageV2 from "@/pages/v2/LoginPageV2";
import InboxPageV2 from "@/pages/v2/InboxPageV2";
import TaskBoardPageV2 from "@/pages/v2/TaskBoardPageV2";
import TemplatesPageV2 from "@/pages/v2/TemplatesPageV2";
import AuditLogPageV2 from "@/pages/v2/AuditLogPageV2";

// Every route renders through VersionedRoute so the UI-version toggle (AppShell header,
// backed by UIVersionContext) can swap the whole screen's presentation layer without
// touching data-fetching, routing, or auth. Legacy components are untouched originals;
// each V2 component starts as a pass-through placeholder (src/pages/v2/*) and gets
// replaced screen-by-screen as the redesign ships.
export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<VersionedRoute Legacy={LoginPage} Redesign={LoginPageV2} />} />
      <Route element={<ProtectedRoute />}>
        <Route element={<AppShell />}>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/inbox" element={<VersionedRoute Legacy={InboxPage} Redesign={InboxPageV2} />} />
          <Route path="/tasks" element={<VersionedRoute Legacy={TaskBoardPage} Redesign={TaskBoardPageV2} />} />
          <Route path="/templates" element={<VersionedRoute Legacy={TemplatesPage} Redesign={TemplatesPageV2} />} />
          <Route path="/audit" element={<VersionedRoute Legacy={AuditLogPage} Redesign={AuditLogPageV2} />} />
          {/* Not versioned — entirely new screen, same "no legacy variant" precedent as
              Dashboard/Settings (P9-06). */}
          <Route path="/sms-history" element={<SmsHistoryPage />} />
          {/* Not versioned — one settings screen serves both UI modes. */}
          <Route path="/settings" element={<SettingsPage />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
