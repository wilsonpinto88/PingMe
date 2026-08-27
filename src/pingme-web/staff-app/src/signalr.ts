import { HubConnection, HubConnectionBuilder } from "@microsoft/signalr";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5190";

export async function connectOrdersHub(token: string): Promise<HubConnection> {
  const connection = new HubConnectionBuilder()
    .withUrl(`${API_BASE_URL}/hubs/orders?access_token=${token}`)
    .withAutomaticReconnect()
    .build();

  await connection.start();
  return connection;
}
