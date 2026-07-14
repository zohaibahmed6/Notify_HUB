import { useState, type FormEvent } from "react";
import { toast } from "sonner";

import { useThreads } from "@/hooks/useThreads";
import { useCreateTaskMutation } from "@/hooks/useThreads";
import { errorMessage } from "@/lib/errorMessage";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { DateTimePicker } from "@/components/v2/date-time-picker";
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
  const [priority, setPriority] = useState<TaskPriority>("Medium");
  // Date required, time optional (defaults 00:00) — P9-01d, now via the shared
  // DateTimePicker (P9-03) instead of two native inputs.
  const [dueAt, setDueAt] = useState("");
  const [taskType, setTaskType] = useState<TaskType>("General");
  const [description, setDescription] = useState("");

  const createTask = useCreateTaskMutation(threadId === "" ? -1 : threadId);

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

    try {
      await createTask.mutateAsync({
        priority,
        dueAt: new Date(dueAt).toISOString(),
        taskType,
        // Left blank -> server auto-populates from the thread's most recent message.
        description: description || undefined,
      });
      toast.success("Task created");
      onDone();
    } catch (error) {
      toast.error(errorMessage(error, "Task creation failed"));
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-3 rounded-md border bg-muted/40 p-4">
      <div className="space-y-1.5">
        <Label htmlFor="new-task-thread">Thread</Label>
        <select
          id="new-task-thread"
          value={threadId}
          onChange={(event) => setThreadId(event.target.value ? Number(event.target.value) : "")}
          className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
        >
          <option value="">Select a thread...</option>
          {threads.map((thread) => (
            <option key={thread.id} value={thread.id}>
              {thread.patientName}
            </option>
          ))}
        </select>
      </div>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <div className="space-y-1.5">
          <Label htmlFor="new-task-priority">Priority</Label>
          <select
            id="new-task-priority"
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
          <Label htmlFor="new-task-due">Due date (time optional, defaults 00:00)</Label>
          <DateTimePicker id="new-task-due" value={dueAt} onChange={setDueAt} timeRequired={false} />
        </div>
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="new-task-type">Task type</Label>
        <select
          id="new-task-type"
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
        <Label htmlFor="new-task-description">Description (optional — defaults to the thread's last message)</Label>
        <Textarea
          id="new-task-description"
          rows={2}
          value={description}
          onChange={(event) => setDescription(event.target.value)}
        />
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
