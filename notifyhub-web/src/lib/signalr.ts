import { HubConnectionBuilder, LogLevel, type HubConnection } from "@microsoft/signalr";

import { tokenStore } from "./tokenStore";

const BASE_URL = import.meta.env.VITE_API_URL;

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
