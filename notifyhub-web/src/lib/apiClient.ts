import { tokenStore } from "./tokenStore";

const BASE_URL = import.meta.env.VITE_API_URL;

export class ApiError extends Error {
  status: number;
  problem: unknown;

  constructor(status: number, problem: unknown) {
    super(`API error ${status}`);
    this.status = status;
    this.problem = problem;
  }
}

interface RequestOptions extends RequestInit {
  /** Skip attaching the Authorization header and skip the 401-refresh flow (e.g. login itself). */
  skipAuth?: boolean;
}

// A single in-flight refresh is shared across concurrent 401s so we don't fire
// multiple refresh requests (and rotate the refresh token multiple times) at once.
let refreshPromise: Promise<boolean> | null = null;

async function refreshTokens(): Promise<boolean> {
  const refreshToken = tokenStore.getRefreshToken();
  if (!refreshToken) return false;

  const response = await fetch(`${BASE_URL}/api/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
  });

  if (!response.ok) return false;

  const data = await response.json();
  tokenStore.set(data);
  return true;
}

async function request<T>(path: string, options: RequestOptions = {}, isRetry = false): Promise<T> {
  const headers = new Headers(options.headers);
  if (!headers.has("Content-Type") && options.body) {
    headers.set("Content-Type", "application/json");
  }

  const accessToken = tokenStore.getAccessToken();
  if (accessToken && !options.skipAuth) {
    headers.set("Authorization", `Bearer ${accessToken}`);
  }

  const response = await fetch(`${BASE_URL}${path}`, { ...options, headers });

  if (response.status === 401 && !options.skipAuth && !isRetry) {
    if (!refreshPromise) {
      refreshPromise = refreshTokens().finally(() => {
        refreshPromise = null;
      });
    }
    const refreshed = await refreshPromise;

    if (refreshed) {
      return request<T>(path, options, true);
    }

    tokenStore.clear();
    window.dispatchEvent(new CustomEvent("auth:logout"));
    throw new ApiError(401, null);
  }

  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    throw new ApiError(response.status, problem);
  }

  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}

export const apiClient = {
  get: <T,>(path: string, options?: RequestOptions) => request<T>(path, { ...options, method: "GET" }),
  post: <T,>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>(path, {
      ...options,
      method: "POST",
      body: body !== undefined ? JSON.stringify(body) : undefined,
    }),
  patch: <T,>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>(path, {
      ...options,
      method: "PATCH",
      body: body !== undefined ? JSON.stringify(body) : undefined,
    }),
};
