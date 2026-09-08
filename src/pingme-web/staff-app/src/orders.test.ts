import { describe, expect, it } from "vitest";
import {
  ageTone,
  formatAge,
  groupByStatus,
  minutesWaiting,
  orderTotal,
  upsertById,
} from "./orders";
import type { AdminOrderDto } from "./types";

function makeOrder(
  id: string,
  status: string,
  overrides: Partial<AdminOrderDto> = {},
): AdminOrderDto {
  return {
    id,
    status,
    createdAt: "2026-01-01T00:00:00Z",
    items: [],
    locationLabel: "Table 1",
    posDeliveryStatus: "NotConfigured",
    ...overrides,
  };
}

describe("upsertById", () => {
  it("appends a new order that is not already in the list", () => {
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

describe("orderTotal", () => {
  it("multiplies unit price by quantity across every line", () => {
    const order = makeOrder("a", "Received", {
      items: [
        { productName: "Burger", unitPrice: 9.5, quantity: 2 },
        { productName: "Beer", unitPrice: 4, quantity: 3 },
      ],
    });
    expect(orderTotal(order)).toBeCloseTo(31);
  });

  it("is zero for an order with no items", () => {
    expect(orderTotal(makeOrder("a", "Received"))).toBe(0);
  });
});

describe("minutesWaiting", () => {
  const createdAt = "2026-01-01T12:00:00Z";

  it("reports whole minutes since the order was placed", () => {
    const now = Date.parse("2026-01-01T12:07:30Z");
    expect(minutesWaiting(makeOrder("a", "Received", { createdAt }), now)).toBe(7);
  });

  it("never reports a negative age when the device clock runs behind", () => {
    const now = Date.parse("2026-01-01T11:55:00Z");
    expect(minutesWaiting(makeOrder("a", "Received", { createdAt }), now)).toBe(0);
  });

  it("treats an unparseable timestamp as zero rather than NaN", () => {
    expect(minutesWaiting(makeOrder("a", "Received", { createdAt: "not-a-date" }))).toBe(0);
  });
});

describe("ageTone", () => {
  it("escalates from normal to warn to late", () => {
    expect(ageTone(0)).toBe("normal");
    expect(ageTone(4)).toBe("normal");
    expect(ageTone(5)).toBe("warn");
    expect(ageTone(9)).toBe("warn");
    expect(ageTone(10)).toBe("late");
    expect(ageTone(45)).toBe("late");
  });
});

describe("formatAge", () => {
  it("reads naturally at each scale", () => {
    expect(formatAge(0)).toBe("just now");
    expect(formatAge(7)).toBe("7m");
    expect(formatAge(59)).toBe("59m");
    expect(formatAge(75)).toBe("1h 15m");
  });
});

describe("groupByStatus", () => {
  it("puts each order in its status column", () => {
    const grouped = groupByStatus([
      makeOrder("a", "Received"),
      makeOrder("b", "Preparing"),
      makeOrder("c", "Ready"),
    ]);

    expect(grouped.Received.map((o) => o.id)).toEqual(["a"]);
    expect(grouped.Preparing.map((o) => o.id)).toEqual(["b"]);
    expect(grouped.Ready.map((o) => o.id)).toEqual(["c"]);
    expect(grouped.Accepted).toEqual([]);
  });

  it("drops delivered orders off the active board", () => {
    const grouped = groupByStatus([makeOrder("a", "Delivered")]);

    expect(Object.values(grouped).every((column) => column.length === 0)).toBe(true);
  });

  it("puts the longest-waiting order at the top of its column", () => {
    const grouped = groupByStatus([
      makeOrder("newer", "Received", { createdAt: "2026-01-01T12:10:00Z" }),
      makeOrder("older", "Received", { createdAt: "2026-01-01T12:00:00Z" }),
    ]);

    expect(grouped.Received.map((o) => o.id)).toEqual(["older", "newer"]);
  });

  it("ignores a status the board does not know about", () => {
    const grouped = groupByStatus([makeOrder("a", "Refunded")]);

    expect(Object.values(grouped).every((column) => column.length === 0)).toBe(true);
  });
});
