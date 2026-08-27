# Plan 4 Implementation Plan: SignalR + Staff Dashboard + Local Deployment

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-optimized:subagent-driven-development (recommended) or superpowers-optimized:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give staff/owners real-time order visibility via a SignalR hub and a new staff dashboard app, plus a local Docker Compose setup for the API and database.

**Architecture:** A new `OrdersHub` (`/hubs/orders`) joins each authenticated connection to a `tenant-{tenantId}` SignalR group using the same JWT `tenantId` claim already used by `TenantResolutionMiddleware`. A new `IOrderNotifier` seam (Application layer) is called from `OrdersController.Create` and `AdminOrdersController.UpdateStatus` after each successful save, broadcasting `OrderReceived`/`OrderStatusChanged` to the owning tenant's group only — best-effort, failures logged and swallowed. A new `staff-app` (Vite/React/TS, same workspace as `customer-app`) connects to the hub, fetches `GET /admin/orders`, and keeps its order list in sync via upsert-by-id. Docker Compose adds a `Dockerfile` for the API and wires it to a Postgres container on a non-conflicting host port.

**Tech Stack:** ASP.NET Core SignalR (server, part of the shared framework — no new NuGet package), `@microsoft/signalr` (client), React 18 + TypeScript + Vite + Vitest (matching `customer-app`), Docker + Docker Compose.

**Assumptions:**
- Assumes Docker is not available in the execution/test environment for this plan (verified absent during design) — the Docker task's verification is limited to `docker compose config` if the CLI is available, or a manual YAML review otherwise. It will NOT include a live `docker compose up` smoke test as part of this plan's automated verification; a manual one is recommended before Plan 4 is considered fully closed.
- Assumes the customer-app's existing 5-second polling stays as-is (Plan 3, unchanged) — this plan does NOT wire the customer app to SignalR.
- Assumes staff dashboard authentication is JWT-only, reusing `/auth/login` (Owner or Staff role) — this plan does NOT add a staff-specific registration flow (Staff users, if any exist, must already be created via a future admin-UI or direct DB action; that gap is out of scope here, same as the already-flagged carried-forward "no Owner/Staff admin UI" item).

---

## Task 1: Move AdminOrderDto/AdminOrderItemDto into the Application layer

**Files:**
- Create: `src/PingMe.Application/Ordering/AdminOrderDto.cs`
- Create: `src/PingMe.Application/Ordering/AdminOrderItemDto.cs`
- Delete: `src/PingMe.Api/Contracts/Ordering/AdminOrderDto.cs`
- Delete: `src/PingMe.Api/Contracts/Ordering/AdminOrderItemDto.cs`
- Modify: `src/PingMe.Api/Controllers/AdminOrdersController.cs`
- Modify: `tests/PingMe.IntegrationTests/Api/AdminOrdersTests.cs`
- Modify: `tests/PingMe.IntegrationTests/Api/OrderingIsolationTests.cs`

**Does NOT cover:** moving any other DTO in `PingMe.Api.Contracts.Ordering` (e.g. `CreateOrderRequest`, `UpdateOrderStatusRequest`) — only `AdminOrderDto`/`AdminOrderItemDto` move, because only these two are needed by the new `IOrderNotifier` interface (Task 2), which must live in `PingMe.Application` and cannot reference `PingMe.Api` (wrong dependency direction — `PingMe.Api.csproj` references `PingMe.Application.csproj`, not the reverse).

- [x] **Step 1: Create the new DTO files**

`src/PingMe.Application/Ordering/AdminOrderItemDto.cs`:
```csharp
namespace PingMe.Application.Ordering;

public record AdminOrderItemDto(string ProductName, decimal UnitPrice, int Quantity);
```

`src/PingMe.Application/Ordering/AdminOrderDto.cs`:
```csharp
namespace PingMe.Application.Ordering;

public record AdminOrderDto(Guid Id, string Status, DateTime CreatedAt, List<AdminOrderItemDto> Items);
```

- [x] **Step 2: Delete the old files**

Delete `src/PingMe.Api/Contracts/Ordering/AdminOrderDto.cs` and `src/PingMe.Api/Contracts/Ordering/AdminOrderItemDto.cs`.

- [x] **Step 3: Update `AdminOrdersController.cs`'s usings**

In `src/PingMe.Api/Controllers/AdminOrdersController.cs`, change:
```csharp
using PingMe.Api.Contracts.Ordering;
```
to:
```csharp
using PingMe.Api.Contracts.Ordering;
using PingMe.Application.Ordering;
```
(The controller still uses `UpdateOrderStatusRequest` from `PingMe.Api.Contracts.Ordering`, so that using stays — `AdminOrderDto`/`AdminOrderItemDto` now resolve from the new namespace.)

- [x] **Step 4: Update the two test files' usings**

In `tests/PingMe.IntegrationTests/Api/AdminOrdersTests.cs`, change:
```csharp
using PingMe.Api.Contracts.Ordering;
```
to:
```csharp
using PingMe.Api.Contracts.Ordering;
using PingMe.Application.Ordering;
```

In `tests/PingMe.IntegrationTests/Api/OrderingIsolationTests.cs`, make the identical change:
```csharp
using PingMe.Api.Contracts.Ordering;
using PingMe.Application.Ordering;
```

- [x] **Step 5: Build and run the full backend test suite**

Run: `dotnet build PingMe.slnx && dotnet test PingMe.slnx`
Expected: PASS — 0 build errors, all 38 existing tests still green (this is a pure namespace move, no behavior change).

- [x] **Step 6: Commit**

```bash
git add src/PingMe.Application/Ordering/AdminOrderDto.cs src/PingMe.Application/Ordering/AdminOrderItemDto.cs src/PingMe.Api/Contracts/Ordering/AdminOrderDto.cs src/PingMe.Api/Contracts/Ordering/AdminOrderItemDto.cs src/PingMe.Api/Controllers/AdminOrdersController.cs tests/PingMe.IntegrationTests/Api/AdminOrdersTests.cs tests/PingMe.IntegrationTests/Api/OrderingIsolationTests.cs
git commit -m "Move AdminOrderDto/AdminOrderItemDto into PingMe.Application.Ordering"
```

