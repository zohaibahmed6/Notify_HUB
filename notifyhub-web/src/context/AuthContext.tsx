import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { useMutation } from "@tanstack/react-query";

import { apiClient } from "@/lib/apiClient";
import { tokenStore, type TokenSet } from "@/lib/tokenStore";

export interface AuthUser {
  id: number;
  username: string;
  fullName: string | null;
  role: "Admin" | "Staff";
}

interface LoginResponse extends TokenSet {
  user: AuthUser;
}

interface AuthContextValue {
  user: AuthUser | null;
  isAuthenticated: boolean;
  isLoggingIn: boolean;
  /** True until the mount-time silent refresh (cookie -> new access token) resolves. */
  isBootstrapping: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isBootstrapping, setIsBootstrapping] = useState(true);

  useEffect(() => {
    // apiClient dispatches this when a 401 survives a refresh attempt — a plain
    // module can't call useNavigate() directly, so it decouples via a DOM event.
    const handleLogout = () => setUser(null);
    window.addEventListener("auth:logout", handleLogout);
    return () => window.removeEventListener("auth:logout", handleLogout);
  }, []);

  useEffect(() => {
    // §6a: the refresh token lives in an httpOnly cookie the browser sends
    // automatically, so a reload can silently restore the session by minting a fresh
    // access token from it, with no re-login prompt — as long as the cookie is still
    // valid. A failure here just means "no valid session," not an error to surface.
    (async () => {
      try {
        const data = await apiClient.post<LoginResponse>("/api/auth/refresh", undefined, { skipAuth: true });
        tokenStore.set(data);
        setUser(data.user);
      } catch {
        // No valid session cookie — stay logged out.
      } finally {
        setIsBootstrapping(false);
      }
    })();
  }, []);

  const loginMutation = useMutation({
    mutationFn: ({ username, password }: { username: string; password: string }) =>
      apiClient.post<LoginResponse>("/api/auth/login", { username, password }, { skipAuth: true }),
    onSuccess: (data) => {
      tokenStore.set(data);
      setUser(data.user);
    },
  });

  const login = async (username: string, password: string) => {
    await loginMutation.mutateAsync({ username, password });
  };

  const logout = async () => {
    try {
      await apiClient.post("/api/auth/logout", undefined, { skipAuth: true });
    } finally {
      tokenStore.clear();
      setUser(null);
    }
  };

  const value: AuthContextValue = {
    user,
    isAuthenticated: user !== null,
    isLoggingIn: loginMutation.isPending,
    isBootstrapping,
    login,
    logout,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within an AuthProvider");
  return ctx;
}
