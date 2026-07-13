export type UIVersion = "legacy" | "redesign";

const STORAGE_KEY = "notifyhub:ui-version";

// Build-time default: "redesign" unless VITE_UI_VERSION=legacy is set in the frontend's
// env (e.g. to demo the old screens deliberately). Runtime override (localStorage, set
// via the Configuration screen) always wins over this default, so a single build can be
// flipped live without a rebuild. First-ever visit (no stored preference yet) now lands
// on the redesign, including /login — product decision 2026-07-13.
const DEFAULT_VERSION: UIVersion =
  import.meta.env.VITE_UI_VERSION === "legacy" ? "legacy" : "redesign";

export function getStoredUIVersion(): UIVersion | null {
  const raw = localStorage.getItem(STORAGE_KEY);
  return raw === "legacy" || raw === "redesign" ? raw : null;
}

export function setStoredUIVersion(version: UIVersion): void {
  localStorage.setItem(STORAGE_KEY, version);
}

export function getInitialUIVersion(): UIVersion {
  return getStoredUIVersion() ?? DEFAULT_VERSION;
}

export const UI_VERSION_STORAGE_KEY = STORAGE_KEY;
