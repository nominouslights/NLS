import type { DutyEventInput, DutyStatusWire, EventSourceWire } from "../api/hos";
import { HosWire, type DutyStatus } from "./engine";

// The `hos.duty` command's payload — what the sync queue captures when the driver changes duty
// status — and the one place it is mapped onto the endpoint's body when the queue drains.
// One place builds it so the two fields that fix the known mislabel bug can never be left off:
// until recently the API hardcoded every HOS submission to the manual source, so a tablet entry
// was recorded as a paper backup keyed by a dispatcher who never touched it.
//
//   source         HosWire.DriverAppSource ("Driver App") for everything the driver does on
//                  this tablet, and HosWire.OtherCarrierSource ("Other carrier (declared)") for
//                  the one thing that is not their own work here — hours declared for another
//                  carrier (s.82(1)(f)), which always carry `carrierName` and `enteredBy`. The
//                  tablet never records a transcription or a paper log; those are the console's.
//   clientEventId  the queued command's own id. It is the Idempotency-Key the drain() sends, the
//                  key lib/hos/ledger.ts reconciles the optimistic event on, and the `eventId`
//                  the endpoint's idempotent 201/200 is keyed on.
//   duty           the wire string (HosWire.status), never the enum member.
//   atUtc          ISO 8601, the instant the driver tapped (device clock) — or, for an
//                  other-carrier day, the instant on that day the event is laid out at.
//
// PERSONAL CONVEYANCE rides on the same kind. The event that STARTS a PC segment carries the
// start odometer and the three attestations the endpoint requires (DutyDay.Validate:
// PcOdometerRequired, PcAttestationRequired); the event that ENDS it — the next duty change,
// whatever its status — carries `odometerEndKm` (PcOdometerEndRequired, PcOdometerDecreasing).
// The tablet's modal asks four attestations; the fourth ("Bus is empty") folds into
// pcNoPassengersOrCargo on the wire, which is the server's field for it.
//
// This is the QUEUE's capture shape, not the endpoint DTO. `dutyEventInputFrom` is the crossing
// onto `POST /api/drivers/{driverId}/hos/duty-events` (DutyEventRequest): `duty` becomes
// `status`, the command id becomes `eventId`, and the source is written again rather than
// trusted from the stored payload — "Driver App" unless the payload is a complete other-carrier
// declaration. lib/wire.test.ts pins both shapes.

export interface DutyCommandPayload {
  duty: string;
  atUtc: string;
  source: string;
  clientEventId: string;
  /** Personal conveyance: the reading that starts the segment (km, one decimal). */
  odometerStartKm?: number | null;
  /** The reading on the event that ends a personal-conveyance segment. */
  odometerEndKm?: number | null;
  pcNoPassengersOrCargo?: boolean;
  pcNoTrailer?: boolean;
  pcNoWork?: boolean;
  /** Other-carrier declarations only. */
  carrierName?: string | null;
  enteredBy?: string | null;
  note?: string | null;
}

/** The optional fields a duty change may carry beyond status and instant. */
export type DutyCommandExtras = Omit<DutyCommandPayload, "duty" | "atUtc" | "source" | "clientEventId">;

/** The fields that start a personal-conveyance segment, as the modal collects them. */
export interface PersonalConveyanceStart {
  odometerStartKm: number;
  pcNoPassengersOrCargo: true;
  pcNoTrailer: true;
  pcNoWork: true;
}

export function dutyCommandPayload(status: DutyStatus, atUtc: string, clientEventId: string, extras: DutyCommandExtras = {}): DutyCommandPayload {
  const otherCarrier = typeof extras.carrierName === "string" && extras.carrierName.trim().length > 0;
  return {
    duty: HosWire.status(status),
    atUtc,
    source: otherCarrier ? HosWire.OtherCarrierSource : HosWire.DriverAppSource,
    clientEventId,
    ...extras,
  };
}

/** The payload as the queue stored it — nothing else is trusted. */
export function isDutyCommandPayload(payload: unknown): payload is DutyCommandPayload {
  if (typeof payload !== "object" || payload === null) return false;
  const p = payload as Record<string, unknown>;
  return typeof p.duty === "string" && typeof p.atUtc === "string" && typeof p.clientEventId === "string";
}

/** A finite, non-negative km reading, or null for anything else. */
export function kmOrNull(value: unknown): number | null {
  return typeof value === "number" && Number.isFinite(value) && value >= 0 ? value : null;
}

/**
 * Whether the stored payload is a complete other-carrier declaration: the other-carrier
 * source AND a carrier name. Anything less goes over the wire as "Driver App", because the
 * endpoint refuses that source without a name (CarrierNameRequired) and a half-formed
 * declaration must not reach the record as the driver's own work under a foreign label.
 */
export function isOtherCarrierPayload(payload: DutyCommandPayload): boolean {
  return payload.source === HosWire.OtherCarrierSource && typeof payload.carrierName === "string" && payload.carrierName.trim().length > 0;
}

/**
 * The endpoint body for one captured duty change. The command id is the eventId (and the
 * Idempotency-Key, which lib/api/hos.ts repeats from it); the source is written here, never
 * trusted from the stored payload: "Driver App", or "Other carrier (declared)" only for a
 * complete declaration. Throws on a payload that is not a duty command — the drain parks it
 * with that message rather than sending junk.
 */
export function dutyEventInputFrom(payload: unknown, commandId: string): DutyEventInput {
  if (!isDutyCommandPayload(payload)) {
    throw new Error("Not a duty-status command payload.");
  }
  if (HosWire.statusFrom(payload.duty) === null) {
    throw new Error(`"${payload.duty}" is not a duty status the API accepts.`);
  }
  const otherCarrier = isOtherCarrierPayload(payload);
  const input: DutyEventInput = {
    eventId: commandId,
    atUtc: payload.atUtc,
    status: payload.duty as DutyStatusWire,
    source: (otherCarrier ? HosWire.OtherCarrierSource : HosWire.DriverAppSource) as EventSourceWire,
  };
  if (otherCarrier) {
    input.carrierName = payload.carrierName!.trim();
    if (typeof payload.enteredBy === "string" && payload.enteredBy.trim().length > 0) input.enteredBy = payload.enteredBy.trim();
  }
  if (typeof payload.note === "string" && payload.note.trim().length > 0) input.note = payload.note.trim();

  const start = kmOrNull(payload.odometerStartKm);
  if (start !== null) {
    input.odometerStartKm = start;
    input.pcNoPassengersOrCargo = payload.pcNoPassengersOrCargo === true;
    input.pcNoTrailer = payload.pcNoTrailer === true;
    input.pcNoWork = payload.pcNoWork === true;
  }
  const end = kmOrNull(payload.odometerEndKm);
  if (end !== null) input.odometerEndKm = end;
  return input;
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
