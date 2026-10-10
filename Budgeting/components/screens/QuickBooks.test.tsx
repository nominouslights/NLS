import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import QuickBooks, { type QuickBooksApi } from "./QuickBooks";
import { ApiError } from "@/lib/api/transport";
import type { QboConnection } from "@/lib/api/qbo";

// The QuickBooks screen takes its requests as an `api` prop (as BudgetItemFormModal does), so
// these inject vi.fn()s. The wire itself is pinned in lib/api/qbo.requests.test.ts.

const NOW = new Date("2026-10-08T12:00:00Z");
const DAY = 24 * 60 * 60 * 1000;
const inDays = (d: number) => new Date(NOW.getTime() + d * DAY).toISOString();

const NOT_CONNECTED: QboConnection = {
  status: "NotConnected",
  realmId: null,
  companyName: null,
  environment: null,
  connectedBy: null,
  connectedByName: null,
  connectedByEmail: null,
  connectedAtUtc: null,
  refreshTokenExpiresAtUtc: null,
  lastSyncAtUtc: null,
  lastErrorCode: null,
};

const ACTIVE: QboConnection = {
  status: "Active",
  realmId: "9341452938471234",
  companyName: "Northern Link Sandbox Co",
  environment: "Sandbox",
  connectedBy: "5f2b1e1c-0000-4000-8000-000000000009",
  connectedByName: "Ada Planner",
  connectedByEmail: "ada@example.com",
  connectedAtUtc: "2026-10-01T15:00:00+00:00",
  refreshTokenExpiresAtUtc: inDays(90),
  lastSyncAtUtc: null,
  lastErrorCode: null,
};

function makeApi(connection: QboConnection, overrides: Partial<QuickBooksApi> = {}): QuickBooksApi {
  return {
    getConnection: vi.fn<QuickBooksApi["getConnection"]>().mockResolvedValue(connection),
    authorize: vi.fn<QuickBooksApi["authorize"]>().mockResolvedValue("https://appcenter.intuit.com/connect/oauth2?x=1"),
    disconnect: vi.fn<QuickBooksApi["disconnect"]>().mockResolvedValue(undefined),
    ...overrides,
  };
}

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("QuickBooks screen — status chip", () => {
  it.each([
    ["Active", "Connected", "✓"],
    ["NeedsReconnect", "Reconnect needed", "!"],
    ["Disconnected", "Not connected", "—"],
    ["NotConnected", "Not connected", "—"],
  ] as const)("%s reads %s with its glyph beside it", async (status, label, glyph) => {
    const c = status === "NotConnected" ? NOT_CONNECTED : { ...ACTIVE, status };
    render(<QuickBooks api={makeApi(c)} now={NOW} navigate={vi.fn()} />);

    const chipLabel = await screen.findByText(label);
    // The chip is glyph tile + text in one span: colour never stands alone.
    expect(chipLabel.textContent).toBe(`${glyph}${label}`);
  });

  it("marks a Sandbox company clearly", async () => {
    render(<QuickBooks api={makeApi(ACTIVE)} now={NOW} navigate={vi.fn()} />);

    expect(await screen.findByText("SANDBOX")).toBeTruthy();
    expect(screen.getByText("Test company — not real books")).toBeTruthy();
    expect(screen.getByText("Northern Link Sandbox Co")).toBeTruthy();
    expect(screen.getByText("Ada Planner")).toBeTruthy();
  });

  it("falls back to the email when the connecting person has no name", async () => {
    render(
      <QuickBooks api={makeApi({ ...ACTIVE, connectedByName: null })} now={NOW} navigate={vi.fn()} />,
    );

    expect(await screen.findByText("ada@example.com")).toBeTruthy();
  });

  it("shows no MOCK tag — the connection is real", async () => {
    render(<QuickBooks api={makeApi(ACTIVE)} now={NOW} navigate={vi.fn()} />);
    await screen.findByText("Connected");

    expect(screen.queryByText("MOCK")).toBeNull();
  });
});

describe("QuickBooks screen — reconnect warning", () => {
  it("warns when the refresh token expires in under 14 days", async () => {
    render(
      <QuickBooks api={makeApi({ ...ACTIVE, refreshTokenExpiresAtUtc: inDays(13) })} now={NOW} navigate={vi.fn()} />,
    );

    expect(await screen.findByText(/^Reconnect by /)).toBeTruthy();
  });

  it("does not warn at exactly 14 days", async () => {
    render(
      <QuickBooks api={makeApi({ ...ACTIVE, refreshTokenExpiresAtUtc: inDays(14) })} now={NOW} navigate={vi.fn()} />,
    );
    await screen.findByText("Connected");

    expect(screen.queryByText(/Reconnect by /)).toBeNull();
  });

  it("does not warn far from expiry", async () => {
    render(<QuickBooks api={makeApi(ACTIVE)} now={NOW} navigate={vi.fn()} />);
    await screen.findByText("Connected");

    expect(screen.queryByText(/Reconnect by /)).toBeNull();
  });
});

