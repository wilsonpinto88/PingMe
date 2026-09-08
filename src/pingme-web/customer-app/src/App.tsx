import { useCallback, useEffect, useMemo, useState } from "react";
import { resolveQrCode, placeOrder } from "./api";
import { addItem, decrementItem, type CartItem } from "./cart";
import { applyTheme, FALLBACK_THEME, normalizeTheme } from "./theme";
import {
  clearPlacedOrder,
  loadCart,
  loadPlacedOrder,
  savePlacedOrder,
  saveCart,
  type PlacedOrder,
} from "./storage";
import { MenuBrowser } from "./components/MenuBrowser";
import { MenuSkeleton } from "./components/MenuSkeleton";
import { VenueHeader } from "./components/VenueHeader";
import { OrderBar } from "./components/OrderBar";
import { CartSheet } from "./components/CartSheet";
import { OrderStatus } from "./components/OrderStatus";
import type { CustomerProduct, ResolveQrCodeResponse } from "./types";

function getCodeFromPath(): string | null {
  const match = window.location.pathname.match(/^\/p\/(.+)$/);
  return match ? decodeURIComponent(match[1]) : null;
}

type LoadState = "loading" | "ready" | "error";

export default function App() {
  const code = useMemo(getCodeFromPath, []);

  const [loadState, setLoadState] = useState<LoadState>(code ? "loading" : "error");
  const [resolved, setResolved] = useState<ResolveQrCodeResponse | null>(null);
  const [loadError, setLoadError] = useState<string | null>(
    code ? null : "No code found in this link. Scan the code at your table to start ordering.",
  );

  const [cart, setCart] = useState<CartItem[]>(() => (code ? loadCart(code) : []));
  const [sheetOpen, setSheetOpen] = useState(false);
  const [placingOrder, setPlacingOrder] = useState(false);
  const [orderError, setOrderError] = useState<string | null>(null);
  const [placedOrder, setPlacedOrder] = useState<PlacedOrder | null>(() =>
    code ? loadPlacedOrder(code) : null,
  );

  const theme = useMemo(
    () => (resolved ? normalizeTheme(resolved.theme) : FALLBACK_THEME),
    [resolved],
  );

  const loadVenue = useCallback(async () => {
    if (!code) {
      return;
    }
    setLoadState("loading");
    setLoadError(null);
    try {
      setResolved(await resolveQrCode(code));
      setLoadState("ready");
    } catch {
      setLoadError("We could not open this menu. Check your connection, or ask a staff member.");
      setLoadState("error");
    }
  }, [code]);

  useEffect(() => {
    void loadVenue();
  }, [loadVenue]);

  // Theme the whole document, not just the React tree, so the browser chrome
  // and the area behind the safe insets match the venue too.
  useEffect(() => {
    applyTheme(theme);
  }, [theme]);

  useEffect(() => {
    if (code) {
      saveCart(code, cart);
    }
  }, [code, cart]);

  const handleAdd = useCallback((product: CustomerProduct) => {
    setCart((current) =>
      addItem(current, {
        productId: product.id,
        name: product.name,
        price: product.price,
        quantity: 1,
      }),
    );
  }, []);

  const handleAddById = useCallback((productId: string) => {
    setCart((current) => {
      const existing = current.find((item) => item.productId === productId);
      return existing ? addItem(current, { ...existing, quantity: 1 }) : current;
    });
  }, []);

  const handleRemoveOne = useCallback((productId: string) => {
    setCart((current) => decrementItem(current, productId));
  }, []);

  const handlePlaceOrder = async () => {
    if (!resolved || cart.length === 0) {
      return;
    }
    setPlacingOrder(true);
    setOrderError(null);
    try {
      const order = await placeOrder(
        resolved.sessionId,
        cart.map((item) => ({ productId: item.productId, quantity: item.quantity })),
      );
      const placed = { orderId: order.orderId, sessionId: resolved.sessionId };
      if (code) {
        savePlacedOrder(code, placed);
      }
      setPlacedOrder(placed);
      setCart([]);
      setSheetOpen(false);
    } catch {
      // The sheet stays open with the cart intact so nothing is retyped.
      setOrderError("That did not go through. Check your connection and try again.");
    } finally {
      setPlacingOrder(false);
    }
  };

  const handleStartNewOrder = () => {
    if (code) {
      clearPlacedOrder(code);
    }
    setPlacedOrder(null);
  };

  if (loadState === "error") {
    return (
      <main className="shell screen-center" id="main">
        <h1 className="status-headline">Something is not right</h1>
        <p className="notice notice--error" role="alert">
          {loadError}
        </p>
        {code && (
          <button type="button" className="btn btn--primary btn--lg" onClick={() => void loadVenue()}>
            Try again
          </button>
        )}
      </main>
    );
  }

  if (loadState === "loading" || !resolved) {
    return (
      <div className="app">
        <p className="visually-hidden" aria-live="polite">
          Loading the menu
        </p>
        <MenuSkeleton />
      </div>
    );
  }

  if (placedOrder) {
    return (
      <OrderStatus
        orderId={placedOrder.orderId}
        sessionId={placedOrder.sessionId}
        locationLabel={resolved.locationLabel}
        onStartNewOrder={handleStartNewOrder}
      />
    );
  }

  return (
    <div className={`app${cart.length > 0 ? " app--with-order-bar" : ""}`}>
      <a className="skip-link" href="#main">
        Skip to the menu
      </a>

      <VenueHeader
        venueName={resolved.venueName}
        locationLabel={resolved.locationLabel}
        theme={theme}
      />

      <main className="shell" id="main">
        <MenuBrowser
          menus={resolved.menus}
          cart={cart}
          currencyCode={theme.currencyCode}
          onAdd={handleAdd}
          onRemoveOne={handleRemoveOne}
        />
      </main>

      <OrderBar
        cart={cart}
        currencyCode={theme.currencyCode}
        onReview={() => setSheetOpen(true)}
      />

      {sheetOpen && (
        <CartSheet
          cart={cart}
          currencyCode={theme.currencyCode}
          locationLabel={resolved.locationLabel}
          placingOrder={placingOrder}
          error={orderError}
          onAddOne={handleAddById}
          onRemoveOne={handleRemoveOne}
          onClose={() => setSheetOpen(false)}
          onPlaceOrder={handlePlaceOrder}
        />
      )}
    </div>
  );
}
