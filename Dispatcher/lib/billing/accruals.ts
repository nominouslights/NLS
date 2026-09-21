// Monthly per-client accruals — the ONE derivation that turns a month of trips
// (plus the invoice details their billing states reference) into the report the
// Reports screen renders, the NL-ACC-01 sheet prints, and the clipboard export
// copies. All three consume the same AccrualsReport so they can never disagree
// about a bucket, a pairing, or a dollar. Sibling of tripDetail.ts, and shaped
// so an email payload mapper (Part B) can be added here without reshaping it.
//
// Ground rules, in order of how much they bite:
//   - The money-bearing unit is a ROUND-TRIP GROUP, and what defines the group
//     depends on whether the work has been billed yet:
//       * BILLED (paid / invoiced / written off) → the INVOICE LINE. The line is
//         what the client is actually billed on, and it is the only pairing that
//         can disagree with the money. roundTripKey is set only by the merge /
//         Create Return flow, so a pair invoiced as one $X line routinely
//         carries no key on either leg; keying those by roundTripKey splits them
//         into two groups that each resolve to that one line and each claim its
//         full amount — the bucket total doubles. Group by line and the report
//         mirrors the invoice by construction.
//       * UNBILLED (ready / scheduled / upcoming) → roundTripKey, falling back
//         to the trip's own id. There is no line yet, so the trip records are
//         the only pairing signal there is; an unpaired leg stays unpaired
//         rather than being guessed into a pair.
//   - Real amounts (invoiced / paid / written off) come ONLY from fetched
//     invoice lines, summed over DISTINCT lineIds per group, and each lineId is
//     claimed at most ONCE per report — so a line can never be counted twice,
//     even if its legs somehow land in different buckets. Missing invoice →
//     "amount unavailable", excluded from totals.
//   - Estimates (ready / scheduled / upcoming) MIRROR the invoice generator leg
//     for leg — Backend/src/Billing/Domain/Invoices/InvoiceDraftBuilder.cs is
//     the authority, and this file's job is to agree with it. Rates are now
//     PER PURCHASE ORDER, with the contract as the fallback:
//
//         contractRate  = contract.ratePerRoundTripCad (RoundTripRate model)
//         group by (EFFECTIVE PO number, roundTripKey)
//           effective PO = trip.poNumber ?? contract.defaultPoNumber
//           roundTripRate = po?.roundTripRateCad ?? contractRate
//           oneWayRate    = po?.oneWayRateCad    ?? roundTripRate * 0.5
//           Outbound leg AND Inbound leg → 1 × roundTripRate
//           otherwise                    → legs.length × oneWayRate, flagged
//
//     The resolution itself is poEffectiveTerms() in lib/api/clients.ts, beside
//     the PurchaseOrderRecord it resolves, so the PO dashboard that edits a rate
//     and the report that prices from it can never diverge.
//       * Grouping by PO is what implements "a round trip must be a single PO":
//         legs on different POs land in different groups, so each becomes a lone
//         one-way leg automatically, carrying the splitPo flag.
//       * ANY unpaired group — a lone leg, two same-direction legs, a
//         direction-less leg — bills EACH leg on its own line at the one-way
//         rate, flagged for review (the oneWay flag), mirroring
//         InvoiceDraftBuilder's unpaired-leg line.
//       * a group whose legs carry NO roundTripKey is never picked up by the
//         draft builder at all (it filters on `RoundTripKey is not null`) and
//         becomes a hand-keyed manual line. That one is genuinely unpriceable,
//         so it stays unpriced with the "manual" note rather than guessed.
//     Pricing a lone leg is the honest estimate, not a rounding liberty: an
//     earlier version of this comment claimed the backend prices complete round
//     trips only, and that wrong fact is why lone legs went unpriced for months.
//     Manual billing / no contract / no rate on either the PO or the contract →
//     no estimate for that group, one banner note.
//   - PO warnings never block, by the owner's decision: a trip outside its PO's
//     [issued, expiry] window and a PO whose committed + upcoming work exceeds
//     its authorised value are both flagged and still counted.
//   - No tax appears anywhere in this report. The platform never computes or
//     displays a tax amount — QuickBooks Online owns all tax calculation — so
//     every amount here, estimated or invoiced, is a bare line total in CAD.

import {
  formatInvoiceCad,
  invoiceChip,
  periodLabel as invoicePeriodLabel,
  type InvoiceDetailRecord,
  type InvoiceLineRecord,
} from "@/lib/api/billing";
import type { AccrualsEmailReport } from "@/lib/api/notifications";
import {
  contractRateLabel,
  contractRoundTripRateCad,
  isWithinPoWindow,
  poEffectiveTerms,
  poTermsLabel,
  type ClientRecord,
  type PurchaseOrderRecord,
} from "@/lib/api/clients";
import { corridorLabel, sortTrips, type TripRecord } from "@/lib/api/trips";
import { periodLabel, type Period } from "@/lib/period";
import type { StatusKind } from "@/lib/theme";

// ---------------------------------------------------------------------------
// Buckets
// ---------------------------------------------------------------------------

export type AccrualBucketId = "paid" | "invoiced" | "ready" | "scheduled" | "upcoming";

/**
 * Fixed bucket order — and NOT merely a display order, so do not reorder it to
 * change what the report leads with. It is the CLAIM order for the claimedLines
 * set below: buckets are built in this sequence, so when one invoice line's legs
 * land in two different buckets the earlier bucket carries the money and the
 * later row says "counted with the paired leg". Reordering these five ids
 * silently moves dollars from one bucket to another and breaks nothing loudly.
 *
 * Presentation order lives in ACCRUAL_SECTION_ORDER instead — a parallel view
 * over these same bucket objects, which is what the screen and the sheets lead
 * with. report.buckets stays in THIS order for the claim rule, the summary
 * table, accrualTotals, and the empty-month check.
 */
export const ACCRUAL_BUCKET_ORDER: AccrualBucketId[] = [
  "paid",
  "invoiced",
  "ready",
  "scheduled",
  "upcoming",
];

/** Chip kind + label + one-line explainer per bucket. The kind pairs a colour
 *  with StatusChip's glyph and this label — colour never stands alone. */
export const ACCRUAL_BUCKET_META: Record<
  AccrualBucketId,
  { label: string; kind: StatusKind; hint: string }
> = {
  paid: { label: "Paid", kind: "ontime", hint: "Payment received — amounts are the issued invoice lines" },
  invoiced: { label: "Invoiced", kind: "info", hint: "Billed, awaiting payment — amounts are the issued invoice lines" },
  ready: { label: "Ready for billing", kind: "soon", hint: "Run complete, not yet invoiced — amounts are PO-rate estimates" },
  scheduled: { label: "Scheduled", kind: "info", hint: "Run due or under way — amounts are PO-rate estimates" },
  upcoming: { label: "Upcoming", kind: "off", hint: "Scheduled after today — amounts are PO-rate estimates" },
};

/**
 * Which accrual bucket a trip belongs to, or null for the reconciliation
 * section (Cancelled / WrittenOff — never counted as accruals). `today` splits
 * Scheduled: a service date after today is genuinely upcoming, on/before today
 * it is a run that should already be under way, counted with InProgress.
 */
