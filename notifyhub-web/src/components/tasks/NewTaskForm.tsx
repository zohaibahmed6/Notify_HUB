import { useState, type FormEvent } from "react";
import { toast } from "sonner";

import { useThreads } from "@/hooks/useThreads";
import { useCreateTaskMutation } from "@/hooks/useThreads";
import { errorMessage } from "@/lib/errorMessage";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { TaskAssignmentFields } from "@/components/tasks/TaskAssignmentFields";
import { DateTimePicker } from "@/components/v2/date-time-picker";
import { defaultDueAt, formatDateTimeLocal } from "@/lib/taskDueDateDefaults";
import { TASK_TYPES, type TaskPriority, type TaskType } from "@/types/tasks";

const PRIORITIES: TaskPriority[] = ["Low", "Medium", "High", "Urgent"];

// The API only creates tasks against an existing thread (POST /api/threads/{id}/tasks —
// there's no standalone task-creation endpoint, matching FR-008's "message→task"
// framing), so "Create" on the Task board picks a thread first rather than creating
// a free-floating task.
export function NewTaskForm({ onDone }: { onDone: () => void }) {
  const { data: threadsData } = useThreads();
  const threads = threadsData?.items ?? [];

  const [threadId, setThreadId] = useState<number | "">("");
  const [assignedStaffId, setAssignedStaffId] = useState<number | "">("");
  const [priority, setPriority] = useState<TaskPriority>("Medium");
  // Date required, time optional (defaults 00:00) — P9-01d, now via the shared
  // DateTimePicker (P9-03) instead of two native inputs. FR-008: pre-filled with the
  // priority-based suggestion, recomputed as Priority changes until the user edits Due
  // Date themselves (dueAtTouched) — see docs/DECISIONS.md.
  const [dueAt, setDueAt] = useState(() => formatDateTimeLocal(defaultDueAt("Medium")));
  const [dueAtTouched, setDueAtTouched] = useState(false);
  const [taskType, setTaskType] = useState<TaskType>("General");
  const [description, setDescription] = useState("");

  // P9-11: creation-time only (matches the backend — next occurrence auto-spawns on
  // completion via SpawnNextOccurrenceIfDue, no edit-after-creation path exists or is
  // being added here).
  const [isRecurring, setIsRecurring] = useState(false);
  const [recurrenceIntervalDays, setRecurrenceIntervalDays] = useState("");
  const [recurrenceEndDate, setRecurrenceEndDate] = useState("");
  const [recurrenceMaxOccurrences, setRecurrenceMaxOccurrences] = useState("");

  const createTask = useCreateTaskMutation(threadId === "" ? -1 : threadId);
  const selectedThread = threads.find((t) => t.id === threadId);

  // Deliberately doesn't touch assignedStaffId: it should stay whatever it currently is
  // (the initial default, or a manual pick) across thread changes, not get re-derived from
  // the newly-selected thread's owner — see docs/DECISIONS.md.
  const handleThreadChange = (id: number) => {
    setThreadId(id);
  };

  const handlePriorityChange = (value: TaskPriority) => {
    setPriority(value);
    if (!dueAtTouched) setDueAt(formatDateTimeLocal(defaultDueAt(value)));
  };

  const handleDueAtChange = (value: string) => {
    setDueAt(value);
    setDueAtTouched(true);
  };

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    if (threadId === "") {
      toast.error("Pick a thread first");
      return;
    }
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
        // Left blank -> server auto-populates from the thread's most recent message.
        description: description || undefined,
        assignedStaffId,
        isRecurring,
        recurrenceIntervalDays: isRecurring ? Number(recurrenceIntervalDays) : undefined,
        recurrenceEndDate: isRecurring && recurrenceEndDate ? new Date(recurrenceEndDate).toISOString() : undefined,
        recurrenceMaxOccurrences: isRecurring && recurrenceMaxOccurrences ? Number(recurrenceMaxOccurrences) : undefined,
      });
      toast.success("Task created");
      onDone();
    } catch (error) {
      toast.error(errorMessage(error, "Task creation failed"));
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div className="space-y-1.5">
        <Label htmlFor="new-task-thread">Thread</Label>
        <Select
          value={threadId === "" ? undefined : String(threadId)}
          onValueChange={(v) => handleThreadChange(Number(v))}
        >
          <SelectTrigger id="new-task-thread">
            <SelectValue placeholder="Select a thread..." />
          </SelectTrigger>
          <SelectContent>
            {threads.map((thread) => (
              <SelectItem key={thread.id} value={String(thread.id)}>
                {thread.patientName}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <TaskAssignmentFields
        threadAssignedStaffId={selectedThread?.assignedStaffId}
        value={assignedStaffId}
        onChange={setAssignedStaffId}
      />
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <div className="space-y-1.5">
          <Label htmlFor="new-task-priority">Priority</Label>
          <Select value={priority} onValueChange={(v) => handlePriorityChange(v as TaskPriority)}>
            <SelectTrigger id="new-task-priority">
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
          <Label htmlFor="new-task-due">Due date (time optional, defaults 00:00)</Label>
          <DateTimePicker id="new-task-due" value={dueAt} onChange={handleDueAtChange} timeRequired={false} />
        </div>
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="new-task-type">Task type</Label>
        <Select value={taskType} onValueChange={(v) => setTaskType(v as TaskType)}>
          <SelectTrigger id="new-task-type">
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
        <Label htmlFor="new-task-description">Description (optional — defaults to the thread's last message)</Label>
        <Textarea
          id="new-task-description"
          rows={2}
          value={description}
          onChange={(event) => setDescription(event.target.value)}
        />
      </div>
      <div className="space-y-2 rounded-md border p-3">
        <label htmlFor="new-task-recurring" className="flex items-center gap-2 text-sm">
          <input
            id="new-task-recurring"
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
              <Label htmlFor="new-task-recurrence-interval">Interval (days)</Label>
              <Input
                id="new-task-recurrence-interval"
                type="number"
                min={1}
                required
                value={recurrenceIntervalDays}
                onChange={(event) => setRecurrenceIntervalDays(event.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="new-task-recurrence-end">End date (optional)</Label>
              <DateTimePicker
                id="new-task-recurrence-end"
                mode="date"
                value={recurrenceEndDate}
                onChange={setRecurrenceEndDate}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="new-task-recurrence-max">Max occurrences (optional)</Label>
              <Input
                id="new-task-recurrence-max"
                type="number"
                min={1}
                value={recurrenceMaxOccurrences}
                onChange={(event) => setRecurrenceMaxOccurrences(event.target.value)}
              />
            </div>
          </div>
        )}
      </div>
      <div className="flex justify-end gap-2">
        <Button type="button" variant="ghost" size="sm" onClick={onDone}>
          Cancel
        </Button>
        <Button type="submit" size="sm" disabled={createTask.isPending}>
          {createTask.isPending ? "Creating..." : "Create task"}
        </Button>
      </div>
    </form>
  );
}
