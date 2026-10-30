import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ClientRecord } from "@/lib/api/clients";
import type { TripListParams, TripPage, TripRecord } from "@/lib/api/trips";
import type { Period } from "@/lib/period";

// The accruals screen's carry-forward failure path: when the earlier-month
// fetch fails, RETRY must be offered whether or not the month has trips of its
// own — exactly once — and must actually refetch. The API modules are mocked at
// the function seam (listClients / listTrips / listPurchaseOrders / getInvoice);
// every derivation underneath is the real lib/billing/accruals.

const { listClients, listPurchaseOrders, listTrips, getInvoice } = vi.hoisted(() => ({
  listClients: vi.fn<() => Promise<ClientRecord[]>>(),
  listPurchaseOrders: vi.fn<(id: string) => Promise<never[]>>(),
  listTrips: vi.fn<(p: TripListParams) => Promise<TripPage>>(),
  getInvoice: vi.fn<(id: string) => Promise<never>>(),
}));

vi.mock("@/lib/api/clients", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api/clients")>()),
  listClients,
  listPurchaseOrders,
}));
vi.mock("@/lib/api/trips", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api/trips")>()),
  listTrips,
}));
vi.mock("@/lib/api/billing", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api/billing")>()),
  getInvoice,
}));

import ClientAccruals from "./ClientAccruals";

const PERIOD: Period = { granularity: "month", start: "2026-10-01", end: "2026-10-31" };

const CLIENT: ClientRecord = {
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
    startDate: "2025-01-01",
    endDate: null,
    billingModel: "RoundTripRate",
    ratePerRoundTripCad: 500,
    budgetCode: "OPS-100",
    billingFrequency: "Monthly",
    netTermsDays: 30,
    defaultPoNumber: "PO-9",
  },
};

let seq = 0;
function trip(overrides: Partial<TripRecord> = {}): TripRecord {
  seq += 1;
  return {
    id: `t${seq}`,
    tripNumber: `NL-${1000 + seq}`,
    serviceDate: "2026-10-10",
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
    roundTripKey: `rt-${seq}`,
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

const page = (items: TripRecord[]): TripPage => ({ items, page: null, pageSize: null, totalCount: items.length });

/** listTrips: the month call (no `status`) answers `own`; the carry-forward
 *  calls (one per open status) reject while `carryFails()` says so. */
function wireTrips(own: TripRecord[], carried: TripRecord[], carryFails: () => boolean) {
  listTrips.mockImplementation(async (p) => {
    if (!p.status) return page(own);
    if (carryFails()) throw new Error("503");
    return page(carried.filter((t) => t.status === p.status));
  });
}

function renderScreen() {
  return render(
    <ClientAccruals clientId="c1" setClientId={() => {}} period={PERIOD} setPeriod={() => {}} tabs={null} />,
  );
}

const CARRY_NOTE = /Open trips from earlier months could not be loaded/;

beforeEach(() => {
  listClients.mockResolvedValue([CLIENT]);
  listPurchaseOrders.mockResolvedValue([]);
  getInvoice.mockRejectedValue(new Error("no invoices in this suite"));
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("ClientAccruals — carry-forward failure", () => {
  it("offers RETRY on a month WITH its own trips, and retrying refetches the earlier months", async () => {
    let fail = true;
    const carried = trip({ serviceDate: "2025-10-08", status: "Scheduled", tripNumber: "NL-OLD" });
    wireTrips([trip({ tripNumber: "NL-OWN" })], [carried], () => fail);
    renderScreen();

    await screen.findByText(CARRY_NOTE);
    expect(screen.getAllByText("RETRY")).toHaveLength(1);
    // Degraded, not blocked — the same banner-and-degrade pattern as a failed PO fetch.
    // (ActionButton is a span; disabled shows as aria-disabled.)
    expect(screen.getByText("PRINT ACCRUALS REPORT").getAttribute("aria-disabled")).toBeNull();
    expect(screen.getByText("EMAIL TO CLIENT").getAttribute("aria-disabled")).toBeNull();

    fail = false;
    fireEvent.click(screen.getByText("RETRY"));
    await waitFor(() => expect(screen.queryAllByText(CARRY_NOTE)).toHaveLength(0));
    expect(screen.queryByText("RETRY")).toBeNull();
    // The carried row now renders — with its year, since it is from 2025.
    expect(await screen.findByText(/, 2025$/)).toBeTruthy();
  });

  it("offers exactly ONE RETRY on an empty month, keeping the 'retry before sending' wording", async () => {
    wireTrips([], [], () => true);
    renderScreen();

    // The banner note AND the empty-month wording both name the failure.
    expect(await screen.findAllByText(CARRY_NOTE)).toHaveLength(2);
    expect(screen.getAllByText("RETRY")).toHaveLength(1);
    expect(screen.getByText(/Retry before printing or sending it\./)).toBeTruthy();
  });

  it("shows no RETRY when the carry-forward fetch succeeded", async () => {
    wireTrips([trip({ tripNumber: "NL-FINE" })], [], () => false);
    renderScreen();

    await screen.findByText(/NL-FINE/);
    expect(screen.queryAllByText(CARRY_NOTE)).toHaveLength(0);
    expect(screen.queryByText("RETRY")).toBeNull();
  });
});
