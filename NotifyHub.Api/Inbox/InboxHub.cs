using Microsoft.AspNetCore.SignalR;

namespace NotifyHub.Api.Inbox;

/// FR-007: shared inbox real-time channel. All connected (authenticated) staff/admin
/// sessions are in a single implicit group — "shared inbox" means everyone sees
/// everything, there's no per-staff filtering. Clients only receive server->client
/// pushes (new inbound message, unread count changes); no client->server hub methods
/// are needed for this build.
public class InboxHub : Hub;