export function accrualBucketFor(t: TripRecord, today: string): AccrualBucketId | null {
  switch (t.status) {
    case "Completed":
      return "paid"; // Completed = payment arrived, for client trips
    case "Invoiced":
      return "invoiced";
    case "ReadyForBilling":
      return "ready"; // OnWorksheet drafts stay here — a draft is not an invoice
    case "InProgress":
      return "scheduled";
    case "Scheduled":
      return t.serviceDate > today ? "upcoming" : "scheduled";
    case "Cancelled":
    case "WrittenOff":
      return null;
    default:
      return t.status satisfies never;
  }
}

// ---------------------------------------------------------------------------
// Shapes
// ---------------------------------------------------------------------------

export type AccrualAmountSource = "invoice" | "estimate";

/** Why a group carries no amount — rendered as colour + glyph + text, never
 *  colour alone. Null amountNote with null amountCad means the banner notes
 *  explain it (manual billing / no contract / no rate on the contract). */
export type AccrualAmountNote = "unavailable" | "manual" | "counted";

export const AMOUNT_NOTE_META: Record<AccrualAmountNote, { kind: StatusKind; label: string }> = {
  unavailable: { kind: "over", label: "Amount unavailable" },
  // No roundTripKey on any leg: InvoiceDraftBuilder never claims these, so they
  // reach an invoice only as a hand-keyed line. Estimating one would print a
  // figure no generated invoice will ever contain.
  manual: { kind: "soon", label: "Manual line — not estimated" },
  // Only reachable when one invoice line's legs land in different buckets: the
  // first bucket carries the money, this row says so rather than repeating it.
  counted: { kind: "info", label: "Counted with the paired leg" },
};

/** A caveat on an amount that DOES exist — deliberately separate from
 *  amountNote, which answers "why is there no amount". A one-way group has a
 *  figure, so folding this into amountNote would corrupt unpricedCount. */
export type AccrualAmountFlag = "oneWay" | "splitPo";

/**
 * Labels are the INVOICE's words, sentence-cased — the backend's review-flag
 * constants, which the generated invoice line carries:
 *   "one-way leg — review"
 *   "round-trip legs on different POs — priced one-way, review"
 *   "outside PO window — review"
 *   "PO value exceeded — review"
 * The report mirrors the invoice by construction, so a client must never see
 * this report and that invoice describe one condition in two different phrasings.
 * Changing a label here without changing the backend constant (or the reverse) is
 * the drift this note exists to prevent.
 */
export const AMOUNT_FLAG_META: Record<AccrualAmountFlag, { kind: StatusKind; label: string }> = {
  // Each leg of an unpaired group bills on its own line at the PO's one-way rate
  // (half the effective round-trip rate when the PO records none).
  oneWay: { kind: "soon", label: "One-way leg — review" },
  // The legs of one roundTripKey sit on DIFFERENT purchase orders. A round trip
  // must be a single PO (owner's decision), so each side prices as its own
  // one-way trip — the stronger of the two flags, hence it wins.
  splitPo: { kind: "over", label: "Round-trip legs on different POs — priced one-way, review" },
};

/** A PO-level warning on a group. Never blocking, never a reason an amount is
 *  missing — the group is still priced and still counted. Colour + glyph +
 *  text, like every other status in the console, and the invoice's own words. */
export type AccrualPoFlag = "outsideWindow";

export const PO_FLAG_META: Record<AccrualPoFlag, { kind: StatusKind; label: string }> = {
  outsideWindow: { kind: "over", label: "Outside PO window — review" },
};

/** One money-bearing row: a round-trip pair, or a lone leg. */
export interface AccrualGroup {
  /** `line:<lineId>` for billed groups (the invoice line is the unit);
   *  `po:<effective PO>|<roundTripKey or the lone trip's own id>` for unbilled
   *  ones — the PO is part of the key because it is part of the price. */
  key: string;
  /** The EFFECTIVE PO number that priced this group:
   *  trip.poNumber ?? contract.defaultPoNumber, or null when neither exists. */
  poNumber: string | null;
  /** True when poNumber came from the contract default rather than the trips. */
  poFromContractDefault: boolean;
  /** PO-level warnings — additive, never blocking (see PO_FLAG_META). */
  poFlags: AccrualPoFlag[];
  /** Legs in service order — two for a matched pair, one or more otherwise. */
  legs: TripRecord[];
  /** A complete round trip by InvoiceDraftBuilder's test: an Outbound leg AND
   *  an Inbound one are both present. Never merely a leg count — three legs or
   *  two same-direction legs are not a pair, and the invoice prices them as
   *  halves. */
  paired: boolean;
  /** Dollars attributed to this group; null = unpriced (see amountNote). */
  amountCad: number | null;
  amountSource: AccrualAmountSource | null;
  amountNote: AccrualAmountNote | null;
  /** A caveat on an amount that exists (one-way / split-PO) — never a reason
   *  there is none. Rendered beside the figure as colour + glyph + text. */
  amountFlag: AccrualAmountFlag | null;
  /** Issued invoice reference (QBO number once entered, worksheet number until). */
  invoiceNumber: string | null;
  /** Draft worksheet already claiming the group — shown as "On worksheet …". */
  onWorksheetNumber: string | null;
}

export interface AccrualBucket {
  id: AccrualBucketId;
  label: string;
  kind: StatusKind;
  groups: AccrualGroup[];
  /** Sum of real invoice-line dollars in this bucket. */
  actualCad: number;
  /** Sum of contract-rate estimates in this bucket (always shown " est."). */
  estimatedCad: number;
  /** Groups carrying no amount at all. */
  unpricedCount: number;
}

// ---------------------------------------------------------------------------
// Sections — the PRESENTATION grouping over the same five buckets. Two headline
// questions lead the report ("what is coming" / "what do I owe"), and settled
// money closes it on one line. No dollar moves between buckets to make this
// happen: a section simply holds references to the bucket objects it covers.
// ---------------------------------------------------------------------------

export type AccrualSectionId = "upcoming" | "owed" | "settled";

/** Reading order of the report: not-yet-done work first, then monies owed,
 *  then the settled closing line. Safe to reorder — unlike
 *  ACCRUAL_BUCKET_ORDER, nothing about money attribution depends on it. */
export const ACCRUAL_SECTION_ORDER: AccrualSectionId[] = ["upcoming", "owed", "settled"];

export const ACCRUAL_SECTION_META: Record<
  AccrualSectionId,
  {
    label: string;
    hint: string;
    kind: StatusKind;
    /** Member buckets, in the order they render inside the section. */
    buckets: AccrualBucketId[];
    /** Does this section print per-trip detail tables? */
    detail: boolean;
  }
> = {
  upcoming: {
    label: "Upcoming work — not yet done",
    kind: "soon",
    hint: "Scheduled trips not yet run — amounts are contract-rate estimates",
    buckets: ["upcoming", "scheduled"],
    detail: true,
  },
  owed: {
    label: "Monies owed",
    kind: "info",
    hint: "Work already performed — issued invoices plus runs not yet billed",
    buckets: ["ready", "invoiced"],
    detail: true,
  },
  settled: {
    label: "Settled this month",
    kind: "ontime",
    hint: "Payment received — no further action",
    buckets: ["paid"],
    // One closing summary line, no per-trip table: settled work still has to
    // reconcile the month, but it is not what the report is for.
    detail: false,
  },
};

