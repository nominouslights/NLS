import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { ReactNode } from "react";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import QboCallback from "./QboCallback";
import { LANDING_STORAGE_KEY } from "@/lib/landing";

// The QuickBooks OAuth callback page: it strips the query, posts Intuit's three values through
// the real request layer (only fetch and lib/auth are stubbed, so the route and body asserted
// here are what goes on the wire), handles a declined consent without posting, and shows a
// refusal in the server's own words.
//
// AuthGate is replaced by a pass-through: its session restore and RoleGate have their own
// tests, and this page's job starts once they let it render.

const { getAccessToken, getValidAccessToken, refreshAccessToken, replace } = vi.hoisted(() => ({
  getAccessToken: vi.fn<() => string | null>(),
  getValidAccessToken: vi.fn<() => Promise<string | null>>(),
  refreshAccessToken: vi.fn<() => Promise<string>>(),
  replace: vi.fn<(href: string) => void>(),
}));

vi.mock("@/lib/auth", () => ({ getAccessToken, getValidAccessToken, refreshAccessToken }));
vi.mock("@/components/AuthGate", () => ({
  default: ({ children }: { children?: ReactNode }) => <>{children}</>,
}));
vi.mock("next/navigation", () => ({ useRouter: () => ({ replace }) }));

const fetchMock = vi.fn<typeof fetch>();

function arriveAt(url: string) {
  window.history.replaceState(null, "", url);
}

beforeEach(() => {
  vi.stubGlobal("fetch", fetchMock);
  getValidAccessToken.mockResolvedValue("access-1");
  getAccessToken.mockReturnValue("access-1");
  sessionStorage.clear();
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.clearAllMocks();
  arriveAt("/");
});

describe("QboCallback", () => {
  it("strips the query and POSTs { code, state, realmId } to complete, then goes to the root", async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));
    arriveAt("/qbo/callback?code=c-1&state=s-1&realmId=9341452938471234");

    render(<QboCallback />);

    // Stripped before the POST settles — the code never outlives the first effect.
    expect(window.location.search).toBe("");
    expect(window.location.pathname).toBe("/qbo/callback");

    await waitFor(() => expect(replace).toHaveBeenCalledWith("/"));
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(String(fetchMock.mock.calls[0][0])).toBe("/api/budgeting/qbo/connection/complete");
    const init = fetchMock.mock.calls[0][1] as RequestInit;
    expect(init.method).toBe("POST");
    expect(JSON.parse(String(init.body))).toEqual({
      code: "c-1",
      state: "s-1",
      realmId: "9341452938471234",
    });
    // The console opens on the QuickBooks screen.
    expect(sessionStorage.getItem(LANDING_STORAGE_KEY)).toBe("qbo");
    expect(screen.getByText("QuickBooks is connected. Opening the console…")).toBeTruthy();
  });

  it("handles access_denied without posting anything", async () => {
    arriveAt("/qbo/callback?error=access_denied&state=s-1");

    render(<QboCallback />);

    expect(await screen.findByText("Access was declined in QuickBooks, so nothing was connected.")).toBeTruthy();
    expect(window.location.search).toBe("");
    expect(fetchMock).not.toHaveBeenCalled();
    expect(replace).not.toHaveBeenCalled();
    expect(screen.getByText("Back to the QuickBooks screen")).toBeTruthy();
  });

  it("shows the server's refusal verbatim, with its code and a link back, and stays put", async () => {
    const message =
      "This QuickBooks sign-in link is not valid for you, or it has already been used. Start the connection again.";
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ code: "Budgeting.Qbo.StateInvalid", message }), {
        status: 400,
        headers: { "Content-Type": "application/json" },
      }),
    );
    arriveAt("/qbo/callback?code=c-1&state=stale&realmId=9341452938471234");

    render(<QboCallback />);

    expect(await screen.findByText(message)).toBeTruthy();
    expect(screen.getByText("Budgeting.Qbo.StateInvalid")).toBeTruthy();
    expect(screen.getByText("Back to the QuickBooks screen")).toBeTruthy();
    expect(replace).not.toHaveBeenCalled();
    expect(sessionStorage.getItem(LANDING_STORAGE_KEY)).toBe("qbo");
  });

  it("posts a callback with missing values anyway, leaving the server to name what is missing", async () => {
    const message =
      "QuickBooks did not send back everything needed to finish connecting. Start the connection again.";
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ code: "Budgeting.Qbo.CallbackIncomplete", message }), {
        status: 400,
        headers: { "Content-Type": "application/json" },
      }),
    );
    arriveAt("/qbo/callback");

    render(<QboCallback />);

    expect(await screen.findByText(message)).toBeTruthy();
    const init = fetchMock.mock.calls[0][1] as RequestInit;
    expect(JSON.parse(String(init.body))).toEqual({ code: null, state: null, realmId: null });
  });
});
