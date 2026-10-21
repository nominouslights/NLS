import { describe, expect, it } from "vitest";
import { accrualsReportHtml } from "./index";
import { bucketsBlock, header, headlineBlock, notesBlock, summaryBlock } from "./sections";
import { ACCRUALS_REPORT_STYLES } from "./styles";
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

/** Like build(), plus the carried-forward trips the screen fetches. */
function buildCarried(trips: TripRecord[], carriedTrips: TripRecord[]): AccrualsReport {
  return buildAccrualsReport({
    client: client(),
    period: PERIOD,
    today: TODAY,
    trips,
    carriedTrips,
    invoices: [],
    purchaseOrders: [],
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
    // 3 section subtotals + 4 buckets + the totals row, one amount each.
    const rows = [...html.matchAll(/<tr[^>]*>[\s\S]*?<\/tr>/g)].map((m) => m[0]);
    const bodyRows = rows.filter((r) => r.includes("class=\"amt\"") && !r.includes("<th"));
    expect(bodyRows).toHaveLength(8);
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

  it("prints the sections — not a nil statement — when only carried work exists", () => {
    // No trips dated in September, but an August run is still not billed: that
    // money is still owed, so the sheet must show it rather than say "nothing".
    const html = bucketsBlock(
      buildCarried([], [readyLeg("Outbound", { serviceDate: "2026-08-12", tripNumber: "NL-AUG-1" })]),
    );
    expect(html).not.toContain("No trips for this client in the period.");
    expect(html).toContain("NL-AUG-1");
    expect(html).toContain("$500.00 EST.");
  });

  it("marks a carried row's date in words, beside plain in-month dates", () => {
    const html = bucketsBlock(
      buildCarried(
        [readyLeg("Outbound", { serviceDate: "2026-09-12", roundTripKey: "sep" })],
        [readyLeg("Outbound", { serviceDate: "2026-08-12", roundTripKey: "aug" })],
      ),
    );
    // Stacked under the date in its own wrapping span — never one long nowrap
    // string that would widen the Date column past the 190mm sheet.
    expect(html).toContain('>2026-08-12<span class="cf">from an earlier month</span></td>');
    expect(html).not.toContain("2026-08-12 · from an earlier month");
    expect(html).toContain(">2026-09-12</td>");
    // The bucket's tally line counts it, and the sheet never ages the debt.
    expect(html).toContain("2 trips · 1 from an earlier month");
    expect(html).not.toMatch(/overdue|days outstanding|past due/i);
  });

  it("prints one Scheduled bucket — Scheduled and InProgress together", () => {
    const html = bucketsBlock(
      build([
        trip({ status: "Scheduled", serviceDate: "2026-09-25", roundTripKey: "a" }),
        trip({ status: "InProgress", serviceDate: "2026-09-14", roundTripKey: "b" }),
      ]),
    );
    expect(html).toContain("Scheduled — Run not yet complete");
    expect(html).not.toMatch(/>Upcoming —/);
    expect(html).toContain("2 trips");
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

  it("prints no tax with carried work either, and words the invoices line for the whole report", () => {
    const html = accrualsReportHtml(
      buildCarried([], [readyLeg("Outbound", { serviceDate: "2026-08-12" })]),
      COMPANY,
    );
    expect(html).not.toMatch(/GST|HST|PST/);
    expect(html).toContain("No issued invoices are referenced by the trips on this report.");
    expect(html).not.toContain("this period's trips");
    // The count says earlier-month work is carried in — as a plain .info line,
    // never inside the .warn box (carry-forward is not a warning).
    expect(html).toContain(
      '<div class="info">1 trip from an earlier month is not yet completed and is carried into this report.</div>',
    );
    expect(html).not.toMatch(/<div class="warn">[^<]*carried into this report/);
    expect(header(COMPANY, build([]))).toContain("Trips from earlier months that are not yet completed");
  });

  it("lets the carried-forward marker wrap inside the nowrap Date cell", () => {
    expect(ACCRUALS_REPORT_STYLES).toMatch(/td\.ref \.cf \{[^}]*white-space: normal/);
    expect(ACCRUALS_REPORT_STYLES).toMatch(/td\.ref \.cf \{[^}]*display: block/);
  });

  it("never prints a nil statement, or claims carry-in, when earlier months failed to load", () => {
    const report = buildAccrualsReport({
      client: client(),
      period: PERIOD,
      today: TODAY,
      trips: [],
      carriedTripsUnavailable: true,
      invoices: [],
      purchaseOrders: [],
    });
    const block = bucketsBlock(report);
    expect(block).toContain("this is NOT a nil statement");
    expect(block).not.toContain("No trips for this client in the period.</td>");
    const head = header(COMPANY, report);
    expect(head).not.toContain("are carried in and marked as such");
    expect(head).toContain("could NOT be loaded");
    expect(notesBlock(report)).toContain("Open trips from earlier months could not be loaded");
  });

  it("prints the carry-forward failure banner rather than a silent omission", () => {
    const report = buildAccrualsReport({
      client: client(),
      period: PERIOD,
      today: TODAY,
      trips: [readyLeg("Outbound")],
      carriedTripsUnavailable: true,
      invoices: [],
      purchaseOrders: [],
    });
    expect(notesBlock(report)).toContain("Open trips from earlier months could not be loaded");
  });
});

// ---------------------------------------------------------------------------
// Carry forward on the printed sheet — edge cases (QA audit of b84ab32)
// ---------------------------------------------------------------------------

describe("NL-ACC-01 carry forward — edge cases", () => {
  function billing(state: "Invoiced" | "Paid", invoiceId = "inv-1"): NonNullable<TripRecord["billing"]> {
    return {
      state,
      invoiceId,
      invoiceNumber: "NL-INV-0001",
      qboInvoiceId: "QBO-77",
      qboEnteredDate: "2026-09-12",
      paymentConfirmedDate: state === "Paid" ? "2026-09-14" : null,
    };
  }

  function report(args: { trips?: TripRecord[]; carriedTrips?: TripRecord[]; invoices?: InvoiceDetailRecord[] }) {
    return buildAccrualsReport({
      client: client(),
      period: PERIOD,
      today: TODAY,
      trips: args.trips ?? [],
      carriedTrips: args.carriedTrips ?? [],
      invoices: args.invoices ?? [],
      purchaseOrders: [],
    });
  }

  it("prints a carried invoiced row with its real amount — never EST. — and the date marker", () => {
    const aug = trip({ status: "Invoiced", serviceDate: "2026-08-20", billing: billing("Invoiced") });
    const html = bucketsBlock(
      report({ carriedTrips: [aug], invoices: [invoice([line({ lineId: "L-AUG", tripIds: [aug.id], amountCad: 480 })])] }),
    );
    expect(html).toContain('>2026-08-20<span class="cf">from an earlier month</span></td>');
    expect(html).toContain("QBO-77");
    expect(amountsIn(html)).toContain("$480.00");
    expect(amountsIn(html)).not.toContain("$480.00 EST.");
  });

  it("prints AMOUNT UNAVAILABLE on a carried row whose invoice failed, with the warning before the plain count", () => {
    const aug = trip({ status: "Invoiced", serviceDate: "2026-08-20", billing: billing("Invoiced", "inv-missing") });
    const r = report({ carriedTrips: [aug] });
    const html = bucketsBlock(r);
    expect(html).toContain('>2026-08-20<span class="cf">from an earlier month</span></td>');
    expect(html).toContain('<td class="miss">AMOUNT UNAVAILABLE</td>');
    // The bucket's tally reads Not priced, never a false $0.00 owed.
    expect(html).toContain(
      '<td class="lbl2" colspan="5">1 trip · 1 from an earlier month · 1 unpriced</td><td class="amt">Not priced</td>',
    );
    const notes = notesBlock(r);
    const warn = notes.indexOf('<div class="warn">1 referenced invoice could not be loaded');
    const info = notes.indexOf('<div class="info">1 trip from an earlier month');
    expect(warn).toBeGreaterThanOrEqual(0);
    expect(info).toBeGreaterThan(warn);
  });

  it("prints 'needs a decision' — never $0.00 — on both sides of a boundary round trip split across POs", () => {
    const html = bucketsBlock(
      report({
        trips: [readyLeg("Inbound", { serviceDate: "2026-09-01", roundTripKey: "edge", poNumber: "PO-B" })],
        carriedTrips: [readyLeg("Outbound", { serviceDate: "2026-08-31", roundTripKey: "edge", poNumber: "PO-A" })],
      }),
    );
    const flag = "ROUND-TRIP LEGS ON DIFFERENT POS — NOT PRICED, NEEDS A DECISION";
    expect(html.split(flag)).toHaveLength(3);
    expect(html).toContain('>2026-08-31<span class="cf">from an earlier month</span></td>');
    expect(html).toContain(">2026-09-01</td>");
    // Neither leg's row carries a figure (the $0.00 on the empty Settled line is not a row).
    const rows = [...html.matchAll(/<tr>[\s\S]*?<\/tr>/g)]
      .map((m) => m[0])
      .filter((r) => r.includes('class="ref">2026-0'));
    expect(rows).toHaveLength(2);
    for (const r of rows) expect(amountsIn(r)).toEqual([]);
  });

  it("leads the headline block with carried estimates, still marked EST.", () => {
    const html = headlineBlock(
      report({
        carriedTrips: [
          readyLeg("Outbound", { serviceDate: "2026-08-12", roundTripKey: "aug" }),
          readyLeg("Inbound", { serviceDate: "2026-08-12", roundTripKey: "aug" }),
        ],
      }),
    );
    expect(html).toContain('<div class="hl-amt">$500.00 EST.</div>');
    expect(html).toContain('<div class="hl-det">1 trip · 1 from an earlier month</div>');
  });

  it("counts carried groups in the summary like any other — the totals row included", () => {
    const aug = trip({ status: "Invoiced", serviceDate: "2026-08-20", roundTripKey: "i", billing: billing("Invoiced") });
    const html = summaryBlock(
      report({
        trips: [readyLeg("Outbound"), readyLeg("Inbound")],
        carriedTrips: [aug],
        invoices: [invoice([line({ lineId: "L-AUG", tripIds: [aug.id], amountCad: 480 })])],
      }),
    );
    // Monies owed subtotal: 2 groups, $980.00 merged (not EST. — part is billed).
    expect(html).toMatch(/<td>Monies owed<\/td>\s*<td class="amt">2<\/td>\s*<td class="amt">\$980\.00<\/td>/);
    expect(html).toMatch(/<td class="lbl2">Totals<\/td>\s*<td class="amt">2<\/td>\s*<td class="amt">\$980\.00<\/td>/);
  });

  it("prints no .info line and the normal carry-in sentence when nothing is carried", () => {
    const r = report({ trips: [readyLeg("Outbound")] });
    expect(notesBlock(r)).not.toContain('class="info"');
    const head = header(COMPANY, r);
    expect(head).toContain("are carried in and marked as such");
    expect(head).not.toContain("could NOT be loaded");
    expect(bucketsBlock(r)).not.toContain('class="cf"');
  });
});

// ---------------------------------------------------------------------------
// A "counted with the paired leg" row is priced elsewhere — the sheet must not
// print it as unpriced anywhere
// ---------------------------------------------------------------------------

describe("NL-ACC-01 counted row", () => {
  // Carried August Invoiced leg + September Completed leg on ONE $500 line:
  // Settled carries the $500, the carried Invoiced row reads "counted".
  function straddle(): AccrualsReport {
    const billing = (state: "Invoiced" | "Paid"): NonNullable<TripRecord["billing"]> => ({
      state,
      invoiceId: "inv-1",
      invoiceNumber: "NL-INV-0001",
      qboInvoiceId: "QBO-77",
      qboEnteredDate: "2026-09-12",
      paymentConfirmedDate: state === "Paid" ? "2026-09-14" : null,
    });
    const out = trip({ status: "Invoiced", serviceDate: "2026-08-31", direction: "Outbound", billing: billing("Invoiced") });
    const back = trip({ status: "Completed", serviceDate: "2026-09-01", direction: "Inbound", billing: billing("Paid") });
    return buildAccrualsReport({
      client: client(),
      period: PERIOD,
      today: TODAY,
      trips: [back],
      carriedTrips: [out],
      invoices: [invoice([line({ lineId: "L-CNT", tripIds: [out.id, back.id] })])],
      purchaseOrders: [],
    });
  }

  it("keeps the row's own words, but tallies the bucket as $0.00 with no unpriced clause", () => {
    const html = bucketsBlock(straddle());
    expect(html).toContain('<td class="flag">COUNTED WITH THE PAIRED LEG</td>');
    expect(html).toContain(
      '<td class="lbl2" colspan="5">1 trip · 1 from an earlier month</td><td class="amt">$0.00</td>',
    );
    expect(html).not.toContain("unpriced");
    expect(html).not.toContain("Not priced");
  });

  it("prints — in every Unpriced cell of the summary, the totals row included", () => {
    const html = summaryBlock(straddle());
    expect(html).toMatch(
      /<td>Monies owed<\/td>\s*<td class="amt">1<\/td>\s*<td class="amt">\$0\.00<\/td>\s*<td class="amt">—<\/td>/,
    );
    expect(html).toMatch(
      /<td class="ind">Invoiced<\/td>\s*<td class="amt">1<\/td>\s*<td class="amt">\$0\.00<\/td>\s*<td class="amt">—<\/td>/,
    );
    expect(html).toMatch(
      /<td class="lbl2">Totals<\/td>\s*<td class="amt">2<\/td>\s*<td class="amt">\$500\.00<\/td>\s*<td class="amt">—<\/td>/,
    );
    expect(html).not.toContain("Not priced");
  });

  it("leads Monies owed with $0.00, not 'Not priced'", () => {
    const html = headlineBlock(straddle());
    expect(html).toContain('<div class="hl-amt">$0.00</div>');
    expect(html).not.toContain("Not priced");
    expect(html).not.toContain("unpriced");
    expect(html).toContain('<div class="hl-det">1 trip · 1 from an earlier month</div>');
  });
});
