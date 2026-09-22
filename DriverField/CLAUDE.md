# Driver Field App — `DriverField/`

Next.js 16 + React 19 on port **3004**, against the shared API. An installable **PWA** for a
company-issued 10-inch landscape Android tablet.

**What is real:** authentication and the Driver role gate.
**What is not:** everything else. This app calls **no domain endpoint**. Every value on every
screen comes from `lib/data.ts`, and every screen carries a `MockTag` saying so.

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
lockfile. So **23 files here are byte-identical copies of Dispatcher's**, each carrying a fixed
two-line header:

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
| `app/` | `globals.css` |
| `components/` | `HeaderClock.tsx` |
| `components/ui/` | `Button` `Chip` `CorridorStepper` `Field` `FileField` `ImageUploadField` `MetricTile` `ModalShell` `MonthGrid` `Pager` `Panel` `PeriodNav` |

The one-command drift check, from the repo root — prints nothing when in step:

```sh
for f in \
  lib/theme.ts lib/format.ts lib/period.ts lib/useToday.ts lib/clipboard.ts \
  lib/inspectionForm.ts \
  lib/api/transport.ts lib/api/format.ts lib/api/shared.ts app/globals.css \
  components/HeaderClock.tsx \
  components/ui/Button.tsx components/ui/Chip.tsx components/ui/CorridorStepper.tsx \
  components/ui/Field.tsx components/ui/FileField.tsx components/ui/ImageUploadField.tsx \
  components/ui/MetricTile.tsx components/ui/ModalShell.tsx components/ui/MonthGrid.tsx \
  components/ui/Pager.tsx components/ui/Panel.tsx components/ui/PeriodNav.tsx
do
  diff -q <(tail -n +3 "DriverField/$f") "Dispatcher/$f" >/dev/null 2>&1 || echo "DRIFT: $f"
done
```

`tail -n +3` skips the two-line header. Copies stay **unpruned** — `CorridorStepper`, `MonthGrid`,
`PeriodNav`, `ImageUploadField`, `theme.ts`'s `ServiceType`/`DutyStatus` and `inspectionForm.ts`'s
`NL_PTI_01`/`PRE_TRIP_VALIDITY_HOURS` all ship unused or only partly used. Dead exports cost
nothing; a broken byte-diff costs the premise.

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

That split is what lets 23 files stay byte-identical while this app renders at roughly twice the
physical size of a desktop console. `lib/tablet.ts` exports geometry only: `touch` (44 floor / 56
primary / 64 rail), `bar.height` 72, `rail` 200-88 with 44px tiles, a type scale, `gap`, `radius`,
`wizard` (the DVIR flow's one-question-per-screen geometry — 168px answer tiles, a 40px question,
an 88px footer, a 6px bar), and `viewport` 1280×800.

`components/ui-tablet/` holds enlarged primitives. It is **not** a fork of `components/ui/*`:
where a copied primitive works at size (`Chip`, `Panel`, `ModalShell`, `MetricTile`) use the copy.
Add here only when the desktop one is geometrically unusable — `TouchButton` (the copy is ~34px),
`AnswerButton` (a 300×168 answer tile with a 34px glyph — not a prop on `StatusButton`, which is
56px and padded to sit two in a card row), `DutyControl`, `TouchTile`, `StatusBanner`, `SyncPill`.

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

