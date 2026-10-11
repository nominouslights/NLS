// ---------------------------------------------------------------------------
// The offline seam — command queue. DURABLE, AND LIVE FOR THE KINDS THAT HAVE A SENDER.
//
// enqueue() mints a client-generated GUID, writes the command to IndexedDB (lib/sync/db.ts)
// and resolves once that write is committed — "held on this device" means durable, not
// "in a JavaScript array". drain() walks the pending commands in capture order and hands each
// to the sender registered for its kind, with the command's id as the Idempotency-Key the
// sender puts on the wire. A kind with no sender is simply held: captured, counted, never sent,
// and the pill says so.
//
// WHAT A RESPONSE MEANS, and this is the whole offline contract:
//   2xx (201 first time, 200 replay)   → sent. A replay is a success, not a duplicate.
//   4xx                                → failed, PARKED with the server's own message, visible
//                                        on the Hours screen, never retried automatically. The
//                                        server has said no; sending the same thing again would
//                                        get the same no. The driver discards it or dispatch
//                                        fixes the record.
//   status 0 (unreachable) / 5xx       → stays pending for the next drain; the rest of the
//                                        queue is left alone this round rather than hammered.
//
// Triggers: the window's `online` event, an enqueue while online, and a 60-second interval —
// all installed by startSync() (components/Console.tsx on mount) and nothing before sign-in.
// Senders are registered by the module that owns the endpoint (lib/hos/ledger.ts for the two
// hos.* kinds); this file knows no URL.
// ---------------------------------------------------------------------------

import { ApiError } from "../api/transport";
import * as db from "./db";
import { setCounts, setEnabledKinds } from "./status";
import type { CommandKind, CommandStatus, QueuedCommand } from "./types";

/** Every kind, as a Record so the compiler refuses a kind added to the union but not here. */
const KIND_SET: Record<CommandKind, true> = {
  "hos.duty": true,
  "hos.certify": true,
  "dvir.submit": true,
  "manifest.board": true,
  "trip.claim": true,
  "incident.report": true,
  "vehicle.fault": true,
};

export const COMMAND_KINDS = Object.keys(KIND_SET) as CommandKind[];

/** Sent commands older than this are pruned on load. The audit trail is the server's, not the tablet's. */
const SENT_RETENTION_MS = 14 * 24 * 3600 * 1000;

/** How often a drain runs while the app is open, on top of the online event and each enqueue. */
export const DRAIN_INTERVAL_MS = 60_000;

/**
 * A payload that needs the command's own id inside it — `hos.duty` carries it as
 * `clientEventId`, the key the ledger and the server reconcile on. The id is minted here, so
 * the payload is built here: pass a function and it receives the id.
 */
export type PayloadFactory = (commandId: string) => unknown;

/**
 * What sends one kind. Resolves on 2xx; throws an ApiError otherwise (the transport's
 * contract). The command id is the Idempotency-Key the sender must put on the wire.
 */
export type Sender = (payload: unknown, commandId: string) => Promise<void>;

export interface DrainResult {
  sent: number;
  failed: number;
  /** Pending commands whose kind has no sender yet. */
  held: number;
  /** True when a network failure stopped the round early; the rest stays pending. */
  unreachable: boolean;
}

// --- state -----------------------------------------------------------------

/** In-memory mirror of the IndexedDB store, in capture order. Loaded once per page load. */
let commands: QueuedCommand[] = [];
let loaded: Promise<void> | null = null;
const senders: Partial<Record<CommandKind, Sender>> = {};
const changeListeners = new Set<() => void>();
const drainListeners = new Set<(result: DrainResult) => void>();
let draining: Promise<DrainResult> | null = null;
let syncStarted = false;

function bySeq(a: QueuedCommand, b: QueuedCommand): number {
  return a.seq - b.seq;
}

function nextSeq(): number {
  let max = 0;
  for (const c of commands) max = Math.max(max, c.seq);
  return max + 1;
}

/**
 * Loads the held commands from IndexedDB. Called by every public function, so the first
 * caller pays once and the rest see the mirror. A command left "sending" by a crash mid-drain
 * goes back to "pending": the server's idempotency makes a resend a replay, never a duplicate.
 */
