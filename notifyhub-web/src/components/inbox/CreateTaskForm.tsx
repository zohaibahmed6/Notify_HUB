import { useEffect, useState, type FormEvent } from "react";
import { toast } from "sonner";

import { useCreateTaskMutation } from "@/hooks/useThreads";
import { errorMessage } from "@/lib/errorMessage";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { TaskAssignmentFields } from "@/components/tasks/TaskAssignmentFields";
import { DateTimePicker } from "@/components/v2/date-time-picker";
import { TASK_TYPES, type TaskPriority, type TaskType } from "@/types/tasks";

const PRIORITIES: TaskPriority[] = ["Low", "Medium", "High", "Urgent"];

export function CreateTaskForm({
  threadId,
  threadAssignedStaffId,
  open,
  onOpenChange,
}: {
  threadId: number;
  threadAssignedStaffId: number | null | undefined;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const [assignedStaffId, setAssignedStaffId] = useState<number | "">("");
  const [priority, setPriority] = useState<TaskPriority>("Medium");
  // Date required, time optional (defaults 00:00) — P9-01d, via the shared
  // DateTimePicker (P9-03).
  const [dueAt, setDueAt] = useState("");
  const [taskType, setTaskType] = useState<TaskType>("General");
  const [description, setDescription] = useState("");

  // P9-11: creation-time only, matches the backend (next occurrence auto-spawns on
  // completion, no edit-after-creation path).
  const [isRecurring, setIsRecurring] = useState(false);
  const [recurrenceIntervalDays, setRecurrenceIntervalDays] = useState("");
  const [recurrenceEndDate, setRecurrenceEndDate] = useState("");
  const [recurrenceMaxOccurrences, setRecurrenceMaxOccurrences] = useState("");

  const createTask = useCreateTaskMutation(threadId);

  // Reset the assignee pick each time the dialog is reopened so it re-derives from the
  // thread's current assignee/default provider rather than sticking to a stale choice.
  useEffect(() => {
    if (open) setAssignedStaffId("");
  }, [open]);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    if (!dueAt) {
      toast.error("Pick a due date");
      return;
    }
    if (!assignedStaffId) {
      toast.error("Pick an assignee");
      return;
    }
    if (isRecurring && !recurrenceIntervalDays) {
      toast.error("Pick a recurrence interval");
      return;
    }

    try {
      await createTask.mutateAsync({
        priority,
        dueAt: new Date(dueAt).toISOString(),
        taskType,
        description: description || undefined,
        assignedStaffId,
        isRecurring,
        recurrenceIntervalDays: isRecurring ? Number(recurrenceIntervalDays) : undefined,
        recurrenceEndDate: isRecurring && recurrenceEndDate ? new Date(recurrenceEndDate).toISOString() : undefined,
        recurrenceMaxOccurrences: isRecurring && recurrenceMaxOccurrences ? Number(recurrenceMaxOccurrences) : undefined,
      });
      toast.success("Task created");
      onOpenChange(false);
    } catch (error) {
      toast.error(errorMessage(error, "Task creation failed"));
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Create task</DialogTitle>
          <DialogDescription>Follow up on this thread with a task assigned to yourself or a teammate.</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <TaskAssignmentFields
            threadAssignedStaffId={threadAssignedStaffId}
            value={assignedStaffId}
            onChange={setAssignedStaffId}
          />
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <div className="space-y-1.5">
              <Label htmlFor="task-priority">Priority</Label>
              <Select value={priority} onValueChange={(v) => setPriority(v as TaskPriority)}>
                <SelectTrigger id="task-priority">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {PRIORITIES.map((p) => (
                    <SelectItem key={p} value={p}>
                      {p}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5 sm:col-span-2">
              <Label htmlFor="task-due">Due date (time optional, defaults 00:00)</Label>
              <DateTimePicker id="task-due" value={dueAt} onChange={setDueAt} timeRequired={false} />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="task-type">Task type</Label>
            <Select value={taskType} onValueChange={(v) => setTaskType(v as TaskType)}>
              <SelectTrigger id="task-type">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {TASK_TYPES.map((t) => (
                  <SelectItem key={t} value={t}>
                    {t}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="task-description">Description (optional — defaults to this thread's last message)</Label>
            <Textarea id="task-description" rows={2} value={description} onChange={(event) => setDescription(event.target.value)} />
          </div>
          <div className="space-y-2 rounded-md border p-3">
            <label htmlFor="task-recurring" className="flex items-center gap-2 text-sm">
              <input
                id="task-recurring"
                type="checkbox"
                checked={isRecurring}
                onChange={(event) => setIsRecurring(event.target.checked)}
                className="size-4 rounded border-input"
              />
              Recurring
            </label>
            {isRecurring && (
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                <div className="space-y-1.5">
                  <Label htmlFor="task-recurrence-interval">Interval (days)</Label>
                  <Input
                    id="task-recurrence-interval"
                    type="number"
                    min={1}
                    required
                    value={recurrenceIntervalDays}
                    onChange={(event) => setRecurrenceIntervalDays(event.target.value)}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="task-recurrence-end">End date (optional)</Label>
                  <DateTimePicker id="task-recurrence-end" mode="date" value={recurrenceEndDate} onChange={setRecurrenceEndDate} />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="task-recurrence-max">Max occurrences (optional)</Label>
                  <Input
                    id="task-recurrence-max"
                    type="number"
                    min={1}
                    value={recurrenceMaxOccurrences}
                    onChange={(event) => setRecurrenceMaxOccurrences(event.target.value)}
                  />
                </div>
              </div>
            )}
          </div>
          <DialogFooter>
            <Button type="button" variant="ghost" size="sm" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" size="sm" disabled={createTask.isPending}>
              {createTask.isPending ? "Creating..." : "Create task"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
