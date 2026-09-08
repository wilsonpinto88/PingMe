import type { CartItem } from "./cart";

/**
 * A phone locks, a browser tab gets swapped out, a customer reloads. Without
 * persistence any of those loses the cart and, worse, loses the reference to an
 * order that was already placed and is being prepared. Both are stored locally,
 * keyed by QR code so two different tables never share state.
 *
 * Every access is wrapped: private browsing and blocked site data make
 * localStorage throw rather than return null.
 */

const CART_PREFIX = "pingme.cart.";
const ORDER_PREFIX = "pingme.order.";

/** Matches the 4-hour customer session the API issues. */
const TTL_MS = 4 * 60 * 60 * 1000;

interface Envelope<T> {
  savedAt: number;
  value: T;
}

function read<T>(key: string): T | null {
  try {
    const raw = window.localStorage.getItem(key);
    if (!raw) {
      return null;
    }
    const envelope = JSON.parse(raw) as Envelope<T>;
    if (typeof envelope?.savedAt !== "number" || Date.now() - envelope.savedAt > TTL_MS) {
      window.localStorage.removeItem(key);
      return null;
    }
    return envelope.value;
  } catch {
    return null;
  }
}

function write<T>(key: string, value: T): void {
  try {
    window.localStorage.setItem(key, JSON.stringify({ savedAt: Date.now(), value }));
  } catch {
    // Storage being unavailable must never break ordering.
  }
}

function clear(key: string): void {
  try {
    window.localStorage.removeItem(key);
  } catch {
    // Ignored for the same reason as above.
  }
}

export function loadCart(code: string): CartItem[] {
  const cart = read<CartItem[]>(CART_PREFIX + code);
  if (!Array.isArray(cart)) {
    return [];
  }
  // Guard against a stale or hand-edited payload reaching the reducer.
  return cart.filter(
    (item) =>
      typeof item?.productId === "string" &&
      typeof item?.name === "string" &&
      typeof item?.price === "number" &&
      typeof item?.quantity === "number" &&
      item.quantity > 0,
  );
}

export function saveCart(code: string, cart: CartItem[]): void {
  if (cart.length === 0) {
    clear(CART_PREFIX + code);
    return;
  }
  write(CART_PREFIX + code, cart);
}

export interface PlacedOrder {
  orderId: string;
  sessionId: string;
}

export function loadPlacedOrder(code: string): PlacedOrder | null {
  const order = read<PlacedOrder>(ORDER_PREFIX + code);
  return typeof order?.orderId === "string" && typeof order?.sessionId === "string" ? order : null;
}

export function savePlacedOrder(code: string, order: PlacedOrder): void {
  write(ORDER_PREFIX + code, order);
}

export function clearPlacedOrder(code: string): void {
  clear(ORDER_PREFIX + code);
}
