import { describe, expect, it } from "vitest";
import { addItem, removeItem, cartTotal, type CartItem } from "./cart";

describe("cart", () => {
  it("adds a new product to an empty cart", () => {
    const cart = addItem([], { productId: "p1", name: "Burger", price: 9.5, quantity: 1 });
    expect(cart).toEqual([{ productId: "p1", name: "Burger", price: 9.5, quantity: 1 }]);
  });

  it("increments quantity when the same product is added again", () => {
    const initial: CartItem[] = [{ productId: "p1", name: "Burger", price: 9.5, quantity: 1 }];
    const cart = addItem(initial, { productId: "p1", name: "Burger", price: 9.5, quantity: 2 });
    expect(cart).toEqual([{ productId: "p1", name: "Burger", price: 9.5, quantity: 3 }]);
  });

  it("removes a product from the cart", () => {
    const initial: CartItem[] = [
      { productId: "p1", name: "Burger", price: 9.5, quantity: 1 },
      { productId: "p2", name: "Beer", price: 4.0, quantity: 2 },
    ];
    const cart = removeItem(initial, "p1");
    expect(cart).toEqual([{ productId: "p2", name: "Beer", price: 4.0, quantity: 2 }]);
  });

  it("calculates the total across quantities", () => {
    const cart: CartItem[] = [
      { productId: "p1", name: "Burger", price: 9.5, quantity: 2 },
      { productId: "p2", name: "Beer", price: 4.0, quantity: 3 },
    ];
    expect(cartTotal(cart)).toBeCloseTo(31.0);
  });

  it("an empty cart has a total of 0", () => {
    expect(cartTotal([])).toBe(0);
  });
});
