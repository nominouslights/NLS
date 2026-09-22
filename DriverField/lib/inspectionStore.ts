// ---------------------------------------------------------------------------
// The in-progress DVIR draft, and the certifications this device has made.
//
// DELIBERATELY NOT UNDER lib/sync/, AND THE DISTINCTION IS THE POINT:
//
//                  | This file's DRAFT                | lib/sync/queue.ts's QUEUED COMMAND
//   ---------------|----------------------------------|-----------------------------------
//   State          | Uncertified, private to the      | Certified — it IS the compliance
//                  | device                           | record
//   Cost of loss   | Re-answer 22 questions           | A COMPLIANCE FAILURE
//   Home           | localStorage — synchronous,      | IndexedDB + navigator.storage
//                  | survives reload, ~2KB            | .persist(), the offline batch's job
//
// Putting a draft under lib/sync/ would make it look like the queue and invite the offline
// batch to migrate it. Putting a queued DVIR in localStorage would be exactly the compliance
// failure DriverField/CLAUDE.md names ("a queued DVIR vanishing because the browser reclaimed
// space is a compliance failure, not a lost draft"). Neither file should grow into the other.
//
// One module holds BOTH drafts and locally-made certifications, because they are two states of
// one object and the transition between them is one function — which gives one set of storage
// guards, one subscribe, and one test file.
//
// ORDER ON SUBMIT, and it is not negotiable:
//   enqueue(...)  →  recordCertification({ commandId, … })  →  discardDraft(...)
// If enqueue throws, the driver's 22 answers survive.
//
// READING FROM REACT: one useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot),
// mirroring lib/sync/status.ts and components/ui-tablet/SyncPill.tsx. NOT useEffect +
// setState — that pattern is one of the three inherited lint errors in lib/useToday.ts, and a
// fourth would break the lint bar. It also avoids an SSR hydration mismatch, because the
// server has no localStorage; see getServerSnapshot below for how.
//
// LATER MIGRATION: `commandId` is the hook. When the offline batch's drain() lands it can mark
// a certification server-accepted by that id, and the "Held on this device" banner starts
// telling a different truth with no change to this file's API.
// ---------------------------------------------------------------------------

import { dvirChecklist, today } from "./data";
import type { CheckState, DefectSeverity, InspectionMode } from "./types";
// Type-only, so there is no runtime import cycle with inspectionGate.ts (which imports
// certifiedToday from here). `import type` is erased at compile time.
import type { InspectionResultName } from "./inspectionGate";

/**
 * Bumped whenever the shape below changes. A draft written by an older version is DISCARDED,
 * never migrated — a half-migrated legal attestation is worse than re-answering 22 questions.
 */
export const INSPECTION_STORE_VERSION = 1;

/** Follows lib/auth.ts's `nl.driverfield.refreshToken` convention. */
const DRAFT_KEY_PREFIX = "nl.driverfield.inspectionDraft";
const CERTIFIED_KEY = "nl.driverfield.inspectionCertified";

/** How many local certifications to retain. Bounded so the key cannot grow without limit. */
const CERTIFIED_LIMIT = 20;

/**
 * `nl.driverfield.inspectionDraft.PreTrip.VEH-11`
 *
 * Keying mode AND vehicle into the key rather than holding one blob buys three things: a mode
 * switch cannot clobber the other mode's draft, a mid-shift vehicle reassignment orphans the
 * old draft instead of silently re-attributing 22 answers to a different vehicle, and
 * discardDraft removes exactly one key.
 */
export function draftKey(mode: InspectionMode, vehicleId: string): string {
  return `${DRAFT_KEY_PREFIX}.${mode}.${vehicleId}`;
}

export interface DraftDefect {
  /** null until the driver grades it — a defect without a severity cannot be submitted. */
  severity: DefectSeverity | null;
  note: string;
}

export interface InspectionDraft {
  v: number;
  mode: InspectionMode;
  vehicleId: string;
  /** The SERVICE DAY, pinned from lib/data.ts's `today` — not the device clock. */
  startedOn: string;
  /** ISO, device clock. Becomes PerformedAt on the wire. */
  startedAt: string;
  answers: Record<string, CheckState>;
  defects: Record<string, DraftDefect>;
  odometerKm: number | null;
  /** A step ID, never an index — an injected defect step shifts every later index. */
  stepId: string;
}

export interface LocalCertification {
  /** The queued command's GUID — the future Idempotency-Key, and the offline batch's hook. */
  commandId: string;
  mode: InspectionMode;
  vehicleId: string;
  /** Service day, so "certified today" is a string compare and not a clock question. */
  onDate: string;
  certifiedAt: string;
  result: InspectionResultName;
  defectCount: number;
  outOfService: boolean;
}

