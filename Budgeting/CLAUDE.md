# Budgeting — Zero-Based Budgeting Console

Next.js 16 app on port **3003**, consuming the shared API. Scaffolded by US-6.0.1 (Track 6,
Stage 6.0). Budget **periods**, **codes**, **budget items** and the **vendor register** are real —
including the period lifecycle and the period dashboard; actuals and variance are still mock — see
[Data](#data-periods-codes-and-budget-items-are-real-actuals-and-variance-are-still-mock).

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
| `lib/nav.ts`, `lib/vendors.ts`, `lib/data.ts`, `lib/money.ts`, `lib/types.ts`, `lib/claims.ts`, `lib/roles.ts`, `lib/workingPeriod.ts`, `lib/periodHold.ts`, `lib/api/budgeting.ts`, `lib/api/identity.ts` | — | new |
| `components/Brandmark.tsx`, `ErrorNotice.tsx`, `RoleGate.tsx`, `AccessDeniedScreen.tsx`, `SetupPendingScreen.tsx`, `PeriodBanner.tsx`, `BudgetPeriodFormModal.tsx`, `BudgetCodeFormModal.tsx`, `BudgetItemFormModal.tsx`, `VendorFormModal.tsx`, `ProfileForm.tsx`, `screens/*`, `screens/vendors/*`, `screens/periods/*` (incl. `PeriodChooser.tsx`, `PriorityBreakdown.tsx`), `screens/codes/*` (`CopyCodesPanel.tsx`) | — | new |
| `lib/costCentres.ts`, `components/CostCentreFormModal.tsx`, `screens/CostCentres.tsx`, `screens/periods/CostCentreBreakdown.tsx` | — | new (cost-centre register) |

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
run time. Twenty-one files; ten need a DOM (the nine component tests, plus `workingPeriod`'s
storage tests, which need `sessionStorage`):

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
  `nextTransition` / `stateAfter` against `BudgetPeriod.Transition`, `canEditPlan` against
  `BudgetPeriod.AllowsPlanChanges` (the one rule for items **and** codes; `canEditAllocations`
  is pinned to agree with it in every state), `allocationCandidates` against the `CodeRetired`
  check alone (a code that already has items **is** offered — many items per code),
  `budgetItemError` against the create/update handlers' `CodeRequired` and then
  `BudgetAllocation.Validate` **rule for rule, in the server's order, with the server's messages
  verbatim** (`BUDGET_ITEM_MESSAGES`), each length/count/ceiling boundary tested on both sides,
  `roundCad` / `computeItemAmount` / `itemAmount` against `BudgetAllocation.Round` and Parse's
  cost block (half away from zero; `1.005 → 1.01`, `3 × 1.005 → 3.03`, quantity `0.005 → 0.01`
  and `0.004` refused as not positive — never `Math.round(x * 100)`, which gets `1.005` wrong),
  `normalizeTags` against the tag loop (trim, case-insensitive de-dup, counted after de-dup),
  `draftToBudgetItemInput` / `recordToDraft` / `itemReflects` (the post-save refetch
  predicate), `groupItemsByCode`, `priorityBreakdown`, `costBuildUpLabel`, the item label maps
  and `PRIORITY_KINDS` / `PRIORITY_GLYPHS` (Must and Should share a colour, so each has its own
  glyph), `needsJustification` /
  `unjustifiedLines` against `BudgetAllocation.NeedsJustification` + `CopyInto`, and
  `copySourceCandidates` / `defaultCopySource` against
  `CopyBudgetAllocationsCommandHandler`'s `CopySourceIsTarget` guard — including the case that a
  **Closed period is offered**, because the server checks editability on the target only — and
  the same two helpers against `CopyBudgetCodesCommandHandler`, whose guards are identical, so
  the Copy codes panel reuses them rather than growing a second rule; `codeCopyOutcomeSummary`
  names every bucket of `BudgetCodeCopyResponse` (singular/plural, zero clauses omitted, an
  empty source as a success, a second copy as "Nothing was copied"). Plus
  the →StatusKind mappings (`periodKind` over all five states, `assignmentState` /
  `ASSIGNMENT_KINDS` over all four), `planBalanced` (an empty plan is **not** balanced),
  `copyOutcomeSummary`, the label maps, `toBudgetCode`'s `isActive` → `active` rename,
  `toBudgetPeriod`'s two totals, `coverage` (codes with at least one item — a code with five
  items counts once) and `planningProgress` (the dashboard's zero-based checklist and stepper
  derive from one tested function; a test pins that no row says a budget is "set").

  `netCad` / `netKind` / `netLabel` and their tests were **deleted**, not adapted, when the Net
  tile became "Left to assign": `netKind(0)` was `info` and `balanced` is now `ontime`, because
  under zero-based budgeting $0 is the goal rather than the neutral case. Adapting an assertion
  through a semantic inversion hides the inversion.