---

## Task 2: Add the IOrderNotifier interface

**Files:**
- Create: `src/PingMe.Application/Ordering/IOrderNotifier.cs`

**Does NOT cover:** any implementation of this interface (Task 4) or wiring callers (Tasks 5, 6) — this task only adds the seam.

- [x] **Step 1: Create the interface**

```csharp
namespace PingMe.Application.Ordering;

public interface IOrderNotifier
{
    Task NotifyOrderReceivedAsync(Guid tenantId, AdminOrderDto order);
    Task NotifyOrderStatusChangedAsync(Guid tenantId, AdminOrderDto order);
}
```

- [x] **Step 2: Build**

Run: `dotnet build PingMe.slnx`
Expected: PASS — 0 build errors (this interface has no implementers yet, which is fine; nothing references it).

- [x] **Step 3: Commit**

```bash
git add src/PingMe.Application/Ordering/IOrderNotifier.cs
git commit -m "Add IOrderNotifier interface"
```

---

## Task 3: Create OrdersHub and wire SignalR into Program.cs

**Files:**
- Create: `src/PingMe.Api/Realtime/OrdersHub.cs`
- Modify: `src/PingMe.Api/Program.cs`
- Test: `tests/PingMe.IntegrationTests/Api/OrdersHubAuthTests.cs`

**Does NOT cover:** actually broadcasting any order events (Tasks 5, 6) or the `IOrderNotifier` implementation (Task 4) — this task only stands up the hub route and its auth gate.

- [x] **Step 1: Write failing tests**

`tests/PingMe.IntegrationTests/Api/OrdersHubAuthTests.cs`:
```csharp
namespace PingMe.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Json;
using PingMe.Api.Contracts.Auth;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class OrdersHubAuthTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public OrdersHubAuthTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Negotiating_the_orders_hub_without_a_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/hubs/orders/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Negotiating_the_orders_hub_with_a_valid_token_returns_200()
    {
        var client = _factory.CreateClient();
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Venue", email, "P@ssw0rd123"));
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "P@ssw0rd123"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        var response = await client.PostAsync(
            $"/hubs/orders/negotiate?negotiateVersion=1&access_token={auth!.Token}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

- [x] **Step 2: Run tests to verify they fail**

Run: `dotnet test PingMe.slnx --filter "FullyQualifiedName~OrdersHubAuthTests"`
Expected: FAIL — both requests 404 (route `/hubs/orders` doesn't exist yet).

- [x] **Step 3: Create the hub**

`src/PingMe.Api/Realtime/OrdersHub.cs`:
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

- [x] **Step 4: Wire SignalR into `Program.cs`**

Add the `using`:
```csharp
using PingMe.Api.Realtime;
```

Add the service registration right after `builder.Services.AddAuthorization();`:
```csharp
builder.Services.AddSignalR();
```

Change the `.AddJwtBearer(options => { ... })` call to add an `Events` block so the hub can authenticate from the query string (browsers cannot set the `Authorization` header on a WebSocket upgrade):
```csharp
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true,
    };
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

Add the hub route mapping right after `app.MapControllers();`:
```csharp
app.MapHub<OrdersHub>("/hubs/orders");
```

- [x] **Step 5: Run tests to verify they pass**

Run: `dotnet test PingMe.slnx --filter "FullyQualifiedName~OrdersHubAuthTests"`
Expected: PASS — both facts green.

- [x] **Step 6: Run the full backend test suite**

Run: `dotnet test PingMe.slnx`
Expected: PASS — all 40 tests green (38 existing + 2 new).

- [x] **Step 7: Commit**

```bash
git add src/PingMe.Api/Realtime/OrdersHub.cs src/PingMe.Api/Program.cs tests/PingMe.IntegrationTests/Api/OrdersHubAuthTests.cs
git commit -m "Add OrdersHub with tenant-scoped groups and JWT-over-querystring auth"
```

---

## Task 4: Add SignalROrderNotifier and prove tenant-scoped delivery

**Files:**
- Create: `src/PingMe.Api/Realtime/SignalROrderNotifier.cs`
- Modify: `src/PingMe.Api/Program.cs`
- Modify: `tests/PingMe.IntegrationTests/PingMe.IntegrationTests.csproj`
- Test: `tests/PingMe.IntegrationTests/Api/OrderNotifierIsolationTests.cs`

**Does NOT cover:** calling the notifier from any controller (Tasks 5, 6) — this task proves the notifier itself only ever delivers to the tenant it's told to, using a raw `IHubContext<OrdersHub>` broadcast for one fact and the real `IOrderNotifier` for the other.

- [x] **Step 1: Add the SignalR client test package**

Run: `dotnet add tests/PingMe.IntegrationTests/PingMe.IntegrationTests.csproj package Microsoft.AspNetCore.SignalR.Client`
Expected: the command succeeds and adds a `<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" ... />` line to `tests/PingMe.IntegrationTests/PingMe.IntegrationTests.csproj`.

- [x] **Step 2: Write failing tests**

