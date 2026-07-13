import type { LucideIcon } from "lucide-react";

import { cn } from "@/lib/utils";

// Shared status-vocabulary primitive for the redesign: every place the app shows a
// state (delivery status, task status/priority, audit action, template trigger type)
// renders through this one component so a given tone always means the same thing
// across screens. Icon + label always paired, never color alone.
export type StatusTone = "neutral" | "progress" | "success" | "danger" | "info" | "muted";

const TONE_STYLES: Record<StatusTone, string> = {
  neutral: "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300",
  progress: "bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-200",
  success: "bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-200",
  danger: "bg-red-100 text-red-800 dark:bg-red-950 dark:text-red-200",
  info: "bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-200",
  muted: "bg-purple-100 text-purple-700 dark:bg-purple-950 dark:text-purple-300",
};

const TONE_ICON_STYLES: Record<StatusTone, string> = {
  neutral: "text-slate-500 dark:text-slate-400",
  progress: "text-amber-600 dark:text-amber-400",
  success: "text-emerald-600 dark:text-emerald-400",
  danger: "text-red-600 dark:text-red-400",
  info: "text-blue-600 dark:text-blue-400",
  muted: "text-purple-500 dark:text-purple-400",
};

export interface StatusBadgeConfig {
  icon: LucideIcon;
  tone: StatusTone;
  label: string;
  /** Only "progress" tones spin by default (e.g. Sending); opt out per-entry if not wanted. */
  spin?: boolean;
}

export function StatusBadge({
  icon: Icon,
  tone,
  label,
  spin = false,
  size = "sm",
  className,
}: StatusBadgeConfig & { size?: "sm" | "xs"; className?: string }) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 rounded-full px-2 py-0.5 font-medium",
        size === "xs" ? "text-2xs" : "text-xs",
        TONE_STYLES[tone],
        className,
      )}
    >
      <Icon
        className={cn(
          size === "xs" ? "size-3" : "size-3.5",
          TONE_ICON_STYLES[tone],
          spin && "animate-spin",
        )}
      />
      {label}
    </span>
  );
}
