// ---------------------------------------------------------------------------
// The offline seam — contracts.
//
// Offline-first is a hard requirement for this app (architecture non-negotiable #7). Cellular
// coverage between Thompson, Lynn Lake and the Alamos mine site is patchy by geography, not as
// a corner case. It is NOT implemented in this scaffold — it is STAGED. These types are real
// and final; the implementations behind them are no-ops until the offline batch.
//
// The architecture reference says SQLite. That was written when this app was going to be
// Flutter. A PWA gets IndexedDB instead — recorded in references/architecture.md §8. Everything
// else that section prescribes survives the framework change verbatim: a local-first store, an
// idempotent command queue, client-generated GUIDs, last-write-wins with a full audit trail,
// and a sync indicator the driver can see at all times.
//
// THE RULE THAT MAKES THE LATER BATCH ADDITIVE RATHER THAN A REWRITE:
//   Every screen mutation goes through queue.enqueue(). Never a direct lib/api/* call, never
//   an in-place mutation of a lib/data.ts array.
// Hold that from day one and the offline batch changes four files here and zero screens. Break
// it in one screen and it is a rewrite.
// ---------------------------------------------------------------------------

/**
 * Everything a driver can do that changes server state. Each maps to exactly one backend write
 * once the wiring batch lands; until then enqueue() applies the change to in-memory mock state.
 */
export type CommandKind =
  | "hos.record"
  | "dvir.submit"
  | "manifest.board"
  | "trip.claim"
  | "incident.report"
  | "vehicle.fault";

export type CommandStatus = "pending" | "sending" | "sent" | "failed";

export interface QueuedCommand {
  /**
   * Client-generated GUID, created at capture time on the device — NOT a server id.
   *
   * This is the single most important field in the offline design. It travels as the
   * Idempotency-Key header so a retried sync can never double-submit a DVIR or duplicate a
   * manifest entry. The backend does not honour it yet (no endpoint anywhere reads an
   * idempotency key today); the client sends it from day one so the server can start honouring
   * it with no client change.
   */
  id: string;
  kind: CommandKind;
  payload: unknown;
  /** ISO 8601, device clock. Server-side handling must tolerate clock skew. */
  createdAt: string;
  attempts: number;
  status: CommandStatus;
  /** Set when status is "failed" — shown to the driver, never swallowed. */
  error?: string;
}

/**
 * What the always-visible sync indicator renders. Never let sync state be invisible: a driver
 * must be able to tell "this is saved on the device" from "this has reached the server".
 */
export interface SyncState {
  /** Whether the device believes it has a network. Not whether the API is reachable. */
  online: boolean;
  /** Commands captured but not yet accepted by the server. */
  pending: number;
  /** Commands the server rejected, needing the driver's attention. */
  failed: number;
  /** ISO 8601 of the last successful drain, or null if never. */
  lastSyncedAt: string | null;
  /**
   * False for the whole of this scaffold. The pill reads it and says "not enabled" rather than
   * showing a green check — a scaffold that LOOKS like it syncs is how a hard requirement
   * quietly dies.
   */
  enabled: boolean;
}
