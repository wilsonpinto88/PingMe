# PingMe POS Integration Design (Plan 5)

Date: 2026-08-26
Status: Approved (pending user spec review)
Source idea: [Docs/Ideas/Scratch_2/PingMe_POS_Integration_Strategy.md](../../Ideas/Scratch_2/PingMe_POS_Integration_Strategy.md)
Approved by user: 2026-08-26

## 1. Scope & Non-Goals

This plan delivers the **Level 1 order-integration MVP** from the POS strategy doc: when a customer places an order, PingMe can push it to the venue's existing POS/ERP so staff don't have to manually re-enter it.

**In scope:**
- A new `Integrations` module (Domain/Application/Infrastructure/Api), following the existing folder-based module convention.
- `IPosIntegration` abstraction limited to sending an order — no menu sync, no location sync, no availability polling. Those get their own interface methods introduced in a later plan, alongside a real consumer that needs them.
- `IPosOrderDispatcher` seam between order creation and the integration itself, so the delivery mechanism (inline HTTP today; a queue/outbox later) can change without touching the order-creation code path.
- One concrete, fully working provider: `WebhookPosIntegration` — POSTs a JSON order snapshot to a per-tenant configured URL. Genuinely useful today for any POS with an HTTP receiver.
- Per-tenant opt-in configuration (`TenantPosIntegrationSettings`), managed via an Owner-only admin endpoint — tenants with nothing configured see no behavior change at all.
- Best-effort delivery: a POS push failure never fails the customer's order. Outcome is recorded on the `Order` for staff visibility.

