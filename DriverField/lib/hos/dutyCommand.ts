import type { DutyEventInput, DutyStatusWire, EventSourceWire } from "../api/hos";
import { HosWire, type DutyStatus } from "./engine";

// The `hos.duty` command's payload — what the sync queue captures when the driver changes duty
// status — and the one place it is mapped onto the endpoint's body when the queue drains.
// One place builds it so the two fields that fix the known mislabel bug can never be left off:
// until recently the API hardcoded every HOS submission to the manual source, so a tablet entry
// was recorded as a paper backup keyed by a dispatcher who never touched it.
//
//   source         always HosWire.DriverAppSource ("Driver App") — the tablet never records a
//                  transcription or a paper log; those are the console's.
//   clientEventId  the queued command's own id. It is the Idempotency-Key the drain() sends, the
//                  key lib/hos/ledger.ts reconciles the optimistic event on, and the `eventId`
//                  the endpoint's idempotent 201/200 is keyed on.
//   duty           the wire string (HosWire.status), never the enum member.
//   atUtc          ISO 8601, the instant the driver tapped (device clock).
//
// This is the QUEUE's capture shape, not the endpoint DTO. `dutyEventInputFrom` is the crossing
// onto `POST /api/drivers/{driverId}/hos/duty-events` (DutyEventRequest): `duty` becomes
// `status`, the command id becomes `eventId`, and the source is written again rather than
// trusted from the stored payload. lib/wire.test.ts pins both shapes.

export interface DutyCommandPayload {
  duty: string;
  atUtc: string;
  source: string;
  clientEventId: string;
}

export function dutyCommandPayload(status: DutyStatus, atUtc: string, clientEventId: string): DutyCommandPayload {
  return {
    duty: HosWire.status(status),
    atUtc,
    source: HosWire.DriverAppSource,
    clientEventId,
  };
}

/** The payload as the queue stored it — nothing else is trusted. */
export function isDutyCommandPayload(payload: unknown): payload is DutyCommandPayload {
  if (typeof payload !== "object" || payload === null) return false;
  const p = payload as Record<string, unknown>;
  return typeof p.duty === "string" && typeof p.atUtc === "string" && typeof p.clientEventId === "string";
}

/**
 * The endpoint body for one captured duty change. The command id is the eventId (and the
 * Idempotency-Key, which lib/api/hos.ts repeats from it); the source is ALWAYS "Driver App"
 * from this app, whatever the stored payload says. Throws on a payload that is not a duty
 * command — the drain parks it with that message rather than sending junk.
 */
export function dutyEventInputFrom(payload: unknown, commandId: string): DutyEventInput {
  if (!isDutyCommandPayload(payload)) {
    throw new Error("Not a duty-status command payload.");
  }
  if (HosWire.statusFrom(payload.duty) === null) {
    throw new Error(`"${payload.duty}" is not a duty status the API accepts.`);
  }
  return {
    eventId: commandId,
    atUtc: payload.atUtc,
    status: payload.duty as DutyStatusWire,
    source: HosWire.DriverAppSource as EventSourceWire,
  };
}

/** The `hos.certify` command's payload: the regulation day (DateOnly, America/Winnipeg). */
export interface CertifyCommandPayload {
  date: string;
}

export function isCertifyCommandPayload(payload: unknown): payload is CertifyCommandPayload {
  if (typeof payload !== "object" || payload === null) return false;
  const p = payload as Record<string, unknown>;
  return typeof p.date === "string" && /^\d{4}-\d{2}-\d{2}$/.test(p.date);
}
