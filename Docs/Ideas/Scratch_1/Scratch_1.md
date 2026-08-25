Yes. For this project, Wilson, I would absolutely build it as a real web application, not a native mobile app. The QR code opens a mobile-friendly React app, and the restaurant doesn't need the customer to install anything.

And given your background, I'd choose React + ASP.NET Core + PostgreSQL rather than introducing Go just for the sake of it.

The nice thing is that you can build the first version for essentially €0.

My recommended stack
                    ┌──────────────────────┐
                    │      Customer        │
                    │  Phone / Browser     │
                    └──────────┬───────────┘
                               │
                         QR / NFC link
                               │
                               ▼
                    ┌──────────────────────┐
                    │       React          │
                    │       Vite           │
                    │   Mobile Web App     │
                    └──────────┬───────────┘
                               │ HTTPS / REST
                               ▼
                    ┌──────────────────────┐
                    │    ASP.NET Core      │
                    │       .NET 10        │
                    │       Web API        │
                    └──────────┬───────────┘
                               │
                         EF Core / SQL
                               │
                               ▼
                    ┌──────────────────────┐
                    │     PostgreSQL       │
                    │      Supabase        │
                    └──────────────────────┘

And later:

              ┌───────────────┐
              │ Restaurant UI │
              │ Kitchen/Bar   │
              └───────┬───────┘
                      │
                      │ realtime
                      ▼
                 ┌─────────┐
                 │  PING!  │
                 └─────────┘
1. Frontend — React + TypeScript + Vite

I'd use:

React
TypeScript
Vite
React Router
TanStack Query
Tailwind CSS

React is a very good fit because the customer interface is essentially:

Menu → Product → Quantity → Cart → Order → Status

And you specifically want to learn/use React.

Also, don't use Create React App. React's current documentation explicitly says it has been deprecated.

Vite is excellent here because the React application can ultimately be compiled into static files and hosted extremely cheaply/free.

Example

Customer scans:

https://pingme.app/v/abc123

React reads:

venue = abc123

and loads:

Venue
 ├── Categories
 │    ├── Drinks
 │    ├── Food
 │    └── Desserts
 │
 ├── Products
 └── Cart

No login required for the customer initially.

2. Backend — I'd stay with .NET

This is where I wouldn't use Go.

Not because Go isn't good. It's excellent.

But you already know C#, .NET, Entity Framework, dependency injection, etc.

This project is a fantastic opportunity to deepen those skills rather than adding another backend ecosystem.

I'd use:

ASP.NET Core Web API + .NET 10

.NET 10 is currently LTS and is supported until November 2028.

That gives you:

ASP.NET Core
     │
     ├── Controllers / Minimal APIs
     ├── Dependency Injection
     ├── Authentication / Authorization
     ├── SignalR
     ├── Validation
     └── EF Core

And you get to practice proper modern .NET architecture.

3. Database — PostgreSQL

This is where I'd make a deliberate choice:

PostgreSQL.

Not SQL Server.

Not MongoDB.

Not SQLite for the real application.

Your domain is highly relational:

Venue
   │
   ├── Areas
   │     ├── Tables
   │     └── Seats
   │
   ├── Menu
   │     ├── Categories
   │     └── Products
   │
   └── Orders
          ├── OrderItems
          └── CustomerSession

Postgres is an excellent fit.

4. Supabase is interesting for the €0 requirement

Here's where it gets interesting.

You can use Supabase primarily as hosted PostgreSQL.

Its free tier currently includes:

PostgreSQL
500 MB database
1 GB storage
5 GB egress
50,000 MAU
500,000 Edge Function invocations
Realtime functionality
2 active free projects

The catch is that free projects can pause after a week of inactivity.

For a prototype, that's more than enough.

And importantly:

You don't have to use Supabase's whole ecosystem.

You could have:

React
   ↓
ASP.NET Core
   ↓
EF Core
   ↓
PostgreSQL
   ↓
Supabase

So you're still learning proper .NET + EF Core.

Supabase is basically giving you a free hosted database.

5. Realtime is actually important for this app

This is one of the interesting technical aspects of your idea.

Imagine:

Customer:

PING!
Order #1042

