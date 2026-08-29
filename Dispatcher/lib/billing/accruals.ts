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
//   - Estimates (ready / scheduled / upcoming) are 1 × the contract's
//     round-trip rate for a PAIRED group only, marked " est." everywhere. An
//     unpaired leg is never estimated — the backend prices complete round trips
//     only, so a half-rate would print a number no invoice will ever contain.
//     Manual billing / no contract / no rate → no estimates, one banner note.
//   - GST is out of scope for the report body (estimates cannot compute it and
//     line amounts are pre-GST); real GST appears only in the Invoices
//     Referenced section, straight off each fetched invoice.

import {
  formatInvoiceCad,
  invoiceChip,
  periodLabel as invoicePeriodLabel,
  type InvoiceDetailRecord,
  type InvoiceLineRecord,
} from "@/lib/api/billing";
import type { AccrualsEmailReport } from "@/lib/api/notifications";
import { contractRateLabel, type ClientRecord } from "@/lib/api/clients";
import { corridorLabel, sortTrips, type TripRecord } from "@/lib/api/trips";
import { periodLabel, type Period } from "@/lib/period";
import type { StatusKind } from "@/lib/theme";

// ---------------------------------------------------------------------------
// Buckets
// ---------------------------------------------------------------------------

export type AccrualBucketId = "paid" | "invoiced" | "ready" | "scheduled" | "upcoming";

/** Fixed rendering order — settled money first, furthest-out accrual last. */
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
  ready: { label: "Ready for billing", kind: "soon", hint: "Run complete, not yet invoiced — amounts are contract-rate estimates" },
  scheduled: { label: "Scheduled", kind: "info", hint: "Run due or under way — amounts are contract-rate estimates" },
  upcoming: { label: "Upcoming", kind: "off", hint: "Scheduled after today — amounts are contract-rate estimates" },
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
export type AccrualAmountNote = "unavailable" | "unpaired" | "counted";

export const AMOUNT_NOTE_META: Record<AccrualAmountNote, { kind: StatusKind; label: string }> = {
  unavailable: { kind: "over", label: "Amount unavailable" },
  unpaired: { kind: "soon", label: "Unpaired — not estimated" },
  // Only reachable when one invoice line's legs land in different buckets: the
  // first bucket carries the money, this row says so rather than repeating it.
  counted: { kind: "info", label: "Counted with the paired leg" },
};

