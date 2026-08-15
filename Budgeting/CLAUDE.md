# Budgeting — Zero-Based Budgeting Console

Next.js 16 app on port **3003**, consuming the shared API. Scaffolded by US-6.0.1 (Track 6,
Stage 6.0). Budget **periods** and **codes** are real; allocations, actuals and variance are
still mock — see [Data](#data-periods-and-codes-are-real-the-rest-is-still-mock).

## Commands

- `npm run dev` — dev server on **3003** (pinned in the script). `/api/*` proxies server-side to
  the API (`next.config.ts`), so the browser only ever talks to this origin and there is no CORS
  anywhere in the stack. Also started by `aspire run` from the workspace root.
- `npm run build` / `npm run lint`
- `npm test` — Vitest. See [Testing](#testing).

## The design system is a copy, not a shared package

`Budgeting/` deliberately holds **identical copies** of Dispatcher's design system. This was a
decision, not an accident: extracting a shared package was the alternative and it was rejected
for now (only two apps share this chrome, and there is no npm workspace at the repo root).

**The rule: change Dispatcher first, then re-copy. Never edit a copied file in place.** Drift
here is a visible product bug — two consoles that no longer look like one platform — not a style
nit.

Every copied file opens with a fixed 2-line header naming its source. The header is why the
files are not byte-identical, so the check skips it:

```sh
# From the repo root. Prints nothing when everything is in step.
for f in \
  lib/theme.ts lib/format.ts lib/period.ts lib/useToday.ts lib/clipboard.ts \
  lib/api/transport.ts lib/api/format.ts lib/api/shared.ts app/globals.css \
  components/HeaderClock.tsx components/NavRail.tsx \
  components/ui/Button.tsx components/ui/Chip.tsx components/ui/CorridorStepper.tsx \
  components/ui/Field.tsx components/ui/FileField.tsx components/ui/ImageUploadField.tsx \
  components/ui/MetricTile.tsx components/ui/ModalShell.tsx components/ui/MonthGrid.tsx \
  components/ui/Pager.tsx components/ui/Panel.tsx components/ui/PeriodNav.tsx
do
  diff -q <(tail -n +3 "Budgeting/$f") "Dispatcher/$f" >/dev/null 2>&1 || echo "DRIFT: $f"
done
```

Run it before touching anything on the list, and whenever a Dispatcher UI story lands.

### Manifest

| Budgeting path | Source | Verbatim? |
|---|---|---|
| `lib/theme.ts` | `Dispatcher/lib/theme.ts` | yes |
| `lib/format.ts`, `lib/period.ts`, `lib/useToday.ts`, `lib/clipboard.ts` | same paths | yes |
| `lib/api/transport.ts`, `lib/api/format.ts`, `lib/api/shared.ts` | same paths | yes |
| `app/globals.css` | `Dispatcher/app/globals.css` | yes |
| `components/ui/*` (12 files) | `Dispatcher/components/ui/*` | yes |
| `components/HeaderClock.tsx`, `components/NavRail.tsx` | same paths | yes |
| `app/layout.tsx` | `Dispatcher/app/layout.tsx` | **no** — title/description only; the four Google Fonts `<link>` tags are byte-identical and must stay that way |
| `lib/auth.ts` | `Dispatcher/lib/auth.ts` | **no** — see below |
| `components/TopBar.tsx`, `AuthGate.tsx`, `LoginScreen.tsx`, `Console.tsx` | same paths | **no** — adapted |
| `lib/nav.ts`, `lib/data.ts`, `lib/types.ts`, `lib/claims.ts`, `lib/roles.ts`, `lib/api/budgeting.ts` | — | new |
| `components/Brandmark.tsx`, `ErrorNotice.tsx`, `RoleGate.tsx`, `AccessDeniedScreen.tsx`, `SetupPendingScreen.tsx`, `BudgetPeriodFormModal.tsx`, `BudgetCodeFormModal.tsx`, `screens/*` | — | new |

`theme.ts` and the 12 `ui/` files are copied **unpruned**, including parts this app never uses
(`ServiceType`, `DutyStatus`, `CorridorStepper`, the two upload fields). Pruning them would break
the one-command diff above, and that check is the entire drift-control strategy. Dead exports
cost nothing; a broken check costs the premise.

`lib/api.ts` — Dispatcher's barrel — is deliberately **not** copied. It re-exports fleet, trips,
drivers and notifications clients that have no counterpart here. Import `@/lib/api/transport`
directly.

### Known inherited lint errors

`npm run lint` reports 3 errors and 2 warnings, **all** in copied files:

- `lib/useToday.ts` — 3 errors (`no-explicit-any`, setState-in-effect, `Date.now()` during
  render). Dispatcher fails on exactly the same 3 today.
- `app/layout.tsx`, `components/ui/ImageUploadField.tsx` — 2 warnings, likewise shared.

Nothing authored for this app lints dirty. Per the rule above, the fix belongs in Dispatcher
first — `useToday` needs its clock value moved into state, which is a real behavioural change to
a shipped screen and was out of scope for US-6.0.1.

## Auth and the role gate

Authentication reuses the platform's existing bespoke JWT flow (email/password + rotating
single-use refresh tokens). **There is no OIDC server** — `JwtAccessTokenIssuer` calls itself the
interim mechanism ahead of a future OpenIddict one, and migrating both consoles to it is its own
story.

`lib/auth.ts` differs from Dispatcher's in four deliberate ways, listed in its header. The one
worth restating: **this app cannot create accounts.** No `createFirstAdmin`, no invite minting or
redemption — first-run setup is a one-shot global gate and two apps racing for it is a bug
factory. Account creation stays in the Dispatch Console (Settings → Users & Roles, which now
mints invites naming any role).

### The gate is a UX gate, not a security boundary

`RoleGate` reads the `role` claim via an **unverified** client-side JWT decode (`lib/claims.ts`)
and blocks anyone who is not Owner or Accountant. A determined user can get past it.

That is acceptable because the real boundary is the `BudgetAccess` authorization policy in
`Backend/src/Api/NorthernLink.Api/Auth/AuthorizationPolicyRegistration.cs` — registered, unit
tested, and (since the first Stage 6.1 slice) attached to the `/api/budgeting` endpoint group in
`Backend/src/Budgeting/Infrastructure/Endpoints/BudgetingEndpoints.cs`. **Every future budgeting
endpoint must join that group or carry the policy itself.** Nothing in this app may treat passing
`RoleGate` as proof of anything.

`lib/roles.ts` mirrors `Roles.BudgetAccess` in `Backend/src/Shared/Kernel/Roles.cs`. Keep them in
step; both have tests that fail if they diverge.

## Testing

Vitest, added here as a **pilot for this app only** — it does not obligate Dispatcher to adopt
it. Four files, and only one needs a DOM:

- `lib/roles.test.ts` — US-6.0.1's acceptance criterion: a Dispatcher account is rejected.
- `lib/claims.test.ts` — JWT decoding, including the non-ASCII round trip (`atob` yields a binary
  string, so a naive decoder mangles accented emails) and malformed-token handling.
- `components/RoleGate.test.tsx` — the gate applies the rule and offers a way out.
- `lib/api/budgeting.test.ts` — the client-side mirrors of server rules stay pinned to the
  server: `previewPeriod` against `BudgetPeriod.Create`, `normalizeBudgetCode` /
  `budgetCodeFormatError` against `BudgetCode.NormalizeCode` / `ValidateCode`, and
  `parentCandidates` against `BudgetCodeParentRule`. Plus the →StatusKind mappings, the three
  label maps, and `toBudgetCode`'s `isActive` → `active` rename.

  The single highest-value assertion in the app is in here: that `SERVICE_LINE_LABELS`' first six
  keys are spelled exactly as `TripServiceType`'s members. That spelling is the join key for
  Stage 6.2's revenue-mix report, and getting it wrong drops a whole revenue category from the
  report with no error on either side.

Anything in this app that re-derives a server rule client-side belongs here, with the C# method
it mirrors named in the test's comment. That is the only thing keeping the two copies honest.

The server-side counterpart is
`Backend/tests/NorthernLink.Api.Tests/AuthorizationPolicyTests.cs`. Both exist on purpose: the
backend test proves the *policy* rejects Dispatcher, this one proves the *console* does, and in
Stage 6.0 the console is the gate a user actually meets.

## Data: periods and codes are real, the rest is still mock

**Budget periods come from the real API** (`GET/POST /api/budgeting/periods` via
`lib/api/budgeting.ts`; `Console.tsx` owns the fetch and threads the list down as props) — the
first Stage 6.1 slice. The wire carries no `planned`/`allocated` yet; `toBudgetPeriod` maps them
to honest zeros until the allocations slice lands, and derives `pk` from `state` (`periodKind`).

**Budget codes are real too** — the second slice, widened to US-6.1.1's full property set:

| Route | |
|---|---|
| `GET /api/budgeting/codes` | the whole chart, retired codes included |
| `GET /api/budgeting/codes/owners` | the owner picker's options, from the user replica |
| `POST /api/budgeting/codes` | |
| `PUT /api/budgeting/codes/{id}` | no `code` in the body — see below |
| `POST /api/budgeting/codes/{id}/activate\|deactivate` | |
| `DELETE /api/budgeting/codes/{id}` | narrow; 409 when the code has children or has been used |
| `POST /api/budgeting/codes/starter-set` | idempotent; returns how many it created |

Unlike periods, codes are **not** hoisted into `Console.tsx`: only `screens/BudgetCodes.tsx`
reads them, so that screen owns its own fetch.

Four rules the UI has to keep visible, because all four are enforced server-side and none is
guessable from the form:

- **The code string is set once.** There is no rename endpoint — allocations and actuals
  reference a code by string, so renaming would orphan every row already tagged. The edit modal
  renders it as read-only text rather than a disabled input, because disabled reads as "not right
  now" when the truth is "not ever". A mistyped code is retire-and-recreate.
- **Retiring is the normal end of a code's life.** A retired code stays listed so last period's
  rows keep resolving. `DELETE` exists only for a code created in error that nothing has ever
  referenced; `IBudgetCodeUsageProbe` turns it into a 409 the moment that stops being true, and
  the server's message names retirement as the alternative. The UI puts it behind a two-click
  confirm.
- **The hierarchy is one level deep**, guarded from both directions: a parent must be top-level,
  *and* a code that already has children cannot be given a parent (otherwise the chain is built
  bottom-up). `parentCandidates` in `lib/api/budgeting.ts` mirrors this so the picker never offers
  an option the server will reject. Retiring a parent does **not** cascade to its children.
- **`glAccountCode` is free text and always will be, for now.** QuickBooks work on this platform
  is manual by decision — `Invoice.EnteredInQbo` is a flag a bookkeeper ticks, and the platform
  never calls the QBO API. There is no synced chart of accounts to validate against and no
  validator abstraction pretending otherwise. The field's hint says so to the user.

`serviceLine`'s six revenue members are **byte-identical to the backend's `TripServiceType`**
(`ContractCrew, Community, Nihb, Charter, Cargo, Grocery`) so Stage 6.2's revenue-mix report joins
on the string Trips and Billing already emit. `budgeting.test.ts` pins those six spellings; do not
"tidy" `Nihb` into `NIHB`.

`lib/data.ts` holds the not-yet-real remainder: allocations, actuals, variance. Its `budgetCodes`
array survives **only** as the name-and-category lookup those three still need — the Budget Codes
screen no longer reads it, and the array is now typed
`Pick<BudgetCode, "id" | "code" | "name" | "category">` so it does not have to grow a
plausible-looking value for every field the real entity gains. Conventions follow
`Dispatcher/lib/data.ts`: flat exported const arrays, string ids, ISO date strings (never `Date`
objects), whole-dollar numbers rendered through `formatCad`, and a `StatusKind` carried on every
status-bearing row so rendering is a pure lookup. Its rows are keyed to mock period ids no real
period will match, so screens on real periods show their empty states rather than fake figures —
those screens keep their `MockTag`.

No screen invents an API shape. Each remaining array is replaced by additions to
`lib/api/budgeting.ts` as its Stage 6.1 slice lands, and the screens keep their props.

Variance thresholds (`varianceKind`) live in `lib/data.ts`, not in the Variance screen, so any
future report agrees with the screen by construction.

## Accessibility

Status is **never** carried by colour alone — the platform rule, and the reason `StatusMeta`
bundles a glyph with every hex. Signed figures always write out their `+` / `−` rather than
relying on red-versus-green.

Code audit (run before any visual pass):

```sh
# Every statusMeta() call must feed a StatusChip/StatusBadge or sit beside text.
grep -rn "statusMeta(" components lib | grep -v components/ui/
# Protected hexes should appear only in lib/theme.ts plus decorative dots/badges that carry text.
grep -rn "#009E73\|#E1B000\|#D55E00\|#7A8899" components lib app | grep -v lib/theme.ts
```

`components/screens/Variance.tsx` is the file to re-check hardest after any edit: a coloured
delta with no sign and no glyph is the exact failure this rule exists to prevent.

Known hazards documented in `theme.ts`: `colors.amber` is a fill/border/icon colour only — use
`colors.amberText` when amber must be text; `colors.textFaint` is decorative only.

### Audit record

| Date | Check | Result |
|---|---|---|
| 2026-08-04 | Code audit (both greps above) | **Pass** — every call site pairs colour with glyph + label; protected hexes appear only in `theme.ts` and in copied decorative elements that carry adjacent text |
| — | Grayscale (DevTools → Rendering → Achromatopsia) | **Not yet run** |
| — | Deuteranopia / Protanopia / Tritanopia | **Not yet run** |
| — | Side-by-side against Dispatcher at equal width | **Not yet run** |

To complete the outstanding rows: run Dispatcher on 3001 and this app on 3003, then in Chrome
DevTools → ⋮ → More tools → **Rendering** → **Emulate vision deficiencies**, walk all seven
screens plus login and access-denied under Achromatopsia, then each CVD mode. Pass condition:
every status is identifiable from glyph and label alone, and `ontime` vs `over` stay
distinguishable. `soon` (`#E1B000`) and `ontime` (`#009E73`) sit at similar luminance — that is
precisely why the glyphs are non-negotiable, and grayscale is what proves it.

Not used here on purpose: axe-core / pa11y / Lighthouse. They catch the automatable subset
(contrast, labels) and none of the colour-alone failures that actually matter on these screens.

## Out of scope (later Stage 6.1 slices and beyond)

- ~~The Budgeting backend domain library~~ — **exists since the create-budget-period story**:
  `Backend/src/Budgeting/` is listed in `ModuleGraph.DomainNames` and serves
  `/api/budgeting/periods` (create + list; periods are Draft-only until the Open/Lock story).
  The architecture tests still cross-check `DomainNames` against disk — any future module needs
  the same paired change.
- ~~The `BudgetCode` table and its RLS policies~~ — **shipped**: `budgeting.budget_codes` +
  `rm_budget_codes` (migrations `AddBudgetCodes`, then `ExtendBudgetCodes` for US-6.1.1's full
  property set). The free-text `stream` field the first slice carried was replaced by the
  `serviceLine` enum; its values were dropped, not translated, because a guessed service line
  silently misattributes the revenue mix the enum exists to compute.
- ~~A user reference for the budget owner~~ — **shipped**, and it is the platform's first
  cross-module user link: Identity gained its first `IIntegrationEventMapper` and publishes
  `identity.user-changed`; Budgeting consumes it into `budgeting.user_lookup`. Because Identity's
  outbox had always been empty, existing accounts arrive via the
  `BackfillBudgetingUserLookup` migration rather than by replay. **`Identity.User` is still
  create-only** — no rename, no deactivation — so the replica never shrinks and never sees an
  email change. Whoever adds those must raise a domain event and extend the mapper, or every
  replica goes stale with no error anywhere.
- `BudgetAllocation` / `ActualTransaction` tables and their RLS policies; QuickBooks actuals
  reconciliation. The Stage 6.2 slice that adds allocations must also replace
  `NeverReferencedBudgetCodeUsageProbe` — until it does, "has this code been used?" answers no,
  which is true today and will silently stop being true.
- **Any QuickBooks automation.** All QBO work is manual for now, by decision. Automating GL
  validation means an Intuit OAuth flow, per-tenant token storage (there is no tenants table),
  a QBO client and a chart-of-accounts sync — a slice of its own, not a gap in this one.
- Writing `event_journal.actor_id`. The column exists in every module schema and nothing fills
  it; this slice threads the actor onto the *domain events* instead, so `payload->>'actorId'`
  answers "who did this" today. Wiring the column properly touches nine DbContexts and nine
  design-time factories and belongs in a platform-wide story.
- **Validating the free-text `budget_code` strings that Clients and Fleet already carry** (on
  contracts, POs and work orders) against this chart. They are unrelated strings today, not
  replicas of these rows; wiring them together is a cross-module story and needs an integration
  event, not a project reference.
- Filtering the codes list, or hiding retired codes from the allocations picker — the list is
  small enough to render whole, and there is no allocations picker yet.
- CI/CD and the OVHcloud deployment target — there is no `.github/` anywhere in this repo yet;
  that is a platform-wide story covering every app at once.
- OIDC/OpenIddict; the `SuperUser` claim from architecture Section 6.1.
