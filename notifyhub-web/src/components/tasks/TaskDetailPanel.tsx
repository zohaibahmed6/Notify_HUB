import { useEffect } from "react";
import { useQueryClient } from "@tanstack/react-query";

import { useTask } from "@/hooks/useTasks";
import { formatUserLabel } from "@/lib/userDisplay";
import { PriorityBadge } from "@/components/PriorityBadge";
import { TaskStatusBadge } from "@/components/TaskStatusBadge";

/// Rendering this (i.e. the task being "open") is what fires GET /api/tasks/{id} and
/// triggers the backend's BR-014 escalated -> in_progress revert. Once the fetch
/// resolves, invalidate the board's list query too, so a reverted status shows up there
/// without a manual refresh.
export function TaskDetailPanel({ taskId }: { taskId: number }) {
  const { data: task, isLoading } = useTask(taskId);
  const queryClient = useQueryClient();

  useEffect(() => {
    if (task) {
      queryClient.invalidateQueries({ queryKey: ["tasks"] });
    }
  }, [task, queryClient]);

  if (isLoading || !task) {
    return <div className="border-t bg-muted/40 p-3 text-sm text-muted-foreground">Loading...</div>;
  }

  return (
    <div className="space-y-2 border-t bg-muted/40 p-3 text-sm">
      <div className="flex items-center gap-2">
        <PriorityBadge priority={task.priority} />
        <TaskStatusBadge status={task.status} />
      </div>
      <div>Due {new Date(task.dueAt).toLocaleString()}</div>
      <div className="text-muted-foreground">
        {task.assignedStaffUsername
          ? `Assigned to ${formatUserLabel({ fullName: task.assignedStaffFullName, username: task.assignedStaffUsername, role: task.assignedStaffRole })}`
          : "Unassigned"}
      </div>
      {task.isRecurring && (
        <div className="text-muted-foreground">
          Recurring every {task.recurrenceIntervalDays} day(s) · occurrence #{task.occurrenceCount}
        </div>
      )}
    </div>
  );
}