`tests/PingMe.IntegrationTests/Api/OrderNotifierIsolationTests.cs`:
```csharp
namespace PingMe.IntegrationTests.Api;

using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using PingMe.Api.Contracts.Auth;
using PingMe.Api.Realtime;
using PingMe.Application.Ordering;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class OrderNotifierIsolationTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public OrderNotifierIsolationTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(Guid TenantId, string Token)> RegisterTenantAsync(string venueName)
    {
        var client = _factory.CreateClient();
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest(venueName, email, "P@ssw0rd123"));
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(auth!.Token);
        var tenantId = Guid.Parse(jwt.Claims.First(c => c.Type == "tenantId").Value);

        return (tenantId, auth.Token);
    }

    private HubConnection BuildHubConnection(string token)
    {
        var client = _factory.CreateClient();
        return new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, $"/hubs/orders?access_token={token}"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }

    [Fact]
    public async Task Broadcast_to_tenant_As_group_is_not_received_by_a_tenant_B_connection()
    {
        var tenantA = await RegisterTenantAsync("Venue A");
        var tenantB = await RegisterTenantAsync("Venue B");

        await using var connectionA = BuildHubConnection(tenantA.Token);
        await using var connectionB = BuildHubConnection(tenantB.Token);

        var tenantAReceived = new TaskCompletionSource<string>();
        var tenantBReceived = false;
        connectionA.On<string>("TestPing", message => tenantAReceived.TrySetResult(message));
        connectionB.On<string>("TestPing", _ => tenantBReceived = true);

        await connectionA.StartAsync();
        await connectionB.StartAsync();

        using var scope = _factory.Services.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<OrdersHub>>();
        await hubContext.Clients.Group($"tenant-{tenantA.TenantId}").SendAsync("TestPing", "hello-a");

        var completed = await Task.WhenAny(tenantAReceived.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(tenantAReceived.Task, completed);
        Assert.Equal("hello-a", await tenantAReceived.Task);
        Assert.False(tenantBReceived, "Tenant B's connection must never receive a broadcast scoped to Tenant A's group.");
    }

    [Fact]
    public async Task NotifyOrderReceivedAsync_delivers_only_to_the_correct_tenants_group()
    {
        var tenantA = await RegisterTenantAsync("Venue A");
        var tenantB = await RegisterTenantAsync("Venue B");

        await using var connectionA = BuildHubConnection(tenantA.Token);
        await using var connectionB = BuildHubConnection(tenantB.Token);

        var received = new TaskCompletionSource<AdminOrderDto>();
        var tenantBReceived = false;
        connectionA.On<AdminOrderDto>("OrderReceived", order => received.TrySetResult(order));
        connectionB.On<AdminOrderDto>("OrderReceived", _ => tenantBReceived = true);

        await connectionA.StartAsync();
        await connectionB.StartAsync();

        using var scope = _factory.Services.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<IOrderNotifier>();
        var dto = new AdminOrderDto(Guid.NewGuid(), "Received", DateTime.UtcNow, new List<AdminOrderItemDto>());
        await notifier.NotifyOrderReceivedAsync(tenantA.TenantId, dto);

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(received.Task, completed);
        Assert.Equal(dto.Id, (await received.Task).Id);
        Assert.False(tenantBReceived);
    }
}
```

- [x] **Step 3: Run tests to verify they fail**

