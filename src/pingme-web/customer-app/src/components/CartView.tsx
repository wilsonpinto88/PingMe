import { cartTotal, type CartItem } from "../cart";

interface CartViewProps {
  cart: CartItem[];
  onRemove: (productId: string) => void;
  onPlaceOrder: () => void;
  placingOrder: boolean;
}

export function CartView({ cart, onRemove, onPlaceOrder, placingOrder }: CartViewProps) {
  if (cart.length === 0) {
    return <p>Your cart is empty.</p>;
  }

  return (
    <div>
      <h2>Your order</h2>
      <ul>
        {cart.map((item) => (
          <li key={item.productId}>
            {item.quantity} x {item.name} — €{(item.price * item.quantity).toFixed(2)}
            <button onClick={() => onRemove(item.productId)}>Remove</button>
          </li>
        ))}
      </ul>
      <p>Total: €{cartTotal(cart).toFixed(2)}</p>
      <button onClick={onPlaceOrder} disabled={placingOrder}>
        {placingOrder ? "Placing order..." : "Place order"}
      </button>
    </div>
  );
}
