import { ChevronDown, ChevronUp, ChevronsUpDown, ClipboardList, Repeat } from "lucide-react";

import { InitialsAvatar } from "@/components/v2/initials-avatar";
import { StatusBadge } from "@/components/v2/status-badge";
import { TASK_PRIORITY_CONFIG, TASK_STATUS_CONFIG } from "@/components/v2/status-config";
import { isTaskOverdue, TaskCard } from "@/components/v2/task-card";
import { EmptyState } from "@/components/v2/empty-state";
import { TableRowSkeleton } from "@/components/v2/skeletons";
import { Button } from "@/components/ui/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { cn } from "@/lib/utils";
import { formatUserLabel, formatUserName } from "@/lib/userDisplay";
import type { TaskDto, TaskSortBy, TaskSortDir } from "@/types/tasks";

// Modeled on SmsHistoryPage's grid (Table primitives, sticky header, mobile stacked-card
// fallback, server-driven Page X of Y pagination) — plus click-to-sort headers, which SMS
// History doesn't have. Real server-side pagination/sorting/filtering (TaskBoardPageV2's
// second, Grid-only useTasks call) rather than the old "List" view's unpaginated 100-task
// card dump, which is what made the Task board hard to scan in the first place.
function SortableHead({
  label,
  column,
  sortBy,
  sortDir,
  onSortChange,
}: {
  label: string;
  column: TaskSortBy;
  sortBy: TaskSortBy;
  sortDir: TaskSortDir;
  onSortChange: (column: TaskSortBy) => void;
}) {
  const active = sortBy === column;
  const Icon = active ? (sortDir === "asc" ? ChevronUp : ChevronDown) : ChevronsUpDown;

  return (
    <TableHead className="sticky top-0 z-10 bg-background">
      <button
        type="button"
        onClick={() => onSortChange(column)}
        className={cn(
          "flex items-center gap-1 transition-colors hover:text-foreground",
          active && "text-foreground",
        )}
      >
        {label}
        <Icon className={cn("size-3.5", !active && "text-muted-foreground/50")} />
      </button>
    </TableHead>
  );
}

