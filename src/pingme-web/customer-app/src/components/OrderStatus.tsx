import { useEffect, useState } from "react";
import { getOrderStatus } from "../api";

interface OrderStatusProps {
  orderId: string;
  sessionId: string;
}

export function OrderStatus({ orderId, sessionId }: OrderStatusProps) {
  const [status, setStatus] = useState("Received");

  useEffect(() => {
    const interval = setInterval(async () => {
      try {
        const result = await getOrderStatus(orderId, sessionId);
        setStatus(result.status);
      } catch {
        // Best-effort polling — a transient failure just retries on the next tick.
      }
    }, 5000);

    return () => clearInterval(interval);
  }, [orderId, sessionId]);

  return (
    <div>
      <h2>Order status</h2>
      <p>{status}</p>
    </div>
  );
}
