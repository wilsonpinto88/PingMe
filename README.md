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

## Deploying (free tier)

Three services, all with a genuine no-cost tier at this scale: **Neon**
(Postgres), **Render** (the API, deployed from the existing Dockerfile), and
**Vercel** (both frontends, deployed independently from the same repo). CI
runs on every push via `.github/workflows/ci.yml`.

The one trade-off worth knowing before you demo this to anyone: Render's free
web service sleeps after 15 minutes idle, so the first request after a quiet
spell takes a few seconds to wake up.

### 1. Database — Neon

1. Create a free account at neon.tech, create a project, and create a
   database (any name — `pingme` is fine).
2. Copy the connection details from Neon's dashboard and build an Npgsql
   connection string in this exact shape (Neon's own connection string is a
   `postgres://` URI, which Npgsql does not read — convert it manually):

   ```
   Host=<neon-host>;Port=5432;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true
   ```

   Keep this for step 2.

### 2. API — Render

1. Create a free account at render.com and connect this GitHub repo.
2. **New → Web Service**, environment **Docker**:
   - Root Directory: leave blank (repo root) — the Dockerfile's `COPY . .`
     expects the whole repo as build context, same as `docker-compose.yml`.
   - Dockerfile Path: `src/PingMe.Api/Dockerfile`
   - Instance Type: Free
   - Health Check Path: `/health`
3. Set these environment variables on the service (**never** reuse the dev
   placeholder key from `appsettings.Development.json` for `Jwt__Key`):

   | Key | Value |
   | --- | --- |
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `ConnectionStrings__PingMe` | the Neon connection string from step 1 |
   | `Jwt__Key` | a long random secret, e.g. `openssl rand -base64 48` |
   | `Jwt__Issuer` | `PingMe` |
   | `Jwt__Audience` | `PingMeAdmin` |
   | `Jwt__ExpiryMinutes` | `60` |
   | `Cors__AllowedOrigins__0` | the customer app's Vercel URL (step 3) |
   | `Cors__AllowedOrigins__1` | the staff app's Vercel URL (step 3) |

   `PORT` is injected by Render automatically — the Dockerfile already listens
   on it. Database migrations run automatically on startup.
4. Deploy. Note the resulting `https://<service>.onrender.com` URL.

### 3. Frontends — Vercel

Create **two** Vercel projects from the same repo (one per app), since each
is an independent app that happens to share a pnpm workspace for local dev:

| Setting | customer-app project | staff-app project |
| --- | --- | --- |
| Root Directory | `src/pingme-web/customer-app` | `src/pingme-web/staff-app` |
| Framework Preset | Vite | Vite |
| Environment Variable | `VITE_API_BASE_URL=https://<render-service>.onrender.com` | same |

Deploy both, note their `https://<project>.vercel.app` URLs, then go back to
Render and set `Cors__AllowedOrigins__0`/`__1` to those exact URLs and
redeploy the API — without that step, the frontends load but every API call
fails CORS.

### 4. Verify

Open the customer app's Vercel URL at `/p/{code}` (register a tenant and
create a QR code first, same as the [local walkthrough](#getting-started)),
and confirm the staff app receives the order live.

## Status

Plan 1 (bootstrap, domain model, EF Core, multi-tenancy) is complete and merged — see [Docs/superpowers optimized/plans/PROGRESS.md](Docs/superpowers%20optimized/plans/PROGRESS.md) for what's done and what's next. No HTTP endpoints exist yet; that starts with Plan 2. See [Docs/Ideas/Scratch_1/Scratch_1.md](Docs/Ideas/Scratch_1/Scratch_1.md) for the original architecture and MVP planning notes.
