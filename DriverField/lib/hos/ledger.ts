// ---------------------------------------------------------------------------
// The duty ledger — the tablet's planning copy of the driver's hours. LIVE.
//
// One module-level store, read from React through useSyncExternalStore (lib/hos/useHosLedger.ts),
// the same shape as lib/sync/status.ts + SyncPill and lib/inspectionStore.ts + useInspectionStore.
// It holds the server's last 14 days of duty days for the signed-in driver, this tablet's
// not-yet-confirmed duty changes, and the as-of instant, and exposes `evaluation()` — the
// engine's verdict (lib/hos/engine.ts, the mirror of Backend/src/Shared/HoursOfService) on
// that ledger at that instant. Every HOS figure on every screen reads this one evaluation:
// Today's tile, the Hours tiles and bar, the top-bar duty chip, the DutyControl's disabled
// reasons, the alerts, and the Trips screen's hours-of-service rule (lib/eligibility.ts,
// through `project`). There is no second band set and no second arithmetic.
//
// WHERE THE EVENTS COME FROM:
//   - The SERVER: GET /api/drivers/{id}/hos/days (the last 14 regulation days, every effective
//     event, who recorded it, the exceptions in force and any out-of-service span on each
//     day), GET …/hos/status (known-from, the server's own evaluation, the out-of-service
//     declaration in force now, whether an ELD malfunction is open, the prior-records
//     attestation) and GET …/hos/exceptions (every status, so a rejected one still shows),
//     fetched on sign-in and again after every drain that sent something. The driver id is
//     GET /api/drivers/me, resolved once and cached. Server events win: nothing here edits one.
//   - THIS TABLET: a duty change appends an OPTIMISTIC event (status, now, "Driver App", the
//     queued command's id as `clientEventId`) so the figures move the moment the driver taps.
//     The command goes through lib/sync/queue.enqueue() as always, drain() sends it with the
//     same id as the eventId, and the next hydrate reconciles: an optimistic event whose id the
//     server now holds is dropped in favour of the server's copy; one whose command the server
//     REJECTED (parked "failed" on the Hours screen) is dropped too, because the server has
//     said it is not on the record. Pending ones are rebuilt from the queue on reload. A
//     personal-conveyance event carries its odometer so the engine can count the kilometres;
//     an other-carrier day's three events carry that source and the carrier's name. A queued
//     exception declaration is applied to the engine the same optimistic way (an adverse
//     extension moves the ceiling the moment it is declared), and a queued withdrawal removes
//     one; both go back to the record on the next hydrate.
//   - A CACHE: the last server copy is kept in IndexedDB (lib/sync/db.ts, `ledger` store) so a
//     reload in a dead zone still shows figures. `hydration` on the snapshot says which of the
//     three the screens are looking at, and when; the Hours screen prints it.
//
// THE OUT-OF-SERVICE SPAN and the exceptions reach the ENGINE as well as the screens, mapped
// exactly as the server's DutyLedgerAssembler maps them (an exception applies from the instant
// the conditions arose, EffectiveFromUtc, not from when it was written; an out-of-service span
// runs from its declaration to its lift or its until). That is what makes the DutyControl grey
// Driving with "Out of service" from the same evaluation as everything else, rather than from
// a flag a screen checks on its own.
//
// Every figure is a PLANNING FIGURE. The record of duty status is on the bus's ELD, and keeping
// two records is a severe violation; the banner on Hours is not optional.
//
// LOOKBACK AND RETENTION: the fetch asks for the same 21 days the server's own evaluation
// assembles (DutyLedgerAssembler.LookbackDays — the 14-day rules plus a week so the 7-day cycle
// and the 36h reset have what they need at the window's edge), and events older than that
// before as-of are dropped at a day boundary. The server's days each begin with their own
// event, so a cut at a day boundary loses nothing; time between `knownFromUnix` (the server's:
// the driver's earliest day on file) and the first fetched event is synthesised by the engine
// exactly as it does for any known-from gap. Time before `knownFromUnix` is unknown, never
// free. The Hours history shows the last 14 of those days.
//
// THE CLOCK is the device's: nowUnix() is Date.now(). The alerts tick it every 30 seconds
// (lib/hos/useHosAlerts.ts) so the figures count down while the screen sits still.
// ---------------------------------------------------------------------------

