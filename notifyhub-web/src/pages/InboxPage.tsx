import { useState } from "react";

import { useThreads } from "@/hooks/useThreads";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import { ConversationPanel } from "@/components/inbox/ConversationPanel";

export default function InboxPage() {
  const { data, isLoading } = useThreads();
  const [selectedThreadId, setSelectedThreadId] = useState<number | null>(null);

  const threads = data?.items ?? [];

  return (
    <div className="flex h-full">
      <aside className="w-80 shrink-0 overflow-y-auto border-r">
        {isLoading ? (
          <div className="p-4 text-sm text-muted-foreground">Loading...</div>
        ) : threads.length === 0 ? (
          <div className="flex h-full flex-col items-center justify-center gap-1 p-6 text-center">
            <p className="font-medium">This is your shared inbox</p>
            <p className="text-sm text-muted-foreground">Patient replies will show up here as they arrive.</p>
          </div>
        ) : (
          <ul>
            {threads.map((thread) => (
              <li key={thread.id}>
                <button
                  type="button"
                  onClick={() => setSelectedThreadId(thread.id)}
                  className={cn(
                    "flex w-full items-center justify-between gap-2 border-b px-4 py-3 text-left text-sm hover:bg-accent",
                    selectedThreadId === thread.id && "bg-accent",
                  )}
                >
                  <div className="min-w-0">
                    <div className="truncate font-medium">{thread.patientName}</div>
                    <div className="truncate text-xs text-muted-foreground">
                      {thread.assignedStaffUsername ? `Assigned to ${thread.assignedStaffUsername}` : "Unassigned"}
                      {thread.patientOptedOut && " · Opted out"}
                    </div>
                  </div>
                  {thread.unreadCount > 0 && <Badge>{thread.unreadCount}</Badge>}
                </button>
              </li>
            ))}
          </ul>
        )}
      </aside>

      <section className="min-w-0 flex-1">
        {selectedThreadId === null ? (
          <div className="flex h-full items-center justify-center text-muted-foreground">
            Select a thread to view the conversation.
          </div>
        ) : (
          <ConversationPanel threadId={selectedThreadId} />
        )}
      </section>
    </div>
  );
}
