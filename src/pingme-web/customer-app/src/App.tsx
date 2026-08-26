import { useEffect, useState } from "react";
import { resolveQrCode, placeOrder } from "./api";
import { addItem, removeItem, type CartItem } from "./cart";
import { MenuBrowser } from "./components/MenuBrowser";
import { CartView } from "./components/CartView";
import { OrderStatus } from "./components/OrderStatus";
import type { ResolveQrCodeResponse } from "./types";

function getCodeFromPath(): string | null {
  const match = window.location.pathname.match(/^\/p\/(.+)$/);
  return match ? match[1] : null;
}

export default function App() {
  const [resolved, setResolved] = useState<ResolveQrCodeResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [cart, setCart] = useState<CartItem[]>([]);
  const [placingOrder, setPlacingOrder] = useState(false);
  const [placedOrderId, setPlacedOrderId] = useState<string | null>(null);

  useEffect(() => {
    const code = getCodeFromPath();
    if (!code) {
      setError("No QR code found in the URL. Scan a code to start ordering.");
      return;
    }

    resolveQrCode(code)
      .then(setResolved)
      .catch(() => setError("This code isn't valid — ask a staff member for help."));
  }, []);

  if (error) {
    return <p>{error}</p>;
  }

  if (!resolved) {
    return <p>Loading menu...</p>;
  }

  if (placedOrderId) {
    return <OrderStatus orderId={placedOrderId} sessionId={resolved.sessionId} />;
  }

  const handleAddToCart = (productId: string, name: string, price: number) => {
    setCart((current) => addItem(current, { productId, name, price, quantity: 1 }));
  };

  const handleRemove = (productId: string) => {
    setCart((current) => removeItem(current, productId));
  };

  const handlePlaceOrder = async () => {
    setPlacingOrder(true);
    try {
      const order = await placeOrder(
        resolved.sessionId,
        cart.map((item) => ({ productId: item.productId, quantity: item.quantity })),
      );
      setPlacedOrderId(order.orderId);
    } catch {
      setError("Couldn't place your order — please try again.");
    } finally {
      setPlacingOrder(false);
    }
  };

  return (
    <div>
      <h1>{resolved.venueName}</h1>
      <p>{resolved.locationLabel}</p>
      <MenuBrowser menus={resolved.menus} onAddToCart={handleAddToCart} />
      <CartView
        cart={cart}
        onRemove={handleRemove}
        onPlaceOrder={handlePlaceOrder}
        placingOrder={placingOrder}
      />
    </div>
  );
}