- `lib/api/transport.test.ts` — the 401 refresh-and-retry path, with `fetch` and `lib/auth`
  mocked. `request<T>`'s doc comment promises it "never loops"; these assert the exact attempt
  count (two fetches, one refresh) so the promise is enforced rather than stated. Plus `ApiError`
  construction — the parsed `{ code, message }` body, the `Http.<status>` fallback, and the
  `Network.Unreachable`/status-0 branch that `Console.tsx` and `screens/BudgetCodes.tsx` both
  branch on via `e instanceof ApiError`.
- `lib/api/budgeting.requests.test.ts` — what each request function puts on the wire. Every
  code route is pinned **under its period** (`/api/budgeting/periods/{id}/codes...`, body shapes
  unchanged and never carrying a `periodId`), plus a sweep asserting no code request is built the
  removed tenant-wide way — `codes/owners` is the one tenant-wide codes route left. Each code
  write's 409 `Budgeting.Code.PeriodNotEditable`, the per-period `DuplicateCode` /
  `ParentNotFound` / `NotFound` messages, and `copyBudgetCodes` (route, `{ sourcePeriodId }`
  object body, target-in-route / source-in-body, the four counts, and its five refusals verbatim
  in guard order) are pinned too. Chiefly
  `setBudgetCodeActive`, whose route is built from a boolean (`activate` / `deactivate`): an
  inverted ternary there returns 204 either way and silently activates a code the planner asked
  to retire. Likewise `copyBudgetAllocations`, which takes the source as an **object**
  (`{ sourcePeriodId }`) rather than a bare second string: two same-typed guids swap silently and
  copy backwards with a 200 on the wire and no error on either side. It also pins the route, the
  four counts and each 400/409/404 message verbatim. Also pins that `deleteBudgetCode`'s 409
  message reaches the caller verbatim, since the server's wording is what names retirement as the
  alternative. And the budget-item routes: `createBudgetItem` POSTs the whole
  `BudgetItemRequest` (every key, explicit nulls, enums as PascalCase strings) and returns the
  201's id; `updateBudgetItem` / `removeBudgetItem` address the **item's own id**
  (`.../allocations/{allocationId}`, 204), never a code id; update refusals surface verbatim.
- `components/BudgetItemFormModal.test.tsx` — the modal takes its three requests as an `api`
  prop, so it injects `vi.fn()`s: the cost-mode toggle computes the total live (`12 × 450`,
  `3 × 1.005 → $3.03`), the exact body a built-up item POSTs (`amountCad: null`), an edit PUTs to
  the item's id and may move it to another code, an item on a retired code opens with no code
  chosen, both a client-side and a server-side refusal show the server's words, and a period with
  no active code of the category gets a note naming the period and OPEN BUDGET CODES instead of
  an empty picker.
- `components/screens/codes/CopyCodesPanel.test.tsx` — `vi.fn()` props: last period (a Closed
  one) is pre-selected, the first click only asks to confirm, the confirm note names both
  periods, the second click copies from the chosen source and shows the outcome summary, a
  refused copy shows no outcome, and a lone period gets "nothing to copy from".
- `lib/money.test.ts` — `formatDeltaCad` / `formatDeltaPct` always write the sign out, so a
  signed figure never rests on colour; `formatCadPrecise` prints cents only when there are cents
  (the copied `formatCad` is whole-dollar and would print a `$3.03` item as `$3`).
- `lib/workingPeriod.test.ts` — the "enter a period" contract, which mirrors no server rule (the
  backend scopes by route id and has no idea which period a tab is in): `suggestedPeriodId`
  (today strictly inside, on the start and on the end date, the latest-start fallback in any
  list order, empty → `null`), `resolveEnteredPeriod` (found, nothing entered, **lost** after a
  good load, **not** lost after a failed load or while loading), the storage key differing by
  tenant and by user and `null` without claims, the `sessionStorage` round trip, `null` removing
  the entry, a throwing store degrading quietly, **never touching `localStorage`**, and
  `isPeriodScoped` being false for `settings` and `costCentres` only (Budget Codes became scoped
  when codes moved under the period; the cost-centre register is tenant-wide).
- `components/screens/periods/PeriodChooser.test.tsx` — `vi.fn()` props, as `ProfileForm` does:
  a row click enters that row's id, each row writes its state out, the suggested row carries
  its tag and focus, the eyebrow names the destination screen, the empty state's create button,
  a load error shown verbatim with RETRY, the lost notice, and "Returning to your period…".
