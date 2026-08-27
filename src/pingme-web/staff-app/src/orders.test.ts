import { describe, expect, it } from "vitest";
import { upsertById } from "./orders";
import type { AdminOrderDto } from "./types";

function makeOrder(id: string, status: string): AdminOrderDto {
  return { id, status, createdAt: "2026-01-01T00:00:00Z", items: [] };
}

describe("upsertById", () => {
  it("appends a new order that isn't already in the list", () => {
    const current = [makeOrder("a", "Received")];
    const result = upsertById(current, makeOrder("b", "Received"));
    expect(result.map((o) => o.id)).toEqual(["a", "b"]);
  });

  it("replaces an existing order in place instead of duplicating it", () => {
    const current = [makeOrder("a", "Received"), makeOrder("b", "Received")];
    const result = upsertById(current, makeOrder("a", "Accepted"));
    expect(result).toHaveLength(2);
    expect(result.find((o) => o.id === "a")!.status).toBe("Accepted");
  });

  it("never grows the list when the same order arrives twice", () => {
    const current = [makeOrder("a", "Received")];
    const once = upsertById(current, makeOrder("a", "Accepted"));
    const twice = upsertById(once, makeOrder("a", "Accepted"));
    expect(twice).toHaveLength(1);
  });
});
