import { useState, type FormEvent } from "react";
import { toast } from "sonner";

import { useThreads } from "@/hooks/useThreads";
import { useCreateTaskMutation } from "@/hooks/useThreads";
import { errorMessage } from "@/lib/errorMessage";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { TaskPriority } from "@/types/tasks";

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
  const [dueAt, setDueAt] = useState("");

  const createTask = useCreateTaskMutation(threadId === "" ? -1 : threadId);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    if (threadId === "") {
      toast.error("Pick a thread first");
      return;
    }

    try {
      await createTask.mutateAsync({
        priority,
        dueAt: dueAt ? new Date(dueAt).toISOString() : undefined,
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
      <div className="grid grid-cols-2 gap-3">
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
        <div className="space-y-1.5">
          <Label htmlFor="new-task-due">Due (optional)</Label>
          <Input
            id="new-task-due"
            type="datetime-local"
            value={dueAt}
            onChange={(event) => setDueAt(event.target.value)}
          />
        </div>
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
