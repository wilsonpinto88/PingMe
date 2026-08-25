# PingMe

QR/NFC-driven ordering platform for restaurants, bars, stadiums, cinemas and other venues. A customer scans a code tied to a physical location, browses the venue menu, and places an order without waiting for staff. Staff receive the order in real time and deliver it to that location.

## Stack

- **Customer / Admin frontends** — React + TypeScript + Vite (pnpm monorepo, `src/pingme-web`)
- **Backend** — ASP.NET Core / .NET 10, modular monolith (`Identity`, `Tenants`, `Catalog`, `Locations`, `Ordering`)
- **ORM** — Entity Framework Core
- **Database** — PostgreSQL
- **Realtime** — SignalR
- **CI/CD** — GitHub Actions

## Repository structure

```
PingMe/
├── src/
│   ├── PingMe.Api/
│   ├── PingMe.Application/
│   ├── PingMe.Domain/
│   ├── PingMe.Infrastructure/
│   └── pingme-web/            (React customer + admin apps)
├── tests/
│   ├── PingMe.UnitTests/
│   └── PingMe.IntegrationTests/
├── Docs/
├── .github/workflows/
└── PingMe.slnx
```

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL](https://www.postgresql.org/download/) running locally (default expected: `localhost:5432`, user `postgres`, password `postgres` — adjust the connection string if yours differs)
- `dotnet-ef` CLI tool:
  ```bash
  dotnet tool install --global dotnet-ef
  ```

### Clone and build

```bash
git clone https://github.com/wilsonpinto88/PingMe.git
cd PingMe
dotnet build PingMe.slnx
```

### Set up the database

Create the `pingme_dev` database and apply migrations:

```bash
dotnet ef database update --project src/PingMe.Infrastructure --startup-project src/PingMe.Api
```

This reads the connection string from `src/PingMe.Api/appsettings.Development.json` (`ConnectionStrings:PingMe`). Update it there if your local PostgreSQL credentials differ from the default.

The integration tests use a separate `pingme_test` database (created automatically on first test run) — no manual setup needed for it.

### Run the tests

```bash
dotnet test PingMe.slnx
```

### Run the API

```bash
dotnet run --project src/PingMe.Api
```

## Status

Plan 1 (bootstrap, domain model, EF Core, multi-tenancy) is complete and merged — see [Docs/superpowers optimized/plans/PROGRESS.md](Docs/superpowers%20optimized/plans/PROGRESS.md) for what's done and what's next. No HTTP endpoints exist yet; that starts with Plan 2. See [Docs/Ideas/Scratch_1/Scratch_1.md](Docs/Ideas/Scratch_1/Scratch_1.md) for the original architecture and MVP planning notes.