**Non-goals (explicitly deferred, not forgotten):**
- Vendor-specific adapters (Zone Soft, XD Rest, WinRest, PRIMAVERA, SAP, etc.) — no real API access/credentials exist yet for any of them. Each needs its own investigation (per the source doc's Section 12) and becomes its own future plan once a real integration target is confirmed. `ProviderType` is modeled as an enum representing the *provider identity*, not the transport, specifically so adding `ZoneSoft` later doesn't require renaming or restructuring `Webhook`.
- Menu synchronization (Level 2) and fiscal/billing integration (Level 3) from the source doc — later plans.
- Retry logic, background workers, message queues, or an outbox pattern. `IPosOrderDispatcher` is the seam that makes adding these later a contained change, but none of them ship in this plan.
- Webhook destination validation/SSRF hardening. For this plan, `WebhookUrl` accepts any tenant-supplied URL — acceptable because the only realistic users during this phase are the venue's own dev/test endpoint or PingMe's own team validating the feature. **This must be revisited before any production rollout that allows a tenant to self-serve a webhook URL.** Future vendor-specific providers (`ZoneSoft`, etc.) don't carry this exposure since their endpoints are fixed by the adapter, not tenant-supplied.
- Optimistic concurrency on `TenantPosIntegrationSettings` updates — last-write-wins is accepted for a single settings row per tenant.
- Payment integration (source doc Section 9) — unrelated to this plan, explicitly out of scope per the source doc itself.

## 2. Dependency on Plan 3

This plan requires `POST /orders` (Plan 3's order-creation endpoint) to already exist, because `IPosOrderDispatcher.TryDispatchAsync` is called from inside that flow, after the `Order` is persisted. **Plan 5 cannot be executed until Plan 3 is done.** Plan 4 (SignalR + staff dashboard + deployment) has no ordering dependency either way and can be executed before or after Plan 5.

## 3. Architecture

```
OrdersController.Create (Plan 3, extended by Plan 4 with SignalR notification)
      │  order persisted first (existing behavior, unchanged)
      ▼
IPosOrderDispatcher.TryDispatchAsync(order)
      │  catches all exceptions internally; never propagates to the caller
      ▼
IPosIntegrationResolver.Resolve(tenantId)
      │  looks up TenantPosIntegrationSettings for the tenant
      ▼
IPosIntegration.SendOrderAsync(order)
      │
      ▼
WebhookPosIntegration
      │  HTTP POST of the order snapshot to TenantPosIntegrationSettings.WebhookUrl
      ▼
External POS (dev/test endpoint for this plan)
```

Module placement:

```
PingMe.Domain/
 └── Integrations/
      ├── TenantPosIntegrationSettings.cs   (ITenantOwned)
      ├── ProviderType.cs                    (enum: Webhook)
      └── PosDeliveryStatus.cs               (enum: NotConfigured, Pending, Sent, Failed)

PingMe.Application/
 └── Integrations/
      ├── IPosIntegration.cs                 (SendOrderAsync only)
      ├── IPosIntegrationResolver.cs
      └── IPosOrderDispatcher.cs

PingMe.Infrastructure/
 └── Integrations/
      ├── WebhookPosIntegration.cs
      ├── PosIntegrationResolver.cs
      └── PosOrderDispatcher.cs

PingMe.Api/
 └── Controllers/
      └── PosIntegrationSettingsController.cs   (Owner-only, admin/pos-integration)
```

`Order` (Domain/Ordering, already exists from Plan 1) gets one new field: `PosDeliveryStatus`, plus a method to record the outcome after dispatch. The order-creation handler being modified belongs to Plan 3 — this plan's Task list only touches the `Order` entity itself (adding the field) and everything under `Integrations`; the actual call site inside `CreateOrderHandler` is a small, explicit one-line addition documented here so Plan 3's code isn't silently expected to have been written differently — see Section 6.

`IPosIntegration` is intentionally minimal:

```csharp
public interface IPosIntegration
{
    Task SendOrderAsync(Order order, string locationLabel, CancellationToken cancellationToken);
}
```

`IPosOrderDispatcher` is the extensibility seam:

```csharp
public interface IPosOrderDispatcher
{
    Task<PosDeliveryStatus> TryDispatchAsync(Order order, CancellationToken cancellationToken);
}
```

Its implementation resolves the integration, calls `SendOrderAsync`, catches any exception (network failure, non-2xx response, missing/misconfigured settings), logs it, and returns `Failed` rather than throwing. If no settings exist or `IsEnabled` is false, it returns `NotConfigured` without attempting a call.

## 4. Data Model

```csharp
public class TenantPosIntegrationSettings : Entity, ITenantOwned
{
    public Guid TenantId { get; }
    public ProviderType ProviderType { get; }   // Webhook, for this plan
    public string WebhookUrl { get; }
    public bool IsEnabled { get; }
}

public enum ProviderType
{
    Webhook
}

public enum PosDeliveryStatus
{
    NotConfigured,
    Pending,
    Sent,
    Failed
}
```

`Order` gains:

```csharp
public PosDeliveryStatus PosDeliveryStatus { get; private set; } = PosDeliveryStatus.NotConfigured;

public void RecordPosDeliveryStatus(PosDeliveryStatus status)
{
    PosDeliveryStatus = status;
}
```

One `TenantPosIntegrationSettings` row per tenant (enforced by a unique index on `TenantId`, same pattern as other tenant-owned singleton-per-tenant concepts in this codebase).

## 5. API Contract

Owner-only, same pattern as `MenusController`/`ProductsController` from Plan 2:

- `GET /admin/pos-integration` → returns the current tenant's settings, or a `204 No Content` if none configured yet.
- `PUT /admin/pos-integration` → upserts `{ providerType, webhookUrl, isEnabled }` for the current tenant. `providerType` only accepts `"Webhook"` in this plan; any other value is a `400`.

DTOs only — no EF entities cross the boundary, consistent with the rest of the Api layer.

## 6. Order-Creation Integration Point (informational — implemented as part of this plan's tasks, called from `OrdersController.Create`)

Corrected against the actual merged Plan 3/Plan 4 code (no repository abstraction exists — `OrdersController` calls `PingMeDbContext` directly, and Plan 4 already added an `IOrderNotifier` call after the first `SaveChangesAsync`). `OrdersController.Create`'s tail currently reads:

```csharp
_dbContext.Orders.Add(order);
await _dbContext.SaveChangesAsync();

var orderDto = new AdminOrderDto(
    order.Id,
    order.Status.ToString(),
    order.CreatedAt,
    order.Items.Select(i => new AdminOrderItemDto(i.ProductName, i.UnitPrice, i.Quantity)).ToList());
await _orderNotifier.NotifyOrderReceivedAsync(order.TenantId, orderDto);

return Created(string.Empty, new CreateOrderResponse(order.Id, order.Status.ToString()));
```

This plan inserts the POS dispatch **after** the SignalR notification and **before** the `Created` response, with its own second `SaveChangesAsync` to persist the `PosDeliveryStatus` field (the first `SaveChangesAsync` has already assigned `order.Id` and persisted the items, which the dispatcher/payload-builder needs):

```csharp
_dbContext.Orders.Add(order);
await _dbContext.SaveChangesAsync();

var orderDto = new AdminOrderDto(
    order.Id,
    order.Status.ToString(),
    order.CreatedAt,
    order.Items.Select(i => new AdminOrderItemDto(i.ProductName, i.UnitPrice, i.Quantity)).ToList());
await _orderNotifier.NotifyOrderReceivedAsync(order.TenantId, orderDto);

var posDeliveryStatus = await _posOrderDispatcher.TryDispatchAsync(order, HttpContext.RequestAborted);
order.RecordPosDeliveryStatus(posDeliveryStatus);
await _dbContext.SaveChangesAsync();

return Created(string.Empty, new CreateOrderResponse(order.Id, order.Status.ToString()));
```

`OrdersController` gains a fourth constructor dependency, `IPosOrderDispatcher`, alongside the existing `PingMeDbContext`, `CurrentTenantProvider`, and `IOrderNotifier`. This plan's tasks build everything up to and including `IPosOrderDispatcher`; wiring the block above into `OrdersController.Create` is one of this plan's own tasks (not deferred), executed directly against the real merged code since Plan 3 and Plan 4 are both already done and merged (Section 2).

## 7. Webhook Payload Contract

```json
{
  "orderId": "b3a7314e-...",
  "locationLabel": "Table 12",
  "items": [
    { "productId": "...", "name": "Burger", "quantity": 2, "unitPrice": 9.50 },
    { "productId": "...", "name": "Beer", "quantity": 2, "unitPrice": 4.00 }
  ],
  "total": 27.00
}
```

The full order snapshot is sent — the external POS never needs to call back into PingMe to reconstruct order contents. `locationLabel` is resolved via `Order.CustomerSessionId → CustomerSession → Location.Name` (all existing Plan 1 entities).

## 8. Error Handling

- Missing/disabled settings → `TryDispatchAsync` returns `NotConfigured` without an HTTP call. Order creation proceeds identically to a tenant with no POS integration at all.
- Webhook unreachable, times out, or returns a non-2xx status → caught inside `WebhookPosIntegration`/`PosOrderDispatcher`, logged server-side with the underlying exception, `TryDispatchAsync` returns `Failed`. The customer's order is created successfully regardless — staff can always complete the POS entry manually (this is the explicit design goal, not a gap).
- Location lookup for the payload fails (e.g. an inconsistent `CustomerSessionId`) → treated as another `Failed` outcome, same non-blocking guarantee.
- `PUT /admin/pos-integration` with an invalid `providerType` → `400`. Standard `401`/`403` for unauthenticated/non-Owner requests, consistent with Plan 2's controllers.

## 9. Testing Strategy

- `PingMe.UnitTests`: `PosOrderDispatcher` returns `NotConfigured` when no settings exist; returns `Failed` (not a thrown exception) when the underlying `IPosIntegration` throws; returns `Sent` on success. `Order.RecordPosDeliveryStatus` behavior.
- `PingMe.IntegrationTests`:
  - Tenant isolation: Tenant A cannot read or modify Tenant B's `TenantPosIntegrationSettings` via `/admin/pos-integration`.
  - Failure isolation: a webhook endpoint that throws/times out still results in a successfully created order with `PosDeliveryStatus = Failed`.
  - Correct payload: a test webhook receiver asserts the exact JSON shape from Section 7 for a multi-item order.
  - `NotConfigured` path: a tenant with no settings configured places an order successfully with `PosDeliveryStatus = NotConfigured`.
  - `Unauthenticated request to /admin/pos-integration returns 401` (matches the pattern of Plan 2's isolation test suite).

  These tests depend on Plan 3's `POST /orders` endpoint existing (Section 2) and will be written against it directly, not against a hypothetical handler.

## 10. Failure-Mode Check (adversarial pass)

| Failure mode | Severity | Resolution |
|---|---|---|
| Tenant-supplied `WebhookUrl` enables SSRF against internal infrastructure | Critical for production, acceptable for this MVP phase | Documented as a required pre-production hardening step in Non-Goals (Section 1) — not silently ignored, explicitly flagged as a blocker before any self-serve production rollout |
| Swallowing dispatch exceptions could mask a real bug, not just a POS outage | Minor | Failure is logged server-side with the full exception and recorded as a visible `PosDeliveryStatus.Failed` on the order — observable, just non-blocking to the customer |
| `CustomerSession`/`Location` state is inconsistent when building the payload | Minor | Treated as another `Failed` outcome; no new failure path introduced for the customer-facing order flow |
| Concurrent updates to `TenantPosIntegrationSettings` race | Minor | Last-write-wins accepted for a single per-tenant settings row; documented non-goal, no optimistic concurrency needed at this scale |

No critical unresolved failure modes remain.

## 11. Next Step

Proceed to `writing-plans` to break this design into an ordered, verifiable implementation plan (Plan 5) once this spec is reviewed and approved. The plan's first task should explicitly confirm Plan 3 is complete/merged before proceeding, per Section 2.
