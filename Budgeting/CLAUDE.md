# Budgeting — Zero-Based Budgeting Console

Next.js 16 app on port **3003**, consuming the shared API. Scaffolded by US-6.0.1 (Track 6,
Stage 6.0). Budget **periods**, **codes** and **allocations** are real — including the period
lifecycle and the period dashboard; actuals and variance are still mock — see
[Data](#data-periods-codes-and-allocations-are-real-actuals-and-variance-are-still-mock).

## Commands

- `npm run dev` — dev server on **3003** (pinned in the script). `/api/*` proxies server-side to
  the API (`next.config.ts`), so the browser only ever talks to this origin and there is no CORS
  anywhere in the stack. Also started by `aspire run` from the workspace root.
- `npm run build` / `npm run lint`
- `npm test` — Vitest. See [Testing](#testing).

## The design system is a copy, not a shared package

`Budgeting/` deliberately holds **identical copies** of Dispatcher's design system. This was a
decision, not an accident: extracting a shared package was the alternative and it was rejected
for now (there is no npm workspace at the repo root and each app has its own lockfile).

**The rule: change Dispatcher first, then re-copy. Never edit a copied file in place.** Drift
here is a visible product bug — two consoles that no longer look like one platform — not a style
nit.

**`DriverField/` now holds copies too (2026-09), so a Dispatcher design-system change is a
TWO-app re-copy.** Doing only this one leaves the Driver Field App silently behind. Its manifest
is 22 files rather than 23 — it omits `NavRail.tsx`, whose geometry is hardcoded and unusable at
tablet size — and it has its own drift check in `DriverField/CLAUDE.md`. Run both. The argument
for extracting a shared package gets stronger with each app that copies; revisit it at the next
one.

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
| `components/HeaderClock.tsx`, `components/NavRail.tsx` | same paths | yes — `NavRail`'s optional `groups` / `onHome` props exist for Dispatcher's app launcher and go unused here; `Console.tsx` still passes only the original three |
| `app/layout.tsx` | `Dispatcher/app/layout.tsx` | **no** — title/description only; the four Google Fonts `<link>` tags are byte-identical and must stay that way |
| `lib/auth.ts` | `Dispatcher/lib/auth.ts` | **no** — see below |
| `components/TopBar.tsx`, `AuthGate.tsx`, `LoginScreen.tsx`, `Console.tsx` | same paths | **no** — adapted |
| `lib/nav.ts`, `lib/data.ts`, `lib/money.ts`, `lib/types.ts`, `lib/claims.ts`, `lib/roles.ts`, `lib/api/budgeting.ts`, `lib/api/identity.ts` | — | new |
| `components/Brandmark.tsx`, `ErrorNotice.tsx`, `RoleGate.tsx`, `AccessDeniedScreen.tsx`, `SetupPendingScreen.tsx`, `BudgetPeriodFormModal.tsx`, `BudgetCodeFormModal.tsx`, `BudgetAllocationFormModal.tsx`, `ProfileForm.tsx`, `screens/*`, `screens/periods/*` | — | new |

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
it. Config is `vitest.config.mts` — the `.mts` extension is load-bearing (Vite loads a plain
`.ts` config as CommonJS and warns), and it must therefore use `import.meta.dirname` for the
`@` alias, never `__dirname`. `@types/node` declares `__dirname` globally, so TypeScript and
`next build` both stay green while every `@/lib/...` import in the suite fails to resolve at
run time. Ten files, and only two need a DOM:

- `lib/roles.test.ts` — US-6.0.1's acceptance criterion: a Dispatcher account is rejected.
- `lib/claims.test.ts` — JWT decoding, including the non-ASCII round trip (`atob` yields a binary
  string, so a naive decoder mangles accented emails) and malformed-token handling. Plus the
  claim **names**, mirrored from `JwtAccessTokenIssuer`'s `RoleClaimType` / `TenantIdClaimType` /
  `TenantTypeClaimType` — without those the file only proves the decoder agrees with payloads it
  wrote itself, and a backend rename would leave it green while every user hit access-denied.
- `lib/accessSeam.test.ts` — the token → claims → role → gate chain end to end, unmocked. Each
  link is covered on its own by the three files around it; this covers the seams between them,
  including the `""` role a decodable-but-roleless token produces.
- `components/RoleGate.test.tsx` — the gate applies the rule and offers a way out. Every denial
  case asserts the denial screen **renders**, not just that the children are absent: absence
  alone would also pass for a gate that showed nobody anything.
- `components/ProfileForm.test.tsx` — the profile form seeds from its props, refuses to save
  when nothing changed (including when the only change is whitespace, matching the server's
  no-op rule), sends `null` for a cleared field, and shows a refused save's message verbatim.
  It takes `onSave` as a prop, so it injects a `vi.fn()` rather than stubbing the transport.
- `lib/api/identity.test.ts` — `normalizeProfileField` against `User.Normalize`,
  `PROFILE_FIELD_MAX_LENGTH` against `User.ProfileFieldMaxLength`, and the
  `profileDisplayName` name-then-email fallback.
- `lib/api/budgeting.test.ts` — the client-side mirrors of server rules stay pinned to the
  server: `previewPeriod` against `BudgetPeriod.Create`, `normalizeBudgetCode` /
  `budgetCodeFormatError` against `BudgetCode.NormalizeCode` / `ValidateCode` (both sides of the
  32-character boundary, and the ASCII-only rule `char.IsAsciiLetterOrDigit` enforces),
  `parentCandidates` against `BudgetCodeParentRule`, `PERIOD_STATE_ORDER` /
  `nextTransition` / `stateAfter` against `BudgetPeriod.Transition`, `canEditAllocations`
  against `BudgetPeriod.AllowsPlanChanges`, `allocationCandidates` against the `CodeRetired`
  check plus the unique (period, code) index, and `allocationAmountError` /
  `allocationJustificationError` against `BudgetAllocation.Validate`. Plus the →StatusKind
  mappings (`periodKind` over all five states, `netKind`), the label maps, `toBudgetCode`'s
  `isActive` → `active` rename, `toBudgetPeriod`'s two totals, `coverage` and
  `planningProgress` (the dashboard's checklist and stepper derive from one tested function).
- `lib/api/transport.test.ts` — the 401 refresh-and-retry path, with `fetch` and `lib/auth`
  mocked. `request<T>`'s doc comment promises it "never loops"; these assert the exact attempt
  count (two fetches, one refresh) so the promise is enforced rather than stated. Plus `ApiError`
  construction — the parsed `{ code, message }` body, the `Http.<status>` fallback, and the
  `Network.Unreachable`/status-0 branch that `Console.tsx` and `screens/BudgetCodes.tsx` both
  branch on via `e instanceof ApiError`.
- `lib/api/budgeting.requests.test.ts` — what each of the nine request functions puts on the
  wire. Chiefly `setBudgetCodeActive`, whose route is built from a boolean (`activate` /
  `deactivate`): an inverted ternary there returns 204 either way and silently activates a code
  the planner asked to retire. Also pins that `deleteBudgetCode`'s 409 message reaches the caller
  verbatim, since the server's wording is what names retirement as the alternative.
- `lib/money.test.ts` — `formatDeltaCad` / `formatDeltaPct` always write the sign out, so a
  signed figure never rests on colour.

`lib/api/transport.ts` is a **copied** file. Tests against it belong here (a test file is not on
the copy manifest), but anything they reveal is a change to *Dispatcher's* source first, then a
re-copy — never an edit in place.

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

## Data: periods, codes and allocations are real; actuals and variance are still mock

**Budget periods come from the real API** (`GET/POST /api/budgeting/periods` via
`lib/api/budgeting.ts`; `Console.tsx` owns the fetch and threads the list down as props) — the
first Stage 6.1 slice. Each record now carries `plannedRevenueCad` / `plannedExpenseCad`, the
server's sums of the period's allocation lines by code category; `toBudgetPeriod` maps them to
`plannedRevenue` / `plannedExpense` and derives `pk` from `state` (`periodKind`).

**The period lifecycle is real** — five states, forward only, in this order:
**Draft → Finalized → Open → In review → Closed** (`PeriodState`; the C# enum spells the fourth
`InReview`). Allocation lines can change **only while the period is Draft or Open** — finalizing
signs the plan off, opening re-allows in-period adjustments, review and close freeze it
(`BudgetPeriod.AllowsPlanChanges`; mirrored by `canEditAllocations`). Each state has exactly one
way forward (`nextTransition`) and the dashboard offers exactly that one button, behind a
two-click confirm. Transitions are **not** gated on plan completeness — finalizing an empty plan
is allowed; the dashboard's checklist makes an empty plan visible instead.

**Allocations are real** — one line per (period, code), **upserted by code**, with a
**required justification** (zero-based: every line is argued from nothing, each period). The
line's `category`, `name` and `serviceLine` are resolved from the code at read time, never
snapshotted, so re-classifying a code moves its lines between the two totals retroactively. A
line on a retired code stays (and still counts) but cannot be re-set until the code is restored
(`CodeRetired`, 409). Amounts are `decimal(12,2)` server-side; this app enters whole dollars.

The period and allocation routes (`BudgetAccess` group, `BudgetingEndpoints.cs`):

| Route | |
|---|---|
| `GET /api/budgeting/periods` | ordered by `startsOn`; each row carries the two planned totals |
| `GET /api/budgeting/periods/{id}` | one period (404) — the `POST` 201 `Location` target |
| `POST /api/budgeting/periods/{id}/finalize` | Draft → Finalized; 409 `Budgeting.Period.NotDraft` |
| `POST /api/budgeting/periods/{id}/open` | Finalized → Open; 409 `NotFinalized` |
| `POST /api/budgeting/periods/{id}/begin-review` | Open → In review; 409 `NotOpen` |
| `POST /api/budgeting/periods/{id}/close` | In review → Closed; 409 `NotInReview` |
| `GET /api/budgeting/periods/{id}/allocations` | the period's lines, ordered by code (404) |
| `PUT /api/budgeting/periods/{id}/allocations/{codeId}` | upsert `{ amountCad, justification }` → `{ id, created }`; 400 validation, 404 period/code, 409 `PeriodNotEditable` / `CodeRetired` |
| `DELETE /api/budgeting/periods/{id}/allocations/{codeId}` | 204; 404 no such line; 409 `PeriodNotEditable` |

Every 400/409 message is shown verbatim — the server's text names the rule. Reads are
projections: after a transition the dashboard refetches the period until it reports the expected
state (`stateAfter`); after a line changes it refetches the lines until the new **values** are
visible (on edit the row was always there), and only then refreshes the period list, whose totals
read from the same projection.

`screens/BudgetPeriods.tsx` is the master/detail host; `screens/periods/*` is the dashboard
(list, lifecycle stepper, planning checklist, one `AllocationSection` per category);
`BudgetAllocationFormModal.tsx` is shared with `screens/Allocations.tsx`, which shows the same
lines flat and sums its tiles from the lines on screen (it has no way to refresh Console's
period list after a save). The dashboard shows the server's own totals.

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

Five rules the UI has to keep visible, because all five are enforced server-side and none is
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
- **A revenue code has no cost centre.** A cost centre attributes cost; revenue is not attributed
  to one. `BudgetCode.Validate` rejects the combination outright, the form hides the field when
  the category is Revenue, and the detail panel hides the row. `costCentreApplies` in
  `lib/api/budgeting.ts` is the single mirror both screens read.

`serviceLine`'s six revenue members are **byte-identical to the backend's `TripServiceType`**
(`ContractCrew, Community, Nihb, Charter, Cargo, Grocery`) so Stage 6.2's revenue-mix report joins
on the string Trips and Billing already emit. `budgeting.test.ts` pins those six spellings; do not
"tidy" `Nihb` into `NIHB`.

`lib/data.ts` holds the not-yet-real remainder: actuals and variance. Its `budgetCodes` array
survives **only** as the name-and-category lookup those two still need — the Budget Codes screen
no longer reads it, and the array is typed `Pick<BudgetCode, "id" | "code" | "name" | "category">`
so it does not have to grow a plausible-looking value for every field the real entity gains.
Conventions follow `Dispatcher/lib/data.ts`: flat exported const arrays, string ids, ISO date
strings (never `Date` objects), whole-dollar numbers rendered through `formatCad`, and a
`StatusKind` carried on every status-bearing row so rendering is a pure lookup. Its rows are
keyed to mock period ids no real period will match, so screens on real periods show their empty
states rather than fake figures — those screens keep their `MockTag`.

No screen invents an API shape. Each remaining array is replaced by additions to
`lib/api/budgeting.ts` as its Stage 6.1 slice lands, and the screens keep their props.

Variance thresholds (`varianceKind`) live in `lib/data.ts`, not in the Variance screen, so any
future report agrees with the screen by construction. The signed formatters
(`formatDeltaCad` / `formatDeltaPct`) live in `lib/money.ts`, **not** in `lib/data.ts`, so a
screen on real data (the dashboard's Net tile) never imports the mock module.

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
  `/api/budgeting/periods` (create, list, get-by-id and the four lifecycle transitions —
  ~~periods are Draft-only until the Open/Lock story~~ superseded by the five-state lifecycle
  above). The architecture tests still cross-check `DomainNames` against disk — any future
  module needs the same paired change.
- ~~The `BudgetCode` table and its RLS policies~~ — **shipped**: `budgeting.budget_codes` +
  `rm_budget_codes` (migrations `AddBudgetCodes`, then `ExtendBudgetCodes` for US-6.1.1's full
  property set). The free-text `stream` field the first slice carried was replaced by the
  `serviceLine` enum; its values were dropped, not translated, because a guessed service line
  silently misattributes the revenue mix the enum exists to compute.
- ~~A user reference for the budget owner~~ — **shipped**, and it is the platform's first
  cross-module user link: Identity gained its first `IIntegrationEventMapper` and publishes
  `identity.user-changed`; Budgeting consumes it into `budgeting.user_lookup`. Because Identity's
  outbox had always been empty, existing accounts arrive via the
  `BackfillBudgetingUserLookup` migration rather than by replay. **`Identity.User` is no longer
  create-only**: it carries a full name and a job title that its owner edits through
  `GET`/`PUT /api/identity/auth/profile`, and `UserProfileUpdatedDomainEvent` maps to the same
  full-snapshot `identity.user-changed` event, so `budgeting.user_lookup.full_name` follows. The
  replica still never *shrinks* — there is no deactivation or deletion, so a departed person
  stays pickable, and that fix still belongs in Identity. Whoever adds an email change, a role
  change or a deactivation must raise a domain event **and** extend
  `IdentityIntegrationEventMapper`, or every replica goes stale with no error anywhere.
- ~~`BudgetAllocation`~~ — **shipped** (`budgeting.budget_allocations` + `rm_budget_allocations`,
  migration `AddBudgetAllocations`), and with it `AllocationBudgetCodeUsageProbe` replaced
  `NeverReferencedBudgetCodeUsageProbe`, so `DELETE /codes/{id}` now answers 409
  `Budgeting.Code.InUse` for a code any period has ever planned. Still open: the
  `ActualTransaction` table and its RLS policies; QuickBooks actuals reconciliation.
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
- Filtering the codes list — it is small enough to render whole. (~~Hiding retired codes from
  the allocations picker~~ — done: `allocationCandidates` offers only active codes of the
  section's category that are not already planned.)
- CI/CD and the OVHcloud deployment target — there is no `.github/` anywhere in this repo yet;
  that is a platform-wide story covering every app at once.
- OIDC/OpenIddict; the `SuperUser` claim from architecture Section 6.1.
