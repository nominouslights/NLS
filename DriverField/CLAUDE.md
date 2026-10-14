# Driver Field App — `DriverField/`

Next.js 16 + React 19 on port **3004**, against the shared API. An installable **PWA** for a
company-issued 10-inch landscape Android tablet.

**What is real:** authentication, the Driver role gate, and the **hours of service** — the
duty ledger (`GET /api/drivers/me`, `GET …/{id}/hos/days`, `GET …/{id}/hos/status`,
`GET …/{id}/hos/exceptions`), a duty change (`hos.duty` → `POST …/hos/duty-events`, personal
conveyance and other-carrier hours included), a day's certification (`hos.certify` →
`POST …/hos/days/{date}/certify`), an adverse-conditions / emergency exception and its
withdrawal (`hos.exception` → `POST …/hos/exceptions`, `…/exceptions/{id}/withdraw`) and an
ELD malfunction report (`hos.eld-malfunction` → `POST …/hos/eld-malfunctions`), through a
durable IndexedDB command queue that drains on reconnect and on a timer. The Hours screen
carries no `MockTag`; the alerts at 60/30/0 minutes run on the same figures on every screen;
an out-of-service declaration from the server takes the whole frame.
**What is not:** everything else. Trips, manifests, the DVIR's vehicles and defects, incidents,
credentials and the driver's own profile come from `lib/data.ts`, and every screen showing
them carries a `MockTag` saying so. One mock value touches the hours: the personal-conveyance
sheet prefills its start odometer from `assignedVehicle.odometerKm` when no event on the
ledger carries a reading — a suggestion the driver corrects, never a figure that goes to the
wire unread.

This app is a Next.js PWA rather than Flutter by the owner's decision (2026-09), reversing the
framework choice recorded in the architecture skill. The device policy, the landscape-only
constraint and the offline-first requirement are **unchanged** — only the framework. See
"Out of scope" for what that costs.

## Commands

- `npm run dev` — dev server on **3004** (pinned in the script). Proxies `/api/*` to
  `http://localhost:5215` server-side exactly as Dispatcher and Budgeting do, so it works
  against a manually-started API with no other setup. Also started by `aspire run`.
  The browser only ever talks to this app's own origin — **there is no CORS anywhere in the
  stack**, and adding some to the backend to "fix" something here is the wrong fix.
- `npm run build` · `npm run lint` · `npm test`

## The design system is a copy, not a shared package

Same decision as `Budgeting/`, and for the same reason: extracting a shared package was the
alternative and was rejected — there is no npm workspace at the root and every app has its own
lockfile. So **24 files here are byte-identical copies of Dispatcher's** (plus one of the
backend's — see `lib/hos/`), each carrying a fixed two-line header:

```
// COPIED FROM Dispatcher/<path> — keep identical below this header.
// Change Dispatcher first, then re-copy. Drift check: see DriverField/CLAUDE.md.
```

`app/globals.css` uses the `/* … */` form. **Change Dispatcher first, then re-copy — never edit
a copied file in place.** Drift here is a visible product bug, not a style nit.

| Copied file | |
|---|---|
| `lib/` | `theme.ts` `format.ts` `period.ts` `useToday.ts` `clipboard.ts` `inspectionForm.ts` |
| `lib/api/` | `transport.ts` `format.ts` `shared.ts` |
| `lib/hos/` | `engine.ts` — and `hos-fixtures.json`, whose source is **`Backend/tests/NorthernLink.Shared.Tests/HoursOfService/hos-fixtures.json`**, not Dispatcher, copied whole with no header (JSON has no comment to carry one) |
| `app/` | `globals.css` |
| `components/` | `HeaderClock.tsx` |
| `components/ui/` | `Button` `Chip` `CorridorStepper` `Field` `FileField` `ImageUploadField` `MetricTile` `ModalShell` `MonthGrid` `Pager` `Panel` `PeriodNav` |

The one-command drift check, from the repo root — prints nothing when in step:

```sh
for f in \
  lib/theme.ts lib/format.ts lib/period.ts lib/useToday.ts lib/clipboard.ts \
  lib/inspectionForm.ts lib/hos/engine.ts \
  lib/api/transport.ts lib/api/format.ts lib/api/shared.ts app/globals.css \
  components/HeaderClock.tsx \
  components/ui/Button.tsx components/ui/Chip.tsx components/ui/CorridorStepper.tsx \
  components/ui/Field.tsx components/ui/FileField.tsx components/ui/ImageUploadField.tsx \
  components/ui/MetricTile.tsx components/ui/ModalShell.tsx components/ui/MonthGrid.tsx \
  components/ui/Pager.tsx components/ui/Panel.tsx components/ui/PeriodNav.tsx
do
  diff -q <(tail -n +3 "DriverField/$f") "Dispatcher/$f" >/dev/null 2>&1 || echo "DRIFT: $f"
done
diff -q DriverField/lib/hos/hos-fixtures.json \
  Backend/tests/NorthernLink.Shared.Tests/HoursOfService/hos-fixtures.json >/dev/null 2>&1 \
  || echo "DRIFT: lib/hos/hos-fixtures.json"
```

`tail -n +3` skips the two-line header. Copies stay **unpruned** — `CorridorStepper`, `MonthGrid`,
`PeriodNav`, `ImageUploadField`, `theme.ts`'s `ServiceType` and `inspectionForm.ts`'s
`NL_PTI_01`/`PRE_TRIP_VALIDITY_HOURS` all ship unused or only partly used, and `lib/hos/engine.ts`
exports far more than this app renders (other-carrier days, deferrals, PC reclassification, the
projection's counterfactual rest). Dead exports cost nothing; a broken byte-diff costs the premise.

**`lib/hos/engine.ts` is a copy for the same hard reason as `inspectionForm.ts`.** It is the
TypeScript mirror of `Backend/src/Shared/HoursOfService` — the one rule engine that decides
whether a driver may drive — and the fixture file beside it is the contract between that C# and
this TypeScript: 72 cases, one minute each side of every limit, each carrying hand-written
expectations and the C# engine's committed snapshot. `lib/hos/engine.copy.test.ts` diffs the
engine against Dispatcher's from disk, `lib/hos/hos-fixtures.copy.test.ts` diffs the fixtures
against the **backend's** (the repo root resolved from the test file — not against Dispatcher's
copy, which could drift in step), and `lib/hos/engine.fixtures.test.ts` holds this app's engine to
every snapshot. Drift here does not make the tablet look different from the console; it makes the
tablet tell a driver they have hours the server will refuse. The fixtures are regenerated on the
backend (`HOS_FIXTURES_UPDATE=1`) and re-copied into Dispatcher AND here; the engine is
re-transliterated in Dispatcher and re-copied. `tsconfig.json` has `resolveJsonModule` on so the
theory can import the JSON directly.