import {
  certifyDay,
  declareHosException,
  getHosStatus,
  getMyDriver,
  listDutyDays,
  listHosExceptions,
  recordDutyEvent,
  reportEldMalfunction,
  withdrawHosException,
  type DutyDaySummary,
  type DutyEventRecord,
  type HosExceptionKindWire,
  type HosExceptionRecord,
  type HosExceptionStatus,
  type HosStatus,
  type OutOfServiceRecord,
} from "../api/hos";
import { ApiError } from "../api/transport";
import * as db from "../sync/db";
import { allCommands, onDrained, ready as queueReady, registerSender, statusOf, subscribeQueue } from "../sync/queue";
import type { QueuedCommand } from "../sync/types";
import { eldMalfunctionInputFrom, exceptionInputFrom, isExceptionCommandPayload } from "./complianceCommand";
import { dutyEventInputFrom, isCertifyCommandPayload, isDutyCommandPayload, isOtherCarrierPayload, kmOrNull } from "./dutyCommand";
import {
  addDays,
  evaluate,
  HosCalendar,
  HosWire,
  type DateOnly,
  type DutyEvent,
  type DutyLedger,
  type DutyStatus,
  type EventSource,
  type HosEvaluation,
  type HosException,
  type OutOfService,
} from "./engine";

/** The regulation's calendar (s.1): America/Winnipeg, day starts at local midnight. */
export const CALENDAR = new HosCalendar();

/**
 * How many regulation days the store fetches and keeps behind as-of — the server's
 * `DutyLedgerAssembler.LookbackDays` (21), so the tablet evaluates the same window the server
 * does. The history on the Hours screen shows the last HISTORY_DAYS of them.
 */
export const RETAINED_DAYS = 21;

/** The regulation's 14-day record-keeping window, as the Hours history lists it. */
export const HISTORY_DAYS = 14;

/**
 * An engine event plus where it came from: `eventId` is the server's id for an event on
 * file (null for an optimistic one not yet confirmed); `clientEventId` is the queued
 * command's id for an event this tablet recorded and the server has not yet echoed back
 * (null for one from the server).
 */
export interface DutyEventLike extends DutyEvent {
  eventId: string | null;
  clientEventId: string | null;
}

/**
 * An exception as the Hours screen lists it: a server record, or a declaration queued on this
 * tablet (`commandId` set, `id` = the command's id, status Declared) that the server has not
 * answered yet. `withdrawalQueued` is a withdrawal of a server record held on this tablet.
 */
export interface ExceptionLike {
  id: string;
  commandId: string | null;
  kind: HosExceptionKindWire;
  status: HosExceptionStatus;
  effectiveFromUnix: number;
  reason: string;
  location: string | null;
  extraDrivingMinutes: number | null;
  withdrawalQueued: boolean;
}

export type HydrationSource = "none" | "cache" | "server";

export interface HydrationState {
  /** What the figures are built on: nothing yet, the device's cached server copy, or the live server copy. */
  source: HydrationSource;
  /** When that copy was fetched from the server, Unix seconds; null for "none". */
  atUnix: number | null;
  /** The last fetch failure, verbatim from the server or the transport; null after a success. */
  error: { code: string; message: string } | null;
}

export interface LedgerSnapshot {
  readonly ledger: DutyLedger;
  readonly events: readonly DutyEventLike[];
  /** The server's day summaries (live or cached), oldest first — what the Hours history renders. */
  readonly days: readonly DutyDaySummary[];
  readonly status: HosStatus | null;
  /** Every exception in the window (server records at any status, plus declarations queued here), newest first. */
  readonly exceptions: readonly ExceptionLike[];
  /** The out-of-service declaration in force at as-of, or null. */
  readonly outOfService: OutOfServiceRecord | null;
  /** An ELD malfunction is open on the server, or a report is queued on this tablet: keep paper RODS. */
  readonly paperLogMode: boolean;
  readonly hydration: HydrationState;
  readonly asOfUnix: number;
  readonly evaluation: HosEvaluation;
}

type Listener = (snapshot: LedgerSnapshot) => void;
const listeners = new Set<Listener>();

// --- state -----------------------------------------------------------------

interface ServerCopy {
  days: DutyDaySummary[];
  status: HosStatus;
  exceptions: HosExceptionRecord[];
  fetchedAtMs: number;
}

