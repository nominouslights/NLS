import { describe, expect, it } from "vitest";
import type { ClientRecord, PurchaseOrderRecord } from "@/lib/api/clients";
import type { InvoiceDetailRecord, InvoiceLineRecord } from "@/lib/api/billing";
import type { TripDirection, TripRecord } from "@/lib/api/trips";
import type { Period } from "@/lib/period";
import {
  ACCRUAL_BUCKET_ORDER,
  ACCRUAL_SECTION_ORDER,
  AMOUNT_FLAG_META,
  PO_FLAG_META,
  accrualHeadlines,
  accrualTotals,
  accrualsClipboardText,
  accrualsEmailPayload,
  buildAccrualsReport,
  groupPoLabel,
  previewDraftsByPo,
  type AccrualBucketId,
  type AccrualsReport,
} from "./accruals";

// The monthly accruals derivation — the ONE place bucket membership, round-trip
// pairing and every dollar on this report are decided, feeding the screen, the
// printed NL-ACC-01 sheet, the clipboard export and the emailed PDF. Two reasons
// this suite matters more than its size suggests:
//
//  1. The estimate rule must mirror the invoice generator
//     (Backend/src/Billing/Domain/Invoices/InvoiceDraftBuilder.cs:88-135) leg for
//     leg. It once did not: `paired` was tested as `legs.length >= 2`, so a
//     3-leg group was priced at 1 × rate instead of 1.5 ×, and a lone leg went
//     unpriced although the invoice will bill it at half. Those are the cases
//     pinned hardest here, on both sides of the pairing test.
//  2. `claimedLines` guarantees a given invoice line is counted at most once per
//     report, and it does that by walking buckets in ACCRUAL_BUCKET_ORDER. That
//     order is a money rule, not a display choice — the sections added on top
//     are a separate, purely presentational view — so both are pinned.
//  3. Rates are now PER PURCHASE ORDER (the contract rate is only the fallback),
//     and grouping is by (effective PO, roundTripKey). That makes the PO a money
//     input, so both halves of the resolution are pinned on both sides — PO
//     override vs contract fallback, explicit one-way rate vs the ½ fallback —
//     plus a no-regression case proving a client whose POs carry no terms at all
//     still prices exactly as it did before any of this existed.

const RATE = 500;
const TODAY = "2026-09-15";
const PERIOD: Period = { granularity: "month", start: "2026-09-01", end: "2026-09-30" };

function client(overrides: Partial<ClientRecord> = {}): ClientRecord {
  return {
    id: "c1",
    name: "Alamos Gold",
    type: "Client",
    serviceType: "ContractCrew",
    tag: "ALA",
    agreementReference: null,
    notes: null,
    createdAtUtc: "2026-01-01T00:00:00Z",
    updatedAtUtc: "2026-01-01T00:00:00Z",
    activeContract: {
      activeContractId: "k1",
      startDate: "2026-01-01",
      endDate: null,
      billingModel: "RoundTripRate",
      ratePerRoundTripCad: RATE,
      budgetCode: "OPS-100",
      billingFrequency: "Monthly",
      netTermsDays: 30,
      defaultPoNumber: "PO-9",
    },
    ...overrides,
  };
}

let tripSeq = 0;

function trip(overrides: Partial<TripRecord> = {}): TripRecord {
  tripSeq += 1;
  return {
    id: `t${tripSeq}`,
    tripNumber: `NL-${1000 + tripSeq}`,
    serviceDate: "2026-09-10",
    windowStart: "06:30:00",
    windowEnd: "10:30:00",
    serviceType: "ContractCrew",
    routeId: "r1",
    routeName: "Thompson ⇄ Site",
    origin: "Thompson",
    destination: "Site",
    stops: [],
    distanceKm: 120,
    scheduleTemplateId: null,
    roundTripKey: "rt-1",
    direction: "Outbound",
    isEmptyLeg: false,
    clientId: "c1",
    clientName: "Alamos Gold",
    poNumber: "PO-9",
    driverId: null,
    driverName: null,
    vehicleId: null,
    vehicleUnit: null,
    seatsCapacity: 12,
    seatsConfirmed: 8,
    seatsMinimum: null,
    demandGuaranteed: false,
    status: "Scheduled",
    manifestId: null,
    hasPostTripInspection: false,
    operationsFinishedAtUtc: null,
    completedAtUtc: null,
    cancelledReason: null,
    writtenOffReason: null,
    createdAtUtc: "2026-09-01T00:00:00Z",
    updatedAtUtc: "2026-09-01T00:00:00Z",
    billing: null,
    ...overrides,
  };
}

/** A leg in the "ready for billing" bucket — unbilled, so it gets an estimate. */
function readyLeg(direction: TripDirection | null, overrides: Partial<TripRecord> = {}): TripRecord {
  return trip({ status: "ReadyForBilling", direction, ...overrides });
}

/** A purchase order with NO terms of its own — the "PO exists but prices
 *  nothing differently" baseline every override test starts from. */
function po(overrides: Partial<PurchaseOrderRecord> = {}): PurchaseOrderRecord {
  return {
    id: "po-1",
    clientId: "c1",
    poNumber: "PO-9",
    issued: "2026-01-01",
    expiry: null,
    amountCad: null,
    roundTripRateCad: null,
    oneWayRateCad: null,
    note: null,
    ...overrides,
  };
}

