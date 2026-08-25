# PingMe MVP Design — Domain Model, Multi-Tenancy & Module Boundaries

Date: 2026-08-25 (revised twice same day after external design reviews)
Status: Approved (pending user spec review)
Source idea: [Docs/Ideas/Scratch_1/Scratch_1.md](../Ideas/Scratch_1/Scratch_1.md)
Reviews incorporated: [Docs/Ideas/aboutDesign1.md](../Ideas/aboutDesign1.md), [Docs/Ideas/aboutDesign2.md](../Ideas/aboutDesign2.md)
Approved by user: 2026-08-25

## 1. Scope & Non-Goals

PingMe is a QR/NFC ordering platform for venues where a customer orders from a
physical location without staff intervention: restaurants, bars, stadiums,
cinemas, festivals.

**In scope for this design / MVP:**
- Single Venue per Tenant (chains deferred, see Non-Goals).
- Generic, venue-type-agnostic Location model (works for a restaurant table,
  a stadium seat, or a festival zone without special-cased code).
- Customer ordering flow: scan → browse menu → cart → place order. No
  customer account.
- One Admin React app used by two roles: Owner (full config) and Staff
  (order dashboard only).
- Realtime order status via SignalR.
- Order line items snapshot product name/price at order time.
- Opaque public QR identifiers resolving to Tenant → Location.
- Simple email/password auth (ASP.NET Core Identity + JWT) for Admin app.
  JWT chosen over cookie auth for simplicity if Admin SPA and API end up on
  different origins/hosts; revisit if they're deployed same-origin — not a
  decision worth blocking on now.
- Strict tenant data isolation enforced at the data-access layer, not by
  convention.

**Non-goals (explicitly deferred, not forgotten):**
- Multi-venue tenants (chains/franchises). Tenant is already the top-level
  entity so this is additive later — no migration of existing data model
  needed, just a new `Venue` collection under `Tenant` instead of 1:1.
- Payments (Stripe etc.) — cash/card at venue for MVP.
- Analytics, promotions, discounts, tax rules, printer/POS integrations, SSO.
- Splitting a single Location's order across multiple simultaneous customers
  (no customer identity in MVP — one active `CustomerSession` per Location at
  a time; see Section 2a).
- Separate staff-only frontend app (Staff is a role inside the one Admin app).
- Menu pricing rules beyond a flat product price (happy hour, venue-specific
  or temporary pricing) — `ProductOption` already models add-ons/modifiers so
  the model isn't blocked from growing into this later, but no pricing-rule
  engine ships in MVP.

## 2. Domain Model

```
Tenant
 ├── Users (Owner, Staff — role-based)
 ├── Venue                          (1:1 for MVP; 1:many post-MVP)
 │    └── Location (self-referencing tree, ParentLocationId nullable)
 │         e.g. "Table 12"                      (depth 1, restaurant)
 │              "Section B" → "Row 12" → "Seat 18"  (depth 3, stadium)
 │         └── CustomerSession (Id, ExpiresAt, ClosedAt nullable)  — see 2a
 ├── Menu
 │    └── Category
 │         └── Product (name, price, availability)
 │              └── ProductOption (child, no own TenantId — see 2b)
 └── Order (CustomerSessionId)
      └── OrderItem (child, no own TenantId — see 2b)
           (ProductId, ProductName snapshot, UnitPrice snapshot, Quantity)

QrCode
 - Opaque public code (e.g. "8Kx92LmQ")
 - Resolves to: TenantId + LocationId
```

**Why Location is a self-referencing tree, not a fixed hierarchy:** a fixed
`Venue → Area → Location` is 2 levels, but Scratch_1's own stadium example
("Section B / Row 12 / Seat 18") needs 3. A nullable `ParentLocationId` on
`Location` supports any depth per venue type without conditional logic in
code — a restaurant just never populates deeper than depth 1.

**Order snapshot rule:** `OrderItem` always stores `ProductName` and
`UnitPrice` as copied values at order time. Historical orders never change
when a product's current name/price changes later.

**QR resolution:** the public code never exposes a database ID. Backend
resolves `code → TenantId, LocationId` before any customer-facing operation.

### 2a. CustomerSession

A scan doesn't go straight to "browse menu, then trust whatever the client
later claims." It opens (or resumes) a `CustomerSession`:

```
CustomerSession
----------------
Id           (opaque, this is what the client actually holds)
TenantId
LocationId
CreatedAt
ExpiresAt
ClosedAt     (nullable — staff closes it, e.g. after payment)
```

