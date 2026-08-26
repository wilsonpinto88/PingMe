# Graph Report - Docs  (2026-08-26)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 305 nodes · 502 edges · 13 communities (12 shown, 1 thin omitted)
- Extraction: 99% EXTRACTED · 1% INFERRED · 0% AMBIGUOUS · INFERRED: 5 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `587b60e7`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- PingMeDbContext
- PingMe.Infrastructure.Persistence
- PingMe.IntegrationTests
- AuthTests
- AuthController
- PingMe.Api.Contracts.Catalog
- TenantIsolationTests.cs
- Order
- MenusController
- http
- .CreateContext
- xunit.runner.json
- CatalogAdminIsolationTests

## God Nodes (most connected - your core abstractions)
1. `PingMeDbContext` - 23 edges
2. `PingMe.Domain.Common` - 15 edges
3. `PingMe.IntegrationTests` - 14 edges
4. `Entity` - 13 edges
5. `Order` - 13 edges
6. `PingMe.Infrastructure.Persistence` - 13 edges
7. `PingMe.Infrastructure` - 12 edges
8. `PingMe.Api.Contracts.Catalog` - 12 edges
9. `ITenantOwned` - 11 edges
10. `PingMe.Infrastructure.Persistence.Migrations` - 11 edges

## Surprising Connections (you probably didn't know these)
- `PingMeWebApplicationFactory` --references--> `Program`  [EXTRACTED]
  tests/PingMe.IntegrationTests/Infrastructure/PingMeWebApplicationFactory.cs → src/PingMe.Api/Program.cs
- `PingMe.IntegrationTests` --references--> `net10.0`  [EXTRACTED]
  tests/PingMe.IntegrationTests/PingMe.IntegrationTests.csproj → src/PingMe.Api/PingMe.Api.csproj
- `PingMe.IntegrationTests` --references--> `Microsoft.NET.Sdk`  [EXTRACTED]
  tests/PingMe.IntegrationTests/PingMe.IntegrationTests.csproj → src/PingMe.Domain/PingMe.Domain.csproj
- `PingMe.UnitTests` --references--> `net10.0`  [EXTRACTED]
  tests/PingMe.UnitTests/PingMe.UnitTests.csproj → src/PingMe.Api/PingMe.Api.csproj
- `PingMe.UnitTests` --references--> `Microsoft.NET.Sdk`  [EXTRACTED]
  tests/PingMe.UnitTests/PingMe.UnitTests.csproj → src/PingMe.Domain/PingMe.Domain.csproj

## Import Cycles
- None detected.

## Communities (13 total, 1 thin omitted)

### Community 0 - "PingMeDbContext"
Cohesion: 0.06
Nodes (37): PingMe.Domain.Tenants, PingMe.Infrastructure.Identity, PingMe.Domain.Catalog, PingMe.Domain.Common, PingMe.Domain.Locations, DbSet, IdentityDbContext, IdentityRole (+29 more)

### Community 1 - "PingMe.Infrastructure.Persistence"
Cohesion: 0.06
Nodes (21): PingMe.Infrastructure.Persistence, PingMe.Infrastructure.Persistence.Migrations, Migration, ModelSnapshot, MigrationBuilder, ModelBuilder, InitialCreate, MigrationBuilder (+13 more)

### Community 2 - "PingMe.IntegrationTests"
Cohesion: 0.11
Nodes (29): Microsoft.AspNetCore.Authentication.JwtBearer (10.0.11), Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.11), Microsoft.AspNetCore.Mvc.Testing (10.0.11), Microsoft.AspNetCore.OpenApi (10.0.2), Microsoft.EntityFrameworkCore.Design (10.0.11), Microsoft.IdentityModel.Tokens (8.22.0), Swashbuckle.AspNetCore (10.2.3), System.IdentityModel.Tokens.Jwt (8.22.0) (+21 more)

### Community 3 - "AuthTests"
Cohesion: 0.21
Nodes (8): IClassFixture, IWebHostBuilder, Fact, HttpClient, Task, AuthTests, PingMeWebApplicationFactory, WebApplicationFactory

### Community 4 - "AuthController"
Cohesion: 0.12
Nodes (15): PingMe.IntegrationTests.Infrastructure, PingMe.IntegrationTests.Api, PingMe.Api.Contracts.Auth, IConfiguration, IList, PasswordHasher, AuthResponse, LoginRequest (+7 more)

### Community 5 - "PingMe.Api.Contracts.Catalog"
Cohesion: 0.12
Nodes (16): PingMe.Api.Contracts.Catalog, HttpPut, IActionResult, CreateProductOptionRequest, CreateProductRequest, Guid, ProductDto, Guid (+8 more)

### Community 6 - "TenantIsolationTests.cs"
Cohesion: 0.10
Nodes (16): PingMe.Api.Controllers, PingMe.IntegrationTests.MultiTenancy, PingMe.Application.Tenants, PingMe.Api.Middleware, PingMe.Infrastructure.Tenants, HttpContext, RequestDelegate, Task (+8 more)

### Community 7 - "Order"
Cohesion: 0.13
Nodes (13): PingMe.Domain.Ordering, PingMe.UnitTests.Ordering, Dictionary, IReadOnlyCollection, DateTime, Guid, List, Order (+5 more)

### Community 8 - "MenusController"
Cohesion: 0.13
Nodes (14): ControllerBase, Guid, CategoryDto, CreateCategoryRequest, CreateMenuRequest, Guid, MenuDto, ActionResult (+6 more)

### Community 9 - "http"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 10 - ".CreateContext"
Cohesion: 0.50
Nodes (4): Fact, Guid, Task, TenantIsolationTests

### Community 12 - "CatalogAdminIsolationTests"
Cohesion: 0.53
Nodes (4): Fact, HttpClient, Task, CatalogAdminIsolationTests

## Knowledge Gaps
- **33 isolated node(s):** `parallelizeTestCollections`, `$schema`, `Microsoft.AspNetCore.Authentication.JwtBearer (10.0.11)`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore (10.0.11)`, `Microsoft.AspNetCore.Mvc.Testing (10.0.11)` (+28 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **1 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `PingMe.Infrastructure.Persistence` connect `PingMe.Infrastructure.Persistence` to `PingMeDbContext`, `AuthTests`, `TenantIsolationTests.cs`?**
  _High betweenness centrality (0.270) - this node is a cross-community bridge._
- **Why does `PingMeDbContext` connect `PingMeDbContext` to `AuthController`, `PingMe.Api.Contracts.Catalog`, `TenantIsolationTests.cs`, `Order`, `MenusController`, `.CreateContext`?**
  _High betweenness centrality (0.268) - this node is a cross-community bridge._
- **Why does `ProductsController` connect `PingMe.Api.Contracts.Catalog` to `MenusController`, `PingMeDbContext`, `TenantIsolationTests.cs`?**
  _High betweenness centrality (0.099) - this node is a cross-community bridge._
- **What connects `parallelizeTestCollections`, `$schema`, `Microsoft.AspNetCore.Authentication.JwtBearer (10.0.11)` to the rest of the system?**
  _33 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `PingMeDbContext` be split into smaller, more focused modules?**
  _Cohesion score 0.06233766233766234 - nodes in this community are weakly interconnected._
- **Should `PingMe.Infrastructure.Persistence` be split into smaller, more focused modules?**
  _Cohesion score 0.05851063829787234 - nodes in this community are weakly interconnected._
- **Should `PingMe.IntegrationTests` be split into smaller, more focused modules?**
  _Cohesion score 0.1103448275862069 - nodes in this community are weakly interconnected._