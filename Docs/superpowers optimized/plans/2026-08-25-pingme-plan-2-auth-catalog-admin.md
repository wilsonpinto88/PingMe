# PingMe Plan 2/4 — Authentication & Catalog/Admin Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-optimized:subagent-driven-development (recommended) or superpowers-optimized:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add ASP.NET Core Identity + JWT authentication for the Admin app (Owner/Staff roles), wire per-request tenant resolution from the JWT claim, and build the first real HTTP surface — Catalog CRUD endpoints an Owner can drive from Swagger — proving the whole stack end-to-end in a browser for the first time.

**Architecture:** `AppUser : IdentityUser<Guid>` lives in `PingMe.Infrastructure.Identity` (not `Domain`, since it depends on ASP.NET Core Identity — see Assumptions). `PingMeDbContext` becomes an `IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>`, so `AppUser` automatically participates in the same reflection-driven tenant filter built in Plan 1 (it implements `ITenantOwned`). A JWT issued at login carries a `tenantId` claim; `TenantResolutionMiddleware` reads it after authentication and sets `CurrentTenantProvider.TenantId` for the rest of the request — this is the "Admin/Staff requests → from the authenticated user's TenantId claim" resolution path from the spec's Multi-Tenancy section, the piece Plan 1 deliberately left as a manual test-only setter. Catalog admin endpoints (`/admin/menus`, `/admin/products`) are `[Authorize(Roles = "Owner")]` and rely entirely on the existing tenant filter for scoping — no manual `WHERE TenantId = ...` anywhere.