The bar/kitchen screen should immediately show:

🔔 NEW ORDER

Table 17

2x Burger
1x Coke
1x Beer

[ACCEPT]

Then:

ACCEPTED
    ↓
PREPARING
    ↓
READY
    ↓
DELIVERED

The customer could see:

Your order

🟢 Order received
🟢 Being prepared
🟡 Ready
⚪ Delivered

That's where I'd use SignalR on your .NET backend.

Customer React
       │
       │
       ▼
 ASP.NET Core
       │
    SignalR
       │
       ├──────────────► Restaurant Dashboard
       │
       └──────────────► Customer

This is actually a very nice project for learning SignalR.

6. Architecture

I wouldn't go crazy with microservices.

Absolutely not.

Start with a modular monolith.

Something like:

PingMe
│
├── PingMe.Api
│
├── PingMe.Application
│
├── PingMe.Domain
│
├── PingMe.Infrastructure
│
└── PingMe.Tests
Domain
Venue
Table
Seat
Menu
Category
Product
Order
OrderItem
CustomerSession
Application
CreateOrder
GetMenu
GetOrder
UpdateOrderStatus
GetVenue
...
Infrastructure
EF Core
PostgreSQL
Repositories
External services
API
Controllers
SignalR hubs
Authentication
Middleware

That's enough architecture for the first version.

7. GitHub

Absolutely.

I'd make:

github.com/yourname/pingme

And I'd probably start with:

main
│
└── develop
       │
       ├── feature/customer-menu
       ├── feature/cart
       ├── feature/orders
       ├── feature/restaurant-dashboard
       └── feature/authentication

And GitHub Actions for CI.

Something simple:

Push
 ↓
Build React
 ↓
Build .NET
 ↓
Run tests
 ↓
Deploy
8. The really cool part: you can develop the whole thing for free

Something approximately like:

Component	Technology	Cost
IDE	VS Code	€0
AI assistance	Claude/Copilot	already available to you
Frontend	React + Vite	€0
Backend	ASP.NET Core	€0
ORM	EF Core	€0
Database	PostgreSQL	€0
Hosted DB	Supabase Free	€0
Source control	GitHub	€0
CI/CD	GitHub Actions	€0 initially
Hosting	Free-tier static/server hosting	€0 initially
QR codes	Generate yourself	€0
Testing	xUnit	€0
API testing	Swagger / Bruno / Postman	€0

The first money I'd expect you to spend is probably domain + eventually production infrastructure, not development.

9. One architectural decision I'd make early

Don't design the application around:

"Restaurant table"

Design it around:

Location

Because your idea already extends beyond restaurants.

For example:

Venue
│
├── Area
│    │
│    ├── Location
│    │
│    ├── Location
│    │
│    └── Location
│
└── Menu

A location could be:

Restaurant
 └── Table 12

Bar
 └── Seat 43

Stadium
 └── Section B / Row 12 / Seat 18

Cinema
 └── Seat F14

Festival
 └── Zone 3 / Table 8

Then your QR doesn't mean:

"Table 12"

It means:

"This QR represents a location where an order can be delivered."

That's a much more powerful domain model.

10. And I would NOT implement payment initially

This is important.

For V1, I'd make:

Scan
 ↓
Browse
 ↓
Add to cart
 ↓
PING!
 ↓
Restaurant receives order
 ↓
Restaurant delivers
 ↓
Payment handled normally

Cash/card at the table/counter.

Then V2:

PING!
 ↓
Order
 ↓
Pay
 ↓
Delivered

And eventually:

Stripe
Apple Pay
Google Pay
Cards
...

Payment introduces a whole different level of complexity: payment intents, webhooks, refunds, idempotency, failed payments, PCI considerations, order/payment reconciliation, etc.

Don't let that derail the interesting part of the project.

What I'd personally build

If I were sitting beside you opening VS Code right now, I'd create:

PINGME
│
├── src
│   ├── PingMe.Api              (.NET 10)
│   ├── PingMe.Application
│   ├── PingMe.Domain
│   ├── PingMe.Infrastructure
│   │
│   └── pingme-web              (React + TypeScript + Vite)
│
├── tests
│   ├── PingMe.UnitTests
│   └── PingMe.IntegrationTests
│
├── docs
│
├── .github
│   └── workflows
│
├── docker-compose.yml
├── README.md
└── PingMe.sln

