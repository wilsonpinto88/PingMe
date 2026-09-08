import { cartItemCount, cartTotal, type CartItem } from "../cart";
import { formatMoney } from "../format";

interface OrderBarProps {
  cart: CartItem[];
  currencyCode: string;
  onReview: () => void;
}

/**
 * Sits above the home indicator and stays put while the menu scrolls, so the
 * running total and the way forward are always one thumb-reach away.
 */
export function OrderBar({ cart, currencyCode, onReview }: OrderBarProps) {
  const count = cartItemCount(cart);
  if (count === 0) {
    return null;
  }

  return (
    <div className="order-bar">
      <div className="shell order-bar__inner">
        <div className="order-bar__summary">
          <p className="order-bar__count">
            {count} {count === 1 ? "item" : "items"}
          </p>
          <p className="order-bar__total">{formatMoney(cartTotal(cart), currencyCode)}</p>
        </div>
        <button type="button" className="btn btn--accent btn--lg" onClick={onReview}>
          Review order
        </button>
      </div>
    </div>
  );
}
