import { useState } from "react";
import { getOrders, login, updateOrderStatus } from "./api";
import { connectOrdersHub } from "./signalr";
import { upsertById } from "./orders";
import { OrderList } from "./components/OrderList";
import type { AdminOrderDto } from "./types";

export default function App() {
  const [token, setToken] = useState<string | null>(null);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loginError, setLoginError] = useState<string | null>(null);
  const [orders, setOrders] = useState<AdminOrderDto[]>([]);
  const [errorByOrderId, setErrorByOrderId] = useState<Record<string, string | undefined>>({});

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoginError(null);
    try {
      const auth = await login(email, password);
      await connectOrdersHub(auth.token, (connection) => {
        connection.on("OrderReceived", (order: AdminOrderDto) => {
          setOrders((current) => upsertById(current, order));
        });
        connection.on("OrderStatusChanged", (order: AdminOrderDto) => {
          setOrders((current) => upsertById(current, order));
        });
      });

      const initialOrders = await getOrders(auth.token);
      setOrders(initialOrders);
      setToken(auth.token);
    } catch {
      setLoginError("Login failed — check your email and password.");
    }
  };

  const handleAdvanceStatus = async (orderId: string, nextStatus: string) => {
    if (!token) {
      return;
    }
    try {
      await updateOrderStatus(token, orderId, nextStatus);
      setErrorByOrderId((current) => ({ ...current, [orderId]: undefined }));
    } catch {
      setErrorByOrderId((current) => ({
        ...current,
        [orderId]: "Couldn't update — try refreshing.",
      }));
    }
  };

  if (!token) {
    return (
      <form onSubmit={handleLogin}>
        <h1>PingMe Staff</h1>
        <input
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="Email"
        />
        <input
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          placeholder="Password"
        />
        <button type="submit">Log in</button>
        {loginError && <p>{loginError}</p>}
      </form>
    );
  }

  return (
    <div>
      <h1>Orders</h1>
      <OrderList orders={orders} onAdvanceStatus={handleAdvanceStatus} errorByOrderId={errorByOrderId} />
    </div>
  );
}
