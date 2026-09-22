import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import Manifest from "./Manifest";
import { recordCertification } from "@/lib/inspectionStore";
import { today } from "@/lib/data";
import type { LocalCertification } from "@/lib/inspectionStore";

// The boarding gate, as a driver meets it.
//
// Following components/RoleGate.test.tsx's rule: EVERY denial asserts that the denial RENDERS,
// not merely that the action is absent. A disabled button with no reason on screen is a driver
// phoning dispatch, which is the outcome this app exists to avoid — and StatusButton renders
// its `disabledReason` only as a `title` tooltip, which a gloved finger on a touchscreen never
// produces. Hence the banner, and hence these assertions on its text.
//
// THE HIGHEST-VALUE TEST HERE is the badge-scan one. submitScan() is a second route into
// board() that no disabled button covers — a hardware scanner types into the field and sends
// Enter with nothing tapped — so it is the easiest way to ship a gate bypass by accident.

const { enqueue } = vi.hoisted(() => ({
  enqueue: vi.fn<(kind: string, payload: unknown) => Promise<string>>(),
}));

vi.mock("@/lib/sync/queue", () => ({
  enqueue,
  pending: () => [],
  drain: async () => {},
  retry: async () => {},
}));

const passCertification: LocalCertification = {
  commandId: "cccccccc-dddd-eeee-ffff-000000000000",
  mode: "PreTrip",
  // TRP-7401 (the active trip) runs on VEH-11 / NL-01.
  vehicleId: "VEH-11",
  onDate: today,
  certifiedAt: `${today}T06:12:00.000Z`,
  result: "Pass",
  defectCount: 0,
  outOfService: false,
};

const onStartInspection = vi.fn();

beforeEach(() => {
  window.localStorage.clear();
  enqueue.mockReset();
  enqueue.mockResolvedValue("cmd-test");
  onStartInspection.mockReset();
});

afterEach(() => {
  cleanup();
});

function renderManifest() {
  return render(<Manifest tripId={null} onStartInspection={onStartInspection} />);
}

function buttons(name: string): HTMLButtonElement[] {
  return screen.getAllByRole("button", { name }) as HTMLButtonElement[];
}

describe("Manifest with no pre-trip certified", () => {
  it("disables Board and No-show on every row", () => {
    renderManifest();

    const board = buttons("Board");
    const noShow = buttons("No-show");
    expect(board.length).toBeGreaterThan(0);
    expect(noShow.length).toBe(board.length);
    for (const b of [...board, ...noShow]) expect(b.disabled).toBe(true);
  });

  it("renders the reason as a banner, not only as a tooltip", () => {
    renderManifest();
    expect(screen.getByText("Pre-trip inspection required before boarding.")).toBeTruthy();
  });

  it("admits on screen that the server does not enforce this", () => {
    // The admission is the difference between an honest scaffold and a demo that looks
    // authoritative. If somebody reworded it away, this test is what should stop them.
    renderManifest();
    expect(screen.getByText(/the server does not enforce it/)).toBeTruthy();
  });

  it("names the unit and the service day", () => {
    renderManifest();
    const body = screen.getByText(/No pre-trip inspection is certified/);
    expect(body.textContent).toContain("NL-01");
    expect(body.textContent).toContain("2026-09-12");
  });

  it("offers a CTA into the flow, for the mode that is owed", () => {
    renderManifest();
    fireEvent.click(screen.getByRole("button", { name: /Start pre-trip inspection/i }));
    expect(onStartInspection).toHaveBeenCalledWith("PreTrip");
  });

  it("suppresses the 'first boarding starts this trip' note", () => {
    // Two stacked banners bury the one that matters, and a note about what the first boarding
    // does is noise when boarding is impossible.
    renderManifest();
    expect(screen.queryByText("The first boarding starts this trip.")).toBeNull();
  });

  it("boards nobody on a badge scan, and says why in the scan note", () => {
    // THE bypass. A hardware scanner types the code and sends Enter — no button involved.
    renderManifest();

    const field = screen.getByPlaceholderText("Scan or type a badge number");
    fireEvent.change(field, { target: { value: "BDG-40204" } });
    fireEvent.keyDown(field, { key: "Enter" });

    expect(enqueue).not.toHaveBeenCalled();
    expect(screen.getByText(/Pre-trip inspection not certified for NL-01 today\./)).toBeTruthy();
  });

  it("enqueues nothing even if a Board click somehow reaches the handler", () => {
    // Belt and braces for the guard inside board(): the disabled attribute is a UI state, and
    // a write path that relies on it is one refactor away from being open.
    renderManifest();
    fireEvent.click(buttons("Board")[2]);
    expect(enqueue).not.toHaveBeenCalled();
  });
});

describe("Manifest with a Pass pre-trip certified", () => {
  it("enables Board and No-show and drops the banner", () => {
    recordCertification(passCertification);
    renderManifest();

    for (const b of [...buttons("Board"), ...buttons("No-show")]) {
      expect(b.disabled).toBe(false);
    }
    expect(screen.queryByText("Pre-trip inspection required before boarding.")).toBeNull();
  });

  it("boards a passenger through the queue, never through lib/api", () => {
    recordCertification(passCertification);
    renderManifest();

    // MR-6103 (L. Castel) is the first unboarded row.
    fireEvent.click(buttons("Board")[2]);

    expect(enqueue).toHaveBeenCalledWith(
      "manifest.board",
      expect.objectContaining({ tripId: "TRP-7401", boarded: true }),
    );
  });

  it("boards on a badge scan once the gate is open", () => {
    recordCertification(passCertification);
    renderManifest();

    const field = screen.getByPlaceholderText("Scan or type a badge number");
    fireEvent.change(field, { target: { value: "BDG-40204" } });
    fireEvent.keyDown(field, { key: "Enter" });

    expect(enqueue).toHaveBeenCalledWith(
      "manifest.board",
      expect.objectContaining({ manifestRowId: "MR-6103", boarded: true }),
    );
  });

  it("reacts to a certification made while the screen is open", () => {
    // The driver taps the CTA, certifies, and comes back — Console unmounts screens on nav, but
    // a certification arriving from anywhere must reopen the gate without a reload. That is
    // what the useSyncExternalStore subscription buys.
    renderManifest();
    expect(buttons("Board")[0].disabled).toBe(true);

    act(() => {
      recordCertification(passCertification);
    });

    expect(buttons("Board")[0].disabled).toBe(false);
  });
});

describe("Manifest with a failed pre-trip", () => {
  it("stays blocked, offers no CTA, and points at dispatch", () => {
    // Re-inspecting is not the remedy. Offering "start a pre-trip" here would invite a driver
    // to inspect their way past an out-of-service defect.
    recordCertification({
      ...passCertification,
      result: "Fail",
      defectCount: 1,
      outOfService: true,
    });
    renderManifest();

    for (const b of buttons("Board")) expect(b.disabled).toBe(true);
    expect(screen.getByText("NL-01 failed its pre-trip inspection.")).toBeTruthy();
    expect(screen.getByText(/call dispatch/)).toBeTruthy();
    expect(screen.queryByRole("button", { name: /pre-trip inspection$/i })).toBeNull();
  });
});
