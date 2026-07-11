import { useState } from "react";
import { toast } from "sonner";

import { useAuth } from "@/context/AuthContext";
import { useTasks, useUpdateTaskMutation } from "@/hooks/useTasks";
import { errorMessage } from "@/lib/errorMessage";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { PriorityBadge } from "@/components/PriorityBadge";
import { TaskStatusBadge } from "@/components/TaskStatusBadge";
import { NewTaskForm } from "@/components/tasks/NewTaskForm";
import type { TaskStatus } from "@/types/tasks";

const STATUS_FILTERS: (TaskStatus | "All")[] = ["All", "Open", "InProgress", "Escalated", "Completed", "Cancelled"];

export default function TaskBoardPage() {
  const { user } = useAuth();
  const [statusFilter, setStatusFilter] = useState<TaskStatus | "All">("All");
  const [showNewTaskForm, setShowNewTaskForm] = useState(false);

  const { data, isLoading } = useTasks(statusFilter);
  const updateTask = useUpdateTaskMutation();

  const tasks = data?.items ?? [];

  const handleComplete = async (taskId: number) => {
    try {
      await updateTask.mutateAsync({ id: taskId, status: "Completed" });
      toast.success("Task completed");
    } catch (error) {
      toast.error(errorMessage(error, "Update failed"));
    }
  };

  const handleAssignToMe = async (taskId: number) => {
    if (!user) return;
    try {
      await updateTask.mutateAsync({ id: taskId, assignedStaffId: user.id });
      toast.success("Task reassigned");
    } catch (error) {
      toast.error(errorMessage(error, "Update failed"));
    }
  };

  return (
    <div className="flex h-full flex-col overflow-y-auto p-4">
      <div className="mb-4 flex items-center justify-between">
        <div className="flex gap-1">
          {STATUS_FILTERS.map((status) => (
            <button
              key={status}
              type="button"
              onClick={() => setStatusFilter(status)}
              className={cn(
                "rounded-md px-3 py-1.5 text-sm font-medium transition-colors hover:bg-accent",
                statusFilter === status && "bg-accent text-accent-foreground",
              )}
            >
              {status === "InProgress" ? "In progress" : status}
            </button>
          ))}
        </div>
        <Button size="sm" onClick={() => setShowNewTaskForm((v) => !v)}>
          New task
        </Button>
      </div>

      {showNewTaskForm && (
        <div className="mb-4">
          <NewTaskForm onDone={() => setShowNewTaskForm(false)} />
        </div>
      )}

      {isLoading ? (
        <p className="text-sm text-muted-foreground">Loading...</p>
      ) : tasks.length === 0 ? (
        <div className="flex flex-1 flex-col items-center justify-center gap-1 text-center">
          <p className="font-medium">Your task board is clear</p>
          <p className="text-sm text-muted-foreground">Follow-ups created from the inbox will show up here.</p>
        </div>
      ) : (
        <div className="space-y-2">
          {tasks.map((task) => {
            const isOverdue = new Date(task.dueAt) < new Date() && task.status !== "Completed" && task.status !== "Cancelled";

            return (
              <div
                key={task.id}
                className="flex items-center justify-between gap-4 rounded-lg border p-3"
              >
                <div className="flex min-w-0 items-center gap-3">
                  <PriorityBadge priority={task.priority} />
                  <TaskStatusBadge status={task.status} />
                  <div className="min-w-0">
                    <div className={cn("text-sm", isOverdue && "font-medium text-destructive")}>
                      Due {new Date(task.dueAt).toLocaleString()}
                      {task.isRecurring && ` · recurring (#${task.occurrenceCount})`}
                    </div>
                    <div className="text-xs text-muted-foreground">
                      {task.assignedStaffUsername ? `Assigned to ${task.assignedStaffUsername}` : "Unassigned"}
                    </div>
                  </div>
                </div>
                <div className="flex shrink-0 gap-2">
                  {task.status !== "Completed" && task.status !== "Cancelled" && (
                    <>
                      <Button variant="outline" size="sm" onClick={() => handleAssignToMe(task.id)}>
                        Assign to me
                      </Button>
                      <Button size="sm" onClick={() => handleComplete(task.id)}>
                        Complete
                      </Button>
                    </>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
