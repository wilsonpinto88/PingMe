# Plan 5 Implementation Plan: POS/ERP Integration (Level 1 Order-Push MVP)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-optimized:subagent-driven-development (recommended) or superpowers-optimized:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a customer places an order, best-effort push it to the venue's configured POS via a webhook, without ever blocking or failing the customer's order.

**Architecture:** A new `Integrations` module (Domain/Application/Infrastructure/Api) adds `TenantPosIntegrationSettings` (one row per tenant, Owner-managed), an `IPosIntegration` abstraction (`SendOrderAsync` only) with one concrete `WebhookPosIntegration`, and an `IPosOrderDispatcher` seam that `OrdersController.Create` calls after its existing SignalR notification. The dispatcher resolves the tenant's integration, calls it, catches every exception, and records the outcome as `Order.PosDeliveryStatus` — `NotConfigured`, `Sent`, or `Failed`. Nothing in this path can ever fail the customer-facing `POST /orders` call.

**Tech Stack:** C#/.NET 10 (Domain/Application/Infrastructure/Api layering already established), EF Core + PostgreSQL, xUnit (no mocking library — this codebase uses hand-written fakes and a stub `HttpMessageHandler`, matching its existing test conventions).

**Assumptions:**
- Assumes Plans 1-4 are merged to main (verified before writing this plan) — will NOT work against an unmerged `OrdersController`/`AdminOrdersController`/`AdminOrderDto` since this plan's tasks edit their exact current post-Plan-4 content.
- Assumes only one provider (`Webhook`) exists — will NOT need a provider-selection UI beyond a single enum value; adding `ZoneSoft` etc. later is out of scope (spec Non-Goals).
- Assumes `AdminOrderDto` gaining a `PosDeliveryStatus` field is backend-only for this plan — will NOT update `staff-app`'s TypeScript types or UI to display it; the extra JSON field is harmless to the existing frontend (it doesn't destructure strictly) but isn't shown anywhere yet.
- Assumes no automated test asserts a byte-for-byte webhook payload over a real socket — will NOT stand up a live `HttpListener` for the "correct payload shape" assertion (see Task 5's deviation note); that assertion is a fast unit test against a stub `HttpMessageHandler` instead, since this codebase has no OS-level HTTP-listener test precedent and a stub handler proves the exact same thing without the platform risk.
- Assumes `IPosOrderDispatcher.TryDispatchAsync` takes `locationLabel` as a parameter rather than re-deriving it from `order.CustomerSessionId` internally (a deliberate, documented refinement of the spec's Section 3 interface sketch — see Task 4's note) — will NOT need `PosOrderDispatcher` to depend on `PingMeDbContext` at all, keeping it fully unit-testable with hand-written fakes.

---

## Task 1: Add Order.PosDeliveryStatus and extend AdminOrderDto

**Files:**
- Create: `src/PingMe.Domain/Integrations/PosDeliveryStatus.cs`
- Modify: `src/PingMe.Domain/Ordering/Order.cs`
- Modify: `src/PingMe.Application/Ordering/AdminOrderDto.cs`
- Modify: `src/PingMe.Api/Controllers/OrdersController.cs`
- Modify: `src/PingMe.Api/Controllers/AdminOrdersController.cs`
- Test: `tests/PingMe.UnitTests/Ordering/OrderTransitionTests.cs`

**Does NOT cover:** any actual POS dispatch logic (Tasks 4-6) or wiring the dispatcher into `OrdersController` (Task 9) — this task only adds the field, the DTO shape, and keeps existing DTO-construction call sites compiling with a `NotConfigured` default.

- [ ] **Step 1: Write a failing test**

Add this fact to `tests/PingMe.UnitTests/Ordering/OrderTransitionTests.cs` (inside the existing `OrderTransitionTests` class, alongside the existing facts — do not remove any):

```csharp
    [Fact]
    public void New_order_starts_with_NotConfigured_pos_delivery_status()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        Assert.Equal(PosDeliveryStatus.NotConfigured, order.PosDeliveryStatus);
    }

    [Fact]
    public void RecordPosDeliveryStatus_updates_the_status()
    {
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        order.RecordPosDeliveryStatus(PosDeliveryStatus.Sent);

        Assert.Equal(PosDeliveryStatus.Sent, order.PosDeliveryStatus);
    }
```

Add `using PingMe.Domain.Integrations;` to the top of `tests/PingMe.UnitTests/Ordering/OrderTransitionTests.cs` (alongside the existing `using PingMe.Domain.Ordering;`).

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test PingMe.slnx --filter "FullyQualifiedName~OrderTransitionTests"`
Expected: FAIL to compile — `PingMe.Domain.Integrations` namespace and `Order.PosDeliveryStatus`/`RecordPosDeliveryStatus` don't exist yet.

- [ ] **Step 3: Create the `PosDeliveryStatus` enum**

`src/PingMe.Domain/Integrations/PosDeliveryStatus.cs`:
```csharp
namespace PingMe.Domain.Integrations;

public enum PosDeliveryStatus
{
    NotConfigured,
    Pending,
    Sent,
    Failed
}
```

`Pending` is intentionally unused by this plan — reserved for a future async/queued dispatch mechanism (spec Non-Goals) so that value doesn't need a breaking enum change later.

- [ ] **Step 4: Add the field and method to `Order`**

In `src/PingMe.Domain/Ordering/Order.cs`, add the using:
```csharp
using PingMe.Domain.Integrations;
```

Add the property (alongside the existing `Status`/`CreatedAt` properties):
```csharp
public PosDeliveryStatus PosDeliveryStatus { get; private set; } = PosDeliveryStatus.NotConfigured;
```

Add the method (alongside `TransitionTo`):
```csharp
public void RecordPosDeliveryStatus(PosDeliveryStatus status)
{
    PosDeliveryStatus = status;
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test PingMe.slnx --filter "FullyQualifiedName~OrderTransitionTests"`
Expected: PASS — all 6 facts in the file green (4 existing + 2 new).

- [ ] **Step 6: Extend `AdminOrderDto` and update its two existing construction call sites**

In `src/PingMe.Application/Ordering/AdminOrderDto.cs`, change:
```csharp
public record AdminOrderDto(Guid Id, string Status, DateTime CreatedAt, List<AdminOrderItemDto> Items);
```
to:
```csharp
public record AdminOrderDto(Guid Id, string Status, DateTime CreatedAt, List<AdminOrderItemDto> Items, string PosDeliveryStatus);
```

In `src/PingMe.Api/Controllers/OrdersController.cs`, change the `orderDto` construction inside `Create`:
```csharp
        var orderDto = new AdminOrderDto(
            order.Id,
            order.Status.ToString(),
            order.CreatedAt,
            order.Items.Select(i => new AdminOrderItemDto(i.ProductName, i.UnitPrice, i.Quantity)).ToList());
```
to:
```csharp
        var orderDto = new AdminOrderDto(
            order.Id,
            order.Status.ToString(),
            order.CreatedAt,
            order.Items.Select(i => new AdminOrderItemDto(i.ProductName, i.UnitPrice, i.Quantity)).ToList(),
            order.PosDeliveryStatus.ToString());
```
(At this point in the method `order.PosDeliveryStatus` is still `NotConfigured` since dispatch hasn't happened yet — that's correct for this broadcast, which fires before Task 9 adds the dispatch call.)

In `src/PingMe.Api/Controllers/AdminOrdersController.cs`, update **both** `AdminOrderDto` construction call sites (`GetOrders`'s projection and `UpdateStatus`'s `orderDto` build) the same way, appending `o.PosDeliveryStatus.ToString()` / `order.PosDeliveryStatus.ToString()` as the fifth constructor argument respectively.

- [ ] **Step 7: Run the full backend build and test suite**

Run: `dotnet build PingMe.slnx && dotnet test PingMe.slnx`
Expected: PASS — 0 build errors, all 46 tests green (44 existing + 2 new).

- [ ] **Step 8: Commit**

```bash
git add src/PingMe.Domain/Integrations/PosDeliveryStatus.cs src/PingMe.Domain/Ordering/Order.cs src/PingMe.Application/Ordering/AdminOrderDto.cs src/PingMe.Api/Controllers/OrdersController.cs src/PingMe.Api/Controllers/AdminOrdersController.cs tests/PingMe.UnitTests/Ordering/OrderTransitionTests.cs
git commit -m "Add Order.PosDeliveryStatus and expose it on AdminOrderDto"
```

---

## Task 2: Add TenantPosIntegrationSettings entity and ProviderType enum

**Files:**
- Create: `src/PingMe.Domain/Integrations/ProviderType.cs`
- Create: `src/PingMe.Domain/Integrations/TenantPosIntegrationSettings.cs`

**Does NOT cover:** EF Core mapping/migration (Task 3) or any controller (Task 8) — this task only adds the domain entity.

- [ ] **Step 1: Create the `ProviderType` enum**

`src/PingMe.Domain/Integrations/ProviderType.cs`:
```csharp
namespace PingMe.Domain.Integrations;

public enum ProviderType
{
    Webhook
}
```

`ProviderType` represents provider *identity* (which POS), not transport — future values like `ZoneSoft`/`Primavera`/`WinRest` slot in here later without restructuring, per the spec's explicit design goal.

- [ ] **Step 2: Create the `TenantPosIntegrationSettings` entity**

`src/PingMe.Domain/Integrations/TenantPosIntegrationSettings.cs`:
```csharp
namespace PingMe.Domain.Integrations;

using PingMe.Domain.Common;

public class TenantPosIntegrationSettings : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public ProviderType ProviderType { get; private set; }
    public string WebhookUrl { get; private set; } = default!;
    public bool IsEnabled { get; private set; }

    private TenantPosIntegrationSettings() { }

    public TenantPosIntegrationSettings(Guid tenantId, ProviderType providerType, string webhookUrl, bool isEnabled)
    {
        TenantId = tenantId;
        ProviderType = providerType;
        WebhookUrl = webhookUrl;
        IsEnabled = isEnabled;
    }

    public void UpdateSettings(ProviderType providerType, string webhookUrl, bool isEnabled)
    {
        ProviderType = providerType;
        WebhookUrl = webhookUrl;
        IsEnabled = isEnabled;
    }
}
```

This follows the same `Entity`/`ITenantOwned`, private-setter-plus-explicit-method pattern as `Order`, `Location`, and `QrCode`.

- [ ] **Step 3: Build**

Run: `dotnet build PingMe.slnx`
Expected: PASS — 0 build errors (no consumers yet; nothing references these new types outside `PingMe.Domain`).

- [ ] **Step 4: Commit**

```bash
git add src/PingMe.Domain/Integrations/ProviderType.cs src/PingMe.Domain/Integrations/TenantPosIntegrationSettings.cs
git commit -m "Add TenantPosIntegrationSettings entity and ProviderType enum"
```

---

## Task 3: Map TenantPosIntegrationSettings in EF Core with a per-tenant unique index

**Files:**
- Modify: `src/PingMe.Infrastructure/Persistence/PingMeDbContext.cs`
- Create (via `dotnet ef`): a new migration under `src/PingMe.Infrastructure/Persistence/Migrations/`

**Does NOT cover:** any controller reading/writing this table (Task 8).

- [ ] **Step 1: Add the DbSet and unique index**

In `src/PingMe.Infrastructure/Persistence/PingMeDbContext.cs`, add the using:
```csharp
using PingMe.Domain.Integrations;
```

Add the `DbSet` (alongside the existing ones):
```csharp
public DbSet<TenantPosIntegrationSettings> TenantPosIntegrationSettings => Set<TenantPosIntegrationSettings>();
```

Add the unique index in `OnModelCreating`, after the existing `QrCode.Code` unique index:
```csharp
        modelBuilder.Entity<TenantPosIntegrationSettings>()
            .HasIndex(t => t.TenantId)
            .IsUnique();
```

Note: the automatic `ApplyTenantFilter<TEntity>` loop later in the same method also calls `HasIndex(e => e.TenantId)` (non-unique) for every `ITenantOwned` entity, including this one. EF Core's fluent API resolves multiple `HasIndex` calls on the same property set to the same underlying index — since only this explicit call sets `.IsUnique()`, the resulting index stays unique regardless of the loop also touching it. This mirrors how `QrCode.Code`'s explicit unique index coexists with the same loop.

- [ ] **Step 2: Generate the migration**

Run: `dotnet ef migrations add AddTenantPosIntegrationSettings --project src/PingMe.Infrastructure --startup-project src/PingMe.Api`
Expected: succeeds, creating a new migration file that adds the `TenantPosIntegrationSettings` table with a unique index on `TenantId`.

- [ ] **Step 3: Build and run the full backend test suite**

Run: `dotnet build PingMe.slnx && dotnet test PingMe.slnx`
Expected: PASS — 0 build errors, all 46 tests green (the integration test factory runs `Database.Migrate()` against `pingme_test`, so the new migration is exercised automatically).

- [ ] **Step 4: Commit**

```bash
git add src/PingMe.Infrastructure/Persistence/PingMeDbContext.cs src/PingMe.Infrastructure/Persistence/Migrations/
git commit -m "Map TenantPosIntegrationSettings with a per-tenant unique index"
```

---

## Task 4: Add the Application-layer Integrations interfaces

**Files:**
- Create: `src/PingMe.Application/Integrations/IPosIntegration.cs`
- Create: `src/PingMe.Application/Integrations/IPosIntegrationResolver.cs`
- Create: `src/PingMe.Application/Integrations/IPosOrderDispatcher.cs`

**Does NOT cover:** any implementation (Tasks 5-6) — this task only adds the seams.

**Deviation from the spec's Section 3 interface sketch (documented, not silent):** the spec shows `IPosOrderDispatcher.TryDispatchAsync(Order order, CancellationToken cancellationToken)` with no `locationLabel` parameter, implying the dispatcher itself would re-derive the location from `order.CustomerSessionId` via a `CustomerSession`/`Location` lookup. That would force `PosOrderDispatcher` (Task 6) to depend on `PingMeDbContext` directly, which breaks the spec's own stated unit-testing strategy (Section 9: pure `NotConfigured`/`Failed`/`Sent` control-flow tests, no database). Instead, `TryDispatchAsync` takes `locationLabel` as a parameter — the caller (`OrdersController.Create`, Task 9) already has the location resolved cheaply from its own in-scope `session`/`Location` lookup, so no extra query is needed either way, and `PosOrderDispatcher` becomes fully unit-testable with hand-written fakes and zero EF dependency.

- [ ] **Step 1: Create `IPosIntegration`**

`src/PingMe.Application/Integrations/IPosIntegration.cs`:
```csharp
namespace PingMe.Application.Integrations;

using PingMe.Domain.Ordering;

public interface IPosIntegration
{
    Task SendOrderAsync(Order order, string locationLabel, CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Create `IPosIntegrationResolver`**

`src/PingMe.Application/Integrations/IPosIntegrationResolver.cs`:
```csharp
namespace PingMe.Application.Integrations;

public interface IPosIntegrationResolver
{
    Task<IPosIntegration?> ResolveAsync(Guid tenantId, CancellationToken cancellationToken);
}
```

Returns `null` when the tenant has no settings row, or `IsEnabled` is `false` — the caller treats a `null` result as `NotConfigured`.

- [ ] **Step 3: Create `IPosOrderDispatcher`**

`src/PingMe.Application/Integrations/IPosOrderDispatcher.cs`:
```csharp
namespace PingMe.Application.Integrations;

using PingMe.Domain.Integrations;
using PingMe.Domain.Ordering;

public interface IPosOrderDispatcher
{
    Task<PosDeliveryStatus> TryDispatchAsync(Order order, string locationLabel, CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Build**

Run: `dotnet build PingMe.slnx`
Expected: PASS — 0 build errors.

- [ ] **Step 5: Commit**

```bash
git add src/PingMe.Application/Integrations/IPosIntegration.cs src/PingMe.Application/Integrations/IPosIntegrationResolver.cs src/PingMe.Application/Integrations/IPosOrderDispatcher.cs
git commit -m "Add IPosIntegration, IPosIntegrationResolver, IPosOrderDispatcher interfaces"
```

---

## Task 5: Implement WebhookPosIntegration with a payload-shape unit test

**Files:**
- Create: `src/PingMe.Infrastructure/Integrations/WebhookPosIntegration.cs`
- Test: `tests/PingMe.UnitTests/Integrations/WebhookPosIntegrationTests.cs`
- Modify: `tests/PingMe.UnitTests/PingMe.UnitTests.csproj`

**Does NOT cover:** resolving settings or catching dispatch failures (Task 6) — this class only knows how to send, given a URL and an `HttpClient` it's handed.

**Deviation from the spec's Section 9 testing strategy (documented):** the spec lists the exact-JSON-shape assertion under `PingMe.IntegrationTests`, implying a live webhook receiver. This plan instead verifies it as a fast `PingMe.UnitTests` test against a stub `HttpMessageHandler` that captures the outgoing request without touching a real socket — this proves the exact same thing (the JSON body `WebhookPosIntegration` sends) without depending on OS-level HTTP-listener behavior, which has no precedent elsewhere in this codebase's test suite.

- [ ] **Step 1: Add a project reference so unit tests can construct `HttpClient`/`HttpMessageHandler`**

`System.Net.Http` types are part of the base class library (no new package needed) — `PingMe.UnitTests.csproj` doesn't need a new `PackageReference` for this task. Skip this step's file changes; it exists only to confirm no csproj edit is needed here (the csproj edit for Task 6's fakes comes later).

- [ ] **Step 2: Write a failing test**

`tests/PingMe.UnitTests/Integrations/WebhookPosIntegrationTests.cs`:
```csharp
namespace PingMe.UnitTests.Integrations;

using System.Net;
using System.Text.Json;
using PingMe.Domain.Ordering;
using Xunit;

public class WebhookPosIntegrationTests
{
    private class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? CapturedRequest { get; private set; }
        public string? CapturedBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            CapturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Posts_the_order_snapshot_as_camelCase_JSON_to_the_configured_url()
    {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler);
        var integration = new PingMe.Infrastructure.Integrations.WebhookPosIntegration(
            "http://example.invalid/webhook", httpClient);

        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        order.AddItem(Guid.NewGuid(), "Burger", 9.50m, 2);
        order.AddItem(Guid.NewGuid(), "Beer", 4.00m, 2);

        await integration.SendOrderAsync(order, "Table 12", CancellationToken.None);

        Assert.NotNull(handler.CapturedRequest);
        Assert.Equal(HttpMethod.Post, handler.CapturedRequest!.Method);
        Assert.Equal("http://example.invalid/webhook", handler.CapturedRequest.RequestUri!.ToString());

        using var payload = JsonDocument.Parse(handler.CapturedBody!);
        var root = payload.RootElement;
        Assert.Equal(order.Id, root.GetProperty("orderId").GetGuid());
        Assert.Equal("Table 12", root.GetProperty("locationLabel").GetString());
        Assert.Equal(27.00m, root.GetProperty("total").GetDecimal());

        var items = root.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("Burger", items[0].GetProperty("name").GetString());
        Assert.Equal(2, items[0].GetProperty("quantity").GetInt32());
        Assert.Equal(9.50m, items[0].GetProperty("unitPrice").GetDecimal());
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test PingMe.slnx --filter "FullyQualifiedName~WebhookPosIntegrationTests"`
Expected: FAIL to compile — `PingMe.Infrastructure.Integrations.WebhookPosIntegration` doesn't exist yet, and `PingMe.UnitTests` doesn't reference `PingMe.Infrastructure` yet.

- [ ] **Step 4: Add the `PingMe.Infrastructure` project reference to the unit test project**

In `tests/PingMe.UnitTests/PingMe.UnitTests.csproj`, change:
```xml
  <ItemGroup>
    <ProjectReference Include="..\..\src\PingMe.Domain\PingMe.Domain.csproj" />
  </ItemGroup>
```
to:
```xml
  <ItemGroup>
    <ProjectReference Include="..\..\src\PingMe.Domain\PingMe.Domain.csproj" />
    <ProjectReference Include="..\..\src\PingMe.Application\PingMe.Application.csproj" />
    <ProjectReference Include="..\..\src\PingMe.Infrastructure\PingMe.Infrastructure.csproj" />
  </ItemGroup>
```

This pulls in EF Core/Npgsql packages transitively (via `PingMe.Infrastructure`), but none of this plan's unit tests open a real database connection — they only construct plain C# objects.

- [ ] **Step 5: Implement `WebhookPosIntegration`**

`src/PingMe.Infrastructure/Integrations/WebhookPosIntegration.cs`:
```csharp
namespace PingMe.Infrastructure.Integrations;

using System.Net.Http.Json;
using System.Text.Json;
using PingMe.Application.Integrations;
using PingMe.Domain.Ordering;

public class WebhookPosIntegration : IPosIntegration
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _webhookUrl;
    private readonly HttpClient _httpClient;

    public WebhookPosIntegration(string webhookUrl, HttpClient httpClient)
    {
        _webhookUrl = webhookUrl;
        _httpClient = httpClient;
    }

    public async Task SendOrderAsync(Order order, string locationLabel, CancellationToken cancellationToken)
    {
        var payload = new WebhookOrderPayload(
            order.Id,
            locationLabel,
            order.Items
                .Select(i => new WebhookOrderItemPayload(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice))
                .ToList(),
            order.Items.Sum(i => i.UnitPrice * i.Quantity));

        var response = await _httpClient.PostAsJsonAsync(_webhookUrl, payload, PayloadJsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private record WebhookOrderPayload(Guid OrderId, string LocationLabel, List<WebhookOrderItemPayload> Items, decimal Total);

    private record WebhookOrderItemPayload(Guid ProductId, string Name, int Quantity, decimal UnitPrice);
}
```

`JsonSerializerDefaults.Web` sets camelCase property naming — this is what makes the outgoing JSON match the spec's Section 7 contract (`orderId`, `locationLabel`, `items[].productId/name/quantity/unitPrice`, `total`) despite the C# record using PascalCase property names.

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test PingMe.slnx --filter "FullyQualifiedName~WebhookPosIntegrationTests"`
Expected: PASS.

- [ ] **Step 7: Run the full backend test suite**

Run: `dotnet test PingMe.slnx`
Expected: PASS — all 47 tests green (46 existing + 1 new).

- [ ] **Step 8: Commit**

```bash
git add src/PingMe.Infrastructure/Integrations/WebhookPosIntegration.cs tests/PingMe.UnitTests/Integrations/WebhookPosIntegrationTests.cs tests/PingMe.UnitTests/PingMe.UnitTests.csproj
git commit -m "Add WebhookPosIntegration with a payload-shape unit test"
```

---

## Task 6: Implement PosIntegrationResolver and PosOrderDispatcher

**Files:**
- Create: `src/PingMe.Infrastructure/Integrations/PosIntegrationResolver.cs`
- Create: `src/PingMe.Infrastructure/Integrations/PosOrderDispatcher.cs`
- Test: `tests/PingMe.UnitTests/Integrations/PosOrderDispatcherTests.cs`

**Does NOT cover:** DI registration (Task 7) or the `HttpClient` factory setup for `WebhookPosIntegration` instances (also Task 7) — this task only implements the two classes.

- [ ] **Step 1: Write failing tests**

`tests/PingMe.UnitTests/Integrations/PosOrderDispatcherTests.cs`:
```csharp
namespace PingMe.UnitTests.Integrations;

using Microsoft.Extensions.Logging.Abstractions;
using PingMe.Application.Integrations;
using PingMe.Domain.Integrations;
using PingMe.Domain.Ordering;
using PingMe.Infrastructure.Integrations;
using Xunit;

public class PosOrderDispatcherTests
{
    private class FakeResolver : IPosIntegrationResolver
    {
        private readonly IPosIntegration? _integration;

        public FakeResolver(IPosIntegration? integration)
        {
            _integration = integration;
        }

        public Task<IPosIntegration?> ResolveAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(_integration);
    }

    private class FakeIntegration : IPosIntegration
    {
        private readonly Exception? _exceptionToThrow;

        public FakeIntegration(Exception? exceptionToThrow = null)
        {
            _exceptionToThrow = exceptionToThrow;
        }

        public Task SendOrderAsync(Order order, string locationLabel, CancellationToken cancellationToken)
        {
            if (_exceptionToThrow is not null)
            {
                throw _exceptionToThrow;
            }

            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Returns_NotConfigured_when_no_integration_is_resolved()
    {
        var dispatcher = new PosOrderDispatcher(new FakeResolver(null), NullLogger<PosOrderDispatcher>.Instance);
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        var status = await dispatcher.TryDispatchAsync(order, "Table 1", CancellationToken.None);

        Assert.Equal(PosDeliveryStatus.NotConfigured, status);
    }

    [Fact]
    public async Task Returns_Failed_when_the_integration_throws()
    {
        var dispatcher = new PosOrderDispatcher(
            new FakeResolver(new FakeIntegration(new InvalidOperationException("boom"))),
            NullLogger<PosOrderDispatcher>.Instance);
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        var status = await dispatcher.TryDispatchAsync(order, "Table 1", CancellationToken.None);

        Assert.Equal(PosDeliveryStatus.Failed, status);
    }

    [Fact]
    public async Task Returns_Sent_when_the_integration_succeeds()
    {
        var dispatcher = new PosOrderDispatcher(
            new FakeResolver(new FakeIntegration()),
            NullLogger<PosOrderDispatcher>.Instance);
        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        var status = await dispatcher.TryDispatchAsync(order, "Table 1", CancellationToken.None);

        Assert.Equal(PosDeliveryStatus.Sent, status);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PingMe.slnx --filter "FullyQualifiedName~PosOrderDispatcherTests"`
Expected: FAIL to compile — `PosOrderDispatcher` doesn't exist yet.

- [ ] **Step 3: Implement `PosIntegrationResolver`**

`src/PingMe.Infrastructure/Integrations/PosIntegrationResolver.cs`:
```csharp
namespace PingMe.Infrastructure.Integrations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http;
using PingMe.Application.Integrations;
using PingMe.Domain.Integrations;
using PingMe.Infrastructure.Persistence;

public class PosIntegrationResolver : IPosIntegrationResolver
{
    private readonly PingMeDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;

    public PosIntegrationResolver(PingMeDbContext dbContext, IHttpClientFactory httpClientFactory)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IPosIntegration?> ResolveAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.TenantPosIntegrationSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

        if (settings is null || !settings.IsEnabled)
        {
            return null;
        }

        return settings.ProviderType switch
        {
            ProviderType.Webhook => new WebhookPosIntegration(
                settings.WebhookUrl,
                _httpClientFactory.CreateClient(nameof(WebhookPosIntegration))),
            _ => null,
        };
    }
}
```

`FirstOrDefaultAsync(s => s.TenantId == tenantId, ...)` relies on the caller (`OrdersController.Create`, Task 9) having already set `_currentTenantProvider.TenantId = session.TenantId` before dispatch runs — the global tenant query filter already scopes this query correctly, and the explicit `TenantId` predicate is redundant-but-safe defense-in-depth, matching the pattern already used in `OrdersController.GetStatus`.

- [ ] **Step 4: Implement `PosOrderDispatcher`**

`src/PingMe.Infrastructure/Integrations/PosOrderDispatcher.cs`:
```csharp
namespace PingMe.Infrastructure.Integrations;

using Microsoft.Extensions.Logging;
using PingMe.Application.Integrations;
using PingMe.Domain.Integrations;
using PingMe.Domain.Ordering;

public class PosOrderDispatcher : IPosOrderDispatcher
{
    private readonly IPosIntegrationResolver _resolver;
    private readonly ILogger<PosOrderDispatcher> _logger;

    public PosOrderDispatcher(IPosIntegrationResolver resolver, ILogger<PosOrderDispatcher> logger)
    {
        _resolver = resolver;
        _logger = logger;
    }

    public async Task<PosDeliveryStatus> TryDispatchAsync(Order order, string locationLabel, CancellationToken cancellationToken)
    {
        try
        {
            var integration = await _resolver.ResolveAsync(order.TenantId, cancellationToken);
            if (integration is null)
            {
                return PosDeliveryStatus.NotConfigured;
            }

            await integration.SendOrderAsync(order, locationLabel, cancellationToken);
            return PosDeliveryStatus.Sent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "POS dispatch failed for order {OrderId}", order.Id);
            return PosDeliveryStatus.Failed;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test PingMe.slnx --filter "FullyQualifiedName~PosOrderDispatcherTests"`
Expected: PASS — all 3 facts green.

- [ ] **Step 6: Run the full backend test suite**

Run: `dotnet test PingMe.slnx`
Expected: PASS — all 50 tests green (47 existing + 3 new).

- [ ] **Step 7: Commit**

```bash
git add src/PingMe.Infrastructure/Integrations/PosIntegrationResolver.cs src/PingMe.Infrastructure/Integrations/PosOrderDispatcher.cs tests/PingMe.UnitTests/Integrations/PosOrderDispatcherTests.cs
git commit -m "Implement PosIntegrationResolver and PosOrderDispatcher"
```

---

## Task 7: Wire DI registrations in Program.cs

**Files:**
- Modify: `src/PingMe.Api/Program.cs`

**Does NOT cover:** any controller (Task 8) — this task only registers services so they're resolvable.

- [ ] **Step 1: Add the usings**

In `src/PingMe.Api/Program.cs`, add:
```csharp
using PingMe.Application.Integrations;
using PingMe.Infrastructure.Integrations;
```

- [ ] **Step 2: Register the named `HttpClient` and the two services**

Add this block right after the existing `builder.Services.AddScoped<IOrderNotifier, SignalROrderNotifier>();` line:
```csharp
builder.Services.AddHttpClient(nameof(WebhookPosIntegration), client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddScoped<IPosIntegrationResolver, PosIntegrationResolver>();
builder.Services.AddScoped<IPosOrderDispatcher, PosOrderDispatcher>();
```

The 5-second timeout matters here specifically because POS dispatch happens inline during `POST /orders` (Task 9) — without a bound, an unresponsive POS endpoint could hang a customer's order request indefinitely. `PosOrderDispatcher`'s try/catch (Task 6) already turns a `TaskCanceledException` from this timeout into a `Failed` status, same as any other dispatch failure.

- [ ] **Step 3: Build**

Run: `dotnet build PingMe.slnx`
Expected: PASS — 0 build errors.

- [ ] **Step 4: Run the full backend test suite**

Run: `dotnet test PingMe.slnx`
Expected: PASS — all 50 tests green (no behavior change yet for existing tests; this task only adds registrations nothing calls yet).

- [ ] **Step 5: Commit**

```bash
git add src/PingMe.Api/Program.cs
git commit -m "Register IPosIntegrationResolver, IPosOrderDispatcher, and a timed HttpClient for webhooks"
```

---

## Task 8: Add the PosIntegrationSettingsController admin endpoint

**Files:**
- Create: `src/PingMe.Api/Contracts/Integrations/PosIntegrationSettingsDto.cs`
- Create: `src/PingMe.Api/Contracts/Integrations/UpsertPosIntegrationSettingsRequest.cs`
- Create: `src/PingMe.Api/Controllers/PosIntegrationSettingsController.cs`
- Test: `tests/PingMe.IntegrationTests/Api/PosIntegrationSettingsTests.cs`

**Does NOT cover:** the order-creation dispatch call (Task 9) — this task only lets an Owner read/write their tenant's settings.

- [ ] **Step 1: Write failing tests**

`tests/PingMe.IntegrationTests/Api/PosIntegrationSettingsTests.cs`:
```csharp
namespace PingMe.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PingMe.Api.Contracts.Auth;
using PingMe.Api.Contracts.Integrations;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class PosIntegrationSettingsTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public PosIntegrationSettingsTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> RegisterOwnerAsync(string venueName)
    {
        var client = _factory.CreateClient();
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest(venueName, email, "P@ssw0rd123"));
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    [Fact]
    public async Task Unauthenticated_request_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/admin/pos-integration");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Getting_settings_before_any_are_configured_returns_204()
    {
        var owner = await RegisterOwnerAsync("Venue A");

        var response = await owner.GetAsync("/admin/pos-integration");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Upserting_settings_then_getting_them_returns_the_saved_values()
    {
        var owner = await RegisterOwnerAsync("Venue B");

        var putResponse = await owner.PutAsJsonAsync("/admin/pos-integration",
            new UpsertPosIntegrationSettingsRequest("Webhook", "http://example.invalid/webhook", true));
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var getResponse = await owner.GetAsync("/admin/pos-integration");
        var settings = await getResponse.Content.ReadFromJsonAsync<PosIntegrationSettingsDto>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal("Webhook", settings!.ProviderType);
        Assert.Equal("http://example.invalid/webhook", settings.WebhookUrl);
        Assert.True(settings.IsEnabled);
    }

    [Fact]
    public async Task Upserting_twice_updates_the_same_row_instead_of_creating_a_second_one()
    {
        var owner = await RegisterOwnerAsync("Venue C");

        await owner.PutAsJsonAsync("/admin/pos-integration",
            new UpsertPosIntegrationSettingsRequest("Webhook", "http://example.invalid/first", true));
        await owner.PutAsJsonAsync("/admin/pos-integration",
            new UpsertPosIntegrationSettingsRequest("Webhook", "http://example.invalid/second", false));

        var getResponse = await owner.GetAsync("/admin/pos-integration");
        var settings = await getResponse.Content.ReadFromJsonAsync<PosIntegrationSettingsDto>();

        Assert.Equal("http://example.invalid/second", settings!.WebhookUrl);
        Assert.False(settings.IsEnabled);
    }

    [Fact]
    public async Task Upserting_with_an_invalid_provider_type_returns_400()
    {
        var owner = await RegisterOwnerAsync("Venue D");

        var response = await owner.PutAsJsonAsync("/admin/pos-integration",
            new UpsertPosIntegrationSettingsRequest("NotARealProvider", "http://example.invalid/webhook", true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TenantA_cannot_see_TenantBs_pos_integration_settings()
    {
        var ownerA = await RegisterOwnerAsync("Venue E");
        var ownerB = await RegisterOwnerAsync("Venue F");

        await owner_B_configures(ownerB);

        var response = await ownerA.GetAsync("/admin/pos-integration");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        async Task owner_B_configures(HttpClient client)
        {
            await client.PutAsJsonAsync("/admin/pos-integration",
                new UpsertPosIntegrationSettingsRequest("Webhook", "http://example.invalid/tenant-b", true));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PingMe.slnx --filter "FullyQualifiedName~PosIntegrationSettingsTests"`
Expected: FAIL to compile — the DTOs and controller don't exist yet.

- [ ] **Step 3: Create the DTOs**

`src/PingMe.Api/Contracts/Integrations/PosIntegrationSettingsDto.cs`:
```csharp
namespace PingMe.Api.Contracts.Integrations;

public record PosIntegrationSettingsDto(string ProviderType, string WebhookUrl, bool IsEnabled);
```

`src/PingMe.Api/Contracts/Integrations/UpsertPosIntegrationSettingsRequest.cs`:
```csharp
namespace PingMe.Api.Contracts.Integrations;

public record UpsertPosIntegrationSettingsRequest(string ProviderType, string WebhookUrl, bool IsEnabled);
```

- [ ] **Step 4: Create the controller**

`src/PingMe.Api/Controllers/PosIntegrationSettingsController.cs`:
```csharp
namespace PingMe.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PingMe.Api.Contracts.Integrations;
using PingMe.Application.Tenants;
using PingMe.Domain.Integrations;
using PingMe.Infrastructure.Persistence;

[ApiController]
[Route("admin/pos-integration")]
[Authorize(Roles = "Owner")]
public class PosIntegrationSettingsController : ControllerBase
{
    private readonly PingMeDbContext _dbContext;
    private readonly ICurrentTenantProvider _currentTenantProvider;

    public PosIntegrationSettingsController(PingMeDbContext dbContext, ICurrentTenantProvider currentTenantProvider)
    {
        _dbContext = dbContext;
        _currentTenantProvider = currentTenantProvider;
    }

    [HttpGet]
    public async Task<ActionResult<PosIntegrationSettingsDto>> Get()
    {
        var settings = await _dbContext.TenantPosIntegrationSettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            return NoContent();
        }

        return Ok(new PosIntegrationSettingsDto(settings.ProviderType.ToString(), settings.WebhookUrl, settings.IsEnabled));
    }

    [HttpPut]
    public async Task<ActionResult<PosIntegrationSettingsDto>> Upsert(UpsertPosIntegrationSettingsRequest request)
    {
        if (!Enum.TryParse<ProviderType>(request.ProviderType, out var providerType))
        {
            return BadRequest($"'{request.ProviderType}' is not a valid provider type.");
        }

        var settings = await _dbContext.TenantPosIntegrationSettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new TenantPosIntegrationSettings(
                _currentTenantProvider.TenantId!.Value, providerType, request.WebhookUrl, request.IsEnabled);
            _dbContext.TenantPosIntegrationSettings.Add(settings);
        }
        else
        {
            settings.UpdateSettings(providerType, request.WebhookUrl, request.IsEnabled);
        }

        await _dbContext.SaveChangesAsync();
        return Ok(new PosIntegrationSettingsDto(settings.ProviderType.ToString(), settings.WebhookUrl, settings.IsEnabled));
    }
}
```

`FirstOrDefaultAsync()` with no predicate is safe here because the global tenant query filter already scopes `TenantPosIntegrationSettings` to the authenticated Owner's tenant, and the per-tenant unique index (Task 3) guarantees at most one row exists for that tenant — same one-row-per-tenant pattern this codebase already uses for `Venue` lookups (`LocationsController.CreateLocation`'s `_dbContext.Venues.FirstAsync()`).

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test PingMe.slnx --filter "FullyQualifiedName~PosIntegrationSettingsTests"`
Expected: PASS — all 6 facts green.

- [ ] **Step 6: Run the full backend test suite**

Run: `dotnet test PingMe.slnx`
Expected: PASS — all 56 tests green (50 existing + 6 new).

- [ ] **Step 7: Commit**

```bash
git add src/PingMe.Api/Contracts/Integrations/PosIntegrationSettingsDto.cs src/PingMe.Api/Contracts/Integrations/UpsertPosIntegrationSettingsRequest.cs src/PingMe.Api/Controllers/PosIntegrationSettingsController.cs tests/PingMe.IntegrationTests/Api/PosIntegrationSettingsTests.cs
git commit -m "Add PosIntegrationSettingsController: GET/PUT /admin/pos-integration"
```

---

## Task 9: Wire POS dispatch into OrdersController.Create

**Files:**
- Modify: `src/PingMe.Api/Controllers/OrdersController.cs`
- Test: `tests/PingMe.IntegrationTests/Api/PosDispatchOnOrderCreationTests.cs`

**Does NOT cover:** re-broadcasting an `OrderStatusChanged` SignalR event after the POS status is recorded — the existing `OrderReceived` broadcast (built before dispatch runs) already fired with `PosDeliveryStatus = NotConfigured`; this plan does not add a second broadcast for the POS outcome. Staff can only see the final `PosDeliveryStatus` by re-fetching `GET /admin/orders`.

- [ ] **Step 1: Write failing tests**

`tests/PingMe.IntegrationTests/Api/PosDispatchOnOrderCreationTests.cs`:
```csharp
namespace PingMe.IntegrationTests.Api;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using PingMe.Api.Contracts.Auth;
using PingMe.Api.Contracts.Catalog;
using PingMe.Api.Contracts.Integrations;
using PingMe.Api.Contracts.Locations;
using PingMe.Api.Contracts.Ordering;
using PingMe.Application.Ordering;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class PosDispatchOnOrderCreationTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public PosDispatchOnOrderCreationTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record SeededTenant(HttpClient OwnerClient, ProductDto Product, string QrCode);

    private async Task<SeededTenant> SeedTenantWithOneOrderableProductAsync(string venueName)
    {
        var ownerClient = _factory.CreateClient();
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await ownerClient.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest(venueName, email, "P@ssw0rd123"));
        var loginResponse = await ownerClient.PostAsJsonAsync("/auth/login", new LoginRequest(email, "P@ssw0rd123"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var menuResponse = await ownerClient.PostAsJsonAsync("/admin/menus", new CreateMenuRequest("Menu"));
        var menu = await menuResponse.Content.ReadFromJsonAsync<MenuDto>();
        var categoryResponse = await ownerClient.PostAsJsonAsync(
            $"/admin/menus/{menu!.Id}/categories", new CreateCategoryRequest("Category", 1));
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>();
        var productResponse = await ownerClient.PostAsJsonAsync(
            $"/admin/products/categories/{category!.Id}", new CreateProductRequest("Product", 5.00m));
        var product = (await productResponse.Content.ReadFromJsonAsync<ProductDto>())!;

        var locationResponse = await ownerClient.PostAsJsonAsync("/admin/locations", new CreateLocationRequest("Table 1", null));
        var location = await locationResponse.Content.ReadFromJsonAsync<LocationDto>();
        var qrResponse = await ownerClient.PostAsJsonAsync("/admin/qrcodes", new CreateQrCodeRequest(location!.Id));
        var qrCode = await qrResponse.Content.ReadFromJsonAsync<QrCodeDto>();

        return new SeededTenant(ownerClient, product, qrCode!.Code);
    }

    [Fact]
    public async Task Placing_an_order_with_no_pos_settings_succeeds_with_NotConfigured_status()
    {
        var seeded = await SeedTenantWithOneOrderableProductAsync("Venue A");
        var customerClient = _factory.CreateClient();
        var resolved = await (await customerClient.GetAsync($"/p/{seeded.QrCode}"))
            .Content.ReadFromJsonAsync<ResolveQrCodeResponse>();

        var orderResponse = await customerClient.PostAsJsonAsync("/orders",
            new CreateOrderRequest(resolved!.SessionId, new List<CreateOrderItemRequest> { new(seeded.Product.Id, 1) }));
        var order = await orderResponse.Content.ReadFromJsonAsync<CreateOrderResponse>();

        var adminOrders = await seeded.OwnerClient.GetFromJsonAsync<List<AdminOrderDto>>("/admin/orders");
        var adminOrder = adminOrders!.Single(o => o.Id == order!.OrderId);

        Assert.Equal("NotConfigured", adminOrder.PosDeliveryStatus);
    }

    [Fact]
    public async Task Placing_an_order_with_an_unreachable_webhook_still_succeeds_with_Failed_status()
    {
        var seeded = await SeedTenantWithOneOrderableProductAsync("Venue B");
        await seeded.OwnerClient.PutAsJsonAsync("/admin/pos-integration",
            new UpsertPosIntegrationSettingsRequest("Webhook", "http://127.0.0.1:1/unreachable", true));

        var customerClient = _factory.CreateClient();
        var resolved = await (await customerClient.GetAsync($"/p/{seeded.QrCode}"))
            .Content.ReadFromJsonAsync<ResolveQrCodeResponse>();

        var orderResponse = await customerClient.PostAsJsonAsync("/orders",
            new CreateOrderRequest(resolved!.SessionId, new List<CreateOrderItemRequest> { new(seeded.Product.Id, 1) }));

        Assert.Equal(System.Net.HttpStatusCode.Created, orderResponse.StatusCode);
        var order = await orderResponse.Content.ReadFromJsonAsync<CreateOrderResponse>();

        var adminOrders = await seeded.OwnerClient.GetFromJsonAsync<List<AdminOrderDto>>("/admin/orders");
        var adminOrder = adminOrders!.Single(o => o.Id == order!.OrderId);

        Assert.Equal("Failed", adminOrder.PosDeliveryStatus);
    }

    [Fact]
    public async Task Disabled_pos_settings_result_in_NotConfigured_not_an_attempted_call()
    {
        var seeded = await SeedTenantWithOneOrderableProductAsync("Venue C");
        await seeded.OwnerClient.PutAsJsonAsync("/admin/pos-integration",
            new UpsertPosIntegrationSettingsRequest("Webhook", "http://127.0.0.1:1/unreachable", false));

        var customerClient = _factory.CreateClient();
        var resolved = await (await customerClient.GetAsync($"/p/{seeded.QrCode}"))
            .Content.ReadFromJsonAsync<ResolveQrCodeResponse>();

        var orderResponse = await customerClient.PostAsJsonAsync("/orders",
            new CreateOrderRequest(resolved!.SessionId, new List<CreateOrderItemRequest> { new(seeded.Product.Id, 1) }));
        var order = await orderResponse.Content.ReadFromJsonAsync<CreateOrderResponse>();

        var adminOrders = await seeded.OwnerClient.GetFromJsonAsync<List<AdminOrderDto>>("/admin/orders");
        var adminOrder = adminOrders!.Single(o => o.Id == order!.OrderId);

        Assert.Equal("NotConfigured", adminOrder.PosDeliveryStatus);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PingMe.slnx --filter "FullyQualifiedName~PosDispatchOnOrderCreationTests"`
Expected: FAIL — `AdminOrderDto.PosDeliveryStatus` is always `"NotConfigured"` for every order today (correct for the first and third tests, but only by coincidence since dispatch never runs) and the second test fails because a webhook was configured but nothing ever calls it, so `PosDeliveryStatus` stays `"NotConfigured"` instead of the expected `"Failed"`.

- [ ] **Step 3: Wire the dispatcher into `OrdersController`**

In `src/PingMe.Api/Controllers/OrdersController.cs`, add the usings:
```csharp
using PingMe.Application.Integrations;
using PingMe.Domain.Locations;
```

Add the fourth dependency and constructor parameter:
```csharp
private readonly PingMeDbContext _dbContext;
private readonly CurrentTenantProvider _currentTenantProvider;
private readonly IOrderNotifier _orderNotifier;
private readonly IPosOrderDispatcher _posOrderDispatcher;

public OrdersController(
    PingMeDbContext dbContext,
    CurrentTenantProvider currentTenantProvider,
    IOrderNotifier orderNotifier,
    IPosOrderDispatcher posOrderDispatcher)
{
    _dbContext = dbContext;
    _currentTenantProvider = currentTenantProvider;
    _orderNotifier = orderNotifier;
    _posOrderDispatcher = posOrderDispatcher;
}
```

Change the tail of `Create` from:
```csharp
        var orderDto = new AdminOrderDto(
            order.Id,
            order.Status.ToString(),
            order.CreatedAt,
            order.Items.Select(i => new AdminOrderItemDto(i.ProductName, i.UnitPrice, i.Quantity)).ToList(),
            order.PosDeliveryStatus.ToString());
        await _orderNotifier.NotifyOrderReceivedAsync(order.TenantId, orderDto);

        return Created(string.Empty, new CreateOrderResponse(order.Id, order.Status.ToString()));
```
to:
```csharp
        var orderDto = new AdminOrderDto(
            order.Id,
            order.Status.ToString(),
            order.CreatedAt,
            order.Items.Select(i => new AdminOrderItemDto(i.ProductName, i.UnitPrice, i.Quantity)).ToList(),
            order.PosDeliveryStatus.ToString());
        await _orderNotifier.NotifyOrderReceivedAsync(order.TenantId, orderDto);

        var sessionLocation = await _dbContext.Locations.FirstOrDefaultAsync(l => l.Id == session.LocationId);
        var locationLabel = sessionLocation?.Name ?? "Unknown location";
        var posDeliveryStatus = await _posOrderDispatcher.TryDispatchAsync(order, locationLabel, HttpContext.RequestAborted);
        order.RecordPosDeliveryStatus(posDeliveryStatus);
        await _dbContext.SaveChangesAsync();

        return Created(string.Empty, new CreateOrderResponse(order.Id, order.Status.ToString()));
```

This reuses `session` (the `CustomerSession` already loaded at the top of `Create`) to resolve the location cheaply, matching the parameter shape locked in by Task 4's `IPosOrderDispatcher.TryDispatchAsync(order, locationLabel, cancellationToken)`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PingMe.slnx --filter "FullyQualifiedName~PosDispatchOnOrderCreationTests"`
Expected: PASS — all 3 facts green.

- [ ] **Step 5: Run the full backend test suite**

Run: `dotnet test PingMe.slnx`
Expected: PASS — all 59 tests green (56 existing + 3 new).

- [ ] **Step 6: Commit**

```bash
git add src/PingMe.Api/Controllers/OrdersController.cs tests/PingMe.IntegrationTests/Api/PosDispatchOnOrderCreationTests.cs
git commit -m "Dispatch orders to the tenant's configured POS on creation"
```

---

## Task 10: Final verification and progress tracking

**Files:**
- Modify: `Docs/superpowers optimized/plans/PROGRESS.md`

- [ ] **Step 1: Run the full backend build and test suite from a clean state**

Run: `dotnet build PingMe.slnx && dotnet test PingMe.slnx`
Expected: PASS — 0 build errors, all 59 tests passing (see Task 9, Step 5 for the breakdown).

- [ ] **Step 2: Confirm no frontend changes are needed**

This plan is backend-only per its Assumptions — run a quick check that neither frontend app references anything this plan touched:
```bash
cd src/pingme-web/customer-app && pnpm test && pnpm build
cd ../staff-app && pnpm test && pnpm build
```
Expected: PASS — both apps build and test identically to their pre-Plan-5 state, since this plan added an extra JSON field (`posDeliveryStatus`) that neither app's TypeScript types declare or read.

- [ ] **Step 3: Manually verify the dispatch flow end-to-end** (not automatable in this plan — do this once by hand)

1. Start the backend: `dotnet run --project src/PingMe.Api`.
2. Via Swagger, register a tenant, log in, create a menu/category/product, a location, and a QR code.
3. `PUT /admin/pos-integration` with a `webhookUrl` pointing at a request-inspection service you control (e.g. a temporary `webhook.site` URL, or a local listener) and `isEnabled: true`.
4. Resolve the QR code and place an order via `POST /orders`.
5. Confirm the configured webhook endpoint received a POST with the exact JSON shape from the spec's Section 7.
6. `GET /admin/orders` and confirm the order's `posDeliveryStatus` is `"Sent"`.
7. Stop the backend afterward.

- [ ] **Step 4: Update `Docs/superpowers optimized/plans/PROGRESS.md`**

Update the table to add this plan's row (`5 | POS/ERP Integration (Level 1) | ... | Done, reviewed | 10 / 10`). Add a new carried-forward item: **"WebhookUrl accepts any tenant-supplied URL with no SSRF hardening — explicitly flagged in Plan 5's spec as required before any production rollout that lets a tenant self-serve a webhook URL. Vendor-specific POS adapters (Zone Soft, PRIMAVERA, WinRest, etc.) remain unbuilt — no real API access/credentials exist yet for any of them."**

- [ ] **Step 5: Commit**

```bash
git add "Docs/superpowers optimized/plans/PROGRESS.md"
git commit -m "Close Plan 5: mark done, flag SSRF hardening and vendor adapters as future work"
```

---

## Self-Review

**1. Spec coverage.**
- Section 1 (Scope): `Integrations` module delivered across all four layers (Tasks 2-8); `IPosIntegration` limited to `SendOrderAsync` (Task 4); `IPosOrderDispatcher` seam delivered (Task 4, 6); one working `WebhookPosIntegration` provider (Task 5); per-tenant opt-in settings via Owner-only admin endpoint (Task 8); best-effort delivery with outcome recorded on `Order` (Tasks 1, 6, 9).
- Section 2 (Plan 3 dependency): confirmed Plans 1-4 are merged before writing this plan; Task 9 edits the real, current `OrdersController.Create`.
- Section 3 (Architecture): `IPosOrderDispatcher` seam delivered with one deliberate, documented signature refinement (`locationLabel` parameter, Task 4) that keeps `PosOrderDispatcher` database-free and unit-testable, matching Section 9's own testing intent more closely than the spec's literal sketch.
- Section 4 (Data Model): `TenantPosIntegrationSettings`, `ProviderType`, `PosDeliveryStatus`, and `Order.PosDeliveryStatus`/`RecordPosDeliveryStatus` all delivered exactly as specified (Tasks 1-2); per-tenant unique index delivered (Task 3).
- Section 5 (API Contract): `GET`/`PUT /admin/pos-integration` delivered with the exact 204/200/400 semantics specified (Task 8).
- Section 6 (Integration point): delivered against the real merged code, not the spec's originally-stale sketch (Task 9; the spec itself was corrected before this plan was written).
- Section 7 (Webhook payload): delivered exactly, camelCase via `JsonSerializerDefaults.Web`, verified by a unit test parsing the actual emitted JSON (Task 5).
- Section 8 (Error Handling): missing/disabled settings → `NotConfigured` without an HTTP call (Task 6, tested Task 9); unreachable/failing webhook → `Failed`, order still succeeds (Task 6, tested Task 9); invalid `providerType` → 400 (Task 8, tested).
- Section 9 (Testing Strategy): tenant isolation for settings (Task 8's isolation fact); failure isolation (Task 9); payload shape (Task 5, as a faster unit test — documented deviation); `NotConfigured` path (Task 9); unauthenticated 401 (Task 8).
- Section 10 (Failure-Mode Check): SSRF non-goal carried forward to `PROGRESS.md` (Task 10); swallowed-exception observability delivered via `_logger.LogWarning` (Task 6); inconsistent session/location state degrades to `Failed`, not a new failure path (Task 9's `sessionLocation?.Name ?? "Unknown location"` null-coalescing).

**2. Placeholder scan.** No "TBD"/"TODO" strings. Every code block is complete, runnable code. Two deliberate, explicitly-labeled deviations from the spec's literal text (Task 4's `locationLabel` parameter, Task 5's unit-test-not-integration-test approach) are documented with their reasoning, not silently substituted.

**3. Type consistency.** `IPosIntegration.SendOrderAsync(Order, string, CancellationToken)` (Task 4) is implemented identically in `WebhookPosIntegration` (Task 5) and consumed identically in `PosOrderDispatcher.TryDispatchAsync` (Task 6) and `OrdersController.Create` (Task 9). `AdminOrderDto`'s new fifth parameter (`PosDeliveryStatus`, Task 1) is threaded through all three of its construction call sites (`OrdersController.Create`, `AdminOrdersController.GetOrders`, `AdminOrdersController.UpdateStatus`) with matching argument order. `PosIntegrationSettingsDto`/`UpsertPosIntegrationSettingsRequest` (Task 8) share the same three-field shape (`ProviderType`, `WebhookUrl`, `IsEnabled`) in both directions.

---

## Execution Handoff

**Plan complete and saved to `Docs/superpowers optimized/plans/2026-08-27-pingme-plan-5-pos-integration.md`. Two execution options:**

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** — Execute tasks in this session using executing-plans, with checkpoints

**Which approach?**
