# Budgeting — Zero-Based Budgeting Console

Next.js 16 app on port **3003**, consuming the shared API. Scaffolded by US-6.0.1 (Track 6,
Stage 6.0). Budget **periods**, **codes**, **allocations** and **actual transactions** are real —
including the period lifecycle, the period dashboard and the plan-against-spend rollup. **Variance
is the last mock screen** — see
[Data](#data-everything-but-variance-is-real).

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
| `components/HeaderClock.tsx`, `components/NavRail.tsx` | same paths | yes — `NavRail`'s optional `groups` / `onHome` props exist for Dispatcher's app launcher and go unused here; `Console.tsx` still passes only the original three |
| `app/layout.tsx` | `Dispatcher/app/layout.tsx` | **no** — title/description only; the four Google Fonts `<link>` tags are byte-identical and must stay that way |
| `lib/auth.ts` | `Dispatcher/lib/auth.ts` | **no** — see below |
| `components/TopBar.tsx`, `AuthGate.tsx`, `LoginScreen.tsx`, `Console.tsx` | same paths | **no** — adapted |
| `lib/nav.ts`, `lib/data.ts`, `lib/money.ts`, `lib/types.ts`, `lib/claims.ts`, `lib/roles.ts`, `lib/api/budgeting.ts`, `lib/api/identity.ts` | — | new |
| `components/Brandmark.tsx`, `ErrorNotice.tsx`, `RoleGate.tsx`, `AccessDeniedScreen.tsx`, `SetupPendingScreen.tsx`, `BudgetPeriodFormModal.tsx`, `BudgetCodeFormModal.tsx`, `BudgetAllocationFormModal.tsx`, `ActualTransactionFormModal.tsx`, `ProfileForm.tsx`, `screens/*`, `screens/periods/*` | — | new |

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
it. Seven files, and only two need a DOM:

- `lib/roles.test.ts` — US-6.0.1's acceptance criterion: a Dispatcher account is rejected.
- `lib/claims.test.ts` — JWT decoding, including the non-ASCII round trip (`atob` yields a binary
  string, so a naive decoder mangles accented emails) and malformed-token handling.
- `components/RoleGate.test.tsx` — the gate applies the rule and offers a way out.
- `lib/api/identity.test.ts` — `normalizeProfileField` against `User.Normalize`,
  `PROFILE_FIELD_MAX_LENGTH` against `User.ProfileFieldMaxLength`, and the
  `profileDisplayName` name-then-email fallback.
- `components/ProfileForm.test.tsx` — the profile form seeds from its props, refuses to save
  when nothing changed (including when the only change is whitespace, matching the server's
  no-op rule), sends `null` for a cleared field, and shows a refused save's message verbatim.
  It takes `onSave` as a prop, so it injects a `vi.fn()` rather than stubbing the transport —
  no client test in this app mocks `request()`.
- `lib/api/budgeting.test.ts` — the client-side mirrors of server rules stay pinned to the
  server: `previewPeriod` against `BudgetPeriod.Create`, `normalizeBudgetCode` /
  `budgetCodeFormatError` against `BudgetCode.NormalizeCode` / `ValidateCode`,
  `parentCandidates` against `BudgetCodeParentRule`, `PERIOD_STATE_ORDER` /
  `nextTransition` / `stateAfter` against `BudgetPeriod.Transition`, `canEditAllocations`
  against `BudgetPeriod.AllowsPlanChanges`, `allocationCandidates` against the `CodeRetired`
  check plus the unique (period, code) index, and `allocationAmountError` /
  `allocationJustificationError` against `BudgetAllocation.Validate`; and, for actuals,
  `periodCovering` / `actualDateError` against `CoveringPeriodRule` + `BudgetPeriod.Covers`,
  `actualCodeCandidates` against the *asymmetric* retired check (refused outright when recording,
  refused only for a move **onto** a retired code when correcting), and `actualAmountError` /
  `actualDescriptionError` against `ActualTransaction.Validate`. `toBudgetRollup` /
  `rollupDeltaPct` mirror no server rule — the rollup ships no band — so what is pinned there is
  the console's own half: the category split, the per-side totals, and both outer-join arms (a
  code with actuals but no plan reports *no baseline*, never an infinite overrun; a code with a
  plan but no actuals reports a plain −100%). Plus the →StatusKind
  mappings (`periodKind` over all five states, `ZBB_BALANCE_KINDS`), the label maps,
  `toBudgetCode`'s `isActive` → `active` rename, `toBudgetPeriod`'s two totals, `coverage`,
  `zbbBalance` / `zbbBalanceDisplay` / `hasPlannedAmounts` (all three arms plus the empty plan)
  and `planningProgress` (the dashboard's checklist and stepper derive from one tested function).
- `lib/money.test.ts` — `formatDeltaCad` / `formatDeltaPct` always write the sign out, so a
  signed figure never rests on colour.

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

## Data: everything but Variance is real

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
two-click confirm. Transitions are **not** gated on plan completeness — finalizing an empty or
unbalanced plan is allowed; the dashboard's checklist, its balance banner and one extra sentence
in the finalize confirmation make that a choice rather than an accident. **Warn, never block** —
see [the ZBB balance rule](#the-zbb-balance-rule-every-planned-dollar-has-a-job).

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

### Actuals are real, by hand — not by QuickBooks

**Actual transactions are entered in this console** (Actuals vs Budget → `+ RECORD TRANSACTION`).
That is worth stating plainly because the old `lib/data.ts` header promised they would arrive
"when the QuickBooks reconciliation slice lands": they did not wait for it. A QBO pull is a later
slice of its own, and until it exists every actual on this platform was typed by a person.

One transaction is *this much, against this code, on this day, for this reason*. Four things about
it are not guessable from the form, and all four are the server's:

- **There is no period on a transaction.** The period is *derived* from `occurredOn` and never
  stored — `CreateBudgetPeriodCommandHandler` refuses overlapping periods, so a date maps to at
  most one period, and "the period's actuals" is a range query that can never disagree with the
  period's own dates. Correcting the date is therefore the only way to move a transaction between
  periods, and it is why the writes are addressed to `/actuals` rather than to a period.
- **A date in no period is refused** (409 `Budgeting.Actual.NoPeriodCoversDate`), not stored
  orphaned — the period view is the only place actuals are listed, so such a row is one the
  product could never show again. The form knows every period, so it names the period a date
  counts in *while the date is being picked* (`periodCovering`) and refuses an uncovered one
  before the round trip; the server still re-checks and its message is shown verbatim.
- **Expenses are positive amounts on expense codes.** The code's category says which side of the
  ledger a figure lands on, so the minus sign has no second job and a negative amount is refused
  (`Budgeting.Actual.AmountNegative`).
- **Actuals are not frozen by the period lifecycle.** A closed period still takes and still
  corrects transactions, deliberately: an invoice dated the 30th lands on the 6th, and a closed
  period's actuals are exactly what a review is reading. Only the *plan* is frozen.

The retired-code rule is **asymmetric**, and the form mirrors both halves (`actualCodeCandidates`):
new spend cannot go on a retired code, but a row already sitting on one stays correctable in place
as long as the edit does not move it to another code — a retired code must never stand between
anyone and fixing a fact.

The actuals routes (`BudgetAccess` group, `BudgetingEndpoints.cs`):

| Route | |
|---|---|
| `GET /api/budgeting/periods/{id}/actuals` | the period's transactions, by date then code (404) |
| `GET /api/budgeting/periods/{id}/rollup` | one row per code: `plannedCad`, `actualCad`, `deltaCad` (404) |
| `POST /api/budgeting/actuals` | `{ budgetCodeId, amountCad, occurredOn, description }` → 201 `{ id }` |
| `PUT /api/budgeting/actuals/{id}` | same body; 204. 404 `NotFound`, 409 `NoPeriodCoversDate` / `CodeRetired` |
| `DELETE /api/budgeting/actuals/{id}` | 204 |

**No period id, no source, no external reference, and no tax field of any kind** on that body. The
first three are the server's to decide (everything entered here is `Manual`, and a caller that
could name its own source could claim a QuickBooks id and defeat the re-import guard); the last is
the platform rule — the platform never computes tax, and `amountCad` is the amount exactly as
recorded.

The rollup is the whole of Actuals vs Budget. It includes **both sides of the outer join** — a
code planned but not spent against, and a code spent against but never planned — and that second
row is what a planner opens the screen to find. It ships **no variance band and no percentage**:
those thresholds are the console's (`varianceKind` / `varianceLabel`), and a server that shipped
its own would put two answers to one question in the product. `toBudgetRollup` groups the rows by
category the way the period dashboard groups its lines and **never adds the two sides together** —
a "total planned" spanning revenue and expense would be money in and money out in one figure. The
only place the two totals meet is the ZBB balance banner, as a difference.

Writes follow the same eventual-consistency discipline as allocations: the modal refetches the
**target** period's transactions until the new values are visible — the target, because a
corrected date may have moved the row out of the period on screen — and only then does the screen
refresh the rollup, whose totals read from the same projection.

### A period is a full view, not a pane

`screens/BudgetPeriods.tsx` is one screen with two modes, chosen by `openPeriodId`:

- **list mode** — `screens/periods/PeriodList.tsx` at full width, one row per period carrying the
  label, the dates, the state chip, both planned totals and the ZBB balance. The row is the way
  in; clicking it opens the period.
- **period mode** — the whole content area is `screens/periods/PeriodDashboard.tsx`, with
  `← ALL PERIODS` in the header's right slot. `key={id}` still remounts it per period, so every
  confirm, modal and fetch resets when a different period is opened.

This replaced a `38% / 1fr` master/detail grid. A period is not a preview beside a list — it is
where all the planning happens, and at 38% the list was charging the dashboard for width it
needed. It is a take-over **inside the console**, not a browser window: neither console has URL
routing, so a literal new tab would mean introducing routing here *and* in Dispatcher's shell.
The dashboard's old right-pane treatment (tinted `detailBg`, left-only padding) became a bordered
surface with balanced padding; the tokens are the same, the geometry is not.

`openPeriodId` is hoisted into `Console.tsx` beside `periodId` for the reason Console's header
comment already gives about selection state: stepping over to Budget Codes and back must not
throw you out of the period you were working in. The two are distinct — `periodId` is what the
other screens read and going back to the list never clears it.

**There is no standalone Allocations screen.** It showed the same real lines flat, with its own
period picker and its own client-summed tiles, and it could not refresh Console's period list
after a save — two places doing one thing, which is the drift risk it was always going to
become. Deleted, along with `ScreenId`'s `"allocations"`, its `NAV_GROUPS` entry (PLANNING is now
two items) and TopBar's `+ NEW ALLOCATION` pill: allocations stopped being a place you navigate
to, so a global shortcut had no destination. A line is added inside a period, where its category
and the balance it moves are both already on screen. `BudgetAllocationFormModal.tsx` stays and is
now the period view's alone.