**`lib/inspectionForm.ts` is a copy for a harder reason than style.** It is the NL-PTI-01
catalogue — the legal form itself, and the `key` field on every row is a **wire value** stored as
`InspectionChecklistItem.Item`. Drift between this copy and Dispatcher's source does not make the
tablet look different from the console; it makes the two collect **different legal forms**, and
files defects against item strings the console cannot address. `lib/inspectionForm.copy.test.ts`
diffs the two files from disk as a second backstop to the loop above, because the loop is a
command somebody has to remember to run and the suite is not.

**`NavRail.tsx` is deliberately NOT copied**, which is why Budgeting's list carries it and this
one does not.
Its geometry is hardcoded in the component (`width: collapsed ? 72 : 236`, 26px code tiles, 13px
labels, `"8px 18px"` row padding) with nothing driven from `lib/nav.ts`. A driver rail needs 64px
rows and 44px tiles, unreachable without editing the copy — the one thing the rule forbids.
Copying a file we would never render would be dead weight that still has to pass the drift check,
and a standing invitation to "fix" it. The rail here is `components/DutyRail.tsx`, new, reading
the same `NAV_GROUPS` contract.

Since a second app now holds these copies, **a Dispatcher design-system change is a two-app
re-copy.** Do Dispatcher, then Budgeting, then here, then run both drift checks.

### Known inherited lint problems

`npm run lint` reports exactly **3 errors and 2 warnings**, all in copied files — `lib/useToday.ts`
×3 (`no-explicit-any`, set-state-in-effect, `Date.now()` during render), `app/layout.tsx` and
`components/ui/ImageUploadField.tsx` one warning each. Budgeting reports the same five and
Dispatcher fails identically. **Nothing authored for this app lints dirty, and that is the bar** —
if the count moves, the new problem is yours. Fixing the inherited ones belongs in Dispatcher
first.

## Tablet layout is app-local

> **`lib/theme.ts` owns colour and type *family*. `lib/tablet.ts` owns *size* and *spacing*.
> No hex literal ever enters `tablet.ts`; no number in `theme.ts` is ever changed to suit this
> app.**

That split is what lets 24 files stay byte-identical while this app renders at roughly twice the
physical size of a desktop console. `lib/tablet.ts` exports geometry only: `touch` (44 floor / 56
primary / 64 rail), `bar.height` 72, `rail` 200-88 with 44px tiles, a type scale, `gap`, `radius`,
`wizard` (the DVIR flow's one-question-per-screen geometry — 168px answer tiles, a 40px question,
an 88px footer, a 6px bar), and `viewport` 1280×800.

`components/ui-tablet/` holds enlarged primitives. It is **not** a fork of `components/ui/*`:
where a copied primitive works at size (`Chip`, `Panel`, `ModalShell`, `MetricTile`) use the copy.
Add here only when the desktop one is geometrically unusable — `TouchButton` (the copy is ~34px),
`AnswerButton` (a 300×168 answer tile with a 34px glyph — not a prop on `StatusButton`, which is
56px and padded to sit two in a card row), `DutyControl`, `DutyChip` (the four duty states
through the copied `dutyMeta()` — `TabletChip` keys on `StatusKind` and cannot render On Duty's
blue), `TouchTile`, `StatusBanner`, `SyncPill`, `AttestationTile` (a 56px statement the driver
turns on — teal ✓ / grey —, read back through `aria-pressed`; a `StatusButton` carries an
action, this carries an assertion) and `PersonalConveyanceModal`.

**The copied `Field.tsx` inputs are used at their desktop size, on purpose.** The
personal-conveyance, exception, ELD-malfunction and other-carrier sheets type into the copied
`NumberField` / `TextField` / `TextAreaField` / `DateField` / `TimeField` — 40px tall, under the
44px floor. That is left visible rather than hidden behind a scale() because it is exactly the
signal this file asks for: a `ui-tablet` field set is owed. Everything *around* the inputs on
those sheets (tiles, buttons, the verdict lines) is at size.

`ThreeStateControl` was **deleted** with the DVIR questionnaire: its only consumer was the
scrolling checklist it replaced, and a compact three-button row has no place in a one-question
flow. Keeping an unrendered component would be the same "standing invitation to fix it" this file
already warns about for `NavRail`. `CheckState` in `lib/types.ts` stays.

**Two status chips carry an optional `glyph` override**, and the reason is worth knowing before
adding a third: a defect's `Major` and `Out of Service` are **both** vermillion, because
`lib/theme.ts` is a protected copy and a fifth `StatusKind` or a new hex is not an option. Sharing
a colour is fine; sharing a colour *and* a glyph would leave the text label as the only
differentiator, which grayscale and a glance both defeat. So `Out of Service` supplies its own
`✕` through `severityGlyph()` in `lib/inspectionGate.ts` — pinned by a test asserting the two
resolve to **different glyphs while sharing a colour**. The prop exists on the copied
`components/ui/Chip.tsx`'s `StatusChip` (added in Dispatcher and re-copied into both apps) and on
the app-local `TabletChip` and `AnswerButton`. It overrides the **icon only**: never a new colour,
and never a substitute for the label.

**Anti-pattern, named so nobody reinvents it:** never wrap a copied component in
`transform: scale()` or a CSS override to enlarge it. It breaks text rendering and hides the
signal you want — that the desktop primitive doesn't fit and a `ui-tablet` counterpart is owed.

**No width-based `@media` query anywhere in this app** — no breakpoints, at all. Architecture
non-negotiable #5: company-issued Android tablet, landscape-only, 10-inch class, never BYOD. There
is no phone to be responsive to, and building for one reopens the data-residency question that
rule exists to close. (The one `@media` in the tree is `prefers-reduced-motion` in the copied
`app/globals.css` — an accessibility preference, not a breakpoint. It stays.)

**Landscape is enforced twice, and the second one is the real mechanism.**
`app/manifest.ts` declares `orientation: "landscape"` — which applies to the *installed* PWA only.
In a browser tab on a tablet with rotation lock off, portrait happens, so
`components/OrientationNotice.tsx` renders a full-bleed rotate panel on
`matchMedia("(orientation: portrait)")`.

## Auth and the role gate

Reuses the platform's bespoke JWT flow. **There is no OIDC server yet** — `JwtAccessTokenIssuer`
calls itself interim ahead of OpenIddict.

`lib/auth.ts` is adapted from Dispatcher's via Budgeting's, with the same four deltas:
1. `REFRESH_TOKEN_KEY` is `nl.driverfield.refreshToken` — app-specific, because the apps share a
   hostname under path prefixes in the DigitalOcean image.
2. Imports `./api/transport` directly, not Dispatcher's `./api` barrel.
3. **No account creation.** No `createFirstAdmin`, no invite minting or redemption. First-run
   setup is a one-shot global gate and several apps racing for it is a bug factory — and a driver
   in a vehicle is the last person who should mint the platform's first Owner.
4. Adds `getClaims`/`getRole` for the gate.

Worth knowing on this app specifically: a refresh that fails with a **network** error (status 0)
leaves the session intact; only a definitive 4xx signs the user out. Losing a session to a dead
zone would mean a driver who cannot log a pre-trip.

