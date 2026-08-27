# Plan 4 Design: SignalR + Staff Dashboard + Local Deployment

## 1. Scope & Non-Goals

**In scope:**
- A SignalR hub broadcasting real-time order events to authenticated staff/owners, scoped per tenant.
- A new staff-facing React dashboard app for viewing and transitioning orders live.
- Local Docker Compose setup for the API + PostgreSQL, for one-command backend/DB spin-up.

**Non-goals (explicit):**
- The customer-facing app (`customer-app`, Plan 3) does not get SignalR in this plan. It keeps its existing 5-second polling of `GET /orders/{id}/status`. This is a smaller-blast-radius choice — it avoids touching already-merged, already-tested Plan 3 frontend code, and polling is already an accepted fallback per the original MVP spec's Section 8.
- No cloud deployment target and no CI pipeline. "Deployment" here means local Docker Compose only.
- No SignalR message retry, outbox, or persistence. A broadcast with no connected staff is not re-delivered — the dashboard's initial fetch-then-connect sequence (Section 3) and its polling fallback on disconnect are the sole recovery mechanisms.
- No JWT refresh/reconnect-on-expiry strategy for long-lived SignalR connections (Section 5, failure mode 2).
- Frontend apps (`customer-app`, `staff-app`) are not containerized — Docker Compose covers the API + Postgres only; both frontends keep running via `pnpm dev` for Vite's HMR.

## 2. Architecture — SignalR Hub

**New file:** `src/PingMe.Api/Realtime/OrdersHub.cs`

```csharp
namespace PingMe.Api.Realtime;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

[Authorize(Roles = "Owner,Staff")]
public class OrdersHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst("tenantId")?.Value;
        if (tenantId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant-{tenantId}");
        }

        await base.OnConnectedAsync();
    }
}
```

Registered at route `/hubs/orders` via `app.MapHub<OrdersHub>("/hubs/orders")` in `Program.cs`, alongside `builder.Services.AddSignalR()`.

**JWT-over-WebSocket auth:** browsers cannot set the `Authorization` header on a WebSocket upgrade, so the SignalR JS client passes the token as a query string parameter (`?access_token=...`). This requires one addition to the existing `AddJwtBearer` configuration in `Program.cs`:

```csharp
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters { /* unchanged */ };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(accessToken) &&
                context.HttpContext.Request.Path.StartsWithSegments("/hubs/orders"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});
```

This reuses the exact same `tenantId`/`role` claims already issued by `JwtTokenGenerator` and read by `TenantResolutionMiddleware` — no new claim types, no new auth model.

**Notification seam:** new interface in `PingMe.Application.Ordering`:

```csharp
namespace PingMe.Application.Ordering;

using PingMe.Api.Contracts.Ordering; // AdminOrderDto lives in Api today; see note below

public interface IOrderNotifier
{
    Task NotifyOrderReceivedAsync(Guid tenantId, AdminOrderDto order);
    Task NotifyOrderStatusChangedAsync(Guid tenantId, AdminOrderDto order);
}
```

> **Note on the DTO reference:** `AdminOrderDto` currently lives in `PingMe.Api.Contracts.Ordering`, but `PingMe.Application` cannot reference `PingMe.Api` (wrong direction for the layered architecture). The implementation plan must either (a) move `AdminOrderDto`/`AdminOrderItemDto` down into `PingMe.Application.Ordering` and have the Api project reference them from there (controllers already just return them as-is, so this is a mechanical move), or (b) define a small notification-only payload type in `Application` and map to it. Option (a) is simpler and removes a duplicate-type risk — the implementation plan should do (a).

Implementation, in `PingMe.Api`:

