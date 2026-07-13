import { useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { toast } from "sonner";
import { ChevronDown, ClipboardList, Plus } from "lucide-react";

import { useAuth } from "@/context/AuthContext";
import { useTasks, useUpdateTaskMutation } from "@/hooks/useTasks";
import { useThreads } from "@/hooks/useThreads";
import { useAssignableUsers } from "@/hooks/useUsers";
import { errorMessage } from "@/lib/errorMessage";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { NewTaskForm } from "@/components/tasks/NewTaskForm";
import { TaskCard } from "@/components/v2/task-card";
import { TaskDetailSheet } from "@/components/v2/task-detail-sheet";
import { EmptyState } from "@/components/v2/empty-state";
import { CardGridSkeleton } from "@/components/v2/skeletons";
import { DistributionBar } from "@/components/v2/sparkline";
import { STATUS_TONE_BAR_CLASS, TASK_STATUS_CONFIG } from "@/components/v2/status-config";
import type { TaskDto, TaskPriority, TaskStatus } from "@/types/tasks";

const COLUMNS: { status: TaskStatus; label: string; defaultCollapsed: boolean }[] = [
  { status: "Open", label: "Open", defaultCollapsed: false },
  { status: "InProgress", label: "In progress", defaultCollapsed: false },
  { status: "Escalated", label: "Escalated", defaultCollapsed: false },
  { status: "Completed", label: "Completed", defaultCollapsed: true },
  { status: "Cancelled", label: "Cancelled", defaultCollapsed: true },
];

const PRIORITIES: TaskPriority[] = ["Low", "Medium", "High", "Urgent"];

export default function TaskBoardPageV2() {
  const { user } = useAuth();
  const { data, isLoading } = useTasks();
  const { data: threadsData } = useThreads();
  const { data: assignableUsers } = useAssignableUsers();
  const updateTask = useUpdateTaskMutation();

  const tasks = data?.items ?? [];
  const threads = threadsData?.items ?? [];
  const threadNameById = useMemo(() => new Map(threads.map((t) => [t.id, t.patientName])), [threads]);

  const [view, setView] = useState<"board" | "list">("board");
  const [priorityFilter, setPriorityFilter] = useState<TaskPriority | "all">("all");
  const [assigneeFilter, setAssigneeFilter] = useState<string>("all");
  const [recurringOnly, setRecurringOnly] = useState(false);
  const [showNewTaskDialog, setShowNewTaskDialog] = useState(false);
  const [collapsed, setCollapsed] = useState<Set<TaskStatus>>(
    () => new Set(COLUMNS.filter((c) => c.defaultCollapsed).map((c) => c.status)),
  );

  const [searchParams, setSearchParams] = useSearchParams();
  const [selectedTaskId, setSelectedTaskId] = useState<number | null>(() => {
    const fromUrl = searchParams.get("task");
    return fromUrl ? Number(fromUrl) : null;
  });

  // Reacts to external URL changes only (e.g. command palette navigation) — opening a
  // task in-page updates the URL via openTask below, not the other way around.
  useEffect(() => {
    const fromUrl = searchParams.get("task");
    const next = fromUrl ? Number(fromUrl) : null;
    if (next !== selectedTaskId) setSelectedTaskId(next);
  }, [searchParams]);

  const openTask = (id: number) => {
    setSelectedTaskId(id);
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set("task", String(id));
      return next;
    });
  };

  const closeTask = () => {
    setSelectedTaskId(null);
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.delete("task");
      return next;
    });
  };

  // Sourced from the real user roster (Active users only) rather than derived from
  // whichever tasks happen to already be assigned — previously a user with zero tasks
  // assigned to them could never appear as a filter option at all.
  const assigneeOptions = assignableUsers ?? [];

  const filteredTasks = useMemo(
    () =>
      tasks.filter((t) => {
        if (priorityFilter !== "all" && t.priority !== priorityFilter) return false;
        if (recurringOnly && !t.isRecurring) return false;
        if (assigneeFilter === "unassigned" && t.assignedStaffId !== null) return false;
        if (assigneeFilter !== "all" && assigneeFilter !== "unassigned" && String(t.assignedStaffId) !== assigneeFilter)
          return false;
        return true;
      }),
    [tasks, priorityFilter, assigneeFilter, recurringOnly],
  );

  const distributionSegments = COLUMNS.map((c) => {
    const config = TASK_STATUS_CONFIG[c.status];
    return {
      label: config.label,
      value: filteredTasks.filter((t) => t.status === c.status).length,
      className: STATUS_TONE_BAR_CLASS[config.tone],
    };
  });

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

  const toggleCollapsed = (status: TaskStatus) => {
    setCollapsed((prev) => {
      const next = new Set(prev);
      if (next.has(status)) next.delete(status);
      else next.add(status);
      return next;
    });
  };

  const renderCard = (task: TaskDto) => (
    <TaskCard
      key={task.id}
      task={task}
      threadName={threadNameById.get(task.threadId)}
      onOpen={() => openTask(task.id)}
      onAssignToMe={() => handleAssignToMe(task.id)}
      onComplete={() => handleComplete(task.id)}
      isMutating={updateTask.isPending}
    />
  );

  return (
    <div className="flex h-full flex-col overflow-y-auto p-4">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <Tabs value={view} onValueChange={(v) => setView(v as "board" | "list")}>
            <TabsList>
              <TabsTrigger value="board">Board</TabsTrigger>
              <TabsTrigger value="list">List</TabsTrigger>
            </TabsList>
          </Tabs>

          <Select value={priorityFilter} onValueChange={(v) => setPriorityFilter(v as TaskPriority | "all")}>
            <SelectTrigger className="h-9 w-[130px] text-sm">
              <SelectValue placeholder="Priority" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All priorities</SelectItem>
              {PRIORITIES.map((p) => (
                <SelectItem key={p} value={p}>
                  {p}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select value={assigneeFilter} onValueChange={setAssigneeFilter}>
            <SelectTrigger className="h-9 w-[150px] text-sm">
              <SelectValue placeholder="Assignee" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All assignees</SelectItem>
              <SelectItem value="unassigned">Unassigned</SelectItem>
              {assigneeOptions.map((u) => (
                <SelectItem key={u.id} value={String(u.id)}>
                  {u.fullName ?? u.username}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Button
            variant={recurringOnly ? "default" : "outline"}
            size="sm"
            className="h-9"
            onClick={() => setRecurringOnly((v) => !v)}
          >
            Recurring only
          </Button>
        </div>

        <Button size="sm" className="gap-1.5" onClick={() => setShowNewTaskDialog(true)}>
          <Plus className="size-4" />
          New task
        </Button>
      </div>

      {!isLoading && tasks.length > 0 && (
        <div className="mb-4">
          <DistributionBar segments={distributionSegments} />
        </div>
      )}

      {isLoading ? (
        <CardGridSkeleton count={6} />
      ) : tasks.length === 0 ? (
        <EmptyState
          icon={ClipboardList}
          title="Your task board is clear"
          description="Follow-ups created from the inbox will show up here."
        />
      ) : filteredTasks.length === 0 ? (
        <EmptyState icon={ClipboardList} title="No tasks match these filters" description="Try widening your filters." />
      ) : view === "list" ? (
        <div className="space-y-2">
          {[...filteredTasks]
            .sort((a, b) => new Date(a.dueAt).getTime() - new Date(b.dueAt).getTime())
            .map(renderCard)}
        </div>
      ) : (
        <div className="flex flex-1 gap-4 overflow-x-auto pb-2">
          {COLUMNS.map((column) => {
            const columnTasks = filteredTasks.filter((t) => t.status === column.status);
            const isCollapsed = collapsed.has(column.status);
            return (
              <div key={column.status} className="flex w-72 shrink-0 flex-col">
                <button
                  type="button"
                  onClick={() => toggleCollapsed(column.status)}
                  className="mb-2 flex items-center justify-between rounded-md px-1 py-1 text-sm font-medium hover:bg-accent"
                >
                  <span>
                    {column.label} <span className="text-muted-foreground">({columnTasks.length})</span>
                  </span>
                  <ChevronDown className={cn("size-4 text-muted-foreground transition-transform", isCollapsed && "-rotate-90")} />
                </button>
                {!isCollapsed && (
                  <div className="space-y-2">
                    {columnTasks.length === 0 ? (
                      <p className="px-1 text-xs text-muted-foreground">No tasks</p>
                    ) : (
                      columnTasks.map(renderCard)
                    )}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}

      <TaskDetailSheet
        taskId={selectedTaskId}
        threadName={selectedTaskId ? threadNameById.get(tasks.find((t) => t.id === selectedTaskId)?.threadId ?? -1) : undefined}
        onOpenChange={(open) => !open && closeTask()}
        onAssignToMe={handleAssignToMe}
        onComplete={handleComplete}
        isMutating={updateTask.isPending}
      />

      <Dialog open={showNewTaskDialog} onOpenChange={setShowNewTaskDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>New task</DialogTitle>
            <DialogDescription>Pick a thread to create a follow-up task against.</DialogDescription>
          </DialogHeader>
          <NewTaskForm onDone={() => setShowNewTaskDialog(false)} />
        </DialogContent>
      </Dialog>
    </div>
  );
}
