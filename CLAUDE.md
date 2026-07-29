# Northern Link Shuttle & Cargo — Platform Workspace

Multi-app platform: one .NET API serving five client apps. This workspace root holds the shared
backend and (currently) one frontend. Full architecture: see the `northern-link-architecture`
skill (`.claude/skills/northern-link-architecture/`) — **consult it before any non-trivial work,
frontend or backend.**

## Folder Map

| Folder | What it is | Stack |
|---|---|---|
| `Backend/` | The one shared API — one class library per domain, composed by the API gateway | .NET 10, CQRS/DDD, PostgreSQL (DigitalOcean managed, no local instance), RabbitMQ |
| `Dispatcher/` | Admin Web App (Dispatch Console) — currently a frontend-only prototype on mock data | Next.js 16, React 19 |
| `AppHost/` | Local dev orchestrator — starts Postgres, RabbitMQ, the API, and Dispatcher together. Platform-level, not part of Backend — it depends on Backend and Dispatcher, not the other way around | .NET 10, Aspire |

Future app folders (Driver Field App, Client Web App/Alamos, Community Mobile, Owner Desktop)
will be added as siblings, and `AppHost/` will grow to orchestrate them too.

`Directory.Build.props` and `Directory.Packages.props` live at this workspace root, not inside
`Backend/` — they're shared, platform-wide build settings and central package versions, picked up
by every .NET project regardless of which folder it lives in (`Backend/`, `AppHost/`, and any
future .NET-based app folder).

## Agents — Parallel Work Routing

- **Backend work** (API, domain libraries, EF Core, database, messaging, auth) → `backend-dev` agent
- **Frontend work** (Dispatcher screens, components, styling) → `frontend-dev` agent

The seam between them is the API contract, owned by the backend (future OpenAPI spec). Until real
endpoints exist, the frontend stays on mock data in `Dispatcher/lib/data.ts` — the frontend never
invents endpoint shapes.

## Non-Negotiables (recap — full list in the skill)

- One class library per domain, composed in the gateway API (`Backend/src/Api`): new features
  extend an existing library; never a new deployment unit. A future microservice = copy the
  library + `NorthernLink.Shared` out
- Domain libraries never reference each other — cross-domain communication is integration-events-only;
  event records live in `NorthernLink.Shared`. Delivery is two-path: storing/projecting events
  (replica upserts, flag flips — all current events) are consumed in-database by each module's
  `OutboxPollingConsumer` polling the producer outbox tables (`processing_status` column tracks
  delivery); RabbitMQ is reserved for future chain-reaction events that must trigger commands in
  another module (`BusPublicationRegistry`, currently empty — the bus is wired but dormant)
- Every tenant-scoped table: `tenant_id` + API-level check + Postgres RLS (both, always)
- Canadian data residency (OVHcloud Canada) governs every infrastructure choice
- Status colors never stand alone: Teal `#009E73` / Gold `#E1B000` / Vermillion `#D55E00` + icon + label
- Library isolation in Backend is enforced by architecture tests — don't fight them, fix the design

## Commands

### One-command local dev (from the workspace root)
- `aspire run` — starts RabbitMQ, the API, and the Dispatcher dev server together via the Aspire
  AppHost, with a dashboard URL printed to the console. Run from **anywhere in the repo, including
  this root** — the Aspire CLI auto-discovers the single AppHost project
  (`AppHost/NorthernLink.AppHost/`) and caches the result in `aspire.config.json` at this root.
  The Aspire CLI version must track the `Aspire.AppHost.Sdk` version in the AppHost csproj
  (currently 13.4.6) — a CLI more than a minor version behind breaks the dashboard's TLS
  connection to the resource service (`RemoteCertificateNameMismatch`); update with
  `curl -sSL https://aspire.dev/install.sh | bash`.
  Equivalent explicit form: `dotnet run --project AppHost/NorthernLink.AppHost`.
  There is **no local Postgres** — every environment (orchestrated, standalone, production) talks
  to the same DigitalOcean managed database, so RLS is exercised for real in every run, not just
  in production. That also means dev needs a working path to
  `db-postgresql-tor1-71774-do-user-37476504-0.e.db.ondigitalocean.com:25060` — if connections time
  out, check DigitalOcean's Trusted Sources firewall for the DB cluster before assuming a code
  problem. The browser only ever talks to the Dispatcher's own origin (`:3001`) — Next.js proxies
  `/api/*` server-side to the API, so there's no CORS configuration anywhere in the stack.

### Backend (`Backend/`)
- `dotnet build` — build the whole solution (warnings are errors)
- `dotnet test` — includes architecture tests that enforce domain-library boundaries
- `dotnet run --project src/Api/NorthernLink.Api` — run just the API standalone (no AppHost); env
  vars come from `Properties/launchSettings.json`, not appsettings — this hits the same
  DigitalOcean Postgres as everything else (no local instance)
- Secrets — `ConnectionStrings__Postgres`, `Identity__JwtSigningKey`, `RabbitMq__UserName`,
  `RabbitMq__Password` — are read directly via `Environment.GetEnvironmentVariable`
  (`NorthernLink.Shared.Kernel.RequiredEnvironmentVariable`), never through `IConfiguration`/
  `appsettings.json`, so they can never end up in a committed config file. Standalone runs get
  them from `Backend/src/Api/NorthernLink.Api/Properties/launchSettings.json`; orchestrated runs
  (`aspire run`) get them from `AppHost/NorthernLink.AppHost/AppHost.cs`; a real deployment sets
  them directly on the host/container. Everything else (RabbitMQ host/port/exchange, logging,
  `AllowedHosts`) is non-secret and still config-bound in `appsettings.json` as normal.
- **Both `AppHost/NorthernLink.AppHost/AppHost.cs` and
  `Backend/src/Api/NorthernLink.Api/Properties/launchSettings.json` are gitignored** — each
  carries the above secrets as literal values (the DO Postgres connection string now that there's
  no local fallback, plus the dev JWT key and RabbitMQ credentials). Neither is in the repo; a
  fresh clone needs local copies recreated by hand before `aspire run` or a standalone
  `dotnet run` will work. Never re-add either to git — edit your local copy and share diffs
  out-of-band, not via a commit.

### Dispatcher (`Dispatcher/`)
- `npm run dev` — dev server standalone (no AppHost), usually lands on port **3001** (3000 is
  often taken on this machine). Requests to `/api/*` proxy to `http://localhost:5215` by default
  (see `next.config.ts`) — works against a manually-started API with no other setup.
