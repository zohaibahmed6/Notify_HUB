// Same-origin-by-default: derive the API's base URL from whatever hostname the browser
// used to load the page, just swapping the port (frontend 5173 -> backend 5000). This
// resolves correctly whether the page was reached via localhost (a developer's own
// browser) or any other hostname (e.g. host.docker.internal, when the Playwright e2e
// suite runs the browser inside a sibling container) — a fixed VITE_API_URL baked at
// dev-server-start time can only ever be correct for one of those audiences at once.
// VITE_API_URL remains available as an explicit override for setups where the
// same-hostname-different-port assumption doesn't hold.
export const API_BASE_URL =
  import.meta.env.VITE_API_URL || `${window.location.protocol}//${window.location.hostname}:5000`;