- `components/PeriodBanner.test.tsx` — label, dates, state and editability; while held, SWITCH
  PERIOD is `aria-disabled`, does not call `onSwitch`, and the reason is written beside it; the
  "isn't tied to a period" sentence on Settings, with no "every period" wording left anywhere;
  CHOOSE A PERIOD with nothing entered.

- `lib/costCentres.test.ts` — every `lib/costCentres.ts` mirror against the C# it names: the
  limits and every `CostCentreErrors` message, trim-only + case-sensitive matching,
  `costCentreError` in the create and the (different) update order, `parentCandidates` against
  `CostCentreParentRule` (self, top-level, retired-current-parent kept, children → none),
  `budgetCodeCostCentreError` against `BudgetCodeCostCentreRule` (unchanged always accepted), that
  `costCentreOptions` never offers a value the server would refuse, and `groupRollup` /
  `rollupSum`.
- `lib/api/costCentres.requests.test.ts` — every cost-centre route/method/body (PUT never carries
  `code`; activate/deactivate both directions; `includeInactive` written out), each of the 17
  `Budgeting.CostCentre.*` refusals and the two new `Budgeting.Code.CostCentre*` refusals
  verbatim, and the rollup route. A separate file from `budgeting.requests.test.ts` on purpose.
- `components/BudgetCodeFormModal.test.tsx` — the cost-centre picker: active entries only, a
  retired / unregistered current value kept and marked and saved unchanged, Revenue hides it and
  sends `null`, inline create selects the new entry (case kept), an inline refusal verbatim.
- `components/screens/periods/CostCentreBreakdown.test.tsx` — grouping under a parent, an absent
  parent named, retired / unregistered chips, owners, "No cost centre" at $0, the server's total,
  and no actual column.

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

## Working in a period

The planner works **inside one period at a time**. Every period action — a transition, a budget item,
a copy, a report — is under the period the banner names, and nothing changes that period
silently.

- **Enter, then switch.** A period-scoped screen — Period Dashboard, Budget Codes, Actuals vs
  Budget, Variance, Reports (`PERIOD_SCOPED` / `isPeriodScoped` in `lib/nav.ts`; Settings and
  the two tenant-wide registers, Vendors and Cost Centres, are the only screens outside it) — shows
  `screens/periods/PeriodChooser.tsx` until a period is entered. Once one is, a strip at the top
  of the main column (`components/PeriodBanner.tsx`) always shows WORKING IN, the label, dates,
  the state chip and "Plan editable / read-only"; leaving takes its explicit **SWITCH PERIOD**.
  Exactly two handlers in `Console.tsx` change the entered period, `enterPeriod` and
  `switchPeriod`, and no screen carries a period picker of its own (the old `PeriodPicker` was
  deleted — picking on Variance used to change which dashboard the Periods screen opened).
  The banner lives in the main column, not the TopBar, whose geometry matches Dispatcher's.
- **Scoped screens remount on every switch.** Console renders them inside a `Fragment` keyed by
  the entered period's id, so no confirm, modal or fetch outlives the period it was for.
- **The entered period is derived, not stored.** Console keeps only `enteredId`;
  `resolveEnteredPeriod` (`lib/workingPeriod.ts`) finds it in the loaded list, so a list refresh
  picks up its new totals and state for free. `applyLoaded` never touches the selection.
- **Remembered per tab, per user: `sessionStorage`, key
  `nl.budgeting.enteredPeriod.{tenantId}.{sub}`** (from `getClaims()`). It survives a reload;
  it dies with the tab, so a shared machine never reopens someone else's period; two tabs can
  work in two periods; and the user in the key covers a sign-out and sign-in in the same tab.
  **Never `localStorage`** — a test pins that. Every access is try/catch'd, and nothing is
  stored without claims.
