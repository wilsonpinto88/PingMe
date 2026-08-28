# PingMe — Current State & Where Plan 5 Fits

_Snapshot date: 2026-08-28_

## What PingMe is

A multi-tenant QR/NFC table-ordering platform: a customer scans a QR code at their table, orders from a menu, and staff see the order arrive in real time. Backend is .NET 10 (layered Domain/Application/Infrastructure/Api) with EF Core + PostgreSQL and tenant isolation enforced by a global EF Core query filter. Frontend is two separate Vite/React/TypeScript apps in one pnpm workspace.

## What's built and merged to `main` (Plans 1-4)

**Plan 1 — Bootstrap + Domain + EF Core + Multi-tenancy**
Solution scaffolding, core domain entities, `ICurrentTenantProvider`/`CurrentTenantProvider`, and the tenant query filter that scopes every tenant-owned row automatically.

**Plan 2 — Auth + Catalog/Admin**
Identity-based auth (JWT), Owner/Staff roles, and the Catalog admin API (products) — API + Swagger only, no UI.

**Plan 3 — Locations/QR + Customer app + Ordering**
- Locations and QR codes (`QrCodesController`, unique-index + retry-on-collision).
- `customer-app` (Vite/React): scan → menu → cart → place order.
- `OrdersController.Create` (`POST /orders`) and order-status polling, with full tenant-isolation coverage (a Tenant A session can't touch Tenant B products).

**Plan 4 — SignalR + Staff dashboard + local Docker Compose**
- `OrdersHub` (SignalR, staff-only, JWT-over-querystring auth, tenant-scoped groups `tenant-{tenantId}`).
- `IOrderNotifier` / `SignalROrderNotifier` — broadcasts `OrderReceived` / `OrderStatusChanged` from `OrdersController.Create` and `AdminOrdersController.UpdateStatus`.
- `staff-app` (Vite/React): live order dashboard, connect-then-fetch sequencing to avoid a duplication race.
- Local Docker Compose (API + Postgres only; frontends still run via `pnpm dev`).
- CORS policy for both dev frontends.

**Current gaps (explicitly deferred, tracked in `PROGRESS.md`):**
- No Owner/Staff admin UI beyond the order dashboard — Locations/QR/Catalog admin is API+Swagger only.
- No containerized frontends, no CI, no cloud deployment target.
- Docker was never actually run in this dev environment (compose file reviewed manually, not live-tested).

At this point, an order's lifecycle is entirely internal to PingMe: customer places it → staff sees it live → staff marks it preparing/ready/complete. Nothing leaves the system.

## Plan 5 — POS/ERP Integration (written, not yet executed)

**The gap it closes:** today, when an order lands in PingMe, nobody's kitchen printer, POS terminal, or back-office ERP knows about it unless a staff member manually re-keys it. Plan 5 makes PingMe *push* each new order out to a tenant's existing POS/ERP system automatically, so PingMe can sit in front of a restaurant's real operational systems instead of being an island.

**Scope for this plan (Level 1 — order-push MVP):** one-way, fire-and-forget delivery of new orders to a single external webhook URL per tenant. It does not attempt bidirectional sync, menu import from the POS, payment reconciliation, or multi-provider adapters beyond a generic webhook — those are future levels.

**Where it plugs into the existing code, concretely:**

```
OrdersController.Create (Plan 3, extended by Plan 4)
  1. _dbContext.Orders.Add(order); await SaveChangesAsync();
  2. build AdminOrderDto
  3. _orderNotifier.NotifyOrderReceivedAsync(...)   ← existing SignalR broadcast (Plan 4)
  4. NEW: _posOrderDispatcher.TryDispatchAsync(order, locationLabel, ct)
  5. NEW: order.RecordPosDeliveryStatus(result); await SaveChangesAsync();
  6. return Created(...)
```

**New pieces it adds:**
- `PosDeliveryStatus` enum (`NotConfigured`, `Pending`, `Sent`, `Failed`) stored on `Order`, and surfaced to staff via a new field on `AdminOrderDto` — so the dashboard staff already use (Plan 4) can show whether an order made it to the POS.
- Per-tenant settings (`TenantPosIntegrationSettings`: webhook URL + provider type + enabled flag), managed through a new Owner-only admin endpoint (`PosIntegrationSettingsController`), mirroring the same `[Authorize(Roles = "Owner")]` + `PingMeDbContext` pattern already used by `AdminOrdersController`/`LocationsController`.
- A small integration seam — `IPosIntegration` → `IPosIntegrationResolver` → `IPosOrderDispatcher` — so the dispatch call in `OrdersController` never throws and never blocks order creation even if a tenant's webhook is down or misconfigured (try/catch, logged, downgraded to a status flag).
- `WebhookPosIntegration`: the one concrete provider for this MVP, POSTs a camelCase JSON envelope (`eventId`, `eventType: "order.created"`, `occurredAt`, `tenantId`, and a nested `order` snapshot with location label, line items, total) to the tenant's configured URL. The envelope wrapper (added after a design-review pass on the written plan) costs nothing now but gives a future retry/idempotency mechanism a stable `eventId` to key on without a breaking payload change.

**Why this design, not something bigger:** it reuses every pattern already proven in Plans 1-4 (tenant-scoped settings entity, Owner-only admin controller, try/catch broadcast seam like `SignalROrderNotifier`) rather than introducing new architecture. The dispatcher is intentionally decoupled from any specific POS vendor — swapping in a real vendor SDK later (Square, Toast, etc.) means adding a new `IPosIntegration` implementation, not touching `OrdersController` again.

**Explicitly out of scope / deferred:** SSRF hardening on tenant-supplied webhook URLs, retry/backoff on failed delivery, and vendor-specific adapters beyond the generic webhook — all flagged as carried-forward items for a later plan.

**Status:** plan document written and committed (`Docs/superpowers optimized/plans/2026-08-27-pingme-plan-5-pos-integration.md`, 10 tasks), zero tasks executed yet.