export interface AccrualSection {
  id: AccrualSectionId;
  label: string;
  hint: string;
  kind: StatusKind;
  /** The member buckets, in ACCRUAL_SECTION_META order — the SAME objects as
   *  report.buckets, not copies. */
  buckets: AccrualBucket[];
  groupCount: number;
  actualCad: number;
  estimatedCad: number;
  unpricedCount: number;
  detail: boolean;
}

// ---------------------------------------------------------------------------
// Purchase-order authorisation — the payoff for the upcoming-expenses headline:
// what each PO authorises against what this period's work draws down on it.
// Money already invoiced plus work not yet billed, per PO, so a PO about to be
// overspent is visible BEFORE the run happens rather than at invoice time.
// A breach warns; it never blocks (owner's decision).
// ---------------------------------------------------------------------------

export interface AccrualPoCommitment {
  /** The effective PO number the groups were priced under. */
  poNumber: string | null;
  /** The matching PurchaseOrderRecord, or null — the number came from a trip /
   *  the contract default but no PO record was found (or POs failed to load). */
  po: PurchaseOrderRecord | null;
  /** Authorised value of the PO — null when unrecorded, so no overage test. */
  amountCad: number | null;
  /** Real invoice dollars in this period's buckets (invoiced + paid). */
  invoicedCad: number;
  /** Estimated dollars for work not yet billed (ready + scheduled + upcoming). */
  upcomingCad: number;
  /** invoicedCad + upcomingCad. */
  committedCad: number;
  /** committedCad − amountCad, only when positive. Null = within the PO value. */
  overageCad: number | null;
  /** amountCad − committedCad when the PO value is known and not exceeded. */
  remainingCad: number | null;
  /** The effective terms this PO priced at, as one line (poTermsLabel). */
  termsLabel: string;
  /** Chip kind + label — colour never stands alone. */
  kind: StatusKind;
  label: string;
}

export interface AccrualsReport {
  client: ClientRecord;
  period: Period;
  /** The local operational day the scheduled/upcoming split was made against. */
  today: string;
  /** All five buckets in ACCRUAL_BUCKET_ORDER — empty buckets included. This is
   *  the claim order and the summary table; read `sections` to render. */
  buckets: AccrualBucket[];
  /** The same buckets grouped for reading, in ACCRUAL_SECTION_ORDER. */
  sections: AccrualSection[];
  /** Cancelled trips with reasons — reconciliation, never counted as accruals. */
  cancelled: TripRecord[];
  /** Written-off groups with reasons + lost amounts — reconciliation. */
  writtenOff: AccrualGroup[];
  /** The fetched invoices behind the real amounts. */
  invoices: InvoiceDetailRecord[];
  /** One row per PO this period's work was priced under, in PO-number order
   *  (a no-PO row last). Rendered by the screen and the printed sheet; the
   *  overage warnings also ride `notes`, so all four consumers carry them. */
  poCommitments: AccrualPoCommitment[];
  /** Degradation banners: manual billing, missing rate, failed fetches,
   *  unpaired (one-way) groups, split-PO pairs, trips outside their PO window,
   *  POs over their authorised value, and no-key groups left for a manual line. */
  notes: string[];
}

/** Footer wording shared by the screen, the printed sheet, and the clipboard. */
export const ACCRUALS_TAX_NOTE =
  "All amounts CAD. Taxes are applied in QuickBooks; this report contains none.";
export const ACCRUALS_ESTIMATE_NOTE =
  "Amounts marked “est.” are estimates at the purchase order’s rates (the contract rate where a PO records none), not invoices — issued invoice amounts govern.";

// ---------------------------------------------------------------------------
// Assembly
// ---------------------------------------------------------------------------

/**
 * Group already-sorted trips into money-bearing rows, preserving
 * first-appearance order so groups stay in service order.
 *
 * `lineByTripId` non-null means these trips are billed, so the invoice line is
 * the grouping key wherever a leg resolves to one — see the header note: the
 * line pairs legs that roundTripKey often does not, and grouping billed legs
 * any other way double-counts the line's amount. Legs with no resolvable line
 * (and every unbilled trip) key on (EFFECTIVE PO, roundTripKey ?? own id): the
 * PO is part of the key because it is part of the price, and that is exactly
 * what implements "a round trip must be a single PO" — legs on two POs land in
 * two groups and each becomes a lone one-way leg with no special case anywhere.
 */
function groupTrips(
  sorted: TripRecord[],
  lineByTripId: Map<string, InvoiceLineRecord> | null,
  effectivePo: (t: TripRecord) => string | null,
): { key: string; legs: TripRecord[] }[] {
  const byKey = new Map<string, TripRecord[]>();
  for (const t of sorted) {
    const line = lineByTripId?.get(t.id);
    const key = line
      ? `line:${line.lineId}`
      : `po:${effectivePo(t) ?? ""}|${t.roundTripKey ?? t.id}`;
    const legs = byKey.get(key);
    if (legs) legs.push(t);
    else byKey.set(key, [t]);
  }
  return [...byKey.entries()].map(([key, legs]) => ({ key, legs }));
}

/**
 * Is this group a complete round trip? InvoiceDraftBuilder's exact test: one
 * Outbound leg AND one Inbound leg must both be present. Leg COUNT is not the
 * test — three legs, or two legs in the same direction, price as halves — and
 * getting this wrong is what mis-estimated 3-leg groups at 1 × rate.
 */
function isRoundTripPair(legs: TripRecord[]): boolean {
  return legs.some((l) => l.direction === "Outbound") && legs.some((l) => l.direction === "Inbound");
}

/** The issued-invoice reference for a group of legs — the QBO number once the
 *  invoice was keyed, the worksheet number until then (tripBillingChip's rule). */
function issuedRef(legs: TripRecord[]): string | null {
  for (const leg of legs) {
    if (leg.billing) return leg.billing.qboInvoiceId ?? leg.billing.invoiceNumber;
  }
  return null;
}