describe("QuickBooks screen — actions", () => {
  it("CONNECT asks for the authorize URL and sends the browser there", async () => {
    const api = makeApi(NOT_CONNECTED);
    const navigate = vi.fn();
    render(<QuickBooks api={api} now={NOW} navigate={navigate} />);

    fireEvent.click(await screen.findByText("CONNECT"));

    await waitFor(() => expect(navigate).toHaveBeenCalledWith("https://appcenter.intuit.com/connect/oauth2?x=1"));
    expect(api.authorize).toHaveBeenCalledTimes(1);
  });

  it("offers RECONNECT on a connection that needs it", async () => {
    const api = makeApi({ ...ACTIVE, status: "NeedsReconnect", lastErrorCode: "invalid_grant" });
    const navigate = vi.fn();
    render(<QuickBooks api={api} now={NOW} navigate={navigate} />);

    fireEvent.click(await screen.findByText("RECONNECT"));

    await waitFor(() => expect(navigate).toHaveBeenCalledTimes(1));
  });

  it("shows 503 NotConfigured verbatim and does not navigate", async () => {
    const message =
      "QuickBooks Online is not set up on this server yet. Ask the platform administrator to add the Intuit app credentials.";
    const api = makeApi(NOT_CONNECTED, {
      authorize: vi.fn<QuickBooksApi["authorize"]>().mockRejectedValue(
        new ApiError("Budgeting.Qbo.NotConfigured", message, 503),
      ),
    });
    const navigate = vi.fn();
    render(<QuickBooks api={api} now={NOW} navigate={navigate} />);

    fireEvent.click(await screen.findByText("CONNECT"));

    expect(await screen.findByText(message)).toBeTruthy();
    expect(screen.getByText("Budgeting.Qbo.NotConfigured")).toBeTruthy();
    expect(navigate).not.toHaveBeenCalled();
  });

  it("DISCONNECT needs two clicks, then refetches", async () => {
    const getConnection = vi
      .fn<QuickBooksApi["getConnection"]>()
      .mockResolvedValueOnce(ACTIVE)
      .mockResolvedValueOnce({ ...ACTIVE, status: "Disconnected", refreshTokenExpiresAtUtc: null });
    const api = makeApi(ACTIVE, { getConnection });
    render(<QuickBooks api={api} now={NOW} navigate={vi.fn()} />);

    fireEvent.click(await screen.findByText("DISCONNECT"));
    expect(api.disconnect).not.toHaveBeenCalled();
    expect(screen.getByText(/Click CONFIRM DISCONNECT to proceed/)).toBeTruthy();

    fireEvent.click(screen.getByText("CONFIRM DISCONNECT"));

    await waitFor(() => expect(api.disconnect).toHaveBeenCalledTimes(1));
    expect(await screen.findByText("Not connected")).toBeTruthy();
    expect(getConnection).toHaveBeenCalledTimes(2);
    expect(screen.queryByText("DISCONNECT")).toBeNull();
  });

  it("CANCEL backs out of the disconnect confirm", async () => {
    const api = makeApi(ACTIVE);
    render(<QuickBooks api={api} now={NOW} navigate={vi.fn()} />);

    fireEvent.click(await screen.findByText("DISCONNECT"));
    fireEvent.click(screen.getByText("CANCEL"));

    expect(screen.getByText("DISCONNECT")).toBeTruthy();
    expect(api.disconnect).not.toHaveBeenCalled();
  });

  it("offers no DISCONNECT when nothing is connected", async () => {
    render(<QuickBooks api={makeApi(NOT_CONNECTED)} now={NOW} navigate={vi.fn()} />);
    await screen.findByText("CONNECT");

    expect(screen.queryByText("DISCONNECT")).toBeNull();
  });

  it("shows a failed load verbatim with RETRY", async () => {
    const getConnection = vi
      .fn<QuickBooksApi["getConnection"]>()
      .mockRejectedValueOnce(new ApiError("Network.Unreachable", "Cannot reach the API at . Is the backend running?", 0))
      .mockResolvedValueOnce(ACTIVE);
    render(<QuickBooks api={makeApi(ACTIVE, { getConnection })} now={NOW} navigate={vi.fn()} />);

    expect(await screen.findByText("Cannot reach the API at . Is the backend running?")).toBeTruthy();
    fireEvent.click(screen.getByText("RETRY"));

    expect(await screen.findByText("Connected")).toBeTruthy();
  });
});
