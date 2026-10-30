import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { statusMeta } from "@/lib/theme";
import { PRIOR_RECORDS_MISSING_TEXT, PriorRecordsPanel } from "./PriorRecordsPanel";
import { hosStatus } from "./hosFixtures";

// The prior-records attestation: a driver with neither an attestation nor 14 days of their
// own ledger reads as "not dispatchable" in the caution kind with ◐; an attestation reads as
// ✓ with its details; ATTEST is compliance-only and posts the exact body.

const { attestPriorRecords } = vi.hoisted(() => ({ attestPriorRecords: vi.fn() }));
vi.mock("@/lib/api/hos", () => ({ attestPriorRecords }));

const TODAY = "2026-10-09";

beforeEach(() => attestPriorRecords.mockReset());
afterEach(cleanup);

describe("PriorRecordsPanel", () => {
  it("reads no attestation + thin ledger as 'Prior 14 days not on file — not dispatchable' in gold with ◐", () => {
    render(<PriorRecordsPanel driverId="driver-1" driverName="K. Beardy" today={TODAY} status={hosStatus({ recordsOnFile: false })} canCompliance={false} onChanged={vi.fn()} />);
    const chip = screen.getByText(PRIOR_RECORDS_MISSING_TEXT);
    expect(chip.parentElement?.textContent).toContain(statusMeta("soon").g);
    expect(screen.queryByText("ATTEST")).toBeNull();
  });

  it("reads no attestation + a full ledger as the neutral note, not a warning", () => {
    render(<PriorRecordsPanel driverId="driver-1" driverName="K. Beardy" today={TODAY} status={hosStatus()} canCompliance={false} onChanged={vi.fn()} />);
    expect(screen.queryByText(PRIOR_RECORDS_MISSING_TEXT)).toBeNull();
    expect(screen.getByText(/own ledger covers the 14-day window/)).toBeTruthy();
  });

  it("shows an attestation as ✓ with covered-through, attested at/by, and the other-carriers flag", () => {
    const status = hosStatus({
      priorRecords: { coveredThroughDate: "2026-09-25", attestedAtUtc: "2026-10-01T15:00:00Z", attestedByUserId: "abcdef12-0000-0000-0000-000000000000", note: "From Thompson Bus Lines", worksForOtherCarriers: true },
    });
    render(<PriorRecordsPanel driverId="driver-1" driverName="K. Beardy" today={TODAY} status={status} canCompliance onChanged={vi.fn()} />);

    const chip = screen.getByText("Prior records attested through Fri, Sep 25");
    expect(chip.parentElement?.textContent).toContain(statusMeta("ontime").g);
    expect(screen.getByText(/Attested Oct 1, 10:00 by user abcdef12/)).toBeTruthy();
    expect(screen.getByText(/From Thompson Bus Lines/)).toBeTruthy();
    expect(screen.getByText(/Works for other carriers/)).toBeTruthy();
    expect(screen.getByText("RE-ATTEST")).toBeTruthy();
  });

  it("ATTEST (compliance only) posts the covered-through date, note and other-carriers flag", async () => {
    const onChanged = vi.fn();
    render(<PriorRecordsPanel driverId="driver-1" driverName="K. Beardy" today={TODAY} status={hosStatus({ recordsOnFile: false })} canCompliance onChanged={onChanged} />);

    fireEvent.click(screen.getByText("ATTEST"));
    const date = document.querySelector('input[type="date"]') as HTMLInputElement;
    expect(date.value).toBe(TODAY);
    fireEvent.change(date, { target: { value: "2026-10-01" } });
    fireEvent.click(document.querySelector('input[type="checkbox"]') as HTMLInputElement);
    fireEvent.change(screen.getByPlaceholderText("Where the records came from"), { target: { value: "Faxed by the previous carrier" } });
    attestPriorRecords.mockResolvedValueOnce({ coveredThroughDate: "2026-10-01" });

    fireEvent.click(screen.getAllByText("ATTEST")[1]);

    await waitFor(() => expect(onChanged).toHaveBeenCalled());
    expect(attestPriorRecords).toHaveBeenCalledWith("driver-1", { coveredThroughDate: "2026-10-01", note: "Faxed by the previous carrier", worksForOtherCarriers: true });
  });
});