Run: `dotnet test PingMe.slnx --filter "FullyQualifiedName~OrderNotifierIsolationTests"`
Expected: FAIL — the first fact fails because nothing is broadcast yet against a real scenario is fine (it uses raw `IHubContext`, so it may actually pass once Task 3's hub exists); the second fact FAILS with a DI resolution error ("Unable to resolve service for type 'PingMe.Application.Ordering.IOrderNotifier'") because `IOrderNotifier` has no registered implementation yet.

- [x] **Step 4: Implement `SignalROrderNotifier`**

`src/PingMe.Api/Realtime/SignalROrderNotifier.cs`:
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

- [x] **Step 5: Register it in `Program.cs`**

Add the `using`:
```csharp
using PingMe.Application.Ordering;
```

Add the registration right after `builder.Services.AddSignalR();`:
```csharp
builder.Services.AddScoped<IOrderNotifier, SignalROrderNotifier>();
```

- [x] **Step 6: Run tests to verify they pass**

Run: `dotnet test PingMe.slnx --filter "FullyQualifiedName~OrderNotifierIsolationTests"`
Expected: PASS — both facts green.

- [x] **Step 7: Run the full backend test suite**

Run: `dotnet test PingMe.slnx`
Expected: PASS — all 42 tests green (40 existing + 2 new).

- [x] **Step 8: Commit**

```bash
git add src/PingMe.Api/Realtime/SignalROrderNotifier.cs src/PingMe.Api/Program.cs tests/PingMe.IntegrationTests/PingMe.IntegrationTests.csproj tests/PingMe.IntegrationTests/Api/OrderNotifierIsolationTests.cs
git commit -m "Add SignalROrderNotifier with tenant-group broadcast isolation tests"
```

---

## Task 5: Broadcast OrderReceived when a customer places an order

**Files:**
- Modify: `src/PingMe.Api/Controllers/OrdersController.cs`
- Create: `tests/PingMe.IntegrationTests/Api/RealtimeOrderNotificationTests.cs`

**Does NOT cover:** the `OrderStatusChanged` broadcast on admin status transitions — that's Task 6, added to the same test file.

- [ ] **Step 1: Write a failing test**

`tests/PingMe.IntegrationTests/Api/RealtimeOrderNotificationTests.cs`:
```csharp
namespace PingMe.IntegrationTests.Api;

using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using PingMe.Api.Contracts.Auth;
using PingMe.Api.Contracts.Catalog;
using PingMe.Api.Contracts.Locations;
using PingMe.Api.Contracts.Ordering;
using PingMe.Application.Ordering;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class RealtimeOrderNotificationTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public RealtimeOrderNotificationTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record SeededTenant(HttpClient OwnerClient, string Token, ProductDto Product, string QrCode);

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

        return new SeededTenant(ownerClient, auth.Token, product, qrCode!.Code);
    }

    private HubConnection BuildHubConnection(string token)
    {
        var client = _factory.CreateClient();
        return new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, $"/hubs/orders?access_token={token}"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }

    [Fact]
    public async Task Placing_an_order_broadcasts_OrderReceived_to_the_owning_tenants_staff()
    {
        var seeded = await SeedTenantWithOneOrderableProductAsync("Venue A");
        await using var staffConnection = BuildHubConnection(seeded.Token);
        var received = new TaskCompletionSource<AdminOrderDto>();
        staffConnection.On<AdminOrderDto>("OrderReceived", order => received.TrySetResult(order));
        await staffConnection.StartAsync();

        var customerClient = _factory.CreateClient();
        var resolved = await (await customerClient.GetAsync($"/p/{seeded.QrCode}"))
            .Content.ReadFromJsonAsync<ResolveQrCodeResponse>();
        var orderResponse = await customerClient.PostAsJsonAsync("/orders",
            new CreateOrderRequest(resolved!.SessionId, new List<CreateOrderItemRequest> { new(seeded.Product.Id, 2) }));
        var order = await orderResponse.Content.ReadFromJsonAsync<CreateOrderResponse>();

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(received.Task, completed);
        var broadcast = await received.Task;
        Assert.Equal(order!.OrderId, broadcast.Id);
        Assert.Equal("Received", broadcast.Status);
        Assert.Single(broadcast.Items);
        Assert.Equal(2, broadcast.Items[0].Quantity);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test PingMe.slnx --filter "FullyQualifiedName~Placing_an_order_broadcasts_OrderReceived"`
Expected: FAIL — times out waiting on `received.Task` (no broadcast is ever sent from `OrdersController.Create` yet).

- [ ] **Step 3: Wire the notifier into `OrdersController`**

In `src/PingMe.Api/Controllers/OrdersController.cs`, add the using:
```csharp
using PingMe.Application.Ordering;
```

Add the dependency and constructor parameter:
```csharp
private readonly PingMeDbContext _dbContext;
private readonly CurrentTenantProvider _currentTenantProvider;
private readonly IOrderNotifier _orderNotifier;

public OrdersController(PingMeDbContext dbContext, CurrentTenantProvider currentTenantProvider, IOrderNotifier orderNotifier)
{
    _dbContext = dbContext;
    _currentTenantProvider = currentTenantProvider;
    _orderNotifier = orderNotifier;
}
```

Change the end of `Create` from:
```csharp
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        return Created(string.Empty, new CreateOrderResponse(order.Id, order.Status.ToString()));
```
to:
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

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test PingMe.slnx --filter "FullyQualifiedName~Placing_an_order_broadcasts_OrderReceived"`
Expected: PASS.

- [ ] **Step 5: Run the full backend test suite**

Run: `dotnet test PingMe.slnx`
Expected: PASS — all 43 tests green.

- [ ] **Step 6: Commit**

```bash
git add src/PingMe.Api/Controllers/OrdersController.cs tests/PingMe.IntegrationTests/Api/RealtimeOrderNotificationTests.cs
git commit -m "Broadcast OrderReceived to the owning tenant's staff on order creation"
```

---

## Task 6: Broadcast OrderStatusChanged when staff transition an order

**Files:**
- Modify: `src/PingMe.Api/Controllers/AdminOrdersController.cs`
- Modify: `tests/PingMe.IntegrationTests/Api/RealtimeOrderNotificationTests.cs`

**Does NOT cover:** any change to `Order.TransitionTo`'s validation rules — the state machine itself is unchanged from Plan 1/3.

- [ ] **Step 1: Add a failing test to the existing file**

Add this fact inside the `RealtimeOrderNotificationTests` class from Task 5 (`tests/PingMe.IntegrationTests/Api/RealtimeOrderNotificationTests.cs`), and add `using PingMe.Api.Contracts.Ordering;`'s `UpdateOrderStatusRequest` is already imported via the existing `using PingMe.Api.Contracts.Ordering;` line:
```csharp
    [Fact]
    public async Task Updating_order_status_broadcasts_OrderStatusChanged_to_the_owning_tenants_staff()
    {
        var seeded = await SeedTenantWithOneOrderableProductAsync("Venue B");
        var customerClient = _factory.CreateClient();
        var resolved = await (await customerClient.GetAsync($"/p/{seeded.QrCode}"))
            .Content.ReadFromJsonAsync<ResolveQrCodeResponse>();
        var order = await (await customerClient.PostAsJsonAsync("/orders",
                new CreateOrderRequest(resolved!.SessionId, new List<CreateOrderItemRequest> { new(seeded.Product.Id, 1) })))
            .Content.ReadFromJsonAsync<CreateOrderResponse>();

        await using var staffConnection = BuildHubConnection(seeded.Token);
        var received = new TaskCompletionSource<AdminOrderDto>();
        staffConnection.On<AdminOrderDto>("OrderStatusChanged", updated => received.TrySetResult(updated));
        await staffConnection.StartAsync();

        await seeded.OwnerClient.PutAsJsonAsync(
            $"/admin/orders/{order!.OrderId}/status", new UpdateOrderStatusRequest("Accepted"));

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(received.Task, completed);
        var broadcast = await received.Task;
        Assert.Equal(order.OrderId, broadcast.Id);
        Assert.Equal("Accepted", broadcast.Status);
        Assert.Single(broadcast.Items);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test PingMe.slnx --filter "FullyQualifiedName~Updating_order_status_broadcasts_OrderStatusChanged"`
Expected: FAIL — times out (no broadcast sent from `AdminOrdersController.UpdateStatus` yet).

- [ ] **Step 3: Wire the notifier into `AdminOrdersController`**

In `src/PingMe.Api/Controllers/AdminOrdersController.cs`, add the dependency:
```csharp
private readonly PingMeDbContext _dbContext;
private readonly IOrderNotifier _orderNotifier;

public AdminOrdersController(PingMeDbContext dbContext, IOrderNotifier orderNotifier)
{
    _dbContext = dbContext;
    _orderNotifier = orderNotifier;
}
```

Change `UpdateStatus` from:
```csharp
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateOrderStatusRequest request)
    {
        if (!Enum.TryParse<OrderStatus>(request.Status, out var targetStatus))
        {
            return BadRequest($"'{request.Status}' is not a valid order status.");
        }

        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order is null)
        {
            return NotFound();
        }

        try
        {
            order.TransitionTo(targetStatus);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }

        await _dbContext.SaveChangesAsync();
        return NoContent();
    }
