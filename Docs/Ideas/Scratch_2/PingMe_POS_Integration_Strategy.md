# PingMe — POS / ERP Integration Strategy

## Purpose

This document defines the longer-term integration strategy for PingMe with the POS/ERP and payment ecosystem used by restaurants, bars and other hospitality venues, particularly in Portugal.

The objective is **not** to replace the venue's existing POS/ERP infrastructure.

Instead:

> **PingMe should become the digital ordering layer that plugs into the venue's existing operational infrastructure.**

This can become a major B2B value proposition because a venue can adopt PingMe without replacing its existing POS, invoicing, accounting or payment processes.

---

# 1. Product Positioning

Instead of positioning PingMe as:

> "another digital menu / QR ordering system"

position it as:

> **"Keep your existing POS. PingMe connects to it and gives your customers a better ordering experience."**

The customer uses PingMe:

```text
QR / NFC
   ↓
PingMe Customer App
   ↓
Menu
   ↓
Cart
   ↓
Order
```

The venue's existing system remains the operational/fiscal system:

```text
PingMe
   ↓
POS / ERP
   ↓
Existing restaurant operations
```

---

# 2. Integration Levels

## Level 1 — Order Integration

The most important initial integration.

Customer:

```text
Scan QR
   ↓
PingMe
   ↓
Order
```

PingMe sends the order to the venue's POS.

Conceptually:

```json
{
  "location": "TABLE-12",
  "items": [
    {
      "product": "Burger",
      "quantity": 2
    },
    {
      "product": "Beer",
      "quantity": 2
    }
  ]
}
```

The POS receives the order and knows:

> Table 12 ordered 2 burgers + 2 beers.

The staff does not need to manually re-enter the order.

### Value

This alone can significantly reduce friction and order-entry errors.

---

# 3. Level 2 — Menu Synchronization

The next major capability is synchronization between the venue's POS and PingMe.

Instead of maintaining:

```text
POS menu
+
PingMe menu
```

the venue could maintain the authoritative product information in its existing system.

Conceptually:

```text
                ┌─────────────┐
                │    POS      │
                │             │
                │ Products    │
                │ Prices      │
                │ VAT         │
                │ Availability│
                └──────┬──────┘
                       │
                    API sync
                       │
                       ▼
                ┌─────────────┐
                │   PingMe    │
                │             │
                │ Digital     │
                │ Menu        │
                └─────────────┘
```

Examples:

- Restaurant changes Burger from €9.50 to €10.00 → PingMe updates.
- Restaurant marks Salmon as unavailable → PingMe disables it.
- New product is added → PingMe can import it.

This reduces duplicate data entry and simplifies onboarding.

---

# 4. Level 3 — Billing / Fiscal Integration

Longer term, PingMe could become part of the complete ordering-to-sale workflow.

Conceptually:

```text
Customer
   │
   │ order
   ▼
PingMe
   │
   ▼
POS / ERP
   │
   ├── Register sale
   ├── Apply VAT
   ├── Close table
   └── Issue fiscal document
```

This is substantially more complex than simple order integration because fiscal requirements, accounting, tax rules and system-specific workflows must be respected.

It should therefore be treated as a later integration stage rather than an MVP feature.

---

# 5. Portuguese POS / ERP Targets

Potential integration targets include:

1. Zone Soft / ZS rest
2. XD Software / XD Rest
3. WinRest
4. PRIMAVERA / Cegid
5. Vendus / Cegid
6. PHC
7. Sage
8. SAP

The initial targets should prioritize systems that are common in the hospitality market and expose practical integration mechanisms.

### Recommended first investigation

**Zone Soft**

Zone Soft is particularly interesting because its hospitality offering publicly describes integrations involving ordering, payment terminals, delivery platforms and other systems.

**Second wave:**

- XD Rest
- WinRest
- PRIMAVERA / Cegid

**Later:**

- SAP
- PHC
- Sage
- other enterprise-specific systems

SAP is technically possible through mechanisms such as SAP Business One Service Layer, but it is likely to involve substantially greater commercial, deployment and support complexity.

---

# 6. Integration Architecture

The PingMe backend should not contain POS-specific logic inside the Ordering module.

Instead, introduce an `Integrations` module and an abstraction such as:

```csharp
public interface IPosIntegration
{
    Task<IEnumerable<PosProduct>> GetProductsAsync();

    Task<IEnumerable<PosLocation>> GetLocationsAsync();

    Task<PosOrderResult> SendOrderAsync(Order order);

    Task<bool> IsAvailableAsync();
}
```

Then implement providers independently:

```text
Integrations
│
├── ZoneSoft
│   └── ZoneSoftPosIntegration
│
├── Primavera
│   └── PrimaveraPosIntegration
│
├── XD
│   └── XdPosIntegration
│
└── WinRest
    └── WinRestPosIntegration
```

The Ordering module only knows:

```text
IPosIntegration
```

It should not know how Zone Soft, PRIMAVERA or XD work internally.

