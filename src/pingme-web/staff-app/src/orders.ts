import type { AdminOrderDto } from "./types";

/** The columns of the board, in the order work moves through them. */
export const ACTIVE_STATUSES = ["Received", "Accepted", "Preparing", "Ready"] as const;

export type ActiveStatus = (typeof ACTIVE_STATUSES)[number];

export const NEXT_STATUS: Record<string, string | undefined> = {
  Received: "Accepted",
  Accepted: "Preparing",
  Preparing: "Ready",
  Ready: "Delivered",
  Delivered: undefined,
};

/** Wording on the button that advances an order, in staff vocabulary. */
export const ADVANCE_LABEL: Record<string, string | undefined> = {
  Received: "Accept",
  Accepted: "Start preparing",
  Preparing: "Mark ready",
  Ready: "Mark delivered",
};

export function upsertById(current: AdminOrderDto[], updated: AdminOrderDto): AdminOrderDto[] {
  const index = current.findIndex((order) => order.id === updated.id);
  if (index === -1) {
    return [...current, updated];
  }

  const next = current.slice();
  next[index] = updated;
  return next;
}

export function orderTotal(order: AdminOrderDto): number {
  return order.items.reduce((sum, item) => sum + item.unitPrice * item.quantity, 0);
}

export function minutesWaiting(order: AdminOrderDto, now: number = Date.now()): number {
  const createdAt = Date.parse(order.createdAt);
  if (Number.isNaN(createdAt)) {
    return 0;
  }
  // A clock skew between server and device must not show a negative age.
  return Math.max(0, Math.floor((now - createdAt) / 60000));
}

export type AgeTone = "normal" | "warn" | "late";

/**
 * Thresholds are on waiting time, not status, so an order that has been sitting
 * in Preparing for twenty minutes is as loud as a never-accepted one.
 */
export function ageTone(minutes: number): AgeTone {
  if (minutes >= 10) {
    return "late";
  }
  if (minutes >= 5) {
    return "warn";
  }
  return "normal";
}

export function formatAge(minutes: number): string {
  if (minutes < 1) {
    return "just now";
  }
  if (minutes < 60) {
    return `${minutes}m`;
  }
  const hours = Math.floor(minutes / 60);
  return `${hours}h ${minutes % 60}m`;
}

/**
 * Groups orders into board columns, oldest first inside each column so the
 * longest-waiting customer is always at the top of the stack.
 */
export function groupByStatus(orders: AdminOrderDto[]): Record<ActiveStatus, AdminOrderDto[]> {
  const grouped = {
    Received: [] as AdminOrderDto[],
    Accepted: [] as AdminOrderDto[],
    Preparing: [] as AdminOrderDto[],
    Ready: [] as AdminOrderDto[],
  };

  for (const order of orders) {
    if ((ACTIVE_STATUSES as readonly string[]).includes(order.status)) {
      grouped[order.status as ActiveStatus].push(order);
    }
  }

  for (const status of ACTIVE_STATUSES) {
    grouped[status].sort((a, b) => Date.parse(a.createdAt) - Date.parse(b.createdAt));
  }

  return grouped;
}