And I'd deliberately not use Docker, Kubernetes, Redis, RabbitMQ, microservices, etc. initially.

You don't need them.

Get this working first:

QR → React → menu → cart → API → PostgreSQL → order → restaurant screen → SignalR → customer sees status.

Once that works, you've got a real PingMe MVP, not just a demo.

And honestly, Wilson, this is a very good project for you specifically because it combines almost everything you've been wanting to work with: React/TypeScript, modern .NET, EF Core, DI, APIs, realtime communication, authentication, Git/CI/CD, cloud deployment, and eventually payments.

this would need a client page and an admin page.. so a restaurant would need to set up theire menu and prices..  
i'm not sure about the monolith architecture.. was never a fan

Exactly. Once you introduce the restaurant/admin side, this stops being just a QR ordering app and becomes a small multi-tenant SaaS platform.

And I agree with your instinct on the monolith. I wouldn't force a traditional monolith if you already know you dislike that architecture. But I also wouldn't jump straight into microservices.

I'd go for a modular architecture with clear boundaries, deployed initially as one application. That gives you separation without the operational nightmare of microservices.

I'd structure PingMe like this
                         ┌──────────────────┐
                         │    PingMe API    │
                         │   ASP.NET Core   │
                         └────────┬─────────┘
                                  │
              ┌───────────────────┼───────────────────┐
              │                   │                   │
              ▼                   ▼                   ▼
       ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
       │  Ordering   │     │   Catalog   │     │   Identity  │
       │             │     │             │     │             │
       │ Orders      │     │ Menus       │     │ Users       │
       │ Cart        │     │ Categories  │     │ Roles       │
       │ Status      │     │ Products    │     │ Tenants     │
       └─────────────┘     └─────────────┘     └─────────────┘
              │                   │                   │
              └───────────────────┼───────────────────┘
                                  ▼
                         ┌─────────────────┐
                         │   PostgreSQL    │
                         └─────────────────┘

The important thing is that these are modules, not necessarily separate deployable services.

Two actual applications

I'd have two React frontends.

Customer
pingme.app/v/{venueCode}

Extremely simple:

Menu
 ↓
Category
 ↓
Product
 ↓
Quantity
 ↓
Cart
 ↓
PING!

No account.

No app download.

No registration.

Restaurant/Admin

Something like:

admin.pingme.app

Restaurant logs in:

Dashboard
│
├── Orders
│
├── Menu
│   ├── Categories
│   ├── Products
│   └── Prices
│
├── Locations
│   ├── Tables
│   ├── Seats
│   └── QR Codes
│
├── Restaurant
│   ├── Name
│   ├── Logo
│   └── Opening hours
│
└── Settings

And later:

Analytics
Staff
Payments
Promotions
Integrations
Multi-tenancy becomes important

This is probably the biggest architectural consideration.

Imagine:

PingMe
│
├── Restaurant A
│    ├── Menu
│    ├── Tables
│    └── Orders
│
├── Restaurant B
│    ├── Menu
│    ├── Tables
│    └── Orders
│
└── Stadium C
     ├── Sections
     ├── Seats
     └── Orders

Every entity needs a relationship back to the Tenant/Venue.

For example:

Tenant
   │
   ├── Users
   ├── Menu
   ├── Locations
   └── Orders

Then:

Order
 └── TenantId
Product
 └── TenantId

etc.

That gives you tenant isolation from day one.

I'd actually go one step further

Instead of thinking:

Restaurant

I'd make the core entity:

Tenant

Then:

Tenant
   │
   ├── Venue
   │
   ├── Users
   │
   ├── Menu
   │
   ├── Locations
   │
   └── Orders

Because eventually you might have:

McDonald's franchise

Tenant: McDonald's Madeira
    │
    ├── Venue: Funchal
    ├── Venue: Câmara de Lobos
    └── Venue: Machico

Or:

Stadium

Tenant: Stadium X
    │
    ├── North Stand
    ├── South Stand
    ├── VIP
    └── Hospitality

