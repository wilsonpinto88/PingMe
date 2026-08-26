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

export function cartTotal(cart: CartItem[]): number {
  return cart.reduce((sum, item) => sum + item.price * item.quantity, 0);
}