/** The IndexedDB row. Keyed to the user so a different account on the same tablet never sees it. */
interface LedgerCache extends ServerCopy {
  key: "ledger";
  userId: string | null;
  driverId: string;
  /** The roster name from /me — what an other-carrier declaration carries as `enteredBy`. Absent on rows written before it was kept. */
  driverName?: string | null;
}

/** Test seam: a raw ledger installed in place of a server copy's events. */
interface SeedLedger {
  knownFromUnix: number;
  events: DutyEventLike[];
  certifiedDays: DateOnly[];
}

let userId: string | null = null;
let driverId: string | null = null;
let driverName: string | null = null;
let server: ServerCopy | null = null;
let seed: SeedLedger | null = null;
let optimistic: DutyEventLike[] = [];
let hydration: HydrationState = { source: "none", atUnix: null, error: null };

const NO_HYDRATION: HydrationState = { source: "none", atUnix: null, error: null };

// --- building the snapshot -------------------------------------------------

function unixOf(iso: string): number {
  return Math.floor(Date.parse(iso) / 1000);
}

function unixOrNull(iso: string | null | undefined): number | null {
  if (typeof iso !== "string") return null;
  const unix = unixOf(iso);
  return Number.isFinite(unix) ? unix : null;
}

/** km with one decimal → tenths of a km, rounded half away from zero as DutyLedgerAssembler.ToDecikm does. */
function decikm(km: number | null): number | null {
  return km === null ? null : Math.round(km * 10);
}

/** One server event → the engine's shape. Mirrors DutyLedgerAssembler: the opener of a PC
 *  segment carries its start odometer, every other event its end odometer. */
function engineEventFrom(e: DutyEventRecord): DutyEventLike | null {
  const status = HosWire.statusFrom(e.status);
  const source = HosWire.sourceFrom(e.source);
  if (status === null || source === null) return null;
  return {
    atUnix: unixOf(e.atUtc),
    status,
    source,
    sequence: e.sequence,
    odometerDecikm: decikm(status === "PersonalConveyance" ? e.odometerStartKm : e.odometerEndKm),
    note: e.note,
    eventId: e.eventId,
    clientEventId: null,
  };
}

function serverEvents(days: readonly DutyDaySummary[]): DutyEventLike[] {
  const events: DutyEventLike[] = [];
  for (const day of days) {
    if (!day.hasRecords) continue;
    for (const e of day.events) {
      if (e.isVoided) continue;
      const mapped = engineEventFrom(e);
      if (mapped !== null) events.push(mapped);
    }
  }
  return events;
}

const APPLYING: readonly HosExceptionStatus[] = ["Declared", "Acknowledged", "Approved"];

/**
 * One server exception → the engine's record, or null when it no longer applies. Mirrors
 * DutyLedgerAssembler.ToEngineException: adverse conditions and an emergency apply from the
 * instant the conditions arose (effectiveFromUtc); a deferral's day one is the regulation day
 * its effective-from falls in.
 */
function engineExceptionFrom(e: HosExceptionRecord): HosException | null {
  if (!APPLYING.includes(e.status)) return null;
  const from = unixOrNull(e.effectiveFromUtc);
  if (from === null) return null;
  const base = { id: e.id, tripId: e.tripId, reason: e.reason };
  switch (e.kind) {
    case HosWire.AdverseDrivingConditionsKind:
      return { ...base, kind: "AdverseDrivingConditions", declaredAtUnix: from, extensionSeconds: (e.extraDrivingMinutes ?? 0) * 60 };
    case HosWire.EmergencyKind:
      return { ...base, kind: "Emergency", declaredAtUnix: from, endedAtUnix: unixOrNull(e.effectiveToUtc) };
    case HosWire.OffDutyDeferralKind:
      return {
        ...base,
        kind: "OffDutyDeferral",
        declaredAtUnix: unixOrNull(e.declaredAtUtc) ?? from,
        dayOne: CALENDAR.dayOf(from),
        deferredSeconds: (e.deferredOffDutyMinutes ?? 0) * 60,
        approvedByComplianceOfficer: e.approvedByUserId ?? "",
      };
    default:
      return null;
  }
}