Same platform.

What I'd do instead of "monolith"

I'd call it:

Modular monolith / modular backend

But with a very important distinction:

The modules don't know much about each other.

Something like:

PingMe.Backend
│
├── Modules
│
│   ├── Identity
│   │   ├── Domain
│   │   ├── Application
│   │   └── Infrastructure
│   │
│   ├── Tenants
│   │   ├── Domain
│   │   ├── Application
│   │   └── Infrastructure
│   │
│   ├── Catalog
│   │   ├── Domain
│   │   ├── Application
│   │   └── Infrastructure
│   │
│   ├── Locations
│   │   ├── Domain
│   │   ├── Application
│   │   └── Infrastructure
│   │
│   └── Ordering
│       ├── Domain
│       ├── Application
│       └── Infrastructure
│
└── Shared

Then later, if Ordering becomes huge:

                    PingMe
                       │
        ┌──────────────┴──────────────┐
        │                             │
   PingMe Platform              Ordering Service
        │                             │
 Catalog / Identity / etc.        Orders

You can extract it.

That's one of the major advantages of modular architecture.

React structure

I'd actually make one React codebase with two applications initially.

pingme-web/
│
├── apps/
│   ├── customer/
│   │
│   └── admin/
│
├── packages/
│   ├── ui/
│   ├── api-client/
│   └── types/

So you can share:

UI components
TypeScript models
API client
validation
utilities

But keep customer/admin experiences completely separate.

This is a good place for a monorepo.

I'd use pnpm workspaces.

Database

I'd probably start roughly like this:

Tenant
Venue
User
Role
Menu
Category
Product
ProductOption
Location
QrCode
Order
OrderItem
OrderStatus

Later:

Payment
Promotion
Discount
Tax
Printer
Integration
Notification

And importantly, Order should contain a snapshot of what was ordered.

For example:

OrderItem

ProductId = 123
ProductName = "Cheeseburger"
UnitPrice = 9.50
Quantity = 2

Don't rely on the current Product record to reconstruct an old order.

If the restaurant changes:

Cheeseburger €9.50 → €10.50

yesterday's order should still say:

2 × Cheeseburger @ €9.50

One thing I'd change from my previous proposal

I'd actually not make Supabase central to the architecture.

I'd use:

React
   ↓
ASP.NET Core
   ↓
EF Core
   ↓
PostgreSQL

And then choose wherever we can get the cheapest/free PostgreSQL hosting.

Supabase can still be the database provider, but I wouldn't build the application around Supabase APIs.

That keeps the architecture portable.

And there's another interesting piece: QR management

The admin needs to be able to say:

Venue
  └── Main Hall
       ├── Table 1
       ├── Table 2
       ├── Table 3
       └── Table 4

Then:

Generate QR codes

Table 1
[ QR ]

Table 2
[ QR ]

Table 3
[ QR ]

The QR might encode:

https://pingme.app/v/7f82a/table/12

But I'd probably actually use a random opaque identifier, not expose database IDs:

https://pingme.app/p/8Kx92LmQ

The backend resolves:

8Kx92LmQ
      ↓
Tenant
      ↓
Venue
      ↓
Location

That becomes the customer's context.

So the architecture I'd choose now
                    ┌──────────────────────┐
                    │      CUSTOMER        │
                    │   React + TypeScript │
                    └──────────┬───────────┘
                               │
                               │ REST / SignalR
                               ▼
┌─────────────────────────────────────────────────────────┐
│                    ASP.NET CORE                          │
│                                                         │
│  Identity │ Tenants │ Catalog │ Locations │ Ordering   │
│                                                         │
└──────────────────────────┬──────────────────────────────┘
                           │
                         EF Core
                           │
                           ▼
                    ┌───────────────┐
                    │  PostgreSQL   │
                    └───────────────┘
                           ▲
                           │
                    ┌──────┴───────┐
                    │              │
              ┌─────┴─────┐  ┌────┴─────┐
              │   ADMIN   │  │  STAFF   │
              │   React   │  │  React   │
              └───────────┘  └──────────┘

GitHub + GitHub Actions + VS Code + Claude + React/TS + .NET 10 + EF Core + PostgreSQL + SignalR.