**The gate is a UX gate, not a security boundary.** `components/RoleGate.tsx` reads an unverified
client-side token decode. The real boundary is the `DriverAccess` policy in
`Backend/src/Api/NorthernLink.Api/Auth/AuthorizationPolicyRegistration.cs`, attached to the
driver-facing endpoint groups.

**And the boundary that matters more than the policy — the own-record rule.** `DriverAccess`
admits Owner, Dispatcher and Supervisor as well as Driver, so passing it says nothing about
*whose* records you may touch. A Driver token may read and write only its own driver row, enforced
server-side on every `{driverId}` route. **Never write anything in this app as though the client's
driver id were trustworthy**, and never assume a 200 proves the caller was entitled to the row.

`lib/roles.ts` mirrors `Roles.DriverAccess` in `Backend/src/Shared/Kernel/Roles.cs`, case-sensitive
to match the backend's ordinal `RequireRole`.

## The offline seam

Offline-first is a hard requirement (architecture non-negotiable #7) and is **partly real,
partly staged**. Coverage between Thompson, Lynn Lake and the Alamos site is patchy by
geography, not as a corner case.

`lib/sync/` is: `types.ts` (`QueuedCommand`, `CommandKind`, `SyncState`), `db.ts` (the one
IndexedDB, `nl-driverfield`, two stores: `commands` keyed by id and `ledger`), `queue.ts`
(`enqueue`/`drain`/`pending`/`retry`/`discard`, a `registerSender` registry, `startSync`),
`status.ts` (cached snapshot + subscribe, written only by the queue and the network events),
`useSyncState.ts` (the React binding), and `store.ts` (local-first reads — still proxying
`lib/data.ts`).

> **Every screen mutation goes through `lib/sync/queue.enqueue()`. Never a direct `lib/api/*`
> call, never an in-place mutation of a `lib/data.ts` array.**

That rule is what let the hours go live by changing files under `lib/sync/` and `lib/hos/` and
**no screen's write path**. It keeps holding: the next `hos.*` kinds are a sender each.

**What the queue does now.** `enqueue()` resolves once the command is committed to IndexedDB —
"held on this device" means durable, and it REJECTS rather than pretend when the device cannot
hold it (the DVIR flow is written so a throwing enqueue leaves the walk-around intact; the
`DutyControl` says "Not recorded" under the control). `navigator.storage.persist()` is requested
on the first write. `drain()` walks pending commands in capture order (`seq`, not the clock —
two taps in one millisecond must still send in order, and a day's duty changes must precede its
certification) and hands each to the sender registered for its kind, with the command id as the
`Idempotency-Key` the sender puts on the wire. The response contract:

| Response | Command becomes | Why |
|---|---|---|
| 2xx — 201 first time, 200 replay | `sent` | a replay is a success, not a duplicate |
| 4xx | `failed`, parked with the server's `message` + `code` | the server said no; sending it again gets the same no. Listed on Hours with RETRY / DISCARD, never retried on its own |
| status 0 (unreachable) / 5xx | stays `pending`; the round stops | a dead zone is not a refusal; the rest waits for the next drain rather than timing out one by one |
| no sender for the kind | stays `pending`, counted as held | captured, never sent, and the pill says so |

Triggers: the window's `online` event, an enqueue while online, and every 60 s — installed by
`startSync()` from `Console.tsx` on mount, never before sign-in. Senders are registered by the
module that owns the endpoint (`lib/hos/ledger.ts` for the four `hos.*` kinds — `hos.duty`,
`hos.certify`, `hos.exception`, `hos.eld-malfunction`); the queue knows no URL. Everything else
(`dvir.submit`, `manifest.board`, `trip.claim`, `incident.report`, `vehicle.fault`) is held.

**The `hos.duty` command (renamed from `hos.record`) is the first whose payload is built by one
function and pinned.** `lib/hos/dutyCommand.ts` writes `source: "Driver App"` and
`clientEventId` = the command's own id — the fix for the mislabel bug where every tablet entry
reached the API as a dispatcher's paper backup. The id is minted inside `enqueue()`, so the
payload is passed as a **factory** (`enqueue("hos.duty", (id) => …)`). The same file's
`dutyEventInputFrom` is the crossing onto `DutyEventRequest` at send time: `duty` → `status`,
the command id → `eventId`, and the source written **again** rather than trusted from the stored
payload. `lib/wire.test.ts` pins both shapes. The same id goes onto the optimistic event in the
ledger store, which is what the next hydrate reconciles on.

The same kind now carries two more things, and the crossing stays the one function. A
**personal-conveyance** start carries `odometerStartKm` and the three attestations the endpoint
requires (`pcNoPassengersOrCargo`, `pcNoTrailer`, `pcNoWork` — the sheet's four tiles fold into
them; nothing invents a fourth field), and the next duty change — whatever its status — carries
`odometerEndKm`. **Other-carrier hours** are three `hos.duty` commands (On Duty at the
regulation day's start, Driving once the on-duty-not-driving time is spent, Off Duty when the
on-duty time is used up; two when there is no driving — a zero-length Driving segment is a false
statement, not a shorter one), each with its own id, carrying `carrierName` and
`enteredBy` = the driver's roster name from `/me`, and the only source this app writes other
than "Driver App": `"Other carrier (declared)"` — written at send time **only for a complete
declaration** (source + carrier name), because the endpoint refuses that source without a name
(`CarrierNameRequired`) and a half declaration must not reach the record under a foreign
label. `knownDriverName()` is null until the server has been reached once on this account, and
the Hours screen disables the other-carrier action with the reason rather than fall back to the
mock driver — `enteredBy` is a wire value on a compliance record.

**`hos.exception` and `hos.eld-malfunction`** are built by `lib/hos/complianceCommand.ts` on the
same pattern: one builder each (`declareExceptionPayload` / `withdrawExceptionPayload` under one
kind, discriminated by `action`; `eldMalfunctionPayload`), one crossing each
(`exceptionInputFrom`, `eldMalfunctionInputFrom`), and a `*DraftError` the sheet runs before it
enqueues — the client-side mirror of `HosException.Declare` (reason ≤ 500, inside the last 24 h
and 5 min of skew, the s.76 knowability attestation, a 1–120 min extension) and of
`EldMalfunctionReport.Report` (code ≤ 32, a description, noticed-at not ahead), so the refusal
reads on the tablet before the server would park it. The senders put the command id on the
wire as `Idempotency-Key` through an optional `WriteOptions` on the API functions; those routes
do **not** honour it yet, so a declaration resent after a status-0 round could duplicate on the
server — the same known gap as every non-`duty-events` kind, and the reason the key is sent
from day one.

**`SyncPill` wording is pinned** (`components/ui-tablet/SyncPill.test.tsx`), because the pill's
honesty is the offline requirement's: `N failed — see Hours` (vermillion ▲, ahead of everything
else), `Sync off · N held` (gray —, no sender registered — before sign-in), `Offline · N held`
and `Sync · hours only · N held` (gold ◐). `SyncState.enabled` is derived — true only when EVERY
kind has a sender — so the green `Synced` cannot appear while a single DVIR is parked on the
device. `enabledKinds` is what carries the partial truth.

