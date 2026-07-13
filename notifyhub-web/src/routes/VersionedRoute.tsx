import type { ComponentType } from "react";

import { useUIVersion } from "@/context/UIVersionContext";

// Renders the redesigned screen when the UI-version toggle is set to "redesign", the
// existing (legacy) screen otherwise. Every screen currently reads its own data via
// hooks and takes no props, so this stays a simple swap rather than a prop-forwarding
// wrapper — if a screen ever needs props, extend this rather than the call sites.
export function VersionedRoute({
  Legacy,
  Redesign,
}: {
  Legacy: ComponentType;
  Redesign: ComponentType;
}) {
  const { version } = useUIVersion();
  return version === "redesign" ? <Redesign /> : <Legacy />;
}
