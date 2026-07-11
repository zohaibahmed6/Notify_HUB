import { useEffect, useRef, useState, type FormEvent } from "react";
import { toast } from "sonner";

import { useAssignMutation, useReplyMutation, useThread } from "@/hooks/useThreads";
import { errorMessage } from "@/lib/errorMessage";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { CreateTaskForm } from "./CreateTaskForm";

export function ConversationPanel({ threadId }: { threadId: number }) {
  const { data: thread, isLoading } = useThread(threadId);
  const assign = useAssignMutation(threadId);
  const reply = useReplyMutation(threadId);

  const [draft, setDraft] = useState("");
  const [showTaskForm, setShowTaskForm] = useState(false);

  const scrollRef = useRef<HTMLDivElement>(null);
  const wasAtBottomRef = useRef(true);

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
  }, [thread?.messages.length]);

  useEffect(() => {
    wasAtBottomRef.current = true;
    setDraft("");
    setShowTaskForm(false);
  }, [threadId]);

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
            <div className="text-xs text-muted-foreground">Assigned to {thread.assignedStaffUsername}</div>
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

      {showTaskForm && (
        <div className="shrink-0 border-b p-3">
          <CreateTaskForm threadId={threadId} onDone={() => setShowTaskForm(false)} />
        </div>
      )}

      <div ref={scrollRef} onScroll={handleScroll} className="flex-1 space-y-3 overflow-y-auto p-4">
        {thread.messages.length === 0 ? (
          <div className="flex h-full flex-col items-center justify-center gap-1 text-center">
            <p className="font-medium">Start the conversation</p>
            <p className="text-sm text-muted-foreground">Send the first message to {thread.patientName}.</p>
          </div>
        ) : (
          thread.messages.map((message, index) => (
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
                  {new Date(message.timestamp).toLocaleString()}
                </p>
              </div>
            </div>
          ))
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