Flow: `GET /p/{code}` resolves the QR to a `Location`, then creates a new
`CustomerSession` for that Location or returns the existing open one. The
client only ever holds `sessionId` from that point on — never a raw
`tenantId`/`locationId`. Staff can close a session from the Admin app (e.g.
once the table has paid); the next scan at that Location starts a fresh
session. This also gives a future path to "two people order into the same
session" without redesigning the ordering system, without building that now.

### 2b. TenantId placement: aggregate roots only

`TenantId` lives on entities that are directly queried or referenced as
roots: `Venue`, `Location`, `CustomerSession`, `Menu`, `Category`, `Product`,
`Order`, `User`, `QrCode`. It does **not** live on `OrderItem` or
`ProductOption` — these are child entities of `Order` and `Product`
respectively, reached only by navigating from their parent, which is already
tenant-filtered. Putting `TenantId` on both a parent and its child creates an
invariant (`OrderItem.TenantId == Order.TenantId`) that has to be maintained
by hand for no isolation benefit, since the child is never queried standalone.

**Hard rule this depends on:** application code must never expose a query
path that reads `OrderItem` or `ProductOption` without joining through their
parent aggregate. No `IOrderItemRepository.GetById`, no ad-hoc
`context.OrderItems.Where(...)`. This rule is enforced by not writing such a
method — Ordering module's public interface only returns `Order` (with items
included), never a bare `OrderItem`.

## 3. Multi-Tenancy & Isolation

Tenant isolation is enforced structurally, not by remembering to add a
`WHERE TenantId = ...` clause everywhere:

- Every tenant-owned aggregate root (`Venue`, `Location`, `CustomerSession`,
  `Menu`, `Category`, `Product`, `Order`, `User`, `QrCode`) carries a
  `TenantId`. Child entities (`OrderItem`, `ProductOption`) do not — see
  Section 2b for why, and the hard rule that makes it safe.
- `PingMeDbContext` applies an EF Core **global query filter** on every
  tenant-owned root entity type, scoped to the current tenant.
- Current tenant is resolved per-request by `ICurrentTenantProvider`:
  - Admin/Staff requests → from the authenticated user's `TenantId` claim.
  - Customer requests → from the QR code resolution (`code → TenantId`)
    performed once at the start of the request pipeline.
- No repository or handler queries `DbSet<T>` with a tenant filter written
  by hand — the global filter makes cross-tenant leakage a compile-time-
  impossible-to-forget class of bug, not a per-query discipline problem.

## 4. Module Boundaries (Approach A — folder-based)

No restructuring of the 4 existing projects. Each layer project gets
per-module subfolders; modules only talk to each other through interfaces
in `Application`, never through concrete classes or `Infrastructure` types.

```
PingMe.Domain/
 ├── Identity/        (User, Role)
 ├── Tenants/         (Tenant)
 ├── Locations/       (Venue, Location)
 ├── Catalog/         (Menu, Category, Product)
 └── Ordering/        (Order, OrderItem, OrderStatus)

PingMe.Application/
 ├── Identity/
 ├── Tenants/
 ├── Locations/
 ├── Catalog/
 └── Ordering/
      (each: commands/queries + the interfaces other modules depend on)

PingMe.Infrastructure/
 ├── Persistence/      (PingMeDbContext, EF configs per module folder)
 ├── Identity/
 └── Realtime/          (SignalR hubs)

PingMe.Api/
 └── Controllers/ (or Minimal API endpoint groups) per module
```

Rule: `Ordering` may depend on `Catalog`'s and `Locations`' *interfaces*
(e.g. `IProductLookup`, `ILocationResolver`) but never on their EF entities
or DbContext directly. This is what makes future extraction (e.g. pulling
`Ordering` into its own service if it becomes the high-traffic module) a
refactor, not a rewrite.

## 5. Data Flow

**Customer:**
```
QR scan → GET /p/{code} → resolve Tenant+Location → create/resume
CustomerSession → client stores sessionId → React loads menu
→ cart (client-side) → POST /orders { sessionId, items } → server derives
Tenant+Location from session → persisted → SignalR notifies staff
→ customer subscribes to order status via SignalR → sees status updates
```

**Staff/Owner (Admin app):**
```
Login (JWT) → dashboard (SignalR-subscribed) → new order arrives
→ Accept → Preparing → Ready → Delivered (each transition persisted +
broadcast to the customer's SignalR connection)
```

## 6. API Contracts (high-level, MVP)

- `GET /p/{code}` — resolve QR code → creates/resumes a `CustomerSession` →
  returns `{ sessionId, venueName, locationLabel, menu }`. Never returns raw
  `tenantId`/`locationId` to the client.