export function buildAccrualsReport(args: {
  client: ClientRecord;
  period: Period;
  /** From todayIso() — passed in so the derivation stays pure and testable. */
  today: string;
  /** Every trip for the client in the period (listTrips unpaged = complete). */
  trips: TripRecord[];
  /** Successfully fetched invoice details for the billing.invoiceIds the trips
   *  reference. A referenced id missing here reads as a failed fetch — the
   *  affected amounts degrade to "unavailable", never to a guess. */
  invoices: InvoiceDetailRecord[];
  /** Every PO on file for the client (listPurchaseOrders). Their rates override
   *  the contract rate; an empty list prices exactly as it did before per-PO
   *  terms existed, at the contract rate. */
  purchaseOrders: PurchaseOrderRecord[];
  /** True when the PO fetch failed. Pricing degrades to the contract rate and
   *  one banner says so — a failed PO fetch never blanks the report. */
  purchaseOrdersUnavailable?: boolean;
}): AccrualsReport {
  const { client, period, today, invoices, purchaseOrders } = args;

  // Real amounts resolve trip → invoice line. A trip is claimed by at most one
  // line (the backend rejects double-invoicing), so first-hit is authoritative.
  const fetchedInvoiceIds = new Set(invoices.map((inv) => inv.id));
  const lineByTripId = new Map<string, InvoiceLineRecord>();
  for (const inv of invoices) {
    for (const line of inv.lines) {
      for (const tripId of line.tripIds) {
        if (!lineByTripId.has(tripId)) lineByTripId.set(tripId, line);
      }
    }
  }

  // Rate resolution: the PO overrides, the contract is the fallback.
  const contract = client.activeContract;
  const contractRate = contractRoundTripRateCad(contract);
  const poByNumber = new Map(purchaseOrders.map((p) => [p.poNumber, p] as const));

  /** The effective PO number of a trip — trip.poNumber, else the contract
   *  default. ONE definition, because it decides both the grouping key and the
   *  rate, and those two must never disagree. */
  const effectivePo = (t: TripRecord): string | null =>
    t.poNumber ?? contract?.defaultPoNumber ?? null;
  const poRecord = (poNumber: string | null): PurchaseOrderRecord | null =>
    poNumber ? poByNumber.get(poNumber) ?? null : null;

  // Partition the month into buckets + the two reconciliation lists.
  const sorted = sortTrips(args.trips);
  const byBucket: Record<AccrualBucketId, TripRecord[]> = {
    paid: [],
    invoiced: [],
    ready: [],
    scheduled: [],
    upcoming: [],
  };
  const cancelled: TripRecord[] = [];
  const writtenOffTrips: TripRecord[] = [];
  for (const t of sorted) {
    const bucket = accrualBucketFor(t, today);
    if (bucket) byBucket[bucket].push(t);
    else if (t.status === "Cancelled") cancelled.push(t);
    else writtenOffTrips.push(t);
  }

  // Sum the DISTINCT invoice lines the legs resolve to. Billed groups are keyed
  // by line, so in practice that is one line per group; claimedLines makes the
  // "at most once per report" rule hold even if a line's legs land in different
  // buckets (one leg Invoiced, its pair already Paid), where grouping alone
  // cannot help. Buckets are built in ACCRUAL_BUCKET_ORDER, so the earlier
  // bucket carries the money and the later row says where it went.
  const missingInvoiceIds = new Set<string>();
  const claimedLines = new Set<string>();
  function invoiceAmount(legs: TripRecord[]): Pick<
    AccrualGroup,
    "amountCad" | "amountSource" | "amountNote" | "amountFlag"
  > {
    const lines = new Map<string, InvoiceLineRecord>();
    let countedElsewhere = false;
    for (const leg of legs) {
      const line = lineByTripId.get(leg.id);
      if (line) {
        if (claimedLines.has(line.lineId)) countedElsewhere = true;
        else lines.set(line.lineId, line);
      } else if (leg.billing && !fetchedInvoiceIds.has(leg.billing.invoiceId)) {
        missingInvoiceIds.add(leg.billing.invoiceId);
      }
    }
    if (lines.size === 0) {
      return countedElsewhere
        ? { amountCad: null, amountSource: null, amountNote: "counted", amountFlag: null }
        : { amountCad: null, amountSource: null, amountNote: "unavailable", amountFlag: null };
    }
    let sum = 0;
    for (const [lineId, line] of lines) {
      claimedLines.add(lineId);
      sum += line.amountCad;
    }
    return { amountCad: sum, amountSource: "invoice", amountNote: null, amountFlag: null };
  }

  // "A round trip must be a single PO": which POs does each roundTripKey's
  // unbilled work sit on? More than one and every unpaired side of it is a split
  // pair. Computed over ALL unbilled trips, not per bucket, because the two legs
  // routinely straddle buckets (one still Scheduled, its return already
  // ReadyForBilling) and the split is a property of the round trip, not a bucket.
  const posByRoundTripKey = new Map<string, Set<string>>();
  for (const t of [...byBucket.ready, ...byBucket.scheduled, ...byBucket.upcoming]) {
    if (!t.roundTripKey) continue;
    const seen = posByRoundTripKey.get(t.roundTripKey) ?? new Set<string>();
    seen.add(effectivePo(t) ?? "");
    posByRoundTripKey.set(t.roundTripKey, seen);
  }
  function isSplitPo(legs: TripRecord[]): boolean {
    const key = legs[0].roundTripKey;
    if (!key) return false;
    return (posByRoundTripKey.get(key)?.size ?? 0) > 1;
  }

  /**
   * Estimate a group exactly the way InvoiceDraftBuilder.Build would price it,
   * at the EFFECTIVE terms of the group's PO (poEffectiveTerms — the PO
   * overrides, the contract rate is the fallback, and a missing one-way rate is
   * half the effective round-trip rate):
   *   - no usable rate            → nothing estimated, one banner note explains;
   *   - no roundTripKey anywhere  → the draft builder never claims it, so it can
   *                                 only become a manual line: unpriced, noted;
   *   - Outbound AND Inbound leg  → one line, 1 × the round-trip rate;
   *   - anything else             → one line PER LEG at the one-way rate,
   *                                 flagged for review (splitPo when the round
   *                                 trip's other leg sits on a different PO).
   */
  function estimateAmount(
    legs: TripRecord[],
    paired: boolean,
    poNumber: string | null,
  ): Pick<AccrualGroup, "amountCad" | "amountSource" | "amountNote" | "amountFlag"> {
    const terms = poEffectiveTerms(poRecord(poNumber), contractRate);
    const rate = paired ? terms.roundTripRateCad : terms.oneWayRateCad;
    if (rate === null) {
      return { amountCad: null, amountSource: null, amountNote: null, amountFlag: null };
    }
    if (!legs.some((l) => l.roundTripKey)) {
      return { amountCad: null, amountSource: null, amountNote: "manual", amountFlag: null };
    }
    if (paired) {
      return { amountCad: rate, amountSource: "estimate", amountNote: null, amountFlag: null };
    }
    return {
      amountCad: legs.length * rate,
      amountSource: "estimate",
      amountNote: null,
      amountFlag: isSplitPo(legs) ? "splitPo" : "oneWay",
    };
  }

  /** Finish one group: the PO facts, then the money. `realAmounts` picks the
   *  invoice-line path (billed work) over the estimator (unbilled work). */
  function finishGroup(key: string, legs: TripRecord[], realAmounts: boolean): AccrualGroup {
    const paired = isRoundTripPair(legs);
    const poNumber = effectivePo(legs[0]);
    const po = poRecord(poNumber);
    const onWorksheet = legs.find((l) => l.billing?.state === "OnWorksheet")?.billing ?? null;
    // Warn, never block: a leg outside the PO's [issued, expiry] window is still
    // priced and still counted — it just says so.
    const poFlags: AccrualPoFlag[] =
      po && legs.some((l) => !isWithinPoWindow(po, l.serviceDate)) ? ["outsideWindow"] : [];
    return {
      key,
      legs,
      paired,
      poNumber,
      poFromContractDefault: legs[0].poNumber == null && poNumber !== null,
      poFlags,
      ...(realAmounts ? invoiceAmount(legs) : estimateAmount(legs, paired, poNumber)),
      invoiceNumber: realAmounts ? issuedRef(legs) : null,
      onWorksheetNumber: onWorksheet?.invoiceNumber ?? null,
    };
  }

  function buildGroups(bucket: AccrualBucketId, trips: TripRecord[]): AccrualGroup[] {
    const realAmounts = bucket === "paid" || bucket === "invoiced";
    return groupTrips(trips, realAmounts ? lineByTripId : null, effectivePo).map(({ key, legs }) =>
      finishGroup(key, legs, realAmounts),
    );
  }

  const buckets: AccrualBucket[] = ACCRUAL_BUCKET_ORDER.map((id) => {
    const groups = buildGroups(id, byBucket[id]);
    return {
      id,
      label: ACCRUAL_BUCKET_META[id].label,
      kind: ACCRUAL_BUCKET_META[id].kind,
      groups,
      actualCad: groups.reduce((s, g) => s + (g.amountSource === "invoice" ? g.amountCad ?? 0 : 0), 0),
      estimatedCad: groups.reduce((s, g) => s + (g.amountSource === "estimate" ? g.amountCad ?? 0 : 0), 0),
      unpricedCount: groups.filter((g) => g.amountCad === null).length,
    };
  });

  // Written-off groups carry their real (lost) amounts + reasons — shown in
  // reconciliation only, never in bucket totals.
  const writtenOff = groupTrips(writtenOffTrips, lineByTripId, effectivePo).map(({ key, legs }) => ({
    ...finishGroup(key, legs, true),
    // Reconciliation only — a written-off group is never "on a worksheet".
    onWorksheetNumber: null,
  }));

  // ---- purchase-order authorisation --------------------------------------
  // What each PO authorises against what this period's work draws down on it.
  // Invoiced dollars are real invoice-line money (invoiced + paid); upcoming is
  // the estimate for work not yet billed. Reconciliation (cancelled /
  // written-off) is excluded — it is not accruing against the PO.
  const commitmentRows = new Map<
    string,
    { poNumber: string | null; invoicedCad: number; upcomingCad: number }
  >();
  for (const b of buckets) {
    for (const g of b.groups) {
      const key = g.poNumber ?? "";
      const row = commitmentRows.get(key) ?? { poNumber: g.poNumber, invoicedCad: 0, upcomingCad: 0 };
      if (g.amountSource === "invoice") row.invoicedCad += g.amountCad ?? 0;
      else if (g.amountSource === "estimate") row.upcomingCad += g.amountCad ?? 0;
      commitmentRows.set(key, row);
    }
  }
  const poCommitments: AccrualPoCommitment[] = [...commitmentRows.values()]
    .sort((a, b) => {
      if (a.poNumber === b.poNumber) return 0;
      if (a.poNumber === null) return 1; // work with no PO at all sorts last
      if (b.poNumber === null) return -1;
      return a.poNumber.localeCompare(b.poNumber);
    })
    .map((row) => {
      const po = poRecord(row.poNumber);
      const amountCad = po?.amountCad ?? null;
      const committedCad = row.invoicedCad + row.upcomingCad;
      const over = amountCad !== null && committedCad > amountCad;
      const overageCad = over ? committedCad - amountCad! : null;
      const remainingCad = amountCad !== null && !over ? amountCad - committedCad : null;
      return {
        poNumber: row.poNumber,
        po,
        amountCad,
        invoicedCad: row.invoicedCad,
        upcomingCad: row.upcomingCad,
        committedCad,
        overageCad,
        remainingCad,
        termsLabel: poTermsLabel(po, contractRate),
        kind: over ? "over" : amountCad === null ? "off" : "ontime",
        // The invoice's words again ("PO value exceeded — review"), plus the
        // figure the invoice line has no room for.
        label:
          overageCad !== null
            ? `PO value exceeded — review · over by ${formatInvoiceCad(overageCad)}`
            : remainingCad !== null
              ? `${formatInvoiceCad(remainingCad)} left of ${formatInvoiceCad(amountCad!)}`
              : "No PO value recorded",
      } satisfies AccrualPoCommitment;
    });

  // Degradation banners — each explains a class of "—" or "unavailable" rows,
  // or a PO warning. Nothing here blocks anything: the report is the warning.
  const notes: string[] = [];
  // The owner's headline question first: is upcoming work about to exceed what a
  // purchase order actually authorises?
  for (const c of poCommitments) {
    if (c.overageCad === null) continue;
    notes.push(
      `Purchase order ${c.poNumber} is over its authorised value: ` +
        `${formatInvoiceCad(c.amountCad!)} authorised · ` +
        `${formatInvoiceCad(c.invoicedCad)} invoiced in this period · ` +
        `${formatInvoiceCad(c.upcomingCad)} upcoming · ` +
        `${formatInvoiceCad(c.overageCad)} over. Warning only — nothing is blocked.`,
    );
  }
  if (args.purchaseOrdersUnavailable) {
    notes.push(
      "Purchase orders could not be loaded — estimates fall back to the contract round-trip rate, so a PO carrying its own rates is not reflected and PO values are not checked.",
    );
  }
  if (!contract) {
    notes.push("No active contract on file — unbilled trips are listed without estimated amounts.");
  } else if (contract.billingModel === "Manual") {
    notes.push(
      "This client bills manually (no per-round-trip rate) — unbilled trips are listed without estimated amounts.",
    );
  } else if (contract.ratePerRoundTripCad === null) {
    notes.push(
      "The active contract has no round-trip rate recorded — unbilled trips are listed without estimated amounts.",
    );
  }
  // …but a PO can price work the contract cannot: say so rather than letting the
  // banner above read as "nothing on this report is estimated".
  if (
    contractRate === null &&
    poCommitments.some((c) => c.po?.roundTripRateCad != null || c.po?.oneWayRateCad != null)
  ) {
    notes.push(
      "Some purchase orders record their own rates — trips priced under those POs are still estimated, from the PO rather than the contract.",
    );
  }
  if (missingInvoiceIds.size > 0) {
    const n = missingInvoiceIds.size;
    notes.push(
      `${n} referenced invoice${n === 1 ? "" : "s"} could not be loaded — affected amounts show as unavailable and are excluded from totals.`,
    );
  }
  const allGroups = buckets.flatMap((b) => b.groups);
  const oneWayCount = allGroups.filter((g) => g.amountFlag === "oneWay").length;
  if (oneWayCount > 0) {
    notes.push(
      `${oneWayCount} group${oneWayCount === 1 ? "" : "s"} of trips ${oneWayCount === 1 ? "does" : "do"} not pair an outbound leg with an inbound one — each leg is estimated at its purchase order's one-way rate (half the effective round-trip rate where the PO records none) and flagged for review, exactly as the invoice would bill it until the return leg is scheduled.`,
    );
  }
  const splitPoCount = allGroups.filter((g) => g.amountFlag === "splitPo").length;
  if (splitPoCount > 0) {
    notes.push(
      `${splitPoCount} group${splitPoCount === 1 ? "" : "s"} of legs share a round trip with a leg on a DIFFERENT purchase order — a round trip must sit on one PO, so each side is estimated as its own one-way trip and flagged. Nothing is blocked; re-issue the legs under one PO to bill them as a round trip.`,
    );
  }
  const outsideWindow = allGroups.filter((g) => g.poFlags.includes("outsideWindow"));
  if (outsideWindow.length > 0) {
    const pos = [...new Set(outsideWindow.map((g) => g.poNumber).filter((p): p is string => p !== null))];
    notes.push(
      `${outsideWindow.length} group${outsideWindow.length === 1 ? "" : "s"} of trips fall outside the issued → expiry window of the purchase order that prices them (${pos.join(", ")}) — priced and counted anyway, flagged for review.`,
    );
  }
  const manualCount = buckets.reduce(
    (s, b) => s + b.groups.filter((g) => g.amountNote === "manual").length,
    0,
  );
  if (manualCount > 0) {
    notes.push(
      `${manualCount} trip${manualCount === 1 ? "" : "s"} ${manualCount === 1 ? "carries" : "carry"} no round-trip key (ad-hoc, charter or cargo work) — invoicing picks ${manualCount === 1 ? "it" : "them"} up as a manual line, so no amount is estimated here.`,
    );
  }

  const sections: AccrualSection[] = ACCRUAL_SECTION_ORDER.map((id) => {
    const meta = ACCRUAL_SECTION_META[id];
    // The SAME bucket objects report.buckets holds — a section is a view, so no
    // dollar can differ between the two readings of the month.
    const members = meta.buckets.map((bid) => buckets.find((b) => b.id === bid)!);
    return {
      id,
      label: meta.label,
      hint: meta.hint,
      kind: meta.kind,
      buckets: members,
      detail: meta.detail,
      groupCount: members.reduce((s, b) => s + b.groups.length, 0),
      actualCad: members.reduce((s, b) => s + b.actualCad, 0),
      estimatedCad: members.reduce((s, b) => s + b.estimatedCad, 0),
      unpricedCount: members.reduce((s, b) => s + b.unpricedCount, 0),
    };
  });

  return {
    client,
    period,
    today,
    buckets,
    sections,
    cancelled,
    writtenOff,
    invoices: [...invoices].sort((a, b) => a.invoiceNumber.localeCompare(b.invoiceNumber)),
    poCommitments,
    notes,
  };
}

