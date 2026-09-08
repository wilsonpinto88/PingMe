import { ADVANCE_LABEL, ageTone, formatAge, minutesWaiting, orderTotal } from "../orders";
import type { AdminOrderDto } from "../types";

interface OrderCardProps {
  order: AdminOrderDto;
  now: number;
  pending: boolean;
  error?: string;
  onAdvance: (orderId: string, nextStatus: string) => void;
  nextStatus?: string;
}

function formatMoney(amount: number): string {
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency: "EUR" }).format(amount);
  } catch {
    return amount.toFixed(2);
  }
}

export function OrderCard({
  order,
  now,
  pending,
  error,
  onAdvance,
  nextStatus,
}: OrderCardProps) {
  const minutes = minutesWaiting(order, now);
  const tone = ageTone(minutes);
  const advanceLabel = ADVANCE_LABEL[order.status];
  const posFailed = order.posDeliveryStatus === "Failed";

  return (
    <li className="order" data-status={order.status}>
      <div className="order__head">
        <div>
          <p className="order__location">{order.locationLabel}</p>
          <p className="order__ref">
            <span className="visually-hidden">Order reference </span>
            {order.id.slice(0, 8)}
          </p>
        </div>
        <span className="order__age" data-age={tone}>
          {formatAge(minutes)}
          {tone !== "normal" && (
            <span className="visually-hidden"> — waiting {tone === "late" ? "too long" : "a while"}</span>
          )}
        </span>
      </div>

      <ul className="order__items">
        {order.items.map((item, index) => (
          <li className="item" key={`${item.productName}-${index}`}>
            <span className="item__qty">{item.quantity}&times;</span>
            <span>{item.productName}</span>
            <span className="item__price">{formatMoney(item.unitPrice * item.quantity)}</span>
          </li>
        ))}
      </ul>

      <div className="order__foot">
        <span className="order__total">{formatMoney(orderTotal(order))}</span>

        {/* Only worth surfacing when it needs a human: a silent POS failure
            means the kitchen system never saw this order. */}
        {posFailed && (
          <span className="badge" data-tone="danger">
            POS not updated
          </span>
        )}

        {advanceLabel && nextStatus && (
          <button
            type="button"
            className="btn btn--advance"
            onClick={() => onAdvance(order.id, nextStatus)}
            disabled={pending}
          >
            {pending ? "Updating..." : advanceLabel}
          </button>
        )}
      </div>

      {error && (
        <p className="notice notice--error notice--inline" role="alert">
          {error}
        </p>
      )}
    </li>
  );
}