/** The out-of-service record → the engine's span, as DutyLedgerAssembler.ToEngineSpan does. */
function engineSpanFrom(r: OutOfServiceRecord): OutOfService | null {
  const from = unixOrNull(r.fromUtc);
  if (from === null) return null;
  return { fromUnix: from, untilUnix: unixOrNull(r.untilUtc), reason: r.reason };
}

function isHeld(status: QueuedCommand["status"]): boolean {
  return status === "pending" || status === "sending";
}

/** The exception commands held on this tablet, in capture order. */
function heldExceptionCommands(): QueuedCommand[] {
  return allCommands().filter((c) => c.kind === "hos.exception" && isHeld(c.status) && isExceptionCommandPayload(c.payload));
}

function exceptionLikeFrom(record: HosExceptionRecord, withdrawalQueued: boolean): ExceptionLike | null {
  const from = unixOrNull(record.effectiveFromUtc);
  if (from === null) return null;
  return {
    id: record.id,
    commandId: null,
    kind: record.kind,
    status: record.status,
    effectiveFromUnix: from,
    reason: record.reason,
    location: record.location,
    extraDrivingMinutes: record.extraDrivingMinutes,
    withdrawalQueued,
  };
}

/** Server exceptions plus the declarations queued here; withdrawals queued here are marked. */
function exceptionsNow(serverRecords: readonly HosExceptionRecord[]): { list: ExceptionLike[]; engine: HosException[] } {
  const held = heldExceptionCommands();
  const withdrawing = new Set<string>();
  for (const c of held) {
    const p = c.payload as { action: string; exceptionId?: string };
    if (p.action === "withdraw" && p.exceptionId) withdrawing.add(p.exceptionId);
  }

  const list: ExceptionLike[] = [];
  const engine: HosException[] = [];
  for (const record of serverRecords) {
    const like = exceptionLikeFrom(record, withdrawing.has(record.id));
    if (like === null) continue;
    list.push(like);
    if (like.withdrawalQueued) continue;
    const mapped = engineExceptionFrom(record);
    if (mapped !== null) engine.push(mapped);
  }

  for (const c of held) {
    const p = c.payload;
    if (!isExceptionCommandPayload(p) || p.action !== "declare") continue;
    const from = unixOrNull(p.effectiveFromUtc);
    if (from === null) continue;
    list.push({
      id: c.id,
      commandId: c.id,
      kind: p.kind,
      status: "Declared",
      effectiveFromUnix: from,
      reason: p.reason,
      location: p.location ?? null,
      extraDrivingMinutes: p.extraDrivingMinutes ?? null,
      withdrawalQueued: false,
    });
    const base = { id: c.id, tripId: p.tripId ?? null, reason: p.reason };
    if (p.kind === HosWire.AdverseDrivingConditionsKind) {
      engine.push({ ...base, kind: "AdverseDrivingConditions", declaredAtUnix: from, extensionSeconds: (p.extraDrivingMinutes ?? 0) * 60 });
    } else {
      engine.push({ ...base, kind: "Emergency", declaredAtUnix: from, endedAtUnix: null });
    }
  }

  list.sort((a, b) => b.effectiveFromUnix - a.effectiveFromUnix);
  return { list, engine };
}

/** Every out-of-service record the server copy mentions — in force now (status) or overlapping a day — once each. */
function outOfServiceRecords(copy: ServerCopy | null): OutOfServiceRecord[] {
  if (copy === null) return [];
  const byId = new Map<string, OutOfServiceRecord>();
  if (copy.status.outOfService) byId.set(copy.status.outOfService.id, copy.status.outOfService);
  for (const day of copy.days) {
    if (day.outOfService) byId.set(day.outOfService.id, day.outOfService);
  }
  return [...byId.values()];
}

function activeOutOfService(records: readonly OutOfServiceRecord[], asOfUnix: number): OutOfServiceRecord | null {
  for (const r of records) {
    const from = unixOrNull(r.fromUtc);
    const until = unixOrNull(r.untilUtc);
    if (from !== null && asOfUnix >= from && (until === null || asOfUnix < until)) return r;
  }
  return null;
}

function paperLogModeNow(copy: ServerCopy | null): boolean {
  if (copy?.status.eldMalfunctionOpen) return true;
  return allCommands().some((c) => c.kind === "hos.eld-malfunction" && isHeld(c.status));
}

