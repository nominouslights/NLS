# Northern Link Shuttle & Cargo — Platform Architecture Document

**Version:** 1.0
**Date:** July 2026
**Owner:** Emelio Campbell
**Status:** Draft for review — foundation document for all five client applications

## Table of Contents

1. Purpose & Scope
2. System Overview
3. Tenant Model
4. Identity & Access (Unified Authentication)
   - 4.1 Approach — Decided
   - 4.2 Token Model
   - 4.3 Roles by Tenant
5. API Domain Boundaries
   - 5.1 Crew & Workforce Visibility (Client Tenant Expansion)
   - 5.2 Tracking Crew Without Phones
   - 5.3 Budget & Financial Control (Zero-Based Budgeting)
   - 5.4 Driver Self-Service Trip Selection & Eligibility Engine
6. Client Applications — API Consumption Map
   - 6.1 Owner/Exec Desktop App — Access Model
7. Data Architecture
8. Offline Sync Strategy (Driver Field App)
9. Integrations
10. Security & Compliance
11. Environments & Deployment
    - 11.1 Monitoring & Observability — Recommendation
12. Technology Stack Summary
13. Open Decisions Flagged for Your Input
14. Next Steps

---

---

## 1. Purpose & Scope

This document defines the shared architecture underneath the Northern Link platform: one API,
one identity system, one data model, serving five distinct client applications. It exists so that
every subsequent app-specific technical spec, UXPilot prompt, and Claude Code build instruction
inherits the same rules instead of re-deciding them.

This is a **consolidation**, not a fresh design — it locks in decisions already made in prior
planning (tech stack, hosting, compliance posture) and extends them to cover the new pieces:
multi-tenant auth spanning Admin, Client, and Vendor/Partner, and the driver app's repositioning
from an admin tool to a field app.

---

## 2. System Overview

```
                         ┌─────────────────────────────┐
                         │     Northern Link API        │
                         │   (.NET 10 · CQRS/DDD)       │
                         │  Domain libraries, one DB     │
                         └───────────────┬───────────────┘
                                         │
        ┌───────────────┬───────────────┼───────────────┬───────────────┐
        │               │               │               │               │
 ┌────────────┐ ┌───────────────┐ ┌───────────┐ ┌───────────────┐ ┌───────────────┐
 │ Community  │ │ Client Web App │ │  Driver   │ │  Admin Web App │ │ Owner/Exec    │
 │ Mobile App │ │   (Alamos)     │ │ Field App │ │ (Dispatch/Ops) │ │ Desktop App   │
 │ iOS/Android│ │   Next.js 15   │ │ (company- │ │  Next.js 15    │ │ (Emelio, Acct)│
 │ (personal  │ │                │ │  issued   │ │                │ │               │
 │  device)   │ │                │ │  tablet   │ │                │ │               │
 │            │ │                │ │  only)    │ │                │ │               │
 └────────────┘ └───────────────┘ └───────────┘ └───────────────┘ └───────────────┘
   Consumers        Client tenant    Offline-first   Admin tenant     Admin tenant
```

One backend, five front doors. No client app talks to the database directly — every app is an
API consumer. This was already validated in prior planning given offline sync needs, third-party
credential centralization (QuickBooks, Stripe, Twilio), and the sensitivity of Alamos contract data.

---

## 3. Tenant Model

The platform has **three tenant categories** plus one non-tenant identity pool:

| Tenant type | Examples | Notes |
|---|---|---|
| **Internal (Admin)** | Northern Link itself | Owner, dispatchers, supervisors, accountant, future board members |
| **Client** | Alamos Gold; future mining clients (Vale, Hudbay) | Sees only their own contracted service data |
| **Vendor / Partner** | Miller the Mover (Thompson); future independent operators (e.g., South Indian Lake feeder route) | Supplies drivers/facility access or runs a feeder route under a partner agreement; scoped to their own operational slice |
| **Consumer (non-tenant)** | Community passengers, NIHB clients, grocery/parcel customers | Individual accounts, not organizations — booking-scoped access only, no "tenant" concept needed |

**Sub-tenant visibility (important nuance from the Alamos portal design work):** Alamos's portal
holds data belonging to Alamos *and* to Alamos's own contractors, but access is restricted to
Alamos only — contractors don't get logins. This means the Client tenant model needs a
**scoped-visibility flag** on records (e.g., a manifest tagged "Alamos contractor: Sigfusson") that
Alamos admins can see but that never becomes a separate tenant of its own. Keep this simple: it's
a data attribute, not a new tenant type.