That would be my stack.

And importantly, it's not a toy architecture. If PingMe actually takes off, you can evolve this into separate services later without throwing the whole thing away.

The next thing I'd design before writing code is the domain model + user journeys. That's where this gets interesting, because there are actually three very different actors: customer, restaurant staff, and restaurant administrator.

create a doc with that so i can bring to my workspace on VS Code

Data analysis isn't available right now. Do you want to continue without it? 

Data analysis will be available again when usage resets after 2:34 PM. Upgrade now for expanded access and advanced intelligence.
Try Plus free

from docx import Document
from docx.shared import Pt
from docx.enum.text import WD_ALIGN_PARAGRAPH

path = "/mnt/data/PingMe_Technical_Architecture.docx"

doc = Document()
styles = doc.styles
styles["Normal"].font.name = "Aptos"
styles["Normal"].font.size = Pt(10.5)

title = doc.add_heading("PingMe — Technical Architecture & MVP Plan", 0)
title.alignment = WD_ALIGN_PARAGRAPH.CENTER
p = doc.add_paragraph("Working technical specification for development in VS Code")
p.alignment = WD_ALIGN_PARAGRAPH.CENTER

doc.add_heading("1. Product Overview", level=1)
doc.add_paragraph(
    "PingMe is a QR/NFC-driven ordering platform for restaurants, bars, stadiums, "
    "festivals, cinemas and other venues. A customer scans a code associated with a "
    "physical location (table, seat, section, etc.), browses the venue menu, selects "
    "quantities, and places an order without waiting for staff. Staff receive the "
    "order and deliver it to the associated location."
)

doc.add_heading("2. MVP Scope", level=1)
for x in [
    "Customer mobile web application — no app installation required.",
    "Restaurant/venue administration application.",
    "Menu/category/product management, including prices and availability.",
    "Venue, area and table/seat/location management.",
    "QR code generation and management.",
    "Customer cart and order placement.",
    "Staff order dashboard.",
    "Real-time order status updates.",
    "Order lifecycle: Received → Accepted → Preparing → Ready → Delivered.",
    "Multi-tenant architecture so multiple venues can use the same platform.",
    "Payment is intentionally deferred from the first MVP; payment can initially happen normally at the venue."
]:
    doc.add_paragraph(x, style="List Bullet")

doc.add_heading("3. Recommended Technology Stack", level=1)
table = doc.add_table(rows=1, cols=3)
table.style = "Table Grid"
for i, h in enumerate(["Area", "Technology", "Reason"]):
    table.rows[0].cells[i].text = h
rows = [
    ("Customer frontend", "React + TypeScript + Vite", "Fast mobile web UI; strong ecosystem; good fit for the project."),
    ("Admin frontend", "React + TypeScript + Vite", "Shared frontend technology and reusable components."),
    ("Backend", "ASP.NET Core / .NET 10", "Leverages existing C#/.NET knowledge and provides strong APIs, DI and SignalR support."),
    ("ORM", "Entity Framework Core", "Relational domain and strong .NET integration."),
    ("Database", "PostgreSQL", "Excellent relational database for tenants, menus, locations and orders."),
    ("Realtime", "ASP.NET Core SignalR", "Push order/status changes to staff and customers."),
    ("Source control", "Git + GitHub", "Repository, branching, PRs and CI/CD."),
    ("CI/CD", "GitHub Actions", "Free-tier automation for build/test/deployment."),
    ("Development", "VS Code + Claude", "Primary development environment and AI assistance."),
    ("Initial hosting", "Free-tier services", "Keep prototype/MVP infrastructure at €0 where practical.")
]
for r in rows:
    cells = table.add_row().cells
    for i, v in enumerate(r):
        cells[i].text = v

doc.add_heading("4. Architecture Direction", level=1)
doc.add_paragraph(
    "Use a modular backend rather than a traditional tightly coupled monolith. "
    "The initial system can be deployed as one ASP.NET Core application, while internal "
    "modules have clear boundaries. This avoids the operational complexity of microservices "
    "while keeping the codebase structured so individual modules can later be extracted."
)