// ---------------------------------------------------------------------------
// Per-PO draft preview — the SAME grouping and rate resolution as the estimator
// above, over the billable-trip rows the Billing generate-draft dialog already
// fetches. It lives here, not in the dialog, because draft generation now
// produces one worksheet PER PURCHASE ORDER: if the dialog re-derived "which POs
// and how much each", its preview could promise a different split from the one
// this file prices and the backend's InvoiceDraftBuilder creates.
// ---------------------------------------------------------------------------

/** The minimum a leg must carry to be grouped and priced. Both TripRecord and
 *  BillableTripRecord satisfy it structurally, so neither module has to convert. */
export interface PricedLeg {
  id: string;
  poNumber: string | null;
  roundTripKey: string | null;
  direction: "Outbound" | "Inbound" | null;
}

/** One prospective worksheet: the PO, the terms it prices at, and what it would
 *  draft. `poNumber: null` is the real "No PO" worksheet — work with no trip PO
 *  and no contract default still bills, and must read as such, never be hidden. */
export interface PoDraftPreview {
  poNumber: string | null;
  /** Money-bearing groups: a paired round trip, or a lone leg. */
  roundTrips: number;
  legCount: number;
  pairedCount: number;
  /** Groups billing per leg at the one-way rate — flagged for review. */
  oneWayCount: number;
  /** Legs with no roundTripKey: the draft builder leaves them to a manual line,
   *  so they are counted but never estimated (same rule as the estimator). */
  manualLegCount: number;
  estimatedCad: number;
  /** True when a group could not be priced at all (no rate on PO or contract). */
  unpriced: boolean;
  /** The PO's effective terms as one line (poTermsLabel). */
  termsLabel: string;
}