### The hours-of-service ledger (`lib/hos/`) — live

`lib/hos/ledger.ts` is a `useSyncExternalStore` store (the `lib/inspectionStore.ts` pattern,
bound by `lib/hos/useHosLedger.ts`) and `evaluation()` is the copied engine's
`evaluate(ledger, asOf, calendar)`. TopBar's duty chip, Today's tile, every figure on Hours, the
`DutyControl`'s disabled reasons, the alerts and the Trips screen's hours rule
(`lib/eligibility.ts`, through `project`) all read that one evaluation on the one band set in
`lib/hos/display.ts` — gold inside the engine's soon window (≤ 2h), vermillion whenever
`canDriveNow` is false, teal otherwise. There is no local threshold and no `MAX_*` constant
anywhere in this app.

**Where the events come from.** On sign-in (`startLedger()` from `Console.tsx`) the store
resolves the driver from `GET /api/drivers/me` (cached for the session; a 404
`Drivers.NotLinked` is shown on Hours as "nothing from the server yet — …"), then fetches
`GET …/hos/days?from=<today − 21>` and `GET …/hos/status`, and again after every drain that sent
something. 21 days, not 14, because that is the server's own `DutyLedgerAssembler.LookbackDays`
— the 14-day rules plus a week so the 7-day cycle and the 36h reset have what they need at the
edge; `knownFromUnix` is the server's (the driver's earliest day on file), and the engine
synthesises the gap before the first fetched event exactly as the server does. Server events are
mapped through `HosWire.statusFrom/sourceFrom`, voided ones excluded, odometers to tenths of a km
as `DutyLedgerAssembler.ToDecikm` does. The Hours history lists the last 14.

A duty change appends an **optimistic** event (status, now, "Driver App", the queued command's
id as `clientEventId`) so the figures move the moment the driver taps. The server wins on every
reconcile: an optimistic event whose id the server now holds is dropped for the server's copy;
one whose command the server **refused** (parked on Hours) is dropped too, and the figures go
back to the record. Pending ones are rebuilt from the queue on reload, so a restart in a dead
zone keeps them. The last server copy is cached in IndexedDB keyed to the signed-in account
(`lib/sync/db.ts`, `ledger` store) so a reload offline still shows figures — and the Hours
banner says **which** copy the figures are built on (live / device copy / none) and the last
fetch error verbatim, because a dead zone must be visible, not silent. The clock is the
device's; the alerts tick it every 30 s.

The banner on Hours is not optional: the record of duty status lives on the bus's ELD and
keeping two records is a severe violation, so every figure here is a **planning figure** and
says so.

**Certification** (`hos.certify`) is the driver's own act under s.84 — the endpoint carries
`OwnDriverOnlyMetadata`, no dispatch override — so the attestation sheet says what is being
signed. `certifyState()` in `Hours.tsx` mirrors `DutyDay.Certify`'s refusals (open PC segment,
nothing to certify, day in the future) and adds the two this device knows (a certification
already queued, one the server refused), so the reason reads on screen before a parked command
would have said it. A certification queued offline drains **after** the day's duty changes, by
capture order.