**Vendor/Partner tenants are new** — nothing before this used a tenant model for Miller the Mover
or future independent operators. A vendor tenant will typically have its own drivers and vehicles,
but those drivers still use the **Driver Field App** against Northern Link's trip/manifest domain
when running Northern Link-branded service. Partner compliance data (Class 4 licence, MPI
Passenger Vehicle for Hire insurance) is tracked per-partner, not per-tenant-globally.

---

## 4. Identity & Access (Unified Authentication)

### 4.1 Approach — Decided

Self-hosted OpenID Connect (OIDC) identity server as a domain inside the .NET 10 API (via
OpenIddict or ASP.NET Core Identity + IdentityServer-equivalent), running on the same
OVHcloud Canada infrastructure as everything else. **Confirmed: self-host for now**, consistent
with the platform's non-negotiable Canadian data residency posture. Revisit only if operational
burden becomes a real problem at scale.

### 4.2 Token Model

- **OIDC / OAuth2**, JWT access tokens + refresh tokens
- Access token claims: `sub` (user id), `tenant_id`, `tenant_type` (admin/client/vendor/consumer),
  `role`, `partner_id` (if applicable)
- Short-lived access tokens (15 min) for web/desktop; longer-lived refresh tokens with silent
  renewal for the **Driver Field App**, since it must function through cellular gaps between
  Thompson and Lynn Lake — token refresh happens opportunistically whenever signal returns, and a
  locally cached valid session lets the driver keep working offline in between.
- MFA required for: Owner, Client Admin roles, Accountant. Optional (recommended) for
  Dispatcher/Supervisor.

### 4.3 Roles by Tenant

| Tenant | Roles |
|---|---|
| Internal (Admin) | Owner, Dispatcher, Supervisor/Manager, Accountant, Board Member (future) |
| Client | Client Admin, Client Viewer, **Logistics Coordinator (new — see 5.1, logs crew movements + roster)** |
| Vendor/Partner | Partner Admin, Partner Driver |
| Internal (Admin) | Driver *(Northern Link's own drivers — distinct from Partner Driver)* |
| Consumer | Community Passenger *(single role, no tiers)* |
| *Future/optional* | Worker Self-Check-In user — see 5.1, sixth app, not in initial scope |

Authorization is enforced two ways, deliberately redundant:
1. **API-level** — every command/query handler checks role + tenant scope before executing.
2. **Database-level** — PostgreSQL Row-Level Security (RLS) policies keyed on `tenant_id`, as a
   defense-in-depth backstop if an API bug ever lets a query through unscoped.

---

## 5. API Domain Boundaries

Built as a **single deployable API composed of one class library per domain** (not microservices)
to start — same CQRS/DDD approach already scoped for the TMS. Each domain library contains its
own `Domain/`, `Application/`, and `Infrastructure/` layers, references only `NorthernLink.Shared`,
and communicates with other domains exclusively through RabbitMQ integration events. That makes
each library extractable into its own microservice later (copy the library + Shared out) if scale
demands it, without paying microservices operational overhead now.

**Domain modeling convention: value objects are `sealed record`.** No `ValueObject` base class —
C# records already give structural equality (component-wise `Equals`/`GetHashCode`) for free.
Standard shape: private constructor, static `Create(...)` factory returning `Result<T>` for
validation failures. Example: `Backend/src/Fleet/Domain/Vehicles/Vin.cs`.

| Domain | Responsibility |
|---|---|
| **Identity & Access** | Auth, users, roles, tenants, sessions |
| **Client & Contract Management** | Alamos and future client profiles, contracts, PO tracking, billing terms |
| **Vendor/Partner Management** | Miller the Mover, independent operators, partner compliance (licence, insurance) |
| **Fleet & Vehicle Management** | Vehicles, capacity, maintenance records (NSC Standard 13) |
| **Driver & Compliance** | Driver profiles, licensing, DVIR (NSC Standard 11), Hours of Service (CVDHS Regulations) |
| **Trip & Manifest Management** | Trip creation, passenger manifests, cargo manifests, dispatch, PDF generation |
| **Incident & Fault Reporting** | Post-trip issues, accident reports, contractor clearance workflow |
| **Community Booking & Payments** | Demand-triggered shuttle booking, Gift-a-Seat, seat inventory, Stripe payments |
| **NIHB Medical Transport** | Voucher-based bookings, Tier 1/2/3 pricing, no-minimum override |
| **Grocery & Parcel Services** | Weekly grocery run, parcel service, pickup hub logistics |
| **Billing & Accounting Integration** | QuickBooks Online sync, invoicing, payment reconciliation, client-facing accrual statement export |
| **Notifications** | SendGrid (email), Twilio (SMS), push notifications |
| **Reporting & Analytics** | Cross-domain dashboards for Admin Web App and Owner Desktop App |
| **Crew & Workforce Visibility** | Client-requested: real-time location/status of client crew across ALL transport modes and carriers, not just Northern Link service. See 5.1. |
| **Budget & Financial Control (Zero-Based Budgeting)** | Every cost and revenue transaction tagged to a Budget Code; codes re-justified from zero each period rather than carried forward. See 5.3. |

### 5.1 Crew & Workforce Visibility (New — Client Tenant Expansion)

This is a distinct category from everything else in the Client tenant scope: the rest of the
Client Web App shows Alamos data *about Northern Link's own service* (manifests, invoices,
cargo). Crew & Workforce Visibility instead answers a safety/accountability question —
**"where is every crew member right now?"** — regardless of who is moving them. This is standard
practice in mine-site logistics (muster/roll-call accountability), and it means Northern Link
becomes an aggregation point for movement data it doesn't fully control.

