import { useState, type FormEvent } from "react";
import { toast } from "sonner";

import { useCreateTaskMutation } from "@/hooks/useThreads";
import { errorMessage } from "@/lib/errorMessage";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { DateTimePicker } from "@/components/v2/date-time-picker";
import { TASK_TYPES, type TaskPriority, type TaskType } from "@/types/tasks";

const PRIORITIES: TaskPriority[] = ["Low", "Medium", "High", "Urgent"];

export function CreateTaskForm({ threadId, onDone }: { threadId: number; onDone: () => void }) {
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

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    if (!dueAt) {
      toast.error("Pick a due date");
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
    <form onSubmit={handleSubmit} className="space-y-3 rounded-md border bg-muted/40 p-3">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <div className="space-y-1.5">
          <Label htmlFor="task-priority">Priority</Label>
          <select
            id="task-priority"
            value={priority}
            onChange={(event) => setPriority(event.target.value as TaskPriority)}
            className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          >
            {PRIORITIES.map((p) => (
              <option key={p} value={p}>
                {p}
              </option>
            ))}
          </select>
        </div>
        <div className="space-y-1.5 sm:col-span-2">
          <Label htmlFor="task-due">Due date (time optional, defaults 00:00)</Label>
          <DateTimePicker id="task-due" value={dueAt} onChange={setDueAt} timeRequired={false} />
        </div>
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="task-type">Task type</Label>
        <select
          id="task-type"
          value={taskType}
          onChange={(event) => setTaskType(event.target.value as TaskType)}
          className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
        >
          {TASK_TYPES.map((t) => (
            <option key={t} value={t}>
              {t}
            </option>
          ))}
        </select>
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
