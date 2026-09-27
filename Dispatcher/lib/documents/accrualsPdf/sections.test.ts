import { describe, expect, it } from "vitest";
import { accrualsReportHtml } from "./index";
import { bucketsBlock, header, summaryBlock } from "./sections";
import { COMPANY } from "@/lib/company";
import { buildAccrualsReport, type AccrualsReport } from "@/lib/billing/accruals";
import type { ClientRecord, PurchaseOrderRecord } from "@/lib/api/clients";
import type { InvoiceDetailRecord, InvoiceLineRecord } from "@/lib/api/billing";
import type { TripDirection, TripRecord } from "@/lib/api/trips";
import type { Period } from "@/lib/period";

// Form NL-ACC-01 as a printable. The sheet is MONOCHROME and goes to the
// client, so the rules it lives or dies by are all about words and money:
//
//   - ONE amount column. Billed and estimated dollars merge into a single
//     figure everywhere (summary rows, bucket footers, the totals row); the
//     " est." marking rides the figure itself, and a bucket that mixes the two
//     keeps the split recoverable in its detail line, not in a second column.
//   - A split-PO group is NOT priced, and must print the words "needs a
//     decision" rather than "$0.00" — a client reading $0.00 would conclude
//     the run was free. That is the one failure mode worth a whole file.
//   - The pricing caveats are the invoice's own words (AMOUNT_FLAG_META), in
//     caps because the page has no colour to lean on.
//   - No tax appears anywhere: QuickBooks Online owns all tax calculation.

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

function readyLeg(direction: TripDirection | null, overrides: Partial<TripRecord> = {}): TripRecord {
  return trip({ status: "ReadyForBilling", direction, ...overrides });
}

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

