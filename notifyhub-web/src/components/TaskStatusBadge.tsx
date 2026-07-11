import { cn } from "@/lib/utils";
import type { TaskStatus } from "@/types/tasks";

const STATUS_STYLES: Record<TaskStatus, string> = {
  Open: "bg-slate-100 text-slate-800 dark:bg-slate-800 dark:text-slate-200",
  InProgress: "bg-sky-100 text-sky-800 dark:bg-sky-950 dark:text-sky-200",
  Completed: "bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-200",
  Escalated: "bg-red-100 text-red-800 dark:bg-red-950 dark:text-red-200",
  Cancelled: "bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400",
};

const STATUS_LABELS: Record<TaskStatus, string> = {
  Open: "Open",
  InProgress: "In progress",
  Completed: "Completed",
  Escalated: "Escalated",
  Cancelled: "Cancelled",
};

export function TaskStatusBadge({ status }: { status: TaskStatus }) {
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium",
        STATUS_STYLES[status],
      )}
    >
      {STATUS_LABELS[status]}
    </span>
  );
}
