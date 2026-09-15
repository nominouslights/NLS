// ---------------------------------------------------------------------------
// The offline seam — command queue. NO-OP IMPLEMENTATION.
//
// Today: enqueue() mints a client-generated GUID, records the command in memory, and returns.
// Nothing is durable and nothing is sent. The GUID is real from day one so screens are already
// written against the contract they will keep.
//
// Later (the offline batch): the same signatures, backed by an IndexedDB object store.
// drain() POSTs each pending command with its `id` as the Idempotency-Key header, marks it sent
// on 2xx, and leaves it pending on a network failure. Screens do not change.
//
// See ./types.ts for the rule that makes that true.
// ---------------------------------------------------------------------------

import type { CommandKind, QueuedCommand } from "./types";
import { bumpPending } from "./status";

/**
 * In-memory only, cleared on reload. Deliberately not exported as a mutable array — screens
 * read their data from lib/data.ts, never from the queue.
 */
const commands: QueuedCommand[] = [];

/**
 * Records an intent to change server state and returns its client-generated id.
 *
 * Every screen mutation goes through here. Resolving immediately is the point, not a shortcut:
 * the driver never waits on the network to complete an action, offline or on.
 */
export async function enqueue(kind: CommandKind, payload: unknown): Promise<string> {
  const id = newCommandId();
  commands.push({
    id,
    kind,
    payload,
    createdAt: new Date().toISOString(),
    attempts: 0,
    status: "pending",
  });
  bumpPending(commands.length);
  return id;
}

/** Commands captured but not yet accepted by the server. */
export function pending(): QueuedCommand[] {
  return commands.filter((c) => c.status === "pending" || c.status === "failed");
}

/**
 * Sends everything pending. A no-op in this scaffold — there is nothing to send to, and no
 * backend endpoint honours an idempotency key yet.
 */
export async function drain(): Promise<void> {
  // Offline batch: iterate pending(), POST with Idempotency-Key, update status.
}

/** Re-queues one failed command. No-op in this scaffold. */
export async function retry(id: string): Promise<void> {
  // Offline batch: reset this command's status to "pending" and drain().
  void id;
}

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