function invoice(lines: InvoiceLineRecord[]): InvoiceDetailRecord {
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
    ...{},
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

function build(
  trips: TripRecord[],
  invoices: InvoiceDetailRecord[] = [],
  purchaseOrders: PurchaseOrderRecord[] = [],
): AccrualsReport {
  return buildAccrualsReport({
    client: client(),
    period: PERIOD,
    today: TODAY,
    trips,
    invoices,
    purchaseOrders,
  });
}

/** The <th> texts of the first table in a block — the column contract. */
function headersOf(html: string): string[] {
  const table = html.slice(html.indexOf("<thead>"), html.indexOf("</thead>"));
  return [...table.matchAll(/<th[^>]*>([^<]*)<\/th>/g)].map((m) => m[1].trim());
}

/** Every money-looking token in a block, in order. */
function amountsIn(html: string): string[] {
  return [...html.matchAll(/\$[\d,]+\.\d{2}(?: EST\.)?/g)].map((m) => m[0]);
}

// ---------------------------------------------------------------------------
// Summary — one amount column
// ---------------------------------------------------------------------------

describe("summaryBlock", () => {
  const billedLeg = trip({ status: "Invoiced", direction: "Outbound", billing: {
    state: "Invoiced",
    invoiceId: "inv-1",
    invoiceNumber: "NL-INV-0001",
    qboInvoiceId: "QBO-77",
    qboEnteredDate: "2026-09-12",
    paymentConfirmedDate: null,
  } });
  const report = build(
    [
      billedLeg,
      readyLeg("Outbound", { roundTripKey: "r1" }),
      readyLeg("Inbound", { roundTripKey: "r1" }),
    ],
    [invoice([line({ tripIds: [billedLeg.id], amountCad: 480 })])],
  );
  const html = summaryBlock(report);

  it("has ONE amount column, not an actual/estimated pair", () => {
    expect(headersOf(html)).toEqual([
      "Section / billing state",
      "Trips",
      "Amount (CAD)",
      "Unpriced",
    ]);
    expect(html).not.toContain("Actual (CAD)");
    expect(html).not.toContain("Estimated (CAD)");
    expect(html).not.toContain("Round trips");
  });

  it("prints exactly one figure per row", () => {
    // 3 section subtotals + 5 buckets + the totals row, one amount each.
    const rows = [...html.matchAll(/<tr[^>]*>[\s\S]*?<\/tr>/g)].map((m) => m[0]);
    const bodyRows = rows.filter((r) => r.includes("class=\"amt\"") && !r.includes("<th"));
    expect(bodyRows).toHaveLength(9);
    for (const row of bodyRows) {
      expect(amountsIn(row)).toHaveLength(1);
    }
  });

  it("marks a wholly-estimated row EST. and leaves billed money bare", () => {
    const rowFor = (label: string) =>
      [...html.matchAll(/<tr[^>]*>[\s\S]*?<\/tr>/g)]
        .map((m) => m[0])
        .find((r) => r.includes(`>${label}</td>`))!;
    // Ready for billing is all estimate; Invoiced is all issued-invoice money.
    expect(amountsIn(rowFor("Ready for billing"))).toEqual(["$500.00 EST."]);
    expect(amountsIn(rowFor("Invoiced"))).toEqual(["$480.00"]);
    // The section mixing them merges to one plain figure — the estimated share
    // is carried by the detail lines, not by a second column.
    expect(amountsIn(rowFor("Monies owed"))).toEqual(["$980.00"]);
  });

  it("totals the whole report on one merged figure", () => {
    const totals = html.slice(html.indexOf("<tfoot>"));
    expect(amountsIn(totals)).toEqual(["$980.00"]);
  });
});

// ---------------------------------------------------------------------------
// Detail tables — the figure, the caveat, and the run that is not priced
// ---------------------------------------------------------------------------

describe("bucketsBlock", () => {
  it("prints an unpaired group's FULL round-trip estimate with the caveat in words", () => {
    const html = bucketsBlock(build([readyLeg("Outbound")]));
    expect(html).toContain("$500.00 EST.");
    expect(html).toContain("FULL ROUND TRIP — LEGS DID NOT PAIR, REVIEW");
    // The retired one-way pricing must not come back in any wording.
    expect(html).not.toMatch(/one.way/i);
  });

  it("renders a split-PO group as needing a decision, never as $0.00", () => {
    // The backend has no null amount, so it emits a zero-amount line for these.
    // Printing that zero verbatim would tell the client the run was free.
    const html = bucketsBlock(
      build(
        [
          readyLeg("Outbound", { poNumber: "PO-A" }),
          readyLeg("Inbound", { poNumber: "PO-B" }),
        ],
        [],
        [po({ id: "a", poNumber: "PO-A" }), po({ id: "b", poNumber: "PO-B" })],
      ),
    );
    expect(html).toContain("ROUND-TRIP LEGS ON DIFFERENT POS — NOT PRICED, NEEDS A DECISION");
    // …and the rows are still there: not priced is not hidden.
    const rows = [...html.matchAll(/<tr>[\s\S]*?<\/tr>/g)].map((m) => m[0]);
    const splitRows = rows.filter((r) => r.includes("PO-A") || r.includes("PO-B"));
    expect(splitRows).toHaveLength(2);
    // Not one dollar figure between them — no "$0.00 of work".
    for (const row of splitRows) {
      expect(amountsIn(row)).toEqual([]);
      expect(row).toContain("NEEDS A DECISION");
    }
  });

  it("closes a bucket with one amount and the estimated share in words", () => {
    const paidLeg = trip({
      status: "Completed",
      direction: "Outbound",
      billing: {
        state: "Paid",
        invoiceId: "inv-1",
        invoiceNumber: "NL-INV-0001",
        qboInvoiceId: "QBO-77",
        qboEnteredDate: "2026-09-12",
        paymentConfirmedDate: "2026-09-14",
      },
    });
    const html = bucketsBlock(
      build(
        [paidLeg, readyLeg("Outbound", { roundTripKey: "r1" }), readyLeg("Inbound", { roundTripKey: "r1" })],
        [invoice([line({ tripIds: [paidLeg.id], amountCad: 520 })])],
      ),
    );
    const footer = html.slice(html.indexOf("<tfoot>"), html.indexOf("</tfoot>"));
    expect(amountsIn(footer)).toEqual(["$500.00 EST."]);
    expect(footer).toContain("1 trip");
    // The old pair of footer cells is gone.
    expect(html).not.toContain("actual</td>");
  });

  it("still prints a nil statement for an empty month", () => {
    const html = bucketsBlock(build([]));
    expect(html).toContain("No trips for this client in the period.");
  });
});

// ---------------------------------------------------------------------------
// Banner + whole sheet
// ---------------------------------------------------------------------------

describe("the sheet as a whole", () => {
  it("says estimates are at the PO's rate, not 'contract-rate'", () => {
    // PO rates govern now; the old wording promised the wrong number.
    const banner = header(COMPANY, build([readyLeg("Outbound")]));
    expect(banner).toContain("estimates at the purchase order's rate");
    expect(banner).not.toContain("contract-rate estimates");
  });

  it("prints no tax anywhere — QuickBooks owns that calculation", () => {
    const html = accrualsReportHtml(build([readyLeg("Outbound"), readyLeg("Inbound")]), COMPANY);
    expect(html).not.toMatch(/GST|HST|PST/);
    expect(html).toContain("Taxes are applied in QuickBooks; this report contains none.");
  });
});
