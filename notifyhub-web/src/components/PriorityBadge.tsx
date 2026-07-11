import { cn } from "@/lib/utils";
import type { TaskPriority } from "@/types/tasks";

// §6c: "Task priority display — color badge always paired with a text label, never
// color alone." Plain utility colors rather than extending the theme — the app has no
// other use for a 4-step severity scale.
const PRIORITY_STYLES: Record<TaskPriority, string> = {
  Low: "bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-200",
  Medium: "bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-200",
  High: "bg-orange-100 text-orange-800 dark:bg-orange-950 dark:text-orange-200",
  Urgent: "bg-red-100 text-red-800 dark:bg-red-950 dark:text-red-200",
};

export function PriorityBadge({ priority }: { priority: TaskPriority }) {
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium",
        PRIORITY_STYLES[priority],
      )}
    >
      {priority}
    </span>
  );
}
