import type { AdminOrderDto } from "./types";

export function upsertById(current: AdminOrderDto[], updated: AdminOrderDto): AdminOrderDto[] {
  const index = current.findIndex((order) => order.id === updated.id);
  if (index === -1) {
    return [...current, updated];
  }

  const next = current.slice();
  next[index] = updated;
  return next;
}