export function previewDraftsByPo(args: {
  legs: PricedLeg[];
  /** The client's active contract — the default PO and the fallback rate. */
  contract: ClientRecord["activeContract"];
  purchaseOrders: PurchaseOrderRecord[];
}): PoDraftPreview[] {
  const { legs, contract, purchaseOrders } = args;
  const contractRate = contractRoundTripRateCad(contract);
  const poByNumber = new Map(purchaseOrders.map((p) => [p.poNumber, p] as const));
  const effectivePo = (l: PricedLeg): string | null =>
    l.poNumber ?? contract?.defaultPoNumber ?? null;

  // Same key as the estimator: (effective PO, roundTripKey ?? own id).
  const groups = new Map<string, { poNumber: string | null; legs: PricedLeg[] }>();
  for (const l of legs) {
    const po = effectivePo(l);
    const key = `po:${po ?? ""}|${l.roundTripKey ?? l.id}`;
    const g = groups.get(key);
    if (g) g.legs.push(l);
    else groups.set(key, { poNumber: po, legs: [l] });
  }

  const byPo = new Map<string, PoDraftPreview>();
  for (const g of groups.values()) {
    const key = g.poNumber ?? "";
    const po = g.poNumber ? poByNumber.get(g.poNumber) ?? null : null;
    const row =
      byPo.get(key) ??
      ({
        poNumber: g.poNumber,
        roundTrips: 0,
        legCount: 0,
        pairedCount: 0,
        oneWayCount: 0,
        manualLegCount: 0,
        estimatedCad: 0,
        unpriced: false,
        termsLabel: poTermsLabel(po, contractRate),
      } satisfies PoDraftPreview);
    row.legCount += g.legs.length;
    // No roundTripKey anywhere → InvoiceDraftBuilder never claims it; it can
    // only reach an invoice as a hand-keyed manual line.
    if (!g.legs.some((l) => l.roundTripKey)) {
      row.manualLegCount += g.legs.length;
      byPo.set(key, row);
      continue;
    }
    const paired =
      g.legs.some((l) => l.direction === "Outbound") && g.legs.some((l) => l.direction === "Inbound");
    const terms = poEffectiveTerms(po, contractRate);
    const rate = paired ? terms.roundTripRateCad : terms.oneWayRateCad;
    row.roundTrips += 1;
    if (paired) row.pairedCount += 1;
    else row.oneWayCount += 1;
    if (rate === null) row.unpriced = true;
    else row.estimatedCad += paired ? rate : g.legs.length * rate;
    byPo.set(key, row);
  }

  return [...byPo.values()].sort((a, b) => {
    if (a.poNumber === b.poNumber) return 0;
    if (a.poNumber === null) return 1; // the No-PO worksheet sorts last
    if (b.poNumber === null) return -1;
    return a.poNumber.localeCompare(b.poNumber);
  });
}

// ---------------------------------------------------------------------------
// Shared row/total derivations — screen, print, and clipboard all read these.
// ---------------------------------------------------------------------------

export interface AccrualTotals {
  groupCount: number;
  actualCad: number;
  estimatedCad: number;
  unpricedCount: number;
}

/** Whole-report totals across the five buckets (reconciliation excluded). */
export function accrualTotals(report: AccrualsReport): AccrualTotals {
  return report.buckets.reduce<AccrualTotals>(
    (acc, b) => ({
      groupCount: acc.groupCount + b.groups.length,
      actualCad: acc.actualCad + b.actualCad,
      estimatedCad: acc.estimatedCad + b.estimatedCad,
      unpricedCount: acc.unpricedCount + b.unpricedCount,
    }),
    { groupCount: 0, actualCad: 0, estimatedCad: 0, unpricedCount: 0 },
  );
}

// ---------------------------------------------------------------------------
// Headline figures — the two numbers the report now leads with. Derived HERE so
// the screen's tiles, the printed sheet, the clipboard export and the emailed
// PDF all print the same strings; a consumer that formatted its own could drift
// by a rounding or an " est." marking, which is exactly the class of bug the
// one-derivation rule exists to prevent.
// ---------------------------------------------------------------------------

export interface AccrualHeadline {
  /** The section the figure summarises (upcoming / owed). */
  id: AccrualSectionId;
  label: string;
  /** Kind for the StatusChip/badge — colour never travels without this label. */
  kind: StatusKind;
  /** Pre-formatted money: "$1,350.00", "$1,350.00 est.", or "Not priced". */
  amountCad: string;
  /** "3 round trips · incl. $450.00 est. · 1 unpriced" */
  detail: string;
}