function build(
  trips: TripRecord[],
  invoices: InvoiceDetailRecord[] = [],
  c = client(),
  purchaseOrders: PurchaseOrderRecord[] = [],
): AccrualsReport {
  return buildAccrualsReport({
    client: c,
    period: PERIOD,
    today: TODAY,
    trips,
    invoices,
    purchaseOrders,
  });
}

function bucket(report: AccrualsReport, id: AccrualBucketId) {
  const b = report.buckets.find((x) => x.id === id);
  if (!b) throw new Error(`no ${id} bucket`);
  return b;
}

function section(report: AccrualsReport, id: "upcoming" | "owed" | "settled") {
  const s = report.sections.find((x) => x.id === id);
  if (!s) throw new Error(`no ${id} section`);
  return s;
}

function invoice(lines: InvoiceLineRecord[], overrides: Partial<InvoiceDetailRecord> = {}): InvoiceDetailRecord {
  const total = lines.reduce((s, l) => s + l.amountCad, 0);
  return {
    id: "inv-1",
    invoiceNumber: "NL-INV-0001",
    clientId: "c1",
    clientName: "Alamos Gold",
    contractId: "k1",
    poNumber: "PO-9",
    budgetCode: "OPS-100",
    netTermsDays: 30,
    periodStart: "2026-09-01",
    periodEnd: "2026-09-30",
    status: "EnteredInQbo",
    issuedAtUtc: "2026-09-12T00:00:00Z",
    totalCad: total,
    qboInvoiceId: "QBO-77",
    qboEnteredDate: "2026-09-12",
    paymentConfirmedDate: null,
    writtenOffAmountCad: null,
    writtenOffDate: null,
    writtenOffReason: null,
    outstandingCad: total,
    lines,
    ...overrides,
  };
}

function line(overrides: Partial<InvoiceLineRecord> = {}): InvoiceLineRecord {
  return {
    lineId: "L1",
    description: "Corridor round trip",
    tripIds: [],
    tripNumber: null,
    serviceDate: "2026-09-10",
    quantity: 1,
    unitPriceCad: RATE,
    amountCad: RATE,
    ...overrides,
  };
}

function billed(state: "Invoiced" | "Paid", invoiceId = "inv-1") {
  return {
    state,
    invoiceId,
    invoiceNumber: "NL-INV-0001",
    qboInvoiceId: "QBO-77",
    qboEnteredDate: "2026-09-12",
    paymentConfirmedDate: state === "Paid" ? "2026-09-14" : null,
  };
}

// ---------------------------------------------------------------------------
// The estimate rule — mirrors InvoiceDraftBuilder exactly
// ---------------------------------------------------------------------------