/** One money-bearing row: a round-trip pair, or a lone leg. */
export interface AccrualGroup {
  /** `line:<lineId>` for billed groups (the invoice line is the unit);
   *  roundTripKey, or the lone trip's own id, for unbilled ones. */
  key: string;
  /** Legs in service order — two for a matched pair, one otherwise. */
  legs: TripRecord[];
  /** Both legs of the round trip landed in this bucket. */
  paired: boolean;
  /** Dollars attributed to this group; null = unpriced (see amountNote). */
  amountCad: number | null;
  amountSource: AccrualAmountSource | null;
  amountNote: AccrualAmountNote | null;
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

export interface AccrualsReport {
  client: ClientRecord;
  period: Period;
  /** The local operational day the scheduled/upcoming split was made against. */
  today: string;
  /** All five buckets in ACCRUAL_BUCKET_ORDER — empty buckets included. */
  buckets: AccrualBucket[];
  /** Cancelled trips with reasons — reconciliation, never counted as accruals. */
  cancelled: TripRecord[];
  /** Written-off groups with reasons + lost amounts — reconciliation. */
  writtenOff: AccrualGroup[];
  /** The fetched invoices behind the real amounts — the only place GST shows. */
  invoices: InvoiceDetailRecord[];
  /** Degradation banners: manual billing, missing rate, failed fetches, unpaired legs. */
  notes: string[];
}

/** Footer wording shared by the screen, the printed sheet, and the clipboard. */
export const ACCRUALS_GST_NOTE =
  "All amounts CAD, excluding GST. GST applies on issued invoices only — see Invoices referenced.";
export const ACCRUALS_ESTIMATE_NOTE =
  "Amounts marked “est.” are contract-rate estimates, not invoices — issued invoice amounts govern.";

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
 * (and every unbilled trip) fall back to roundTripKey, then to their own id.
 */
function groupTrips(
  sorted: TripRecord[],
  lineByTripId: Map<string, InvoiceLineRecord> | null,
): { key: string; legs: TripRecord[] }[] {
  const byKey = new Map<string, TripRecord[]>();
  for (const t of sorted) {
    const line = lineByTripId?.get(t.id);
    const key = line ? `line:${line.lineId}` : (t.roundTripKey ?? t.id);
    const legs = byKey.get(key);
    if (legs) legs.push(t);
    else byKey.set(key, [t]);
  }
  return [...byKey.entries()].map(([key, legs]) => ({ key, legs }));
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
}): AccrualsReport {
  const { client, period, today, invoices } = args;

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

  // Estimation is possible only under a per-round-trip contract with a rate.
  const contract = client.activeContract;
  const rate = contract?.billingModel === "RoundTripRate" ? contract.ratePerRoundTripCad : null;

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
    "amountCad" | "amountSource" | "amountNote"
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
        ? { amountCad: null, amountSource: null, amountNote: "counted" }
        : { amountCad: null, amountSource: null, amountNote: "unavailable" };
    }
    let sum = 0;
    for (const [lineId, line] of lines) {
      claimedLines.add(lineId);
      sum += line.amountCad;
    }
    return { amountCad: sum, amountSource: "invoice", amountNote: null };
  }

  // Estimate a PAIRED group at 1 × rate; never price an unpaired leg. With no
  // usable rate nothing is estimated and one banner note explains every "—".
  function estimateAmount(paired: boolean): Pick<
    AccrualGroup,
    "amountCad" | "amountSource" | "amountNote"
  > {
    if (rate === null) return { amountCad: null, amountSource: null, amountNote: null };
    if (!paired) return { amountCad: null, amountSource: null, amountNote: "unpaired" };
    return { amountCad: rate, amountSource: "estimate", amountNote: null };
  }

  function buildGroups(bucket: AccrualBucketId, trips: TripRecord[]): AccrualGroup[] {
    const realAmounts = bucket === "paid" || bucket === "invoiced";
    return groupTrips(trips, realAmounts ? lineByTripId : null).map(({ key, legs }) => {
      const paired = legs.length >= 2;
      const onWorksheet = legs.find((l) => l.billing?.state === "OnWorksheet")?.billing ?? null;
      return {
        key,
        legs,
        paired,
        ...(realAmounts ? invoiceAmount(legs) : estimateAmount(paired)),
        invoiceNumber: realAmounts ? issuedRef(legs) : null,
        onWorksheetNumber: onWorksheet?.invoiceNumber ?? null,
      };
    });
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
  const writtenOff = groupTrips(writtenOffTrips, lineByTripId).map(({ key, legs }) => ({
    key,
    legs,
    paired: legs.length >= 2,
    ...invoiceAmount(legs),
    invoiceNumber: issuedRef(legs),
    onWorksheetNumber: null,
  }));

  // Degradation banners — each explains a class of "—" or "unavailable" rows.
  const notes: string[] = [];
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
  if (missingInvoiceIds.size > 0) {
    const n = missingInvoiceIds.size;
    notes.push(
      `${n} referenced invoice${n === 1 ? "" : "s"} could not be loaded — affected amounts show as unavailable and are excluded from totals.`,
    );
  }
  const unpairedCount = buckets.reduce(
    (s, b) => s + b.groups.filter((g) => g.amountNote === "unpaired").length,
    0,
  );
  if (unpairedCount > 0) {
    notes.push(
      `${unpairedCount} unpaired leg${unpairedCount === 1 ? "" : "s"} not estimated — the contract prices complete round trips only, so a half-trip figure would never match an invoice.`,
    );
  }

  return {
    client,
    period,
    today,
    buckets,
    cancelled,
    writtenOff,
    invoices: [...invoices].sort((a, b) => a.invoiceNumber.localeCompare(b.invoiceNumber)),
    notes,
  };
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

function plainAmount(g: AccrualGroup): string {
  return groupAmountLabel(g) ?? (g.amountNote ? AMOUNT_NOTE_META[g.amountNote].label : "—");
}

export function accrualsClipboardText(report: AccrualsReport): string {
  const out: string[] = [];
  out.push(`Accruals report — ${report.client.name}`);
  out.push(`Period: ${periodLabel(report.period)}`);
  out.push(`Prepared: ${report.today}`);
  const contract = report.client.activeContract;
  out.push(`Contract: ${contract ? contractRateLabel(contract) : "No active contract"}`);
  for (const note of report.notes) out.push(`Note: ${note}`);

  for (const b of report.buckets) {
    out.push("");
    const tally = [
      `${b.groups.length} round trip${b.groups.length === 1 ? "" : "s"}`,
      `actual ${formatInvoiceCad(b.actualCad)}`,
      `estimated ${formatInvoiceCad(b.estimatedCad)}`,
    ];
    if (b.unpricedCount > 0) tally.push(`${b.unpricedCount} unpriced`);
    out.push(`${b.label.toUpperCase()} — ${tally.join(" · ")}`);
    if (b.groups.length === 0) continue;
    out.push(["Date", "Trips", "Route", "PO", "Ref", "Amount (CAD)"].join("\t"));
    for (const g of b.groups) {
      out.push(
        [
          g.legs[0].serviceDate,
          groupTripsText(g),
          groupRouteLabel(g),
          g.legs[0].poNumber ?? "—",
          groupRefLabel(g),
          plainAmount(g),
        ].join("\t"),
      );
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
    out.push(["Invoice", "QBO #", "Status", "Invoice period", "Subtotal", "GST", "Total (CAD)"].join("\t"));
    for (const inv of report.invoices) {
      out.push(
        [
          inv.invoiceNumber,
          inv.qboInvoiceId ?? "—",
          invoiceChip(inv).label,
          invoicePeriodLabel(inv),
          formatInvoiceCad(inv.subtotalCad),
          formatInvoiceCad(inv.gstCad),
          formatInvoiceCad(inv.totalCad),
        ].join("\t"),
      );
    }
  }

  out.push("");
  out.push(ACCRUALS_GST_NOTE);
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

export function accrualsEmailPayload(report: AccrualsReport): AccrualsEmailReport {
  return {
    clientName: report.client.name,
    periodLabel: periodLabel(report.period),
    preparedDate: report.today,
    // Degradation banners only — the backend PDF bakes its own GST/estimate
    // disclaimer banner, so sending ours here would print it twice.
    notes: report.notes,
    // All five buckets, zeros included — the summary is the complete position.
    // Unpriced counts ride the round-trips cell (the wire row has no column).
    summary: report.buckets.map((b) => ({
      bucketLabel: b.label,
      roundTrips:
        b.unpricedCount > 0
          ? `${b.groups.length} (${b.unpricedCount} unpriced)`
          : String(b.groups.length),
      actualCad: formatInvoiceCad(b.actualCad),
      estimatedCad: b.estimatedCad > 0 ? `${formatInvoiceCad(b.estimatedCad)} est.` : "—",
    })),
    // Detail sections for the non-empty buckets only, like the printed sheet —
    // the summary above already carries every zero.
    buckets: report.buckets
      .filter((b) => b.groups.length > 0)
      .map((b) => ({
        label: b.label,
        rows: b.groups.map((g) => ({
          date: g.legs[0].serviceDate,
          tripNumbers: groupTripsText(g),
          route: groupRouteLabel(g),
          poNumber: g.legs[0].poNumber ?? "—",
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
      invoiceNumber: inv.qboInvoiceId
        ? `${inv.invoiceNumber} · QBO ${inv.qboInvoiceId}`
        : inv.invoiceNumber,
      status: invoiceChip(inv).label,
      subtotalCad: formatInvoiceCad(inv.subtotalCad),
      gstCad: formatInvoiceCad(inv.gstCad),
      totalCad: formatInvoiceCad(inv.totalCad),
    })),
  };
}