```
to:
```csharp
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateOrderStatusRequest request)
    {
        if (!Enum.TryParse<OrderStatus>(request.Status, out var targetStatus))
        {
            return BadRequest($"'{request.Status}' is not a valid order status.");
        }

        var order = await _dbContext.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null)
        {
            return NotFound();
        }

        try
        {
            order.TransitionTo(targetStatus);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }

        await _dbContext.SaveChangesAsync();

        var orderDto = new AdminOrderDto(
            order.Id,
            order.Status.ToString(),
            order.CreatedAt,
            order.Items.Select(i => new AdminOrderItemDto(i.ProductName, i.UnitPrice, i.Quantity)).ToList());
        await _orderNotifier.NotifyOrderStatusChangedAsync(order.TenantId, orderDto);

        return NoContent();
    }
```

Note the added `.Include(o => o.Items)` — without it, `order.Items` would be empty at broadcast time (the same non-eager-navigation gap already fixed once in `GetOrders` during Plan 3), which is exactly what the test's `Assert.Single(broadcast.Items)` catches if missed.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test PingMe.slnx --filter "FullyQualifiedName~Updating_order_status_broadcasts_OrderStatusChanged"`
Expected: PASS.

- [ ] **Step 5: Run the full backend test suite**

Run: `dotnet test PingMe.slnx`
Expected: PASS — all 44 tests green.

- [ ] **Step 6: Commit**

```bash
git add src/PingMe.Api/Controllers/AdminOrdersController.cs tests/PingMe.IntegrationTests/Api/RealtimeOrderNotificationTests.cs
git commit -m "Broadcast OrderStatusChanged to the owning tenant's staff on status transition"
```

---

## Task 7: Scaffold the staff-app

**Files:**
- Create: `src/pingme-web/staff-app/package.json`
- Create: `src/pingme-web/staff-app/tsconfig.json`
- Create: `src/pingme-web/staff-app/tsconfig.node.json`
- Create: `src/pingme-web/staff-app/vite.config.ts`
- Create: `src/pingme-web/staff-app/index.html`
- Create: `src/pingme-web/staff-app/src/main.tsx`
- Create: `src/pingme-web/staff-app/src/vite-env.d.ts`
- Modify: `src/pingme-web/pnpm-workspace.yaml`

**Does NOT cover:** any actual dashboard UI (Tasks 8-12) — `main.tsx` imports `./App`, which does not exist until Task 12, matching Plan 3's precedent for `customer-app`'s scaffold task.

- [ ] **Step 1: Add `staff-app` to the pnpm workspace**

In `src/pingme-web/pnpm-workspace.yaml`, change:
```yaml
packages:
  - "customer-app"
allowBuilds:
  esbuild: true
```
to:
```yaml
packages:
  - "customer-app"
  - "staff-app"
allowBuilds:
  esbuild: true
```

- [ ] **Step 2: Create `package.json`**

`src/pingme-web/staff-app/package.json`:
```json
{
  "name": "pingme-staff-app",
  "private": true,
  "version": "0.0.0",
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "test": "vitest run"
  },
  "dependencies": {
    "@microsoft/signalr": "^8.0.7",
    "react": "^18.3.1",
    "react-dom": "^18.3.1"
  },
  "devDependencies": {
    "@types/react": "^18.3.3",
    "@types/react-dom": "^18.3.0",
    "@vitejs/plugin-react": "^4.3.1",
    "typescript": "^5.5.3",
    "vite": "^5.4.0",
    "vitest": "^2.0.5"
  }
}
```

- [ ] **Step 3: Create `tsconfig.json` and `tsconfig.node.json`**

