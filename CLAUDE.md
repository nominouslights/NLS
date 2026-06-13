# Shuttle Software — Project Standards

Northern Link Shuttle & Cargo multi-platform transportation management system.

## Repository Structure

```
Shuttle Software/
├── shuttle-api/        .NET 10 REST API (CQRS + DDD + Clean Architecture)
├── shuttle_tablet/     Flutter tablet operations app (Riverpod + Clean Architecture)
├── shuttle_driver/     Flutter driver mobile app (early stage)
├── shuttle_client/     React 19 web client portal (early stage)
└── .claude/agents/     Sub-agent definitions (see below)
```

## Sub-Agents

Use these agents by typing `@agent-name` when working in a specific area:

| Agent | Use for |
|-------|---------|
| `shuttle-master` | Cross-project features, architecture decisions, routing work to other agents |
| `shuttle-api` | Anything in `shuttle-api/` — endpoints, CQRS, domain logic, migrations |
| `shuttle-tablet` | Anything in `shuttle_tablet/` — pages, providers, use cases, widgets |
| `shuttle-client` | Anything in `shuttle_client/` — React pages, components, hooks |
| `shuttle-driver` | Anything in `shuttle_driver/` — driver app screens and logic |

---

## Global Principles

These apply to every project in this repository.

1. **Clean Architecture boundaries are inviolable.** Higher layers never import lower layers directly. Domain has zero dependencies on frameworks.
2. **No business logic in controllers or UI.** Business rules live in domain entities (API) or use cases (Flutter/React).
3. **Fail explicitly.** Return typed errors (`Result<T>` in C#, `Either<Failure, T>` in Dart) — do not swallow exceptions silently.
4. **Every schema change requires a migration.** Never modify a deployed migration.
5. **Secrets never in source control.** Use `.env` files (gitignored) and environment variables.
6. **`DateTime.UtcNow` / `DateTime.now().toUtc()` everywhere.** Never use local time for stored values.
7. **Soft delete auditable entities.** Set `IsDeleted = true` — do not hard-delete records that affect billing or compliance.

---

## shuttle-api (.NET 10)

### Architecture Layers

```
ShuttleApi.Domain          → Entities, aggregates, value objects, domain events
ShuttleApi.Application     → Commands, queries, handlers, DTOs, behaviors
ShuttleApi.Infrastructure  → EF Core, repositories, auth, email, S3
ShuttleApi.Api             → Controllers, middleware, DI wiring
```

Dependency direction: `Api → Infrastructure → Application → Domain`.

### CQRS

- Every mutation is a `ICommand<TResponse>` + `ICommandHandler`.
- Every read is a `IQuery<TResponse>` + `IQueryHandler`.
- Use the **custom mediator** — do not introduce MediatR.
- Handlers orchestrate; domain entities enforce invariants.

### Domain Entities

```csharp
public sealed class MyEntity : AggregateRoot<Guid>
{
    private MyEntity() { }                    // EF Core constructor

    public static MyEntity Create(string name)
    {
        Guard.Against.NullOrEmpty(name, nameof(name));
        var e = new MyEntity { Id = Guid.NewGuid(), Name = name };
        e.RaiseDomainEvent(new MyEntityCreatedEvent(e.Id));
        return e;
    }

    public string Name { get; private set; } = default!;
}
```

- Private setters; mutate through methods only.
- Use `Guard.Against.*` for invariants — never raw `throw new Exception(...)`.
- Raise a domain event for every meaningful state transition.
- Use `Result<T>` for operations that can fail with a domain reason.

### Naming

| Artifact | Pattern | Example |
|----------|---------|---------|
| Command | `[Verb][Noun]Command` | `CreateTripCommand` |
| Query | `[Get\|List][Noun]Query` | `GetTripByIdQuery` |
| Handler | `[Name]Handler` | `CreateTripCommandHandler` |
| DTO / Response | `[Noun]Response` or `[Noun]Dto` | `TripResponse` |
| Domain Event | `[Noun][Verb]Event` | `TripDispatchedEvent` |
| Repository | `I[Noun]Repository` | `ITripRepository` |

### EF Core

- One `IEntityTypeConfiguration<T>` per entity in `Infrastructure/Persistence/Configurations/`.
- Always generate a migration after schema changes:
  ```
  dotnet ef migrations add <Name> -p src/ShuttleApi.Infrastructure -s src/ShuttleApi.Api
  ```
- Never modify an applied migration — add a corrective one instead.
- Query filters on `IsDeleted` where soft delete is needed.

### Controllers

- Thin — no logic. Wire request → command/query → `IActionResult`.
- All endpoints are `[Authorize]` unless intentionally public.
- Use `record` types for request bodies.
- Return `Ok`, `Created`, `BadRequest`, `NotFound`, `Forbid` — not raw status codes.

---

## shuttle_tablet / shuttle_driver (Flutter)

Both apps share the same Clean Architecture pattern.

### Layer Rules

```
features/[feature]/
├── data/
│   ├── datasources/     Dio HTTP calls only — no business logic
│   ├── models/          Freezed JSON DTOs — no domain logic
│   └── repositories/    Implements domain interface, maps model → entity
├── domain/
│   ├── entities/        Plain Dart — no framework imports
│   ├── repositories/    Abstract interfaces
│   └── usecases/        Single public method, returns Either<Failure, T>
└── presentation/
    ├── pages/           Route destinations — call providers, no direct API calls
    ├── providers/       Riverpod AsyncNotifier — calls use cases only
    └── widgets/         Stateless or minimal stateful components
```

### State Management (Riverpod)

- `AsyncNotifier<T>` for async state that can refresh or mutate.
- `FutureProvider` for computed/derived read-only values.
- `StateProvider<T>` for simple sync UI state only.
- **Never** use `StateNotifier` (deprecated).
- Providers call use cases — never datasources directly.
- Handle all `AsyncValue` states: `.when(data:, loading:, error:)`.

### Error Handling (fpdart)

- Use cases return `Future<Either<Failure, T>>`.
- `Right(value)` = success. `Left(Failure(...))` = expected error.
- Providers fold and throw the failure so Riverpod captures it as `AsyncError`.
- Show `Failure.message` to users — never expose stack traces.

### Freezed Models

- All API response models use `@freezed`.
- Extend with `.toEntity()` via an extension — keep Freezed classes as pure DTOs.
- Run after any model/annotation change:
  ```
  dart run build_runner build --delete-conflicting-outputs
  ```

### DI (GetIt + injectable)

- `@injectable` on implementations, `@lazySingleton` on services.
- Resolve with `sl<T>()`.
- Register all bindings in `core/di/injection_container.dart`.

### Naming

| Artifact | Pattern | Example |
|----------|---------|---------|
| Page | `[Feature]Page` | `TripDetailPage` |
| Provider class | `[Feature]Notifier` | `TripsNotifier` |
| Provider ref | `[feature]NotifierProvider` | `tripsNotifierProvider` |
| Use case | `[Verb][Noun]UseCase` | `GetTripsUseCase` |
| Model (DTO) | `[Noun]Model` | `TripModel` |
| Entity | `[Noun]` | `Trip` |

### Routing

- All routes in `core/routing/app_router.dart`.
- Use named routes — never hardcode path strings outside the router file.
- Protect routes with `redirect` callbacks for auth/role guards.

---

## shuttle_client (React + TypeScript)

### Rules

- Strict TypeScript — no `any`, no `@ts-ignore` without explanation.
- Function components only (`export function ComponentName`).
- CSS Modules for styling — no inline styles except for dynamic values.
- One component per file — filename matches component name in PascalCase.
- All API calls go through `src/api/client.ts` — no raw `fetch` in components.
- `VITE_` prefix for all environment variables.

### Naming

| Artifact | Pattern | Example |
|----------|---------|---------|
| Component | PascalCase | `TripCard.tsx` |
| Hook | `use` prefix, camelCase | `useTrips.ts` |
| Type/Interface | PascalCase | `Trip`, `TripCardProps` |
| CSS Module | `[Component].module.css` | `TripCard.module.css` |

---

## API Contract — Shared Types

These must stay in sync across all projects:

```
TripStatus:   Scheduled | Dispatched | EnRoute | Completed | Cancelled
ServiceType:  Charter | Community
UserRole:     Admin | Driver | Client
```

When adding a new status or type:
1. Update the C# enum in `ShuttleApi.Domain`.
2. Update the Dart use in `shuttle_tablet` and `shuttle_driver`.
3. Update the TypeScript type in `shuttle_client`.

---

## Git Workflow

- Branch naming: `feature/[description]`, `fix/[description]`, `chore/[description]`
- PRs target `main`
- Migrations must be included in the same PR as the feature that requires them
- Never force-push `main`

---

## Local Development

```bash
# API + database
cd shuttle-api && docker-compose up -d
dotnet run --project src/ShuttleApi.Api

# Tablet app
cd shuttle_tablet && flutter run

# Client web app
cd shuttle_client && npm run dev

# Driver app
cd shuttle_driver && flutter run
```
