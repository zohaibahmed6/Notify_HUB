import type { ReactNode } from "react";

import { cn } from "@/lib/utils";

// Shared dense inline-label filter grid, used by TaskBoardPageV2/SmsHistoryPage/
// AuditLogPageV2's filter bars (previously each screen duplicated its own
// label-above-input `space-y-1.5` stack with inconsistent widths). `FilterBar` is the
// grid wrapper; `FilterField` is one label+control row within it. Column count reduces
// lg:4 -> sm:2 -> 1 as the viewport narrows; the label always stays directly left of its
// control (never switches to label-above), so at 1 column each row is still a
// label:control pair, just full-width.
export function FilterBar({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cn("grid grid-cols-1 gap-x-6 gap-y-3 sm:grid-cols-2 lg:grid-cols-4", className)}>{children}</div>;
}

export function FilterField({
  label,
  htmlFor,
  children,
}: {
  label: string;
  htmlFor?: string;
  children: ReactNode;
}) {
  return (
    <div className="flex items-center gap-2">
      <label htmlFor={htmlFor} className="w-[100px] shrink-0 text-sm font-medium text-foreground">
        {label}:
      </label>
      <div className="min-w-0 flex-1">{children}</div>
    </div>
  );
}
