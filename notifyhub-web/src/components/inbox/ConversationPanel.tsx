import { useEffect, useRef, useState, type FormEvent } from "react";
import { toast } from "sonner";

import { useAssignMutation, useReplyMutation, useThread } from "@/hooks/useThreads";
import { apiClient } from "@/lib/apiClient";
import { errorMessage } from "@/lib/errorMessage";
import { formatUtc } from "@/lib/dateUtc";
import { formatUserLabel } from "@/lib/userDisplay";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import type { ThreadDetailDto, ThreadMessageDto } from "@/types/inbox";
import { CreateTaskForm } from "./CreateTaskForm";

const MESSAGES_PAGE_SIZE = 25;

export function ConversationPanel({ threadId }: { threadId: number }) {
  const { data: thread, isLoading } = useThread(threadId);
  const assign = useAssignMutation(threadId);
  const reply = useReplyMutation(threadId);

  const [draft, setDraft] = useState("");
  const [showTaskForm, setShowTaskForm] = useState(false);

  // FR-010: the API only returns one page of messages at a time (most recent first).
  // Older pages are fetched on demand and prepended here rather than through TanStack
  // Query's cache, since this is an ever-growing local scrollback, not a replaceable page.
  const [olderMessages, setOlderMessages] = useState<ThreadMessageDto[]>([]);
  const [loadedPageCount, setLoadedPageCount] = useState(1);
  const [isLoadingOlder, setIsLoadingOlder] = useState(false);

  const scrollRef = useRef<HTMLDivElement>(null);
  const wasAtBottomRef = useRef(true);

  const pageMessages = thread?.messages.items ?? [];
  const allMessages = [...olderMessages, ...pageMessages];
  const hasMoreOlder = thread !== undefined && thread !== null && allMessages.length < thread.messages.totalCount;

  // §6c: a real-time message arriving while a draft is open must append below without
  // touching the draft, and only auto-scroll if the view was already scrolled to the
  // bottom — so tracking "were we at the bottom before this update" separately from the
  // draft text (which is untouched by design, since it's local state the query refetch
  // never overwrites).
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
      await reply.mutateAsync(draft);
      setDraft("");
      toast.success("Message sent");
    } catch (error) {
      toast.error(errorMessage(error, "Message failed to send"));
    }
  };

  if (isLoading || !thread) {
    return <div className="flex h-full items-center justify-center text-muted-foreground">Loading...</div>;
  }

  return (
    <div className="flex h-full flex-col">
      <div className="flex shrink-0 items-center justify-between border-b px-4 py-3">
        <div>
          <div className="font-medium">{thread.patientName}</div>
          {thread.assignedStaffUsername && (
            <div className="text-xs text-muted-foreground">
              Assigned to{" "}
              {formatUserLabel({
                fullName: thread.assignedStaffFullName,
                username: thread.assignedStaffUsername,
                role: thread.assignedStaffRole,
              })}
            </div>
          )}
        </div>
        <div className="flex items-center gap-2">
          {!thread.assignedStaffId && (
            <Button variant="outline" size="sm" onClick={handleAssign} disabled={assign.isPending}>
              {assign.isPending ? "Assigning..." : "Assign to me"}
            </Button>
          )}
          <Button variant="outline" size="sm" onClick={() => setShowTaskForm((v) => !v)}>
            Make task
          </Button>
        </div>
      </div>

      {thread.patientOptedOut && (
        <div className="shrink-0 border-b bg-destructive/10 px-4 py-2 text-sm text-destructive">
          Patient opted out — no further messages can be sent to this thread.
        </div>
      )}

      <CreateTaskForm
        threadId={threadId}
        threadAssignedStaffId={thread.assignedStaffId}
        open={showTaskForm}
        onOpenChange={setShowTaskForm}
      />

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
                <Button variant="outline" size="sm" onClick={handleLoadOlder} disabled={isLoadingOlder}>
                  {isLoadingOlder ? "Loading..." : "Load earlier messages"}
                </Button>
              </div>
            )}
            {allMessages.map((message, index) => (
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
                  <p>{message.body}</p>
                  <p
                    className={cn(
                      "mt-1 text-[10px] opacity-70",
                      message.direction === "outbound" && "text-right",
                    )}
                  >
                    {message.direction === "outbound" && message.senderType === "Staff" ? "You" : message.senderType ?? "Patient"}
                    {" · "}
                    {formatUtc(message.timestamp)}
                  </p>
                </div>
              </div>
            ))}
          </>
        )}
      </div>

      <form onSubmit={handleReply} className="shrink-0 border-t p-3">
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
            Send
          </Button>
        </div>
      </form>
    </div>
  );
}