- `POST /orders` — `{ sessionId, items: [{productId, quantity}] }` → server
  resolves `TenantId`/`LocationId` from the session server-side (never trusts
  client-supplied tenant/location) → `201` with order id + initial status.
  Rejects with `409`/`410` if the session is closed or expired.
- `GET /orders/{id}/status` — polling fallback if SignalR unavailable.
- SignalR hub `/hubs/orders` — server → client events: `OrderReceived`
  (to staff group), `OrderStatusChanged` (to customer connection + staff
  group).
- Admin: standard CRUD under `/admin/menu`, `/admin/locations`,
  `/admin/qrcodes`, `/admin/orders`, guarded by JWT + role.
- `POST /admin/locations/{id}/close-session` — Staff/Owner closes the active
  `CustomerSession` at a Location (e.g. after payment); next scan starts
  fresh.

DTOs only cross the API boundary — no EF entities serialized directly.

## 7. Order State Machine

`Received → Accepted → Preparing → Ready → Delivered`

Future (not MVP): `Cancelled`, `Rejected`, `PaymentPending`, `PaymentFailed`,
`Refunded`.

## 8. Error Handling

- QR resolution failure (unknown/expired code) → `404` with a customer-
  friendly React error screen ("This code isn't valid — ask a staff member").
- Order placement against an unavailable product → `409`, cart item flagged
  client-side for removal/quantity adjustment.
- Admin auth failure → standard `401`/`403`; no distinction leaked between
  "wrong password" and "unknown user".
- SignalR disconnect → client falls back to polling `GET /orders/{id}/status`
  until reconnected.

## 9. Testing Strategy

- `PingMe.UnitTests`: domain logic per module (order state transitions,
  tenant filter behavior, QR resolution logic) — no database.
- `PingMe.IntegrationTests`: EF Core against a real PostgreSQL (Testcontainers
  or local dev DB), verifying the global tenant query filter actually blocks
  cross-tenant reads — this is the single most important test in the suite
  given Section 3. Also covers: `POST /orders` rejects a `sessionId` that
  doesn't resolve, is closed, or is expired; closing a session at a Location
  makes the next `GET /p/{code}` create a new one.

  Treat cross-tenant isolation as **security-critical tests**, not ordinary
  integration tests. Minimum required cases:
  - Tenant A cannot read Tenant B's `Product`, `Order`, or `Location`.
  - Tenant A cannot manipulate Tenant B's `Order` (e.g. status transitions).
  - Tenant A cannot use/resolve Tenant B's `CustomerSession`.
  - A `CustomerSession` from Tenant A submitting `productId`s belonging to
    Tenant B on `POST /orders` is rejected, not silently cross-joined.
- Frontend: component tests for cart/menu logic; no e2e in MVP.

## 10. Rollout / Migration Notes

- EF Core Code-First migrations from day one; no hand-written SQL schema.
- Seed data: one demo Tenant + Venue + a few Locations/Products for local
  dev and for the "test with one real restaurant/bar" milestone from
  Scratch_1.
- No production data exists yet — no migration-of-existing-data concerns
  at this stage.

## 11. Failure-Mode Check (adversarial pass)

| Failure mode | Severity | Resolution |
|---|---|---|
| Fixed 2-level Area/Location can't represent stadium's 3-level Section/Row/Seat | Critical | Resolved — self-referencing `Location` tree (Section 2) |
| Hand-written tenant filters get forgotten on a new query, leaking cross-tenant data | Critical | Resolved — EF Core global query filter + `ICurrentTenantProvider` (Section 3), verified by integration test (Section 9) |
| Client-supplied `tenantId`/`locationId` on order creation lets a customer spoof which tenant/location they order into | Critical | Resolved — `CustomerSession` holds the server-resolved identity; `POST /orders` only accepts `sessionId` (Section 2a, 6) |
| No mechanism to reset a table/seat between customers | Minor (was undocumented, now resolved) | `CustomerSession.ClosedAt` + `POST /admin/locations/{id}/close-session` (Section 2a, 6) |
| Tenant later needs multiple Venues (chain) | Minor | Documented non-goal — additive change, `Tenant` is already top-level (Section 1) |
| No customer identity means two people at the same table can't have separate carts | Minor | Documented non-goal for MVP — one `CustomerSession` per Location at a time; the session model gives a path to fix this later without a redesign |
| SignalR connection drops mid-session | Minor | Polling fallback endpoint specified (Section 8) |

## 12. Next Step

Proceed to `writing-plans` to break this design into an ordered, verifiable
implementation plan once this spec is reviewed and approved.