/** Section tally as one line — "3 round trips · incl. $450.00 est. · 1 unpriced".
 *  Actual and estimated never merge silently: a combined figure always says how
 *  much of it is an estimate. */
export function accrualSectionDetail(section: AccrualSection): string {
  const parts = [`${section.groupCount} round trip${section.groupCount === 1 ? "" : "s"}`];
  // Only when the figure itself is NOT already marked wholly estimated —
  // otherwise the subline would repeat what the amount already says.
  if (section.estimatedCad > 0 && section.actualCad > 0) {
    parts.push(`incl. ${formatInvoiceCad(section.estimatedCad)} est.`);
  }
  if (section.unpricedCount > 0) parts.push(`${section.unpricedCount} unpriced`);
  return parts.join(" · ");
}

/** Section headline money: wholly-estimated sections carry " est."; a section
 *  with nothing priced at all reads "Not priced" rather than a false $0.00
 *  (manual billing / no contract / no rate — the banner notes say which). */
export function accrualSectionAmountLabel(section: AccrualSection): string {
  const total = section.actualCad + section.estimatedCad;
  if (total === 0 && section.unpricedCount > 0) return "Not priced";
  return section.actualCad === 0 && section.estimatedCad > 0
    ? `${formatInvoiceCad(total)} est.`
    : formatInvoiceCad(total);
}

/** Wording for each headline — deliberately the owner's two questions, not the
 *  section labels: "what is this going to cost me" and "what do I still owe". */
const HEADLINE_LABELS: Record<"upcoming" | "owed", string> = {
  upcoming: "Upcoming expenses",
  owed: "Monies owed",
};

/** The two leading figures, upcoming first. `settled` is not a headline — it is
 *  the closing line. */
export function accrualHeadlines(report: AccrualsReport): AccrualHeadline[] {
  return report.sections
    .filter((s): s is AccrualSection & { id: "upcoming" | "owed" } => s.id !== "settled")
    .map((s) => ({
      id: s.id,
      label: HEADLINE_LABELS[s.id],
      kind: s.kind,
      amountCad: accrualSectionAmountLabel(s),
      detail: accrualSectionDetail(s),
    }));
}

/** The settled closing line — "Settled this month · 4 round trips · $1,800.00".
 *  One line, by design: no per-trip detail table (see ACCRUAL_SECTION_META). */
export function accrualSettledLine(report: AccrualsReport): string {
  const settled = report.sections.find((s) => s.id === "settled");
  if (!settled) return "";
  return `${settled.label} · ${accrualSectionDetail(settled)} · ${accrualSectionAmountLabel(settled)}`;
}

/** "$450.00" / "$450.00 est." — null when unpriced (render amountNote or "—"). */
export function groupAmountLabel(g: AccrualGroup): string | null {
  if (g.amountCad === null) return null;
  return g.amountSource === "estimate"
    ? `${formatInvoiceCad(g.amountCad)} est.`
    : formatInvoiceCad(g.amountCad);
}

/** Ref column: issued invoice #, "On worksheet …", or "—". */
export function groupRefLabel(g: AccrualGroup): string {
  if (g.invoiceNumber) return g.invoiceNumber;
  if (g.onWorksheetNumber) return `On worksheet ${g.onWorksheetNumber}`;
  return "—";
}

/** PO column: the EFFECTIVE PO that priced the group — "PO-9", or
 *  "PO-9 (contract default)" when the trips carry none of their own, or "No PO".
 *  Per-PO terms make the PO a pricing fact, so every consumer prints which PO
 *  priced the row, not merely the string stamped on the first leg. */
export function groupPoLabel(g: AccrualGroup): string {
  if (g.poNumber === null) return "No PO";
  return g.poFromContractDefault ? `${g.poNumber} (contract default)` : g.poNumber;
}

/** PO cell for the colourless outputs (clipboard, print, emailed PDF): the
 *  label plus any PO warning spelled out in words. */
export function groupPoText(g: AccrualGroup): string {
  const flags = g.poFlags.map((f) => PO_FLAG_META[f].label);
  return flags.length > 0 ? `${groupPoLabel(g)} (${flags.join("; ")})` : groupPoLabel(g);
}

/** One PO's draw-down as a line — "$96,000.00 authorised · $80,000.00 invoiced
 *  in this period · $20,000.00 upcoming". The chip label beside it carries the
 *  remaining/overage figure (commitment.label), so colour never stands alone. */
export function poCommitmentDetail(c: AccrualPoCommitment): string {
  return [
    c.amountCad === null ? "No authorised value recorded" : `${formatInvoiceCad(c.amountCad)} authorised`,
    `${formatInvoiceCad(c.invoicedCad)} invoiced in this period`,
    `${formatInvoiceCad(c.upcomingCad)} upcoming`,
  ].join(" · ");
}

/** Route line for a group — the first leg's corridor (a pair mirrors it). */
export function groupRouteLabel(g: AccrualGroup): string {
  return corridorLabel(g.legs[0]);
}

/** "NL-1042 outbound + NL-1043 inbound" — plain-text trips cell (clipboard). */
function groupTripsText(g: AccrualGroup): string {
  return g.legs
    .map((l) => {
      const dir = l.direction ? ` ${l.direction.toLowerCase()}` : "";
      const dead = l.isEmptyLeg ? " (deadhead)" : "";
      return `${l.tripNumber}${dir}${dead}`;
    })
    .join(" + ");
}

// ---------------------------------------------------------------------------
// Clipboard export — header lines, then tab-delimited rows per bucket (pastes
// straight into a spreadsheet), then reconciliation and the invoices
// referenced. Same shape idea as invoiceClipboardText.
// ---------------------------------------------------------------------------

/** Plain-text amount for the clipboard / the emailed PDF: the figure plus any
 *  half-rate caveat in words (those outputs have no colour to lean on), or the
 *  spelled-out reason there is no figure. */
function plainAmount(g: AccrualGroup): string {
  const label = groupAmountLabel(g);
  if (label !== null) {
    return g.amountFlag ? `${label} (${AMOUNT_FLAG_META[g.amountFlag].label})` : label;
  }
  return g.amountNote ? AMOUNT_NOTE_META[g.amountNote].label : "—";
}