`screens/periods/*` is the rest of the dashboard (lifecycle stepper, ZBB balance banner, planning
checklist, one `AllocationSection` per category). The dashboard shows the server's own totals.

### The ZBB balance rule: every planned dollar has a job

There is no top-down budget amount anywhere in this app, and there never was —
`BudgetPeriodFormModal` asks only for granularity, year and ordinal. **The pool is the period's
planned revenue lines**, so the plan balances when `plannedExpense === plannedRevenue`. Both
totals are the server's sums; `zbbBalance(period)` in `lib/api/budgeting.ts` only judges them,
returning `{ amount, state }` over `"balanced" | "unallocated" | "over"`.

The arm that matters is **`unallocated`**. Its predecessor (`netCad` / `netKind` / `netLabel`)
read a surplus as good news — `"ontime"`, the healthy kind. Under zero-based a surplus is revenue
with no job, which is the exact failure the rule exists to catch, so it carries `"soon"` instead;
`over` keeps `"over"` and `balanced` gets `"ontime"` (`ZBB_BALANCE_KINDS`). If you ever find
yourself reinstating a "surplus is good" reading, that is the rule being undone.

Two things the banner has to get right:

- **Colour is never alone**, as everywhere else: the chip's glyph, the headline in words
  (`Balanced` / `Unallocated $X` / `Over budget by $X`) and the signed total through
  `formatDeltaCad` are three channels before the colour is the fourth.
