import { Repeat } from "lucide-react";

import { InitialsAvatar } from "@/components/v2/initials-avatar";
import { StatusBadge } from "@/components/v2/status-badge";
import { TASK_PRIORITY_CONFIG } from "@/components/v2/status-config";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import type { TaskDto } from "@/types/tasks";

export function isTaskOverdue(task: TaskDto): boolean {
  return new Date(task.dueAt) < new Date() && task.status !== "Completed" && task.status !== "Cancelled";
}

export function TaskCard({
  task,
  threadName,
  onOpen,
  onAssignToMe,
  onComplete,
  isMutating,
}: {
  task: TaskDto;
  threadName?: string;
  onOpen: () => void;
  onAssignToMe: () => void;
  onComplete: () => void;
  isMutating: boolean;
}) {
  const overdue = isTaskOverdue(task);
  const isActionable = task.status !== "Completed" && task.status !== "Cancelled";
  const priority = TASK_PRIORITY_CONFIG[task.priority];

  return (
    <div
      role="button"
      tabIndex={0}
      onClick={onOpen}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          onOpen();
        }
      }}
      className="w-full cursor-pointer rounded-lg border bg-card p-3 text-left shadow-sm transition-shadow hover:shadow-md"
    >
      <div className="flex items-start justify-between gap-2">
        <StatusBadge {...priority} size="xs" />
        {task.isRecurring && (
          <span className="flex items-center gap-1 text-2xs text-muted-foreground" title={`Recurring · occurrence #${task.occurrenceCount}`}>
            <Repeat className="size-3.5" />
          </span>
        )}
      </div>

      <div className="mt-2 truncate text-sm font-medium">
        {threadName ?? `Thread #${task.threadId}`}
      </div>

      <div className={cn("mt-1 text-xs", overdue ? "font-medium text-destructive" : "text-muted-foreground")}>
        {overdue ? "Overdue · " : "Due "}
        {new Date(task.dueAt).toLocaleDateString(undefined, { month: "short", day: "numeric", hour: "numeric", minute: "2-digit" })}
      </div>

      <div className="mt-3 flex items-center justify-between gap-2">
        {task.assignedStaffUsername ? (
          <div className="flex min-w-0 items-center gap-1.5">
            <InitialsAvatar name={task.assignedStaffUsername} size="sm" />
            <span className="truncate text-xs text-muted-foreground">{task.assignedStaffUsername}</span>
          </div>
        ) : (
          <span className="text-xs text-muted-foreground">Unassigned</span>
        )}

        {isActionable && (
          <div className="flex shrink-0 gap-1" onClick={(e) => e.stopPropagation()}>
            <Button
              variant="ghost"
              size="sm"
              className="h-6 px-1.5 text-2xs"
              onClick={onAssignToMe}
              disabled={isMutating}
            >
              Assign to me
            </Button>
            <Button size="sm" className="h-6 px-1.5 text-2xs" onClick={onComplete} disabled={isMutating}>
              Complete
            </Button>
          </div>
        )}
      </div>
    </div>
  );
}
