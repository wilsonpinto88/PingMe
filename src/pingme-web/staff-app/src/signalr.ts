import { HubConnection, HubConnectionBuilder } from "@microsoft/signalr";
import type { ConnectionState } from "./types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5190";

export async function connectOrdersHub(
  token: string,
  registerHandlers: (connection: HubConnection) => void,
  onStateChange?: (state: ConnectionState) => void,
): Promise<HubConnection> {
  const connection = new HubConnectionBuilder()
    .withUrl(`${API_BASE_URL}/hubs/orders?access_token=${token}`, {
      // Auth here is the JWT in the query string, not a cookie, so the client
      // has no reason to ask for credentialed CORS. Leaving this at the
      // client's true default sends credentials: 'include' on every negotiate
      // request, which the browser then blocks unless the server responds
      // with Access-Control-Allow-Credentials: true — which our CORS policy
      // deliberately does not set, since it never needed to.
      withCredentials: false,
    })
    .withAutomaticReconnect()
    .build();

  // Handlers are registered before start() so no message can arrive unhandled
  // in the window between the socket opening and the subscription existing.
  registerHandlers(connection);

  // A staff board that has quietly dropped its connection looks identical to a
  // quiet night. These callbacks are what let the UI tell the two apart.
  connection.onreconnecting(() => onStateChange?.("reconnecting"));
  connection.onreconnected(() => onStateChange?.("connected"));
  connection.onclose(() => onStateChange?.("disconnected"));

  onStateChange?.("connecting");
  await connection.start();
  onStateChange?.("connected");
  return connection;
}
