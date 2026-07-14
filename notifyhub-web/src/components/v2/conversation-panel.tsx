import { useEffect, useRef, useState, type FormEvent } from "react";
import { toast } from "sonner";
import { AlarmClock, ArrowLeft, CalendarClock, ChevronUp, FileText, MessageSquareOff, UserPlus } from "lucide-react";

import { useAssignMutation, useReplyMutation, useThread } from "@/hooks/useThreads";
import { useTemplates } from "@/hooks/useTemplates";
import { apiClient } from "@/lib/apiClient";
import { errorMessage } from "@/lib/errorMessage";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { Skeleton } from "@/components/ui/skeleton";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { InitialsAvatar } from "@/components/v2/initials-avatar";
import { StatusBadge } from "@/components/v2/status-badge";
import { DELIVERY_STATUS_CONFIG, UNKNOWN_STATUS_CONFIG } from "@/components/v2/status-config";
import { CreateTaskForm } from "@/components/inbox/CreateTaskForm";
import { DateTimePicker } from "@/components/v2/date-time-picker";
import { ReminderSmsDialog } from "@/components/v2/reminder-sms-dialog";
import type { ThreadDetailDto, ThreadMessageDto } from "@/types/inbox";

const MESSAGES_PAGE_SIZE = 25;