export function ready(): Promise<void> {
  if (loaded) return loaded;
  loaded = (async () => {
    const rows = await db.getAll<QueuedCommand>(db.COMMANDS_STORE);
    const cutoff = Date.now() - SENT_RETENTION_MS;
    const kept: QueuedCommand[] = [];
    for (const row of rows.sort(bySeq)) {
      if (row.status === "sent" && Date.parse(row.createdAt) < cutoff) {
        void db.remove(db.COMMANDS_STORE, row.id).catch(() => undefined);
        continue;
      }
      kept.push(row.status === "sending" ? { ...row, status: "pending" } : row);
    }
    commands = kept;
    publishCounts();
  })().catch((err) => {
    loaded = null;
    throw err;
  });
  return loaded;
}

function publishCounts(lastSyncedAt?: string | null): void {
  setCounts({
    pending: commands.filter((c) => c.status === "pending" || c.status === "sending").length,
    failed: commands.filter((c) => c.status === "failed").length,
    lastSyncedAt,
  });
}

function notifyChange(): void {
  for (const listener of [...changeListeners]) listener();
}

/** Persists a mutation. Memory is authoritative for the session; a failed write is tolerated
 *  because the worst case on reload is a resend the server answers as a replay. */
async function update(command: QueuedCommand, patch: Partial<QueuedCommand>): Promise<void> {
  Object.assign(command, patch);
  try {
    await db.put(db.COMMANDS_STORE, { ...command });
  } catch {
    // see above
  }
}

function isOnline(): boolean {
  return typeof navigator === "undefined" ? true : navigator.onLine;
}

// --- capture ---------------------------------------------------------------

/**
 * Records an intent to change server state and returns its client-generated id. Resolves once
 * the command is durable on the device — never after a network call. If the device cannot hold
 * it (no IndexedDB, quota exhausted) this REJECTS rather than pretending: the DVIR flow is
 * written so that a throwing enqueue leaves the driver's walk-around intact.
 */
export async function enqueue(kind: CommandKind, payload: PayloadFactory): Promise<string>;
export async function enqueue(kind: CommandKind, payload: unknown): Promise<string>;
export async function enqueue(kind: CommandKind, payload: unknown): Promise<string> {
  await ready();
  const id = newCommandId();
  const command: QueuedCommand = {
    id,
    kind,
    payload: typeof payload === "function" ? (payload as PayloadFactory)(id) : payload,
    createdAt: new Date().toISOString(),
    seq: nextSeq(),
    attempts: 0,
    status: "pending",
  };
  await db.put(db.COMMANDS_STORE, command);
  void db.requestPersistence();
  commands.push(command);
  publishCounts();
  notifyChange();
  if (syncStarted && senders[kind] !== undefined && isOnline()) void drain();
  return id;
}

// --- reads -----------------------------------------------------------------

/** Commands captured but not yet accepted by the server: pending, sending and parked. */
export function pending(): QueuedCommand[] {
  return commands.filter((c) => c.status === "pending" || c.status === "sending" || c.status === "failed");
}

/** Commands the server rejected, in capture order, with the server's message on each. */
export function failedCommands(): QueuedCommand[] {
  return commands.filter((c) => c.status === "failed");
}

/** Everything held, including sent ones not yet pruned. For the ledger's reconciliation. */
export function allCommands(): readonly QueuedCommand[] {
  return commands;
}

export function statusOf(id: string): CommandStatus | undefined {
  return commands.find((c) => c.id === id)?.status;
}

/** Fires after any mutation (enqueue, drain, retry, discard). */
export function subscribeQueue(listener: () => void): () => void {
  changeListeners.add(listener);
  return () => {
    changeListeners.delete(listener);
  };
}

/** Fires at the end of every drain round with what happened. */
export function onDrained(listener: (result: DrainResult) => void): () => void {
  drainListeners.add(listener);
  return () => {
    drainListeners.delete(listener);
  };
}

// --- senders ---------------------------------------------------------------

/** Registers what sends one kind. Returns the unregister function. */
export function registerSender(kind: CommandKind, sender: Sender): () => void {
  senders[kind] = sender;
  setEnabledKinds(enabledKinds(), COMMAND_KINDS);
  return () => {
    if (senders[kind] === sender) {
      delete senders[kind];
      setEnabledKinds(enabledKinds(), COMMAND_KINDS);
    }
  };
}

export function enabledKinds(): CommandKind[] {
  return COMMAND_KINDS.filter((k) => senders[k] !== undefined);
}

// --- drain -----------------------------------------------------------------

/**
 * Sends everything pending that has a sender, in capture order. Single-flight: a drain
 * requested while one runs joins it. Never throws — a sender's failure is recorded on its
 * command, and a network failure leaves the rest pending for the next round.
 */
