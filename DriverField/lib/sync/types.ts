// ---------------------------------------------------------------------------
// The offline seam — contracts.
//
// Offline-first is a hard requirement for this app (architecture non-negotiable #7). Cellular
// coverage between Thompson, Lynn Lake and the Alamos mine site is patchy by geography, not as
// a corner case. The seam is now PARTLY REAL: the queue is durable (IndexedDB), drains on a
// timer and on reconnect, and the two `hos.*` kinds below reach the server. Every other kind is
// captured, held and never sent — the pill says exactly that.
//
// The architecture reference says SQLite. That was written when this app was going to be
// Flutter. A PWA gets IndexedDB instead — recorded in references/architecture.md §8. Everything
// else that section prescribes survives the framework change verbatim: a local-first store, an
// idempotent command queue, client-generated GUIDs, last-write-wins with a full audit trail,
// and a sync indicator the driver can see at all times.
//
// THE RULE THAT MADE THIS BATCH ADDITIVE RATHER THAN A REWRITE, and still holds:
//   Every screen mutation goes through queue.enqueue(). Never a direct lib/api/* call, never
//   an in-place mutation of a lib/data.ts array.
// Going live for hours changed the files under lib/sync/ and lib/hos/ and no screen's write
// path.
// ---------------------------------------------------------------------------

/**
 * Everything a driver can do that changes server state. Each maps to exactly one backend write
 * once its slice lands; until then enqueue() holds it on the device and nothing sends it.
 *
 * LIVE TODAY (a sender is registered — lib/hos/ledger.ts):
 *   `hos.duty`     one duty-status change → POST /api/drivers/{driverId}/hos/duty-events, the
 *                  first endpoint to honour the Idempotency-Key. Payload built by
 *                  lib/hos/dutyCommand.ts; always `source: "Driver App"` and `clientEventId`
 *                  (= this command's id), pinned in lib/wire.test.ts.
 *   `hos.certify`  the driver certifies one day → POST …/hos/days/{date}/certify. Payload
 *                  `{ date }` (DateOnly, America/Winnipeg).
 *
 * HELD ONLY: everything else. The remaining hos.* kinds (exception, eld-malfunction, pc,
 * other-carrier) arrive with the next slice.
 */
export type CommandKind =
  | "hos.duty"
  | "hos.certify"
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
   * duty event. The duty-events endpoint honours it (as the eventId alias); the others do not
   * yet, and the client sends it from day one so they can start honouring it with no client
   * change.
   */
  id: string;
  kind: CommandKind;
  payload: unknown;
  /** ISO 8601, device clock. Server-side handling must tolerate clock skew. */
  createdAt: string;
  /**
   * Capture order on this device, monotonic across reloads. The drain sends in this order —
   * a duty change must reach the server before the certification of the day it belongs to —
   * and two taps inside one millisecond share a `createdAt`, so the clock cannot be the key.
   */
  seq: number;
  attempts: number;
  status: CommandStatus;
  /** Set when status is "failed" — the server's own message, shown to the driver, never swallowed. */
  error?: string;
  /** The server's error code when status is "failed" (e.g. "Drivers.Hos.PcSegmentOpen"). */
  errorCode?: string;
}

/**
 * What the always-visible sync indicator renders. Never let sync state be invisible: a driver
 * must be able to tell "this is saved on the device" from "this has reached the server".
 */
export interface SyncState {
  /** Whether the device believes it has a network. Not whether the API is reachable. */
  online: boolean;
  /** Commands captured but not yet accepted by the server (pending + sending). */
  pending: number;
  /** Commands the server rejected (4xx), parked for the driver's attention. */
  failed: number;
  /** ISO 8601 of the last drain that sent something, or null if never. */
  lastSyncedAt: string | null;
  /**
   * True only when EVERY kind has a sender — i.e. never, in this build. The pill reads it and
   * never shows a green check while it is false: a scaffold that LOOKS like it syncs is how a
   * hard requirement quietly dies. Partial reality is reported through `enabledKinds`.
   */
  enabled: boolean;
  /** The kinds a sender is registered for — today `hos.duty` and `hos.certify` (lib/hos/ledger.ts). */
  enabledKinds: CommandKind[];
}