doc.add_paragraph("Initial backend modules:")
for x in ["Identity", "Tenants", "Catalog", "Locations", "Ordering"]:
    doc.add_paragraph(x, style="List Bullet")

doc.add_heading("5. High-Level Architecture", level=1)
doc.add_paragraph(
    "Customer React app → ASP.NET Core API → EF Core → PostgreSQL\n"
    "Admin React app → ASP.NET Core API → EF Core → PostgreSQL\n"
    "Staff dashboard ↔ ASP.NET Core SignalR ↔ Ordering module"
)

doc.add_heading("6. Frontend Applications", level=1)
doc.add_heading("Customer Application", level=2)
for x in [
    "Open from QR/NFC link.",
    "Resolve venue and physical location from an opaque public identifier.",
    "Browse categories and products.",
    "Select quantities/options.",
    "View cart.",
    "Place order.",
    "Track order status in real time.",
    "No customer account required for MVP."
]:
    doc.add_paragraph(x, style="List Bullet")

doc.add_heading("Admin Application", level=2)
for x in [
    "Authentication and role-based access.",
    "Venue configuration.",
    "Menu management.",
    "Category management.",
    "Product management.",
    "Price management.",
    "Product availability.",
    "Location/table/seat management.",
    "QR code generation.",
    "Order monitoring.",
    "Restaurant/staff settings."
]:
    doc.add_paragraph(x, style="List Bullet")

doc.add_heading("7. Multi-Tenancy", level=1)
doc.add_paragraph(
    "The core business entity should be Tenant rather than Restaurant. This keeps the "
    "platform suitable for restaurants, bars, stadiums, cinemas, festivals and future "
    "multi-location businesses."
)
doc.add_paragraph("Conceptual hierarchy:")
doc.add_paragraph(
    "Tenant → Venue → Area → Location\n"
    "Tenant → Users\n"
    "Tenant → Menu → Categories → Products\n"
    "Tenant → Orders"
)

doc.add_paragraph(
    "Every tenant-owned entity should have a clear ownership relationship to prevent "
    "cross-tenant data access."
)

doc.add_heading("8. Core Domain Model", level=1)
for x in [
    "Tenant",
    "Venue",
    "User",
    "Role",
    "Menu",
    "Category",
    "Product",
    "ProductOption",
    "Location",
    "QrCode",
    "Order",
    "OrderItem",
    "OrderStatus"
]:
    doc.add_paragraph(x, style="List Bullet")

doc.add_heading("9. Location Model", level=1)
doc.add_paragraph(
    "Do not model the system exclusively around restaurant tables. A Location represents "
    "where an order should be delivered."
)
for x in [
    "Restaurant → Table 12",
    "Bar → Seat 43",
    "Stadium → Section B / Row 12 / Seat 18",
    "Cinema → Seat F14",
    "Festival → Zone 3 / Table 8"
]:
    doc.add_paragraph(x, style="List Bullet")

doc.add_heading("10. QR/NFC Identification", level=1)
doc.add_paragraph(
    "Avoid exposing sequential database IDs in public URLs. Use an opaque/random public "
    "identifier that resolves to Tenant → Venue → Location."
)
doc.add_paragraph("Example:")
doc.add_paragraph("https://pingme.app/p/8Kx92LmQ")

doc.add_heading("11. Order Model", level=1)
doc.add_paragraph(
    "OrderItem should store a snapshot of the product name and price at the time of ordering. "
    "Historical orders must not change when the venue subsequently edits a product or price."
)
doc.add_paragraph("Example:")
doc.add_paragraph("ProductId = 123\nProductName = Cheeseburger\nUnitPrice = 9.50\nQuantity = 2")

doc.add_heading("12. Realtime Ordering", level=1)
doc.add_paragraph("Expected flow:")
for x in [
    "Customer places order.",
    "API persists the order.",
    "SignalR notifies the staff dashboard.",
    "Staff accepts the order.",
    "Customer receives status update.",
    "Staff changes status as preparation progresses.",
    "Customer receives final Ready/Delivered updates."
]:
    doc.add_paragraph(x, style="List Number")

doc.add_heading("13. Order State Machine", level=1)
doc.add_paragraph("Received → Accepted → Preparing → Ready → Delivered")
doc.add_paragraph(
    "Future states may include Cancelled, Rejected, PaymentPending, PaymentFailed and Refunded."
)

