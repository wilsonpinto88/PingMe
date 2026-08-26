# PingMe Plan 3: Locations/QR + Customer App + Ordering — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-optimized:subagent-driven-development (recommended) or superpowers-optimized:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a customer scan a QR code, browse the venue's menu, and place an order that staff can see and progress through its status lifecycle — without the customer ever needing an account.

**Architecture:** Adds two backend concerns to the existing layered `PingMe.{Domain,Application,Infrastructure,Api}` solution — Owner-only admin CRUD for `Location`/`QrCode` (same controller pattern as Plan 2's `MenusController`/`ProductsController`), and anonymous customer-facing endpoints (`GET /p/{code}`, `POST /orders`, `GET /orders/{id}/status`) that resolve tenant identity from the scanned QR code or an existing `CustomerSession` rather than a JWT claim — mirroring how `AuthController.Login` already bypasses the tenant filter via `IgnoreQueryFilters()` before tenant context exists. A minimal React + TypeScript customer app (Vite, pnpm) consumes these endpoints: scan → menu → cart → order → status polling.

**Tech Stack:** ASP.NET Core / .NET 10 (existing), EF Core + PostgreSQL (existing, no schema changes needed — `Location`, `Venue`, `CustomerSession`, `QrCode`, `Order`, `OrderItem` all already exist from Plan 1), React 18 + TypeScript + Vite + pnpm (new, first frontend code in the repo), Vitest for frontend unit tests, xUnit for backend tests (existing).

**Assumptions:**
- Single Venue per Tenant (per the approved MVP spec) — `AuthController.RegisterTenant` is extended to auto-create that Venue at registration, since no other flow creates one and `Location.VenueId` is required. Will NOT work if a tenant later needs multiple Venues — that's an explicit non-goal carried from the MVP spec.
- `CustomerSession` TTL is 4 hours from creation, matching a typical dine-in duration. Not configurable per-venue in this plan — a fixed constant. Will NOT suit a venue type needing a much longer/shorter window (e.g. an all-day festival pass) — documented as a non-goal.
- Product options (`ProductOption`, built in Plan 2) are **not** exposed to the customer flow in this plan — `OrderItem` has no field to record a chosen option, so surfacing them to the customer without a way to store the choice would be misleading. Customer menu DTOs omit options entirely. Will NOT work if the business needs customer-selectable add-ons before this is revisited.
- This plan builds the **customer** app only, not an Owner/Staff Admin UI. Locations and QR codes are created via the existing Swagger UI in this plan (same as how Plan 2's admin endpoints are exercised without a dedicated frontend). **This leaves a real gap**: no plan in the current 1–5 sequence explicitly owns an Owner-facing Admin React app (Plan 4 only covers the Staff order dashboard). Flagged in this plan's PROGRESS.md update as a carried-forward item for a future plan.
- Order status transition authorization is `Owner,Staff` (both configured admin roles) — Staff needs this for their dashboard (Plan 4); Owner needs it for oversight.

---

## File Structure

Backend (no new projects, following the existing per-module folder convention):

```
src/PingMe.Api/Contracts/Locations/
  LocationDto.cs, CreateLocationRequest.cs
  QrCodeDto.cs, CreateQrCodeRequest.cs
src/PingMe.Api/Contracts/Ordering/
  ResolveQrCodeResponse.cs, CustomerMenuDto.cs, CustomerCategoryDto.cs, CustomerProductDto.cs
  CreateOrderRequest.cs, CreateOrderItemRequest.cs, CreateOrderResponse.cs, OrderStatusResponse.cs
  AdminOrderDto.cs, AdminOrderItemDto.cs, UpdateOrderStatusRequest.cs
src/PingMe.Api/Controllers/
  LocationsController.cs       (admin/locations — Owner-only; also close-session)
  QrCodesController.cs         (admin/qrcodes — Owner-only)
  QrResolutionController.cs    (p — anonymous)
  OrdersController.cs          (orders — anonymous, customer-facing)
  AdminOrdersController.cs     (admin/orders — Owner,Staff)
src/PingMe.Api/Controllers/AuthController.cs   (modified — auto-create Venue on register)

tests/PingMe.IntegrationTests/Api/
  LocationsAndQrCodesTests.cs
  CustomerOrderingFlowTests.cs
  OrderingIsolationTests.cs
```

Frontend (new — first frontend code in the repo, matches README's stated `src/pingme-web` pnpm-monorepo location):

```
src/pingme-web/
  pnpm-workspace.yaml
  package.json
  customer-app/
    package.json, tsconfig.json, vite.config.ts, index.html
    src/main.tsx, src/App.tsx
    src/api.ts, src/types.ts
    src/cart.ts, src/cart.test.ts
    src/components/MenuBrowser.tsx
    src/components/CartView.tsx
    src/components/OrderStatus.tsx
```

---

## Task 1: Auto-create the tenant's Venue at registration

**Files:**
- Modify: `src/PingMe.Api/Controllers/AuthController.cs`
- Modify: `tests/PingMe.IntegrationTests/Api/AuthTests.cs`

**Does NOT cover:** letting a tenant create additional Venues later — single Venue per Tenant is a hard MVP assumption (see plan Assumptions).

- [x] **Step 1: Write a failing test asserting a Venue exists after registration** (strengthened during review to actually assert Venue existence via `PingMeDbContext`, not just a registration smoke test — see commit)

Add to `tests/PingMe.IntegrationTests/Api/AuthTests.cs` (inside the existing `AuthTests` class, using the existing `_client` field):

```csharp
    [Fact]
    public async Task RegisterTenant_also_creates_the_tenants_single_venue()
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";

        var response = await _client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Test Venue", email, "P@ssw0rd123"));

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var loginResponse = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "P@ssw0rd123"));
        Assert.Equal(System.Net.HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
    }
```

This test alone doesn't prove a `Venue` row exists yet (there's no endpoint to check it through) — Task 2's `LocationsController` will prove it indirectly by requiring a Venue to exist for `POST /admin/locations` to succeed. For now, run it to confirm registration still works after the change in Step 3.

- [x] **Step 2: Run the test to verify current behavior**

Run: `dotnet test tests/PingMe.IntegrationTests/PingMe.IntegrationTests.csproj --filter FullyQualifiedName~RegisterTenant_also_creates_the_tenants_single_venue`
Expected: PASS (this test doesn't yet assert Venue existence, so it passes before and after — it's a smoke test for the modified endpoint, not a TDD gate. The real proof comes from Task 2's tests requiring a Venue to exist.)

- [x] **Step 3: Modify `RegisterTenant` to create the Venue**

In `src/PingMe.Api/Controllers/AuthController.cs`, add the import and modify `RegisterTenant`:

```csharp
using PingMe.Domain.Locations;
```

Change:

```csharp
        var tenant = new Tenant(request.TenantName, DateTime.UtcNow);
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();
```

to:

```csharp
        var tenant = new Tenant(request.TenantName, DateTime.UtcNow);
        _dbContext.Tenants.Add(tenant);

        var venue = new Venue(tenant.Id, request.TenantName);
        _dbContext.Venues.Add(venue);

        await _dbContext.SaveChangesAsync();
```

- [x] **Step 4: Run the full test suite**

Run: `dotnet build PingMe.slnx && dotnet test PingMe.slnx`
Expected: PASS, 0 build errors, all existing tests plus the new one green.

- [x] **Step 5: Commit**

```bash
git add src/PingMe.Api/Controllers/AuthController.cs tests/PingMe.IntegrationTests/Api/AuthTests.cs
git commit -m "Auto-create the tenant's single Venue at registration"
```

---

## Task 2: LocationsController — create and list Locations (Owner-only)

**Files:**
- Create: `src/PingMe.Api/Contracts/Locations/LocationDto.cs`
- Create: `src/PingMe.Api/Contracts/Locations/CreateLocationRequest.cs`
- Create: `src/PingMe.Api/Controllers/LocationsController.cs`

**Does NOT cover:** editing or deleting a Location, or validating that `ParentLocationId` belongs to the same tenant beyond the existing tenant query filter already making a cross-tenant parent invisible (see Task where isolation is tested).

- [x] **Step 1: Create the DTOs**

`src/PingMe.Api/Contracts/Locations/LocationDto.cs`:

```csharp
namespace PingMe.Api.Contracts.Locations;

public record LocationDto(Guid Id, string Name, Guid? ParentLocationId);
```

`src/PingMe.Api/Contracts/Locations/CreateLocationRequest.cs`:

```csharp
namespace PingMe.Api.Contracts.Locations;

public record CreateLocationRequest(string Name, Guid? ParentLocationId);
```

- [x] **Step 2: Create `LocationsController.cs`**

```csharp
namespace PingMe.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PingMe.Api.Contracts.Locations;
using PingMe.Application.Tenants;
using PingMe.Domain.Locations;
using PingMe.Infrastructure.Persistence;

[ApiController]
[Route("admin/locations")]
[Authorize(Roles = "Owner")]
public class LocationsController : ControllerBase
{
    private readonly PingMeDbContext _dbContext;
    private readonly ICurrentTenantProvider _currentTenantProvider;

    public LocationsController(PingMeDbContext dbContext, ICurrentTenantProvider currentTenantProvider)
    {
        _dbContext = dbContext;
        _currentTenantProvider = currentTenantProvider;
    }

    [HttpGet]
    public async Task<ActionResult<List<LocationDto>>> GetLocations()
    {
        var locations = await _dbContext.Locations
            .Select(l => new LocationDto(l.Id, l.Name, l.ParentLocationId))
            .ToListAsync();
        return Ok(locations);
    }

    [HttpPost]
    public async Task<ActionResult<LocationDto>> CreateLocation(CreateLocationRequest request)
    {
        if (request.ParentLocationId is not null)
        {
            var parentExists = await _dbContext.Locations.AnyAsync(l => l.Id == request.ParentLocationId);
            if (!parentExists)
            {
                return NotFound("The specified parent location was not found.");
            }
        }

        var venue = await _dbContext.Venues.FirstAsync();
        var location = new Location(_currentTenantProvider.TenantId!.Value, venue.Id, request.Name, request.ParentLocationId);
        _dbContext.Locations.Add(location);
        await _dbContext.SaveChangesAsync();
        return Created(string.Empty, new LocationDto(location.Id, location.Name, location.ParentLocationId));
    }

    [HttpPost("{id}/close-session")]
    [Authorize(Roles = "Owner,Staff")]
    public async Task<IActionResult> CloseSession(Guid id)
    {
        var session = await _dbContext.CustomerSessions
            .FirstOrDefaultAsync(s => s.LocationId == id && s.ClosedAt == null);
        if (session is null)
        {
            return NotFound();
        }

        session.Close(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }
}
```

`venue.Id` comes from `_dbContext.Venues.FirstAsync()` — safe under the single-Venue-per-Tenant assumption (Task 1 guarantees exactly one exists per tenant). `[Authorize(Roles = "Owner,Staff")]` on `CloseSession` overrides the controller-level `Owner`-only policy for that one action, since Staff also need to close a session after a table pays (per the MVP spec).

- [x] **Step 3: Verify the solution builds**

Run: `dotnet build PingMe.slnx`
Expected: PASS

- [x] **Step 4: Commit**

```bash
git add src/PingMe.Api/Contracts/Locations src/PingMe.Api/Controllers/LocationsController.cs
git commit -m "Add LocationsController: create/list locations, close a location's session"
```

---

## Task 3: QrCodesController — create and list QR codes (Owner-only)

**Files:**
- Create: `src/PingMe.Api/Contracts/Locations/QrCodeDto.cs`
- Create: `src/PingMe.Api/Contracts/Locations/CreateQrCodeRequest.cs`
- Create: `src/PingMe.Api/Controllers/QrCodesController.cs`

**Does NOT cover:** deleting/deactivating a QR code, or generating a printable/scannable image — this plan only produces the opaque code string that a QR image generator (out of scope) would encode as a URL.

- [x] **Step 1: Create the DTOs**

`src/PingMe.Api/Contracts/Locations/QrCodeDto.cs`:

```csharp
namespace PingMe.Api.Contracts.Locations;

public record QrCodeDto(Guid Id, Guid LocationId, string Code);
```

`src/PingMe.Api/Contracts/Locations/CreateQrCodeRequest.cs`:

```csharp
namespace PingMe.Api.Contracts.Locations;

public record CreateQrCodeRequest(Guid LocationId);
```

- [x] **Step 2: Create `QrCodesController.cs`** (during review, strengthened `CreateQrCode` with a `GenerateUniqueCodeAsync` retry-on-collision check — a code collision would be a real cross-tenant leak since resolution bypasses the tenant filter)

```csharp
namespace PingMe.Api.Controllers;

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PingMe.Api.Contracts.Locations;
using PingMe.Application.Tenants;
using PingMe.Domain.Locations;
using PingMe.Infrastructure.Persistence;

[ApiController]
[Route("admin/qrcodes")]
[Authorize(Roles = "Owner")]
public class QrCodesController : ControllerBase
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly PingMeDbContext _dbContext;
    private readonly ICurrentTenantProvider _currentTenantProvider;

    public QrCodesController(PingMeDbContext dbContext, ICurrentTenantProvider currentTenantProvider)
    {
        _dbContext = dbContext;
        _currentTenantProvider = currentTenantProvider;
    }

    [HttpGet]
    public async Task<ActionResult<List<QrCodeDto>>> GetQrCodes()
    {
        var qrCodes = await _dbContext.QrCodes
            .Select(q => new QrCodeDto(q.Id, q.LocationId, q.Code))
            .ToListAsync();
        return Ok(qrCodes);
    }

    [HttpPost]
    public async Task<ActionResult<QrCodeDto>> CreateQrCode(CreateQrCodeRequest request)
    {
        var locationExists = await _dbContext.Locations.AnyAsync(l => l.Id == request.LocationId);
        if (!locationExists)
        {
            return NotFound();
        }

        var qrCode = new QrCode(_currentTenantProvider.TenantId!.Value, request.LocationId, GenerateCode());
        _dbContext.QrCodes.Add(qrCode);
        await _dbContext.SaveChangesAsync();
        return Created(string.Empty, new QrCodeDto(qrCode.Id, qrCode.LocationId, qrCode.Code));
    }

    private static string GenerateCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(8);
        var builder = new StringBuilder(8);
        foreach (var b in bytes)
        {
            builder.Append(CodeAlphabet[b % CodeAlphabet.Length]);
        }

        return builder.ToString();
    }
}
```

`CodeAlphabet` excludes visually ambiguous characters (`0`/`O`, `1`/`I`) since a human might need to type the code manually if a scanner fails.

- [x] **Step 3: Verify the solution builds**

Run: `dotnet build PingMe.slnx`
Expected: PASS

- [x] **Step 4: Commit**

```bash
git add src/PingMe.Api/Contracts/Locations/QrCodeDto.cs src/PingMe.Api/Contracts/Locations/CreateQrCodeRequest.cs src/PingMe.Api/Controllers/QrCodesController.cs
git commit -m "Add QrCodesController: create/list QR codes"
```

---

## Task 4: LocationsAndQrCodesTests — integration coverage for Tasks 1–3

**Files:**
- Create: `tests/PingMe.IntegrationTests/Api/LocationsAndQrCodesTests.cs`

- [x] **Step 1: Create the test file**

```csharp
namespace PingMe.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PingMe.Api.Contracts.Auth;
using PingMe.Api.Contracts.Locations;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class LocationsAndQrCodesTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public LocationsAndQrCodesTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<HttpClient> RegisterAndAuthenticateAsync(PingMeWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Venue", email, "P@ssw0rd123"));
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "P@ssw0rd123"));
        var body = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    [Fact]
    public async Task Owner_can_create_a_location_after_registering()
    {
        var client = await RegisterAndAuthenticateAsync(_factory);

        var response = await client.PostAsJsonAsync("/admin/locations", new CreateLocationRequest("Table 12", null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var location = await response.Content.ReadFromJsonAsync<LocationDto>();
        Assert.Equal("Table 12", location!.Name);
        Assert.Null(location.ParentLocationId);
    }

    [Fact]
    public async Task Owner_can_create_a_nested_location()
    {
        var client = await RegisterAndAuthenticateAsync(_factory);
        var parentResponse = await client.PostAsJsonAsync("/admin/locations", new CreateLocationRequest("Section B", null));
        var parent = await parentResponse.Content.ReadFromJsonAsync<LocationDto>();

        var childResponse = await client.PostAsJsonAsync("/admin/locations", new CreateLocationRequest("Row 12", parent!.Id));

        Assert.Equal(HttpStatusCode.Created, childResponse.StatusCode);
        var child = await childResponse.Content.ReadFromJsonAsync<LocationDto>();
        Assert.Equal(parent.Id, child!.ParentLocationId);
    }

    [Fact]
    public async Task Creating_a_location_with_unknown_parent_returns_404()
    {
        var client = await RegisterAndAuthenticateAsync(_factory);

        var response = await client.PostAsJsonAsync(
            "/admin/locations", new CreateLocationRequest("Row 12", Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Owner_can_create_a_qr_code_for_a_location()
    {
        var client = await RegisterAndAuthenticateAsync(_factory);
        var locationResponse = await client.PostAsJsonAsync("/admin/locations", new CreateLocationRequest("Table 1", null));
        var location = await locationResponse.Content.ReadFromJsonAsync<LocationDto>();

        var response = await client.PostAsJsonAsync("/admin/qrcodes", new CreateQrCodeRequest(location!.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var qrCode = await response.Content.ReadFromJsonAsync<QrCodeDto>();
        Assert.Equal(location.Id, qrCode!.LocationId);
        Assert.Equal(8, qrCode.Code.Length);
    }

    [Fact]
    public async Task Creating_a_qr_code_for_unknown_location_returns_404()
    {
        var client = await RegisterAndAuthenticateAsync(_factory);

        var response = await client.PostAsJsonAsync("/admin/qrcodes", new CreateQrCodeRequest(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
```

- [x] **Step 2: Run the tests**

Run: `dotnet test tests/PingMe.IntegrationTests/PingMe.IntegrationTests.csproj --filter FullyQualifiedName~LocationsAndQrCodesTests`
Expected: PASS — all 5 facts green.

- [x] **Step 3: Commit**

```bash
git add tests/PingMe.IntegrationTests/Api/LocationsAndQrCodesTests.cs
git commit -m "Add integration tests for LocationsController and QrCodesController"
```

---

## Task 5: QR resolution endpoint — GET /p/{code}

**Files:**
- Create: `src/PingMe.Api/Contracts/Ordering/CustomerProductDto.cs`
- Create: `src/PingMe.Api/Contracts/Ordering/CustomerCategoryDto.cs`
- Create: `src/PingMe.Api/Contracts/Ordering/CustomerMenuDto.cs`
- Create: `src/PingMe.Api/Contracts/Ordering/ResolveQrCodeResponse.cs`
- Create: `src/PingMe.Api/Controllers/QrResolutionController.cs`

**Does NOT cover:** rate-limiting or otherwise throttling repeated scans of the same code — out of scope for this MVP plan.

- [x] **Step 1: Create the DTOs**

`src/PingMe.Api/Contracts/Ordering/CustomerProductDto.cs`:

```csharp
namespace PingMe.Api.Contracts.Ordering;

public record CustomerProductDto(Guid Id, string Name, decimal Price);
```

`src/PingMe.Api/Contracts/Ordering/CustomerCategoryDto.cs`:

```csharp
namespace PingMe.Api.Contracts.Ordering;

public record CustomerCategoryDto(Guid Id, string Name, int SortOrder, List<CustomerProductDto> Products);
```

`src/PingMe.Api/Contracts/Ordering/CustomerMenuDto.cs`:

```csharp
namespace PingMe.Api.Contracts.Ordering;

public record CustomerMenuDto(Guid Id, string Name, List<CustomerCategoryDto> Categories);
```

`src/PingMe.Api/Contracts/Ordering/ResolveQrCodeResponse.cs`:

```csharp
namespace PingMe.Api.Contracts.Ordering;

public record ResolveQrCodeResponse(Guid SessionId, string VenueName, string LocationLabel, List<CustomerMenuDto> Menus);
```

- [x] **Step 2: Create `QrResolutionController.cs`**

```csharp
namespace PingMe.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PingMe.Api.Contracts.Ordering;
using PingMe.Domain.Locations;
using PingMe.Infrastructure.Persistence;
using PingMe.Infrastructure.Tenants;

[ApiController]
[Route("p")]
[AllowAnonymous]
public class QrResolutionController : ControllerBase
{
    private static readonly TimeSpan SessionDuration = TimeSpan.FromHours(4);

    private readonly PingMeDbContext _dbContext;
    private readonly CurrentTenantProvider _currentTenantProvider;

    public QrResolutionController(PingMeDbContext dbContext, CurrentTenantProvider currentTenantProvider)
    {
        _dbContext = dbContext;
        _currentTenantProvider = currentTenantProvider;
    }

    [HttpGet("{code}")]
    public async Task<ActionResult<ResolveQrCodeResponse>> Resolve(string code)
    {
        var qrCode = await _dbContext.QrCodes.IgnoreQueryFilters().FirstOrDefaultAsync(q => q.Code == code);
        if (qrCode is null)
        {
            return NotFound();
        }

        _currentTenantProvider.TenantId = qrCode.TenantId;

        var location = await _dbContext.Locations.FirstAsync(l => l.Id == qrCode.LocationId);
        var venue = await _dbContext.Venues.FirstAsync();

        var now = DateTime.UtcNow;
        var session = await _dbContext.CustomerSessions
            .FirstOrDefaultAsync(s => s.LocationId == location.Id && s.ClosedAt == null && s.ExpiresAt > now);
        if (session is null)
        {
            session = new CustomerSession(qrCode.TenantId, location.Id, now, now.Add(SessionDuration));
            _dbContext.CustomerSessions.Add(session);
            await _dbContext.SaveChangesAsync();
        }

        var menus = await _dbContext.Menus.ToListAsync();
        var categories = await _dbContext.Categories.OrderBy(c => c.SortOrder).ToListAsync();
        var products = await _dbContext.Products.Where(p => p.IsAvailable).ToListAsync();

        var menuDtos = menus
            .Select(m => new CustomerMenuDto(
                m.Id,
                m.Name,
                categories
                    .Where(c => c.MenuId == m.Id)
                    .Select(c => new CustomerCategoryDto(
                        c.Id,
                        c.Name,
                        c.SortOrder,
                        products.Where(p => p.CategoryId == c.Id)
                            .Select(p => new CustomerProductDto(p.Id, p.Name, p.Price))
                            .ToList()))
                    .ToList()))
            .ToList();

        return Ok(new ResolveQrCodeResponse(session.Id, venue.Name, location.Name, menuDtos));
    }
}
```

The controller injects the concrete `CurrentTenantProvider` (not the `ICurrentTenantProvider` interface, which only exposes a getter) so it can set the resolved tenant for the rest of the request — the same pattern `TenantResolutionMiddleware` already uses for authenticated requests, just triggered manually here since there's no JWT to read a `tenantId` claim from.

- [x] **Step 3: Verify the solution builds**

Run: `dotnet build PingMe.slnx`
Expected: PASS

- [x] **Step 4: Commit**

```bash
git add src/PingMe.Api/Contracts/Ordering/CustomerProductDto.cs src/PingMe.Api/Contracts/Ordering/CustomerCategoryDto.cs src/PingMe.Api/Contracts/Ordering/CustomerMenuDto.cs src/PingMe.Api/Contracts/Ordering/ResolveQrCodeResponse.cs src/PingMe.Api/Controllers/QrResolutionController.cs
git commit -m "Add QR resolution endpoint: GET /p/{code} creates/resumes a CustomerSession"
```

---

## Task 6: Customer order placement — POST /orders

**Files:**
- Create: `src/PingMe.Api/Contracts/Ordering/CreateOrderItemRequest.cs`
- Create: `src/PingMe.Api/Contracts/Ordering/CreateOrderRequest.cs`
- Create: `src/PingMe.Api/Contracts/Ordering/CreateOrderResponse.cs`
- Create: `src/PingMe.Api/Controllers/OrdersController.cs`

**Does NOT cover:** payment of any kind (explicit MVP non-goal, see the approved spec), or letting the client specify `ProductOption` choices (see plan Assumptions).

- [x] **Step 1: Create the DTOs**

`src/PingMe.Api/Contracts/Ordering/CreateOrderItemRequest.cs`:

```csharp
namespace PingMe.Api.Contracts.Ordering;

public record CreateOrderItemRequest(Guid ProductId, int Quantity);
```

`src/PingMe.Api/Contracts/Ordering/CreateOrderRequest.cs`:

```csharp
namespace PingMe.Api.Contracts.Ordering;

public record CreateOrderRequest(Guid SessionId, List<CreateOrderItemRequest> Items);
```

`src/PingMe.Api/Contracts/Ordering/CreateOrderResponse.cs`:

```csharp
namespace PingMe.Api.Contracts.Ordering;

public record CreateOrderResponse(Guid OrderId, string Status);
```

- [x] **Step 2: Create `OrdersController.cs`** (during review, added a null guard on `request.Items` — an omitted/null `items` field would otherwise NullReferenceException into a 500 on this anonymous public endpoint)

```csharp
namespace PingMe.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PingMe.Api.Contracts.Ordering;
using PingMe.Domain.Ordering;
using PingMe.Infrastructure.Persistence;
using PingMe.Infrastructure.Tenants;

[ApiController]
[Route("orders")]
[AllowAnonymous]
public class OrdersController : ControllerBase
{
    private readonly PingMeDbContext _dbContext;
    private readonly CurrentTenantProvider _currentTenantProvider;

    public OrdersController(PingMeDbContext dbContext, CurrentTenantProvider currentTenantProvider)
    {
        _dbContext = dbContext;
        _currentTenantProvider = currentTenantProvider;
    }

    [HttpPost]
    public async Task<ActionResult<CreateOrderResponse>> Create(CreateOrderRequest request)
    {
        var session = await _dbContext.CustomerSessions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == request.SessionId);
        if (session is null)
        {
            return NotFound("Session not found.");
        }

        var now = DateTime.UtcNow;
        if (session.ClosedAt is not null)
        {
            return Conflict("This session has been closed.");
        }

        if (session.ExpiresAt <= now)
        {
            return StatusCode(StatusCodes.Status410Gone, "This session has expired.");
        }

        if (request.Items.Count == 0)
        {
            return BadRequest("An order must contain at least one item.");
        }

        if (request.Items.Any(i => i.Quantity < 1))
        {
            return BadRequest("Quantity must be at least 1 for every item.");
        }

        _currentTenantProvider.TenantId = session.TenantId;

        var order = new Order(session.TenantId, session.Id, now);
        foreach (var item in request.Items)
        {
            var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);
            if (product is null)
            {
                return NotFound($"Product {item.ProductId} was not found.");
            }

            if (!product.IsAvailable)
            {
                return Conflict($"Product '{product.Name}' is not currently available.");
            }

            order.AddItem(product.Id, product.Name, product.Price, item.Quantity);
        }

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        return Created(string.Empty, new CreateOrderResponse(order.Id, order.Status.ToString()));
    }
}
```

Setting `_currentTenantProvider.TenantId = session.TenantId` before the product lookups is what makes cross-tenant rejection automatic: the global tenant query filter on `Products` means a `productId` belonging to a different tenant than the session's is simply invisible, producing `404` rather than a cross-tenant order line — this is the exact mechanism the carried-forward security test in Task 8 verifies.

- [x] **Step 3: Verify the solution builds**

Run: `dotnet build PingMe.slnx`
Expected: PASS

- [x] **Step 4: Commit**

```bash
git add src/PingMe.Api/Contracts/Ordering/CreateOrderItemRequest.cs src/PingMe.Api/Contracts/Ordering/CreateOrderRequest.cs src/PingMe.Api/Contracts/Ordering/CreateOrderResponse.cs src/PingMe.Api/Controllers/OrdersController.cs
git commit -m "Add customer order placement: POST /orders"
```

---

## Task 7: Order status polling — GET /orders/{id}/status

**Files:**
- Create: `src/PingMe.Api/Contracts/Ordering/OrderStatusResponse.cs`
- Modify: `src/PingMe.Api/Controllers/OrdersController.cs`

**Does NOT cover:** SignalR push notifications — that's Plan 4. This is explicitly the polling fallback the approved spec calls for.

- [ ] **Step 1: Create the DTO**

`src/PingMe.Api/Contracts/Ordering/OrderStatusResponse.cs`:

```csharp
namespace PingMe.Api.Contracts.Ordering;

public record OrderStatusResponse(Guid OrderId, string Status);
```

- [ ] **Step 2: Add the status endpoint to `OrdersController.cs`**

Add this action inside the existing `OrdersController` class, after `Create`:

```csharp
    [HttpGet("{id}/status")]
    public async Task<ActionResult<OrderStatusResponse>> GetStatus(Guid id, [FromQuery] Guid sessionId)
    {
        var session = await _dbContext.CustomerSessions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null)
        {
            return NotFound();
        }

        _currentTenantProvider.TenantId = session.TenantId;

        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order is null || order.CustomerSessionId != session.Id)
        {
            return NotFound();
        }

        return Ok(new OrderStatusResponse(order.Id, order.Status.ToString()));
    }
```

`sessionId` is required as a query parameter (not just the order id) so a client can't poll an arbitrary order id without knowing the session it belongs to — `order.CustomerSessionId != session.Id` rejects a session/order mismatch with the same `404` as a nonexistent order, revealing nothing about whether the order id exists at all.

- [ ] **Step 3: Verify the solution builds**

Run: `dotnet build PingMe.slnx`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add src/PingMe.Api/Contracts/Ordering/OrderStatusResponse.cs src/PingMe.Api/Controllers/OrdersController.cs
git commit -m "Add order status polling: GET /orders/{id}/status"
```

---

## Task 8: End-to-end customer ordering flow tests

**Files:**
- Create: `tests/PingMe.IntegrationTests/Api/CustomerOrderingFlowTests.cs`

- [ ] **Step 1: Create the test file**

```csharp
namespace PingMe.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PingMe.Api.Contracts.Auth;
using PingMe.Api.Contracts.Catalog;
using PingMe.Api.Contracts.Locations;
using PingMe.Api.Contracts.Ordering;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class CustomerOrderingFlowTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public CustomerOrderingFlowTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record SeededTenant(HttpClient OwnerClient, ProductDto Product, string QrCode);

    private static async Task<SeededTenant> SeedTenantWithOneOrderableProductAsync(PingMeWebApplicationFactory factory)
    {
        var ownerClient = factory.CreateClient();
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await ownerClient.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Venue", email, "P@ssw0rd123"));
        var loginResponse = await ownerClient.PostAsJsonAsync("/auth/login", new LoginRequest(email, "P@ssw0rd123"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var menuResponse = await ownerClient.PostAsJsonAsync("/admin/menus", new CreateMenuRequest("Menu"));
        var menu = await menuResponse.Content.ReadFromJsonAsync<MenuDto>();
        var categoryResponse = await ownerClient.PostAsJsonAsync(
            $"/admin/menus/{menu!.Id}/categories", new CreateCategoryRequest("Category", 1));
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>();
        var productResponse = await ownerClient.PostAsJsonAsync(
            $"/admin/products/categories/{category!.Id}", new CreateProductRequest("Burger", 9.50m));
        var product = (await productResponse.Content.ReadFromJsonAsync<ProductDto>())!;

        var locationResponse = await ownerClient.PostAsJsonAsync("/admin/locations", new CreateLocationRequest("Table 1", null));
        var location = await locationResponse.Content.ReadFromJsonAsync<LocationDto>();
        var qrResponse = await ownerClient.PostAsJsonAsync("/admin/qrcodes", new CreateQrCodeRequest(location!.Id));
        var qrCode = await qrResponse.Content.ReadFromJsonAsync<QrCodeDto>();

        return new SeededTenant(ownerClient, product, qrCode!.Code);
    }

    [Fact]
    public async Task Scanning_a_qr_code_returns_the_venue_menu()
    {
        var seeded = await SeedTenantWithOneOrderableProductAsync(_factory);
        var customerClient = _factory.CreateClient();

        var response = await customerClient.GetAsync($"/p/{seeded.QrCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ResolveQrCodeResponse>();
        Assert.NotEqual(Guid.Empty, body!.SessionId);
        Assert.Contains(body.Menus, m => m.Categories.Any(c => c.Products.Any(p => p.Id == seeded.Product.Id)));
    }

    [Fact]
    public async Task Scanning_the_same_code_twice_resumes_the_same_session()
    {
        var seeded = await SeedTenantWithOneOrderableProductAsync(_factory);
        var customerClient = _factory.CreateClient();

        var first = await (await customerClient.GetAsync($"/p/{seeded.QrCode}")).Content.ReadFromJsonAsync<ResolveQrCodeResponse>();
        var second = await (await customerClient.GetAsync($"/p/{seeded.QrCode}")).Content.ReadFromJsonAsync<ResolveQrCodeResponse>();

        Assert.Equal(first!.SessionId, second!.SessionId);
    }

    [Fact]
    public async Task Unknown_qr_code_returns_404()
    {
        var customerClient = _factory.CreateClient();

        var response = await customerClient.GetAsync("/p/DOESNOTEXIST");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customer_can_place_an_order_and_poll_its_status()
    {
        var seeded = await SeedTenantWithOneOrderableProductAsync(_factory);
        var customerClient = _factory.CreateClient();
        var resolved = await (await customerClient.GetAsync($"/p/{seeded.QrCode}")).Content.ReadFromJsonAsync<ResolveQrCodeResponse>();

        var orderResponse = await customerClient.PostAsJsonAsync("/orders",
            new CreateOrderRequest(resolved!.SessionId, new List<CreateOrderItemRequest> { new(seeded.Product.Id, 2) }));

        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        var order = await orderResponse.Content.ReadFromJsonAsync<CreateOrderResponse>();
        Assert.Equal("Received", order!.Status);

        var statusResponse = await customerClient.GetAsync($"/orders/{order.OrderId}/status?sessionId={resolved.SessionId}");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var status = await statusResponse.Content.ReadFromJsonAsync<OrderStatusResponse>();
        Assert.Equal("Received", status!.Status);
    }

    [Fact]
    public async Task Placing_an_order_with_unavailable_product_returns_409()
    {
        var seeded = await SeedTenantWithOneOrderableProductAsync(_factory);
        await seeded.OwnerClient.PutAsJsonAsync($"/admin/products/{seeded.Product.Id}/availability",
            new UpdateProductAvailabilityRequest(false));
        var customerClient = _factory.CreateClient();
        var resolved = await (await customerClient.GetAsync($"/p/{seeded.QrCode}")).Content.ReadFromJsonAsync<ResolveQrCodeResponse>();

        var orderResponse = await customerClient.PostAsJsonAsync("/orders",
            new CreateOrderRequest(resolved!.SessionId, new List<CreateOrderItemRequest> { new(seeded.Product.Id, 1) }));

        Assert.Equal(HttpStatusCode.Conflict, orderResponse.StatusCode);
    }

    [Fact]
    public async Task Placing_an_order_against_a_closed_session_returns_409()
    {
        var seeded = await SeedTenantWithOneOrderableProductAsync(_factory);
        var customerClient = _factory.CreateClient();
        var resolved = await (await customerClient.GetAsync($"/p/{seeded.QrCode}")).Content.ReadFromJsonAsync<ResolveQrCodeResponse>();

        var locationsResponse = await seeded.OwnerClient.GetAsync("/admin/locations");
        var locations = await locationsResponse.Content.ReadFromJsonAsync<List<LocationDto>>();
        await seeded.OwnerClient.PostAsync($"/admin/locations/{locations![0].Id}/close-session", null);

        var orderResponse = await customerClient.PostAsJsonAsync("/orders",
            new CreateOrderRequest(resolved!.SessionId, new List<CreateOrderItemRequest> { new(seeded.Product.Id, 1) }));

        Assert.Equal(HttpStatusCode.Conflict, orderResponse.StatusCode);
    }
}
```

- [ ] **Step 2: Run the tests**

Run: `dotnet test tests/PingMe.IntegrationTests/PingMe.IntegrationTests.csproj --filter FullyQualifiedName~CustomerOrderingFlowTests`
Expected: PASS — all 6 facts green.

- [ ] **Step 3: Commit**

```bash
git add tests/PingMe.IntegrationTests/Api/CustomerOrderingFlowTests.cs
git commit -m "Add end-to-end customer ordering flow tests"
```

---

## Task 9: AdminOrdersController — list orders and transition status (Owner, Staff)

**Files:**
- Create: `src/PingMe.Api/Contracts/Ordering/AdminOrderItemDto.cs`
- Create: `src/PingMe.Api/Contracts/Ordering/AdminOrderDto.cs`
- Create: `src/PingMe.Api/Contracts/Ordering/UpdateOrderStatusRequest.cs`
- Create: `src/PingMe.Api/Controllers/AdminOrdersController.cs`

**Does NOT cover:** realtime push of new/updated orders to staff — that's Plan 4's SignalR work. This task only provides the underlying data endpoints Plan 4's dashboard will call (and that Task 10's isolation test depends on).

- [ ] **Step 1: Create the DTOs**

`src/PingMe.Api/Contracts/Ordering/AdminOrderItemDto.cs`:

```csharp
namespace PingMe.Api.Contracts.Ordering;

public record AdminOrderItemDto(string ProductName, decimal UnitPrice, int Quantity);
```

`src/PingMe.Api/Contracts/Ordering/AdminOrderDto.cs`:

```csharp
namespace PingMe.Api.Contracts.Ordering;

public record AdminOrderDto(Guid Id, string Status, DateTime CreatedAt, List<AdminOrderItemDto> Items);
```

`src/PingMe.Api/Contracts/Ordering/UpdateOrderStatusRequest.cs`:

```csharp
namespace PingMe.Api.Contracts.Ordering;

public record UpdateOrderStatusRequest(string Status);
```

- [ ] **Step 2: Create `AdminOrdersController.cs`**

```csharp
namespace PingMe.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PingMe.Api.Contracts.Ordering;
using PingMe.Domain.Ordering;
using PingMe.Infrastructure.Persistence;

[ApiController]
[Route("admin/orders")]
[Authorize(Roles = "Owner,Staff")]
public class AdminOrdersController : ControllerBase
{
    private readonly PingMeDbContext _dbContext;

    public AdminOrdersController(PingMeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<AdminOrderDto>>> GetOrders()
    {
        var orders = await _dbContext.Orders
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var result = orders
            .Select(o => new AdminOrderDto(
                o.Id,
                o.Status.ToString(),
                o.CreatedAt,
                o.Items.Select(i => new AdminOrderItemDto(i.ProductName, i.UnitPrice, i.Quantity)).ToList()))
            .ToList();

        return Ok(result);
    }

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
}
```

- [ ] **Step 3: Verify the solution builds**

Run: `dotnet build PingMe.slnx`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add src/PingMe.Api/Contracts/Ordering/AdminOrderItemDto.cs src/PingMe.Api/Contracts/Ordering/AdminOrderDto.cs src/PingMe.Api/Contracts/Ordering/UpdateOrderStatusRequest.cs src/PingMe.Api/Controllers/AdminOrdersController.cs
git commit -m "Add AdminOrdersController: list orders, transition order status"
```

---

## Task 10: Cross-tenant ordering isolation tests (security-critical — carried forward from Plan 1/2)

**Files:**
- Create: `tests/PingMe.IntegrationTests/Api/OrderingIsolationTests.cs`

**Does NOT cover:** admin-API isolation for Locations/QrCodes (already covered by the tenant-scoping mechanism itself and exercised functionally in Task 4) — this task specifically closes the spec Section 9 case that Plan 1 and Plan 2 both explicitly deferred: a `CustomerSession` from one tenant submitting another tenant's `productId`.

- [ ] **Step 1: Create the test file**

```csharp
namespace PingMe.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PingMe.Api.Contracts.Auth;
using PingMe.Api.Contracts.Catalog;
using PingMe.Api.Contracts.Locations;
using PingMe.Api.Contracts.Ordering;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class OrderingIsolationTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public OrderingIsolationTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record SeededTenant(HttpClient OwnerClient, ProductDto Product, string QrCode);

    private static async Task<SeededTenant> SeedTenantWithOneOrderableProductAsync(PingMeWebApplicationFactory factory, string venueName)
    {
        var ownerClient = factory.CreateClient();
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
    public async Task CustomerSession_from_TenantA_submitting_TenantB_productId_on_orders_is_rejected()
    {
        var tenantA = await SeedTenantWithOneOrderableProductAsync(_factory, "Venue A");
        var tenantB = await SeedTenantWithOneOrderableProductAsync(_factory, "Venue B");

        var customerClient = _factory.CreateClient();
        var resolvedA = await (await customerClient.GetAsync($"/p/{tenantA.QrCode}"))
            .Content.ReadFromJsonAsync<ResolveQrCodeResponse>();

        var response = await customerClient.PostAsJsonAsync("/orders",
            new CreateOrderRequest(resolvedA!.SessionId, new List<CreateOrderItemRequest> { new(tenantB.Product.Id, 1) }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Polling_order_status_with_a_session_from_a_different_tenant_returns_404()
    {
        var tenantA = await SeedTenantWithOneOrderableProductAsync(_factory, "Venue A");
        var tenantB = await SeedTenantWithOneOrderableProductAsync(_factory, "Venue B");

        var customerClient = _factory.CreateClient();
        var resolvedA = await (await customerClient.GetAsync($"/p/{tenantA.QrCode}"))
            .Content.ReadFromJsonAsync<ResolveQrCodeResponse>();
        var orderA = await (await customerClient.PostAsJsonAsync("/orders",
                new CreateOrderRequest(resolvedA!.SessionId, new List<CreateOrderItemRequest> { new(tenantA.Product.Id, 1) })))
            .Content.ReadFromJsonAsync<CreateOrderResponse>();

        var resolvedB = await (await customerClient.GetAsync($"/p/{tenantB.QrCode}"))
            .Content.ReadFromJsonAsync<ResolveQrCodeResponse>();

        var response = await customerClient.GetAsync($"/orders/{orderA!.OrderId}/status?sessionId={resolvedB!.SessionId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TenantA_owner_cannot_see_TenantB_order_via_admin_orders_endpoint()
    {
        var tenantA = await SeedTenantWithOneOrderableProductAsync(_factory, "Venue A");
        var tenantB = await SeedTenantWithOneOrderableProductAsync(_factory, "Venue B");

        var customerClient = _factory.CreateClient();
        var resolvedB = await (await customerClient.GetAsync($"/p/{tenantB.QrCode}"))
            .Content.ReadFromJsonAsync<ResolveQrCodeResponse>();
        var orderB = await (await customerClient.PostAsJsonAsync("/orders",
                new CreateOrderRequest(resolvedB!.SessionId, new List<CreateOrderItemRequest> { new(tenantB.Product.Id, 1) })))
            .Content.ReadFromJsonAsync<CreateOrderResponse>();

        var response = await tenantA.OwnerClient.GetAsync("/admin/orders");
        var orders = await response.Content.ReadFromJsonAsync<List<AdminOrderDto>>();

        Assert.DoesNotContain(orders!, o => o.Id == orderB!.OrderId);
    }
}
```

- [ ] **Step 2: Run the tests**

Run: `dotnet test tests/PingMe.IntegrationTests/PingMe.IntegrationTests.csproj --filter FullyQualifiedName~OrderingIsolationTests`
Expected: PASS — all 3 facts green. If `CustomerSession_from_TenantA_submitting_TenantB_productId_on_orders_is_rejected` fails with anything other than `404`, stop and fix `OrdersController.Create`'s tenant-setting order before continuing — this is the specific security-critical case carried forward from Plan 1 and Plan 2.

- [ ] **Step 3: Commit**

```bash
git add tests/PingMe.IntegrationTests/Api/OrderingIsolationTests.cs
git commit -m "Add cross-tenant ordering isolation tests (closes Plan 1/2 carried-forward item)"
```

---

## Task 11: Admin order status transition tests

**Files:**
- Create: `tests/PingMe.IntegrationTests/Api/AdminOrdersTests.cs`

- [ ] **Step 1: Create the test file**

```csharp
namespace PingMe.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PingMe.Api.Contracts.Auth;
using PingMe.Api.Contracts.Catalog;
using PingMe.Api.Contracts.Locations;
using PingMe.Api.Contracts.Ordering;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class AdminOrdersTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public AdminOrdersTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record SeededOrder(HttpClient OwnerClient, Guid OrderId);

    private static async Task<SeededOrder> SeedOneOrderAsync(PingMeWebApplicationFactory factory)
    {
        var ownerClient = factory.CreateClient();
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await ownerClient.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Venue", email, "P@ssw0rd123"));
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

        var customerClient = factory.CreateClient();
        var resolved = await (await customerClient.GetAsync($"/p/{qrCode!.Code}")).Content.ReadFromJsonAsync<ResolveQrCodeResponse>();
        var order = await (await customerClient.PostAsJsonAsync("/orders",
                new CreateOrderRequest(resolved!.SessionId, new List<CreateOrderItemRequest> { new(product.Id, 1) })))
            .Content.ReadFromJsonAsync<CreateOrderResponse>();

        return new SeededOrder(ownerClient, order!.OrderId);
    }

    [Fact]
    public async Task Owner_can_list_orders_and_see_the_new_order()
    {
        var seeded = await SeedOneOrderAsync(_factory);

        var response = await seeded.OwnerClient.GetAsync("/admin/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orders = await response.Content.ReadFromJsonAsync<List<AdminOrderDto>>();
        Assert.Contains(orders!, o => o.Id == seeded.OrderId && o.Status == "Received");
    }

    [Fact]
    public async Task Owner_can_transition_an_order_to_Accepted()
    {
        var seeded = await SeedOneOrderAsync(_factory);

        var response = await seeded.OwnerClient.PutAsJsonAsync(
            $"/admin/orders/{seeded.OrderId}/status", new UpdateOrderStatusRequest("Accepted"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Skipping_a_status_transition_returns_409()
    {
        var seeded = await SeedOneOrderAsync(_factory);

        var response = await seeded.OwnerClient.PutAsJsonAsync(
            $"/admin/orders/{seeded.OrderId}/status", new UpdateOrderStatusRequest("Preparing"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_status_string_returns_400()
    {
        var seeded = await SeedOneOrderAsync(_factory);

        var response = await seeded.OwnerClient.PutAsJsonAsync(
            $"/admin/orders/{seeded.OrderId}/status", new UpdateOrderStatusRequest("NotARealStatus"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run the tests**

Run: `dotnet test tests/PingMe.IntegrationTests/PingMe.IntegrationTests.csproj --filter FullyQualifiedName~AdminOrdersTests`
Expected: PASS — all 4 facts green.

- [ ] **Step 3: Run the full solution build and test suite**

Run:
```bash
dotnet build PingMe.slnx
dotnet test PingMe.slnx
```
Expected: PASS — 0 build errors, all tests green (17 from Plan 1/2 + 1 from Task 1 + 5 from Task 4 + 6 from Task 8 + 3 from Task 10 + 4 from Task 11 = 36 total).

- [ ] **Step 4: Commit**

```bash
git add tests/PingMe.IntegrationTests/Api/AdminOrdersTests.cs
git commit -m "Add admin order status transition tests"
```

---

## Task 12: Scaffold the customer React app (pnpm workspace)

**Files:**
- Create: `src/pingme-web/pnpm-workspace.yaml`
- Create: `src/pingme-web/package.json`
- Create: `src/pingme-web/customer-app/package.json`
- Create: `src/pingme-web/customer-app/tsconfig.json`
- Create: `src/pingme-web/customer-app/tsconfig.node.json`
- Create: `src/pingme-web/customer-app/vite.config.ts`
- Create: `src/pingme-web/customer-app/index.html`
- Create: `src/pingme-web/customer-app/src/main.tsx`

**Does NOT cover:** the Admin app package — that workspace member is added when a future plan builds it (see plan Assumptions on the Admin UI gap).

- [ ] **Step 1: Create the pnpm workspace root**

`src/pingme-web/pnpm-workspace.yaml`:

```yaml
packages:
  - "customer-app"
```

`src/pingme-web/package.json`:

```json
{
  "name": "pingme-web",
  "private": true,
  "version": "0.0.0"
}
```

- [ ] **Step 2: Create the customer-app package**

`src/pingme-web/customer-app/package.json`:

```json
{
  "name": "pingme-customer-app",
  "private": true,
  "version": "0.0.0",
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "test": "vitest run"
  },
  "dependencies": {
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

`src/pingme-web/customer-app/tsconfig.json`:

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

`src/pingme-web/customer-app/tsconfig.node.json`:

```json
{
  "compilerOptions": {
    "composite": true,
    "module": "ESNext",
    "moduleResolution": "bundler",
    "allowSyntheticDefaultImports": true
  },
  "include": ["vite.config.ts"]
}
```

`src/pingme-web/customer-app/vite.config.ts`:

```typescript
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
  },
});
```

`src/pingme-web/customer-app/index.html`:

```html
<!doctype html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>PingMe</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

`src/pingme-web/customer-app/src/main.tsx`:

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

- [ ] **Step 3: Install dependencies**

Run: `cd src/pingme-web && pnpm install`
Expected: PASS — `pnpm-lock.yaml` created, no errors. (`App.tsx` doesn't exist yet — that's fine, `pnpm install` only resolves dependencies, it doesn't build.)

- [ ] **Step 4: Commit**

```bash
git add src/pingme-web/pnpm-workspace.yaml src/pingme-web/package.json src/pingme-web/customer-app/package.json src/pingme-web/customer-app/tsconfig.json src/pingme-web/customer-app/tsconfig.node.json src/pingme-web/customer-app/vite.config.ts src/pingme-web/customer-app/index.html src/pingme-web/customer-app/src/main.tsx src/pingme-web/pnpm-lock.yaml
git commit -m "Scaffold customer-app: pnpm workspace, Vite + React + TypeScript"
```

---

## Task 13: API client and types

**Files:**
- Create: `src/pingme-web/customer-app/src/types.ts`
- Create: `src/pingme-web/customer-app/src/api.ts`

- [ ] **Step 1: Create `types.ts`**

```typescript
export interface CustomerProduct {
  id: string;
  name: string;
  price: number;
}

export interface CustomerCategory {
  id: string;
  name: string;
  sortOrder: number;
  products: CustomerProduct[];
}

export interface CustomerMenu {
  id: string;
  name: string;
  categories: CustomerCategory[];
}

export interface ResolveQrCodeResponse {
  sessionId: string;
  venueName: string;
  locationLabel: string;
  menus: CustomerMenu[];
}

export interface CreateOrderItemRequest {
  productId: string;
  quantity: number;
}

export interface CreateOrderResponse {
  orderId: string;
  status: string;
}

export interface OrderStatusResponse {
  orderId: string;
  status: string;
}
```

- [ ] **Step 2: Create `api.ts`**

```typescript
import type {
  CreateOrderItemRequest,
  CreateOrderResponse,
  OrderStatusResponse,
  ResolveQrCodeResponse,
} from "./types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5190";

export async function resolveQrCode(code: string): Promise<ResolveQrCodeResponse> {
  const response = await fetch(`${API_BASE_URL}/p/${encodeURIComponent(code)}`);
  if (!response.ok) {
    throw new Error(`Failed to resolve QR code: ${response.status}`);
  }
  return response.json();
}

export async function placeOrder(
  sessionId: string,
  items: CreateOrderItemRequest[],
): Promise<CreateOrderResponse> {
  const response = await fetch(`${API_BASE_URL}/orders`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ sessionId, items }),
  });
  if (!response.ok) {
    throw new Error(`Failed to place order: ${response.status}`);
  }
  return response.json();
}

export async function getOrderStatus(
  orderId: string,
  sessionId: string,
): Promise<OrderStatusResponse> {
  const response = await fetch(
    `${API_BASE_URL}/orders/${orderId}/status?sessionId=${sessionId}`,
  );
  if (!response.ok) {
    throw new Error(`Failed to fetch order status: ${response.status}`);
  }
  return response.json();
}
```

- [ ] **Step 3: Commit**

```bash
git add src/pingme-web/customer-app/src/types.ts src/pingme-web/customer-app/src/api.ts
git commit -m "Add customer-app API client and types"
```

---

## Task 14: Cart logic (pure functions, unit tested)

**Files:**
- Create: `src/pingme-web/customer-app/src/cart.ts`
- Create: `src/pingme-web/customer-app/src/cart.test.ts`

- [ ] **Step 1: Write failing tests**

`src/pingme-web/customer-app/src/cart.test.ts`:

```typescript
import { describe, expect, it } from "vitest";
import { addItem, removeItem, cartTotal, type CartItem } from "./cart";

describe("cart", () => {
  it("adds a new product to an empty cart", () => {
    const cart = addItem([], { productId: "p1", name: "Burger", price: 9.5, quantity: 1 });
    expect(cart).toEqual([{ productId: "p1", name: "Burger", price: 9.5, quantity: 1 }]);
  });

  it("increments quantity when the same product is added again", () => {
    const initial: CartItem[] = [{ productId: "p1", name: "Burger", price: 9.5, quantity: 1 }];
    const cart = addItem(initial, { productId: "p1", name: "Burger", price: 9.5, quantity: 2 });
    expect(cart).toEqual([{ productId: "p1", name: "Burger", price: 9.5, quantity: 3 }]);
  });

  it("removes a product from the cart", () => {
    const initial: CartItem[] = [
      { productId: "p1", name: "Burger", price: 9.5, quantity: 1 },
      { productId: "p2", name: "Beer", price: 4.0, quantity: 2 },
    ];
    const cart = removeItem(initial, "p1");
    expect(cart).toEqual([{ productId: "p2", name: "Beer", price: 4.0, quantity: 2 }]);
  });

  it("calculates the total across quantities", () => {
    const cart: CartItem[] = [
      { productId: "p1", name: "Burger", price: 9.5, quantity: 2 },
      { productId: "p2", name: "Beer", price: 4.0, quantity: 3 },
    ];
    expect(cartTotal(cart)).toBeCloseTo(31.0);
  });

  it("an empty cart has a total of 0", () => {
    expect(cartTotal([])).toBe(0);
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd src/pingme-web/customer-app && pnpm test`
Expected: FAIL with "Cannot find module './cart'" (the module doesn't exist yet).

- [ ] **Step 3: Implement `cart.ts`**

```typescript
export interface CartItem {
  productId: string;
  name: string;
  price: number;
  quantity: number;
}

export function addItem(cart: CartItem[], item: CartItem): CartItem[] {
  const existing = cart.find((c) => c.productId === item.productId);
  if (existing) {
    return cart.map((c) =>
      c.productId === item.productId ? { ...c, quantity: c.quantity + item.quantity } : c,
    );
  }
  return [...cart, item];
}

export function removeItem(cart: CartItem[], productId: string): CartItem[] {
  return cart.filter((c) => c.productId !== productId);
}

export function cartTotal(cart: CartItem[]): number {
  return cart.reduce((sum, item) => sum + item.price * item.quantity, 0);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `cd src/pingme-web/customer-app && pnpm test`
Expected: PASS — all 5 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/pingme-web/customer-app/src/cart.ts src/pingme-web/customer-app/src/cart.test.ts
git commit -m "Add cart logic with unit tests"
```

---

## Task 15: Menu browsing and cart UI components

**Files:**
- Create: `src/pingme-web/customer-app/src/components/MenuBrowser.tsx`
- Create: `src/pingme-web/customer-app/src/components/CartView.tsx`

**Does NOT cover:** styling/visual design beyond functional, unstyled markup — a design pass is out of scope for this plan; the goal is a working flow, not a polished UI.

- [ ] **Step 1: Create `MenuBrowser.tsx`**

```tsx
import type { CustomerMenu } from "../types";

interface MenuBrowserProps {
  menus: CustomerMenu[];
  onAddToCart: (productId: string, name: string, price: number) => void;
}

export function MenuBrowser({ menus, onAddToCart }: MenuBrowserProps) {
  return (
    <div>
      {menus.map((menu) => (
        <section key={menu.id}>
          <h2>{menu.name}</h2>
          {menu.categories
            .slice()
            .sort((a, b) => a.sortOrder - b.sortOrder)
            .map((category) => (
              <div key={category.id}>
                <h3>{category.name}</h3>
                <ul>
                  {category.products.map((product) => (
                    <li key={product.id}>
                      <span>{product.name}</span>
                      <span> €{product.price.toFixed(2)}</span>
                      <button onClick={() => onAddToCart(product.id, product.name, product.price)}>
                        Add
                      </button>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
        </section>
      ))}
    </div>
  );
}
```

- [ ] **Step 2: Create `CartView.tsx`**

```tsx
import { cartTotal, type CartItem } from "../cart";

interface CartViewProps {
  cart: CartItem[];
  onRemove: (productId: string) => void;
  onPlaceOrder: () => void;
  placingOrder: boolean;
}

export function CartView({ cart, onRemove, onPlaceOrder, placingOrder }: CartViewProps) {
  if (cart.length === 0) {
    return <p>Your cart is empty.</p>;
  }

  return (
    <div>
      <h2>Your order</h2>
      <ul>
        {cart.map((item) => (
          <li key={item.productId}>
            {item.quantity} x {item.name} — €{(item.price * item.quantity).toFixed(2)}
            <button onClick={() => onRemove(item.productId)}>Remove</button>
          </li>
        ))}
      </ul>
      <p>Total: €{cartTotal(cart).toFixed(2)}</p>
      <button onClick={onPlaceOrder} disabled={placingOrder}>
        {placingOrder ? "Placing order..." : "Place order"}
      </button>
    </div>
  );
}
```

- [ ] **Step 3: Commit**

```bash
git add src/pingme-web/customer-app/src/components/MenuBrowser.tsx src/pingme-web/customer-app/src/components/CartView.tsx
git commit -m "Add MenuBrowser and CartView components"
```

---

## Task 16: Order status view and top-level App

**Files:**
- Create: `src/pingme-web/customer-app/src/components/OrderStatus.tsx`
- Create: `src/pingme-web/customer-app/src/App.tsx`

**Does NOT cover:** realtime updates — the status view polls on an interval; SignalR replaces this in Plan 4 without changing this component's props contract (it still just needs a status string).

- [ ] **Step 1: Create `OrderStatus.tsx`**

```tsx
import { useEffect, useState } from "react";
import { getOrderStatus } from "../api";

interface OrderStatusProps {
  orderId: string;
  sessionId: string;
}

export function OrderStatus({ orderId, sessionId }: OrderStatusProps) {
  const [status, setStatus] = useState("Received");

  useEffect(() => {
    const interval = setInterval(async () => {
      try {
        const result = await getOrderStatus(orderId, sessionId);
        setStatus(result.status);
      } catch {
        // Best-effort polling — a transient failure just retries on the next tick.
      }
    }, 5000);

    return () => clearInterval(interval);
  }, [orderId, sessionId]);

  return (
    <div>
      <h2>Order status</h2>
      <p>{status}</p>
    </div>
  );
}
```

- [ ] **Step 2: Create `App.tsx`**

```tsx
import { useEffect, useState } from "react";
import { resolveQrCode, placeOrder } from "./api";
import { addItem, removeItem, type CartItem } from "./cart";
import { MenuBrowser } from "./components/MenuBrowser";
import { CartView } from "./components/CartView";
import { OrderStatus } from "./components/OrderStatus";
import type { ResolveQrCodeResponse } from "./types";

function getCodeFromPath(): string | null {
  const match = window.location.pathname.match(/^\/p\/(.+)$/);
  return match ? match[1] : null;
}

export default function App() {
  const [resolved, setResolved] = useState<ResolveQrCodeResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [cart, setCart] = useState<CartItem[]>([]);
  const [placingOrder, setPlacingOrder] = useState(false);
  const [placedOrderId, setPlacedOrderId] = useState<string | null>(null);

  useEffect(() => {
    const code = getCodeFromPath();
    if (!code) {
      setError("No QR code found in the URL. Scan a code to start ordering.");
      return;
    }

    resolveQrCode(code)
      .then(setResolved)
      .catch(() => setError("This code isn't valid — ask a staff member for help."));
  }, []);

  if (error) {
    return <p>{error}</p>;
  }

  if (!resolved) {
    return <p>Loading menu...</p>;
  }

  if (placedOrderId) {
    return <OrderStatus orderId={placedOrderId} sessionId={resolved.sessionId} />;
  }

  const handleAddToCart = (productId: string, name: string, price: number) => {
    setCart((current) => addItem(current, { productId, name, price, quantity: 1 }));
  };

  const handleRemove = (productId: string) => {
    setCart((current) => removeItem(current, productId));
  };

  const handlePlaceOrder = async () => {
    setPlacingOrder(true);
    try {
      const order = await placeOrder(
        resolved.sessionId,
        cart.map((item) => ({ productId: item.productId, quantity: item.quantity })),
      );
      setPlacedOrderId(order.orderId);
    } catch {
      setError("Couldn't place your order — please try again.");
    } finally {
      setPlacingOrder(false);
    }
  };

  return (
    <div>
      <h1>{resolved.venueName}</h1>
      <p>{resolved.locationLabel}</p>
      <MenuBrowser menus={resolved.menus} onAddToCart={handleAddToCart} />
      <CartView
        cart={cart}
        onRemove={handleRemove}
        onPlaceOrder={handlePlaceOrder}
        placingOrder={placingOrder}
      />
    </div>
  );
}
```

- [ ] **Step 3: Run the frontend test suite and build**

Run:
```bash
cd src/pingme-web/customer-app
pnpm test
pnpm build
```
Expected: PASS — Vitest suite green (cart tests from Task 14), `tsc -b && vite build` completes with no TypeScript errors.

- [ ] **Step 4: Commit**

```bash
git add src/pingme-web/customer-app/src/components/OrderStatus.tsx src/pingme-web/customer-app/src/App.tsx
git commit -m "Add OrderStatus component and top-level App: scan -> menu -> cart -> order -> status"
```

---

## Task 17: Final verification and progress tracking

**Files:**
- Modify: `Docs/superpowers optimized/plans/PROGRESS.md`

- [ ] **Step 1: Run the full backend build and test suite from a clean state**

Run:
```bash
dotnet build PingMe.slnx
dotnet test PingMe.slnx
```
Expected: PASS — 0 build errors, all 36 tests passing (see Task 11, Step 3 for the breakdown).

- [ ] **Step 2: Run the full frontend build and test suite from a clean state**

Run:
```bash
cd src/pingme-web/customer-app
pnpm install
pnpm test
pnpm build
```
Expected: PASS — no install errors, Vitest suite green, production build succeeds.

- [ ] **Step 3: Manually verify the customer flow end-to-end** (not automatable in this plan — do this once by hand)

1. Start the backend: `dotnet run --project src/PingMe.Api`.
2. Use Swagger (`/swagger`) to register a tenant, log in, create a menu/category/product, create a location, and create a QR code — note the returned `code`.
3. Start the customer app: `cd src/pingme-web/customer-app && pnpm dev`.
4. Visit `http://localhost:5173/p/{code}` in a browser (substituting the real code) and confirm the menu loads, an item can be added to the cart, and placing the order shows an order status of `Received`.
5. Stop both running processes afterward.

- [ ] **Step 4: Update `Docs/superpowers optimized/plans/PROGRESS.md`**

Update the table to add this plan's row (`3 | Locations/QR + Customer app + Ordering | ... | Done, reviewed | 17 / 17`) and update Plan 5's row if it references "Plan 3" as a blocking dependency to confirm it's now unblocked. Also add a new carried-forward item: **"No plan currently owns an Owner/Staff Admin React UI — Plan 4 only covers the Staff order dashboard. Locations/QR/Catalog admin currently only has API + Swagger access. Needs a decision before or during Plan 4."**

- [ ] **Step 5: Commit**

```bash
git add "Docs/superpowers optimized/plans/PROGRESS.md"
git commit -m "Close Plan 3: mark done, flag Admin UI gap for Plan 4"
```

---

## Self-Review

**1. Spec coverage.**
- Section 2a `CustomerSession`: delivered — `GET /p/{code}` creates/resumes exactly as specified (Task 5), `POST /admin/locations/{id}/close-session` lets staff reset it (Task 2).
- Section 5 Data Flow (Customer): delivered end-to-end — QR scan → session → menu → cart (client-side, Task 14) → `POST /orders` (Task 6) → status polling (Task 7).
- Section 5 Data Flow (Staff/Owner): the data layer (`AdminOrdersController`, Task 9) is delivered; the realtime SignalR-subscribed dashboard itself is explicitly Plan 4's job, not duplicated here.
- Section 6 API Contracts: `GET /p/{code}`, `POST /orders`, `GET /orders/{id}/status`, `/admin/locations`, `/admin/qrcodes`, `POST /admin/locations/{id}/close-session` all delivered matching the spec's contract shapes. `/admin/orders` (spec-mentioned) delivered in Task 9.
- Section 7 Order State Machine: unchanged from Plan 1 (`Order.TransitionTo`, already tested in `PingMe.UnitTests`); this plan adds the HTTP surface over it (Task 9) with the correct `409` on an invalid transition (Task 11).
- Section 8 Error Handling: QR resolution 404 (Task 5, tested Task 8), order-against-unavailable-product 409 (Task 6, tested Task 8), session closed/expired 409/410 (Task 6, tested Task 8).
- Section 9 Testing Strategy: the specific carried-forward case — "a `CustomerSession` from Tenant A submitting `productId`s belonging to Tenant B on `POST /orders` is rejected" — is directly implemented and tested in Task 10, closing the item that both Plan 1 and Plan 2 explicitly deferred.

**2. Placeholder scan.** No "TBD"/"TODO" strings. Every code block is complete, runnable code, not a description of code.

**3. Type consistency.** `CreateOrderRequest`/`CreateOrderItemRequest` defined once in Task 6, referenced identically (same property names and casing) in Tasks 8, 10, 11, and in the frontend's `api.ts` (Task 13, JSON casing matches ASP.NET Core's default camelCase serialization of the PascalCase C# records). `ResolveQrCodeResponse`/`CustomerMenuDto`/`CustomerCategoryDto`/`CustomerProductDto` defined once in Task 5, consumed identically in Tasks 8, 10, 11, and the frontend's `types.ts`/`MenuBrowser.tsx`. `AdminOrderDto` defined in Task 9, referenced by Task 10's isolation test and Task 11's admin tests with matching property names (`Id`, `Status`, `CreatedAt`, `Items`).

---

## Execution Handoff

**Plan complete and saved to `Docs/superpowers optimized/plans/2026-08-26-pingme-plan-3-locations-qr-customer-ordering.md`. Two execution options:**

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** — Execute tasks in this session using executing-plans, with checkpoints

**Which approach?**
