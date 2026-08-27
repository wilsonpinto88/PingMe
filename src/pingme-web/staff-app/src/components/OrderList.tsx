import type { AdminOrderDto } from "../types";

const NextStatus: Record<string, string | undefined> = {
  Received: "Accepted",
  Accepted: "Preparing",
  Preparing: "Ready",
  Ready: "Delivered",
  Delivered: undefined,
};

interface OrderListProps {
  orders: AdminOrderDto[];
  onAdvanceStatus: (orderId: string, nextStatus: string) => void;
  errorByOrderId: Record<string, string | undefined>;
}

export function OrderList({ orders, onAdvanceStatus, errorByOrderId }: OrderListProps) {
  if (orders.length === 0) {
    return <p>No orders yet.</p>;
  }

  return (
    <ul>
      {orders.map((order) => {
        const next = NextStatus[order.status];
        return (
          <li key={order.id}>
            <strong>{order.status}</strong> — order {order.id.slice(0, 8)}
            <ul>
              {order.items.map((item, index) => (
                <li key={index}>
                  {item.quantity} x {item.productName}
                </li>
              ))}
            </ul>
            {next && (
              <button onClick={() => onAdvanceStatus(order.id, next)}>
                Mark as {next}
              </button>
            )}
            {errorByOrderId[order.id] && <p>{errorByOrderId[order.id]}</p>}
          </li>
        );
      })}
    </ul>
  );
}
