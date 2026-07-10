import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { useMutation } from "@tanstack/react-query";

import { apiClient } from "@/lib/apiClient";
import { tokenStore, type TokenSet } from "@/lib/tokenStore";

export interface AuthUser {
  id: number;
  username: string;
  role: "Admin" | "Staff";
}

interface LoginResponse extends TokenSet {
  user: AuthUser;
}

interface AuthContextValue {
  user: AuthUser | null;
  isAuthenticated: boolean;
  isLoggingIn: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);

  useEffect(() => {
    // apiClient dispatches this when a 401 survives a refresh attempt — a plain
    // module can't call useNavigate() directly, so it decouples via a DOM event.
    const handleLogout = () => setUser(null);
    window.addEventListener("auth:logout", handleLogout);
    return () => window.removeEventListener("auth:logout", handleLogout);
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

  const logout = () => {
    tokenStore.clear();
    setUser(null);
  };

  const value: AuthContextValue = {
    user,
    isAuthenticated: user !== null,
    isLoggingIn: loginMutation.isPending,
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
