import { useState, type FormEvent } from "react";
import { toast } from "sonner";

import { useCreateTaskMutation } from "@/hooks/useThreads";
import { errorMessage } from "@/lib/errorMessage";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { TASK_TYPES, type TaskPriority, type TaskType } from "@/types/tasks";

const PRIORITIES: TaskPriority[] = ["Low", "Medium", "High", "Urgent"];

export function CreateTaskForm({ threadId, onDone }: { threadId: number; onDone: () => void }) {
  const [priority, setPriority] = useState<TaskPriority>("Medium");
  // Date required, time optional (defaults 00:00) — P9-01d.
  const [dueDate, setDueDate] = useState("");
  const [dueTime, setDueTime] = useState("");
  const [taskType, setTaskType] = useState<TaskType>("General");
  const [description, setDescription] = useState("");
  const createTask = useCreateTaskMutation(threadId);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    if (!dueDate) {
      toast.error("Pick a due date");
      return;
    }

    try {
      await createTask.mutateAsync({
        priority,
        dueAt: new Date(`${dueDate}T${dueTime || "00:00"}`).toISOString(),
        taskType,
        description: description || undefined,
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
        <div className="space-y-1.5">
          <Label htmlFor="task-due-date">Due date</Label>
          <Input
            id="task-due-date"
            type="date"
            required
            value={dueDate}
            onChange={(event) => setDueDate(event.target.value)}
          />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="task-due-time">Due time (optional)</Label>
          <Input
            id="task-due-time"
            type="time"
            value={dueTime}
            onChange={(event) => setDueTime(event.target.value)}
          />
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