- **An empty plan is not a balanced one.** Zero equals zero is arithmetically balanced and the
  wrong thing to tell a planner staring at nothing, so `zbbBalanceDisplay` takes a `hasLines` flag
  and reads **"Nothing planned yet"** on the neutral kind. The dashboard answers it from the lines
  it fetched; the period list, which has only the totals, answers from `hasPlannedAmounts`.

`planningProgress` carries it as a third checklist row, "Every dollar assigned", done only when
the state is `balanced` *and* there is at least one line. It is its own `BalanceStep` kind in the
`ChecklistStep` union rather than a bent `LineStep`, because it is about the relationship between
both totals rather than one category's lines. `PlanningChecklist.tsx` renders whatever
`planningProgress` returns and re-derives nothing — each step's whole supporting sentence is
written in `planningProgress`, so a fourth row is a change to one function, not to two files that
must agree.

**Finalize stays allowed when the plan does not balance.** That is deliberate and matches the
existing choice that transitions are not gated on plan completeness: the console makes an
unbalanced plan loud (banner, checklist row, and a sentence appended to the finalize confirmation
naming the figure), and then lets the Owner sign it off. No client-side gate, and no backend rule
— Part A of the ZBB restructure plan touches no backend code at all. Reversing this to "refuse"
later would be a backend change (a new `BudgetPeriodErrors.PlanNotBalanced` in the finalize
handler, which can see the allocation totals the aggregate cannot), and nothing written here
would have to be undone first.

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
- **Retiring is the normal end of a code's life, but it is no longer unconditional.** A retired
  code stays listed so last period's rows keep resolving. `DELETE` exists only for a code created
  in error that nothing has ever referenced; `IBudgetCodeUsageProbe` turns it into a 409
  `Budgeting.Code.InUse` the moment that stops being true — an allocation line **or** an actual
  transaction counts — and the server's message names retirement as the alternative. The UI puts
  it behind a two-click confirm.
  **`deactivate` now also answers 409 `Budgeting.Code.PeriodNotClosed`** while any period using
  the code is still open: a code can only be retired once every period referencing it — again, by
  a line *or* a transaction — is `Closed`. Retiring a code out from under a live plan would leave
  a planner holding lines the server then refuses every edit to. The message names the blocking
  periods by label (capped at three, then "and N more"), so show it verbatim — it is the only
  thing telling the user which period to close. Two consequences worth knowing before filing a
  bug: a code used by a **Draft or Open** period can be retired once its line is removed, which
  the user can do because the plan is still editable; a code used by a **Finalized or In-review**
  period cannot be retired until that period closes, because its plan is frozen — and that is the
  point, not a deadlock. `activate` is unaffected.
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

