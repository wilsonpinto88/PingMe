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

### Run the frontends

Both apps live in one pnpm workspace:

```bash
cd src/pingme-web
pnpm install
pnpm --filter pingme-customer-app dev   # customer ordering app, port 5173
pnpm --filter pingme-staff-app dev      # staff order board, port 5174
```

The customer app is reached at `/p/{code}`, where `code` is a QR code created
through `POST /admin/qrcodes` — for example `http://localhost:5173/p/AB12CD34`.

## Testing on a phone

Both dev servers bind to every network interface, so a phone on the same Wi-Fi
can open them. Three things have to line up:

1. **Start the API on the network**, not just on localhost:

   ```bash
   dotnet run --project src/PingMe.Api --launch-profile lan
   ```

2. **Point the frontends at your machine.** Copy `.env.example` to `.env.local`
   in each app and set your LAN IP:

   ```
   VITE_API_BASE_URL=http://192.168.1.42:5190
   ```

3. **Allow the phone's origin through CORS.** Add the Network URLs that `pnpm
   dev` prints to `Cors:AllowedOrigins` in
   `src/PingMe.Api/appsettings.Development.json`, then restart the API.

HTTPS redirection is disabled in Development on purpose: a phone would be sent
to a dev certificate it does not trust.

## Per-venue branding

Each venue carries its own branding, and the customer app themes itself from it
at runtime — no rebuild, no per-venue code. Owners set it with:

```
PUT /admin/venues/{id}/branding
{
  "primaryColor": "#0F766E",
  "accentColor":  "#FACC15",
  "currencyCode": "GBP",
  "themeMode":    "Dark",
  "logoUrl":      "https://cdn.example.com/logo.png",
  "heroImageUrl": "https://cdn.example.com/hero.jpg",
  "tagline":      "Cocktails with a view"
}
```

| Field | Effect in the customer app |
| --- | --- |
| `primaryColor` | Header background, primary buttons, mobile browser chrome |
| `accentColor` | Add-to-order buttons, the live step on the order tracker, focus rings |
| `currencyCode` | Every price, formatted for the visitor's locale |
| `themeMode` | `Light` or `Dark` surface palette |
| `logoUrl` | Replaces the venue name in the header |
| `heroImageUrl` | Full-bleed header photo behind a readable scrim |
| `tagline` | One line under the venue name |

Text placed on a brand colour is chosen for contrast at runtime, so a pale
brand colour still produces readable buttons. Colours must be `#RRGGBB` and
image URLs must be absolute `http(s)` — anything else is rejected with a 400.

Products also take a `description` and an `imageUrl` through
`PUT /admin/products/{id}/presentation`.

## Status

Plan 1 (bootstrap, domain model, EF Core, multi-tenancy) is complete and merged — see [Docs/superpowers optimized/plans/PROGRESS.md](Docs/superpowers%20optimized/plans/PROGRESS.md) for what's done and what's next. No HTTP endpoints exist yet; that starts with Plan 2. See [Docs/Ideas/Scratch_1/Scratch_1.md](Docs/Ideas/Scratch_1/Scratch_1.md) for the original architecture and MVP planning notes.