// --- change notification ---------------------------------------------------

type StoreListener = () => void;
const listeners = new Set<StoreListener>();

/**
 * A monotonic revision, starting at 1. Its only job is to be a stable value that changes when
 * something in this store changes.
 */
let revision = 1;

function bump(): void {
  revision += 1;
  for (const listener of [...listeners]) listener();
}

/** Subscribe to store changes. Returns an unsubscribe function. */
export function subscribe(listener: StoreListener): () => void {
  listeners.add(listener);

  // Another tab writing the same key. Not a real scenario on a kiosk-pinned tablet, but the
  // module cache would otherwise serve a stale draft forever if it ever happened.
  const onStorage = (e: StorageEvent) => {
    if (e.key === null || e.key.startsWith(DRAFT_KEY_PREFIX) || e.key === CERTIFIED_KEY) {
      bump();
    }
  };
  if (typeof window !== "undefined") window.addEventListener("storage", onStorage);

  return () => {
    listeners.delete(listener);
    if (typeof window !== "undefined") window.removeEventListener("storage", onStorage);
  };
}

/** The client snapshot. Always a number, so it can never equal the server snapshot. */
export function getSnapshot(): number | null {
  return revision;
}

/**
 * The server snapshot — deliberately `null`, and that is the whole hydration story.
 *
 * React uses this value for the server render AND for the first client (hydrating) render, then
 * re-reads getSnapshot once hydration completes. So a caller writes:
 *
 *   const rev = useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
 *   const draft = rev === null ? null : getDraft(mode, vehicleId);
 *
 * and the localStorage read cannot happen during hydration, where the server had no way to
 * produce the same markup. No useEffect, no setState, no mismatch.
 */
export function getServerSnapshot(): number | null {
  return null;
}

// --- storage guards (following lib/auth.ts exactly) ------------------------

let storageBroken = false;

/**
 * True once a localStorage read or write has failed. A write failure is SWALLOWED — a driver
 * mid-DVIR must not see a thrown error from what is, for them, a convenience — but it is never
 * hidden: Inspection.tsx renders "This draft is not being saved on this device." off this flag.
 * Silent-and-honest, never silent-and-lying.
 *
 * JSON corruption does NOT set it. That is bad data, not broken storage, and the remedy
 * (discard the key) is different.
 */
export function storageFailed(): boolean {
  return storageBroken;
}

function readRaw(key: string): string | null {
  if (typeof window === "undefined") return null;
  try {
    return window.localStorage.getItem(key);
  } catch {
    storageBroken = true;
    return null;
  }
}

function writeRaw(key: string, value: string): void {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.setItem(key, value);
  } catch {
    storageBroken = true;
  }
}

function removeRaw(key: string): void {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.removeItem(key);
  } catch {
    storageBroken = true;
  }
}

// --- draft validation ------------------------------------------------------

const KNOWN_ITEM_IDS: ReadonlySet<string> = new Set(
  dvirChecklist.flatMap((g) => g.items.map((i) => i.id)),
);

const CHECK_STATES: ReadonlySet<string> = new Set<CheckState>(["pass", "defect", "na"]);
const SEVERITIES: ReadonlySet<string> = new Set<DefectSeverity>([
  "Minor",
  "Major",
  "Out of Service",
]);

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/**
 * VALIDATION, NOT A CAST. Anything in localStorage was written by a program that may no longer
 * exist. Beyond the version and identity checks, this DROPS any answer or defect whose itemId
 * is not in dvirChecklist, so a renamed or removed checklist item cannot resurrect from storage
 * and reach a compliance payload.
 */
