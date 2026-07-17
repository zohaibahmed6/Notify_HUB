import { useEffect, useState } from "react";
import { Inbox, Plus, Search } from "lucide-react";

import { InitialsAvatar } from "@/components/v2/initials-avatar";
import { EmptyState } from "@/components/v2/empty-state";
import { ListRowSkeleton } from "@/components/v2/skeletons";
import { NewConversationDialog } from "@/components/v2/new-conversation-dialog";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { formatUserLabel } from "@/lib/userDisplay";
import type { ThreadDto } from "@/types/inbox";

// Deliberately no last-message preview/timestamp here: ThreadDto (GET /api/threads) carries
// no last-message field, and fetching each thread's detail just to populate a sidebar
// preview would mean one extra request per visible row (up to 100) — worse than the
// current gap, not a fix for it. Left out rather than faked; see conversation with the
// user this was built in.
export function ThreadList({
  threads,
  isLoading,
  selectedThreadId,
  onSelect,
  onSearchChange,
  className,
}: {
  threads: ThreadDto[];
  isLoading: boolean;
  selectedThreadId: number | null;
  onSelect: (id: number) => void;
  onSearchChange: (search: string) => void;
  className?: string;
}) {
  const [query, setQuery] = useState("");
  const [newConversationOpen, setNewConversationOpen] = useState(false);

  // Debounced so typing doesn't fire an API request per keystroke — searching is now a real
  // server-side query (across every thread, not just the currently-loaded page), unlike the
  // old client-side filter over an already-fetched array.
  useEffect(() => {
    const timeout = setTimeout(() => onSearchChange(query.trim()), 300);
    return () => clearTimeout(timeout);
  }, [query, onSearchChange]);

  return (
    <aside className={cn("flex h-full w-full shrink-0 flex-col border-r md:w-80", className)}>
      <div className="shrink-0 space-y-2 border-b p-2">
        <div className="relative">
          <Search className="pointer-events-none absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search threads..."
            className="h-8 pl-8 text-sm"
          />
        </div>
        <Button
          type="button"
          variant="outline"
          size="sm"
          className="h-7 w-full gap-1.5 text-xs"
          onClick={() => setNewConversationOpen(true)}
        >
          <Plus className="size-3.5" />
          New conversation
        </Button>
      </div>

      <NewConversationDialog
        open={newConversationOpen}
        onOpenChange={setNewConversationOpen}
        onCreated={onSelect}
      />

      <div className="min-h-0 flex-1 overflow-y-auto">
        {isLoading ? (
          <div className="divide-y">
            {Array.from({ length: 8 }).map((_, i) => (
              <ListRowSkeleton key={i} />
            ))}
          </div>
        ) : threads.length === 0 && query.trim() === "" ? (
          <EmptyState
            icon={Inbox}
            title="This is your shared inbox"
            description="Patient replies will show up here as they arrive."
          />
        ) : threads.length === 0 ? (
          <EmptyState icon={Search} title="No matching threads" description="Try a different name." />
        ) : (
          <ul>
            {threads.map((thread) => {
              const isSelected = thread.id === selectedThreadId;
              const isUnread = thread.unreadCount > 0;
              return (
                <li key={thread.id}>
                  <button
                    type="button"
                    onClick={() => onSelect(thread.id)}
                    className={cn(
                      "flex w-full items-center gap-2.5 border-b px-3 py-2.5 text-left transition-colors hover:bg-accent",
                      isSelected && "bg-accent",
                    )}
                  >
                    <InitialsAvatar name={thread.patientName} size="sm" className="shrink-0" />
                    <div className="min-w-0 flex-1">
                      <div className={cn("truncate text-sm", isUnread ? "font-semibold" : "font-medium")}>
                        {thread.patientName}
                      </div>
                      <div className="truncate text-xs text-muted-foreground">
                        {thread.assignedStaffUsername
                          ? `Assigned to ${formatUserLabel({ fullName: thread.assignedStaffFullName, username: thread.assignedStaffUsername, role: thread.assignedStaffRole })}`
                          : "Unassigned"}
                        {thread.patientOptedOut && " · Opted out"}
                      </div>
                    </div>
                    {isUnread && (
                      <Badge className="h-5 shrink-0 rounded-full px-1.5 text-2xs">{thread.unreadCount}</Badge>
                    )}
                  </button>
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </aside>
  );
}