Offline-first is a hard requirement (architecture non-negotiable #7) and is **staged, not
dropped**. Coverage between Thompson, Lynn Lake and the Alamos site is patchy by geography, not as
a corner case.

`lib/sync/` ships now with real types and no-op bodies: `types.ts` (`QueuedCommand`, `CommandKind`,
`SyncState`), `queue.ts` (`enqueue`/`drain`/`pending`/`retry`), `status.ts` (cached snapshot +
subscribe), `store.ts` (local-first reads, proxying `lib/data.ts` today).

> **Every screen mutation goes through `lib/sync/queue.enqueue()`. Never a direct `lib/api/*`
> call, never an in-place mutation of a `lib/data.ts` array.**

Hold that and the offline batch changes four files under `lib/sync/` and **zero screens**. Break it
in one screen and it is a rewrite.

`SyncPill` reads only `lib/sync/status.ts`, so it ships today and never changes again. It says
**"Sync off"** in neutral gray, never a green check — a scaffold that renders a reassuring
"Synced" while syncing nothing is exactly how a hard requirement dies between a demo and a
deployment.

**`lib/inspectionStore.ts` is deliberately NOT under `lib/sync/`, and the distinction is the
point.** An in-progress DVIR **draft** is uncertified, private to the device, and costs a driver the
whole NL-PTI-01 walk-around (67–80 questions, depending on unit and mode) if lost — so it lives
in `localStorage`, synchronously, and survives a
reload. A **certified** DVIR is the compliance record, and its home is the offline batch's
IndexedDB queue with `navigator.storage.persist()`. Putting a draft under `lib/sync/` would make
it look like the queue and invite the offline batch to migrate it; putting a queued DVIR in
`localStorage` would be exactly the compliance failure named below. The transition between the two
states is one function, and the order is not negotiable: **`enqueue()` → `recordCertification()` →
`discardDraft()`** — if `enqueue` throws, the driver's whole walk-around survives. `commandId` on a local
certification is the offline batch's hook: `drain()` can mark one server-accepted by that id and
the "Held on this device" banner starts telling a different truth with no API change.

Three things the offline batch must not miss:
- **`Idempotency-Key`.** Every `QueuedCommand` already carries a client-generated GUID. No backend
  endpoint honours an idempotency key today — none. The client sends it from day one so the server
  can start honouring it with no client change; the server side needs a per-module
  `processed_commands` table.
- **IndexedDB, not SQLite.** §8 of the architecture reference says SQLite because it was written
  for Flutter. Everything else in §8 survives verbatim.
- **`navigator.storage.persist()`.** IndexedDB is evictable unless persistence is granted. A queued
  DVIR vanishing because the browser reclaimed space is a **compliance** failure, not a lost draft.

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

Vitest. `vitest.config.mts` is copied from Budgeting **exactly**, and two things about it are
load-bearing: the **`.mts` extension** (Vite loads a plain `.ts` config as CJS and warns) and
**`import.meta.dirname`, never `__dirname`** — `@types/node` declares `__dirname` globally, so a
"fix" to it leaves TypeScript and `next build` green while every `@/lib/...` import in the suite
fails to resolve at run time.

| File | Why it exists |
|---|---|
| `lib/roles.test.ts` | The rule. Acceptance criterion: **an Accountant account is rejected** |
| `lib/claims.test.ts` | Claim names pinned to `JwtAccessTokenIssuer`; non-ASCII round trip |
| `lib/accessSeam.test.ts` | The **chain**, unmocked: token → claims → role → gate. A rename on either side of that seam leaves the other three files green and locks every driver out |
| `components/RoleGate.test.tsx` | The gate applies the rule. Every denial asserts the denial screen **renders**, not just that children are absent |
| `lib/eligibility.test.ts` | Each of the five §5.4 rules failing in isolation, and the verdict being conjunctive |
| `lib/hos.test.ts` | CVDHS thresholds on **both sides** of every boundary. An off-by-one in a compliance figure is not cosmetic |
| `lib/wire.test.ts` | Exact spelling of the duty and source strings against `HosDisplay`'s constants, and of the three inspection enums (`InspectionDefectSeverity`, `InspectionType`, `InspectionSource`) — including `"Out of Service"` → `"OutOfService"` |
| `lib/sync/queue.test.ts` | Distinct client-generated id per `enqueue`; the no-op still satisfies the `SyncState` contract |
| `lib/inspectionForm.copy.test.ts` | The NL-PTI-01 copy is byte-identical to `Dispatcher/lib/inspectionForm.ts` below its two-line header, read from disk. Drift means the tablet and the console collect **different legal forms** |
| `lib/inspectionSteps.test.ts` | The DVIR wizard's step model: unique item keys, the four real denominators (NL-01 pre 67, NL-01 post 73, NL-02 pre 74, NL-02 post 80) **derived, never written as a literal**, and a denominator that cannot be inflated however many defect follow-ups are injected |
| `lib/inspectionStore.test.ts` | The draft: per-mode-per-vehicle keys, a reload, a version bump and a stale service day both discarding rather than migrating, unknown item ids dropped on load, and hostile storage failing honestly |
| `lib/inspectionGate.test.ts` | `deriveResult` against `VehicleInspection.DeriveResult` and `odometerError` against `Vehicle.RecordOdometer`, both sides of each boundary; the boarding gate in both directions; **the negative pin** that this is deliberately *not* a sixth §5.4 rule |
| `components/screens/Manifest.test.tsx` | The gate as a driver meets it — both buttons disabled with the reason **on screen**, the not-server-enforced admission present, and **a badge scan boarding nobody** |

The governing rule, same as Budgeting's: **anything in this app that re-derives a server rule
client-side belongs here, with the C# method it mirrors named in the test's comment.**

## Data: everything is mock

`lib/data.ts` follows the documented conventions — flat exported `const` arrays, prefixed string
ids, ISO date strings never `Date` objects, every status-bearing row carrying its own `StatusKind`,
derived arrays computed rather than duplicated, threshold logic in `data.ts` rather than in a
screen. Row shapes live in `lib/types.ts`.

**No screen invents an API shape.** The backend owns endpoint shapes; each array is replaced by
additions to a `lib/api/<domain>.ts` written against the real response as its slice lands, and
screens keep their props.

Budgeting keys its mock rows to ids no real record can match, so screens already on real data show
empty states. That needs real data to work against and this app has none — so instead **every
screen carries a `MockTag`.** Nothing here should be demonstrable as working software.

The highest-value function in the file is **`eligibility()`** — the five §5.4 rules. It is a
client-side mirror of a rule the server must own, in the same way Budgeting's `previewPeriod`
mirrors `BudgetPeriod.Create`.

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

- **The offline queue itself.** The seam ships; the IndexedDB store, the drain loop and
  `navigator.storage.persist()` do not.
- **Every live domain API call.** Auth only.
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