**Location states (crew member status):**
- At Camp
- At Mine
- In Thompson
- In Transit — with two required sub-attributes:
  - **Mode**: Air / Ground – Short Haul / Ground – Long Haul
  - **Carrier**: Northern Link, or a named external carrier (charter airline, other ground operator)

**Data model additions:**
- `CrewMember` — a person entity, likely keyed to an Alamos employee/contractor ID, distinct from
  (but linkable to) a `Passenger` record on a Northern Link manifest
- `CrewStatus` — current location + mode + carrier + timestamp + source (system-generated vs.
  manually entered) — this is the "last known" record
- `CrewMovementEvent` — append-only history log every time status changes, for audit and for
  after-the-fact incident review

**Data sources — resolved:**
1. **Automatic**: when a crew member is on a Northern Link trip/manifest, their status updates
   automatically from the Trip & Manifest domain (e.g., manifest marked complete → status flips
   from "In Transit (Ground, Northern Link)" to "At Mine").
2. **Manual entry for other carriers**: logged by **Alamos Logistics Coordinators** (Client tenant
   role — see 4.3), not by Northern Link staff. Northern Link is providing the system of record,
   not doing the data entry for movements it didn't operate.
3. **Roster maintenance**: a joint responsibility — both Northern Link dispatchers and Alamos
   dispatchers can maintain the crew roster. Both need write access to the `CrewMember` entity,
   scoped to Alamos's tenant, with the audit trail (`CrewMovementEvent` / roster edit history)
   showing which organization made each change.
4. **Preferred long-term direction — system integration, not manual entry**: the stated strategy
   is to *propose an approach first, then integrate with whatever systems Alamos already runs*
   (their own crew roster, mine-site check-in/muster software, camp management systems) rather
   than asking their logistics coordinators to re-key data Northern Link doesn't control. This
   should be scoped as a phased plan:
   - **Phase 1 (launch)**: manual entry by Alamos Logistics Coordinators + automatic sync from
     Northern Link's own trip data. Fast to ship, proves the concept.
   - **Phase 2 (integration)**: work with Alamos to identify their existing systems (roster HR
     feed, camp check-in, flight manifests) and build import/sync connectors — this is a
     discovery conversation with Alamos, not a Northern Link-only technical decision, so it
     belongs in the Alamos relationship/proposal track as much as the technical roadmap.