`src/pingme-web/staff-app/tsconfig.json` (identical to `customer-app`'s):
```json
{
  "compilerOptions": {
    "target": "ES2020",
    "useDefineForClassFields": true,
    "lib": ["ES2020", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "skipLibCheck": true,
    "moduleResolution": "bundler",
    "resolveJsonModule": true,
    "isolatedModules": true,
    "noEmit": true,
    "jsx": "react-jsx",
    "strict": true
  },
  "include": ["src"],
  "references": [{ "path": "./tsconfig.node.json" }]
}
```

`src/pingme-web/staff-app/tsconfig.node.json` (identical to `customer-app`'s, including the `skipLibCheck` that Plan 3 discovered is required or `tsc -b` fails on Vite's own bundled `.d.ts` files):
```json
{
  "compilerOptions": {
    "composite": true,
    "module": "ESNext",
    "moduleResolution": "bundler",
    "allowSyntheticDefaultImports": true,
    "skipLibCheck": true
  },
  "include": ["vite.config.ts"]
}
```

- [ ] **Step 4: Create `vite.config.ts`**

`src/pingme-web/staff-app/vite.config.ts` (port `5174` — one above `customer-app`'s `5173`, so both dev servers can run simultaneously):
```ts
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5174,
  },
});
```

- [ ] **Step 5: Create `index.html`, `main.tsx`, `vite-env.d.ts`**

`src/pingme-web/staff-app/index.html`:
```html
<!doctype html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>PingMe Staff</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

`src/pingme-web/staff-app/src/main.tsx`:
```tsx
import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
```

`src/pingme-web/staff-app/src/vite-env.d.ts` (required for `import.meta.env` typing, per Plan 3's discovery):
```ts
/// <reference types="vite/client" />
```

- [ ] **Step 6: Install dependencies**

Run:
```bash
cd src/pingme-web
pnpm install
```
Expected: succeeds with no `ERR_PNPM_IGNORED_BUILDS` prompt (the workspace's existing `allowBuilds: { esbuild: true }` already covers the new app's transitive `esbuild` dependency).

- [ ] **Step 7: Commit**

```bash
git add src/pingme-web/pnpm-workspace.yaml src/pingme-web/staff-app/package.json src/pingme-web/staff-app/tsconfig.json src/pingme-web/staff-app/tsconfig.node.json src/pingme-web/staff-app/vite.config.ts src/pingme-web/staff-app/index.html src/pingme-web/staff-app/src/main.tsx src/pingme-web/staff-app/src/vite-env.d.ts src/pingme-web/pnpm-lock.yaml
git commit -m "Scaffold staff-app: pnpm workspace, Vite + React + TypeScript"
```

---

## Task 8: Add staff-app types and API client

**Files:**
- Create: `src/pingme-web/staff-app/src/types.ts`
- Create: `src/pingme-web/staff-app/src/api.ts`

**Does NOT cover:** the SignalR connection wrapper (Task 10) or any UI (Tasks 11-12).

- [ ] **Step 1: Create `types.ts`**

`src/pingme-web/staff-app/src/types.ts` (mirrors the backend's `AuthResponse`, `AdminOrderDto`/`AdminOrderItemDto` exactly — camelCase per ASP.NET Core's default JSON serialization):
```ts
export interface AuthResponse {
  token: string;
}

export interface AdminOrderItemDto {
  productName: string;
  unitPrice: number;
  quantity: number;
}

export interface AdminOrderDto {
  id: string;
  status: string;
  createdAt: string;
  items: AdminOrderItemDto[];
}
```

- [ ] **Step 2: Create `api.ts`**

`src/pingme-web/staff-app/src/api.ts`:
```ts
import type { AdminOrderDto, AuthResponse } from "./types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5190";

export async function login(email: string, password: string): Promise<AuthResponse> {
  const response = await fetch(`${API_BASE_URL}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });
  if (!response.ok) {
    throw new Error(`Login failed: ${response.status}`);
  }
  return response.json();
}

export async function getOrders(token: string): Promise<AdminOrderDto[]> {
  const response = await fetch(`${API_BASE_URL}/admin/orders`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!response.ok) {
    throw new Error(`Failed to fetch orders: ${response.status}`);
  }
  return response.json();
}

export async function updateOrderStatus(
  token: string,
  orderId: string,
  status: string,
): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/admin/orders/${orderId}/status`, {
    method: "PUT",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ status }),
  });
  if (!response.ok) {
    throw new Error(`Failed to update order status: ${response.status}`);
  }
}
```

- [ ] **Step 3: Type-check**

Run:
```bash
cd src/pingme-web/staff-app
pnpm exec tsc -b
```
Expected: FAILS only on the still-missing `./App` import in `main.tsx` (`TS2307: Cannot find module './App'`) — this is the same expected, deliberate gap as Plan 3's `customer-app` scaffold task, closed in Task 12. No error should reference `types.ts` or `api.ts`.

- [ ] **Step 4: Commit**

```bash
git add src/pingme-web/staff-app/src/types.ts src/pingme-web/staff-app/src/api.ts
git commit -m "Add staff-app API client and types"
```

---

## Task 9: Add order-list upsert logic with unit tests

**Files:**
- Create: `src/pingme-web/staff-app/src/orders.ts`
- Test: `src/pingme-web/staff-app/src/orders.test.ts`

**Does NOT cover:** any SignalR or fetch wiring — this is a pure function, testable in isolation, matching Plan 3's `cart.ts`/`cart.test.ts` precedent.

- [ ] **Step 1: Write failing tests**

`src/pingme-web/staff-app/src/orders.test.ts`:
```ts
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run:
```bash
cd src/pingme-web/staff-app
pnpm test
```
Expected: FAIL — `orders.ts` does not exist (`Cannot find module './orders'`).

- [ ] **Step 3: Implement `orders.ts`**

`src/pingme-web/staff-app/src/orders.ts`:
```ts
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run:
```bash
cd src/pingme-web/staff-app
pnpm test
```
Expected: PASS — all 3 facts green.

- [ ] **Step 5: Commit**

```bash
git add src/pingme-web/staff-app/src/orders.ts src/pingme-web/staff-app/src/orders.test.ts
git commit -m "Add order-list upsert logic with unit tests"
```

---

## Task 10: Add the SignalR connection wrapper

**Files:**
- Create: `src/pingme-web/staff-app/src/signalr.ts`

**Does NOT cover:** any UI wiring (Task 12) — this is a thin, testable-by-inspection wrapper around `@microsoft/signalr`'s `HubConnectionBuilder`.

- [ ] **Step 1: Create `signalr.ts`**

`src/pingme-web/staff-app/src/signalr.ts`:
```ts
import { HubConnection, HubConnectionBuilder } from "@microsoft/signalr";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5190";

export async function connectOrdersHub(token: string): Promise<HubConnection> {
  const connection = new HubConnectionBuilder()
    .withUrl(`${API_BASE_URL}/hubs/orders?access_token=${token}`)
    .withAutomaticReconnect()
    .build();

  await connection.start();
  return connection;
}
```

- [ ] **Step 2: Type-check**

Run:
```bash
cd src/pingme-web/staff-app
pnpm exec tsc -b
```
Expected: FAILS only on the still-missing `./App` import in `main.tsx`, same as Task 8's step 3 — no error referencing `signalr.ts`.

- [ ] **Step 3: Commit**

```bash
git add src/pingme-web/staff-app/src/signalr.ts
git commit -m "Add SignalR connection wrapper for the orders hub"
```

---

## Task 11: Add the OrderList component

**Files:**
- Create: `src/pingme-web/staff-app/src/components/OrderList.tsx`

**Does NOT cover:** styling/visual design beyond functional, unstyled markup — matches Plan 3's `MenuBrowser`/`CartView` precedent; a design pass is out of scope for this plan.

- [ ] **Step 1: Create `OrderList.tsx`**

```tsx
import type { AdminOrderDto } from "../types";

const NextStatus: Record<string, string | undefined> = {
  Received: "Accepted",
  Accepted: "Preparing",
  Preparing: "Ready",
  Ready: "Delivered",
  Delivered: undefined,
};

interface OrderListProps {
  orders: AdminOrderDto[];
  onAdvanceStatus: (orderId: string, nextStatus: string) => void;
  errorByOrderId: Record<string, string | undefined>;
}