`lib/data.ts` holds the not-yet-real remainder, and it is down to **one array**: `variance`. The
`actuals` array is gone — actuals are real now — and `variance` no longer derives from it, because
deriving a mock from a mock that had been deleted was the only thing standing between this slice
and a broken build. It carries its own literal planned/actual pairs and fills `delta`, `deltaPct`
and `vk` from one function, so a row's colour can never disagree with its own numbers. Its rows
stay keyed to mock period ids no real period will match, so a real period shows the empty state
rather than fake figures, and `Variance.tsx` and `Reports.tsx` keep their `MockTag`. (Reports'
revenue-mix card read `actuals` too; it reads the same pairs off `variance` now, and goes real
when it is rebuilt on the rollup endpoint — which already ships the triple it needs.)

Its `budgetCodes` array survives **only** as the name-and-category lookup those two screens need —
the Budget Codes screen no longer reads it, and the array is typed
`Pick<BudgetCode, "id" | "code" | "name" | "category">` so it does not have to grow a
plausible-looking value for every field the real entity gains. Conventions follow
`Dispatcher/lib/data.ts`: flat exported const arrays, string ids, ISO date strings (never `Date`
objects), whole-dollar numbers rendered through `formatCad`, and a `StatusKind` carried on every
status-bearing row so rendering is a pure lookup.

No screen invents an API shape. The last array is replaced by additions to
`lib/api/budgeting.ts` when the variance slice lands, and the screens keep their props.

**`varianceKind` / `varianceLabel` are not mock**, and they stay in `lib/data.ts` on purpose: they
are the console's variance bands, and Actuals vs Budget — on real data since this slice — reads
them from there so the two screens and any future report can never disagree about what "on plan"
means. That is the one sanctioned import of the mock module from a real screen, and it ends when
the last mock array does. The signed formatters (`formatDeltaCad` / `formatDeltaPct`) live in
`lib/money.ts`, **not** in `lib/data.ts`, so printing a signed figure never requires reaching into
that module at all.

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

`components/screens/Variance.tsx` and `components/screens/ActualsVsBudget.tsx` are the files to
re-check hardest after any edit: both are full of signed figures, and a coloured delta with no
sign and no glyph is the exact failure this rule exists to prevent. Actuals vs Budget calls
`statusMeta` nowhere — every band it shows goes through `StatusChip`, which pairs the glyph with
the band's words — and every delta prints through `formatDeltaCad`.

Known hazards documented in `theme.ts`: `colors.amber` is a fill/border/icon colour only — use
`colors.amberText` when amber must be text; `colors.textFaint` is decorative only.

### Audit record

| Date | Check | Result |
|---|---|---|
| 2026-08-04 | Code audit (both greps above) | **Pass** — every call site pairs colour with glyph + label; protected hexes appear only in `theme.ts` and in copied decorative elements that carry adjacent text |
| 2026-09-18 | Code audit (both greps above), after wiring Actuals vs Budget to the rollup | **Pass** — no new `statusMeta` call sites and no new protected hexes; the new screen and modal carry every band as a `StatusChip` and every delta through `formatDeltaCad` |
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
  `Budgeting.Code.InUse` for a code any period has ever planned.
- ~~The `ActualTransaction` table and its RLS policies~~ — **shipped**
  (`budgeting.actual_transactions` + `rm_actual_transactions`, migration `AddActualTransactions`),
  with the five routes above and `BudgetCodeRetirementRule` alongside them. Still open:
  QuickBooks actuals reconciliation, and rebuilding Variance on the rollup endpoint.
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