export function TaskGrid({
  tasks,
  isLoading,
  page,
  totalPages,
  totalCount,
  onPageChange,
  sortBy,
  sortDir,
  onSortChange,
  onOpen,
  onAssignToMe,
  onComplete,
  isMutating,
  currentUserId,
}: {
  tasks: TaskDto[];
  isLoading: boolean;
  page: number;
  totalPages: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  sortBy: TaskSortBy;
  sortDir: TaskSortDir;
  onSortChange: (column: TaskSortBy) => void;
  onOpen: (id: number) => void;
  onAssignToMe: (id: number) => void;
  onComplete: (id: number) => void;
  isMutating: boolean;
  currentUserId?: number;
}) {
  if (isLoading) {
    return (
      <div className="divide-y rounded-lg border">
        {Array.from({ length: 8 }).map((_, i) => (
          <TableRowSkeleton key={i} columns={8} />
        ))}
      </div>
    );
  }

  if (tasks.length === 0) {
    return <EmptyState icon={ClipboardList} title="No tasks match these filters" description="Try widening your filters." />;
  }

  return (
    <>
      {/* Mobile: reuse TaskCard directly (already carries the inline quick actions) instead
          of hand-rolling a second card layout, same P9-00 stacked-list convention as every
          other grid in the app. */}
      <div className="min-h-0 flex-1 space-y-2 overflow-y-auto md:hidden">
        {tasks.map((task) => (
          <TaskCard
            key={task.id}
            task={task}
            onOpen={() => onOpen(task.id)}
            onAssignToMe={() => onAssignToMe(task.id)}
            onComplete={() => onComplete(task.id)}
            isMutating={isMutating}
            isAssignedToCurrentUser={task.assignedStaffId != null && task.assignedStaffId === currentUserId}
          />
        ))}
      </div>

      <div className="hidden min-h-0 flex-1 overflow-auto rounded-lg border md:block">
        <Table>
          <TableHeader>
            <TableRow className="hover:bg-transparent">
              <SortableHead label="Patient" column="patientName" sortBy={sortBy} sortDir={sortDir} onSortChange={onSortChange} />
              <TableHead className="sticky top-0 z-10 bg-background">Description</TableHead>
              <SortableHead label="Priority" column="priority" sortBy={sortBy} sortDir={sortDir} onSortChange={onSortChange} />
              <SortableHead label="Status" column="status" sortBy={sortBy} sortDir={sortDir} onSortChange={onSortChange} />
              <SortableHead label="Due" column="dueAt" sortBy={sortBy} sortDir={sortDir} onSortChange={onSortChange} />
              <SortableHead label="Assignee" column="assignedStaffUsername" sortBy={sortBy} sortDir={sortDir} onSortChange={onSortChange} />
              <TableHead className="sticky top-0 z-10 bg-background">Type</TableHead>
              <TableHead className="sticky top-0 z-10 bg-background">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {tasks.map((task) => {
              const overdue = isTaskOverdue(task);
              const isActionable = task.status !== "Completed" && task.status !== "Cancelled";
              const isAssignedToCurrentUser = task.assignedStaffId != null && task.assignedStaffId === currentUserId;
              const priority = TASK_PRIORITY_CONFIG[task.priority];
              const status = TASK_STATUS_CONFIG[task.status];

              return (
                <TableRow key={task.id} className="cursor-pointer" onClick={() => onOpen(task.id)}>
                  <TableCell className="text-sm font-medium">
                    <div className="flex items-center gap-1.5">
                      {task.patientName}
                      {task.isRecurring && (
                        <Repeat className="size-3 text-muted-foreground" aria-label="Recurring" />
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="max-w-xs truncate text-xs text-muted-foreground">{task.description ?? "—"}</TableCell>
                  <TableCell>
                    <StatusBadge {...priority} size="xs" />
                  </TableCell>
                  <TableCell>
                    <StatusBadge {...status} size="xs" />
                  </TableCell>
                  <TableCell className={cn("text-xs", overdue ? "font-medium text-destructive" : "text-muted-foreground")}>
                    {overdue ? "Overdue · " : ""}
                    {new Date(task.dueAt).toLocaleDateString(undefined, { month: "short", day: "numeric", hour: "numeric", minute: "2-digit" })}
                  </TableCell>
                  <TableCell>
                    {task.assignedStaffUsername ? (
                      <div className="flex min-w-0 items-center gap-1.5">
                        <InitialsAvatar
                          name={formatUserName({ fullName: task.assignedStaffFullName, username: task.assignedStaffUsername })}
                          size="sm"
                        />
                        <span className="truncate text-xs text-muted-foreground">
                          {formatUserLabel({
                            fullName: task.assignedStaffFullName,
                            username: task.assignedStaffUsername,
                            role: task.assignedStaffRole,
                          })}
                        </span>
                      </div>
                    ) : (
                      <span className="text-xs text-muted-foreground">Unassigned</span>
                    )}
                  </TableCell>
                  <TableCell className="text-xs text-muted-foreground">{task.taskType}</TableCell>
                  <TableCell>
                    {isActionable && (
                      <div className="flex shrink-0 gap-1" onClick={(e) => e.stopPropagation()}>
                        {!isAssignedToCurrentUser && (
                          <Button
                            variant="ghost"
                            size="sm"
                            className="h-6 px-1.5 text-2xs"
                            onClick={() => onAssignToMe(task.id)}
                            disabled={isMutating}
                          >
                            Assign to me
                          </Button>
                        )}
                        <Button size="sm" className="h-6 px-1.5 text-2xs" onClick={() => onComplete(task.id)} disabled={isMutating}>
                          Complete
                        </Button>
                      </div>
                    )}
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </div>

      <div className="mt-3 flex shrink-0 items-center justify-between text-sm text-muted-foreground">
        <span className="font-mono text-xs">
          Page {page} of {totalPages} ({totalCount} total)
        </span>
        <div className="flex gap-2">
          <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => onPageChange(Math.max(1, page - 1))}>
            Previous
          </Button>
          <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>
            Next
          </Button>
        </div>
      </div>
    </>
  );
}
