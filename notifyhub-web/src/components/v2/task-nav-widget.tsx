import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { ClipboardList } from "lucide-react";

import { useAuth } from "@/context/AuthContext";
import { useTasks } from "@/hooks/useTasks";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { StatusBadge } from "@/components/v2/status-badge";
import { TASK_PRIORITY_CONFIG } from "@/components/v2/status-config";
import type { TaskStatus } from "@/types/tasks";

const OPEN_STATUSES: TaskStatus[] = ["Open", "InProgress", "Escalated"];

/// §10: top-nav task indicator — icon + count of tasks assigned to the logged-in user;
/// clicking a task navigates to /tasks?task={id}, the same deep-link the command palette
/// and task board already use to open TaskDetailSheet directly (reused, not duplicated).
export function TaskNavWidget() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);

  // Filtered server-side (`statuses`, comma-joined into the `status` query param) rather
  // than fetched-then-filtered client-side: a user with more "isActive" tasks than
  // `pageSize` (e.g. lots of historical Completed/Cancelled rows, since completing a task
  // never clears IsActive) used to have real Open tasks silently pushed off the fetched
  // page by older, already-terminal ones sorted first by DueAt — the badge would then
  // undercount or show nothing even though the assignment was correct. `totalCount` (not
  // `items.length`, which is still capped at `pageSize`) is the true count for the badge
  // number; `items` (already status-filtered) is only used for the popover's own slice(0, 8).
  const { data } = useTasks({ assignedStaffId: user?.id, isActive: true, statuses: OPEN_STATUSES });
  const myOpenTasks = data?.items ?? [];
  const myOpenTaskCount = data?.totalCount ?? 0;

  // Brief pulse when a live task-board update (see useInboxHub's taskAssignmentChanged
  // handler) raises this count while the user is already looking at the app — not on
  // the initial page load, which would otherwise blink on every mount.
  const prevCountRef = useRef<number | null>(null);
  const [justUpdated, setJustUpdated] = useState(false);

  useEffect(() => {
    if (!data) return;
    if (prevCountRef.current !== null && myOpenTaskCount > prevCountRef.current) {
      setJustUpdated(true);
      const timer = setTimeout(() => setJustUpdated(false), 2000);
      prevCountRef.current = myOpenTaskCount;
      return () => clearTimeout(timer);
    }
    prevCountRef.current = myOpenTaskCount;
  }, [data, myOpenTaskCount]);

  const handleSelect = (taskId: number) => {
    setOpen(false);
    navigate(`/tasks?task=${taskId}`);
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button variant="outline" size="icon" className="relative">
          <ClipboardList className="size-4" />
          {myOpenTaskCount > 0 && (
            <>
              {justUpdated && (
                <span className="absolute -right-1.5 -top-1.5 h-4 min-w-4 animate-ping rounded-full bg-primary opacity-75" />
              )}
              <Badge className="absolute -right-1.5 -top-1.5 h-4 min-w-4 rounded-full px-1 text-2xs">
                {myOpenTaskCount}
              </Badge>
            </>
          )}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="end" className="w-80 p-0">
        <div className="border-b p-2 text-sm font-medium">My tasks</div>
        {myOpenTasks.length === 0 ? (
          <p className="p-3 text-sm text-muted-foreground">Nothing assigned to you right now.</p>
        ) : (
          <ul className="max-h-80 overflow-y-auto">
            {myOpenTasks.slice(0, 8).map((task) => (
              <li key={task.id}>
                <button
                  type="button"
                  onClick={() => handleSelect(task.id)}
                  className="flex w-full flex-col items-start gap-1 border-b px-3 py-2 text-left text-sm last:border-b-0 hover:bg-accent"
                >
                  <span className="flex items-center gap-1.5">
                    <StatusBadge {...TASK_PRIORITY_CONFIG[task.priority]} size="xs" />
                    <span className="truncate font-medium">{task.description ?? `Task #${task.id}`}</span>
                  </span>
                  <span className="text-xs text-muted-foreground">Due {new Date(task.dueAt).toLocaleString()}</span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </PopoverContent>
    </Popover>
  );
}