export function accrualsClipboardText(report: AccrualsReport): string {
  const out: string[] = [];
  out.push(`Accruals report — ${report.client.name}`);
  out.push(`Period: ${periodLabel(report.period)}`);
  out.push(`Prepared: ${report.today}`);
  const contract = report.client.activeContract;
  out.push(`Contract: ${contract ? contractRateLabel(contract) : "No active contract"}`);
  for (const note of report.notes) out.push(`Note: ${note}`);

  // The two questions the report answers, first — then the sections in reading
  // order, upcoming work ahead of monies owed.
  out.push("");
  for (const h of accrualHeadlines(report)) {
    out.push([h.label.toUpperCase(), h.amountCad, h.detail].join("\t"));
  }

  // What each PO authorises against what this month draws down on it — the
  // reason the upcoming figure above is worth having.
  out.push("");
  out.push("PURCHASE ORDERS — AUTHORISED VALUE VS THIS MONTH'S WORK");
  if (report.poCommitments.length === 0) {
    out.push("No purchase-order work in this period.");
  } else {
    out.push(["PO", "Terms", "Authorised", "Invoiced", "Upcoming", "Status"].join("\t"));
    for (const c of report.poCommitments) {
      out.push(
        [
          c.poNumber ?? "No PO",
          c.termsLabel,
          c.amountCad === null ? "—" : formatInvoiceCad(c.amountCad),
          formatInvoiceCad(c.invoicedCad),
          formatInvoiceCad(c.upcomingCad),
          c.label,
        ].join("\t"),
      );
    }
  }

  for (const section of report.sections) {
    out.push("");
    out.push(
      `${section.label.toUpperCase()} — ${accrualSectionDetail(section)} · ${accrualSectionAmountLabel(section)}`,
    );
    // The settled section closes the month on that one line: no per-trip table.
    if (!section.detail) continue;
    for (const b of section.buckets) {
      const tally = [
        `${b.groups.length} round trip${b.groups.length === 1 ? "" : "s"}`,
        `actual ${formatInvoiceCad(b.actualCad)}`,
        `estimated ${formatInvoiceCad(b.estimatedCad)}`,
      ];
      if (b.unpricedCount > 0) tally.push(`${b.unpricedCount} unpriced`);
      out.push(`${b.label} — ${tally.join(" · ")}`);
      if (b.groups.length === 0) continue;
      out.push(["Date", "Trips", "Route", "PO", "Ref", "Amount (CAD)"].join("\t"));
      for (const g of b.groups) {
        out.push(
          [
            g.legs[0].serviceDate,
            groupTripsText(g),
            groupRouteLabel(g),
            groupPoText(g),
            groupRefLabel(g),
            plainAmount(g),
          ].join("\t"),
        );
      }
    }
  }

  if (report.cancelled.length > 0 || report.writtenOff.length > 0) {
    out.push("");
    out.push("RECONCILIATION — NOT COUNTED IN ACCRUALS");
    for (const t of report.cancelled) {
      out.push(
        [t.serviceDate, t.tripNumber, corridorLabel(t), "Cancelled", t.cancelledReason ?? "no reason recorded"].join("\t"),
      );
    }
    for (const g of report.writtenOff) {
      const reason = g.legs.map((l) => l.writtenOffReason).find(Boolean) ?? "no reason recorded";
      out.push(
        [g.legs[0].serviceDate, groupTripsText(g), groupRouteLabel(g), `Written off ${plainAmount(g)}`, reason].join("\t"),
      );
    }
  }

  out.push("");
  out.push("INVOICES REFERENCED");
  if (report.invoices.length === 0) {
    out.push("No issued invoices are referenced by this period's trips.");
  } else {
    out.push(["Invoice", "QBO #", "Status", "Invoice period", "Total (CAD)"].join("\t"));
    for (const inv of report.invoices) {
      out.push(
        [
          inv.invoiceNumber,
          inv.qboInvoiceId ?? "—",
          invoiceChip(inv).label,
          invoicePeriodLabel(inv),
          formatInvoiceCad(inv.totalCad),
        ].join("\t"),
      );
    }
  }

  out.push("");
  out.push(ACCRUALS_TAX_NOTE);
  out.push(ACCRUALS_ESTIMATE_NOTE);
  return out.join("\n");
}

// ---------------------------------------------------------------------------
// Email payload — the wire report for POST /api/notifications/emails/
// client-accruals (and its preview). Pre-formatted strings only: the backend's
// QuestPDF renderer prints them verbatim, doing zero domain lookups
// (Notifications holds no trips/billing/clients data). Every cell reuses the
// SAME label/format helpers as the screen, the printed sheet, and the
// clipboard — one derivation, so the emailed PDF can never disagree with them
// about an amount or an " est." marking.
// ---------------------------------------------------------------------------

/** "3" / "3 (1 unpriced)" — the wire summary row has no unpriced column. */
function roundTripsCell(groupCount: number, unpricedCount: number): string {
  return unpricedCount > 0 ? `${groupCount} (${unpricedCount} unpriced)` : String(groupCount);
}

export function accrualsEmailPayload(report: AccrualsReport): AccrualsEmailReport {
  return {
    clientName: report.client.name,
    periodLabel: periodLabel(report.period),
    preparedDate: report.today,
    // Degradation banners only — the backend PDF bakes its own tax/estimate
    // disclaimer banner, so sending ours here would print it twice.
    notes: report.notes,
    // The two leading figures — upcoming expenses first, then monies owed. The
    // backend renders them above the summary; a client that sends none still
    // renders (the block is simply skipped), which is what makes this additive.
    headline: accrualHeadlines(report).map((h) => ({
      label: h.label,
      amountCad: h.amountCad,
      detail: h.detail,
    })),
    // Summary in SECTION order: a subtotal row per section (emphasis true) with
    // its buckets nested under it (emphasis false). Still all five buckets and
    // every zero, so the summary remains the complete position of the month.
    // Unpriced counts ride the round-trips cell (the wire row has no column).
    summary: report.sections.flatMap((section) => [
      {
        bucketLabel: section.label,
        roundTrips: roundTripsCell(section.groupCount, section.unpricedCount),
        actualCad: formatInvoiceCad(section.actualCad),
        estimatedCad: section.estimatedCad > 0 ? `${formatInvoiceCad(section.estimatedCad)} est.` : "—",
        emphasis: true,
      },
      ...section.buckets.map((b) => ({
        bucketLabel: b.label,
        roundTrips: roundTripsCell(b.groups.length, b.unpricedCount),
        actualCad: formatInvoiceCad(b.actualCad),
        estimatedCad: b.estimatedCad > 0 ? `${formatInvoiceCad(b.estimatedCad)} est.` : "—",
        emphasis: false,
      })),
    ]),
    // Detail tables for the non-empty buckets of the detail-bearing sections
    // only, in section order — the settled section is one summary row above and
    // carries no per-trip table at all, and the summary already has every zero.
    buckets: report.sections
      .filter((s) => s.detail)
      .flatMap((s) => s.buckets)
      .filter((b) => b.groups.length > 0)
      .map((b) => ({
        label: b.label,
        rows: b.groups.map((g) => ({
          date: g.legs[0].serviceDate,
          tripNumbers: groupTripsText(g),
          route: groupRouteLabel(g),
          poNumber: groupPoText(g),
          reference: groupRefLabel(g),
          amountCad: plainAmount(g),
        })),
      })),
    reconciliation: [
      ...report.cancelled.map((t) => ({
        date: t.serviceDate,
        tripNumbers: t.tripNumber,
        route: corridorLabel(t),
        status: "Cancelled",
        reason: t.cancelledReason ?? "no reason recorded",
        amountCad: "—",
      })),
      ...report.writtenOff.map((g) => ({
        date: g.legs[0].serviceDate,
        tripNumbers: groupTripsText(g),
        route: groupRouteLabel(g),
        status: "Written off",
        reason: g.legs.map((l) => l.writtenOffReason).find(Boolean) ?? "no reason recorded",
        amountCad: plainAmount(g),
      })),
    ],
    invoices: report.invoices.map((inv) => ({
      invoiceNumber: inv.invoiceNumber,
      qboInvoiceId: inv.qboInvoiceId ?? "—",
      status: invoiceChip(inv).label,
      periodLabel: invoicePeriodLabel(inv),
      totalCad: formatInvoiceCad(inv.totalCad),
    })),
  };
}