// Same data flow as the legacy ConversationPanel (same hooks, same pagination/scroll
// behavior) — restyled, plus a delivery-status chip per outbound message (new:
// ThreadMessageDto.status was already returned by the API but never rendered).
export function ConversationPanelV2({ threadId, onBack }: { threadId: number; onBack?: () => void }) {
  const { data: thread, isLoading } = useThread(threadId);
  const { data: templates } = useTemplates(true);
  const assign = useAssignMutation(threadId);
  const reply = useReplyMutation(threadId);

  const [draft, setDraft] = useState("");
  const [showTaskForm, setShowTaskForm] = useState(false);
  const [showSchedule, setShowSchedule] = useState(false);
  const [scheduledAt, setScheduledAt] = useState("");
  const [showReminderDialog, setShowReminderDialog] = useState(false);

  const [olderMessages, setOlderMessages] = useState<ThreadMessageDto[]>([]);
  const [loadedPageCount, setLoadedPageCount] = useState(1);
  const [isLoadingOlder, setIsLoadingOlder] = useState(false);

  const scrollRef = useRef<HTMLDivElement>(null);
  const wasAtBottomRef = useRef(true);

  const pageMessages = thread?.messages.items ?? [];
  const allMessages = [...olderMessages, ...pageMessages];
  const hasMoreOlder = thread !== undefined && thread !== null && allMessages.length < thread.messages.totalCount;

  useEffect(() => {
    const el = scrollRef.current;
    if (el && wasAtBottomRef.current) {
      el.scrollTop = el.scrollHeight;
    }
  }, [pageMessages.length]);

  useEffect(() => {
    wasAtBottomRef.current = true;
    setDraft("");
    setShowTaskForm(false);
    setShowSchedule(false);
    setScheduledAt("");
    setOlderMessages([]);
    setLoadedPageCount(1);
  }, [threadId]);

  const handleLoadOlder = async () => {
    const nextPage = loadedPageCount + 1;
    setIsLoadingOlder(true);
    try {
      const data = await apiClient.get<ThreadDetailDto>(
        `/api/threads/${threadId}?page=${nextPage}&pageSize=${MESSAGES_PAGE_SIZE}`,
      );
      setOlderMessages((prev) => [...data.messages.items, ...prev]);
      setLoadedPageCount(nextPage);
    } catch (error) {
      toast.error(errorMessage(error, "Failed to load earlier messages"));
    } finally {
      setIsLoadingOlder(false);
    }
  };

  const handleScroll = () => {
    const el = scrollRef.current;
    if (!el) return;
    wasAtBottomRef.current = el.scrollHeight - el.scrollTop - el.clientHeight < 40;
  };

  const handleAssign = async () => {
    try {
      await assign.mutateAsync();
      toast.success("Thread assigned");
    } catch (error) {
      toast.error(errorMessage(error, "Assignment failed"));
    }
  };

  const handleReply = async (event: FormEvent) => {
    event.preventDefault();
    if (!draft.trim()) return;

    try {
      await reply.mutateAsync({
        body: draft,
        scheduledAt: showSchedule && scheduledAt ? new Date(scheduledAt).toISOString() : undefined,
      });
      setDraft("");
      setShowSchedule(false);
      setScheduledAt("");
      toast.success(showSchedule && scheduledAt ? "Message scheduled" : "Message sent");
    } catch (error) {
      toast.error(errorMessage(error, "Message failed to send"));
    }
  };

  // P9-04: resolves {{patient_name}}/{{appointment_time}} to real values (this thread's
  // patient, a real upcoming appointment or a generated future dummy time) before filling
  // the composer — the result lands in the editable textarea, same as any ad-hoc reply,
  // not a locked preview. Falls back to the raw template body if the preview call fails,
  // rather than leaving the composer empty.
  const handleInsertTemplate = async (templateId: string) => {
    const template = templates?.find((t) => String(t.id) === templateId);
    if (!template) return;
    try {
      const preview = await apiClient.get<{ renderedBody: string }>(
        `/api/threads/${threadId}/templates/${templateId}/preview`,
      );
      setDraft(preview.renderedBody);
    } catch (error) {
      toast.error(errorMessage(error, "Couldn't resolve template fields, inserting raw text"));
      setDraft(template.body);
    }
  };

  if (isLoading || !thread) {
    return (
      <div className="flex h-full flex-col">
        <div className="flex shrink-0 items-center gap-3 border-b px-4 py-3">
          <Skeleton className="size-9 rounded-full" />
          <Skeleton className="h-4 w-32" />
        </div>
        <div className="flex-1 space-y-3 p-4">
          <Skeleton className="h-12 w-2/5 rounded-lg" />
          <Skeleton className="ml-auto h-12 w-1/2 rounded-lg" />
          <Skeleton className="h-8 w-1/3 rounded-lg" />
        </div>
      </div>
    );
  }

  return (
    <div className="flex h-full flex-col">
      <div className="flex shrink-0 items-center justify-between gap-3 border-b px-4 py-3">
        <div className="flex min-w-0 items-center gap-3">
          {onBack && (
            <Button variant="ghost" size="icon" className="-ml-2 shrink-0 md:hidden" onClick={onBack}>
              <ArrowLeft className="size-4" />
              <span className="sr-only">Back to threads</span>
            </Button>
          )}
          <InitialsAvatar name={thread.patientName} />
          <div className="min-w-0">
            <div className="truncate font-medium">{thread.patientName}</div>
            {thread.assignedStaffUsername && (
              <div className="truncate text-xs text-muted-foreground">
                Assigned to {thread.assignedStaffUsername}
              </div>
            )}
          </div>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          {!thread.assignedStaffId && (
            <Button variant="outline" size="sm" onClick={handleAssign} disabled={assign.isPending} className="gap-1.5">
              <UserPlus className="size-3.5" />
              {assign.isPending ? "Assigning..." : "Assign to me"}
            </Button>
          )}
          <Button variant="outline" size="sm" onClick={() => setShowTaskForm((v) => !v)}>
            Make task
          </Button>
        </div>
      </div>

      {thread.patientOptedOut && (
        <div className="flex shrink-0 items-center gap-2 border-b bg-destructive/10 px-4 py-2 text-sm text-destructive">
          <MessageSquareOff className="size-4 shrink-0" />
          Patient opted out — no further messages can be sent to this thread.
        </div>
      )}

      {showTaskForm && (
        <div className="shrink-0 border-b p-3">
          <CreateTaskForm threadId={threadId} onDone={() => setShowTaskForm(false)} />
        </div>
      )}

      <div ref={scrollRef} onScroll={handleScroll} className="flex-1 space-y-3 overflow-y-auto p-4">
        {allMessages.length === 0 ? (
          <div className="flex h-full flex-col items-center justify-center gap-1 text-center">
            <p className="font-medium">Start the conversation</p>
            <p className="text-sm text-muted-foreground">Send the first message to {thread.patientName}.</p>
          </div>
        ) : (
          <>
            {hasMoreOlder && (
              <div className="flex justify-center pb-1">
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={handleLoadOlder}
                  disabled={isLoadingOlder}
                  className="h-7 gap-1 rounded-full text-xs text-muted-foreground"
                >
                  <ChevronUp className="size-3.5" />
                  {isLoadingOlder ? "Loading..." : "Load earlier messages"}
                </Button>
              </div>
            )}
            {allMessages.map((message, index) => {
              const statusConfig =
                message.direction === "outbound" && message.status
                  ? (DELIVERY_STATUS_CONFIG[message.status] ?? UNKNOWN_STATUS_CONFIG)
                  : null;
              return (
                <div
                  key={index}
                  className={cn("flex", message.direction === "inbound" ? "justify-start" : "justify-end")}
                >
                  <div
                    className={cn(
                      "max-w-[75%] rounded-lg px-3 py-2 text-sm",
                      message.direction === "inbound"
                        ? "bg-muted text-foreground"
                        : "bg-primary text-primary-foreground",
                    )}
                  >
                    <p className="whitespace-pre-wrap break-words">{message.body}</p>
                    <div
                      className={cn(
                        "mt-1 flex items-center gap-1.5 text-[10px] opacity-80",
                        message.direction === "outbound" && "justify-end",
                      )}
                    >
                      <span>
                        {message.direction === "outbound" && message.senderType === "Staff"
                          ? "You"
                          : (message.senderType ?? "Patient")}
                        {" · "}
                        {new Date(message.timestamp).toLocaleString()}
                      </span>
                    </div>
                    {statusConfig && (
                      <div className="mt-1 flex justify-end">
                        <StatusBadge {...statusConfig} size="xs" />
                      </div>
                    )}
                  </div>
                </div>
              );
            })}
          </>
        )}
      </div>

      <form onSubmit={handleReply} className="shrink-0 border-t p-3">
        {!thread.patientOptedOut && (
          <div className="mb-2 flex flex-wrap items-center gap-2">
            <Select onValueChange={handleInsertTemplate}>
              <SelectTrigger className="h-7 w-44 gap-1.5 text-xs">
                <FileText className="size-3.5" />
                <SelectValue placeholder="Insert template" />
              </SelectTrigger>
              <SelectContent>
                {(templates ?? []).map((t) => (
                  <SelectItem key={t.id} value={String(t.id)}>
                    {t.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button
              type="button"
              variant={showSchedule ? "default" : "outline"}
              size="sm"
              className="h-7 gap-1.5 text-xs"
              onClick={() => setShowSchedule((v) => !v)}
            >
              <CalendarClock className="size-3.5" />
              Schedule
            </Button>
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="h-7 gap-1.5 text-xs"
              onClick={() => setShowReminderDialog(true)}
            >
              <AlarmClock className="size-3.5" />
              Reminder SMS
            </Button>
            {showSchedule && (
              <DateTimePicker
                value={scheduledAt}
                onChange={setScheduledAt}
                placeholder="Pick a time"
                className="h-7 w-auto gap-1.5 px-2 text-xs"
              />
            )}
          </div>
        )}
        <div className="flex gap-2">
          <Textarea
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            placeholder={thread.patientOptedOut ? "Patient has opted out" : "Type a reply..."}
            disabled={thread.patientOptedOut || reply.isPending}
            className="min-h-10"
            onKeyDown={(event) => {
              if (event.key === "Enter" && !event.shiftKey) {
                event.preventDefault();
                handleReply(event);
              }
            }}
          />
          <Button type="submit" disabled={thread.patientOptedOut || reply.isPending || !draft.trim()}>
            {showSchedule && scheduledAt ? "Schedule" : "Send"}
          </Button>
        </div>
      </form>

      <ReminderSmsDialog threadId={threadId} open={showReminderDialog} onOpenChange={setShowReminderDialog} />
    </div>
  );
}
