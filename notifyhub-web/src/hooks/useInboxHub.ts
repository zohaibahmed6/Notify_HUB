import { useEffect } from "react";
import { useQueryClient } from "@tanstack/react-query";

import { createInboxConnection } from "@/lib/signalr";

interface InboundMessageEvent {
  threadId: number;
}

interface ThreadAssignedEvent {
  threadId: number;
}

interface OutboundMessageSentEvent {
  threadId: number;
}

interface MessageStatusUpdatedEvent {
  threadId: number;
  messageId: number;
  status: string;
}

interface TaskAssignmentChangedEvent {
  taskId: number;
  assignedStaffId: number;
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
      queryClient.invalidateQueries({ queryKey: ["dashboard", "summary"] });
    });

    connection.on("threadAssigned", (event: ThreadAssignedEvent) => {
      queryClient.invalidateQueries({ queryKey: ["threads"] });
      queryClient.invalidateQueries({ queryKey: ["thread", event.threadId] });
    });

    connection.on("outboundMessageSent", (event: OutboundMessageSentEvent) => {
      queryClient.invalidateQueries({ queryKey: ["threads"] });
      queryClient.invalidateQueries({ queryKey: ["thread", event.threadId] });
    });

    // P9-02: delivery-status changes (Queued -> Sent -> Delivered/Failed) now push live
    // instead of only ever being visible after an unrelated refetch — the actual fix for
    // the "double tick" bug (WebhooksController.GatewayReceipt previously updated the DB
    // with no broadcast at all).
    connection.on("messageStatusUpdated", (event: MessageStatusUpdatedEvent) => {
      queryClient.invalidateQueries({ queryKey: ["thread", event.threadId] });
    });

    // Broadcast on every task creation/reassignment/forward (ThreadsController.CreateTask,
    // TasksController.Update/Forward, UsersController's auto-forward-on-deactivation) — a
    // single handler here refreshes both TaskNavWidget's badge and the Task Board's list
    // for whichever session the task is now assigned to, without per-component polling.
    connection.on("taskAssignmentChanged", (_event: TaskAssignmentChangedEvent) => {
      queryClient.invalidateQueries({ queryKey: ["tasks"] });
    });

    connection.start().catch((error) => {
      console.error("SignalR connection failed:", error);
    });

    return () => {
      connection.stop();
    };
  }, [queryClient]);
}
