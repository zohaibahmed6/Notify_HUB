import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { Repeat, Send } from "lucide-react";
import { toast } from "sonner";

import { useForwardTaskMutation, useTask, useUpdateTaskMutation } from "@/hooks/useTasks";
import { useAssignableUsers } from "@/hooks/useUsers";
import { errorMessage } from "@/lib/errorMessage";
import { formatUserLabel, formatUserName } from "@/lib/userDisplay";
import { InitialsAvatar } from "@/components/v2/initials-avatar";
import { StatusBadge } from "@/components/v2/status-badge";
import { TASK_PRIORITY_CONFIG, TASK_STATUS_CONFIG } from "@/components/v2/status-config";
import { isTaskOverdue } from "@/components/v2/task-card";
import { encodeThreadId } from "@/lib/threadIdCodec";
import { formatUtc } from "@/lib/dateUtc";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { Skeleton } from "@/components/ui/skeleton";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";

// Rendering useTask(taskId) while the sheet is open is what fires GET /api/tasks/{id} and
// triggers the backend's BR-014 escalated -> in_progress revert — same trigger mechanism
// as the legacy TaskDetailPanel, just Sheet-based instead of an inline expando.
export function TaskDetailSheet({
  taskId,
  onOpenChange,
  onAssignToMe,
  onComplete,
  isMutating,
  currentUserId,
}: {
  taskId: number | null;
  onOpenChange: (open: boolean) => void;
  onAssignToMe: (taskId: number) => void;
  onComplete: (taskId: number) => void;
  isMutating: boolean;
  currentUserId?: number;
}) {
  const navigate = useNavigate();
  const { data: task, isLoading } = useTask(taskId);
  const queryClient = useQueryClient();
  const updateTask = useUpdateTaskMutation();
  const forwardTask = useForwardTaskMutation();
  const { data: assignableUsers } = useAssignableUsers();

  const [forwardOpen, setForwardOpen] = useState(false);
  const [forwardTargetId, setForwardTargetId] = useState<string>("");
  const [forwardNote, setForwardNote] = useState("");

  useEffect(() => {
    if (task) {
      queryClient.invalidateQueries({ queryKey: ["tasks"] });
    }
  }, [task, queryClient]);

  const isActionable = task && task.status !== "Completed" && task.status !== "Cancelled";
  const isAssignedToCurrentUser = task?.assignedStaffId != null && task.assignedStaffId === currentUserId;

  // P9-01c: the sheet auto-closes after any action taken on the task from within it
  // (forward, complete, active/inactive toggle, assign-to-me, or any other
  // reassignment) — on success only, so a failed action leaves the sheet open with its
  // error toast visible in context rather than silently closing on a no-op.
  const handleToggleActive = async () => {
    if (!task) return;
    try {
      await updateTask.mutateAsync({ id: task.id, isActive: !task.isActive });
      toast.success(task.isActive ? "Task marked inactive" : "Task marked active");
      onOpenChange(false);
    } catch (error) {
      toast.error(errorMessage(error, "Update failed"));
    }
  };

  const handleForward = async () => {
    if (!task || !forwardTargetId) return;
    try {
      await forwardTask.mutateAsync({ id: task.id, targetUserId: Number(forwardTargetId), note: forwardNote || undefined });
      toast.success("Task forwarded");
      setForwardOpen(false);
      setForwardTargetId("");
      setForwardNote("");
      onOpenChange(false);
    } catch (error) {
      toast.error(errorMessage(error, "Forward failed"));
    }
  };

  const handleAssignToMe = async () => {
    if (!task) return;
    await onAssignToMe(task.id);
    onOpenChange(false);
  };

  const handleCompleteTask = async () => {
    if (!task) return;
    await onComplete(task.id);
    onOpenChange(false);
  };

  return (
    <Sheet open={taskId !== null} onOpenChange={onOpenChange}>
      <SheetContent className="flex flex-col gap-6">
        {isLoading || !task ? (
          <div className="space-y-3 pt-8">
            <Skeleton className="h-5 w-1/2" />
            <Skeleton className="h-4 w-1/3" />
            <Skeleton className="h-4 w-2/3" />
          </div>
        ) : (
          <>
            <SheetHeader>
              <SheetTitle>{task.patientName}</SheetTitle>
              <SheetDescription>
                <button
                  type="button"
                  onClick={() => navigate(`/inbox?thread=${encodeThreadId(task.threadId)}`)}
                  className="text-primary underline-offset-2 hover:underline"
                >
                  View conversation
                </button>
              </SheetDescription>
            </SheetHeader>

            <div className="flex flex-wrap items-center gap-2">
              <StatusBadge {...TASK_PRIORITY_CONFIG[task.priority]} />
              <StatusBadge {...TASK_STATUS_CONFIG[task.status]} />
              <span className="rounded-full border px-2 py-0.5 text-2xs text-muted-foreground">{task.taskType}</span>
              {!task.isActive && (
                <span className="rounded-full border border-dashed px-2 py-0.5 text-2xs text-muted-foreground">Inactive</span>
              )}
            </div>

            <div className="space-y-3 text-sm">
              <div className={cn(isTaskOverdue(task) && "font-medium text-destructive")}>
                {isTaskOverdue(task) ? "Overdue — due " : "Due "}
                {formatUtc(task.dueAt)}
              </div>

              {task.originalOwnerId !== task.assignedStaffId ? (
                <div className="space-y-1.5">
                  <div className="flex items-center gap-2 text-muted-foreground">
                    <InitialsAvatar
                      name={formatUserName({ fullName: task.originalOwnerFullName, username: task.originalOwnerUsername })}
                      size="sm"
                    />
                    Originally assigned to{" "}
                    {formatUserLabel({
                      fullName: task.originalOwnerFullName,
                      username: task.originalOwnerUsername,
                      role: task.originalOwnerRole,
                    })}
                  </div>
                  <div className="flex items-center gap-2 text-muted-foreground">
                    {task.assignedStaffUsername ? (
                      <>
                        <InitialsAvatar
                          name={formatUserName({ fullName: task.assignedStaffFullName, username: task.assignedStaffUsername })}
                          size="sm"
                        />
                        Now assigned to{" "}
                        {formatUserLabel({
                          fullName: task.assignedStaffFullName,
                          username: task.assignedStaffUsername,
                          role: task.assignedStaffRole,
                        })}
                      </>
                    ) : (
                      "Currently unassigned"
                    )}
                  </div>
                </div>
              ) : (
                <div className="flex items-center gap-2 text-muted-foreground">
                  {task.assignedStaffUsername ? (
                    <>
                      <InitialsAvatar
                        name={formatUserName({ fullName: task.assignedStaffFullName, username: task.assignedStaffUsername })}
                        size="sm"
                      />
                      Assigned to{" "}
                      {formatUserLabel({
                        fullName: task.assignedStaffFullName,
                        username: task.assignedStaffUsername,
                        role: task.assignedStaffRole,
                      })}
                    </>
                  ) : (
                    "Unassigned"
                  )}
                </div>
              )}

              {task.isRecurring && (
                <div className="flex items-center gap-1.5 text-muted-foreground">
                  <Repeat className="size-3.5" />
                  Recurring every {task.recurrenceIntervalDays} day(s) · occurrence #{task.occurrenceCount}
                </div>
              )}

              {task.description && (
                <div>
                  <div className="mb-1 text-xs font-medium text-muted-foreground">Description</div>
                  <p className="whitespace-pre-wrap rounded-md border bg-muted/40 p-2">{task.description}</p>
                </div>
              )}
            </div>

            <SheetFooter className="mt-auto flex-col gap-2 sm:flex-col">
              <div className="flex w-full flex-col gap-2 sm:flex-row">
                <Button variant="outline" className="flex-1" onClick={handleToggleActive} disabled={updateTask.isPending}>
                  {task.isActive ? "Mark inactive" : "Mark active"}
                </Button>
                <Button variant="outline" className="flex-1 gap-1.5" onClick={() => setForwardOpen(true)}>
                  <Send className="size-3.5" />
                  Forward
                </Button>
              </div>
              {isActionable && (
                <div className="flex w-full flex-col gap-2 sm:flex-row">
                  {!isAssignedToCurrentUser && (
                    <Button variant="outline" className="flex-1" onClick={handleAssignToMe} disabled={isMutating}>
                      Assign to me
                    </Button>
                  )}
                  <Button className="flex-1" onClick={handleCompleteTask} disabled={isMutating}>
                    Complete
                  </Button>
                </div>
              )}
            </SheetFooter>
          </>
        )}
      </SheetContent>

      <Dialog open={forwardOpen} onOpenChange={setForwardOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Forward task</DialogTitle>
            <DialogDescription>Reassign this task to another active user.</DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1.5">
              <Label>Forward to</Label>
              <Select value={forwardTargetId} onValueChange={setForwardTargetId}>
                <SelectTrigger>
                  <SelectValue placeholder="Select a user..." />
                </SelectTrigger>
                <SelectContent>
                  {/* Excludes the current assignee — forwarding to the same person isn't a
                      real reassignment. Only the current assignee, not the original owner. */}
                  {(assignableUsers ?? [])
                    .filter((u) => u.id !== task?.assignedStaffId)
                    .map((u) => (
                      <SelectItem key={u.id} value={String(u.id)}>
                        {formatUserLabel(u)}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="forward-note">Note (optional)</Label>
              <Textarea id="forward-note" rows={2} value={forwardNote} onChange={(event) => setForwardNote(event.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="ghost" onClick={() => setForwardOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleForward} disabled={!forwardTargetId || forwardTask.isPending}>
              {forwardTask.isPending ? "Forwarding..." : "Forward"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Sheet>
  );
}