describe("estimates mirror InvoiceDraftBuilder", () => {
  it("prices an Outbound + Inbound pair at 1 × the round-trip rate", () => {
    const report = build([readyLeg("Outbound"), readyLeg("Inbound")]);
    const groups = bucket(report, "ready").groups;
    expect(groups).toHaveLength(1);
    expect(groups[0].paired).toBe(true);
    expect(groups[0].amountCad).toBe(RATE);
    expect(groups[0].amountSource).toBe("estimate");
    expect(groups[0].amountFlag).toBeNull();
    expect(groups[0].amountNote).toBeNull();
  });

  it("prices a lone leg at the one-way rate (½ the round trip) and flags it", () => {
    // The old rule left this unpriced — the common case for upcoming work, and
    // the reason the new headline figure would have understated by half a trip.
    const report = build([readyLeg("Outbound")]);
    const g = bucket(report, "ready").groups[0];
    expect(g.paired).toBe(false);
    expect(g.amountCad).toBe(RATE * 0.5);
    expect(g.amountSource).toBe("estimate");
    expect(g.amountFlag).toBe("oneWay");
    // A one-way-priced group HAS an amount, so it is not unpriced and must not
    // be counted as such — that separation is why amountFlag exists.
    expect(g.amountNote).toBeNull();
    expect(bucket(report, "ready").unpricedCount).toBe(0);
  });

  it("prices a 3-leg group that HAS both directions at 1 × the rate", () => {
    // InvoiceDraftBuilder's pairing test is direction PRESENCE, not leg count:
    // Outbound + Inbound + a third leg still bills as one round-trip line at
    // qty 1. Pinned explicitly because the plan's shorthand ("a 3-leg group is
    // mis-estimated at 1 × rate") reads as if leg count decided it — the leg
    // count only matters once the direction test has failed (next case).
    const report = build([readyLeg("Outbound"), readyLeg("Inbound"), readyLeg("Outbound")]);
    const g = bucket(report, "ready").groups[0];
    expect(g.legs).toHaveLength(3);
    expect(g.paired).toBe(true);
    expect(g.amountCad).toBe(RATE);
    expect(g.amountFlag).toBeNull();
  });

  it("prices a 3-leg SAME-direction group at 1.5 × the rate", () => {
    const report = build([
      readyLeg("Outbound"),
      readyLeg("Outbound"),
      readyLeg("Outbound"),
    ]);
    const g = bucket(report, "ready").groups[0];
    expect(g.legs).toHaveLength(3);
    expect(g.paired).toBe(false);
    expect(g.amountCad).toBe(3 * 0.5 * RATE);
    expect(g.amountFlag).toBe("oneWay");
  });

  it("prices two SAME-direction legs at 2 × 0.5 = 1 × the rate, still flagged", () => {
    // The total coincides with a paired round trip, so the flag is the only
    // thing telling the dispatcher these are two review-flagged half lines.
    const report = build([readyLeg("Outbound"), readyLeg("Outbound")]);
    const g = bucket(report, "ready").groups[0];
    expect(g.paired).toBe(false);
    expect(g.amountCad).toBe(RATE);
    expect(g.amountFlag).toBe("oneWay");
  });

  it("prices a direction-less leg at half, not at nothing", () => {
    const report = build([readyLeg(null)]);
    const g = bucket(report, "ready").groups[0];
    expect(g.amountCad).toBe(RATE * 0.5);
    expect(g.amountFlag).toBe("oneWay");
  });

  it("leaves a group with no roundTripKey unpriced, with the manual note", () => {
    // InvoiceDraftBuilder filters on `RoundTripKey is not null`, so these never
    // reach a generated line at all — estimating one would print a figure no
    // invoice will contain.
    const report = build([readyLeg("Outbound", { roundTripKey: null })]);
    const b = bucket(report, "ready");
    expect(b.groups[0].amountCad).toBeNull();
    expect(b.groups[0].amountNote).toBe("manual");
    expect(b.groups[0].amountFlag).toBeNull();
    expect(b.unpricedCount).toBe(1);
    expect(b.estimatedCad).toBe(0);
    expect(report.notes.some((n) => n.includes("no round-trip key"))).toBe(true);
  });

  it("estimates nothing at all without a usable round-trip rate", () => {
    const manual = client({
      activeContract: { ...client().activeContract!, billingModel: "Manual", ratePerRoundTripCad: null },
    });
    const report = build([readyLeg("Outbound"), readyLeg("Inbound")], [], manual);
    const b = bucket(report, "ready");
    expect(b.groups[0].amountCad).toBeNull();
    // No amountNote either: the banner note carries the explanation.
    expect(b.groups[0].amountNote).toBeNull();
    expect(b.unpricedCount).toBe(1);
    expect(report.notes.some((n) => n.includes("bills manually"))).toBe(true);
    // …and the headline says "Not priced" rather than a false $0.00.
    const owed = accrualHeadlines(report).find((h) => h.id === "owed")!;
    expect(owed.amountCad).toBe("Not priced");
  });

  it("raises a banner counting the half-rate groups", () => {
    const report = build([readyLeg("Outbound"), readyLeg("Inbound", { roundTripKey: "rt-2" })]);
    expect(bucket(report, "ready").groups).toHaveLength(2);
    expect(report.notes.some((n) => n.startsWith("2 groups of trips"))).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Per-PO pricing terms — the PO overrides, the contract is the fallback
// ---------------------------------------------------------------------------

describe("per-PO pricing terms", () => {
  it("prices a pair at the PO's round-trip rate, overriding the contract rate", () => {
    const report = build([readyLeg("Outbound"), readyLeg("Inbound")], [], client(), [
      po({ roundTripRateCad: 1_600 }),
    ]);
    const g = bucket(report, "ready").groups[0];
    expect(g.paired).toBe(true);
    expect(g.amountCad).toBe(1_600);
    expect(g.amountCad).not.toBe(RATE);
    expect(g.poNumber).toBe("PO-9");
  });

  it("falls back to the contract rate when the PO records none", () => {
    const report = build([readyLeg("Outbound"), readyLeg("Inbound")], [], client(), [po()]);
    expect(bucket(report, "ready").groups[0].amountCad).toBe(RATE);
  });

  it("prices a lone leg at the PO's EXPLICIT one-way rate, not at half", () => {
    // The whole point of an explicit one-way rate: a one-way run is not
    // necessarily half a round trip, and where the PO says so the estimate must
    // use the PO's figure rather than the arithmetic.
    const report = build([readyLeg("Outbound")], [], client(), [
      po({ roundTripRateCad: 1_600, oneWayRateCad: 900 }),
    ]);
    const g = bucket(report, "ready").groups[0];
    expect(g.amountCad).toBe(900);
    expect(g.amountCad).not.toBe(800);
    expect(g.amountFlag).toBe("oneWay");
  });

  it("uses ½ the EFFECTIVE round-trip rate when the PO records no one-way rate", () => {
    const report = build([readyLeg("Outbound")], [], client(), [po({ roundTripRateCad: 1_600 })]);
    expect(bucket(report, "ready").groups[0].amountCad).toBe(800);
  });

  it("prices a round trip split across two POs as two one-way trips, flagged", () => {
    // Decision 4: a round trip must be a single PO. Grouping by (PO, key) is
    // what implements it — the two legs land in two groups with no special case.
    const report = build(
      [
        readyLeg("Outbound", { poNumber: "PO-A" }),
        readyLeg("Inbound", { poNumber: "PO-B" }),
      ],
      [],
      client(),
      [
        po({ id: "a", poNumber: "PO-A", roundTripRateCad: 1_000 }),
        po({ id: "b", poNumber: "PO-B", roundTripRateCad: 2_000, oneWayRateCad: 1_200 }),
      ],
    );
    const groups = bucket(report, "ready").groups;
    expect(groups).toHaveLength(2);
    expect(groups.every((g) => !g.paired)).toBe(true);
    expect(groups.map((g) => g.poNumber)).toEqual(["PO-A", "PO-B"]);
    // Each side prices at ITS OWN PO's one-way rate: ½ × 1,000, then the
    // explicit 1,200.
    expect(groups.map((g) => g.amountCad)).toEqual([500, 1_200]);
    expect(groups.map((g) => g.amountFlag)).toEqual(["splitPo", "splitPo"]);
    expect(report.notes.some((n) => n.includes("DIFFERENT purchase order"))).toBe(true);
  });

  it("does not flag a split when the whole round trip sits on one PO", () => {
    const report = build(
      [readyLeg("Outbound", { poNumber: "PO-A" }), readyLeg("Inbound", { poNumber: "PO-A" })],
      [],
      client(),
      [po({ poNumber: "PO-A", roundTripRateCad: 1_000 })],
    );
    const groups = bucket(report, "ready").groups;
    expect(groups).toHaveLength(1);
    expect(groups[0].amountCad).toBe(1_000);
    expect(groups[0].amountFlag).toBeNull();
    expect(report.notes.some((n) => n.includes("DIFFERENT purchase order"))).toBe(false);
  });

  it("flags a trip outside its PO's window — and still prices and counts it", () => {
    // Warn, don't block (decision 3): the amount is unchanged, the row says so.
    const report = build([readyLeg("Outbound", { serviceDate: "2026-09-10" })], [], client(), [
      po({ issued: "2026-09-01", expiry: "2026-09-05", roundTripRateCad: 1_600 }),
    ]);
    const g = bucket(report, "ready").groups[0];
    expect(g.poFlags).toEqual(["outsideWindow"]);
    expect(g.amountCad).toBe(800);
    expect(bucket(report, "ready").estimatedCad).toBe(800);
    expect(report.notes.some((n) => n.includes("outside the issued → expiry window"))).toBe(true);
  });

  it("keeps a trip inside its PO's window unflagged", () => {
    const report = build([readyLeg("Outbound", { serviceDate: "2026-09-10" })], [], client(), [
      po({ issued: "2026-09-01", expiry: "2026-09-30" }),
    ]);
    expect(bucket(report, "ready").groups[0].poFlags).toEqual([]);
  });

  it("raises the banner when committed + upcoming work exceeds the PO value", () => {
    const report = build([readyLeg("Outbound"), readyLeg("Inbound")], [], client(), [
      po({ amountCad: 300 }),
    ]);
    const [commitment] = report.poCommitments;
    expect(commitment.poNumber).toBe("PO-9");
    expect(commitment.amountCad).toBe(300);
    expect(commitment.invoicedCad).toBe(0);
    expect(commitment.upcomingCad).toBe(RATE);
    expect(commitment.committedCad).toBe(RATE);
    expect(commitment.overageCad).toBe(200);
    expect(commitment.remainingCad).toBeNull();
    expect(commitment.kind).toBe("over");
    const banner = report.notes.find((n) => n.startsWith("Purchase order PO-9 is over"));
    expect(banner).toBeDefined();
    // PO value, invoiced, upcoming and the overage all spelled out — and it is a
    // warning, never a block.
    expect(banner).toContain("$300.00 authorised");
    expect(banner).toContain("$0.00 invoiced in this period");
    expect(banner).toContain("$500.00 upcoming");
    expect(banner).toContain("$200.00 over");
    expect(banner).toContain("nothing is blocked");
  });

  it("counts invoiced dollars and estimates together against the PO value", () => {
    const inv1 = trip({ status: "Invoiced", direction: "Outbound", billing: billed("Invoiced") });
    const report = build(
      [inv1, readyLeg("Outbound"), readyLeg("Inbound")],
      [invoice([line({ tripIds: [inv1.id], amountCad: 480 })])],
      client(),
      [po({ amountCad: 10_000 })],
    );
    const [c] = report.poCommitments;
    expect(c.invoicedCad).toBe(480);
    expect(c.upcomingCad).toBe(RATE);
    expect(c.committedCad).toBe(980);
    expect(c.overageCad).toBeNull();
    expect(c.remainingCad).toBe(9_020);
    expect(c.kind).toBe("ontime");
  });

  it("degrades to contract-rate pricing with a banner when POs failed to load", () => {
    const report = buildAccrualsReport({
      client: client(),
      period: PERIOD,
      today: TODAY,
      trips: [readyLeg("Outbound"), readyLeg("Inbound")],
      invoices: [],
      purchaseOrders: [],
      purchaseOrdersUnavailable: true,
    });
    // Still a full report, still priced — just at the contract rate.
    expect(bucket(report, "ready").groups[0].amountCad).toBe(RATE);
    expect(report.notes.some((n) => n.includes("Purchase orders could not be loaded"))).toBe(true);
  });

  it("names the effective PO on every group, contract default included", () => {
    const report = build([readyLeg("Outbound", { poNumber: null })], [], client(), [po()]);
    const g = bucket(report, "ready").groups[0];
    // The trip carries no PO of its own, so the contract default priced it — and
    // the row says which, rather than printing a bare "—".
    expect(g.poNumber).toBe("PO-9");
    expect(g.poFromContractDefault).toBe(true);
    expect(groupPoLabel(g)).toBe("PO-9 (contract default)");
  });

  it("reads as No PO when neither the trip nor the contract names one", () => {
    const noDefault = client({
      activeContract: { ...client().activeContract!, defaultPoNumber: null },
    });
    const report = build([readyLeg("Outbound", { poNumber: null })], [], noDefault);
    const g = bucket(report, "ready").groups[0];
    expect(g.poNumber).toBeNull();
    expect(groupPoLabel(g)).toBe("No PO");
    // Still priced at the contract rate — no PO is not no money.
    expect(g.amountCad).toBe(RATE * 0.5);
  });

  it("pins the flag labels — the INVOICE's words, sentence-cased", () => {
    // These mirror the backend's review-flag constants exactly in substance
    // ("one-way leg — review", "round-trip legs on different POs — priced
    // one-way, review", "outside PO window — review"). A client can see this
    // report and that invoice side by side, so the two must not describe one
    // condition in two phrasings — that is what this pin is for.
    expect(AMOUNT_FLAG_META.oneWay.label).toBe("One-way leg — review");
    expect(AMOUNT_FLAG_META.splitPo.label).toBe(
      "Round-trip legs on different POs — priced one-way, review",
    );
    expect(PO_FLAG_META.outsideWindow.label).toBe("Outside PO window — review");
    for (const meta of [AMOUNT_FLAG_META.oneWay, AMOUNT_FLAG_META.splitPo, PO_FLAG_META.outsideWindow]) {
      expect(meta.kind).toBeTruthy();
      expect(meta.label.length).toBeGreaterThan(0);
    }
  });

  it("NO REGRESSION: POs with no terms price byte-identically to no POs at all", () => {
    // The whole point of "PO overrides, contract is fallback": a client whose POs
    // record no rates must produce exactly today's numbers, to the cent.
    const trips = () => [
      readyLeg("Outbound", { roundTripKey: "r1" }),
      readyLeg("Inbound", { roundTripKey: "r1" }),
      readyLeg("Outbound", { roundTripKey: "r2" }),
      readyLeg(null, { roundTripKey: "r3" }),
      readyLeg("Outbound", { roundTripKey: null }),
    ];
    const withoutPos = build(trips());
    const withBarePos = build(trips(), [], client(), [po(), po({ id: "po-2", poNumber: "PO-OTHER" })]);

    const amounts = (r: AccrualsReport) =>
      r.buckets.flatMap((b) => b.groups.map((g) => [g.amountCad, g.amountSource, g.amountFlag, g.amountNote]));
    expect(amounts(withBarePos)).toEqual(amounts(withoutPos));
    // …and the figures themselves are the pre-PO ones: pair 1 ×, each unpaired
    // leg ½ ×, the keyless leg unpriced.
    expect(bucket(withBarePos, "ready").estimatedCad).toBe(RATE + 0.5 * RATE + 0.5 * RATE);
    expect(bucket(withBarePos, "ready").unpricedCount).toBe(1);
    expect(accrualTotals(withBarePos)).toEqual(accrualTotals(withoutPos));
  });
});

// ---------------------------------------------------------------------------
// Per-PO draft preview — what the generate-draft dialog promises it will create
// ---------------------------------------------------------------------------

describe("previewDraftsByPo", () => {
  const contract = client().activeContract!; // defaultPoNumber PO-9, rate 500

  it("reads as ONE unremarkable worksheet when all the work is on one PO", () => {
    const rows = previewDraftsByPo({
      legs: [
        { id: "a", poNumber: "PO-9", roundTripKey: "r1", direction: "Outbound" },
        { id: "b", poNumber: "PO-9", roundTripKey: "r1", direction: "Inbound" },
      ],
      contract,
      purchaseOrders: [po({ roundTripRateCad: 1_600 })],
    });
    expect(rows).toHaveLength(1);
    expect(rows[0].poNumber).toBe("PO-9");
    expect(rows[0].roundTrips).toBe(1);
    expect(rows[0].pairedCount).toBe(1);
    expect(rows[0].estimatedCad).toBe(1_600);
    expect(rows[0].termsLabel).toBe("$1,600 / round trip · $800 one way (½)");
  });

  it("splits one worksheet per PO, each at its own rates, No PO last", () => {
    const rows = previewDraftsByPo({
      legs: [
        { id: "a", poNumber: "PO-B", roundTripKey: "r1", direction: "Outbound" },
        { id: "b", poNumber: "PO-B", roundTripKey: "r1", direction: "Inbound" },
        { id: "c", poNumber: "PO-A", roundTripKey: "r2", direction: "Outbound" },
        { id: "d", poNumber: null, roundTripKey: "r3", direction: "Outbound" },
      ],
      // No contract default → the last leg has no PO at all.
      contract: { ...contract, defaultPoNumber: null },
      purchaseOrders: [
        po({ id: "a", poNumber: "PO-A", oneWayRateCad: 900 }),
        po({ id: "b", poNumber: "PO-B", roundTripRateCad: 2_000 }),
      ],
    });
    expect(rows.map((r) => r.poNumber)).toEqual(["PO-A", "PO-B", null]);
    expect(rows.map((r) => r.estimatedCad)).toEqual([900, 2_000, 250]);
    expect(rows[0].oneWayCount).toBe(1);
    expect(rows[1].pairedCount).toBe(1);
    // Work with no PO still drafts — it is a worksheet, not a hidden row.
    expect(rows[2].roundTrips).toBe(1);
  });

  it("counts a keyless leg as manual and never estimates it", () => {
    const rows = previewDraftsByPo({
      legs: [{ id: "a", poNumber: "PO-9", roundTripKey: null, direction: "Outbound" }],
      contract,
      purchaseOrders: [po()],
    });
    expect(rows[0].manualLegCount).toBe(1);
    expect(rows[0].roundTrips).toBe(0);
    expect(rows[0].estimatedCad).toBe(0);
  });

  it("marks a worksheet unpriced when neither the PO nor the contract has a rate", () => {
    const rows = previewDraftsByPo({
      legs: [{ id: "a", poNumber: "PO-9", roundTripKey: "r1", direction: "Outbound" }],
      contract: { ...contract, billingModel: "Manual", ratePerRoundTripCad: null },
      purchaseOrders: [po()],
    });
    expect(rows[0].unpriced).toBe(true);
    expect(rows[0].estimatedCad).toBe(0);
  });
});

// ---------------------------------------------------------------------------
// Buckets and the claim rule
// ---------------------------------------------------------------------------

describe("bucket membership and the claim rule", () => {
  it("splits Scheduled on the operational day", () => {
    const report = build([
      trip({ status: "Scheduled", serviceDate: "2026-09-20", direction: "Outbound", roundTripKey: "a" }),
      trip({ status: "Scheduled", serviceDate: "2026-09-10", direction: "Outbound", roundTripKey: "b" }),
      trip({ status: "InProgress", serviceDate: "2026-09-15", direction: "Outbound", roundTripKey: "c" }),
    ]);
    expect(bucket(report, "upcoming").groups).toHaveLength(1);
    expect(bucket(report, "scheduled").groups).toHaveLength(2);
  });

  it("counts one invoice line once when its legs straddle two buckets", () => {
    // One $500 line covering a Completed (paid) leg and an Invoiced one. Buckets
    // are built in ACCRUAL_BUCKET_ORDER, so `paid` carries the money and the
    // `invoiced` row says where it went — never $1,000 for one $500 line.
    const out = trip({ status: "Completed", direction: "Outbound", billing: billed("Paid") });
    const back = trip({ status: "Invoiced", direction: "Inbound", billing: billed("Invoiced") });
    const inv = invoice([line({ tripIds: [out.id, back.id] })]);
    const report = build([out, back], [inv]);

    expect(bucket(report, "paid").groups[0].amountCad).toBe(RATE);
    expect(bucket(report, "paid").actualCad).toBe(RATE);
    const straddler = bucket(report, "invoiced").groups[0];
    expect(straddler.amountCad).toBeNull();
    expect(straddler.amountNote).toBe("counted");
    expect(bucket(report, "invoiced").actualCad).toBe(0);
    expect(accrualTotals(report).actualCad).toBe(RATE);
  });

  it("degrades to 'amount unavailable' when a referenced invoice did not load", () => {
    const t = trip({ status: "Invoiced", direction: "Outbound", billing: billed("Invoiced", "inv-missing") });
    const report = build([t], []);
    const g = bucket(report, "invoiced").groups[0];
    expect(g.amountCad).toBeNull();
    expect(g.amountNote).toBe("unavailable");
    expect(report.notes.some((n) => n.includes("could not be loaded"))).toBe(true);
  });

  it("keeps cancelled and written-off trips out of every bucket", () => {
    const report = build([
      trip({ status: "Cancelled", cancelledReason: "road closure" }),
      trip({ status: "WrittenOff", writtenOffReason: "client dispute", roundTripKey: "w" }),
    ]);
    expect(accrualTotals(report).groupCount).toBe(0);
    expect(report.cancelled).toHaveLength(1);
    expect(report.writtenOff).toHaveLength(1);
  });

  it("keeps the claim order pinned — it is a money rule, not a display order", () => {
    expect(ACCRUAL_BUCKET_ORDER).toEqual(["paid", "invoiced", "ready", "scheduled", "upcoming"]);
  });
});

// ---------------------------------------------------------------------------
// Sections — presentation only, no money moves
// ---------------------------------------------------------------------------

describe("sections", () => {
  const trips = [
    // upcoming: a lone future leg → half rate
    trip({ status: "Scheduled", serviceDate: "2026-09-25", direction: "Outbound", roundTripKey: "f1" }),
    // scheduled: a pair under way → 1 × rate
    trip({ status: "InProgress", serviceDate: "2026-09-14", direction: "Outbound", roundTripKey: "s1" }),
    trip({ status: "InProgress", serviceDate: "2026-09-14", direction: "Inbound", roundTripKey: "s1" }),
    // ready: a pair complete but unbilled → 1 × rate
    trip({ status: "ReadyForBilling", serviceDate: "2026-09-08", direction: "Outbound", roundTripKey: "r1" }),
    trip({ status: "ReadyForBilling", serviceDate: "2026-09-08", direction: "Inbound", roundTripKey: "r1" }),
  ];

  function withBilled(): { trips: TripRecord[]; invoices: InvoiceDetailRecord[] } {
    const inv1 = trip({ status: "Invoiced", serviceDate: "2026-09-04", direction: "Outbound", billing: billed("Invoiced") });
    const paid1 = trip({ status: "Completed", serviceDate: "2026-09-02", direction: "Outbound", billing: billed("Paid") });
    const inv = invoice([
      line({ lineId: "L-INV", tripIds: [inv1.id], amountCad: 480 }),
      line({ lineId: "L-PAID", tripIds: [paid1.id], amountCad: 520 }),
    ]);
    return { trips: [...trips, inv1, paid1], invoices: [inv] };
  }

  it("yields the three sections in reading order", () => {
    const report = build(trips);
    expect(report.sections.map((s) => s.id)).toEqual(ACCRUAL_SECTION_ORDER);
    expect(report.sections.map((s) => s.id)).toEqual(["upcoming", "owed", "settled"]);
    expect(report.sections.map((s) => s.detail)).toEqual([true, true, false]);
  });

  it("holds the same bucket objects report.buckets holds", () => {
    const report = build(trips);
    for (const s of report.sections) {
      for (const b of s.buckets) {
        expect(report.buckets).toContain(b);
      }
    }
    // Every bucket belongs to exactly one section — nothing is double-counted
    // and nothing silently disappears from the month.
    const covered = report.sections.flatMap((s) => s.buckets.map((b) => b.id));
    expect([...covered].sort()).toEqual([...ACCRUAL_BUCKET_ORDER].sort());
  });

  it("subtotals each section as the sum of its buckets", () => {
    const { trips: all, invoices } = withBilled();
    const report = build(all, invoices);
    for (const s of report.sections) {
      expect(s.groupCount).toBe(s.buckets.reduce((n, b) => n + b.groups.length, 0));
      expect(s.actualCad).toBe(s.buckets.reduce((n, b) => n + b.actualCad, 0));
      expect(s.estimatedCad).toBe(s.buckets.reduce((n, b) => n + b.estimatedCad, 0));
      expect(s.unpricedCount).toBe(s.buckets.reduce((n, b) => n + b.unpricedCount, 0));
    }
    // upcoming = the lone future leg (half) + the in-progress pair (full)
    expect(section(report, "upcoming").estimatedCad).toBe(0.5 * RATE + RATE);
    expect(section(report, "upcoming").actualCad).toBe(0);
    // owed = the unbilled-but-complete pair (estimate) + the issued invoice line
    expect(section(report, "owed").estimatedCad).toBe(RATE);
    expect(section(report, "owed").actualCad).toBe(480);
    // settled = the paid line only
    expect(section(report, "settled").actualCad).toBe(520);
  });

  it("does not move money: section totals sum to accrualTotals", () => {
    const { trips: all, invoices } = withBilled();
    const report = build(all, invoices);
    const totals = accrualTotals(report);
    const summed = report.sections.reduce(
      (acc, s) => ({
        groupCount: acc.groupCount + s.groupCount,
        actualCad: acc.actualCad + s.actualCad,
        estimatedCad: acc.estimatedCad + s.estimatedCad,
        unpricedCount: acc.unpricedCount + s.unpricedCount,
      }),
      { groupCount: 0, actualCad: 0, estimatedCad: 0, unpricedCount: 0 },
    );
    expect(summed).toEqual(totals);
    expect(totals.actualCad).toBe(1000);
  });

  it("keeps paid work out of every detail-bearing section", () => {
    const { trips: all, invoices } = withBilled();
    const report = build(all, invoices);
    const detailBuckets = report.sections.filter((s) => s.detail).flatMap((s) => s.buckets.map((b) => b.id));
    expect(detailBuckets).not.toContain("paid");
    // …but paid is still in the summary, so the month reconciles.
    expect(accrualsEmailPayload(report).summary.map((r) => r.bucketLabel)).toContain("Paid");
  });

  it("still yields three sections for an empty month", () => {
    const report = build([]);
    expect(report.sections.map((s) => s.id)).toEqual(["upcoming", "owed", "settled"]);
    expect(report.sections.every((s) => s.groupCount === 0)).toBe(true);
    expect(report.buckets.every((b) => b.groups.length === 0)).toBe(true);
    expect(accrualHeadlines(report).map((h) => h.amountCad)).toEqual(["$0.00", "$0.00"]);
  });
});

// ---------------------------------------------------------------------------
// Headline figures + the four outputs
// ---------------------------------------------------------------------------

describe("headline figures", () => {
  it("leads with upcoming expenses, then monies owed", () => {
    const report = build([
      trip({ status: "Scheduled", serviceDate: "2026-09-25", direction: "Outbound", roundTripKey: "f1" }),
      trip({ status: "ReadyForBilling", direction: "Outbound", roundTripKey: "r1" }),
      trip({ status: "ReadyForBilling", direction: "Inbound", roundTripKey: "r1" }),
    ]);
    const [upcoming, owed] = accrualHeadlines(report);
    expect(upcoming.label).toBe("Upcoming expenses");
    // Wholly estimated → the figure itself carries " est.", never bare dollars.
    expect(upcoming.amountCad).toBe("$250.00 est.");
    expect(upcoming.detail).toBe("1 round trip");
    expect(owed.label).toBe("Monies owed");
    expect(owed.amountCad).toBe("$500.00 est.");
    expect(accrualHeadlines(report)).toHaveLength(2);
  });

  it("keeps an estimated share visible inside a combined figure", () => {
    const inv1 = trip({ status: "Invoiced", direction: "Outbound", billing: billed("Invoiced") });
    const report = build(
      [
        inv1,
        trip({ status: "ReadyForBilling", direction: "Outbound", roundTripKey: "r1" }),
        trip({ status: "ReadyForBilling", direction: "Inbound", roundTripKey: "r1" }),
      ],
      [invoice([line({ tripIds: [inv1.id], amountCad: 480 })])],
    );
    const owed = accrualHeadlines(report).find((h) => h.id === "owed")!;
    expect(owed.amountCad).toBe("$980.00");
    expect(owed.detail).toContain("incl. $500.00 est.");
  });

  it("carries the unpriced count in the subline", () => {
    const report = build([readyLeg("Outbound", { roundTripKey: null })]);
    const owed = accrualHeadlines(report).find((h) => h.id === "owed")!;
    expect(owed.detail).toContain("1 unpriced");
  });
});

describe("clipboard export", () => {
  it("leads with the headline figures, then the sections in reading order", () => {
    const report = build([
      trip({ status: "Scheduled", serviceDate: "2026-09-25", direction: "Outbound", roundTripKey: "f1" }),
      trip({ status: "ReadyForBilling", direction: "Outbound", roundTripKey: "r1" }),
      trip({ status: "ReadyForBilling", direction: "Inbound", roundTripKey: "r1" }),
    ]);
    const text = accrualsClipboardText(report);
    const iUpcomingFigure = text.indexOf("UPCOMING EXPENSES");
    const iOwedFigure = text.indexOf("MONIES OWED");
    const iUpcomingSection = text.indexOf("UPCOMING WORK — NOT YET DONE");
    const iSettled = text.indexOf("SETTLED THIS MONTH");
    expect(iUpcomingFigure).toBeGreaterThan(-1);
    expect(iOwedFigure).toBeGreaterThan(iUpcomingFigure);
    expect(iUpcomingSection).toBeGreaterThan(iOwedFigure);
    expect(iSettled).toBeGreaterThan(iUpcomingSection);
    // Tax never appears on any output — a platform non-negotiable.
    expect(text).toContain("Taxes are applied in QuickBooks; this report contains none.");
    expect(text).not.toMatch(/GST|HST|PST/);
  });

  it("prints the one-way caveat in words beside the figure", () => {
    const report = build([readyLeg("Outbound")]);
    expect(accrualsClipboardText(report)).toContain("$250.00 est. (One-way leg — review)");
  });

  it("prints the PO authorisation table, with the terms that priced it", () => {
    const report = build(
      [readyLeg("Outbound"), readyLeg("Inbound")],
      [],
      client(),
      [po({ amountCad: 10_000, roundTripRateCad: 1_600 })],
    );
    const text = accrualsClipboardText(report);
    expect(text).toContain("PURCHASE ORDERS — AUTHORISED VALUE VS THIS MONTH'S WORK");
    expect(text).toContain("$1,600 / round trip · $800 one way (½)");
    expect(text).toContain("$8,400.00 left of $10,000.00");
    expect(text).not.toMatch(/GST|HST|PST/);
  });
});

describe("email payload", () => {
  const paid = trip({ status: "Completed", direction: "Outbound", billing: billed("Paid") });
  const report = build(
    [
      paid,
      trip({ status: "Scheduled", serviceDate: "2026-09-25", direction: "Outbound", roundTripKey: "f1" }),
      trip({ status: "ReadyForBilling", direction: "Outbound", roundTripKey: "r1" }),
      trip({ status: "ReadyForBilling", direction: "Inbound", roundTripKey: "r1" }),
    ],
    [invoice([line({ lineId: "L-PAID", tripIds: [paid.id], amountCad: 520 })])],
  );

  it("emits the two headline figures", () => {
    expect(accrualsEmailPayload(report).headline).toEqual([
      { label: "Upcoming expenses", amountCad: "$250.00 est.", detail: "1 round trip" },
      { label: "Monies owed", amountCad: "$500.00 est.", detail: "1 round trip" },
    ]);
  });

  it("orders summary by section, subtotal row first then its buckets", () => {
    const rows = accrualsEmailPayload(report).summary;
    expect(rows.map((r) => [r.bucketLabel, r.emphasis])).toEqual([
      ["Upcoming work — not yet done", true],
      ["Upcoming", false],
      ["Scheduled", false],
      ["Monies owed", true],
      ["Ready for billing", false],
      ["Invoiced", false],
      ["Settled this month", true],
      ["Paid", false],
    ]);
    // Still the complete position: all five buckets, every zero.
    expect(rows.filter((r) => !r.emphasis)).toHaveLength(ACCRUAL_BUCKET_ORDER.length);
  });

  it("emits detail tables only for the detail-bearing sections, in section order", () => {
    const payload = accrualsEmailPayload(report);
    expect(payload.buckets.map((b) => b.label)).toEqual(["Upcoming", "Ready for billing"]);
    // Paid has a row in the summary and an invoice in Invoices Referenced, but
    // no per-trip table anywhere.
    expect(payload.buckets.map((b) => b.label)).not.toContain("Paid");
    expect(payload.invoices).toHaveLength(1);
  });
});
