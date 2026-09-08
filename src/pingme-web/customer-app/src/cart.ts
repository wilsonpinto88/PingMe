export interface CartItem {
  productId: string;
  name: string;
  price: number;
  quantity: number;
}

export function addItem(cart: CartItem[], item: CartItem): CartItem[] {
  const existing = cart.find((c) => c.productId === item.productId);
  if (existing) {
    return cart.map((c) =>
      c.productId === item.productId ? { ...c, quantity: c.quantity + item.quantity } : c,
    );
  }
  return [...cart, item];
}

export function removeItem(cart: CartItem[], productId: string): CartItem[] {
  return cart.filter((c) => c.productId !== productId);
}

/**
 * Steps a line down by one, dropping it entirely at zero so the cart never
 * holds a line with a quantity of 0.
 */
export function decrementItem(cart: CartItem[], productId: string): CartItem[] {
  const existing = cart.find((c) => c.productId === productId);
  if (!existing) {
    return cart;
  }
  if (existing.quantity <= 1) {
    return removeItem(cart, productId);
  }
  return cart.map((c) => (c.productId === productId ? { ...c, quantity: c.quantity - 1 } : c));
}

export function quantityOf(cart: CartItem[], productId: string): number {
  return cart.find((c) => c.productId === productId)?.quantity ?? 0;
}

/** Total number of units, not number of lines — this is what the order bar shows. */
export function cartItemCount(cart: CartItem[]): number {
  return cart.reduce((sum, item) => sum + item.quantity, 0);
}

export function cartTotal(cart: CartItem[]): number {
  return cart.reduce((sum, item) => sum + item.price * item.quantity, 0);
}