**Tech Stack:** ASP.NET Core Identity (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`), JWT Bearer auth (`Microsoft.AspNetCore.Authentication.JwtBearer`), Swashbuckle for Swagger UI, `Microsoft.AspNetCore.Mvc.Testing` for HTTP-level integration tests, xUnit, PostgreSQL (continuing from Plan 1).

**Assumptions:**
- Assumes `AppUser` belongs in `PingMe.Infrastructure.Identity`, not `PingMe.Domain.Identity` as the original spec's folder sketch suggested — `IdentityUser<Guid>` is a framework type and the Domain layer must stay framework-agnostic. Will NOT create a parallel domain-level `User` shim just to satisfy the folder sketch; this is a documented, deliberate deviation.
- Assumes email must be globally unique across all tenants, not just within one — login resolves a user by email *before* the tenant is known, so two tenants can't both have an owner/staff member with the same email. Will NOT support that case; a person managing two venues needs two separate accounts with different emails (consistent with Plan 1's single-venue-per-tenant assumption).
- Assumes ASP.NET Core Identity's built-in `RequireUniqueEmail` check is disabled and replaced with a manual uniqueness check in `RegisterTenant` — Identity's built-in check silently stops working once the global tenant query filter is active (it looks up existing users scoped to the *current* tenant, which is `null` during anonymous registration, so it would never find a real collision). This is a non-obvious interaction between Plan 1's tenant filter and Identity's default behavior; see Task 7 for the fix.
- Assumes a single hardcoded JWT signing key in `appsettings.Development.json` for local dev. Will NOT wire a secrets manager or key rotation now — Plan 4 (Deployment) must replace this with a real secret before anything is exposed outside localhost.
- Assumes "Owner" and "Staff" are global, non-tenant-scoped Identity roles (seeded once at startup) — every tenant's owner references the same shared "Owner" role row. This matches the spec's role model (Owner/Staff are role *names*, not per-tenant entities).
- Does NOT add any Staff-authorized endpoint yet — "Staff" is seeded and assignable, but no controller currently accepts it. Plan 4's Staff Dashboard is where Staff-accessible endpoints appear. All Catalog/Admin endpoints in this plan are Owner-only.
- Does NOT implement password reset, email confirmation, 2FA, or catalog DELETE endpoints — out of MVP scope per spec Section 1; Create/Read/Update is enough to prove the admin flow end-to-end.
- Does NOT close the PROGRESS.md carried-forward item "CustomerSession from Tenant A submitting Tenant B's productId on `POST /orders` is rejected" — that needs the ordering HTTP endpoint, which is Plan 3's job.

---

## File Structure

```
src/PingMe.Infrastructure/
 ├── Identity/
 │    ├── AppUser.cs                                  (new)
 │    └── JwtTokenGenerator.cs                         (new)
 ├── PingMe.Infrastructure.csproj                      (modified — add Identity EF Core Stores package)
 └── Persistence/
      └── PingMeDbContext.cs                           (modified — IdentityDbContext base, base.OnModelCreating reordered)

src/PingMe.Api/
 ├── PingMe.Api.csproj                                 (modified — add JwtBearer, Swashbuckle packages)
 ├── Program.cs                                        (modified — Identity/JWT/Swagger wiring, role seeding, partial Program marker)
 ├── appsettings.Development.json                      (modified — Jwt settings)
 ├── Middleware/
 │    └── TenantResolutionMiddleware.cs                (new)
 ├── Contracts/
 │    ├── Auth/
 │    │    ├── RegisterTenantRequest.cs                (new)
 │    │    ├── LoginRequest.cs                         (new)
 │    │    └── AuthResponse.cs                         (new)
 │    └── Catalog/
 │         ├── MenuDto.cs                              (new)
 │         ├── CreateMenuRequest.cs                    (new)
 │         ├── CategoryDto.cs                          (new)
 │         ├── CreateCategoryRequest.cs                (new)
 │         ├── ProductDto.cs                           (new)
 │         ├── CreateProductRequest.cs                 (new)
 │         ├── UpdateProductAvailabilityRequest.cs     (new)
 │         ├── ProductOptionDto.cs                     (new)
 │         └── CreateProductOptionRequest.cs           (new)
 └── Controllers/
      ├── AuthController.cs                            (new)
      ├── MenusController.cs                           (new)
      └── ProductsController.cs                        (new)

tests/PingMe.IntegrationTests/
 ├── PingMe.IntegrationTests.csproj                    (modified — add Mvc.Testing package)
 ├── Infrastructure/
 │    └── PingMeWebApplicationFactory.cs               (new)
 └── Api/
      ├── AuthTests.cs                                 (new)
      └── CatalogAdminIsolationTests.cs                (new)
```

---

### Task 1: Add Identity, JWT, and Swagger NuGet packages

**Files:**
- Modify: `src/PingMe.Infrastructure/PingMe.Infrastructure.csproj`
- Modify: `src/PingMe.Api/PingMe.Api.csproj`

- [x] **Step 1: Add the Identity EF Core store package to Infrastructure**

Run:
```bash
dotnet add src/PingMe.Infrastructure/PingMe.Infrastructure.csproj package Microsoft.AspNetCore.Identity.EntityFrameworkCore
```

- [x] **Step 2: Add JWT Bearer and Swagger packages to the Api project**

Run:
```bash
dotnet add src/PingMe.Api/PingMe.Api.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src/PingMe.Api/PingMe.Api.csproj package Swashbuckle.AspNetCore
```

- [x] **Step 3: Verify the solution still builds**

Run: `dotnet build PingMe.slnx`
Expected: PASS

- [x] **Step 4: Commit**

```bash
git add src/PingMe.Infrastructure/PingMe.Infrastructure.csproj src/PingMe.Api/PingMe.Api.csproj
git commit -m "Add Identity, JWT Bearer, and Swashbuckle package references"
```

_Completed 2026-08-25: commit `c99cb56`._

---

### Task 2: Infrastructure — AppUser entity

**Files:**
- Create: `src/PingMe.Infrastructure/Identity/AppUser.cs`

**Does NOT cover:** wiring `AppUser` into the DbContext or Identity services — that's Tasks 3 and 5.

- [x] **Step 1: Create `AppUser.cs`**

```csharp
namespace PingMe.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;
using PingMe.Domain.Common;

public class AppUser : IdentityUser<Guid>, ITenantOwned
{
    public AppUser()
    {
        Id = Guid.NewGuid();
    }

    public Guid TenantId { get; set; }
}
```

`IdentityUser<Guid>` has no built-in default-constructor `Id` assignment the way the string-keyed `IdentityUser` does — without this constructor, every new `AppUser` would default to `Guid.Empty` until EF/Identity assigned one, which never happens automatically for a `Guid` key. `TenantId` is a plain mutable property (not constructor-set like the Plan 1 domain entities) because `AppUser` is created via object-initializer syntax by `UserManager.CreateAsync`, not through a domain constructor.

- [x] **Step 2: Verify it compiles**

Run: `dotnet build src/PingMe.Infrastructure/PingMe.Infrastructure.csproj`
Expected: PASS

- [x] **Step 3: Commit**

```bash
git add src/PingMe.Infrastructure/Identity/AppUser.cs
git commit -m "Add AppUser identity entity implementing ITenantOwned"
```

_Completed 2026-08-25: commit `555922b`._

---

### Task 3: Infrastructure — convert PingMeDbContext to IdentityDbContext

**Files:**
- Modify: `src/PingMe.Infrastructure/Persistence/PingMeDbContext.cs`

**Does NOT cover:** registering `AppUser`/Identity services in DI — that's Task 5.

- [x] **Step 1: Read the current file, then replace its full contents with:**

```csharp
namespace PingMe.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PingMe.Application.Tenants;
using PingMe.Domain.Catalog;
using PingMe.Domain.Common;
using PingMe.Domain.Locations;
using PingMe.Domain.Ordering;
using PingMe.Domain.Tenants;
using PingMe.Infrastructure.Identity;

public class PingMeDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    private readonly ICurrentTenantProvider _currentTenantProvider;

    public PingMeDbContext(DbContextOptions<PingMeDbContext> options, ICurrentTenantProvider currentTenantProvider)
        : base(options)
    {
        _currentTenantProvider = currentTenantProvider;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<CustomerSession> CustomerSessions => Set<CustomerSession>();
    public DbSet<QrCode> QrCodes => Set<QrCode>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductOption> ProductOptions => Set<ProductOption>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>().Property(p => p.Price).HasPrecision(10, 2);
        modelBuilder.Entity<ProductOption>().Property(p => p.PriceDelta).HasPrecision(10, 2);
        modelBuilder.Entity<OrderItem>().Property(i => i.UnitPrice).HasPrecision(10, 2);
        modelBuilder.Entity<OrderItem>().ToTable("OrderItems");

        modelBuilder.Entity<Order>()
            .HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Order>()
            .Navigation(o => o.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        modelBuilder.Entity<Location>()
            .HasOne<Location>()
            .WithMany()
            .HasForeignKey(l => l.ParentLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(PingMeDbContext)
                    .GetMethod(nameof(ApplyTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(this, new object[] { modelBuilder });
            }
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantOwned
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == _currentTenantProvider.TenantId);
        modelBuilder.Entity<TEntity>().HasIndex(e => e.TenantId);
    }
}
```

Two changes from Plan 1's version: the base class is now `IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>` instead of plain `DbContext`, and `base.OnModelCreating(modelBuilder)` moved from the end of the method to the **start** — Identity's own model configuration must run first so its tables/keys are established before any other fluent configuration touches the model (standard guidance for deriving from `IdentityDbContext`). Everything else is unchanged. `AppUser` implements `ITenantOwned`, so the existing reflection loop picks it up automatically — no new code needed to filter it.

- [x] **Step 2: Add EF/Identity design-time tooling is already present from Plan 1 — regenerate the migration**

Run:
```bash
dotnet ef migrations add AddIdentityTables --project src/PingMe.Infrastructure --startup-project src/PingMe.Api --output-dir Persistence/Migrations
```
Expected: PASS — creates a migration adding `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, and related Identity tables, plus the `TenantId` column/index on `AspNetUsers` (since `AppUser` is `ITenantOwned`).

- [x] **Step 3: Apply the migration**

Run: `dotnet ef database update --project src/PingMe.Infrastructure --startup-project src/PingMe.Api`
Expected: PASS

- [x] **Step 4: Verify the solution builds**

Run: `dotnet build PingMe.slnx`
Expected: PASS

- [x] **Step 5: Commit**

```bash
git add src/PingMe.Infrastructure
git commit -m "Convert PingMeDbContext to IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>"
```

_Completed 2026-08-26: commit `88129a7`. Migration `AddIdentityTables` confirmed AspNetUsers has TenantId column+index; tenant filter correctly picked up AppUser; 8/8 existing tests still pass._

---

### Task 4: Infrastructure — JWT settings and token generator

**Files:**
- Modify: `src/PingMe.Api/appsettings.Development.json`
- Create: `src/PingMe.Infrastructure/Identity/JwtTokenGenerator.cs`

- [x] **Step 1: Add JWT settings to `appsettings.Development.json`**

Update `src/PingMe.Api/appsettings.Development.json` to:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "PingMe": "Host=localhost;Port=5432;Database=pingme_dev;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Key": "dev-only-signing-key-change-before-production-1234567890",
    "Issuer": "PingMe",
    "Audience": "PingMeAdmin",
    "ExpiryMinutes": "60"
  }
}
```

- [x] **Step 2: Create `JwtTokenGenerator.cs`**

```csharp
namespace PingMe.Infrastructure.Identity;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