5. **Optional future addition — Worker Self-Check-In App**: a lightweight, optional mobile app
   for individual crew members to self-report their own status (e.g., "boarding flight now,"
   "arrived at camp"). This would be a **sixth client application**, distinct from the five core
   apps in Section 2 — flagged here as a *future/optional* addition, not part of the initial
   five-app scope, since it depends on Alamos wanting worker-level adoption and raises its own
   consent/privacy questions (a worker's employer seeing their self-reported location). Worth
   revisiting once Phase 1/2 above are proven out.

**Because this is safety-critical (muster accountability), the design needs to be honest about
data staleness.** Confirmed requirement: always show **last confirmed location, date, and time**
as a single unambiguous fact — never a vague "recently" or an implied-current status.

**Recommendations to make this more versatile:**

- **Traffic-light freshness indicator**: pair the raw timestamp with a colour cue (green =
  confirmed recently, amber = aging, red = overdue) — reuses the amber/blue/status-colour
  conventions already established across the platform's design system, so it reads instantly to
  someone scanning a full crew list.
- **Context-aware thresholds, not one global number**: "stale" should mean something different
  depending on state. A crew member marked "At Camp" for 18 hours overnight is normal; a crew
  member marked "In Transit" for 18 hours is a problem. Recommend configurable thresholds per
  location-state (e.g., In Transit: flag after expected travel time + buffer; At Camp/At Mine:
  flag only after a much longer window, e.g., 24–48h with no check-in at all).
- **Expected-vs-actual comparison**: where a trip or flight has a scheduled arrival time, compare
  against it proactively — flag "overdue" the moment a crew member misses their expected
  check-in, rather than waiting for a generic staleness clock to run out. This turns the feature
  from passive record-keeping into an early-warning tool.
- **One-tap "confirm current location"**: let a coordinator or driver bump the timestamp without
  necessarily logging a full move — useful when someone is confirmed present but hasn't
  physically transitioned anywhere.
- **Escalation, not just display**: once a status crosses the stale threshold, this should be able
  to trigger a notification (via the existing Notifications domain — SMS/email) to the relevant
  Alamos Logistics Coordinator and/or Northern Link dispatcher, rather than relying on someone to
  notice a red indicator on a dashboard they might not have open.
- **Always show source**: "system-confirmed" (from a Northern Link trip/manifest) reads as more
  reliable than "manually entered" — showing which applies helps a coordinator judge trust, not
  just recency.

### 5.2 Tracking Crew Without Phones

Mine-site no-phone policies are common (safety, security, focus), and they push the design toward
something better than an app for this population anyway: **infrastructure-based check-ins rather
than device-based self-reporting.** A few approaches, roughly in order of how much new hardware
they require:

1. **Badge scan integrated into the Driver Field App (lowest new hardware — recommended first)**:
   crew already carry an ID/access badge for mine-site security. Add barcode/QR or NFC scanning to
   the Driver Field App's passenger manifest screen — the driver scans each badge as crew board the
   shuttle, which both builds the manifest *and* automatically fires the "In Transit" status update
   with zero extra data entry. This directly strengthens the existing Trip & Manifest domain rather
   than adding a separate system.
2. **Checkpoint kiosks at fixed transition points** (camp entrance, mine gate, Thompson depot):
   a simple tablet + USB/Bluetooth badge reader where crew tap in/out. Feeds the same `CrewStatus`
   record. This is the most direct answer to "at camp vs. at mine" — those are exactly the moments
   a checkpoint tap would fire.
3. **Vehicle-and-manifest inference**: while someone is aboard a Northern Link vehicle, their
   location is inherently "wherever the vehicle is" — if the fleet has (or gets) GPS/telematics,
   crew status can inherit live vehicle position during transit rather than only updating at
   trip start/end.
4. **Flight/charter manifest capture**: charter operators already scan boarding passes or take a
   paper manifest; the Phase 2 integration work (5.1) should treat this as a prime target — even a
   photo-of-manifest upload by an Alamos Logistics Coordinator is a big step up from fully manual
   name entry.
5. **Radio check-in as a fallback, not a foundation**: since Zello Work/PTT is already part of the
   operational toolkit, a coordinator relaying a verbal check-in and logging it manually remains a
   reasonable fallback for edge cases — but shouldn't be the primary mechanism given how easy it is
   to skip under pressure.
6. **BLE/proximity beacons (longer-term, more hardware)**: badges with a Bluetooth beacon and
   fixed readers in camp buildings could give passive, continuous presence-tracking without any
   worker action at all — worth considering once the simpler checkpoint model is proven, not as a
   v1 investment.

The badge-scan approach (#1) is the strongest starting point: it needs no new hardware beyond
what mine sites already require for site access, it strengthens a domain you're building anyway
(Trip & Manifest), and it removes the accuracy risk of manual name entry entirely for the legs
Northern Link actually operates.

**Access model change**: this means the Client Web App is **not purely read-only** as originally
scoped in Section 6 — Alamos Logistics Coordinators and dispatchers need write access for the
crew movement log and roster, scoped narrowly to that one domain.

### 5.3 Budget & Financial Control (Zero-Based Budgeting)

**Purpose**: every dollar moving through Northern Link — cost or revenue — is tagged to a
**Budget Code**. Codes are re-justified from zero each budget period rather than carried forward
by default, which forces an active allocation decision every cycle instead of incremental
drift. This closes a real gap: the roadmap currently has plenty of ways to *generate* revenue and
cost data (contracts, trips, apprenticeship spend, fleet maintenance) but no unifying structure
for *governing* it.

**Data model:**
- `BudgetCode` — code, name, category (Revenue or Expense), and a parent stream label (e.g.,
  "Community Shuttle," "Alamos Contract," "NIHB," "Apprenticeship Program," "Fleet Maintenance,"
  "Parcel/Grocery," "Crew Coordination Services")
- `BudgetPeriod` — a month or quarter
- `BudgetAllocation` — a Budget Code × Period × planned amount, entered fresh each period
  (the "zero-based" part — no auto-carry-forward)
- `ActualTransaction` — synced from QuickBooks Online via the existing Billing & Accounting
  Integration (read-only, same pattern already established for the NLBC concept), tagged to a
  Budget Code

**Rider Express lens applied directly here**: a natural starter set of Budget Codes maps onto the
same revenue categories Rider Express's model tracks — Passenger, Parcel/Freight, Charter,
Ancillary — plus Northern Link-specific streams (NIHB, Grocery, Crew Coordination, Apprenticeship).
That makes it possible to literally read Northern Link's revenue mix against the Rider Express
benchmark (70–75% / 15–20% / 5–10% / 2–5%) as a real dashboard number, not just an aspiration.

**Relationship to the NLBC SaaS concept — resolved (default adopted):** build once, dogfood
first. The Budget & Financial Control domain is designed with the same multi-tenant-ready
patterns as the NLBC spec from day one; Northern Link uses it internally first, and becomes
NLBC's first real proof of concept before any external SaaS launch. Flag if this should be
revisited — it's a default applied in the absence of an objection, not a hard commitment.

**Access**: primarily the Owner/Exec Desktop App (super-user financial oversight — Owner,
Accountant, future Board), consistent with its "strategic, not operational" scope from Section 6.1.
The Admin Web App may get a narrow read-only view for operational categories (e.g., a dispatcher
seeing fuel budget-vs-actual for their route) — worth confirming once Phase 3 (Admin Web App) is
underway rather than deciding now.

### 5.4 Driver Self-Service Trip Selection & Eligibility Engine

**Purpose:** extend Trip & Manifest Management so drivers browse and claim available trips
directly from the Driver Field App, rather than every run requiring a dispatcher to hand-assign
it. Note: this doesn't have a Rider Express equivalent — a carrier running centralized dispatch at
that scale wouldn't need this. It maps instead onto Northern Link's own established competitive
advantage: **flexibility and responsiveness that a larger, more rigid carrier can't match.**

**Trip modes:**
- **Open** — any eligible driver may claim it
- **Assigned** — a dispatcher (or, in Phase 1, the Owner acting as dispatcher) has designated a
  specific driver, used when a client contractually requires a particular driver

**Eligibility calculation** — a driver only sees an Open trip in their available list if *all* of
the following hold:
1. **Remaining Hours of Service** (Feature under Driver & Compliance, Section on HOS) covers the
   trip's estimated duration
2. **Licence class** matches the vehicle's requirement (the current fleet — 7-seat van, 24-seat
   bus — falls under Class 4 per Manitoba's licensing schedule; this should still be encoded as a
   per-vehicle rule rather than hardcoded, since a future larger vehicle could require Class 2)
3. **Vehicle status** is Active, with no outstanding failed DVIR
4. **No schedule conflict** with a trip the driver has already claimed
5. **Client-specific clearance**, where applicable — most notably, **Alamos contractor
   clearance**. This directly reflects the accountability standard established after the June
   2026 incident (driver removal from active roster, contractor clearance desk workflow): a
   driver without active Alamos clearance should never see an Alamos-routed trip in their eligible
   list, automatically, rather than depending on a dispatcher remembering to check.

**Concurrency requirement:** claiming an Open trip must be an atomic, server-validated operation,
not just a client-side filter — two drivers must never be able to claim the same trip in a race
condition, and eligibility (especially HOS and clearance status) must be re-checked at the moment
of claiming, not just when the list was last loaded.

---

---

**Strategic framing**: this domain repositions Northern Link from a ground-transportation
*carrier* to a **Human Logistics Coordinator** — the system of record for workforce location
regardless of who physically moves the person. This is a distinct, higher-value proposition worth
its own conversation with Brendan D'Allaire, separate from the shuttle contract renewal, and
potentially its own billable service line (see Thompson Logistics Services precedent — $50/hour
field coordination billed separately from shuttle rates).

---

## 6. Client Applications — API Consumption Map

| App | Tenant/Users | Primary domains consumed | Offline requirement |
|---|---|---|---|
| **Community Mobile App** (iOS/Android) | Consumer | Booking, Payments, NIHB, Grocery, Parcel, Notifications | None (light caching only) |
| **Client Web App** (Alamos, future clients) | Client | Client & Contract (read), Trip/Manifest (read), Billing (invoices, read), **Crew & Workforce Visibility (read + write)** | None |
| **Driver Field App** (company-issued tablet) | Internal Driver + Partner Driver | Driver & Compliance, Trip/Manifest, Incident & Fault, Fleet (status updates) | **Offline-first — critical** |
| **Admin Web App** (dispatch/ops/supervisors) | Internal (Admin) | Nearly all domains — dispatch, client mgmt, vendor mgmt, fleet, reporting (operational view), Crew & Workforce Visibility (write, on client's behalf) | None (office-based) |
| **Owner/Exec Desktop App** | Internal (Admin) — Owner, Accountant, future Board | Reporting & Analytics, Billing, Client & Contract (strategic view), cross-cutting dashboards | None |

The **Driver Field App scope correction** you made (admin functions moved out) means it should
consume only: Driver & Compliance, Trip/Manifest, Incident & Fault, and a narrow slice of Fleet
(status/fault reporting) and Client & Contract (read-only, e.g., "which client is this trip for" —
needed to route to correct manifest template, nothing more).

### 6.1 Owner/Exec Desktop App — Access Model

**Confirmed: super-users only.** This app is not a general internal tool — access is restricted
to a small, named set of roles: Owner, Accountant, and (future) Board Members. This has two
practical implications:

- **Authorization**: a distinct `SuperUser` claim/flag on top of the normal role system, checked
  at both the API and the app's login gate, rather than just relying on "Internal tenant" being
  enough. Someone could be an Internal-tenant Dispatcher without ever having Desktop App access.
- **Feature scope stays strategic, not operational**: dashboards, financial reporting, contract
  overviews, cross-cutting analytics. Day-to-day dispatch stays in the Admin Web App. This keeps
  the two internal apps cleanly separated by *purpose* (run the business day-to-day vs. see the
  whole business), not just by role label.

---

## 7. Data Architecture

- **Single PostgreSQL database**, shared schema, `tenant_id` column on all tenant-scoped tables,
  enforced by RLS (Section 4.3).
- **CQRS read models** (materialized views or a dedicated read schema) power the Admin and Owner
  dashboards without hammering the transactional tables.
- **Object storage** (OVHcloud Object Storage, S3-compatible) for DVIR photos, signatures, PDF
  manifests, invoices — referenced by URL/key in Postgres, not stored as blobs in the DB.
- **Audit log** as its own append-only table (or event store), capturing who accessed or modified
  what — particularly important for Alamos contractor data and NIHB client records.

---

## 8. Offline Sync Strategy (Driver Field App)

This is the one app where offline-first is not optional — cellular coverage between Thompson,
Lynn Lake, and the Alamos mine site is patchy by design of geography, not a corner case.

- **Local-first store** on the tablet (SQLite) mirrors the subset of data the driver needs for
  their assigned trips.
- **Command queue**: every action (DVIR submission, manifest update, fault report, cargo log
  entry) is written locally first, queued, and synced when connectivity returns.
- **Idempotent commands**: each queued command carries a client-generated GUID so retried syncs
  never double-submit a DVIR or duplicate a manifest.
- **Conflict resolution**: since a vehicle/trip has exactly one driver of record at a time,
  conflicts should be rare. Default to last-write-wins with a full audit trail rather than
  building complex merge logic — over-engineering this for a rare case isn't worth it.
- **Sync status indicator**: visible to the driver at all times (already an established pattern
  from prior design work) — never let sync state be invisible.

---

## 9. Integrations

| Service | Purpose | Notes |
|---|---|---|
| **QuickBooks Online** | Invoicing, payment sync | OAuth tokens held centrally in API, never on client devices |
| **Stripe** | Community app payments | PCI scope stays on Stripe's side |
| **SendGrid** | Transactional email | Trip confirmations, invoices, endorsements |
| **Twilio** | SMS notifications | Departure reminders, driver contact reveal |
| **Google Maps** | Geocoding, routing | Trip distance calc, ETA |
| **Zello Work / AINA PTT** | Driver voice comms | Stays outside the API for now — operational radio layer, not a data integration target in v1 |

---

## 10. Security & Compliance

- **PIPEDA compliance**, Canadian data residency: all infrastructure on OVHcloud Canada
  (Beauharnois, QC) — already established as non-negotiable.
- **Encryption** at rest and in transit (TLS everywhere, encrypted DB volumes).
- **Retention rules** enforced in code, not just policy: 6-month minimum for Hours of Service
  logs (CVDHS), maintenance records per NSC Standard 13.
- **Contractor data scoping**: Alamos sees its contractors' manifest data; contractors themselves
  never get direct platform access in v1.
- **SOC 2**: explicitly a future aspiration, not a current claim — keep that language consistent
  across every client-facing document, as already agreed with the Alamos portal proposal.

---

## 11. Environments & Deployment

- **Hosting**: OVHcloud Canada (Beauharnois, QC)
- **Environments**: Dev → Staging → Production
- **CI/CD**: GitHub Actions, automated tests + migrations (Entity Framework)
- **Monitoring**: application logging + error tracking (tool TBD — flagged as an open item, not yet decided in prior planning)

### 11.1 Monitoring & Observability — Recommendation

Given the data-residency principle already governing every other infrastructure choice, the
recommendation is a **self-hosted, open-source stack**, running on OVHcloud Canada alongside
everything else — rather than a US-based SaaS monitoring product that would otherwise be the
default choice:

| Purpose | Recommended tool | Why |
|---|---|---|
| Error tracking (exceptions, stack traces) | **Self-hosted Sentry** (open-source edition) | Sentry Cloud is US-hosted by default; self-hosting keeps crash/error data — which can include user context — inside Canada |
| Metrics & dashboards | **Prometheus + Grafana** | Standard, well-documented pairing for .NET/Postgres metrics; Grafana dashboards give you (and the Owner Desktop App, eventually) a visual operational health view |
| Log aggregation | **Grafana Loki** | Pairs naturally with Grafana; keeps log search cheap compared to a hosted log SaaS |
| Uptime/external monitoring | **Uptime Kuma** (self-hosted) | Lightweight, simple status-page style monitoring for "is the API/apps reachable" |

This is a heavier lift to stand up than clicking "connect" on a SaaS dashboard, but it's consistent
with everything else in this document, and at your current scale the operational overhead is
manageable — a small number of Docker containers alongside the main application stack.

### 11.2 Local Development

The canonical local dev entry point is the **.NET Aspire AppHost** (`AppHost/NorthernLink.AppHost/`
at the workspace root — a platform-level orchestrator, deliberately not nested inside `Backend/`,
since it starts Postgres, RabbitMQ, the API, *and* the Dispatcher dev server together with a live
dashboard). Run `aspire run` from anywhere in the repo, or `dotnet run --project
AppHost/NorthernLink.AppHost` — this replaced what used to be three separate manual steps
(docker-compose + `dotnet run` + `npm run dev`). `Directory.Build.props`/`Directory.Packages.props`
live at the workspace root too, for the same reason — shared build settings and central package
versions available to every .NET project regardless of folder.

Postgres/RabbitMQ and the API's own config stay **orchestration only** — fixed environment-variable
values matching what a standalone `dotnet run` on the API also uses, not dynamically injected.
The one deliberate exception is the API→Dispatcher relationship: the AppHost wires
`.WithReference()` so Aspire injects the API's resolved URL into the Dispatcher process, which
`Dispatcher/next.config.ts` uses to proxy `/api/*` requests server-side. That's what lets the
browser only ever talk to the Dispatcher's own origin — **no CORS configuration exists anywhere
in the stack**, by design, not by omission.

Local Postgres runs as a plain superuser, so **Row-Level Security is unenforced in local dev by
design**; RLS is only actually verified against the live server (see
`Backend/docker/initdb/01-app-role.sql`, which provisions the non-superuser role there).
`ConnectionStrings:Postgres` lives entirely in environment variables — never
`appsettings.Development.json`.

---

## 12. Technology Stack Summary

| Layer | Choice |
|---|---|
| Frontend (web apps) | Next.js 15 |
| Backend API | .NET 10, CQRS/DDD — one class library per domain, composed by the API gateway |
| Database | PostgreSQL (+ RLS for tenant isolation) |
| Object storage | OVHcloud Object Storage (S3-compatible) |
| Mobile — Community App | **Flutter — decided.** Personal device (passenger's own phone), iOS/Android |
| Mobile — Driver Field App | **Flutter — decided, company-issued tablet only.** Deployed exclusively on Northern Link-owned, MDM-enrolled tablets — **not** a BYOD (bring-your-own-device) app, and not intended for a driver's personal phone. This keeps company/client data off personal devices (a real PIPEDA-relevant simplification), ensures consistent hardware for badge-scan check-in (Feature 3.2/3.4), and matches the existing operational pattern already established for the Zello Work / Starlink tablet setup |
| Desktop (Owner/Exec App) | **Access restricted to super-user roles only** (Owner, Accountant, future Board) — see 6.1. Technical approach still open; recommend a lightweight Electron wrapper reusing the Admin Web App's Next.js dashboard code rather than a separate native build, given the very small, low-churn user base |
| Hosting | OVHcloud Canada, Beauharnois QC |
| Identity | Self-hosted OIDC (OpenIddict recommended) — confirmed |
| Monitoring/Observability | **Recommended stack** — see Section 11.1 |

*Both Flutter apps share the same offline-storage approach (SQLite via `sqflite`/`drift`), which
matters most for the Driver Field App given its offline-first requirement, but keeps the codebase
patterns consistent across both.*

---

## 13. Open Decisions Flagged for Your Input

These are real decisions, not defaults I've silently picked — worth a deliberate answer before
the roadmap locks them in:

1. ~~Identity provider~~ — **Resolved**: self-host OIDC for now.
2. ~~Mobile app framework~~ — **Resolved**: Flutter, for both Community App and Driver Field App.
3. ~~Desktop app approach~~ — **Resolved (access model)**: restricted to super-user roles (Owner,
   Accountant, future Board). Technical implementation recommended as Electron wrapping the Admin
   Web App's Next.js code, given the small user base — confirm if you want a different approach.
4. **Vendor/Partner onboarding depth**: how much self-service does Miller the Mover / future
   independent operators get in v1 vs. Northern Link admin managing it all on their behalf
   initially? — **Still TBD**, no rush to resolve before the roadmap.
5. ~~Monitoring/observability tooling~~ — **Recommended**: self-hosted Sentry + Prometheus/Grafana
   + Loki + Uptime Kuma, all on OVHcloud Canada (Section 11.1).
6. ~~Crew roster source of truth~~ — **Resolved**: joint maintenance by Northern Link and Alamos
   dispatchers in Phase 1; Phase 2 explores integration with Alamos's existing HR/roster systems
   (discovery conversation with Alamos, not a unilateral technical decision).
7. ~~External carrier data ingestion~~ — **Resolved**: manual entry by Alamos Logistics
   Coordinators in Phase 1, with system integration (flight manifests, camp check-in systems) as
   the preferred Phase 2 direction, contingent on what Alamos is willing/able to connect.
8. ~~Staleness threshold~~ — **Resolved (design)**: always display last confirmed location, date,
   and time; context-aware thresholds per location-state; traffic-light indicator; escalation via
   Notifications domain. See Section 5.1.
9. ~~Write permission for crew movement~~ — **Resolved**: Alamos Logistics Coordinators (Client
   tenant) log non-Northern-Link movements; Northern Link does not do this data entry on their
   behalf. Both NL and Alamos dispatchers maintain the shared roster.
10. **Worker Self-Check-In App (optional, future)**: worth revisiting once Phase 1/2 of crew
    visibility are proven — raises its own consent/privacy questions (worker location visible to
    employer) that would need a clear policy before building. **Superseded in priority by the
    badge-scan approach (Section 5.2)**, which achieves the same accountability goal without
    requiring workers to carry a phone at all.

---

## 14. Next Steps

This document is the foundation. Next up, per your sequencing: the **Roadmap & Phasing**
document, which sequences the five apps and the API domains above against your actual business
priorities (Alamos contract renewal, NIHB rollout, driver app corrective action from the June
incident, apprenticeship program timeline).

---

*Northern Link Shuttle & Cargo — Internal Platform Documentation*
*Prepared for: Claude Code build instructions, UXPilot design briefs, and internal stakeholder reference*
