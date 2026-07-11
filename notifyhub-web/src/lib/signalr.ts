import { HubConnectionBuilder, LogLevel, type HubConnection } from "@microsoft/signalr";

import { API_BASE_URL as BASE_URL } from "./apiBaseUrl";
import { tokenStore } from "./tokenStore";

/** FR-007: browsers can't set custom headers on a WebSocket handshake, so the JWT is
 * passed via an "access_token" query param instead (the Api's JwtBearerEvents.OnMessageReceived
 * hook only honors this for /hubs/* paths — see AuthServiceCollectionExtensions.cs). */
export function createInboxConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${BASE_URL}/hubs/inbox`, {
      accessTokenFactory: () => tokenStore.getAccessToken() ?? "",
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}