export function OrderList({ orders, onAdvanceStatus, errorByOrderId }: OrderListProps) {
  if (orders.length === 0) {
    return <p>No orders yet.</p>;
  }

  return (
    <ul>
      {orders.map((order) => {
        const next = NextStatus[order.status];
        return (
          <li key={order.id}>
            <strong>{order.status}</strong> — order {order.id.slice(0, 8)}
            <ul>
              {order.items.map((item, index) => (
                <li key={index}>
                  {item.quantity} x {item.productName}
                </li>
              ))}
            </ul>
            {next && (
              <button onClick={() => onAdvanceStatus(order.id, next)}>
                Mark as {next}
              </button>
            )}
            {errorByOrderId[order.id] && <p>{errorByOrderId[order.id]}</p>}
          </li>
        );
      })}
    </ul>
  );
}
```

- [ ] **Step 2: Type-check**

Run:
```bash
cd src/pingme-web/staff-app
pnpm exec tsc -b
```
Expected: FAILS only on the still-missing `./App` import in `main.tsx` — no error referencing `OrderList.tsx`.

- [ ] **Step 3: Commit**

```bash
git add src/pingme-web/staff-app/src/components/OrderList.tsx
git commit -m "Add OrderList component"
```

---

## Task 12: Add the login gate and top-level App

**Files:**
- Create: `src/pingme-web/staff-app/src/App.tsx`

**Does NOT cover:** any staff-registration flow — login only, matching this plan's stated assumption that Staff/Owner accounts already exist.

- [ ] **Step 1: Create `App.tsx`**

This implements the connect-then-fetch sequencing from the spec's failure-mode resolution (Section 5 of the design doc): the hub connection is awaited *before* the initial `getOrders()` fetch, and every update — from the fetch or from a broadcast — goes through `upsertById`, so an order arriving during the handoff between the two can never appear twice.

```tsx
import { useState } from "react";
import { getOrders, login, updateOrderStatus } from "./api";
import { connectOrdersHub } from "./signalr";
import { upsertById } from "./orders";
import { OrderList } from "./components/OrderList";
import type { AdminOrderDto } from "./types";

export default function App() {
  const [token, setToken] = useState<string | null>(null);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loginError, setLoginError] = useState<string | null>(null);
  const [orders, setOrders] = useState<AdminOrderDto[]>([]);
  const [errorByOrderId, setErrorByOrderId] = useState<Record<string, string | undefined>>({});

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoginError(null);
    try {
      const auth = await login(email, password);
      const connection = await connectOrdersHub(auth.token);
      connection.on("OrderReceived", (order: AdminOrderDto) => {
        setOrders((current) => upsertById(current, order));
      });
      connection.on("OrderStatusChanged", (order: AdminOrderDto) => {
        setOrders((current) => upsertById(current, order));
      });

      const initialOrders = await getOrders(auth.token);
      setOrders(initialOrders);
      setToken(auth.token);
    } catch {
      setLoginError("Login failed — check your email and password.");
    }
  };

  const handleAdvanceStatus = async (orderId: string, nextStatus: string) => {
    if (!token) {
      return;
    }
    try {
      await updateOrderStatus(token, orderId, nextStatus);
      setErrorByOrderId((current) => ({ ...current, [orderId]: undefined }));
    } catch {
      setErrorByOrderId((current) => ({
        ...current,
        [orderId]: "Couldn't update — try refreshing.",
      }));
    }
  };

  if (!token) {
    return (
      <form onSubmit={handleLogin}>
        <h1>PingMe Staff</h1>
        <input
          type="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="Email"
        />
        <input
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          placeholder="Password"
        />
        <button type="submit">Log in</button>
        {loginError && <p>{loginError}</p>}
      </form>
    );
  }

  return (
    <div>
      <h1>Orders</h1>
      <OrderList orders={orders} onAdvanceStatus={handleAdvanceStatus} errorByOrderId={errorByOrderId} />
    </div>
  );
}
```

- [ ] **Step 2: Run the full staff-app test suite and build**

Run:
```bash
cd src/pingme-web/staff-app
pnpm test
pnpm build
```
Expected: PASS — Vitest suite green (the 3 `orders.test.ts` facts from Task 9), `tsc -b && vite build` completes with no TypeScript errors. This is the first point where the full `staff-app` project builds cleanly (`main.tsx`'s import of `./App` finally resolves).

- [ ] **Step 3: Commit**

```bash
git add src/pingme-web/staff-app/src/App.tsx
git commit -m "Add login gate and top-level App: login -> live order list -> status transitions"
```

---

## Task 13: Add Dockerfile and docker-compose.yml

**Files:**
- Create: `src/PingMe.Api/Dockerfile`
- Create: `docker-compose.yml`
- Modify: `.gitignore`

**Does NOT cover:** containerizing `customer-app` or `staff-app` (explicit non-goal, Section 1 of the design doc) — both keep running via `pnpm dev`. Does NOT cover any cloud deployment target or CI pipeline.

- [ ] **Step 1: Create the Dockerfile**

`src/PingMe.Api/Dockerfile`:
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

- [ ] **Step 2: Create `docker-compose.yml`**

`docker-compose.yml` at the repo root (Postgres mapped to host port `55432`, not the native `5432` already used by the existing local dev Postgres, to avoid a port collision between the two workflows):
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

Migrations are not applied automatically on container startup — run `dotnet ef database update` against the compose Postgres instance (`Host=localhost;Port=55432;...`) the same way migrations are already applied manually against the native local dev instance today; this plan does not add migrate-on-startup logic.

- [ ] **Step 3: Verify the compose file**

Run: `docker compose config`
Expected: if the Docker CLI is available, this prints the fully resolved compose configuration with no errors. **If the Docker CLI is not available in the current environment** (confirmed absent during this plan's design), skip execution and instead manually re-read `docker-compose.yml` against the YAML above to confirm it's syntactically identical — do not claim a live smoke test (`docker compose up`) was performed if Docker isn't available; note this limitation explicitly in the task's completion notes and flag it to the user as a recommended manual check.

- [ ] **Step 4: Update `.gitignore`**

Add a rule so the frontend apps' `dist/` build output under `src/pingme-web` (already covered by the existing generic `dist/` rule) isn't accidentally mixed up with Docker build context expectations — verify the existing `.gitignore` already has `dist/` and `node_modules/` (it does, from Plan 3); no new rule is needed unless `docker compose config` surfaces one. If it doesn't, skip this file entirely and remove it from the commit in Step 5.

- [ ] **Step 5: Commit**

```bash
git add src/PingMe.Api/Dockerfile docker-compose.yml
git commit -m "Add Dockerfile and docker-compose.yml for local API+Postgres deployment"
```

---

## Task 14: Final verification and progress tracking

**Files:**
- Modify: `Docs/superpowers optimized/plans/PROGRESS.md`

- [ ] **Step 1: Run the full backend build and test suite from a clean state**

Run: `dotnet build PingMe.slnx && dotnet test PingMe.slnx`
Expected: PASS — 0 build errors, all 44 tests passing (see Task 6, Step 5 for the breakdown).

- [ ] **Step 2: Run the full frontend build and test suite for both apps from a clean state**

Run:
```bash
cd src/pingme-web
pnpm install
cd customer-app && pnpm test && pnpm build && cd ..
cd staff-app && pnpm test && pnpm build && cd ..
```
Expected: PASS — no install errors, both apps' Vitest suites green, both production builds succeed.

- [ ] **Step 3: Manually verify the staff flow end-to-end** (not automatable in this plan — do this once by hand)

1. Start the backend: `dotnet run --project src/PingMe.Api`.
2. Start the staff app: `cd src/pingme-web/staff-app && pnpm dev`.
3. Start the customer app: `cd src/pingme-web/customer-app && pnpm dev`.
4. Log into the staff app (`http://localhost:5174`) with an Owner account (register one via Swagger if needed).
5. In another browser tab, scan/visit a QR code in the customer app and place an order.
6. Confirm the order appears in the staff dashboard within a second or two, without a page refresh.
7. Click through the status buttons (`Mark as Accepted` → `Preparing` → `Ready` → `Delivered`) and confirm each transition updates instantly in the dashboard and the button disappears once `Delivered` is reached.
8. Stop both frontend dev servers and the backend afterward.