public class JwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(AppUser user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("tenantId", user.TenantId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"]!);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

- [x] **Step 3: Verify it compiles**

Run: `dotnet build src/PingMe.Infrastructure/PingMe.Infrastructure.csproj`
Expected: PASS

- [x] **Step 4: Commit**

```bash
git add src/PingMe.Api/appsettings.Development.json src/PingMe.Infrastructure/Identity/JwtTokenGenerator.cs
git commit -m "Add JWT settings and JwtTokenGenerator"
```

_Completed 2026-08-26: commit `4899960`. Needed 2 extra package refs (Microsoft.IdentityModel.Tokens, System.IdentityModel.Tokens.Jwt) since they didn't resolve transitively — anticipated fallback in the task text, not a deviation._

---

### Task 5: Api — wire Identity, JWT auth, Swagger, tenant-resolution middleware, and role seeding

**Files:**
- Create: `src/PingMe.Api/Middleware/TenantResolutionMiddleware.cs`
- Modify: `src/PingMe.Api/Program.cs`

**Does NOT cover:** any controller — this task only wires the pipeline. There is nothing to call yet; verification is "does it start and build", not "does an endpoint respond."

- [x] **Step 1: Create `TenantResolutionMiddleware.cs`**

```csharp
namespace PingMe.Api.Middleware;

using PingMe.Infrastructure.Tenants;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, CurrentTenantProvider currentTenantProvider)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = context.User.FindFirst("tenantId")?.Value;
            if (tenantClaim is not null && Guid.TryParse(tenantClaim, out var tenantId))
            {
                currentTenantProvider.TenantId = tenantId;
            }
        }

        await _next(context);
    }
}
```

- [x] **Step 2: Replace the full contents of `Program.cs` with:**

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PingMe.Api.Middleware;
using PingMe.Application.Tenants;
using PingMe.Infrastructure.Identity;
using PingMe.Infrastructure.Persistence;
using PingMe.Infrastructure.Tenants;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT returned by /auth/login (no \"Bearer \" prefix needed)."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<PingMeDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PingMe")));

builder.Services.AddScoped<CurrentTenantProvider>();
builder.Services.AddScoped<ICurrentTenantProvider>(sp => sp.GetRequiredService<CurrentTenantProvider>());
builder.Services.AddScoped<JwtTokenGenerator>();

builder.Services.AddIdentityCore<AppUser>(options =>
{
    options.Password.RequiredLength = 8;
    // Identity's built-in email-uniqueness check silently stops working once the
    // global tenant query filter is active (it looks up existing users scoped to
    // the *current* tenant, which is null during anonymous registration). Disabled
    // here; AuthController.RegisterTenant enforces global uniqueness manually.
    options.User.RequireUniqueEmail = false;
})
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<PingMeDbContext>();

var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
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
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    foreach (var roleName in new[] { "Owner", "Staff" })
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program
{
}
```

The `public partial class Program { }` at the bottom is required so `Microsoft.AspNetCore.Mvc.Testing`'s `WebApplicationFactory<Program>` (Task 6) can reference the entry point — top-level statement programs don't otherwise expose a usable `Program` type. `TenantResolutionMiddleware` is placed after `UseAuthentication()` (so `context.User` is populated) and before `UseAuthorization()` (so the resolved tenant is available to anything downstream, including model-level query filters that run during controller action execution).

- [x] **Step 3: Verify the solution builds and the app starts**

Run: `dotnet build PingMe.slnx`
Expected: PASS

Run: `dotnet run --project src/PingMe.Api --launch-profile https &` then `curl -k https://localhost:7xxx/swagger/index.html` (use whichever port `launchSettings.json` assigns), then stop the process.
Expected: the Swagger UI page loads (HTTP 200). Role seeding runs on startup without throwing — check the console output for no unhandled exceptions during the `using (var scope = ...)` block.

- [x] **Step 4: Commit**

```bash
git add src/PingMe.Api
git commit -m "Wire Identity, JWT auth, Swagger UI, tenant-resolution middleware, and role seeding"
```

_Completed 2026-08-26: commit `24ef802`, plus fix-up `8eaa646` (Swagger's Bearer security requirement was serializing as an empty object due to `null` host document; fixed by passing the in-progress `OpenApiDocument` to `OpenApiSecuritySchemeReference`, empirically verified against `/swagger/v1/swagger.json`). Swashbuckle.AspNetCore 10.2.3's API surface differs from the plan's assumed shape (`Microsoft.OpenApi` namespace, not `Microsoft.OpenApi.Models`) — noted as an accepted, necessary adaptation._

---

### Task 6: Integration test infrastructure — PingMeWebApplicationFactory

**Files:**
- Modify: `tests/PingMe.IntegrationTests/PingMe.IntegrationTests.csproj`
- Create: `tests/PingMe.IntegrationTests/Infrastructure/PingMeWebApplicationFactory.cs`

**Does NOT cover:** any actual test — this is shared test infrastructure only, used starting Task 7.

- [x] **Step 1: Add the ASP.NET Core test-hosting package**

Run:
```bash
dotnet add tests/PingMe.IntegrationTests/PingMe.IntegrationTests.csproj package Microsoft.AspNetCore.Mvc.Testing
```

- [x] **Step 2: Create `PingMeWebApplicationFactory.cs`**

```csharp
namespace PingMe.IntegrationTests.Infrastructure;

using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PingMe.Infrastructure.Persistence;

public class PingMeWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<PingMeDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<PingMeDbContext>(options =>
                options.UseNpgsql(
                    "Host=localhost;Port=5432;Database=pingme_test;Username=postgres;Password=postgres"));

            using var scope = services.BuildServiceProvider().CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PingMeDbContext>();
            dbContext.Database.EnsureCreated();
        });
    }
}
```

This targets the same `pingme_test` database Plan 1's integration tests use — each test in this plan registers a brand-new tenant with a random-GUID email, so tests don't collide with each other or with Plan 1's leftover rows, matching the pattern already accepted in Plan 1 (no cleanup between runs; correctness doesn't depend on a clean slate since every test uses fresh identifiers).

- [x] **Step 3: Verify the test project builds**

Run: `dotnet build tests/PingMe.IntegrationTests/PingMe.IntegrationTests.csproj`
Expected: PASS — this requires `PingMe.Api`'s `public partial class Program` from Task 5 and a project reference from `PingMe.IntegrationTests` to `PingMe.Api`. Add the reference if it's missing:
```bash
dotnet add tests/PingMe.IntegrationTests/PingMe.IntegrationTests.csproj reference src/PingMe.Api/PingMe.Api.csproj
```
Then rebuild and confirm PASS.

- [x] **Step 4: Commit**

```bash
git add tests/PingMe.IntegrationTests
git commit -m "Add PingMeWebApplicationFactory for HTTP-level integration tests"
```

_Completed 2026-08-26: commit `5542193`. 8/8 existing tests still pass; factory not yet consumed by a test class (expected — Task 7 is the first real usage)._

---

### Task 7: Auth DTOs and AuthController.RegisterTenant (TDD)

**Files:**
- Create: `src/PingMe.Api/Contracts/Auth/RegisterTenantRequest.cs`
- Create: `src/PingMe.Api/Contracts/Auth/LoginRequest.cs`
- Create: `src/PingMe.Api/Contracts/Auth/AuthResponse.cs`
- Create: `src/PingMe.Api/Controllers/AuthController.cs`
- Test: `tests/PingMe.IntegrationTests/Api/AuthTests.cs`

**Does NOT cover:** the `Login` endpoint's implementation (stubbed as not-yet-existing here; added in Task 8). This task's test file includes both `RegisterTenant` and `Login` facts because a single HTTP-level TDD cycle is more natural when the two endpoints are tightly coupled (you can't test registration's token without also exercising login) — the RED step below covers both facts failing, and Task 8 turns the login-specific facts green without touching `RegisterTenant` again.

- [x] **Step 1: Create the 3 DTO files**

`src/PingMe.Api/Contracts/Auth/RegisterTenantRequest.cs`:
```csharp
namespace PingMe.Api.Contracts.Auth;

public record RegisterTenantRequest(string TenantName, string OwnerEmail, string OwnerPassword);
```

`src/PingMe.Api/Contracts/Auth/LoginRequest.cs`:
```csharp
namespace PingMe.Api.Contracts.Auth;

public record LoginRequest(string Email, string Password);
```

`src/PingMe.Api/Contracts/Auth/AuthResponse.cs`:
```csharp
namespace PingMe.Api.Contracts.Auth;

public record AuthResponse(string Token);
```

- [x] **Step 2: Write the failing tests**

Create `tests/PingMe.IntegrationTests/Api/AuthTests.cs`:

```csharp
namespace PingMe.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Json;
using PingMe.Api.Contracts.Auth;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class AuthTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthTests(PingMeWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RegisterTenant_returns_201_with_a_token()
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";

        var response = await _client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Test Venue", email, "P@ssw0rd123"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
    }

    [Fact]
    public async Task RegisterTenant_with_duplicate_email_returns_409()
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Test Venue", email, "P@ssw0rd123"));

        var secondAttempt = await _client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Another Venue", email, "DifferentPass1"));

        Assert.Equal(HttpStatusCode.Conflict, secondAttempt.StatusCode);
    }

    [Fact]
    public async Task Login_after_register_succeeds()
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Test Venue", email, "P@ssw0rd123"));

        var loginResponse = await _client.PostAsJsonAsync("/auth/login",
            new LoginRequest(email, "P@ssw0rd123"));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var body = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Test Venue", email, "P@ssw0rd123"));

        var loginResponse = await _client.PostAsJsonAsync("/auth/login",
            new LoginRequest(email, "WrongPassword1"));

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Login_with_unknown_email_returns_401_not_404()
    {
        var response = await _client.PostAsJsonAsync("/auth/login",
            new LoginRequest($"unknown-{Guid.NewGuid():N}@example.com", "whatever123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

- [x] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/PingMe.IntegrationTests/PingMe.IntegrationTests.csproj --filter FullyQualifiedName~AuthTests`
Expected: FAIL — all 5 facts fail with 404 Not Found (no `AuthController` exists yet to map `/auth/register-tenant` or `/auth/login`).

- [x] **Step 4: Create `AuthController.cs`**

```csharp
namespace PingMe.Api.Controllers;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PingMe.Api.Contracts.Auth;
using PingMe.Domain.Tenants;
using PingMe.Infrastructure.Identity;
using PingMe.Infrastructure.Persistence;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly PingMeDbContext _dbContext;
    private readonly UserManager<AppUser> _userManager;
    private readonly JwtTokenGenerator _tokenGenerator;
    private readonly PasswordHasher<AppUser> _passwordHasher = new();

    public AuthController(PingMeDbContext dbContext, UserManager<AppUser> userManager, JwtTokenGenerator tokenGenerator)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _tokenGenerator = tokenGenerator;
    }

    [HttpPost("register-tenant")]
    public async Task<ActionResult<AuthResponse>> RegisterTenant(RegisterTenantRequest request)
    {
        var normalizedEmail = request.OwnerEmail.ToUpperInvariant();
        var emailTaken = await _dbContext.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.NormalizedEmail == normalizedEmail);

        if (emailTaken)
        {
            return Conflict("An account with this email already exists.");
        }

        var tenant = new Tenant(request.TenantName, DateTime.UtcNow);
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var owner = new AppUser
        {
            TenantId = tenant.Id,
            UserName = request.OwnerEmail,
            Email = request.OwnerEmail,
        };

        var createResult = await _userManager.CreateAsync(owner, request.OwnerPassword);
        if (!createResult.Succeeded)
        {
            return BadRequest(createResult.Errors.Select(e => e.Description));
        }

        await _userManager.AddToRoleAsync(owner, "Owner");

        var token = _tokenGenerator.GenerateToken(owner, new List<string> { "Owner" });
        return Created(string.Empty, new AuthResponse(token));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        return Unauthorized();
    }
}
```

`Login` is a deliberate stub returning `401` unconditionally — this task only needs `RegisterTenant` and the duplicate-email check to go green; the 3 login-related facts stay red until Task 8. The uniqueness check uses `IgnoreQueryFilters()` because at this point in the request no JWT exists yet, so `CurrentTenantProvider.TenantId` is `null` — without bypassing the filter, this query would see zero existing users regardless of how many are actually registered, and duplicate emails across tenants would never be caught (see this plan's Assumptions section).

- [x] **Step 5: Run the tests again**

Run: `dotnet test tests/PingMe.IntegrationTests/PingMe.IntegrationTests.csproj --filter FullyQualifiedName~AuthTests`
Expected: `RegisterTenant_returns_201_with_a_token` and `RegisterTenant_with_duplicate_email_returns_409` PASS. The 3 `Login_*` facts still FAIL (expected — `Login` is a stub, that's Task 8's job).

- [x] **Step 6: Commit**

```bash
git add src/PingMe.Api tests/PingMe.IntegrationTests
git commit -m "Add RegisterTenant endpoint with manual cross-tenant email-uniqueness check (TDD)"
```

_Completed 2026-08-26: commit `bf83ae4`, plus follow-up `9d8af31` (discovered `pingme_test` predated the Identity migration since Plan 1 created it via `EnsureCreatedAsync` with no migration tracking; replaced a fragile hardcoded-migration-ID reconciliation with `EnsureDeleted()`+`Migrate()` on factory construction, and disabled xUnit parallel test collections to prevent races with Plan 1's `TenantIsolationTests` — verified stable across 2 consecutive full test runs). Noted non-blocking: TOCTOU on email uniqueness check, no transaction around tenant+user creation — out of this task's scope._

---

### Task 8: AuthController.Login (TDD)

**Files:**
- Modify: `src/PingMe.Api/Controllers/AuthController.cs`

**Does NOT cover:** rate limiting or account lockout after repeated failed attempts — out of MVP scope.

- [x] **Step 1: Confirm the 3 `Login_*` tests are still failing** (carried over from Task 7 — no new test file needed)

Run: `dotnet test tests/PingMe.IntegrationTests/PingMe.IntegrationTests.csproj --filter FullyQualifiedName~AuthTests`
Expected: FAIL on `Login_after_register_succeeds`, `Login_with_wrong_password_returns_401`, `Login_with_unknown_email_returns_401_not_404` (all currently hit the `Unauthorized()` stub, so `Login_with_wrong_password_returns_401` and `Login_with_unknown_email_returns_401_not_404` might already pass by coincidence — the meaningful RED signal is `Login_after_register_succeeds`, which cannot pass against a stub that always returns 401).

- [x] **Step 2: Replace the `Login` method in `AuthController.cs`**

```csharp
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var normalizedEmail = request.Email.ToUpperInvariant();
        var user = await _dbContext.Users
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

        if (user is null)
        {
            return Unauthorized();
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, request.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenGenerator.GenerateToken(user, roles);
        return Ok(new AuthResponse(token));
    }
```

Same reasoning as `RegisterTenant`: the lookup by email must run with `IgnoreQueryFilters()` because the tenant isn't known until *after* this lookup succeeds — that's the whole point of resolving identity by email first. Both the "user not found" and "wrong password" branches return the same `401 Unauthorized` with no distinguishing detail, per spec Section 8 ("no distinction leaked between 'wrong password' and 'unknown user'").

- [x] **Step 3: Run the tests to verify they pass**

Run: `dotnet test tests/PingMe.IntegrationTests/PingMe.IntegrationTests.csproj --filter FullyQualifiedName~AuthTests`
Expected: PASS — all 5 facts in `AuthTests` green.

- [x] **Step 4: Commit**

```bash
git add src/PingMe.Api/Controllers/AuthController.cs
git commit -m "Implement Login endpoint (TDD) — no wrong-password-vs-unknown-user distinction leaked"
```

_Completed 2026-08-26: commit `a0ef39a`. 13/13 tests pass (4 unit + 9 integration). Confirmed PasswordVerificationResult.SuccessRehashNeeded correctly falls through to a successful login._

---

### Task 9: Catalog DTOs

**Files:**
- Create: `src/PingMe.Api/Contracts/Catalog/MenuDto.cs`
- Create: `src/PingMe.Api/Contracts/Catalog/CreateMenuRequest.cs`
- Create: `src/PingMe.Api/Contracts/Catalog/CategoryDto.cs`
- Create: `src/PingMe.Api/Contracts/Catalog/CreateCategoryRequest.cs`
- Create: `src/PingMe.Api/Contracts/Catalog/ProductDto.cs`
- Create: `src/PingMe.Api/Contracts/Catalog/CreateProductRequest.cs`
- Create: `src/PingMe.Api/Contracts/Catalog/UpdateProductAvailabilityRequest.cs`
- Create: `src/PingMe.Api/Contracts/Catalog/ProductOptionDto.cs`
- Create: `src/PingMe.Api/Contracts/Catalog/CreateProductOptionRequest.cs`

- [ ] **Step 1: Create all 9 DTO files**

`src/PingMe.Api/Contracts/Catalog/MenuDto.cs`:
```csharp
namespace PingMe.Api.Contracts.Catalog;

public record MenuDto(Guid Id, string Name);
```

`src/PingMe.Api/Contracts/Catalog/CreateMenuRequest.cs`:
```csharp
namespace PingMe.Api.Contracts.Catalog;

public record CreateMenuRequest(string Name);
```

`src/PingMe.Api/Contracts/Catalog/CategoryDto.cs`:
```csharp
namespace PingMe.Api.Contracts.Catalog;

public record CategoryDto(Guid Id, Guid MenuId, string Name, int SortOrder);
```

`src/PingMe.Api/Contracts/Catalog/CreateCategoryRequest.cs`:
```csharp
namespace PingMe.Api.Contracts.Catalog;

public record CreateCategoryRequest(string Name, int SortOrder);
```

`src/PingMe.Api/Contracts/Catalog/ProductDto.cs`:
```csharp
namespace PingMe.Api.Contracts.Catalog;

public record ProductDto(Guid Id, Guid CategoryId, string Name, decimal Price, bool IsAvailable);
```

`src/PingMe.Api/Contracts/Catalog/CreateProductRequest.cs`:
```csharp
namespace PingMe.Api.Contracts.Catalog;

public record CreateProductRequest(string Name, decimal Price);
```

`src/PingMe.Api/Contracts/Catalog/UpdateProductAvailabilityRequest.cs`:
```csharp
namespace PingMe.Api.Contracts.Catalog;

public record UpdateProductAvailabilityRequest(bool IsAvailable);
```

`src/PingMe.Api/Contracts/Catalog/ProductOptionDto.cs`:
```csharp
namespace PingMe.Api.Contracts.Catalog;

public record ProductOptionDto(Guid Id, Guid ProductId, string Name, decimal PriceDelta);
```

`src/PingMe.Api/Contracts/Catalog/CreateProductOptionRequest.cs`:
```csharp
namespace PingMe.Api.Contracts.Catalog;

public record CreateProductOptionRequest(string Name, decimal PriceDelta);
```

- [ ] **Step 2: Verify the solution builds**

Run: `dotnet build PingMe.slnx`
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add src/PingMe.Api/Contracts/Catalog
git commit -m "Add Catalog admin DTOs"
```

---

### Task 10: MenusController — Menu and Category CRUD (Owner-only)

**Files:**
- Create: `src/PingMe.Api/Controllers/MenusController.cs`

**Does NOT cover:** deleting a menu or category, or reordering categories beyond accepting a `SortOrder` on creation — out of scope per this plan's Assumptions.

- [ ] **Step 1: Create `MenusController.cs`**

```csharp
namespace PingMe.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PingMe.Api.Contracts.Catalog;
using PingMe.Application.Tenants;
using PingMe.Domain.Catalog;
using PingMe.Infrastructure.Persistence;

[ApiController]
[Route("admin/menus")]
[Authorize(Roles = "Owner")]
public class MenusController : ControllerBase
{
    private readonly PingMeDbContext _dbContext;
    private readonly ICurrentTenantProvider _currentTenantProvider;

    public MenusController(PingMeDbContext dbContext, ICurrentTenantProvider currentTenantProvider)
    {
        _dbContext = dbContext;
        _currentTenantProvider = currentTenantProvider;
    }

    [HttpGet]
    public async Task<ActionResult<List<MenuDto>>> GetMenus()
    {
        var menus = await _dbContext.Menus
            .Select(m => new MenuDto(m.Id, m.Name))
            .ToListAsync();
        return Ok(menus);
    }

    [HttpPost]
    public async Task<ActionResult<MenuDto>> CreateMenu(CreateMenuRequest request)
    {
        var menu = new Menu(_currentTenantProvider.TenantId!.Value, request.Name);
        _dbContext.Menus.Add(menu);
        await _dbContext.SaveChangesAsync();
        return Created(string.Empty, new MenuDto(menu.Id, menu.Name));
    }

    [HttpPost("{menuId}/categories")]
    public async Task<ActionResult<CategoryDto>> CreateCategory(Guid menuId, CreateCategoryRequest request)
    {
        var menuExists = await _dbContext.Menus.AnyAsync(m => m.Id == menuId);
        if (!menuExists)
        {
            return NotFound();
        }

        var category = new Category(_currentTenantProvider.TenantId!.Value, menuId, request.Name, request.SortOrder);
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();
        return Created(string.Empty, new CategoryDto(category.Id, category.MenuId, category.Name, category.SortOrder));
    }
}
```

`_currentTenantProvider.TenantId!.Value` is safe here (never actually null at runtime) because `[Authorize(Roles = "Owner")]` guarantees `TenantResolutionMiddleware` already ran on an authenticated request and set it from the JWT's `tenantId` claim before this action executes. `menuExists` is checked against `_dbContext.Menus`, which is tenant-filtered — a `menuId` belonging to another tenant is invisible here and correctly produces `404`, not a cross-tenant category creation.

- [ ] **Step 2: Verify the solution builds**

Run: `dotnet build PingMe.slnx`
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add src/PingMe.Api/Controllers/MenusController.cs
git commit -m "Add MenusController: create/list menus, create categories under a menu"
```

---

### Task 11: ProductsController — Product and ProductOption CRUD (Owner-only)

**Files:**
- Create: `src/PingMe.Api/Controllers/ProductsController.cs`

**Does NOT cover:** deleting products/options, editing name or price after creation (only availability toggling), or listing options for a product — out of scope for this plan; add in a later plan if the Admin UI needs it.

- [ ] **Step 1: Create `ProductsController.cs`**

```csharp
namespace PingMe.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PingMe.Api.Contracts.Catalog;
using PingMe.Application.Tenants;
using PingMe.Domain.Catalog;
using PingMe.Infrastructure.Persistence;

[ApiController]
[Route("admin/products")]
[Authorize(Roles = "Owner")]
public class ProductsController : ControllerBase
{
    private readonly PingMeDbContext _dbContext;
    private readonly ICurrentTenantProvider _currentTenantProvider;

    public ProductsController(PingMeDbContext dbContext, ICurrentTenantProvider currentTenantProvider)
    {
        _dbContext = dbContext;
        _currentTenantProvider = currentTenantProvider;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> GetById(Guid id)
    {
        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        return Ok(new ProductDto(product.Id, product.CategoryId, product.Name, product.Price, product.IsAvailable));
    }

    [HttpPost("categories/{categoryId}")]
    public async Task<ActionResult<ProductDto>> Create(Guid categoryId, CreateProductRequest request)
    {
        var categoryExists = await _dbContext.Categories.AnyAsync(c => c.Id == categoryId);
        if (!categoryExists)
        {
            return NotFound();
        }

        var product = new Product(_currentTenantProvider.TenantId!.Value, categoryId, request.Name, request.Price);
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();
        return Created(string.Empty, new ProductDto(product.Id, product.CategoryId, product.Name, product.Price, product.IsAvailable));
    }

    [HttpPut("{id}/availability")]
    public async Task<IActionResult> UpdateAvailability(Guid id, UpdateProductAvailabilityRequest request)
    {
        var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        product.SetAvailability(request.IsAvailable);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{productId}/options")]
    public async Task<ActionResult<ProductOptionDto>> AddOption(Guid productId, CreateProductOptionRequest request)
    {
        var productExists = await _dbContext.Products.AnyAsync(p => p.Id == productId);
        if (!productExists)
        {
            return NotFound();
        }

        var option = new ProductOption(productId, request.Name, request.PriceDelta);
        _dbContext.ProductOptions.Add(option);
        await _dbContext.SaveChangesAsync();
        return Created(string.Empty, new ProductOptionDto(option.Id, option.ProductId, option.Name, option.PriceDelta));
    }
}
```

`AddOption` inserts directly into `_dbContext.ProductOptions` — this does not violate the Plan 1 "never query `ProductOption` standalone" hard rule, because inserting isn't a query (query filters only affect reads) and the parent-existence check (`_dbContext.Products.AnyAsync(p => p.Id == productId)`) already goes through the tenant-filtered `Products` set, so a `productId` belonging to another tenant is rejected with `404` before any `ProductOption` row is touched. No endpoint in this controller (or anywhere else in the codebase) lists or fetches a `ProductOption` independent of its parent `Product`.

- [ ] **Step 2: Verify the solution builds**

Run: `dotnet build PingMe.slnx`
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add src/PingMe.Api/Controllers/ProductsController.cs
git commit -m "Add ProductsController: create/read products, toggle availability, add options"
```

---

### Task 12: Integration tests — cross-tenant isolation at the HTTP layer (security-critical)

**Files:**
- Create: `tests/PingMe.IntegrationTests/Api/CatalogAdminIsolationTests.cs`

**Does NOT cover:** the ordering-endpoint cross-tenant test from spec Section 9 ("session from Tenant A submits Tenant B's productId on `POST /orders`") — that endpoint doesn't exist until Plan 3; this task closes the *admin-API* half of HTTP-layer isolation, not the customer-ordering half.

- [ ] **Step 1: Create `CatalogAdminIsolationTests.cs`**

```csharp
namespace PingMe.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PingMe.Api.Contracts.Auth;
using PingMe.Api.Contracts.Catalog;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class CatalogAdminIsolationTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public CatalogAdminIsolationTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<string> RegisterAndLoginAsync(HttpClient client)
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Venue", email, "P@ssw0rd123"));

        var loginResponse = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(email, "P@ssw0rd123"));
        var body = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    private static async Task<ProductDto> CreateProductForTenantAsync(HttpClient client)
    {
        var menuResponse = await client.PostAsJsonAsync("/admin/menus", new CreateMenuRequest("Menu"));
        var menu = await menuResponse.Content.ReadFromJsonAsync<MenuDto>();

        var categoryResponse = await client.PostAsJsonAsync(
            $"/admin/menus/{menu!.Id}/categories", new CreateCategoryRequest("Category", 1));
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>();

        var productResponse = await client.PostAsJsonAsync(
            $"/admin/products/categories/{category!.Id}", new CreateProductRequest("Product", 9.99m));
        return (await productResponse.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    [Fact]
    public async Task TenantA_cannot_read_TenantB_product_via_admin_api()
    {
        var clientA = _factory.CreateClient();
        var clientB = _factory.CreateClient();

        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await RegisterAndLoginAsync(clientA));
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await RegisterAndLoginAsync(clientB));

        var productB = await CreateProductForTenantAsync(clientB);

        var response = await clientA.GetAsync($"/admin/products/{productB.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TenantA_cannot_modify_TenantB_product_availability()
    {
        var clientA = _factory.CreateClient();
        var clientB = _factory.CreateClient();

        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await RegisterAndLoginAsync(clientA));
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await RegisterAndLoginAsync(clientB));

        var productB = await CreateProductForTenantAsync(clientB);

        var response = await clientA.PutAsJsonAsync(
            $"/admin/products/{productB.Id}/availability", new UpdateProductAvailabilityRequest(false));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TenantA_cannot_add_option_to_TenantB_product()
    {
        var clientA = _factory.CreateClient();
        var clientB = _factory.CreateClient();

        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await RegisterAndLoginAsync(clientA));
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await RegisterAndLoginAsync(clientB));

        var productB = await CreateProductForTenantAsync(clientB);

        var response = await clientA.PostAsJsonAsync(
            $"/admin/products/{productB.Id}/options", new CreateProductOptionRequest("Extra cheese", 1.50m));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_request_to_admin_endpoint_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/admin/menus");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run the tests to verify they pass**

Run: `dotnet test tests/PingMe.IntegrationTests/PingMe.IntegrationTests.csproj --filter FullyQualifiedName~CatalogAdminIsolationTests`
Expected: PASS — all 4 facts green. If `TenantA_cannot_read_TenantB_product_via_admin_api` instead fails with a `200 OK`, that means `TenantResolutionMiddleware` or the tenant filter isn't wired correctly — stop and fix before continuing, this is the security-critical case the whole plan exists to prove.

- [ ] **Step 3: Run the full test suite and full solution build**

Run:
```bash
dotnet build PingMe.slnx
dotnet test PingMe.slnx
```
Expected: PASS — solution builds, all tests pass (8 from Plan 1 + 5 `AuthTests` + 4 `CatalogAdminIsolationTests` = 17 total).

- [ ] **Step 4: Commit**

```bash
git add tests/PingMe.IntegrationTests
git commit -m "Add HTTP-layer cross-tenant isolation tests for Catalog admin endpoints"
```

---

### Task 13: Final verification and progress tracking

**Files:**
- Modify: `Docs/superpowers optimized/plans/PROGRESS.md`

- [ ] **Step 1: Run the full solution build and test suite one more time from a clean state**

Run:
```bash
dotnet build PingMe.slnx
dotnet test PingMe.slnx
```
Expected: PASS — 0 build errors, all 17 tests passing.

- [ ] **Step 2: Manually verify Swagger works end-to-end** (not automatable in this plan — do this once by hand)

Run `dotnet run --project src/PingMe.Api`, open the printed HTTPS URL + `/swagger` in a browser, use "Authorize" with a token obtained by calling `POST /auth/register-tenant` via the Swagger UI itself, then call `POST /admin/menus` and confirm it returns `201` with a menu. Stop the running process afterward.

- [ ] **Step 3: Update `Docs/superpowers optimized/plans/PROGRESS.md`**

Update the Plan 2 row's Status to `Done, reviewed` and Tasks done to `13 / 13` once execution and review are complete (do this after, not before, the task-by-task review cycle — this step is a placeholder reminder for whoever executes the plan, not something to do while still mid-plan). Also remove the two Plan 2-related bullets from "Carried-forward items" (`User`/Identity entity + Swagger UI) since both are now closed, and add a note that the ordering-endpoint isolation test is still owed to Plan 3.

- [ ] **Step 4: Commit**

```bash
git add "Docs/superpowers optimized/plans/PROGRESS.md"
git commit -m "Close Plan 2 carried-forward items in progress tracker"
```

---

## Self-Review

**1. Spec coverage.**
- Section 1 scope: "Simple email/password auth (ASP.NET Core Identity + JWT) for Admin app" — delivered (Tasks 1-8).
- Section 3 Multi-Tenancy: "Admin/Staff requests → from the authenticated user's TenantId claim" — delivered via `TenantResolutionMiddleware` (Task 5), the exact gap Plan 1 left open.
- Section 4 Module Boundaries: `AppUser` deliberately placed in Infrastructure rather than Domain, documented as a deviation with reasoning (Assumptions) rather than silently diverging from the spec's folder sketch.
- Section 6 API Contracts: `/admin/menu`-shaped endpoints (spec names it `/admin/menu`; this plan implements `/admin/menus` — a minor pluralization difference, noted here rather than left silently inconsistent). DTOs only cross the boundary, no EF entities serialized directly (checked: `MenuDto`, `CategoryDto`, `ProductDto`, `ProductOptionDto`, `AuthResponse` are all plain records, never `Menu`/`Category`/`Product`/`AppUser` themselves).
- Section 8 Error Handling: "Admin auth failure → standard 401/403; no distinction leaked between 'wrong password' and 'unknown user'" — delivered and tested (Task 7/8's `Login_with_wrong_password_returns_401` and `Login_with_unknown_email_returns_401_not_404` both assert `401`).
- Section 9 Testing Strategy: extends the security-critical cross-tenant test list to the HTTP layer for Catalog admin endpoints (Task 12) — the ordering-endpoint case is explicitly and correctly left to Plan 3 (stated in Task 12's Does NOT cover and this plan's Assumptions).
- Swagger UI checkpoint (decided in a prior session, tracked in `PROGRESS.md`) — delivered in Task 5, manually verified in Task 13.

**2. Placeholder scan.** No "TBD"/"TODO" strings. `Login`'s Task 7 stub (`return Unauthorized();`) is not a placeholder in the forbidden sense — it's a deliberate, temporary TDD state explicitly called out as such, immediately replaced in the very next task with working code, and its presence is proven by a passing/failing test at each step rather than asserted by prose.

**3. Type consistency.** `PingMeDbContext`'s constructor signature is unchanged from Plan 1 (`DbContextOptions<PingMeDbContext>, ICurrentTenantProvider`) — Task 3 only changes the base class and reorders `base.OnModelCreating`, avoiding a repeat of any "signature drifts across tasks" risk. `AppUser`, `JwtTokenGenerator`, and all DTOs are defined once (Tasks 2, 4, 9) and referenced identically by name in every later task (`AuthController`, `MenusController`, `ProductsController`, both test files) — cross-checked all constructor calls (`new Menu(tenantId, name)`, `new Category(tenantId, menuId, name, sortOrder)`, `new Product(tenantId, categoryId, name, price)`, `new ProductOption(productId, name, priceDelta)`) against their Plan 1 definitions; all match.

---

## Execution Handoff

**Plan complete and saved to `Docs/superpowers optimized/plans/2026-08-25-pingme-plan-2-auth-catalog-admin.md`. Two execution options:**

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** — Execute tasks in this session using executing-plans, with checkpoints

**Which approach?**
