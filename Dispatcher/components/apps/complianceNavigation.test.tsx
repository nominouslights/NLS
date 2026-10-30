import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { clearRetainedState } from "@/lib/appState";
import type { Shell } from "./shell";
import type { ScreenId } from "@/lib/nav";

// The two hops into and out of the Compliance screen:
//  - Compliance → Drivers: a click on a driver retains {driverId, HOS tab} in the app's
//    survive-unmount state and navigates; the Drivers screen receives it and clears it.
//  - Dispatch Board tile → Compliance for a compliance role, Drivers for dispatch.

const { getRole } = vi.hoisted(() => ({ getRole: vi.fn<() => string | null>() }));
vi.mock("@/lib/claims", () => ({ getRole }));

vi.mock("@/components/screens/Compliance", () => ({
  default: ({ onOpenDriver }: { onOpenDriver: (id: string) => void }) => (
    <button type="button" onClick={() => onOpenDriver("driver-7")}>
      open driver-7
    </button>
  ),
}));
vi.mock("@/components/screens/Drivers", () => ({
  HOS_TAB_INDEX: 2,
  default: ({ focus, onFocusApplied }: { focus: { driverId: string; tab: number } | null; onFocusApplied: () => void }) => (
    <div>
      <span data-testid="focus">{focus ? `${focus.driverId}@${focus.tab}` : "none"}</span>
      <button type="button" onClick={onFocusApplied}>
        applied
      </button>
    </div>
  ),
}));
vi.mock("@/components/screens/Incidents", () => ({ default: () => null }));
vi.mock("@/components/screens/DispatchBoard", () => ({
  default: ({ onOpenCompliance }: { onOpenCompliance: () => void }) => (
    <button type="button" onClick={onOpenCompliance}>
      tile
    </button>
  ),
}));
vi.mock("@/components/screens/LiveMap", () => ({ default: () => null }));

function shell(navigate: (s: ScreenId) => void): Shell {
  return { openTrip: vi.fn(), createTrip: vi.fn(), importBookeo: null, tripsRefreshKey: 0, navigate, goHome: vi.fn() };
}

afterEach(() => {
  cleanup();
  clearRetainedState();
});

describe("Compliance → Drivers hand-off", () => {
  it("retains the driver and the HOS tab across the screen switch, then clears it", async () => {
    const { default: DriversApp } = await import("./DriversApp");
    const navigate = vi.fn();
    const { rerender } = render(<DriversApp screen="compliance" shell={shell(navigate)} />);

    fireEvent.click(screen.getByText("open driver-7"));
    expect(navigate).toHaveBeenCalledWith("drivers");

    // Console swaps the screen; the app re-renders on Drivers with the hand-off.
    rerender(<DriversApp screen="drivers" shell={shell(navigate)} />);
    expect(screen.getByTestId("focus").textContent).toBe("driver-7@2");

    fireEvent.click(screen.getByText("applied"));
    expect(screen.getByTestId("focus").textContent).toBe("none");
  });
});

describe("Dispatch Board tile target", () => {
  it.each([
    ["ComplianceOfficer", "compliance"],
    ["Owner", "compliance"],
    ["Admin", "compliance"],
    ["Dispatcher", "drivers"],
    ["Supervisor", "drivers"],
  ])("%s lands on %s", async (role, target) => {
    getRole.mockReturnValue(role);
    const { default: TodayApp } = await import("./TodayApp");
    const navigate = vi.fn();
    render(<TodayApp screen="dispatch" shell={shell(navigate)} />);
    fireEvent.click(screen.getByText("tile"));
    expect(navigate).toHaveBeenCalledWith(target);
  });
});
