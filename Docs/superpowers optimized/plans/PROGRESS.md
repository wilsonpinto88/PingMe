# PingMe Implementation Progress

Cross-plan tracker. Each plan file owns its own task checkboxes (`- [ ]`) — this file only tracks plan-level status so you can see where the whole MVP stands at a glance. Update the **Status** and **Tasks done** columns as plans are executed; do not duplicate individual task descriptions here.

| # | Plan | File | Status | Tasks done |
|---|------|------|--------|------------|
| 1 | Bootstrap + Domain + EF Core + Multi-tenancy | [2026-08-25-pingme-plan-1-bootstrap-domain-multitenancy.md](2026-08-25-pingme-plan-1-bootstrap-domain-multitenancy.md) | Done, reviewed — merged to main | 13 / 13 |
| 2 | Auth + Catalog/Admin | [2026-08-25-pingme-plan-2-auth-catalog-admin.md](2026-08-25-pingme-plan-2-auth-catalog-admin.md) | Done, reviewed — merged to main | 13 / 13 |
| 3 | Locations/QR + Customer app + Ordering | [2026-08-26-pingme-plan-3-locations-qr-customer-ordering.md](2026-08-26-pingme-plan-3-locations-qr-customer-ordering.md) | In progress (branch `plan-3-locations-qr-customer-ordering`) | 11 / 17 |
| 4 | SignalR + Staff dashboard + Deployment | *(not yet written)* | Not started | — |

**Status values:** `Not started` → `Written, not started` → `In progress` → `Done, reviewed`.

## Carried-forward items

Requirements from the spec that a plan explicitly defers to a later plan — tracked here so they aren't dropped:

- **Plan 1 → Plan 3:** spec Section 9's test case "CustomerSession from Tenant A submitting a Tenant B `productId` on `POST /orders` is rejected" needs the ordering HTTP endpoint. Add it to Plan 3's task list.
- **Plan 2 → Plan 3:** the admin-API half of HTTP-layer cross-tenant isolation is closed (`CatalogAdminIsolationTests`, Plan 2 Task 12); the ordering-endpoint isolation test above still requires Plan 3.

## How to use this file

- Before writing the next plan, check this table for "Carried-forward items" targeting it.
- After finishing a plan's execution and review, flip its Status to `Done, reviewed` and update `state.md`'s Plan Status to point at the next plan.
- When a new plan is written, add its row here (replace `*(not yet written)*` with the filename link).