- **No silent auto-enter.** A first visit shows the chooser with the period containing today —
  else the latest start — highlighted, tagged `INCLUDES TODAY` / `LATEST`, and focused, so
  entering it is one click (`suggestedPeriodId`, formerly Console's `defaultPeriodId`).
- **Lost is not the same as unloaded.** A stored id that matches nothing after a *good* load is
  `lost`, and the chooser says "The period you were working in is no longer available — choose
  another." A failed or pending load is never lost; while a stored period loads the chooser
  reads "Returning to your period…".
- **The hold guard** (`lib/periodHold.ts`). `PeriodDashboard` (`busy`: transition, remove, copy),
  `BudgetItemFormModal` (its save), `screens/BudgetCodes.tsx` (`busy`: retire, restore, delete,
  starter set, copy codes) and `BudgetCodeFormModal` (its save) call `usePeriodHold(busy)`. While any hold is taken,
  SWITCH PERIOD and the TopBar's + NEW PERIOD refuse (`newPeriodDisabled`), and the banner writes
  "Finishing a change to {label}…" beside the disabled button. Both modals also ignore
  ✕ and CANCEL while saving. **Rail navigation is deliberately not blocked** — changing screen
  never changes the period, and blocking it would mean editing the copied `NavRail`.
- **Creating a period enters it.** The New Period modal lives in `Console.tsx` (opened by the
  TopBar pill and the chooser); `handlePeriodCreated` enters the new id and lands on the Period
  Dashboard, where the plan can be seeded from an earlier period.
- **Every action names its period.** The transition confirm ("Click CONFIRM FINALIZE to move
  Q3 2026 from Draft to Finalized"), the remove confirm ("Removes “{title}” ({code}) from {label}'s plan.
  Other items and other periods are not affected."), the item modal's eyebrow and note, and the copy panel ("Into:
  {label}", `COPY INTO {LABEL}`). On Budget Codes: the eyebrow ("Planning · {label}"), the code
  modal's eyebrow, the retire confirm ("Retiring FUEL in Q3 2026 changes Q3 2026's chart only —
  other periods keep their own FUEL…"), the delete confirm, and the Copy codes panel ("Into:
  {label}", `COPY CODES INTO {LABEL}`, a confirm naming both periods). `nextTransition`'s own
  button labels are unchanged — tests pin them.
- **Budget codes belong to the period** (they used to be tenant-wide; the owner reversed that).
  Budget Codes is scoped like the dashboard, shows the entered period's chart, and is read-only
  outside Draft/Open. RETIRE and DELETE are both two-click. Only Settings, Vendors and Cost Centres render
  with or without an entered period; there the banner says "This screen isn't tied to a period." or
  offers CHOOSE A PERIOD.

## Data: periods, codes and budget items are real; actuals and variance are still mock

**Budget periods come from the real API** (`GET/POST /api/budgeting/periods` via
`lib/api/budgeting.ts`; `Console.tsx` owns the fetch and threads the list down as props) — the
first Stage 6.1 slice. Each record now carries `plannedRevenueCad` / `plannedExpenseCad`, the
server's sums of the period's budget items by code category; `toBudgetPeriod` maps them to
`plannedRevenue` / `plannedExpense` and derives `pk` from `state` (`periodKind`).

**The period lifecycle is real** — five states, forward only, in this order:
**Draft → Finalized → Open → In review → Closed** (`PeriodState`; the C# enum spells the fourth
`InReview`). Budget items can change **only while the period is Draft or Open** — finalizing
signs the plan off, opening re-allows in-period adjustments, review and close freeze it
(`BudgetPeriod.AllowsPlanChanges`; mirrored by `canEditPlan`, which `canEditAllocations`
reads). The same rule freezes the period's **chart of budget codes**. Each state has exactly one
way forward (`nextTransition`) and the dashboard offers exactly that one button, behind a
two-click confirm. Transitions are **not** gated on plan completeness — finalizing an empty plan
is allowed; the dashboard's checklist makes an empty plan visible instead.

**Budget items are real — and a code's budget is the sum of its items.** There is no fixed,
"set" figure per code any more: a period holds **any number of items per code** (the old unique
(period, code) index and the upsert-by-code PUT are gone), each addressed by its own id. The
backend keeps the aggregate name `BudgetAllocation`, the `allocations` route segment and the
`BudgetAllocationRecord` wire type; the console calls them **budget items**. Each item is a small
zero-based decision package:

- **What** — the budget code, a `title` (required, ≤ 120), `vendor`, `tags` (≤ 10, each 1–32
  after trimming, de-duplicated case-insensitively).
- **Cost** — a lump sum (`amountCad`), **or** `quantity` × `unitCostCad` with an optional
  `unit`. Built up, the **server computes the amount** — each factor rounded to 2 dp, then the
  product, all half away from zero (`BudgetAllocation.Round`) — and ignores any amount sent, so
  the modal sends `amountCad: null`. `computeItemAmount` mirrors it in exact integer arithmetic.
  Amounts are `decimal(12,2)` and may carry cents (the whole-dollar rule this app used to impose
  is gone — a built-up item lands on cents); item figures render through `formatCadPrecise`.
- **Why** — the `justification` (**required**: zero-based, every item is argued from nothing,
  each period), `priority` (`MustHave` / `ShouldHave` / `NiceToHave` — what gets cut first),
  `assumptions`, `consequenceIfUnfunded`.
- **Classification** — `spendType` (`Operating` / `Capital`), `recurrence` (`OneTime` /
  `Recurring`). No tax field and no tax arithmetic — QuickBooks owns tax.

Items follow the period lifecycle only; there is **no per-item approval**. The item's `category`,
`name` and `serviceLine` are resolved from the code at read time, never snapshotted, so
re-classifying a code moves its items between the two totals retroactively. An item on a retired
code stays (and still counts), but `CodeRetired` (409) is checked on **every** update, even when
the code is unchanged — it can only be moved to an active code (the code is editable in the
modal) or removed.

On the dashboard each `AllocationSection` groups its items by code (`groupItemsByCode`): a code
header (tag, name, retired chip, subtotal, item count, `+ ITEM`) with its items beneath, and a
section-level `+ ADD BUDGET ITEM`. A "By priority" panel totals the expense items per priority
(`priorityBreakdown`). Those subtotals and buckets are summed **client-side** from the loaded
items; the headline tiles stay the server's own period totals.

The period and budget-item routes (`BudgetAccess` group, `BudgetingEndpoints.cs`):

| Route | |
|---|---|
| `GET /api/budgeting/periods` | ordered by `startsOn`; each row carries the two planned totals |
| `GET /api/budgeting/periods/{id}` | one period (404) — the `POST` 201 `Location` target |
| `POST /api/budgeting/periods/{id}/finalize` | Draft → Finalized; 409 `Budgeting.Period.NotDraft` |
| `POST /api/budgeting/periods/{id}/open` | Finalized → Open; 409 `NotFinalized` |
| `POST /api/budgeting/periods/{id}/begin-review` | Open → In review; 409 `NotOpen` |
| `POST /api/budgeting/periods/{id}/close` | In review → Closed; 409 `NotInReview` |
| `GET /api/budgeting/periods/{id}/allocations` | the period's items, ordered by code, then priority (MustHave first), then `createdAtUtc`, then id (404) |
| `POST /api/budgeting/periods/{id}/allocations` | create an item — body `BudgetItemRequest` → 201 `{ id }`; 400 validation (`CodeRequired`, `TitleRequired`, `QuantityWithoutUnitCost`, … — see `BUDGET_ITEM_MESSAGES`), 404 period/code, 409 `PeriodNotEditable` / `CodeRetired` |
| `PUT /api/budgeting/periods/{id}/allocations/{allocationId}` | rewrite the item (its code may change) → 204; 400 validation, 404 `Budgeting.Allocation.NotFound`, 409 `PeriodNotEditable` / `CodeRetired` (checked on every update) |
| `DELETE /api/budgeting/periods/{id}/allocations/{allocationId}` | 204; 404 `NotFound`; 409 `PeriodNotEditable` |
| `POST /api/budgeting/periods/{id}/allocations/copy` | seed this period's plan from an earlier one — body `{ sourcePeriodId }` → 200 `{ copied, skippedAlreadyPlanned, skippedRetiredCode, sourceLineCount }`, counted **per item** (the first three always sum to the fourth); each source item lands on **this period's code with the same code string** — `skippedRetiredCode` counts items with no *active* code of that string here (retired, or never copied over; the field name predates per-period codes); a source item is skipped as "already planned" when that code already has any item here, so a second copy copies nothing; 400 `CopySourceRequired` / `CopySourceIsTarget`, 404 `CopySourceNotFound` (the **source**) or `Budgeting.Period.NotFound` (the **target**), 409 `PeriodNotEditable` |

`BudgetItemRequest` is `{ budgetCodeId, title, amountCad, quantity, unitCostCad, unit,
justification, spendType, recurrence, vendor, tags, priority, assumptions,
consequenceIfUnfunded }`, enums as PascalCase strings. The server accepts nulls for most of it
(defaults Operating / OneTime / ShouldHave); this app always sends every key, explicitly.

**The copy brings every field but the justification.** `BudgetAllocation.CopyInto` carries the
title, amount, build-up, classification, vendor, tags, priority, assumptions and consequence, and
sets `Justification = string.Empty`, so a copied item arrives `NeedsJustification` and `PUT
.../allocations/{allocationId}` keeps refusing it with 400 `JustificationRequired` until somebody
argues it. That is the feature, not a gap: zero-based means last period's reasoning is not this
period's. The dashboard shows those items with a "Needs justification" chip, counts them in the
checklist's "Every item argued" row, and names them in the finalize warning.

**Editability is checked on the target only.** A Closed period is a perfectly legal *source* —
copying a closed plan into a fresh Draft is the whole point — so `copySourceCandidates` offers
periods in every state and excludes only the target itself. Do not "fix" it with
`canEditAllocations`; the asymmetry is deliberate and documented on
`CopyBudgetAllocationsCommandHandler`.

Every 400/409 message is shown verbatim — the server's text names the rule. Reads are
projections: after a transition the dashboard refetches the period until it reports the expected
state (`stateAfter`); after an item is saved it refetches the items until **that item's id**
carries the new **values** (`itemReflects` — on edit the row was always there), after a remove
until the id is gone, and only then refreshes the period list, whose totals read from the same
projection.

`screens/BudgetPeriods.tsx` is the **Period Dashboard** screen (rail label; id `periods` and
code `BP` unchanged): a `Screen` around `screens/periods/PeriodDashboard.tsx` for the entered
period only — no list, no picker, no load states of its own (those live on the chooser, see
[Working in a period](#working-in-a-period)). `screens/periods/*` is the chooser and the
dashboard (lifecycle stepper, zero-based checklist, the by-priority breakdown, the
copy-from-an-earlier-period panel, one `AllocationSection` per category), and the dashboard is
the **one** place a period is planned. `BudgetItemFormModal.tsx` opens from there. The
dashboard's headline tiles show the server's own totals, never sums re-derived from the items on
screen.

There is no separate Allocations screen and no `"allocations"` `ScreenId`: a second place to do
the same job could not refresh Console's period list after a save, so its tiles trailed the
dashboard's. The TopBar pill is `+ NEW PERIOD` — the console's one global create action, with
the only create target that is unambiguous from any screen. The New Period modal itself lives
in `Console.tsx` (the pill and the chooser both open it), and creating a period enters it.

**Budget codes are real too — and each period has its own chart.** The second slice, widened
to US-6.1.1's full property set, then moved under the period at the owner's request ("budget
codes should be a part of periods, with a way to copy codes to another period"). This reverses
the earlier "one tenant-wide chart" decision and matches architecture §5.3 (codes are
re-justified from zero each period rather than carried forward by default). The same code string
— `FUEL` — exists once **per period**, as its own row with its own id; uniqueness is (tenant,
period, code). The **code string is the cross-period identity**: the items copy maps by it, and
reports and future QuickBooks actuals will join on it.

| Route | |
|---|---|
| `GET /api/budgeting/periods/{id}/codes` | that period's chart, retired codes included, ordered by code; 404 `Budgeting.Period.NotFound`; allowed in every state |
| `POST /api/budgeting/periods/{id}/codes` | 201 `{ id }` |
| `PUT /api/budgeting/periods/{id}/codes/{codeId}` | 204; no `code` in the body — see below |
| `POST /api/budgeting/periods/{id}/codes/{codeId}/activate\|deactivate` | 204 |
| `DELETE /api/budgeting/periods/{id}/codes/{codeId}` | narrow; 409 when the code has children, or `Budgeting.Code.InUse` when this period has items on it |
| `POST /api/budgeting/periods/{id}/codes/starter-set` | idempotent per period; 200 `{ created }` |
| `POST /api/budgeting/periods/{id}/codes/copy` | body `{ sourcePeriodId }` → 200 `{ copied, skippedExisting, skippedRetired, sourceCodeCount }` (the first three sum to the fourth); 400 `CopySourceRequired` / `CopySourceIsTarget`, then the target's 404 `Budgeting.Period.NotFound` / 409 `PeriodNotEditable`, then 404 `CopySourceNotFound` (the **source**) |
| `GET /api/budgeting/codes/owners` | the owner picker's options, from the user replica — **tenant-wide**, since it lists people, not codes |

The old tenant-wide `codes*` routes are **gone** server-side (only `codes/owners` stays), and a
test sweeps every code request to prove none is built that way. Body and response shapes did
not change: the period is always the route's, `BudgetCodeResponse` carries no `periodId`.

**The chart follows the period lifecycle.** Every code write answers 404
`Budgeting.Period.NotFound`, then 409 `Budgeting.Code.PeriodNotEditable` ("A period's budget
codes can only change while it is Draft or Open.") — the same `AllowsPlanChanges` rule as items,
mirrored by `canEditPlan`. Outside Draft/Open the Budget Codes screen hides create, edit, retire,
restore, delete, the starter set and the copy, and one note names the period and its state.

**Copying codes** (`screens/codes/CopyCodesPanel.tsx`, on the Budget Codes screen, mirroring the
dashboard's `CopyFromPeriodPanel`): every **active** source code whose string the target does
not already have (active or retired) is copied as an active code with a new id and every
descriptive field; the hierarchy comes too — a copied child rolls up into the target's code with
its parent's string, or sits top-level when there is none. Retired codes are not copied, so a
code both retired and existing counts as retired. A second copy adds nothing. The source picker
offers every other period in **any** state (a Closed chart is a fine starting point — the server
checks editability on the target only), defaulting to the latest period starting before this
one; it reuses `copySourceCandidates` / `defaultCopySource`, whose tests pin that the codes copy
has the same guards. Two-click confirm naming both periods; `codeCopyOutcomeSummary` reports the
four counts; the chart is refetched until `copied` more rows are visible (skipped when 0).

**An empty chart is the normal start of a new period.** The screen says "No budget codes in
{label} yet" and offers three ways out: copy from an earlier period, load the starter set, or
`+ NEW CODE`. On the dashboard, a category with no active code in the period shows a note and
OPEN BUDGET CODES instead of `+ ADD BUDGET ITEM` (Console's `openCode(null)` path), and the item
modal says the same instead of rendering an empty picker. The item copy notes that items land on
the target's code **by string**, so codes should be copied first.

Unlike periods, codes are **not** hoisted into `Console.tsx`: `screens/BudgetCodes.tsx` and the
dashboard each fetch the **entered period's** chart themselves (the dashboard passes it to
`BudgetItemFormModal` as a prop).

Five rules the UI has to keep visible, because all five are enforced server-side and none is
guessable from the form:

- **The code string is set once.** There is no rename endpoint — allocations and actuals
  reference a code by string, so renaming would orphan every row already tagged. The edit modal
  renders it as read-only text rather than a disabled input, because disabled reads as "not right
  now" when the truth is "not ever". A mistyped code is retire-and-recreate.
- **Retiring is the normal end of a code's life in a period.** It changes that period's chart
  only — other periods keep their own row for the same string — and a retired code stays listed
  so the period's items on it keep resolving. `DELETE` exists only for a code created in error
  that has no items in this period; `IBudgetCodeUsageProbe` turns it into a 409 the moment that
  stops being true, and the server's message names retirement as the alternative. Both are
  two-click, and both confirms name the period.
- **The hierarchy is one level deep**, guarded from both directions: a parent must be top-level,
  *and* a code that already has children cannot be given a parent (otherwise the chain is built
  bottom-up), and a parent must be in the **same period** (`ParentNotFound` otherwise — the
  modal's picker is fed only the entered period's chart). `parentCandidates` in
  `lib/api/budgeting.ts` mirrors this so the picker never offers an option the server will
  reject. Retiring a parent does **not** cascade to its children.
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

**The vendor register is real, and tenant-wide — NOT period-scoped.** A vendor is the same
counterparty in every period, so `screens/Vendors.tsx` (rail item Vendors, code `VN`, in
PLANNING) is deliberately left out of `PERIOD_SCOPED`, names no period and takes no period hold.
Routes (same `BudgetAccess` group): `GET vendors?includeInactive=` (ordered by name), `GET
vendors/{id}`, `POST vendors` → 201 `{ id }`, `PUT vendors/{id}` (**full replace** — an omitted
optional field is cleared, so `draftToVendorInput` always sends all nine keys, null when blank),
`POST vendors/{id}/activate|deactivate` → 204, `DELETE vendors/{id}` → 204 / 409
`Budgeting.Vendor.InUse`. The screen fetches the whole register (`includeInactive=true`) and its
"Show retired" toggle filters client-side, because the duplicate-name check must see retired
vendors: names are unique per tenant **ignoring case, retired included** (`VendorNameRule`), so
the modal warns live in the server's own words (`duplicateVendorMessage` ↔
`VendorErrors.DuplicateName`) and links to the existing vendor — or arms a restore when it is
retired. Delete is offered blind (usage is not knowable client-side); its 409 shows verbatim with
RETIRE … INSTEAD. Retire, restore and delete are each two-click. The GST registration number is
reference data only (its hint says the platform never calculates tax); the QuickBooks display
name is the future import's match key. `lib/vendors.ts` mirrors `Vendor.Normalize` rule for rule
(`vendorError`, server order and messages), `Vendor.NormalizeName`, `VendorNameRule`
(`findDuplicateVendor`, with `exceptId`), and the default code's `BudgetCode.NormalizeCode` /
`HasValidCodeFormat` (`hasValidCodeFormat` in `lib/api/budgeting.ts`, shared with the code
form). Tests: `lib/vendors.test.ts`, the vendor block in `budgeting.requests.test.ts`, and
`components/screens/Vendors.test.tsx` (screen and modal, `api` prop injected).

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

## Cost centres: a tenant-wide register

A cost centre is an organisational unit or base (owner decision — never a vehicle). The register
is **tenant-wide, not per period** — the opposite of budget codes — so it is its own rail item,
**Cost Centres** (`CC`, id `costCentres`, PLANNING group), **not** a tab on Budget Codes and
**not** in `PERIOD_SCOPED`: Budget Codes is one period's chart and remounts on every switch, and
an edit under a period banner would read as "this period only". It renders with or without an
entered period, like Settings.

| Route | |
|---|---|
| `GET /api/budgeting/cost-centres?includeInactive=` | ordered by code; the app always sends the flag and always asks for `true` (the parent and children rules need retired entries) |
| `GET /api/budgeting/cost-centres/{id}` | 404 `NotFound` |
| `POST /api/budgeting/cost-centres` | `{ code, name, description, ownerUserId, parentId }` → 201 `{ id }` |
| `PUT /api/budgeting/cost-centres/{id}` | same body **without `code`** (immutable; a different code is 400 `CodeImmutable`) → 204 |
| `POST /api/budgeting/cost-centres/{id}/activate\|deactivate` | 204; deactivate is 409 `HasActiveChildren` for a parent with active children (never cascaded) |
| `DELETE /api/budgeting/cost-centres/{id}` | 204; 409 `HasChildren`, then 409 `InUse` (any budget code in any period carries it) — the screen then offers RETIRE INSTEAD |
| `GET /api/budgeting/periods/{id}/rollups/cost-centres` | the period's **planned** expense per cost centre + `noCostCentre` + `totalPlannedExpenseCad` (= the period's planned-expense tile). No actual field — the panel shows no actual column |

- **The code is trimmed only — case preserved — and every match is ordinal.** Never route a
  cost-centre string through `normalizeBudgetCode` (which upper-cases). `lib/costCentres.ts`
  mirrors `CostCentre.NormalizeCode`, `CostCentre.Create`/`Validate` + the update handler's order
  (`costCentreError`), `CostCentreParentRule` (`parentCandidates`: active top-level only, the
  current parent kept even if retired since, nothing for an entry that has children), and
  `BudgetCodeCostCentreRule` (`budgetCodeCostCentreError`); messages verbatim in
  `COST_CENTRE_MESSAGES` / `BUDGET_CODE_COST_CENTRE_MESSAGES`.
- **The budget-code form's cost centre is a picker of ACTIVE entries** (still absent for Revenue).
  A code whose current value is retired or not in the register keeps it, selectable and marked
  with a chip — the server accepts an **unchanged** value unconditionally (`costCentreOptions`).
  "+ New cost centre…" opens `CostCentreFormModal` as a sibling overlay (not a child — the
  shell's `backdrop-filter` would trap a nested fixed overlay) and selects what it creates.
- **The dashboard's "Expense by cost centre" panel** (`screens/periods/CostCentreBreakdown.tsx`)
  sits beside "Expense by priority". It is the server's rollup, refetched whenever the items list
  lands (retried until its total agrees with the loaded expense items); children nest under a
  parent present in the rollup (`groupRollup`), "No cost centre" is always listed, and the total
  shown is the server's `totalPlannedExpenseCad`.
- Writes refetch with `refetchUntil` (`costCentreReflects` after a save; `isActive` / absence
  after retire, restore, delete). The register writes take **no period hold** — they belong to no
  period.

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
| 2026-09-29 | Code audit after the period workspace | **Pass** — the one new `statusMeta` call (`PeriodChooser`'s accent stripe) sits beside the row's state `StatusChip`; the banner and chooser carry state only via `StatusChip`; no new protected hex |
| 2026-09-29 | Code audit after budget items | **Pass** — no new `statusMeta` call and no new protected hex; the priority chip is a `StatusChip` with a per-priority glyph (M / S / N) + written label, because Must and Should share the `info` colour; the modal's segmented choices use `aria-pressed` plus a ✓ and bold on the selected option |
| 2026-09-29 | Code audit after per-period codes | **Pass** — no new `statusMeta` call and no new protected hex; the "Applies to every period" chip is gone; the Copy codes outcome is a `StatusChip` (Copied / Nothing copied) beside the written summary, as on the items copy |
| 2026-10-08 | Code audit after the vendor register | **Pass** — no new `statusMeta` call and no new protected hex; Active/Retired is a `StatusChip` (glyph + label) on every row and in the detail pane; the duplicate-name note carries a "Name taken" `StatusChip` beside the written message |
| 2026-10-08 | Code audit after the cost-centre register | **Pass** — no new `statusMeta` call and no new protected hex; status on the register rows/detail, the code form's kept value and the rollup's retired/unregistered rows is always a `StatusChip` (glyph + written label) |
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
  `NeverReferencedBudgetCodeUsageProbe`, so `DELETE periods/{id}/codes/{codeId}` answers 409
  `Budgeting.Code.InUse` for a code with items in its period (codes are per period now, so the
  probe is too). Still open: the
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
  the item picker~~ — done: `allocationCandidates` offers only active codes of the
  section's category — planned codes included, since a code may carry many items.)
- CI/CD and the OVHcloud deployment target — there is no `.github/` anywhere in this repo yet;
  that is a platform-wide story covering every app at once.
- OIDC/OpenIddict; the `SuperUser` claim from architecture Section 6.1.