If a real browser isn't available to perform this check (as was the case during Plan 3's equivalent step), perform the curl/SignalR-client-level verification instead (the integration tests in Tasks 4-6 already prove the wire protocol end-to-end) and note explicitly in the completion report that interactive browser verification is still recommended before considering this plan fully closed.

- [ ] **Step 4: Update `Docs/superpowers optimized/plans/PROGRESS.md`**

Update the table to add this plan's row (`4 | SignalR + Staff dashboard + Deployment | ... | Done, reviewed | 14 / 14`), replacing the `*(not yet written)*` placeholder. Also add a new carried-forward item: **"Docker Compose covers API + Postgres only — customer-app and staff-app are not containerized and still require `pnpm dev`. No CI pipeline or cloud deployment target exists yet."**

- [ ] **Step 5: Commit**

```bash
git add "Docs/superpowers optimized/plans/PROGRESS.md"
git commit -m "Close Plan 4: mark done, note Docker Compose scope and remaining deployment gaps"
```

---

## Self-Review

**1. Spec coverage.**
- Section 2 (SignalR Hub): delivered — `OrdersHub` with tenant-group join (Task 3), JWT-over-querystring auth (Task 3), `IOrderNotifier`/`SignalROrderNotifier` (Tasks 2, 4), call sites in `OrdersController`/`AdminOrdersController` (Tasks 5, 6).
- Section 3 (Staff Dashboard): delivered — `staff-app` scaffold (Task 7), API client (Task 8), connect-then-fetch sequencing with `upsertById` dedup (Tasks 9, 12), `OrderList` (Task 11), login gate (Task 12).
- Section 4 (Local Docker Compose): delivered — `Dockerfile` + `docker-compose.yml` with the non-conflicting Postgres port (Task 13).
- Section 5 (Failure-Mode Check): failure mode 1 (duplication race) resolved by Task 12's connect-then-fetch-then-upsert sequencing, verified structurally (no live browser test, but the same logic is unit-tested via `orders.test.ts`, Task 9); failure mode 2 (stale JWT) documented as a non-goal in the plan header's Assumptions; failure mode 3 (port collision) resolved by Task 13's `55432` mapping.
- Section 6 (Testing Strategy): backend tenant-isolation SignalR test delivered (Task 4); `upsertById` unit tests delivered (Task 9); no e2e browser test, matching the spec's own stated ceiling.
- Section 7 (Rollout Notes): the `AdminOrderDto`/`AdminOrderItemDto` relocation is Task 1, done first so every later task can depend on the new location without rework.

**2. Placeholder scan.** No "TBD"/"TODO" strings. Every code block is complete, runnable code. Task 13's `.gitignore` step is conditional but resolves to a concrete "skip if not needed" instruction, not an open-ended placeholder.

**3. Type consistency.** `AdminOrderDto`/`AdminOrderItemDto` are defined once (Task 1) and referenced identically (same property names/casing) in Tasks 4, 5, 6's backend tests, and mirrored field-for-field in the frontend's `types.ts` (Task 8) — verified against ASP.NET Core's default camelCase serialization the same way Plan 3's DTOs were. `IOrderNotifier`'s two method names (`NotifyOrderReceivedAsync`, `NotifyOrderStatusChangedAsync`, Task 2) are used identically in `SignalROrderNotifier` (Task 4) and both controller call sites (Tasks 5, 6). `upsertById`'s signature (Task 9) matches its usage in `App.tsx` (Task 12) exactly.

---

## Execution Handoff

**Plan complete and saved to `Docs/superpowers optimized/plans/2026-08-26-pingme-plan-4-signalr-staff-deployment.md`. Two execution options:**

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** — Execute tasks in this session using executing-plans, with checkpoints

**Which approach?**