```csharp
namespace PingMe.Api.Realtime;

using Microsoft.AspNetCore.SignalR;
using PingMe.Application.Ordering;

public class SignalROrderNotifier : IOrderNotifier
{
    private readonly IHubContext<OrdersHub> _hubContext;
    private readonly ILogger<SignalROrderNotifier> _logger;

    public SignalROrderNotifier(IHubContext<OrdersHub> hubContext, ILogger<SignalROrderNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyOrderReceivedAsync(Guid tenantId, AdminOrderDto order)
    {
        try
        {
            await _hubContext.Clients.Group($"tenant-{tenantId}").SendAsync("OrderReceived", order);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast OrderReceived for order {OrderId}", order.Id);
        }
    }

    public async Task NotifyOrderStatusChangedAsync(Guid tenantId, AdminOrderDto order)
    {
        try
        {
            await _hubContext.Clients.Group($"tenant-{tenantId}").SendAsync("OrderStatusChanged", order);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast OrderStatusChanged for order {OrderId}", order.Id);
        }
    }
}
```

A failed broadcast is logged and swallowed — it must never fail the HTTP request that triggered it, matching the same "don't let a downstream integration break the customer-facing response" principle already established for Plan 5's POS dispatcher.

**Call sites:**
- `OrdersController.Create` — after `SaveChangesAsync()` succeeds, call `NotifyOrderReceivedAsync(session.TenantId, <mapped AdminOrderDto>)`.
- `AdminOrdersController.UpdateStatus` — after `SaveChangesAsync()` succeeds, call `NotifyOrderStatusChangedAsync(_currentTenantProvider.TenantId!.Value, <mapped AdminOrderDto>)`.

Both controllers already have the order and its items in scope at that point; building the `AdminOrderDto` is a direct mapping identical to `AdminOrdersController.GetOrders`'s existing projection.

## 3. Architecture — Staff Dashboard (`src/pingme-web/staff-app`)

New Vite + React 18 + TypeScript + Vitest app in the existing pnpm workspace (add `staff-app` to `src/pingme-web/pnpm-workspace.yaml`'s `packages` list), mirroring `customer-app`'s `package.json`/`tsconfig.json`/`tsconfig.node.json`/`vite.config.ts`/`vite-env.d.ts` conventions exactly (including the `skipLibCheck` and `vite-env.d.ts` fixes already discovered during Plan 3).

**Files:**
- `src/App.tsx` — top-level: login gate → dashboard.
- `src/api.ts` — `login(email, password)`, `getOrders()`, `updateOrderStatus(orderId, status)`, all attaching `Authorization: Bearer <token>`.
- `src/signalr.ts` — thin wrapper around `@microsoft/signalr`'s `HubConnectionBuilder` connecting to `/hubs/orders?access_token={token}`, exposing `onOrderReceived`/`onOrderStatusChanged` callback registration and `onclose`/`onreconnected` hooks.
- `src/components/OrderList.tsx` — renders the current order list, one row per order with a "next status" action button.
- `src/types.ts` — `AdminOrderDto`/`AdminOrderItemDto` mirrored from the backend contracts (same pattern as `customer-app/src/types.ts`).

**Connect-then-fetch sequencing (resolves failure mode 1, Section 5):**

```ts
// App.tsx, after login succeeds:
const connection = await connectOrdersHub(token); // await full connection start
const initialOrders = await getOrders();          // fetch AFTER the hub connection is live
setOrders(initialOrders);

connection.on("OrderReceived", (order: AdminOrderDto) => {
  setOrders((current) => upsertById(current, order));
});
connection.on("OrderStatusChanged", (order: AdminOrderDto) => {
  setOrders((current) => upsertById(current, order));
});
```

`upsertById` replaces the order if its `id` already exists, otherwise prepends it — so an order that arrives via broadcast during/after the initial fetch never duplicates.

**Reconnect/polling fallback:** on `connection.onclose`, start a 10-second polling interval calling `getOrders()` and replacing the full list; on `connection.onreconnected`, stop polling and resume relying on broadcasts (mirrors the customer app's already-established polling-as-fallback pattern from Plan 3's `OrderStatus.tsx`).

**Status transitions:** each order row's action button maps to the *next* status in the fixed sequence `Received → Accepted → Preparing → Ready → Delivered` (same sequence `Order.TransitionTo` already enforces server-side); calls `PUT /admin/orders/{id}/status`, and a `409` response shows an inline "couldn't update — try refreshing" message rather than attempting client-side rollback, since the next broadcast or poll will resync the real state regardless. An order already at `Delivered` (the terminal status) shows no action button — there is no next status to transition to.

## 4. Architecture — Local Docker Compose

**New file:** `src/PingMe.Api/Dockerfile` — multi-stage build:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/PingMe.Api/PingMe.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "PingMe.Api.dll"]
```

**New file:** `docker-compose.yml` at the repo root:

```yaml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: pingme_dev
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "55432:5432"
    volumes:
      - pingme-postgres-data:/var/lib/postgresql/data

  api:
    build:
      context: .
      dockerfile: src/PingMe.Api/Dockerfile
    depends_on:
      - postgres
    ports:
      - "5190:8080"
    environment:
      ConnectionStrings__PingMe: "Host=postgres;Port=5432;Database=pingme_dev;Username=postgres;Password=postgres"
      Jwt__Key: "dev-only-signing-key-change-before-production-1234567890"
      Jwt__Issuer: "PingMe"
      Jwt__Audience: "PingMeAdmin"
      Jwt__ExpiryMinutes: "60"

