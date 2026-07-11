// Plain module-level singleton — not a React construct — so apiClient.ts can read/write
// tokens from outside component/hook context (e.g. inside TanStack Query queryFns).
// Only the access token lives here, in memory (wiped on reload, not readable across
// tabs). The refresh token never touches JS at all — it lives in an httpOnly cookie
// (§6a) set by the backend, so a page reload can silently restore the session via
// POST /api/auth/refresh (cookie sent automatically) without ever exposing the refresh
// token to script.

export interface TokenSet {
  accessToken: string;
  accessTokenExpiresAt: string;
}

let tokens: TokenSet | null = null;

export const tokenStore = {
  get(): TokenSet | null {
    return tokens;
  },
  set(next: TokenSet): void {
    tokens = next;
  },
  clear(): void {
    tokens = null;
  },
  getAccessToken(): string | null {
    return tokens?.accessToken ?? null;
  },
};