function validateDraft(
  parsed: unknown,
  mode: InspectionMode,
  vehicleId: string,
): InspectionDraft | null {
  if (!isPlainObject(parsed)) return null;
  if (parsed.v !== INSPECTION_STORE_VERSION) return null;
  if (parsed.mode !== "PreTrip" && parsed.mode !== "PostTrip") return null;
  if (parsed.mode !== mode) return null;
  if (parsed.vehicleId !== vehicleId) return null;
  if (typeof parsed.startedOn !== "string" || typeof parsed.startedAt !== "string") return null;
  if (typeof parsed.stepId !== "string") return null;
  if (!isPlainObject(parsed.answers)) return null;

  const answers: Record<string, CheckState> = {};
  for (const [itemId, state] of Object.entries(parsed.answers)) {
    if (!KNOWN_ITEM_IDS.has(itemId)) continue;
    if (typeof state === "string" && CHECK_STATES.has(state)) {
      answers[itemId] = state as CheckState;
    }
  }

  const defects: Record<string, DraftDefect> = {};
  if (isPlainObject(parsed.defects)) {
    for (const [itemId, raw] of Object.entries(parsed.defects)) {
      if (!KNOWN_ITEM_IDS.has(itemId)) continue;
      if (!isPlainObject(raw)) continue;
      const severity =
        typeof raw.severity === "string" && SEVERITIES.has(raw.severity)
          ? (raw.severity as DefectSeverity)
          : null;
      defects[itemId] = { severity, note: typeof raw.note === "string" ? raw.note : "" };
    }
  }

  const odometerKm =
    typeof parsed.odometerKm === "number" && Number.isFinite(parsed.odometerKm)
      ? parsed.odometerKm
      : null;

  return {
    v: INSPECTION_STORE_VERSION,
    mode,
    vehicleId,
    startedOn: parsed.startedOn,
    startedAt: parsed.startedAt,
    answers,
    defects,
    odometerKm,
    stepId: parsed.stepId,
  };
}

// --- draft cache -----------------------------------------------------------

/**
 * Keyed by storage key, holding the raw string it was parsed from. Keeping the raw string is
 * what makes this safe: if something outside this module rewrites the key — another tab, or a
 * test seeding storage to simulate a reload — the raw strings differ and the cache is bypassed.
 * The identity stability is worth having because the wizard reads the draft on every render.
 */
const cache = new Map<string, { raw: string; draft: InspectionDraft }>();

// --- drafts ----------------------------------------------------------------

/**
 * The live draft for this mode and vehicle, or null.
 *
 * Returns null AND REMOVES THE KEY in three cases, all of them deliberate:
 *   • a version mismatch — discarded, never migrated;
 *   • `startedOn !== asOf` — yesterday's pre-trip must never resume into today's
 *     certification, which is the whole reason startedOn is the pinned service day rather than
 *     the device clock;
 *   • unparseable or invalid JSON.
 */
export function getDraft(
  mode: InspectionMode,
  vehicleId: string,
  asOf: string = today,
): InspectionDraft | null {
  const key = draftKey(mode, vehicleId);
  const raw = readRaw(key);
  if (raw === null) {
    cache.delete(key);
    return null;
  }

  const cached = cache.get(key);
  if (cached && cached.raw === raw) {
    return cached.draft.startedOn === asOf ? cached.draft : discardAndNull(key);
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    // Corrupt data, not broken storage — storageFailed() stays false on purpose.
    return discardAndNull(key);
  }

  const draft = validateDraft(parsed, mode, vehicleId);
  if (!draft) return discardAndNull(key);
  if (draft.startedOn !== asOf) return discardAndNull(key);

  cache.set(key, { raw, draft });
  return draft;
}

function discardAndNull(key: string): null {
  cache.delete(key);
  removeRaw(key);
  return null;
}

export function hasDraft(
  mode: InspectionMode,
  vehicleId: string,
  asOf: string = today,
): boolean {
  return getDraft(mode, vehicleId, asOf) !== null;
}

/** Creates (or replaces) the draft for this mode and vehicle and returns it. */
export function startDraft(
  mode: InspectionMode,
  vehicleId: string,
  asOf: string = today,
): InspectionDraft {
  const draft: InspectionDraft = {
    v: INSPECTION_STORE_VERSION,
    mode,
    vehicleId,
    startedOn: asOf,
    startedAt: new Date().toISOString(),
    answers: {},
    defects: {},
    odometerKm: null,
    stepId: "odometer",
  };
  return persist(draft);
}

function persist(draft: InspectionDraft): InspectionDraft {
  const key = draftKey(draft.mode, draft.vehicleId);
  const raw = JSON.stringify(draft);
  cache.set(key, { raw, draft });
  writeRaw(key, raw);
  bump();
  return draft;
}

/** Applies `change` to the existing draft, starting one if there is none. */
function update(
  mode: InspectionMode,
  vehicleId: string,
  asOf: string,
  change: (draft: InspectionDraft) => InspectionDraft,
): InspectionDraft {
  const current =
    getDraft(mode, vehicleId, asOf) ?? {
      v: INSPECTION_STORE_VERSION,
      mode,
      vehicleId,
      startedOn: asOf,
      startedAt: new Date().toISOString(),
      answers: {},
      defects: {},
      odometerKm: null,
      stepId: "odometer",
    };
  return persist(change(current));
}

/**
 * Records one checklist answer.
 *
 * CHANGING AN ANSWER AWAY FROM "defect" PRUNES THE DEFECT RECORD IN THE SAME MUTATION. That is
 * what makes buildSteps drop the injected follow-up step and guarantees no orphan defect can
 * reach the payload — a defect on an item the driver has since marked Pass would be a false
 * entry in a compliance record.
 */
