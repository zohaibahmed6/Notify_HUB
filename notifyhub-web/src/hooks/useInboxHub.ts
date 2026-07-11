import { useEffect } from "react";
import { useQueryClient } from "@tanstack/react-query";

import { createInboxConnection } from "@/lib/signalr";

interface InboundMessageEvent {
  threadId: number;
}

interface ThreadAssignedEvent {
  threadId: number;
}

/** FR-007: shared inbox real-time — invalidates the affected queries on server push so
 * every connected session (e.g. two open browser tabs) converges without polling. */
export function useInboxHub() {
  const queryClient = useQueryClient();

  useEffect(() => {
    const connection = createInboxConnection();

    connection.on("inboundMessageReceived", (event: InboundMessageEvent) => {
      queryClient.invalidateQueries({ queryKey: ["threads"] });
      queryClient.invalidateQueries({ queryKey: ["thread", event.threadId] });
    });

    connection.on("threadAssigned", (event: ThreadAssignedEvent) => {
      queryClient.invalidateQueries({ queryKey: ["threads"] });
      queryClient.invalidateQueries({ queryKey: ["thread", event.threadId] });
    });

    connection.start().catch((error) => {
      console.error("SignalR connection failed:", error);
    });

    return () => {
      connection.stop();
    };
  }, [queryClient]);
}
