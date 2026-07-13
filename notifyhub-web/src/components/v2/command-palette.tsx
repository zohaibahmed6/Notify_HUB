import { useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { FileText, Inbox, Plus } from "lucide-react";

import { useThreads } from "@/hooks/useThreads";
import { useTasks } from "@/hooks/useTasks";
import { useTemplates } from "@/hooks/useTemplates";
import { NewTaskForm } from "@/components/tasks/NewTaskForm";
import { QuickCreateTemplateForm } from "@/components/v2/quick-create-template-form";
import { TASK_PRIORITY_CONFIG, TASK_STATUS_CONFIG } from "@/components/v2/status-config";
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
} from "@/components/ui/command";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

type QuickAction = "task" | "template" | null;

// Client-side only: filters over data useThreads()/useTasks()/useTemplates() already
// fetched and cached via TanStack Query — no new endpoint, no extra request when the
// palette opens. Jump targets use a `?thread=`/`?task=`/`?template=` query param so the
// redesigned Inbox/Task board/Templates screens can deep-link to the specific item once
// they're built (Step 4); today the params are inert on the still-stub V2 pages.
export function CommandPalette({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const navigate = useNavigate();
  const { data: threadsData } = useThreads();
  const { data: tasksData } = useTasks();
  const { data: templates } = useTemplates();

  const [quickAction, setQuickAction] = useState<QuickAction>(null);

  const threads = threadsData?.items ?? [];
  const tasks = tasksData?.items ?? [];

  const threadNameById = useMemo(() => new Map(threads.map((t) => [t.id, t.patientName])), [threads]);

  const go = (path: string) => {
    onOpenChange(false);
    navigate(path);
  };

  const runQuickAction = (action: QuickAction) => {
    onOpenChange(false);
    setQuickAction(action);
  };

  return (
    <>
      <CommandDialog open={open} onOpenChange={onOpenChange}>
        <DialogTitle className="sr-only">Command palette</DialogTitle>
        <DialogDescription className="sr-only">
          Jump to a thread, task, or template, or run a quick action.
        </DialogDescription>
        <CommandInput placeholder="Search threads, tasks, templates..." />
        <CommandList>
          <CommandEmpty>No results found.</CommandEmpty>

          <CommandGroup heading="Quick actions">
            <CommandItem value="new task" onSelect={() => runQuickAction("task")}>
              <Plus />
              New task
            </CommandItem>
            <CommandItem value="new template" onSelect={() => runQuickAction("template")}>
              <Plus />
              New template
            </CommandItem>
          </CommandGroup>

          {threads.length > 0 && (
            <>
              <CommandSeparator />
              <CommandGroup heading="Threads">
                {threads.map((thread) => (
                  <CommandItem
                    key={thread.id}
                    value={`thread ${thread.patientName}`}
                    onSelect={() => go(`/inbox?thread=${thread.id}`)}
                  >
                    <Inbox />
                    <span className="truncate">{thread.patientName}</span>
                    {thread.unreadCount > 0 && (
                      <span className="ml-auto shrink-0 text-xs text-muted-foreground">
                        {thread.unreadCount} unread
                      </span>
                    )}
                  </CommandItem>
                ))}
              </CommandGroup>
            </>
          )}

          {tasks.length > 0 && (
            <>
              <CommandSeparator />
              <CommandGroup heading="Tasks">
                {tasks.map((task) => {
                  const priority = TASK_PRIORITY_CONFIG[task.priority];
                  const status = TASK_STATUS_CONFIG[task.status];
                  const threadName = threadNameById.get(task.threadId);
                  return (
                    <CommandItem
                      key={task.id}
                      value={`task ${task.priority} ${task.status} ${threadName ?? ""} #${task.id}`}
                      onSelect={() => go(`/tasks?task=${task.id}`)}
                    >
                      <priority.icon className="text-muted-foreground" />
                      <span className="truncate">
                        {threadName ? `${threadName} · ${priority.label}` : `Task #${task.id} · ${priority.label}`}
                      </span>
                      <span className="ml-auto shrink-0 text-xs text-muted-foreground">{status.label}</span>
                    </CommandItem>
                  );
                })}
              </CommandGroup>
            </>
          )}

          {templates && templates.length > 0 && (
            <>
              <CommandSeparator />
              <CommandGroup heading="Templates">
                {templates.map((template) => (
                  <CommandItem
                    key={template.id}
                    value={`template ${template.name}`}
                    onSelect={() => go(`/templates?template=${template.id}`)}
                  >
                    <FileText />
                    <span className="truncate">{template.name}</span>
                  </CommandItem>
                ))}
              </CommandGroup>
            </>
          )}
        </CommandList>
      </CommandDialog>

      <Dialog open={quickAction === "task"} onOpenChange={(next) => !next && setQuickAction(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>New task</DialogTitle>
            <DialogDescription>Pick a thread to create a follow-up task against.</DialogDescription>
          </DialogHeader>
          <NewTaskForm onDone={() => setQuickAction(null)} />
        </DialogContent>
      </Dialog>

      <Dialog open={quickAction === "template"} onOpenChange={(next) => !next && setQuickAction(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>New template</DialogTitle>
            <DialogDescription>Create a message template for reminders or alerts.</DialogDescription>
          </DialogHeader>
          <QuickCreateTemplateForm onDone={() => setQuickAction(null)} />
        </DialogContent>
      </Dialog>
    </>
  );
}