---

# 7. Why This Fits the Modular Architecture

Current architecture:

```text
PingMe
│
├── Identity
├── Tenants
├── Locations
├── Catalog
├── Ordering
└── Integrations
```

Ordering:

```text
Ordering
    │
    ▼
IPosIntegration
    │
    ├── ZoneSoft
    ├── Primavera
    ├── XD
    └── WinRest
```

This preserves the modular boundary.

If a future integration becomes large enough to justify a separate service, the interface provides a natural extraction point.

For example:

```text
PingMe Platform
│
├── Identity
├── Tenants
├── Catalog
└── Locations

        │
        │ API / messaging
        ▼

POS Integration Service
│
├── ZoneSoft
├── Primavera
├── XD
└── WinRest
```

Do **not** build this as a separate microservice initially. Keep it inside the modular backend until there is a concrete reason to extract it.

---

# 8. Onboarding Experience

A strong future feature would be:

```text
Restaurant Admin
      ↓
Connect your POS
      ↓
Choose system
      │
      ├── Zone Soft
      ├── XD Rest
      ├── WinRest
      ├── PRIMAVERA
      └── Other
      ↓
Configure credentials / connection
      ↓
Import menu + locations
      ↓
Review
      ↓
Activate PingMe
```

The venue should ideally not have to recreate its entire menu manually.

This could become a major differentiator.

---

# 9. Payment Integration

Payment should remain separate from POS integration.

Potential future architecture:

```text
                    PingMe
                       │
          ┌────────────┼────────────┐
          ▼            ▼            ▼
        POS          Payments     Other
                    Providers
```

Potential payment providers can be investigated later, including:

- Stripe
- MB WAY / Portuguese payment ecosystem
- payment terminal providers
- POS-integrated payment systems

Do not implement online payment in the initial MVP.

Payment introduces additional concerns:

- payment intents
- webhooks
- idempotency
- refunds
- failed payments
- reconciliation
- PCI/security requirements
- fiscal integration

---

# 10. Recommended Product Roadmap

## V1 — Core Ordering

```text
QR
 ↓
Menu
 ↓
Cart
 ↓
PING!
 ↓
Order
 ↓
Staff receives order
```

## V1.5 — Venue Operations

```text
Admin
 ↓
Menu management
Locations
QR management
Staff dashboard
Realtime order status
```

## V2 — POS Integrations

Prioritize:

```text
Zone Soft
   ↓
XD Rest
   ↓
WinRest
   ↓
PRIMAVERA / Cegid
```

Then evaluate:

```text
PHC
Sage
SAP
```

## V3 — Payments

```text
PingMe
 ↓
Payment Provider
 ↓
Payment confirmation
 ↓
POS / ERP
```

## V4 — Full Operational Integration

```text
                    ┌── POS
                    │
Customer → PingMe ──┼── Payments
                    │
                    ├── Kitchen
                    │
                    ├── Inventory
                    │
                    └── Accounting
```

---

# 11. Business Value Proposition

The integration strategy changes the product proposition substantially.

Without integrations:

> "PingMe is a QR ordering system."

With integrations:

> **"PingMe connects your existing restaurant systems to a modern customer ordering experience."**

The venue keeps:

- existing POS
- existing fiscal workflow
- existing accounting
- existing payment infrastructure
- existing staff processes

PingMe adds:

- QR/NFC ordering
- mobile-first menu
- realtime ordering
- table/seat context
- digital customer experience
- reduced manual order entry
- future payment options

The strongest message is:

> **Don't replace your infrastructure. Plug PingMe into it.**

---

# 12. Important Technical Considerations

Before implementing a specific integration, investigate:

- Does the provider expose a public API?
- REST, SOAP, SDK, OData or another protocol?
- Authentication mechanism?
- Sandbox/test environment?
- Webhooks?
- Realtime synchronization?
- Product/menu APIs?
- Location/table APIs?
- Order creation APIs?
- Order status APIs?
- Price/VAT information?
- Fiscal document APIs?
- Rate limits?
- Offline behavior?
- Versioning?
- Commercial licensing?
- Partner certification requirements?
- Whether third-party integrations are officially supported?

Never assume two systems expose the same capabilities just because both advertise an "API."

Each provider needs its own technical investigation.

---

# 13. Design Principle

PingMe should own the **customer experience and ordering domain**.

The POS/ERP should remain authoritative for the capabilities that belong to it, especially:

- fiscalization
- accounting
- existing operational workflows
- potentially product/pricing master data

The integration layer translates between the two systems.

```text
Customer
   ↓
PingMe Domain
   ↓
Integration Adapter
   ↓
External POS / ERP
```

This separation is important for long-term maintainability.

---

# 14. Strategic Goal

The long-term opportunity is bigger than QR codes.

PingMe could become:

> **A hospitality integration and ordering platform that connects customers directly to the venue's existing operational systems.**

The QR code is simply the entry point.

The real value is the integration layer.
