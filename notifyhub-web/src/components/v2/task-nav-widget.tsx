import { useMemo, useState } from "react";
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

  const { data } = useTasks({ assignedStaffId: user?.id, isActive: true });
  const myOpenTasks = useMemo(
    () => (data?.items ?? []).filter((t) => OPEN_STATUSES.includes(t.status)),
    [data],
  );

  const handleSelect = (taskId: number) => {
    setOpen(false);
    navigate(`/tasks?task=${taskId}`);
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button variant="outline" size="icon" className="relative">
          <ClipboardList className="size-4" />
          {myOpenTasks.length > 0 && (
            <Badge className="absolute -right-1.5 -top-1.5 h-4 min-w-4 rounded-full px-1 text-2xs">
              {myOpenTasks.length}
            </Badge>
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