function retainFromUnix(asOfUnix: number): number {
  return CALENDAR.dayStartUnix(addDays(CALENDAR.dayOf(asOfUnix), -RETAINED_DAYS));
}

function byInstant(a: DutyEventLike, b: DutyEventLike): number {
  return a.atUnix - b.atUnix || a.sequence - b.sequence;
}

function build(asOfUnix: number): LedgerSnapshot {
  const base = seed ? seed.events : server ? serverEvents(server.days) : [];
  const onFile = new Set(base.map((e) => e.eventId));
  const live = optimistic.filter((o) => !onFile.has(o.clientEventId));
  const cutoff = retainFromUnix(asOfUnix);
  const events = [...base, ...live].filter((e) => e.atUnix >= cutoff).sort(byInstant);

  // Known-from: the server's, or the first optimistic event when there is no server copy at
  // all (an offline first use) — never earlier than what something actually recorded. With
  // nothing at all, as-of: everything unknown, which the engine reads as NoLedger — blocked.
  const serverKnown = seed ? seed.knownFromUnix : server?.status.knownFromUtc ? unixOf(server.status.knownFromUtc) : null;
  const firstLive = live.length > 0 ? Math.min(...live.map((e) => e.atUnix)) : null;
  const candidates = [serverKnown, firstLive].filter((v): v is number => v !== null);
  const knownFromUnix = candidates.length > 0 ? Math.min(...candidates) : asOfUnix;

  const certifiedDays = seed ? seed.certifiedDays : server ? server.days.filter((d) => d.certified).map((d) => d.date) : [];

  const exceptions = exceptionsNow(server?.exceptions ?? []);
  const oosRecords = outOfServiceRecords(server);

  const ledger: DutyLedger = {
    driverId: driverId ?? "",
    knownFromUnix,
    events,
    otherCarrierDays: [],
    exceptions: exceptions.engine,
    certifiedDays,
    outOfService: oosRecords.map(engineSpanFrom).filter((s): s is OutOfService => s !== null),
    malfunctions: [],
  };

  return {
    ledger,
    events,
    days: server?.days ?? [],
    status: server?.status ?? null,
    exceptions: exceptions.list,
    outOfService: activeOutOfService(oosRecords, asOfUnix),
    paperLogMode: paperLogModeNow(server),
    hydration,
    asOfUnix,
    evaluation: evaluate(ledger, asOfUnix, CALENDAR),
  };
}

/**
 * The stable server-render snapshot: an empty ledger at the epoch. Pure, so the server and
 * the hydrating client agree. The Console never renders on the server (AuthGate shows a splash
 * until the session is restored client-side), so nothing reads this for its figures.
 */
const serverSnapshot: LedgerSnapshot = (() => {
  const ledger: DutyLedger = {
    driverId: "",
    knownFromUnix: 0,
    events: [],
    otherCarrierDays: [],
    exceptions: [],
    certifiedDays: [],
    outOfService: [],
    malfunctions: [],
  };
  return {
    ledger,
    events: [],
    days: [],
    status: null,
    exceptions: [],
    outOfService: null,
    paperLogMode: false,
    hydration: NO_HYDRATION,
    asOfUnix: 0,
    evaluation: evaluate(ledger, 0, CALENDAR),
  };
})();

let snapshot: LedgerSnapshot = serverSnapshot;

// --- reads -----------------------------------------------------------------

/** "Now" on the device clock, Unix seconds. */
export function nowUnix(): number {
  return Math.floor(Date.now() / 1000);
}

export function getSnapshot(): LedgerSnapshot {
  return snapshot;
}

export function getServerSnapshot(): LedgerSnapshot {
  return serverSnapshot;
}

/** The engine's verdict on the ledger as of the store's clock: `evaluate(ledger, asOf, calendar)`. */
export function evaluation(): HosEvaluation {
  return snapshot.evaluation;
}

/** The regulation day the store is evaluating. */
export function todayDate(): DateOnly {
  return CALENDAR.dayOf(snapshot.asOfUnix);
}

/** The driver id the senders act on, once known (from /me or the cache). Null before sign-in hydrates. */
export function knownDriverId(): string | null {
  return driverId;
}

/**
 * The driver's roster name (from /me or the cache) — the `enteredBy` an other-carrier
 * declaration carries. Null until the server has been reached once on this account; the Hours
 * screen disables that action with the reason rather than fall back to a mock name, because
 * `enteredBy` is a wire value on a compliance record.
 */
