# NorthernLink.Shared.HoursOfService

A pure, deterministic rule engine for Manitoba's Drivers Hours of Service Regulation (M.R. 72/2007,
which adopts federal SOR/2005-313). Both the Drivers module (display, violations, audit) and the
Trips module (assignment / start gate on its replica) call it, so the two can never disagree on a
boundary. That is why it lives in Shared — a deliberate exception to "no business logic in Shared",
recorded in the architecture reference, Section 13.

## Purity contract (enforced by `HoursOfServicePurityTests`)

- **System-only.** No reference to any other `NorthernLink.*` namespace — not even
  `Shared.Kernel`. Defects are reported as `HosLedgerProblem`s, never Kernel `Error`s.
- **No clock.** No `DateTime.UtcNow`/`Now`, no `TimeZoneInfo.Local`. Every "now" is the
  `asOfUnix` argument; the calendar resolves its IANA zone by id.
- **No I/O, no `Environment`, no `Random`, no `async`/`Task`.**
- **No floating point.** Durations are integer seconds, instants are Unix seconds (`long`),
  odometers are tenths of a kilometre (`Decikm`). The TypeScript mirror
  (`Dispatcher/lib/hos/engine.ts`) uses the same integers and is held to the same fixtures
  (`tests/NorthernLink.Shared.Tests/HoursOfService/hos-fixtures.json`).
- **Limits are inclusive.** Exactly 13h00m00s of driving is lawful; one second more is not.
  Exactly at a ceiling, `CanDriveNow` is already false.

## Rules and their sections

| `HosRule` | Section | What it checks |
|---|---|---|
| `DailyDriving13h` | s.12(1) | driving in a day ≤ 13h (+ adverse extension) |
| `DailyOnDuty14h` | s.12(2) | no driving once 14h on duty in a day (+ extension); on-duty-not-driving past 14h is **not** a violation |
| `ShiftDriving13h` | s.13(1) | driving since the last ≥ 8h off run ≤ 13h (+ extension) |
| `ShiftOnDuty14h` | s.13(2) | no driving once 14h on duty in the shift (+ extension) |
| `ShiftElapsed16h` | s.13(3) | no driving once 16h have elapsed since the shift began (+ extension) |
| `DailyOffDuty10h` | s.14 | ≥ 10h off in a completed day, counting only blocks ≥ 30 min; floor 8h under adverse conditions; today reports a shortfall and breaches early once it can no longer be made up |
| `Rest24hIn14d` | s.25 | an off run with ≥ 24h inside the rolling 336h window |
| `Cycle1_70hIn7d` | s.26 / s.28 | on duty in any 7 days ≤ 70h, counting only time after the last ≥ 36h off run |
| `DeferralConditions` | s.16 | day-one / day-two conditions of a CO-approved off-duty deferral |
| `PcExceeds75km` | s.1 "personal use" | personal conveyance over 75.0 km in a day; the remainder is driving from the linear crossing instant |
| `PcOdometerMissing` | s.1 | PC without start and end odometers is driving |
| `PcWhileOutOfService` | s.91 | PC while out of service is driving |
| `DroveWhileOutOfService` | s.91 | any driving inside an out-of-service span |
| `RodsNotCertified` | s.84 | a completed day with own events not certified (never blocking) |
| `RecordsIncomplete14d` | s.86 | known-from inside the last 14 days, or a completed day with no records (blocking) |
| `AdverseDeclaredOutsideShift` | s.76 | an adverse-conditions declaration outside a shift is ignored |
| `ConflictingSources` | s.82 | two sources recorded different statuses at the same instant |

`HosViolation.AtUnix` is the instant the figure reached the ceiling — the last lawful second.
For `PcExceeds75km` the figure and limit are tenths of a kilometre, not seconds.

## Interpretation choices worth knowing

- Time between `KnownFromUnix` and the first own event is treated as off duty. A day in that span
  with a declared other-carrier row is laid out from the day start — declared driving, then the
  rest of the declared on-duty time, then off — so the untimed hours count against every rule
  and the night earns only the rest the declaration leaves room for. A row dated after the
  driver's own records begin is flagged and its hours are added to the day's totals only.
- An open personal-conveyance segment with a start odometer but no end yet is PC in progress
  with 0 km — it is reclassified only once a closing event without an odometer appears.
- `Project` evaluates with adverse extensions cut off at as-of, so a declared extension covers
  what has already been driven but never projected time.
- `RequiredOffBeforeStartSeconds` is a counterfactual: the ledger is overwritten with one off
  run of that length ending at the first leg's start, even if that reaches into the past.