export function drain(): Promise<DrainResult> {
  if (draining) return draining;
  draining = runDrain().finally(() => {
    draining = null;
  });
  return draining;
}

async function runDrain(): Promise<DrainResult> {
  const result: DrainResult = { sent: 0, failed: 0, held: 0, unreachable: false };
  try {
    await ready();
  } catch {
    result.unreachable = true;
    return result;
  }

  for (const command of [...commands]) {
    if (command.status !== "pending") continue;
    const sender = senders[command.kind];
    if (sender === undefined) {
      result.held += 1;
      continue;
    }
    if (result.unreachable) continue;

    await update(command, { status: "sending", attempts: command.attempts + 1 });
    try {
      await sender(command.payload, command.id);
      await update(command, { status: "sent", error: undefined, errorCode: undefined });
      result.sent += 1;
    } catch (err) {
      const verdict = classify(err);
      if (verdict.rejected) {
        await update(command, { status: "failed", error: verdict.message, errorCode: verdict.code });
        result.failed += 1;
      } else {
        await update(command, { status: "pending" });
        result.unreachable = true;
      }
    }
  }

  publishCounts(result.sent > 0 ? new Date().toISOString() : undefined);
  notifyChange();
  for (const listener of [...drainListeners]) listener(result);
  return result;
}

/**
 * A 4xx is the server's answer and is final for this payload. Anything that is not an HTTP
 * answer — status 0 from the transport, a 5xx, or a thrown non-ApiError — is "try again later".
 */
function classify(err: unknown): { rejected: boolean; message: string; code: string } {
  if (err instanceof ApiError) {
    const rejected = err.status >= 400 && err.status < 500;
    return { rejected, message: err.message, code: err.code };
  }
  return { rejected: false, message: err instanceof Error ? err.message : String(err), code: "Client.Unknown" };
}

/** Re-queues one parked command and drains if sync is running. */
export async function retry(id: string): Promise<void> {
  await ready();
  const command = commands.find((c) => c.id === id);
  if (!command || command.status !== "failed") return;
  await update(command, { status: "pending", error: undefined, errorCode: undefined });
  publishCounts();
  notifyChange();
  if (syncStarted) void drain();
}

/** Drops one parked command for good — the driver's DISCARD on the Hours screen. */
export async function discard(id: string): Promise<void> {
  await ready();
  const index = commands.findIndex((c) => c.id === id);
  if (index < 0) return;
  commands.splice(index, 1);
  try {
    await db.remove(db.COMMANDS_STORE, id);
  } catch {
    // memory already dropped it; on reload it would reappear parked, which is honest
  }
  publishCounts();
  notifyChange();
}

// --- triggers --------------------------------------------------------------

/**
 * Installs the drain triggers: a drain now, one on every `online` event, and one every
 * DRAIN_INTERVAL_MS. Returns the stop function. Called once the shell mounts, never before
 * sign-in — there is no token to send with until then.
 */
export function startSync(): () => void {
  syncStarted = true;
  const onOnline = () => void drain();
  if (typeof window !== "undefined") window.addEventListener("online", onOnline);
  const timer = setInterval(() => void drain(), DRAIN_INTERVAL_MS);
  void drain();
  return () => {
    syncStarted = false;
    if (typeof window !== "undefined") window.removeEventListener("online", onOnline);
    clearInterval(timer);
  };
}

// --- ids -------------------------------------------------------------------

/**
 * crypto.randomUUID needs a secure context. The app is always served over HTTPS or localhost,
 * so this is belt and braces — but a queue that throws at capture time would lose a driver's
 * DVIR, and that is a compliance failure, not a UI glitch.
 */
function newCommandId(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  return `cmd-${Date.now().toString(16)}-${Math.random().toString(16).slice(2, 10)}`;
}

// --- test seam -------------------------------------------------------------

/**
 * Forgets the in-memory mirror and every sender, so the next call reloads from IndexedDB
 * exactly as a page reload would. `wipe` also deletes the database — a fresh device.
 */
export async function resetQueueForTests(options: { wipe?: boolean } = {}): Promise<void> {
  commands = [];
  loaded = null;
  draining = null;
  syncStarted = false;
  for (const kind of COMMAND_KINDS) delete senders[kind];
  changeListeners.clear();
  drainListeners.clear();
  if (options.wipe) await db.deleteDatabaseForTests();
  setEnabledKinds([], COMMAND_KINDS);
  setCounts({ pending: 0, failed: 0, lastSyncedAt: null });
}