doc.add_heading("14. Suggested Repository Structure", level=1)
repo = """PingMe/
├── src/
│   ├── PingMe.Api/
│   ├── PingMe.Application/
│   ├── PingMe.Domain/
│   ├── PingMe.Infrastructure/
│   └── pingme-web/
│       ├── apps/
│       │   ├── customer/
│       │   └── admin/
│       └── packages/
│           ├── ui/
│           ├── api-client/
│           └── types/
├── tests/
│   ├── PingMe.UnitTests/
│   └── PingMe.IntegrationTests/
├── docs/
├── .github/
│   └── workflows/
├── docker-compose.yml
├── README.md
└── PingMe.sln"""
doc.add_paragraph(repo)

doc.add_heading("15. Git Strategy", level=1)
doc.add_paragraph("Suggested branches:")
for x in [
    "main — stable/releasable code.",
    "develop — integration branch.",
    "feature/customer-menu",
    "feature/cart",
    "feature/orders",
    "feature/admin-menu",
    "feature/restaurant-dashboard",
    "feature/authentication"
]:
    doc.add_paragraph(x, style="List Bullet")

doc.add_heading("16. Development Principles", level=1)
for x in [
    "Start as a modular application, not a distributed microservice system.",
    "Keep domain/business logic independent from HTTP and persistence concerns.",
    "Use dependency injection throughout the backend.",
    "Use DTOs between API and frontend rather than exposing EF entities directly.",
    "Keep tenant isolation explicit.",
    "Use asynchronous APIs where appropriate.",
    "Add automated tests around domain and application logic.",
    "Keep external integrations behind interfaces/adapters.",
    "Avoid premature infrastructure such as Kubernetes, message brokers or Redis."
]:
    doc.add_paragraph(x, style="List Bullet")

doc.add_heading("17. Payment Strategy", level=1)
doc.add_paragraph(
    "Do not implement online payment in the first MVP. Initially, the platform handles "
    "ordering and delivery while payment happens through the venue's existing process. "
    "Payment can later be introduced using a provider such as Stripe, with explicit handling "
    "for payment intents, webhooks, idempotency, refunds and reconciliation."
)

doc.add_heading("18. Initial Cost Strategy", level=1)
doc.add_paragraph(
    "Target a €0 development/prototype environment. Use VS Code, GitHub, React, .NET, EF Core, "
    "PostgreSQL and free-tier hosting/services. Supabase can be used as a hosted PostgreSQL "
    "provider, but the application should communicate through the ASP.NET Core backend rather "
    "than becoming dependent on Supabase-specific APIs."
)

doc.add_heading("19. MVP Development Sequence", level=1)
for x in [
    "Create GitHub repository and solution structure.",
    "Define domain model and tenant boundaries.",
    "Create PostgreSQL schema and EF Core migrations.",
    "Implement Tenant/Venue/Location management.",
    "Implement Catalog/Menu management.",
    "Build customer React menu.",
    "Build cart and order creation.",
    "Build staff order dashboard.",
    "Add SignalR realtime notifications.",
    "Build admin menu/location/QR management.",
    "Add authentication and authorization.",
    "Add automated unit/integration tests.",
    "Set up GitHub Actions.",
    "Deploy a free-tier MVP.",
    "Test with one real restaurant/bar scenario."
]:
    doc.add_paragraph(x, style="List Number")

doc.add_heading("20. Future Evolution", level=1)
doc.add_paragraph(
    "If individual modules eventually require independent scaling or deployment, extract them "
    "into services. Ordering is the most likely candidate because realtime ordering can become "
    "a high-traffic component. The modular boundaries should make this evolution possible without "
    "starting with microservice complexity."
)

doc.add_heading("21. Working Product Principle", level=1)
doc.add_paragraph(
    "The first meaningful milestone is not payment, analytics or integrations. It is:"
)
doc.add_paragraph(
    "QR → React menu → Cart → PING! → Order stored → Staff receives realtime notification → "
    "Order status updates → Customer sees delivery status."
)

doc.save(path)
print(path)