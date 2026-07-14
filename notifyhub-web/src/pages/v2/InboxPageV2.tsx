import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Inbox } from "lucide-react";

import { useThreads } from "@/hooks/useThreads";
import { ThreadList } from "@/components/v2/thread-list";
import { ConversationPanelV2 } from "@/components/v2/conversation-panel";
import { EmptyState } from "@/components/v2/empty-state";
import { cn } from "@/lib/utils";

export default function InboxPageV2() {
  const { data, isLoading } = useThreads();
  const threads = data?.items ?? [];

  // ?thread= lets the command palette (and any future deep link) jump straight to a
  // conversation; kept in sync both ways so selecting a thread updates the URL too.
  const [searchParams, setSearchParams] = useSearchParams();
  const [selectedThreadId, setSelectedThreadId] = useState<number | null>(() => {
    const fromUrl = searchParams.get("thread");
    return fromUrl ? Number(fromUrl) : null;
  });

  // Deliberately keyed on searchParams only (not selectedThreadId) — this effect reacts
  // to external URL changes (e.g. palette navigation); selecting a thread in-page updates
  // the URL via handleSelect below, not the other way around.
  useEffect(() => {
    const fromUrl = searchParams.get("thread");
    if (fromUrl && Number(fromUrl) !== selectedThreadId) {
      setSelectedThreadId(Number(fromUrl));
    }
  }, [searchParams]);

  const handleSelect = (id: number) => {
    setSelectedThreadId(id);
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set("thread", String(id));
      return next;
    });
  };

  // Mobile: single-pane with back-navigation instead of squeezing both panes side by
  // side (P9-00) — the list is hidden once a thread is selected, and the conversation
  // panel's back button clears the selection to return to it. md+ keeps both panes.
  const handleBack = () => {
    setSelectedThreadId(null);
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.delete("thread");
      return next;
    });
  };

  return (
    <div className="flex h-full">
      <ThreadList
        threads={threads}
        isLoading={isLoading}
        selectedThreadId={selectedThreadId}
        onSelect={handleSelect}
        className={cn(selectedThreadId !== null && "hidden md:flex")}
      />
      <section className={cn("min-w-0 flex-1", selectedThreadId === null && "hidden md:block")}>
        {selectedThreadId === null ? (
          <EmptyState icon={Inbox} title="Select a thread" description="Pick a conversation from the list to view it." />
        ) : (
          <ConversationPanelV2 threadId={selectedThreadId} onBack={handleBack} />
        )}
      </section>
    </div>
  );
}
