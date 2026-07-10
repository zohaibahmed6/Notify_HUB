// Plain module-level singleton — not a React construct — so apiClient.ts can read/write
// tokens from outside component/hook context (e.g. inside TanStack Query queryFns).
// Deliberately in-memory only (no localStorage) to reduce XSS exposure (§6a); a hard
// page reload forces re-login even within the refresh token's window.

export interface TokenSet {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
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
  getRefreshToken(): string | null {
    return tokens?.refreshToken ?? null;
  },
};
