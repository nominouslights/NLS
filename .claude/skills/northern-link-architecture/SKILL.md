---
name: northern-link-architecture
description: Canonical platform architecture for Northern Link Shuttle & Cargo's multi-app system — tenant model, unified authentication, API domain boundaries, data architecture, and tech stack. Use this skill for ANY work on Northern Link Shuttle & Cargo code: backend API/domain work, the Driver Field App, Client Web App (Alamos), Admin Web App, Community Mobile App, or Owner/Exec Desktop App, database schema or migrations, authentication/authorization, or CI/CD and hosting config. Also consult whenever the user mentions Northern Link, Alamos, driver field app, client portal, crew visibility, workforce tracking, budget codes, NIHB, tenant or role questions, multi-tenancy, or asks how a new feature should fit into the existing domain structure. Trigger this even for small changes (adding a field, a new endpoint, a new screen) — the point is to keep new code consistent with decisions already made, not just for big architectural questions.
---

# Northern Link Shuttle & Cargo — Platform Architecture

This skill exists so that any code written for Northern Link's platform — API, database, or any
of the five (soon six) client apps — matches decisions already made, instead of silently
reinventing tenant scoping, domain boundaries, or naming conventions from scratch.

**Full detail, rationale, and data models**: `references/architecture.md` (the complete Platform
Architecture Document, ~540 lines, with its own table of contents). Read it fully before doing
any non-trivial design work (new domain, new table, new auth flow). For quick orientation or a
small, well-scoped change, the summary below may be enough — but when in doubt, read the full
reference file rather than guessing from this summary.

---

## Non-Negotiable Rules — Always Apply These

1. **Modular monolith, not microservices.** One .NET 10 API, CQRS/DDD, organized into clearly
   separated domain modules (see domain list below). Do not introduce a new service/deployment
   unit for a new feature — add a new module instead.
2. **Multi-tenant, three tenant categories, enforced twice.** Internal (Admin), Client, and
   Vendor/Partner tenants, plus a non-tenant Consumer identity pool. Every tenant-scoped table
   needs a `tenant_id` and **both** an API-level authorization check **and** a Postgres Row-Level
   Security policy — never rely on API-level checks alone.
3. **Self-hosted OIDC identity** (OpenIddict), not a third-party IdP — this is a deliberate,
   confirmed choice tied to Canadian data residency, not an open question.
4. **Canadian data residency is non-negotiable.** All infrastructure on OVHcloud Canada
   (Beauharnois, QC). This governs hosting, the identity provider choice, and the
   self-hosted-over-SaaS monitoring stack (Sentry + Prometheus/Grafana + Loki + Uptime Kuma, all
   self-hosted).
5. **Driver Field App is a company-issued Android tablet, landscape-only, 10-inch class —
   never a BYOD/personal-phone app.** Don't build phone-responsive layouts for it.
6. **Status indicators never rely on color alone**, anywhere in any app. The established
   colorblind-safe palette: Teal `#009E73` (good/confirmed), Muted Gold `#E1B000`
   (caution/pending), Vermillion `#D55E00` (problem/overdue), Neutral Gray `#4A4A4A` with
   diagonal hash (unavailable/offline). Always pair color with an icon and a text label.
7. **Offline-first for the Driver Field App is a hard requirement, not a nice-to-have** —
   local-first store, command queue with client-generated GUIDs for idempotency, visible sync
   status at all times. See reference file Section 8 for the full sync strategy.
8. **QuickBooks Online is read-only from the platform's perspective for accounting sync**, but
   the platform is the source of truth for Budget Codes (Section 5.3) — every transaction gets
   tagged, don't let untagged transactions accumulate.
9. **The Owner/Exec Desktop App is super-user-only** (Owner, Accountant, future Board) — this is
   a distinct authorization flag, not just "Internal tenant." Never let a Dispatcher/Supervisor
   account incidentally gain access to it.

---

## Quick Reference: API Domains

Modular monolith, one database, organized into these domains (full detail in the reference file,
Section 5):

Identity & Access · Client & Contract Management · Vendor/Partner Management ·
Fleet & Vehicle Management · Driver & Compliance · Trip & Manifest Management ·
Incident & Fault Reporting · Community Booking & Payments · NIHB Medical Transport ·
Grocery & Parcel Services · Billing & Accounting Integration · Notifications ·
Reporting & Analytics · Crew & Workforce Visibility · Budget & Financial Control (ZBB)

## Quick Reference: Client Apps

| App | Tenant/Users | Device | Offline? |
|---|---|---|---|
| Community Mobile App | Consumer | iOS/Android (Flutter) | No |
| Client Web App (Alamos) | Client | Web | No |
| Driver Field App | Internal Driver + Partner Driver | Company-issued Android tablet only (Flutter) | **Yes — critical** |
| Admin Web App | Internal (Admin) | Web | No |
| Owner/Exec Desktop App | Internal (Admin), super-user only | Desktop (Electron recommended) | No |

## Quick Reference: Tech Stack

Frontend: Next.js 15 · Backend: .NET 10, CQRS/DDD · Database: PostgreSQL + RLS · Mobile: Flutter ·
Object storage: OVHcloud Object Storage (S3-compatible) · Hosting: OVHcloud Canada (Beauharnois,
QC) · Identity: self-hosted OIDC (OpenIddict) · Monitoring: self-hosted Sentry + Prometheus/Grafana
+ Loki + Uptime Kuma

---

## When Writing Code

- Check whether a new feature fits an **existing domain** before proposing a new one — the domain
  list above is deliberately broad; most new features extend something already there.
- Check the **tenant model** before adding any new role or access pattern — does it belong to
  Internal, Client, Vendor/Partner, or Consumer? Does it need a genuinely new role, or does an
  existing one already cover it?
- If a feature touches money (pricing, invoicing, any cost or revenue), check whether it needs a
  **Budget Code** tag (Section 5.3) — the platform's convention is tag-at-creation, not
  tag-after-the-fact.
- If a feature involves showing status to a user, apply the colorblind-safe palette from
  Non-Negotiable Rule 6 — don't introduce a new ad hoc color scheme.
- If unsure whether something is already decided vs. still open, check reference file Section 13
  ("Open Decisions") before assuming it's undecided — many items there are already resolved and
  marked as such.
