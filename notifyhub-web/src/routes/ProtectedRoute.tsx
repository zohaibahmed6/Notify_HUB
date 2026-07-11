import { Navigate, Outlet } from "react-router-dom";

import { useAuth } from "@/context/AuthContext";

export default function ProtectedRoute() {
  const { isAuthenticated, isBootstrapping } = useAuth();

  // Wait for the silent cookie-based refresh (AuthContext's mount effect) to resolve
  // before deciding whether to redirect — otherwise a page reload with a still-valid
  // session would flash/redirect to /login before the refresh had a chance to restore it.
  if (isBootstrapping) {
    return null;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  return <Outlet />;
}
