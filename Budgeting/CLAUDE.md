# Budgeting — Zero-Based Budgeting Console

Next.js 16 app on port **3003**, consuming the shared API. Scaffolded by US-6.0.1 (Track 6,
Stage 6.0). Everything on screen is mock data until Stage 6.1 builds the Budgeting domain
library — see [Mock data](#mock-data).

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
  lib/api/transport.ts lib/api/format.ts app/globals.css \
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
| `lib/api/transport.ts`, `lib/api/format.ts` | same paths | yes |
| `app/globals.css` | `Dispatcher/app/globals.css` | yes |
| `components/ui/*` (12 files) | `Dispatcher/components/ui/*` | yes |
| `components/HeaderClock.tsx`, `components/NavRail.tsx` | same paths | yes |
| `app/layout.tsx` | `Dispatcher/app/layout.tsx` | **no** — title/description only; the four Google Fonts `<link>` tags are byte-identical and must stay that way |
| `lib/auth.ts` | `Dispatcher/lib/auth.ts` | **no** — see below |
| `components/TopBar.tsx`, `AuthGate.tsx`, `LoginScreen.tsx`, `Console.tsx` | same paths | **no** — adapted |
| `lib/nav.ts`, `lib/data.ts`, `lib/types.ts`, `lib/claims.ts`, `lib/roles.ts` | — | new |
| `components/Brandmark.tsx`, `ErrorNotice.tsx`, `RoleGate.tsx`, `AccessDeniedScreen.tsx`, `SetupPendingScreen.tsx`, `screens/*` | — | new |

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

That is acceptable *today* because there is nothing behind it but mock data. The real boundary is
the `BudgetAccess` authorization policy in
`Backend/src/Api/NorthernLink.Api/Auth/AuthorizationPolicyRegistration.cs` — registered and unit
tested, but attached to no endpoint, because no budgeting endpoints exist until Stage 6.1.
**When they land, they must carry that policy.** Nothing in this app may treat passing `RoleGate`
as proof of anything.

`lib/roles.ts` mirrors `Roles.BudgetAccess` in `Backend/src/Shared/Kernel/Roles.cs`. Keep them in
step; both have tests that fail if they diverge.

## Testing

Vitest, added here as a **pilot for this app only** — it does not obligate Dispatcher to adopt
it. Three files, and only one needs a DOM:

- `lib/roles.test.ts` — US-6.0.1's acceptance criterion: a Dispatcher account is rejected.
- `lib/claims.test.ts` — JWT decoding, including the non-ASCII round trip (`atob` yields a binary
  string, so a naive decoder mangles accented emails) and malformed-token handling.
- `components/RoleGate.test.tsx` — the gate applies the rule and offers a way out.

The server-side counterpart is
`Backend/tests/NorthernLink.Api.Tests/AuthorizationPolicyTests.cs`. Both exist on purpose: the
backend test proves the *policy* rejects Dispatcher, this one proves the *console* does, and in
Stage 6.0 the console is the gate a user actually meets.

## Mock data

`lib/data.ts` is the only data source. Conventions follow `Dispatcher/lib/data.ts`: flat exported
const arrays, string ids, ISO date strings (never `Date` objects), whole-dollar numbers rendered
through `formatCad`, and a `StatusKind` carried on every status-bearing row so rendering is a
pure lookup.

No screen invents an API shape. When Stage 6.1 lands, `lib/data.ts` is replaced by
`lib/api/budgeting.ts` and the screens keep their props.

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

## Out of scope (Stage 6.1 and later)

- The Budgeting backend domain library. **Do not create `Backend/src/Budgeting/`** — the
  architecture tests cross-check `ModuleGraph.DomainNames` against disk and an unlisted folder
  fails the build.
- `BudgetCode` / `BudgetPeriod` / `BudgetAllocation` / `ActualTransaction` tables and their RLS
  policies; QuickBooks actuals reconciliation.
- CI/CD and the OVHcloud deployment target — there is no `.github/` anywhere in this repo yet;
  that is a platform-wide story covering every app at once.
- OIDC/OpenIddict; the `SuperUser` claim from architecture Section 6.1.
