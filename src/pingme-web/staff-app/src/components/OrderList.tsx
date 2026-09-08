import { ACTIVE_STATUSES, groupByStatus, NEXT_STATUS, type ActiveStatus } from "../orders";
import { OrderCard } from "./OrderCard";
import type { AdminOrderDto } from "../types";

const COLUMN_TITLES: Record<ActiveStatus, string> = {
  Received: "New",
  Accepted: "Accepted",
  Preparing: "Preparing",
  Ready: "Ready to run",
};

const EMPTY_COPY: Record<ActiveStatus, string> = {
  Received: "No new orders.",
  Accepted: "Nothing accepted yet.",
  Preparing: "Nothing being prepared.",
  Ready: "Nothing waiting to be run out.",
};

interface OrderListProps {
  orders: AdminOrderDto[];
  now: number;
  /** On narrow screens the board collapses to one status at a time. */
  visibleStatus: ActiveStatus | null;
  pendingOrderIds: Set<string>;
  errorByOrderId: Record<string, string | undefined>;
  onAdvanceStatus: (orderId: string, nextStatus: string) => void;
}

export function OrderList({
  orders,
  now,
  visibleStatus,
  pendingOrderIds,
  errorByOrderId,
  onAdvanceStatus,
}: OrderListProps) {
  const grouped = groupByStatus(orders);
  const columns = visibleStatus ? [visibleStatus] : ACTIVE_STATUSES;

  const nothingActive = ACTIVE_STATUSES.every((status) => grouped[status].length === 0);
  if (nothingActive) {
    return (
      <div className="board">
        <div className="empty" style={{ gridColumn: "1 / -1" }}>
          <p className="empty__title">All caught up</p>
          <p>New orders appear here the moment a customer sends one.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="board">
      {columns.map((status) => (
        <section key={status} aria-labelledby={`column-${status}`}>
          <h2 className="column__head" id={`column-${status}`}>
            <span>{COLUMN_TITLES[status]}</span>
            <span className="column__rule" aria-hidden="true" />
            <span className="column__count">{grouped[status].length}</span>
          </h2>

          {grouped[status].length === 0 ? (
            <p className="empty">{EMPTY_COPY[status]}</p>
          ) : (
            <ul className="column__list">
              {grouped[status].map((order) => (
                <OrderCard
                  key={order.id}
                  order={order}
                  now={now}
                  pending={pendingOrderIds.has(order.id)}
                  error={errorByOrderId[order.id]}
                  nextStatus={NEXT_STATUS[order.status]}
                  onAdvance={onAdvanceStatus}
                />
              ))}
            </ul>
          )}
        </section>
      ))}
    </div>
  );
}
