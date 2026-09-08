import { useEffect, useRef } from "react";
import { cartTotal, type CartItem } from "../cart";
import { formatMoney } from "../format";

interface CartSheetProps {
  cart: CartItem[];
  currencyCode: string;
  locationLabel: string;
  placingOrder: boolean;
  error: string | null;
  onAddOne: (productId: string) => void;
  onRemoveOne: (productId: string) => void;
  onClose: () => void;
  onPlaceOrder: () => void;
}

export function CartSheet({
  cart,
  currencyCode,
  locationLabel,
  placingOrder,
  error,
  onAddOne,
  onRemoveOne,
  onClose,
  onPlaceOrder,
}: CartSheetProps) {
  const closeButtonRef = useRef<HTMLButtonElement>(null);

  // Escape closes the sheet, and the page underneath must not scroll behind it.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        onClose();
      }
    };
    document.addEventListener("keydown", onKeyDown);
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    closeButtonRef.current?.focus();

    return () => {
      document.removeEventListener("keydown", onKeyDown);
      document.body.style.overflow = previousOverflow;
    };
  }, [onClose]);

  return (
    <div
      className="sheet-backdrop"
      onClick={(event) => {
        if (event.target === event.currentTarget) {
          onClose();
        }
      }}
    >
      <div className="sheet" role="dialog" aria-modal="true" aria-labelledby="cart-title">
        <div className="sheet__grabber" aria-hidden="true" />

        <div className="sheet__header">
          <h2 className="sheet__title" id="cart-title">
            Your order
          </h2>
          <button type="button" className="btn btn--outline" onClick={onClose} ref={closeButtonRef}>
            Keep browsing
          </button>
        </div>

        <p className="order-bar__count">Delivered to {locationLabel}</p>

        {cart.length === 0 ? (
          <p className="empty">
            Your order is empty. Add something from the menu to get started.
          </p>
        ) : (
          <>
            <ul>
              {cart.map((item) => (
                <li className="cart-line" key={item.productId}>
                  <span className="cart-line__name">{item.name}</span>
                  <span className="cart-line__price">
                    {formatMoney(item.price * item.quantity, currencyCode)}
                  </span>

                  <div className="cart-line__controls">
                    <div className="stepper">
                      <button
                        type="button"
                        className="stepper__btn"
                        onClick={() => onRemoveOne(item.productId)}
                        aria-label={`Remove one ${item.name}`}
                      >
                        <span aria-hidden="true">&minus;</span>
                      </button>
                      <span className="stepper__count">
                        <span className="visually-hidden">{item.name} quantity: </span>
                        {item.quantity}
                      </span>
                      <button
                        type="button"
                        className="stepper__btn"
                        onClick={() => onAddOne(item.productId)}
                        aria-label={`Add another ${item.name}`}
                      >
                        <span aria-hidden="true">+</span>
                      </button>
                    </div>
                    <span className="order-bar__count">
                      {formatMoney(item.price, currencyCode)} each
                    </span>
                  </div>
                </li>
              ))}
            </ul>

            <p className="cart-total">
              <span>Total</span>
              <span className="cart-total__value">
                {formatMoney(cartTotal(cart), currencyCode)}
              </span>
            </p>

            {error && (
              <p className="notice notice--error" role="alert">
                {error}
              </p>
            )}

            <button
              type="button"
              className="btn btn--accent btn--lg btn--block"
              onClick={onPlaceOrder}
              disabled={placingOrder}
              style={{ marginTop: "var(--space-4)" }}
            >
              {placingOrder ? "Sending your order..." : "Send order to the bar"}
            </button>
          </>
        )}
      </div>
    </div>
  );
}