export function setAnswer(
  mode: InspectionMode,
  vehicleId: string,
  itemId: string,
  state: CheckState,
  asOf: string = today,
): InspectionDraft {
  return update(mode, vehicleId, asOf, (draft) => {
    const defects = { ...draft.defects };
    if (state === "defect") {
      defects[itemId] = defects[itemId] ?? { severity: null, note: "" };
    } else {
      delete defects[itemId];
    }
    return { ...draft, answers: { ...draft.answers, [itemId]: state }, defects };
  });
}

export function setDefect(
  mode: InspectionMode,
  vehicleId: string,
  itemId: string,
  defect: DraftDefect,
  asOf: string = today,
): InspectionDraft {
  return update(mode, vehicleId, asOf, (draft) => ({
    ...draft,
    defects: { ...draft.defects, [itemId]: defect },
  }));
}

export function setOdometer(
  mode: InspectionMode,
  vehicleId: string,
  odometerKm: number | null,
  asOf: string = today,
): InspectionDraft {
  return update(mode, vehicleId, asOf, (draft) => ({ ...draft, odometerKm }));
}

export function setStep(
  mode: InspectionMode,
  vehicleId: string,
  stepId: string,
  asOf: string = today,
): InspectionDraft {
  return update(mode, vehicleId, asOf, (draft) => ({ ...draft, stepId }));
}

/** Removes exactly one draft key. Called only AFTER enqueue and recordCertification succeed. */
export function discardDraft(mode: InspectionMode, vehicleId: string): void {
  const key = draftKey(mode, vehicleId);
  cache.delete(key);
  removeRaw(key);
  bump();
}

// --- local certifications --------------------------------------------------

function readCertifications(): LocalCertification[] {
  const raw = readRaw(CERTIFIED_KEY);
  if (raw === null) return [];

  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    removeRaw(CERTIFIED_KEY);
    return [];
  }

  if (!isPlainObject(parsed)) return dropCertifications();
  if (parsed.v !== INSPECTION_STORE_VERSION) return dropCertifications();
  if (!Array.isArray(parsed.items)) return dropCertifications();

  const items: LocalCertification[] = [];
  for (const entry of parsed.items) {
    if (!isPlainObject(entry)) continue;
    if (entry.mode !== "PreTrip" && entry.mode !== "PostTrip") continue;
    if (typeof entry.vehicleId !== "string" || typeof entry.onDate !== "string") continue;
    if (typeof entry.commandId !== "string" || typeof entry.certifiedAt !== "string") continue;
    if (
      entry.result !== "Pass" &&
      entry.result !== "PassWithDefects" &&
      entry.result !== "Fail"
    ) {
      continue;
    }
    items.push({
      commandId: entry.commandId,
      mode: entry.mode,
      vehicleId: entry.vehicleId,
      onDate: entry.onDate,
      certifiedAt: entry.certifiedAt,
      result: entry.result,
      defectCount: typeof entry.defectCount === "number" ? entry.defectCount : 0,
      outOfService: entry.outOfService === true,
    });
  }
  return items;
}

function dropCertifications(): LocalCertification[] {
  removeRaw(CERTIFIED_KEY);
  return [];
}

/**
 * Records that THIS DEVICE certified an inspection. Persisted rather than held in memory
 * because the boarding gate reads it: a driver who certifies a pre-trip and then reloads the
 * tab must not be blocked from boarding again. (lib/sync/queue.ts's queue is in-memory and
 * cleared on reload, so reading the gate off the queue would be exactly that bug — and
 * queue.ts's own rule says screens never read the queue anyway.)
 */
export function recordCertification(certification: LocalCertification): void {
  const items = [...readCertifications(), certification].slice(-CERTIFIED_LIMIT);
  writeRaw(CERTIFIED_KEY, JSON.stringify({ v: INSPECTION_STORE_VERSION, items }));
  bump();
}

/** All certifications this device holds, oldest first. */
export function localCertifications(): LocalCertification[] {
  return readCertifications();
}

/**
 * The most recent certification THIS DEVICE made for this mode, vehicle and service day, or
 * null. Compared on `onDate` rather than a timestamp, so it is a string compare and not a
 * timezone question.
 */
export function certifiedToday(
  mode: InspectionMode,
  vehicleId: string,
  asOf: string = today,
): LocalCertification | null {
  const matches = readCertifications().filter(
    (c) => c.mode === mode && c.vehicleId === vehicleId && c.onDate === asOf,
  );
  return matches.length === 0 ? null : matches[matches.length - 1];
}
