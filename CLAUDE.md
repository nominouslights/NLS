# Northern Link Shuttle & Cargo — Platform Workspace

Multi-app platform: one .NET API serving five client apps. This workspace root holds the shared
backend and (currently) one frontend. Full architecture: see the `northern-link-architecture`
skill (`.claude/skills/northern-link-architecture/`) — **consult it before any non-trivial work,
frontend or backend.**

## Folder Map

| Folder | What it is | Stack |
|---|---|---|
| `Backend/` | The one shared API — modular monolith, all domain modules | .NET 10, CQRS/DDD, PostgreSQL, RabbitMQ |
| `Dispatcher/` | Admin Web App (Dispatch Console) — currently a frontend-only prototype on mock data | Next.js 16, React 19 |

Future app folders (Driver Field App, Client Web App/Alamos, Community Mobile, Owner Desktop)
will be added as siblings.

## Agents — Parallel Work Routing

- **Backend work** (API, domain modules, EF Core, database, messaging, auth) → `backend-dev` agent
- **Frontend work** (Dispatcher screens, components, styling) → `frontend-dev` agent

The seam between them is the API contract, owned by the backend (future OpenAPI spec). Until real
endpoints exist, the frontend stays on mock data in `Dispatcher/lib/data.ts` — the frontend never
invents endpoint shapes.

## Non-Negotiables (recap — full list in the skill)

- Modular monolith: new features extend an existing domain module; never a new deployment unit
- Every tenant-scoped table: `tenant_id` + API-level check + Postgres RLS (both, always)
- Canadian data residency (OVHcloud Canada) governs every infrastructure choice
- Status colors never stand alone: Teal `#009E73` / Gold `#E1B000` / Vermillion `#D55E00` + icon + label
- Module isolation in Backend is enforced by architecture tests — don't fight them, fix the design

## Commands

### Backend (`Backend/`)
- `docker compose up -d` — local Postgres 17 (5432) + RabbitMQ 4 (5672, UI at 15672)
- `dotnet build` — build the whole solution (warnings are errors)
- `dotnet test` — includes architecture tests that enforce module boundaries
- `dotnet run --project src/Api/NorthernLink.Api` — boot the API host

### Dispatcher (`Dispatcher/`)
- `npm run dev` — dev server (note: usually lands on port **3001**; 3000 is often taken on this machine)