export function knownDriverName(): string | null {
  return driverName;
}

export function subscribe(listener: Listener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

function publish(next: LedgerSnapshot): void {
  snapshot = next;
  for (const listener of [...listeners]) listener(snapshot);
}

function republish(): void {
  publish(build(Math.max(snapshot.asOfUnix, nowUnix())));
}

// --- writes ----------------------------------------------------------------

/**
 * Appends one duty event and re-evaluates. This is the optimistic half of a duty change: the
 * caller has ALREADY enqueued the hos.duty command and passes its id as `clientEventId`, so the
 * next hydrate can match the server's accepted event to this one (or drop this one when the
 * server rejects it) without a screen knowing. A second append with the same id replaces the
 * first — the queue restore on reload goes through here too.
 *
 * `odometerDecikm` is the start reading on an event that opens personal conveyance and the end
 * reading on the event that closes it — the engine's convention, the same one engineEventFrom
 * applies to the server's events. `source` defaults to "Driver App"; an other-carrier day's
 * events pass "OtherCarrier".
 *
 * `atUnix` defaults to the store's clock. The as-of moves forward to the event (never back), so
 * an event is never "in the future" of its own evaluation — the engine would ignore it.
 */
export function appendDutyEvent(input: {
  status: DutyStatus;
  clientEventId: string;
  atUnix?: number;
  source?: EventSource;
  note?: string | null;
  odometerDecikm?: number | null;
}): DutyEventLike {
  const atUnix = input.atUnix ?? nowUnix();
  let sequence = 1;
  for (const e of snapshot.events) {
    sequence = Math.max(sequence, e.sequence + 1);
  }

  const event: DutyEventLike = {
    atUnix,
    status: input.status,
    source: input.source ?? "DriverApp",
    sequence,
    odometerDecikm: input.odometerDecikm ?? null,
    note: input.note ?? null,
    eventId: null,
    clientEventId: input.clientEventId,
  };
  optimistic = [...optimistic.filter((o) => o.clientEventId !== input.clientEventId), event];
  publish(build(Math.max(snapshot.asOfUnix, atUnix)));
  return event;
}

/** Moves as-of to the clock's now and re-evaluates. The alerts call it every 30 seconds. */
export function tick(): void {
  const now = nowUnix();
  if (now <= snapshot.asOfUnix) return;
  publish(build(now));
}

/**
 * Reconciles the snapshot with the queue: rebuilds the optimistic duty events from the held
 * hos.duty commands, drops the ones the server refused or the driver discarded, and
 * re-evaluates with the exception declarations, withdrawals and malfunction reports held now.
 * startLedger() runs this on every queue change; a test that enqueues without starting the
 * ledger calls it directly.
 */
export function syncWithQueue(): void {
  restoreOptimisticFromQueue();
  pruneOptimistic();
  republish();
}

// --- the queue: optimistic events come from it and go with it ---------------

/** Drops optimistic events whose command the server rejected or the driver discarded. */
function pruneOptimistic(): void {
  optimistic = optimistic.filter((o) => {
    const status = o.clientEventId === null ? undefined : statusOf(o.clientEventId);
    return status === "pending" || status === "sending" || status === "sent";
  });
}

/** Rebuilds the optimistic events from the queue's held hos.duty commands — a reload, or a change. */
function restoreOptimisticFromQueue(): void {
  const held = new Set(optimistic.map((o) => o.clientEventId));
  for (const command of allCommands()) {
    if (command.kind !== "hos.duty" || held.has(command.id)) continue;
    if (command.status === "failed" || !isDutyCommandPayload(command.payload)) continue;
    const status = HosWire.statusFrom(command.payload.duty);
    if (status === null) continue;
    const atUnix = unixOf(command.payload.atUtc);
    if (!Number.isFinite(atUnix)) continue;
    const payload = command.payload;
    const odometerDecikm = decikm(kmOrNull(status === "PersonalConveyance" ? payload.odometerStartKm : payload.odometerEndKm));
    optimistic = [
      ...optimistic,
      {
        atUnix,
        status,
        source: isOtherCarrierPayload(payload) ? "OtherCarrier" : "DriverApp",
        sequence: 0,
        odometerDecikm,
        note: typeof payload.note === "string" ? payload.note : null,
        eventId: null,
        clientEventId: command.id,
      },
    ];
  }
}

// --- the server -------------------------------------------------------------

let resolvingDriver: Promise<string> | null = null;

/**
 * The signed-in account's driver id: GET /api/drivers/me, cached for the session. When the
 * network is down and a cached copy named the driver, that id stands in — one company tablet,
 * one driver, and the cache is keyed to the account. A definitive answer (404 Drivers.NotLinked,
 * 403) is thrown as-is so the caller parks or shows it.
 */
export function resolveDriverId(): Promise<string> {
  if (resolvingDriver) return resolvingDriver;
  resolvingDriver = (async () => {
    try {
      const me = await getMyDriver();
      driverId = me.id;
      driverName = typeof me.name === "string" && me.name.trim().length > 0 ? me.name.trim() : null;
      return me.id;
    } catch (err) {
      if (driverId !== null && err instanceof ApiError && (err.status === 0 || err.status >= 500)) {
        return driverId;
      }
      throw err;
    } finally {
      resolvingDriver = null;
    }
  })();
  return resolvingDriver;
}

let hydrating: Promise<boolean> | null = null;

/**
 * Fetches the server copy and re-evaluates. Resolves true on success. On failure the last copy
 * (live or cached) stays, `hydration.error` carries the server's message, and the screens keep
 * their figures — a dead zone must never blank the Hours screen.
 */
export function hydrate(): Promise<boolean> {
  if (hydrating) return hydrating;
  hydrating = (async () => {
    try {
      const id = await resolveDriverId();
      // From RETAINED_DAYS ago to today (the server's default `to`), on the regulation's calendar.
      const from = addDays(CALENDAR.dayOf(nowUnix()), -RETAINED_DAYS);
      const [days, status, exceptions] = await Promise.all([listDutyDays(id, { from }), getHosStatus(id), listHosExceptions(id, { from })]);
      server = {
        days: [...days].sort((a, b) => (a.date < b.date ? -1 : a.date > b.date ? 1 : 0)),
        status,
        exceptions: Array.isArray(exceptions) ? exceptions : [],
        fetchedAtMs: Date.now(),
      };
      hydration = { source: "server", atUnix: Math.floor(server.fetchedAtMs / 1000), error: null };
      pruneOptimistic();
      republish();
      await saveCache(id, server);
      return true;
    } catch (err) {
      const error =
        err instanceof ApiError
          ? { code: err.code, message: err.message }
          : { code: "Client.Unknown", message: err instanceof Error ? err.message : String(err) };
      hydration = { ...hydration, error };
      republish();
      return false;
    } finally {
      hydrating = null;
    }
  })();
  return hydrating;
}

async function saveCache(id: string, copy: ServerCopy): Promise<void> {
  try {
    const row: LedgerCache = { key: "ledger", userId, driverId: id, driverName, ...copy };
    await db.put(db.LEDGER_STORE, row);
  } catch {
    // The live copy is on screen; only the next offline reload loses out.
  }
}

async function loadCache(): Promise<void> {
  try {
    const row = await db.get<LedgerCache>(db.LEDGER_STORE, "ledger");
    if (!row || row.userId !== userId) return;
    driverId = row.driverId;
    driverName = typeof row.driverName === "string" ? row.driverName : null;
    // A row written before the exceptions were fetched has none; the next hydrate fills them.
    server = { days: row.days, status: row.status, exceptions: Array.isArray(row.exceptions) ? row.exceptions : [], fetchedAtMs: row.fetchedAtMs };
    hydration = { source: "cache", atUnix: Math.floor(row.fetchedAtMs / 1000), error: null };
  } catch {
    // no cache — the first hydrate will fill it
  }
}

// --- senders: the four hos.* kinds the queue can deliver -----------------------

async function sendDuty(payload: unknown, commandId: string): Promise<void> {
  const input = dutyEventInputFrom(payload, commandId);
  const id = await resolveDriverId();
  await recordDutyEvent(id, input);
}

async function sendCertify(payload: unknown): Promise<void> {
  if (!isCertifyCommandPayload(payload)) {
    throw new Error("Not a certification command payload.");
  }
  const id = await resolveDriverId();
  await certifyDay(id, payload.date);
}

/** A declaration or a withdrawal, by the payload's `action`; the command id rides as the Idempotency-Key either way. */
async function sendException(payload: unknown, commandId: string): Promise<void> {
  if (!isExceptionCommandPayload(payload)) {
    throw new Error("Not an exception command payload.");
  }
  const id = await resolveDriverId();
  if (payload.action === "withdraw") {
    await withdrawHosException(id, payload.exceptionId, payload.note, { idempotencyKey: commandId });
    return;
  }
  await declareHosException(id, exceptionInputFrom(payload), { idempotencyKey: commandId });
}

async function sendEldMalfunction(payload: unknown, commandId: string): Promise<void> {
  const input = eldMalfunctionInputFrom(payload);
  const id = await resolveDriverId();
  await reportEldMalfunction(id, input, { idempotencyKey: commandId });
}

// --- lifecycle ---------------------------------------------------------------

/**
 * Brings the ledger up for the signed-in account: registers the four senders, restores held
 * duty changes from the queue, loads the cached server copy, then fetches a fresh one. Hooks
 * every drain that sent something to a re-fetch, and every queue change to a reconcile.
 * Returns the stop function. Called by components/Console.tsx on mount, after sign-in.
 */
export function startLedger(signedInUserId: string | null): () => void {
  userId = signedInUserId;
  const unregister = [
    registerSender("hos.duty", sendDuty),
    registerSender("hos.certify", sendCertify),
    registerSender("hos.exception", sendException),
    registerSender("hos.eld-malfunction", sendEldMalfunction),
  ];
  const unsubscribeQueue = subscribeQueue(syncWithQueue);
  const unsubscribeDrained = onDrained((result) => {
    if (result.sent > 0) void hydrate();
  });

  void (async () => {
    await loadCache();
    try {
      await queueReady();
      restoreOptimisticFromQueue();
    } catch {
      // no durable queue — nothing to restore
    }
    republish();
    await hydrate();
  })();

  return () => {
    for (const stop of unregister) stop();
    unsubscribeQueue();
    unsubscribeDrained();
  };
}

// --- test seams ---------------------------------------------------------------

/** Back to empty: no server copy, no optimistic events, no driver, snapshot at the epoch. */
export function resetLedgerForTests(): void {
  userId = null;
  driverId = null;
  driverName = null;
  server = null;
  seed = null;
  optimistic = [];
  hydration = NO_HYDRATION;
  resolvingDriver = null;
  hydrating = null;
  publish(serverSnapshot);
}

/**
 * Installs a raw ledger in place of a server copy and evaluates it at `asOfUnix` — for tests
 * that need a known timeline without a mocked API. Events become server-side (eventId set,
 * no clientEventId); optimistic appends still layer on top. `status`, `exceptions` and `days`
 * stand in for the server copy's own (an out-of-service declaration on `status` reaches the
 * engine exactly as a hydrated one would), defaulting to a quiet status with none of them.
 */
export function installLedgerForTests(input: {
  driverId?: string;
  driverName?: string | null;
  knownFromUnix: number;
  events: DutyEvent[];
  certifiedDays: DateOnly[];
  asOfUnix: number;
  status?: Partial<HosStatus>;
  exceptions?: HosExceptionRecord[];
  days?: DutyDaySummary[];
}): void {
  driverId = input.driverId ?? "driver-test";
  driverName = input.driverName === undefined ? "Test Driver" : input.driverName;
  seed = {
    knownFromUnix: input.knownFromUnix,
    events: input.events.map((e, i) => ({ ...e, eventId: `seed-${i + 1}`, clientEventId: null })),
    certifiedDays: input.certifiedDays,
  };
  optimistic = [];
  const quiet: HosStatus = {
    driverId,
    label: "Planning figures",
    currentDuty: "Off Duty",
    currentDutySinceUtc: new Date(input.knownFromUnix * 1000).toISOString(),
    recordsOnFile: true,
    knownFromUtc: new Date(input.knownFromUnix * 1000).toISOString(),
    outOfService: null,
    eldMalfunctionOpen: false,
    priorRecords: null,
    evaluation: serverSnapshot.evaluation,
  };
  server = {
    days: input.days ?? [],
    status: { ...quiet, ...input.status },
    exceptions: input.exceptions ?? [],
    fetchedAtMs: input.asOfUnix * 1000,
  };
  publish(build(input.asOfUnix));
}
