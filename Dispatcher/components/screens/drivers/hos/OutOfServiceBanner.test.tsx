import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { statusMeta } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import { OutOfServiceBanner, outOfServiceBannerText } from "./OutOfServiceBanner";
import RecordOutOfServiceModal, { DEFAULT_OOS_HOURS, LiftOutOfServiceModal, outOfServiceInput } from "./RecordOutOfServiceModal";
import { outOfService } from "./hosFixtures";

// The out-of-service banner and its two modals. The banner's sentence is the one the
// task pins ("Out of service until {local} — {reason}"), vermillion with its own ✕; LIFT is
// compliance-only. The record modal shows the server's 409 verbatim.

const { recordOutOfService, liftOutOfService } = vi.hoisted(() => ({ recordOutOfService: vi.fn(), liftOutOfService: vi.fn() }));
vi.mock("@/lib/api/hos", () => ({ recordOutOfService, liftOutOfService }));

beforeEach(() => {
  recordOutOfService.mockReset();
  liftOutOfService.mockReset();
});

afterEach(cleanup);

describe("OutOfServiceBanner", () => {
  it("reads 'Out of service until {local} — {reason}' with ✕ in the vermillion kind", () => {
    const oos = outOfService();
    expect(outOfServiceBannerText(oos)).toBe("Out of service until Oct 12, 06:00 — Roadside inspection — hours exceeded");
    expect(outOfServiceBannerText(outOfService({ untilUtc: null }))).toBe("Out of service until lifted — Roadside inspection — hours exceeded");

    render(<OutOfServiceBanner outOfService={oos} canCompliance={false} onLift={vi.fn()} />);
    const chip = screen.getByText(outOfServiceBannerText(oos));
    expect(chip.parentElement?.textContent).toContain("✕");
    expect(chip.parentElement?.textContent).not.toContain(statusMeta("over").g);
  });

  it("offers LIFT to compliance roles only", () => {
    const onLift = vi.fn();
    const { unmount } = render(<OutOfServiceBanner outOfService={outOfService()} canCompliance={false} onLift={onLift} />);
    expect(screen.queryByText("LIFT")).toBeNull();
    unmount();

    render(<OutOfServiceBanner outOfService={outOfService()} canCompliance onLift={onLift} />);
    fireEvent.click(screen.getByText("LIFT"));
    expect(onLift).toHaveBeenCalled();
  });
});

describe("RecordOutOfServiceModal", () => {
  it("builds a 72h body by default, or open-ended with no untilUtc", () => {
    const declared = Date.UTC(2026, 9, 9, 11) / 1000;
    expect(DEFAULT_OOS_HOURS).toBe(72);
    expect(outOfServiceInput(declared, 72, false, " Inspection ", "")).toEqual({
      declaredAtUtc: "2026-10-09T11:00:00.000Z",
      reason: "Inspection",
      untilUtc: "2026-10-12T11:00:00.000Z",
      evidence: null,
    });
    expect(outOfServiceInput(declared, 72, true, "Inspection", "Report 4411")).toEqual({
      declaredAtUtc: "2026-10-09T11:00:00.000Z",
      reason: "Inspection",
      openEnded: true,
      evidence: "Report 4411",
    });
  });

  it("defaults the period to 72 hours, requires a reason, and posts the body", async () => {
    const onSaved = vi.fn();
    render(<RecordOutOfServiceModal driverId="driver-1" driverName="K. Beardy" onClose={vi.fn()} onSaved={onSaved} />);
    const hours = screen.getByPlaceholderText; // keep the helper referenced for clarity below
    void hours;
    expect((document.querySelector('input[type="number"]') as HTMLInputElement).value).toBe("72");

    fireEvent.click(screen.getByText("RECORD OUT OF SERVICE"));
    expect(recordOutOfService).not.toHaveBeenCalled();
    expect(screen.getByText(/Enter the reason/)).toBeTruthy();

    fireEvent.change(screen.getByPlaceholderText(/Roadside inspection/), { target: { value: "Hours exceeded" } });
    recordOutOfService.mockResolvedValueOnce({ id: "oos-9" });
    fireEvent.click(screen.getByText("RECORD OUT OF SERVICE"));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith({ id: "oos-9" }));
    const [driverId, body] = recordOutOfService.mock.calls[0];
    expect(driverId).toBe("driver-1");
    expect(body.reason).toBe("Hours exceeded");
    expect(body.evidence).toBeNull();
    expect(Date.parse(body.untilUtc) - Date.parse(body.declaredAtUtc)).toBe(72 * 3600 * 1000);
  });

  it("shows the 409 Drivers.Hos.AlreadyOutOfService sentence verbatim", async () => {
    render(<RecordOutOfServiceModal driverId="driver-1" driverName="K. Beardy" onClose={vi.fn()} onSaved={vi.fn()} />);
    fireEvent.change(screen.getByPlaceholderText(/Roadside inspection/), { target: { value: "Hours exceeded" } });
    const message = "This driver already has an active out-of-service declaration; lift it before recording another.";
    recordOutOfService.mockRejectedValueOnce(new ApiError("Drivers.Hos.AlreadyOutOfService", message, 409));

    fireEvent.click(screen.getByText("RECORD OUT OF SERVICE"));

    await waitFor(() => expect(screen.getByText(message)).toBeTruthy());
  });
});

describe("LiftOutOfServiceModal", () => {
  it("posts the lift with the reason (null when blank) and shows the early-lift refusal verbatim", async () => {
    const onSaved = vi.fn();
    render(<LiftOutOfServiceModal driverId="driver-1" driverName="K. Beardy" recordId="oos-1" onClose={vi.fn()} onSaved={onSaved} />);
    const message = "Lifting an out-of-service declaration early requires a reason.";
    liftOutOfService.mockRejectedValueOnce(new ApiError("Drivers.Hos.LiftReasonRequired", message, 400));

    fireEvent.click(screen.getByText("LIFT"));
    await waitFor(() => expect(screen.getByText(message)).toBeTruthy());
    expect(liftOutOfService).toHaveBeenCalledWith("driver-1", "oos-1", null);

    fireEvent.change(screen.getByPlaceholderText(/Required when lifting/), { target: { value: "Records produced" } });
    liftOutOfService.mockResolvedValueOnce({ id: "oos-1", status: "Lifted" });
    fireEvent.click(screen.getByText("LIFT"));
    await waitFor(() => expect(onSaved).toHaveBeenCalled());
    expect(liftOutOfService).toHaveBeenLastCalledWith("driver-1", "oos-1", "Records produced");
  });
});
