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

## Status

Early scaffolding. See [Docs/Ideas/Scratch_1/Scratch_1.md](Docs/Ideas/Scratch_1/Scratch_1.md) for the architecture and MVP planning notes.
