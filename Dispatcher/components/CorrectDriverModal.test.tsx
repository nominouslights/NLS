import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import CorrectDriverModal from "./CorrectDriverModal";
import type { TripRecord } from "@/lib/api/trips";

// CORRECT DRIVER dialog: every driver on record minus the current one,
// non-Active drivers labelled, clearance warns but never blocks, a required
// trimmed reason, and server errors shown verbatim. fetch is mocked at the
// transport seam, the same way ChangeRouteModal.test does.

const { getAccessToken, getValidAccessToken, refreshAccessToken } = vi.hoisted(() => ({
  getAccessToken: vi.fn<() => string | null>(),
  getValidAccessToken: vi.fn<() => Promise<string | null>>(),
  refreshAccessToken: vi.fn<() => Promise<string>>(),
}));

vi.mock("@/lib/auth", () => ({ getAccessToken, getValidAccessToken, refreshAccessToken }));

const fetchMock = vi.fn<typeof fetch>();

function json(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

function driver(id: string, name: string, status: "Active" | "Inactive" | "Deactivated") {
  return {
    id,
    name,
    licenceClass: "Class 2",
    status,
    credentialCount: 0,
    soonestCredentialExpiry: null,
  };
}

const DRIVERS = [
  driver("drv-current", "Casey Current", "Active"),
  driver("drv-cleared", "Avery Cleared", "Active"),
  driver("drv-uncleared", "Blake Uncleared", "Active"),
  driver("drv-inactive", "Indy Inactive", "Inactive"),
  driver("drv-gone", "Dale Deactivated", "Deactivated"),
];

const CLEARANCE = { id: "c-1", clientName: "Alamos Gold", expiry: null };

const TRIP = {
  id: "trip-1",
  tripNumber: "TR-0042",
  serviceDate: "2026-10-10",
  windowStart: "06:30:00",
  windowEnd: null,
  serviceType: "ContractCrew",
  routeId: null,
  routeName: "Lynn Lake Run",
  origin: "Lynn Lake",
  destination: "Thompson",
  stops: [],
  distanceKm: 320,
  roundTripKey: null,
  direction: null,
  isEmptyLeg: false,
  clientId: "client-1",
  clientName: "Alamos Gold",
  driverId: "drv-current",
  driverName: "Casey Current",
  vehicleId: null,
  status: "ReadyForBilling",
} as unknown as TripRecord;

let postBodies: unknown[] = [];
let postResponse: () => Response = () => new Response(null, { status: 204 });

beforeEach(() => {
  postBodies = [];
  postResponse = () => new Response(null, { status: 204 });
  getValidAccessToken.mockResolvedValue("access-1");
  getAccessToken.mockReturnValue("access-1");
  fetchMock.mockImplementation(async (input, init) => {
    const url = String(input);
    const method = init?.method ?? "GET";
    if (url === "/api/drivers") return json(200, DRIVERS);
    const m = /^\/api\/drivers\/([^/]+)\/clearances$/.exec(url);
    if (m) return json(200, m[1] === "drv-cleared" ? [CLEARANCE] : []);
    if (url === "/api/trips/trip-1/correct-driver" && method === "POST") {
      postBodies.push(JSON.parse(String(init!.body)));
      return postResponse();
    }
    return json(404, { code: "Test.Unexpected", message: `${method} ${url}` });
  });
  vi.stubGlobal("fetch", fetchMock);
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});

// ActionButton renders a span with a hand-rolled aria-disabled (components/ui/Button.tsx).
const submitButton = () => screen.getByText("CORRECT DRIVER");
const isDisabled = () => submitButton().getAttribute("aria-disabled") === "true";
const option = (name: RegExp) => screen.getByRole("radio", { name });
const reasonBox = () => screen.getByRole("textbox") as HTMLTextAreaElement;

async function renderModal(onCorrected = vi.fn<(driverId: string) => Promise<void>>(async () => undefined), onClose = vi.fn()) {
  render(<CorrectDriverModal trip={TRIP} onClose={onClose} onCorrected={onCorrected} />);
  await screen.findByRole("radio", { name: /Avery Cleared/ });
  return { onCorrected, onClose };
}

describe("CorrectDriverModal", () => {
  it("uses the Correction eyebrow, shows the current driver and the HOS note", async () => {
    await renderModal();
    expect(screen.getByText("Operations · TR-0042 · Correction")).toBeTruthy();
    expect(screen.getByTestId("current-driver").textContent).toBe("Casey Current");
    expect(screen.getByTestId("hos-note").textContent).toContain(
      "Hours-of-service logs and inspection records are not changed — amend hours on the driver's Hours tab.",
    );
  });

  it("lists every other driver on record and excludes the current one", async () => {
    await renderModal();
    const names = screen.getAllByTestId("driver-option").map((o) => o.textContent ?? "");
    expect(names).toHaveLength(4);
    expect(names.some((n) => n.includes("Casey Current"))).toBe(false);
    expect(names.some((n) => n.includes("Indy Inactive"))).toBe(true);
    expect(names.some((n) => n.includes("Dale Deactivated"))).toBe(true);
  });

  it("labels Inactive and Deactivated drivers with text, not colour alone", async () => {
    await renderModal();
    expect(option(/Indy Inactive/).textContent).toContain("Inactive");
    expect(option(/Dale Deactivated/).textContent).toContain("Deactivated");
    // Active drivers carry no status label.
    expect(option(/Avery Cleared/).textContent).not.toMatch(/Inactive|Deactivated|Active/);
  });

  it("keeps submit disabled until both a driver and a non-blank reason are given", async () => {
    await renderModal();
    expect(isDisabled()).toBe(true);

    fireEvent.change(reasonBox(), { target: { value: "   " } });
    fireEvent.click(option(/Avery Cleared/));
    expect(isDisabled()).toBe(true);

    fireEvent.change(reasonBox(), { target: { value: "Driver swap never entered" } });
    expect(isDisabled()).toBe(false);

    cleanup();
    await renderModal();
    fireEvent.change(reasonBox(), { target: { value: "Reason but no driver" } });
    expect(isDisabled()).toBe(true);
  });

  it("counts trimmed characters against the 500 limit and blocks an over-long reason", async () => {
    await renderModal();
    fireEvent.click(option(/Avery Cleared/));
    fireEvent.change(reasonBox(), { target: { value: "  abc  " } });
    expect(screen.getByTestId("reason-counter").textContent).toContain("3 / 500");
    fireEvent.change(reasonBox(), { target: { value: "x".repeat(501) } });
    expect(screen.getByText("Reason is 1 characters too long")).toBeTruthy();
    expect(isDisabled()).toBe(true);
  });

  it("warns when the picked driver lacks the client clearance, but still submits", async () => {
    const { onCorrected } = await renderModal();
    expect(option(/Blake Uncleared/).textContent).toContain("No Alamos Gold clearance");
    expect(option(/Avery Cleared/).textContent).toContain("Alamos Gold clearance");

    fireEvent.click(option(/Blake Uncleared/));
    expect(screen.getByTestId("clearance-warning").textContent).toContain("warning, not a block");
    fireEvent.change(reasonBox(), { target: { value: "Blake drove it" } });
    expect(isDisabled()).toBe(false);
    fireEvent.click(submitButton());
    await waitFor(() => expect(onCorrected).toHaveBeenCalledWith("drv-uncleared"));
  });

  it("POSTs the picked driver with the trimmed reason, then refreshes and closes", async () => {
    const { onCorrected, onClose } = await renderModal();
    fireEvent.click(option(/Indy Inactive/));
    fireEvent.change(reasonBox(), { target: { value: "  Indy covered the shift  \n" } });
    fireEvent.click(submitButton());

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(postBodies).toEqual([{ driverId: "drv-inactive", reason: "Indy covered the shift" }]);
    expect(onCorrected).toHaveBeenCalledWith("drv-inactive");
  });

  it("shows the server's error message verbatim and stays open", async () => {
    postResponse = () =>
      json(409, { code: "Trips.Trip.DriverCorrectionNotAllowed", message: "A driver can't be corrected on this trip." });
    const { onCorrected, onClose } = await renderModal();
    fireEvent.click(option(/Avery Cleared/));
    fireEvent.change(reasonBox(), { target: { value: "Fix it" } });
    fireEvent.click(submitButton());

    await screen.findByText("A driver can't be corrected on this trip.");
    expect(onCorrected).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
    expect(isDisabled()).toBe(false);
  });
});