volumes:
  pingme-postgres-data:
```

Postgres is mapped to host port `55432` (not the native `5432` already used by the existing local dev Postgres instance) to avoid a port collision between the two workflows (resolves failure mode 3, Section 5). EF Core migrations still run via `dotnet ef database update` against whichever Postgres instance is targeted — this plan does not add automatic migration-on-startup, matching the "no hand-written SQL, EF Core Code-First" convention already established, applied manually as in local dev today.

## 5. Failure-Mode Check (adversarial pass)

| Failure mode | Severity | Resolution |
|---|---|---|
| Staff dashboard fetches `GET /admin/orders` and connects to SignalR concurrently — an order created in that window could appear via both, duplicating it in the list | Critical for this feature | Resolved — connect to the hub first and await it, fetch orders second, and key all updates on order `id` via upsert (Section 3) |
| SignalR connection stays authenticated past the JWT's nominal expiry (no re-validation after handshake) | Minor | Documented non-goal — no refresh/reconnect-on-expiry logic this plan; acceptable since dashboard sessions are short-lived and the connection grants no capability beyond the original token's |
| Docker Compose's Postgres container collides with the native Postgres already used for local dev on port 5432 | Minor | Resolved — compose maps to host port `55432` instead (Section 4) |

## 6. Testing Strategy

- **Backend:** integration test verifying `OrdersHub.OnConnectedAsync` adds the connection to the correct tenant group (using SignalR's `TestHubConnection`/in-memory test server pattern already compatible with `PingMeWebApplicationFactory`). Integration test verifying a Staff/Owner JWT from Tenant A cannot receive broadcasts intended for Tenant B (two hub connections, two tenants, assert cross-delivery does not happen) — this is the SignalR-specific instance of the same tenant-isolation rigor already applied throughout Plans 1-3.
- **Frontend:** component/unit test for `upsertById` (the dedup logic from Section 3) covering: new order appended, existing order replaced in place, order count never grows from a duplicate event.
- **No end-to-end SignalR browser test** in this plan — matches Plan 3's precedent of curl/API-level verification plus a manual browser check as the practical ceiling without a headless browser tool.

## 7. Rollout Notes

- `AdminOrderDto`/`AdminOrderItemDto` move from `PingMe.Api.Contracts.Ordering` to `PingMe.Application.Ordering` (Section 2) — a mechanical relocation; `AdminOrdersController`'s existing usage becomes a `using` change, no behavior change.
- Docker Compose is additive — does not replace or change the existing local dev setup (native Postgres on 5432, `dotnet run`, `pnpm dev`); it is an alternative, opt-in path.