**Alerts** (`lib/hos/useHosAlerts.ts`, rendered by `components/HosAlerts.tsx` from `Console.tsx`
on every screen) are a module-level store driven from the ledger, not an effect, for the same
lint and testability reasons as every other store here. The thresholds are
`ALERT_THRESHOLDS_SECONDS` in `lib/hos/display.ts` (60/30/0 min, the owner's policy default,
pinned). 60/30 → a dismissible `ModalShell` with the dispatch number on a `tel:` button; 0 while
Driving → an app-local non-dismissible panel (not `ModalShell`: its ✕ would close) cleared only
when duty leaves Driving, then a persistent out-of-hours `StatusBanner`; once per
(limit, threshold) per duty period (the engine's shift — it resets after ≥ 8h off); re-remind
10 min after a dismissal if still Driving; nothing fires Off Duty or in PC, but the last figures
are kept so a crossing during a break fires on return. The dispatch number is
`lib/company.ts`, **app-local for now** — Dispatcher's `company.ts` has no `dispatchPhone` yet;
once it does, this file becomes a header-copied file on the manifest.

`lib/api/hos.ts` is likewise **app-local for now**: the same type and function names as
`Dispatcher/lib/api/hos.ts` (`listDutyDays` / `getHosStatus` / `recordDutyEvent` /
`listHosExceptions` / `declareHosException` / `withdrawHosException` / `listEldMalfunctions` /
`reportEldMalfunction` and their records identical), plus `getMyDriver` and `certifyDay` which
are this app's alone, and an optional `WriteOptions` (`idempotencyKey`) on the compliance
writes that the console never passes. It becomes a copy once the console's settles. Never
invent a field in it: the shapes are `HosResponses.cs` and `ComplianceResponses.cs`.

### The compliance slice — live

**Exceptions, the out-of-service span and the ELD malfunction reach the ENGINE, not just the
screens.** `lib/hos/ledger.ts` maps the server's exception records onto the engine's
`HosException` exactly as `DutyLedgerAssembler.ToEngineException` does (an adverse or emergency
exception applies from `effectiveFromUtc`, the instant the conditions arose — not from when the
record was written; only Declared / Acknowledged / Approved apply), and the out-of-service
declaration — the one in force on `status` plus any overlapping a fetched day — onto
`OutOfService` spans as `ToEngineSpan` does. So the DutyControl greys Driving with "Out of
service" from the same `blockingReasons` as every other figure, and an adverse extension moves
the shift ceiling the moment it is declared. A declaration queued on this tablet is applied
optimistically (id = the command id); a queued withdrawal stops a server exception applying at
once; both go back to the record on the next hydrate, and a refused declaration drops out of the
figures when it parks. `LedgerSnapshot` carries the list (`exceptions`, server records plus
queued ones, newest first), the active declaration (`outOfService`) and `paperLogMode`
(`status.eldMalfunctionOpen`, or a report pending on this device). `syncWithQueue()` is the
queue-subscription body as a public function — what a test calls after an enqueue without
`startLedger`.

**Out of service takes the whole frame.** `components/OutOfServiceScreen.tsx` is rendered by
`Console.tsx` in place of the rail and every screen while `snapshot.outOfService` is set: ✕
(a glyph override on the `over` colour — never a new hex), "Out of service until {local} — do
not drive" (or "until lifted"), enforcement's reason verbatim, the dispatch number, and one way
out — a read-only Hours (`<Hours readOnly />`: no actions, no certification) whose duty control
offers Off Duty and On Duty (yard work) and greys Driving (the engine's reason) and Personal
Conveyance (its own: PC while out of service is driving). The TopBar shows the ✕ chip, and the
✎ "Paper-log mode" chip while an ELD malfunction is open or a report is queued here.

**Personal conveyance** (`components/ui-tablet/PersonalConveyanceModal.tsx`): the start
odometer and four attestation tiles, every one of which gates Confirm; leaving PC by any duty
tap opens the same sheet for the end odometer, refused below the start on the tablet
(`PcOdometerDecreasing`). Today's kilometres read through `pcDayVerdict()` in
`lib/hos/display.ts` — teal under 60.0 km, gold ◐ from 60.0 (`PC_WARN_DECIKM`, the owner's
warn-at figure), vermillion ▲ from 75.0 (`HOS_LIMITS.pcDailyLimitDecikm`, the engine's; the
server refuses to open a segment at or past it, `PcAllowanceExhausted`) — pinned a tenth each
side. `pcReason()` in `DutyControl.tsx` greys PC while out of service, once today's allowance
is used up, and after the server has answered `PcAllowanceExhausted` to a change made today.
Kilometres are odometer readings the driver types, and every surface says so: this tablet has
no GPS and reads no odometer.

**Prior records.** `status.priorRecords` is the compliance officer's s.82(1)(f) attestation.
Hours shows "Prior 14 days not on file — not dispatchable" when it is null **and**
`recordsOnFile` is false — a driver whose own 14 days are on file needs no attestation, and
the line would be false for them. The history rows show each day's exceptions (kind · status)
and an out-of-service declaration overlapping it.

`eligibility()` moved from `lib/data.ts` to `lib/eligibility.ts` when its hours rule went onto
the engine — the store is live and `data.ts` is the read-only mock layer, so the rule could not
live in `data.ts` without the mock file importing the live ledger. The other four §5.4 rules are
unchanged and still mock.

**`lib/inspectionStore.ts` is deliberately NOT under `lib/sync/`, and the distinction is the
point.** An in-progress DVIR **draft** is uncertified, private to the device, and costs a driver the
whole NL-PTI-01 walk-around (53 or 64 pre-trip questions depending on the unit; the post-trip is 6 Close-Out checks plus
its new-defects list) if lost — so it lives in `localStorage`, synchronously, and survives a
reload. The post-trip is **not a re-check list, by design** (NL-PTI-01 rev 4): NSC 13 requires no
full post-trip inspection — the duty at the end of a run is to record defects found en route — so
after Close-Out the driver answers "Any defect found after the pre-trip?" and files each new
defect against the pre-trip item it concerns (`lib/newDefects.ts`). Do not "restore" a list to it.

A draft whose **shape** a form revision changes is **discarded, not migrated**
(`INSPECTION_STORE_VERSION` is bumped — v3 for rev 2 and the location field, v4 for rev 4's
new-defects step), and on load the
store also drops any answer, note or defect whose key is not on that half of the form; the screen
then narrows to the exact unit before counting or submitting. A row new to the form has no answer,
so it reads "Not answered" and blocks Certify — nothing ever defaults to Ok. **Rev 3 did not
bump**: it changed no shape, only retired keys (listed in the catalogue's `RETIRED_KEYS`), and the
load filter drops every one of them — a rev 2 draft keeps its surviving answers, and the
replacement rows ("Engine fluid levels", "Remote / winter kit", …) read unanswered. `RETIRED_KEYS`
is display metadata; it is never used to carry an old answer onto a new row.

**The per-section "All OK" (rev 3) is the one bulk answer in the wizard, and it is bounded.** On
the first unanswered row of a sub-group, `CheckStep` offers a separate "All OK — <section>
(n checks)" button; it opens `SectionConfirm`, which lists every row of that sub-group by label
with what will happen to it, and only its "Confirm all OK" writes. The write is
`markSectionOk(mode, vehicleId, unit, groupKey)` in `lib/inspectionStore.ts` — it takes a group key,
so it cannot span sub-groups, and it fills **blanks only** (never a Defect, N/A or existing Pass).
Filled rows are ordinary `"pass"` answers: each goes over the wire as its own row with
`state: "Ok"`, nothing says "section passed", and any of them can be changed afterwards. There is
**no whole-form All OK** and there must never be one. The confirm list is label-only in two
columns, budgeted for `wizard.sectionMaxRows` (12 — Controls & Instruments on NL-02) in
`lib/tablet.ts`; a larger sub-group gets no shortcut rather than a clipped list, and a test fails if
the catalogue ever grows one. Local certifications carry their own
`CERTIFIED_STORE_VERSION`, so a draft bump never drops today's certifications and re-blocks
boarding. The header step also collects the inspection **location** (town or highway), required
here under Man. Reg. 95/2008 s.12(1), trimmed and capped at 200 to mirror
`VehicleInspection.Create`. A **certified** DVIR is the compliance record, and its home is the offline batch's
IndexedDB queue with `navigator.storage.persist()`. Putting a draft under `lib/sync/` would make
it look like the queue and invite the offline batch to migrate it; putting a queued DVIR in
`localStorage` would be exactly the compliance failure named below. The transition between the two
states is one function, and the order is not negotiable: **`enqueue()` → `recordCertification()` →
`discardDraft()`** — if `enqueue` throws, the driver's whole walk-around survives. `commandId` on a local
certification is the offline batch's hook: `drain()` can mark one server-accepted by that id and
the "Held on this device" banner starts telling a different truth with no API change.

Three things the rest of the offline batch must keep:
- **`Idempotency-Key`.** Every `QueuedCommand` carries a client-generated GUID and the drain sends
  it. `POST …/hos/duty-events` is the one endpoint that honours it today (as the `eventId`
  alias, 200 on replay); the others do not yet, so the client sending it from day one is what
  lets each start honouring it with no client change — the server side needs a per-module
  `processed_commands` table.
- **IndexedDB, not SQLite.** §8 of the architecture reference says SQLite because it was written
  for Flutter. Everything else in §8 survives verbatim. Done: `lib/sync/db.ts`.
- **`navigator.storage.persist()`.** IndexedDB is evictable unless persistence is granted. A queued
  DVIR vanishing because the browser reclaimed space is a **compliance** failure, not a lost draft.
  Done: requested on the first durable write.

## PWA

- **`app/manifest.ts`, not a static `public/manifest.webmanifest`** — `start_url`/`scope` must equal
  the basePath, which is `/` locally and `/driver` in the image. A route gets `basePath` applied
  automatically; a static file would need a build-time substitution step.
- **Icon gotcha:** Next does *not* auto-prefix icon `src` strings inside the manifest body. They
  carry `${basePath}` explicitly. A missing prefix surfaces only in DevTools → Application →
  Manifest, and only on the prefixed build — everything looks fine locally while the deployed app
  silently never offers an install.
- **`public/sw.js` is hand-written**, ~40 lines, no `next-pwa` or Serwist. A fourth runtime
  dependency that rewrites the build is too much to pay for a batch implementing no offline
  behaviour. Two rules in it must not be relaxed: **nothing under `/api/` is ever cached** (a stale
  auth response or a stale manifest is worse than a failure), and navigations fall back to the
  cached shell so a dead zone shows the UI rather than a browser error page.
- **Registration is production-only.** A service worker in dev caches turbopack chunks and serves
  them back after you edit the source — the single most confusing bug class in this stack.
  `next.config.ts` also sets `Cache-Control: no-cache` on `sw.js` so a browser cannot pin a stale
  worker.
- **The Dockerfile differs from the other three in two ways, and both are load-bearing.** It does
  *not* `mkdir -p public` (that line exists in the others only because they have no `public/`), and
  its `COPY … /app/public ./public` is **real work here**: `output: "standalone"` does not copy
  `public/` into `.next/standalone`, so deleting that line 404s the manifest, the icons and the
  service worker with no build error and nothing in the logs.
- **Debt: the fonts are fetched from `fonts.googleapis.com` at runtime**, which defeats the offline
  story — a first run in a dead zone renders fallback families, at which point `theme.ts`'s tuned
  sizes are wrong. `CommunityMobile/` bundles the same families locally for exactly this reason.
  Self-hosting them as woff2 under `public/fonts/` is a change to `app/layout.tsx` (adapted, not a
  protected copy, so it is legal) and belongs in the offline batch.

## Testing

Vitest. `vitest.config.mts` is Budgeting's plus one line — `setupFiles: ["./vitest.setup.ts"]`,
which imports `fake-indexeddb/auto` so the real durable queue runs in every test — and two
things about it are load-bearing: the **`.mts` extension** (Vite loads a plain `.ts` config as
CJS and warns) and **`import.meta.dirname`, never `__dirname`** — `@types/node` declares
`__dirname` globally, so a "fix" to it leaves TypeScript and `next build` green while every
`@/lib/...` import in the suite fails to resolve at run time. A test that fakes timers must leave
`setTimeout`/`setImmediate` real (`vi.useFakeTimers({ toFake: ["Date", …] })`) or the fake
IndexedDB never completes a transaction.

| File | Why it exists |
|---|---|
| `lib/roles.test.ts` | The rule. Acceptance criterion: **an Accountant account is rejected** |
| `lib/claims.test.ts` | Claim names pinned to `JwtAccessTokenIssuer`; non-ASCII round trip |
| `lib/accessSeam.test.ts` | The **chain**, unmocked: token → claims → role → gate. A rename on either side of that seam leaves the other three files green and locks every driver out |
| `components/RoleGate.test.tsx` | The gate applies the rule. Every denial asserts the denial screen **renders**, not just that children are absent |
| `lib/eligibility.test.ts` | Each of the five §5.4 rules failing in isolation, and the verdict being conjunctive. The hours rule is `HosEngine.Project` with the default margin: a trip that fits only without the margin fails, the breach is the server's sentence, and the s.13 shift reset after 8h off is demonstrated rather than assumed |
| `lib/hos/engine.copy.test.ts` | The engine copy is byte-identical to `Dispatcher/lib/hos/engine.ts` below its two-line header, read from disk |
| `lib/hos/hos-fixtures.copy.test.ts` | The fixture copy is byte-identical to the **backend's** `hos-fixtures.json`, read from disk with the repo root resolved from the test file |
| `lib/hos/engine.fixtures.test.ts` | **The mirror guarantee**: all 72 fixture cases meet their `expect` block and reproduce the C# engine's committed `snapshot` exactly (`HosFixtureTests.cs`), plus event-order independence and "a larger margin never turns infeasible into feasible" |
| `lib/hos/ledger.test.ts` | The live store against a mocked `lib/api/hos`: `/me` → days (the server's 21-day lookback) + status + exceptions, events mapped through `HosWire` with voided ones excluded and odometers as `DutyLedgerAssembler.ToDecikm`; the device clock and `tick()`; an optimistic append moving the status and figures at once with the command id on the event; Driving re-greyed with its reason when the window is used up; reconciliation (the server's copy replaces the optimistic one; a **refused** command's event is dropped and the figures follow; a sent one triggers a re-fetch; held ones are rebuilt from the queue on start); the cache serving a dead-zone reload for the same account and never another; retention at a day boundary; the subscribe contract. The compliance slice: the roster name kept from `/me`; the out-of-service span reaching the engine (**Driving blocked with "Out of service"**) and exposed only while in force; exceptions mapped as `ToEngineException` does — an applying adverse one moving the shift ceiling, rejected / withdrawn ones listed but never applied, an emergency's end; a queued declaration applied at once and dropped when refused; a queued withdrawal stopping a server exception applying; paper-log mode from the flag and from a queued report; a held PC event rebuilt with its odometer and an other-carrier one with its source |
| `lib/hos/useHosAlerts.test.ts` | The alert rules on a real ledger and the real engine with a controlled clock: 61→59 fires 60, 31→29 fires 30, 1→0 fires the non-dismissible stop alert cleared only by leaving Driving with the banner staying; once per (limit, threshold) per period; one modal naming the most specific limit when several cross together; nothing fires Off Duty or in PC and the crossing fires on return; the banner alone when 0 arrives On Duty; the 10-minute reminder while still Driving and not otherwise; the period reset after 8h off; the 30 s tick starting and stopping |
| `lib/hos/display.test.ts` | The one band set: every engine band renders as colour + glyph + label, the 7-day bar's per-day banding on **both sides** of the engine's soon window, the alert thresholds pinned at 60/30/0 with `alertThresholdCrossed` on both sides of each (and the tightest on a jump), `limitRemaining` watching the window alone rather than `mustStopDrivingBy`, and the local-time helpers agreeing with `HosCalendar` on the DST days; `pcDayVerdict` at 59.9 / 60 / 74.9 / 75.0 km with its words, `kmLabel`, the five exception statuses each with a band and a label, and the ✕ / ✎ glyph overrides differing from their colour-mates' |
| `lib/wire.test.ts` | Exact spelling of the four duty strings, the five source strings, the exception kinds and the `HosRule` / `BlockingReason` / `HosBand` member names against `HosWire.cs` (which `HosDisplay` delegates to); the `hos.duty` payload carrying `source: "Driver App"` and `clientEventId`, and `dutyEventInputFrom` mapping it onto `DutyEventRequest` with the source written again — the PC opener's odometer and three attestations, the closer's end odometer, and `"Other carrier (declared)"` written only for a complete declaration; and the three inspection enums (`InspectionDefectSeverity`, `InspectionType`, `InspectionSource`) — including `"Out of Service"` → `"OutOfService"` |
| `lib/api/hos.requests.test.ts` | What goes on the wire, fetch-mocked through the real transport: `/api/drivers/me` with no id in the path and its 404 `Drivers.NotLinked`; the days route with and without `from`/`to`; the status route; `recordDutyEvent` repeating the `eventId` as `Idempotency-Key` and a 200 replay resolving; `certifyDay` as a bodiless POST resolving on 204; the exceptions list with its range, a declaration's body and optional `Idempotency-Key`, a withdrawal's route and note body, the malfunction list and report; and the server's sentences verbatim for `EventOutsideDay`, `PcSegmentOpen`, `DayNotFound`, `CertifyDriverOnly`, `NotYourRecord`, the declaration's three 400s, the withdrawal's 403 and `EldCodeTooLong` |
| `lib/sync/queue.test.ts` | The durable queue on the fake IndexedDB: distinct ids, the payload factory, resolving on the local write with nothing fetched, **surviving a simulated reload in capture order**; the drain handing each command to its sender with its id in order, the id reaching the wire as `Idempotency-Key` through the real transport, a 200 replay counted as sent, a **4xx parked with the server's message and never retried on its own**, **status 0 and 5xx left pending with the round stopped**, a kind with no sender held, retry and a durable discard, single-flight; the triggers (start, enqueue while online, the `online` event, the interval — and none before `startSync`); `enabled` never true while any kind has no sender, with all four hos.* kinds registered; the two compliance kinds known to the queue and parked by a 4xx with the server's message |
| `components/ui-tablet/SyncPill.test.tsx` | The pill's wording, word for word: `Sync off`, `Sync · hours only · N held`, `N failed — see Hours` (ahead of everything), `Offline · N held`, and **never the green check while any kind is parked**; the rendered pill once the hos.* senders exist |
| `components/screens/Hours.test.tsx` | The screen as a driver meets it: no `MockTag`; a refused change listed with the server's message and code, and DISCARD removing it; Certify today opening the attestation sheet and queuing `hos.certify` with the date; the button disabled **with the reason on screen** while a PC segment is open and when nothing is recorded today. The compliance slice: the adverse sheet's Declare locked until the s.76 attestation is on and the extension is 1–120 min (both sides), then `hos.exception` queued with the `HosWire` kind and applied to the shift ceiling at once; the emergency path; today's exceptions listed with their status and WITHDRAW queuing a withdrawal only while Declared; the ELD report queued and the paper-log line shown at once and from the server's flag; other-carrier hours as **three `hos.duty` events in order with distinct ids, the other-carrier source, the carrier and the roster name**, and the action unavailable with the reason until the name is known; the prior-records line on both sides of `recordsOnFile`; the history rows' exception and out-of-service chips; `readOnly` hiding every action |
| `components/ui-tablet/PersonalConveyanceModal.test.tsx` | The four attestations **all** gating Confirm (three on is still off; turning one off re-locks it); the odometer required and never negative; the end-odometer prompt refusing a reading below the start on the tablet with the start named; the projected band at 59.9 / 60 / 74.9 / 75 km by colour, glyph and words |
| `components/ui-tablet/DutyControl.test.tsx` | The PC tap opening the sheet and nothing queued until Confirm; the queued `hos.duty` carrying the PC fields and the optimistic event its odometer; leaving PC prompting for the end odometer first, the closing event carrying it, and 30.0 km counted from the readings; PC greyed with the reason once the allowance is used up and after the server's `PcAllowanceExhausted`; out of service greying Driving and PC with their reasons on screen and leaving Off/On Duty |
| `components/OutOfServiceScreen.test.tsx` | "Out of service until {local} — do not drive" (and "until lifted"), the reason verbatim, the dispatch number; View hours showing a read-only Hours with Driving / PC disabled **with the reason on screen**, Off / On Duty enabled, no actions, no certification; Back to the notice |
| `components/TopBar.test.tsx` | The ✕ Out of service chip while the declaration is in force and not once it has run out; the ✎ Paper-log mode chip from the server's flag and from a report queued here before the server has it |
| `lib/hos/complianceCommand.test.ts` | The mirrors of `HosException.Declare` (knowability, 1/120 minutes both sides, reason 500/501, the 24h window and 5-min skew both sides) and `EldMalfunctionReport.Report` (code 32/33, description, noticed-at ahead); the payloads and their bodies, a withdrawal, the other-carrier day laid out from the day's start (three events, two with no driving) and its bounds (carrier required, driving ≤ on-duty, ≤ 24h, not ahead, inside 14 days); and through `startLedger`'s real senders: each kind sent to its route with the command id as `Idempotency-Key`, **a 4xx parked with the server's message**, a wrong-shaped payload never sent |
| `lib/inspectionForm.copy.test.ts` | The NL-PTI-01 copy is byte-identical to `Dispatcher/lib/inspectionForm.ts` below its two-line header, read from disk. Drift means the tablet and the console collect **different legal forms** |
| `lib/defects.test.ts` | The mock defect model: open means NOT RESOLVED — a defect with a work order stays open until it is Repaired or No fault found; resolved ones never count toward open; the Today tile counts only the assigned vehicle's open defects; the 14-day "Recently resolved" window (14 in, 15 out) and its wording |
| `lib/newDefects.test.ts` | The rev 4 post-trip's new-defects rules: items offered are the unit's pre-trip rows minus what today's pre-trip already reported and what this report already picked, the wire shape (real item key, never a checklist row), and what blocks Certify (unanswered No/Yes, Yes with no defect, a defect missing item, grade or note) |
| `lib/inspectionSteps.test.ts` | The DVIR wizard's step model: unique item keys, the real denominators (NL-01 pre 53, NL-02 pre 64, post 6 Close-Out for both units and an unknown one, pre 64 for an unknown unit) **derived, never written as a literal**, the post-trip being Close-Out plus the new-defects step, a denominator that cannot be inflated however many defect follow-ups are injected, and when the per-section All OK is offered and where it lands — plus the pin that no sub-group outgrows `wizard.sectionMaxRows` |
| `lib/inspectionStore.test.ts` | The draft: per-mode-per-vehicle keys, a reload, a version bump (including a rev-1 v2 draft) and a stale service day both discarding rather than migrating, unknown and other-half item ids dropped on load, a rev 2 draft's **retired** keys dropped with their replacements left unanswered, new rows left unanswered, `markSectionOk` filling blanks in one sub-group only, the location kept raw, certifications surviving a draft bump, and hostile storage failing honestly |
| `lib/inspectionGate.test.ts` | `deriveResult` against `VehicleInspection.DeriveResult`, `odometerError` against `Vehicle.RecordOdometer` and `locationError` against `VehicleInspection.Create`'s Normalize + `LocationMaxLength` (200 vs 201, trimmed), both sides of each boundary; the boarding gate in both directions; **the negative pin** that this is deliberately *not* a sixth §5.4 rule |
| `components/screens/Manifest.test.tsx` | The gate as a driver meets it — both buttons disabled with the reason **on screen**, the not-server-enforced admission present, and **a badge scan boarding nobody** |

The governing rule, same as Budgeting's: **anything in this app that re-derives a server rule
client-side belongs here, with the C# method it mirrors named in the test's comment.**

## Data: everything except hours is mock

`lib/data.ts` follows the documented conventions — flat exported `const` arrays, prefixed string
ids, ISO date strings never `Date` objects, every status-bearing row carrying its own `StatusKind`,
derived arrays computed rather than duplicated, threshold logic in `data.ts` rather than in a
screen. Row shapes live in `lib/types.ts`.

**No screen invents an API shape.** The backend owns endpoint shapes; each array is replaced by
additions to a `lib/api/<domain>.ts` written against the real response as its slice lands, and
screens keep their props.

Budgeting keys its mock rows to ids no real record can match, so screens already on real data show
empty states. That needs real data to work against and most of this app has none — so instead
**every screen showing mock values carries a `MockTag`.** Hours is the one that does not: its
figures are the driver's own. Nothing tagged should be demonstrable as working software.

The highest-value function in this layer is **`eligibility()`** (`lib/eligibility.ts`, moved out of
`data.ts` — see "The hours-of-service ledger") — the five §5.4 rules. It is a client-side mirror
of a rule the server must own, in the same way Budgeting's `previewPeriod` mirrors
`BudgetPeriod.Create`; its hours rule is now the real engine's `project`.

## Accessibility

Status is never carried by colour alone — colour + glyph + text label, always all three. The four
protected hexes (`#009E73` / `#E1B000` / `#D55E00` / `#7A8899`) live only in `lib/theme.ts`. Signed
figures always write an explicit `+` or `−`.

Two greps before any visual pass:

```sh
grep -rn "statusMeta(" components lib | grep -v components/ui/
grep -rn "#009E73\|#E1B000\|#D55E00\|#7A8899" components lib app | grep -v lib/theme.ts
```

| Audit | Status |
|---|---|
| Code audit (colour-alone, glyph+label) | Pass — 2026-09-12 |
| Grayscale pass, all 8 screens + login + access-denied | Not yet run |
| Deuteranopia / achromatopsia emulation | Not yet run |
| **Glove + daylight legibility, on the actual tablet** | **Not yet run** |

The last row is specific to this app and matters more than anywhere else on the platform: a
dash-mounted 10-inch tablet in northern daylight is the highest-glare, lowest-attention surface we
ship. The 44/56px touch floors and the glyph+label rule exist for that, and neither is verified
until someone holds the device outside with gloves on.

To run the emulation rows: Chrome DevTools → Rendering → Emulate vision deficiencies, with the
device toolbar at 1280×800. Not used, deliberately: axe-core / pa11y / Lighthouse — they catch the
automatable subset and none of the colour-alone failures that actually matter here.

## Out of scope

- **The rest of the offline batch.** The queue, its drain loop, `navigator.storage.persist()`
  and the ledger cache are real; the local-first **read** store (`lib/sync/store.ts`) still
  proxies `lib/data.ts`, and every kind but the four `hos.*` ones is held with no sender.
- **Every live domain API call except hours.** Auth and `/api/drivers/me` + `…/hos/*` only.
- **What the hours still leave to the console:** amendments, transcription, acknowledging /
  rejecting an exception, deferrals, recording or lifting an out-of-service declaration,
  resolving an ELD malfunction, findings and the prior-records attestation — all
  ComplianceAccess or dispatch acts. The ELD report sends `vehicleId: null`: the mock fleet's
  ids are not the server's, and a wrong id on a compliance record is worse than none.
- **Server-side `Idempotency-Key` on the compliance routes.** The client sends it on every
  `hos.exception` and `hos.eld-malfunction` command; only `…/hos/duty-events` honours it today
  (backend ask: a per-module `processed_commands` table), so a declaration resent after a
  status-0 round can duplicate until it does.
- **A `ui-tablet` field set.** The four compliance sheets type into the copied 40px `Field.tsx`
  inputs, under the touch floor; see "Tablet layout is app-local".
- **Badge scanning hardware.** The scan field is a text input, which is exactly right for a
  hardware HID/Bluetooth scanner (they present as a keyboard). **If the badges are NFC rather than
  barcode this is the wrong mechanism** — Web NFC (`NDEFReader`) is Chrome-on-Android only, HTTPS
  and user-gesture gated, and `BarcodeDetector` is camera-based and Chromium-only. **Confirm the
  badge technology before the offline batch**; §5.2's entire premise depends on the answer.
- **Kiosk / MDM lockdown.** An installed WebAPK can be pinned via Android managed configuration but
  is not an MDM-pushed APK. Confirm with whoever runs the tablets.
- **Photo attachments.** No attachment endpoint exists for inspections or defects — only driver
  credentials have object storage. Rendered disabled with the reason on screen rather than omitted.
- **The incidents API.** `Backend/src/Incidents` is a two-file stub: DI wired, no domain, no
  endpoint. The form's shape is not a contract.
- **Fuel log.** Fuel exists only as three fields inside a DVIR submission. No resource to read or
  write.
- **Per-stop arrive/depart.** The API has whole-trip `InProgress` → `finish` and nothing else — no
  per-stop actuals anywhere.
- **Atomic trip claiming.** §5.4 requires a server-validated, race-free claim with eligibility
  re-checked at claim time. The backend has `POST /api/trips/{id}/assign` with no concurrency guard
  and no eligibility engine. `Trips.tsx` says so on screen, because it would otherwise look
  authoritative in a demo.
- **The §5.4 hide-vs-grey deviation.** §5.4 says an ineligible driver should never *see* an Open
  trip. This scaffold greys them and names the failing rule, because a screen that hides rows
  cannot demonstrate the engine it exists to prove. The real implementation filters server-side.
- **Server enforcement of the pre-trip boarding gate.** `lib/inspectionGate.ts` hard-blocks Board
  and No-show until a pre-trip is certified for the trip's vehicle today, and it is a **UX gate on
  one device** — no backend endpoint requires a certified pre-trip before a manifest write. Named
  on screen in two places (the Manifest/Today banner, and the review step) rather than left
  implied, because a demo that looks authoritative here is worse than one that is visibly a
  scaffold. It is also deliberately **not** a sixth §5.4 eligibility rule: §5.4 asks whether a
  driver may *claim* an Open trip, this asks whether they may *start moving crew* on one they
  already hold, and conflating them would grey out every Open trip. Remaining backend ask: the
  precondition on the manifest write. (The other ask — a tri-state for `ChecklistItemInput.Passed`,
  so an N/A row need not be omitted — **landed**: `ChecklistItemState` is `Ok | Defect |
  NotApplicable` and every row now carries `state` and `note`.)
- **Mutating `Vehicle.hasFailedDvir` from a local certification.** `lib/data.ts` is read-only by
  rule, and making eligibility rule 3 read `inspectionStore` would create a
  `data.ts → inspectionGate.ts → data.ts` import cycle. The server owns the flag; the consequence
  is named on screen instead.
- **GPS / position ingest.** No endpoint exists.
- **Push notifications.** No registration endpoint; `/api/notifications` is DispatchAccess email.
- **Flutter.** Reversed 2026-09 — see the architecture skill's decision register.
