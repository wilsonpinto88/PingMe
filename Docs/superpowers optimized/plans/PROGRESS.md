# PingMe Implementation Progress

Cross-plan tracker. Each plan file owns its own task checkboxes (`- [ ]`) — this file only tracks plan-level status so you can see where the whole MVP stands at a glance. Update the **Status** and **Tasks done** columns as plans are executed; do not duplicate individual task descriptions here.

| # | Plan | File | Status | Tasks done |
|---|------|------|--------|------------|
| 1 | Bootstrap + Domain + EF Core + Multi-tenancy | [2026-08-25-pingme-plan-1-bootstrap-domain-multitenancy.md](2026-08-25-pingme-plan-1-bootstrap-domain-multitenancy.md) | Done, reviewed — merged to main | 13 / 13 |
| 2 | Auth + Catalog/Admin | [2026-08-25-pingme-plan-2-auth-catalog-admin.md](2026-08-25-pingme-plan-2-auth-catalog-admin.md) | Done, reviewed — merged to main | 13 / 13 |
| 3 | Locations/QR + Customer app + Ordering | [2026-08-26-pingme-plan-3-locations-qr-customer-ordering.md](2026-08-26-pingme-plan-3-locations-qr-customer-ordering.md) | Done, reviewed | 17 / 17 |
| 4 | SignalR + Staff dashboard + Deployment | [2026-08-26-pingme-plan-4-signalr-staff-deployment.md](2026-08-26-pingme-plan-4-signalr-staff-deployment.md) | Done, reviewed | 14 / 14 |
| 5 | POS/ERP Integration (Level 1 order-push MVP) | [2026-08-27-pingme-plan-5-pos-integration.md](2026-08-27-pingme-plan-5-pos-integration.md) | Done, reviewed | 10 / 10 |

**Status values:** `Not started` → `Written, not started` → `In progress` → `Done, reviewed`.

## Carried-forward items

Requirements from the spec that a plan explicitly defers to a later plan — tracked here so they aren't dropped:

- **Plan 1 → Plan 3:** spec Section 9's test case "CustomerSession from Tenant A submitting a Tenant B `productId` on `POST /orders` is rejected" needs the ordering HTTP endpoint. Add it to Plan 3's task list.
- **Plan 2 → Plan 3:** the admin-API half of HTTP-layer cross-tenant isolation is closed (`CatalogAdminIsolationTests`, Plan 2 Task 12); the ordering-endpoint isolation test above is now closed too (`OrderingIsolationTests`, Plan 3 Task 10).
- **Plan 3 → Plan 4:** no plan currently owns an Owner/Staff Admin React UI — Plan 4 only covers the Staff order dashboard (live orders + status transitions). Locations/QR/Catalog admin still has only API + Swagger access — remains open, not resolved by Plan 4, still needs a decision for a future plan.
- **Plan 4 → future:** Docker Compose covers API + Postgres only — `customer-app` and `staff-app` are not containerized and still require `pnpm dev`. No CI pipeline or cloud deployment target exists yet. Docker itself was unavailable in the execution environment during Plan 4 — `docker-compose.yml` was verified by manual YAML review only; a real `docker compose up` should be run once Docker is available.
- **Plan 5 → future:** `WebhookUrl` accepts any tenant-supplied URL with no SSRF hardening — explicitly flagged in Plan 5's spec as required before any production rollout that lets a tenant self-serve a webhook URL. Vendor-specific POS adapters (Zone Soft, PRIMAVERA, WinRest, etc.) remain unbuilt — no real API access/credentials exist yet for any of them.

## How to use this file

- Before writing the next plan, check this table for "Carried-forward items" targeting it.
- After finishing a plan's execution and review, flip its Status to `Done, reviewed` and update `state.md`'s Plan Status to point at the next plan.
- When a new plan is written, add its row here (replace `*(not yet written)*` with the filename link).
