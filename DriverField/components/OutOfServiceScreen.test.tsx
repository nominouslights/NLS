import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import OutOfServiceScreen, { outOfServiceTitle } from "./OutOfServiceScreen";
import { COMPANY } from "@/lib/company";
import { whenLabel } from "@/lib/hos/display";
import { addDays, type DateOnly, type DutyEvent } from "@/lib/hos/engine";
import { CALENDAR, installLedgerForTests, resetLedgerForTests } from "@/lib/hos/ledger";
import { resetQueueForTests } from "@/lib/sync/queue";

// The full-screen notice while enforcement's declaration stands: "do not drive" with the
// until-instant in local time, the reason verbatim, the dispatch number, and the one way out —
// a read-only Hours where Driving and Personal Conveyance are greyed with the reason.

const H = 3600;

function install(record: { id: string; fromUtc: string; untilUtc: string | null; reason: string }): void {
  const now = Math.floor(Date.now() / 1000);
  const knownFrom = now - 20 * 86400;
  const certifiedDays: DateOnly[] = [];
  for (let d = CALENDAR.dayOf(knownFrom); d < CALENDAR.dayOf(now); d = addDays(d, 1)) certifiedDays.push(d);
  const off = (atUnix: number, sequence: number): DutyEvent => ({ atUnix, status: "OffDuty", source: "DriverApp", sequence, odometerDecikm: null, note: null });
  const events: DutyEvent[] = [off(knownFrom, 1)];
  for (let d = addDays(CALENDAR.dayOf(knownFrom), 1); d < CALENDAR.dayOf(now); d = addDays(d, 1)) events.push(off(CALENDAR.dayStartUnix(d), events.length + 1));
  events.push({ atUnix: now - H, status: "OnDuty", source: "DriverApp", sequence: events.length + 1, odometerDecikm: null, note: null });
  installLedgerForTests({ knownFromUnix: knownFrom, events, certifiedDays, asOfUnix: now, status: { outOfService: record } });
}

beforeEach(async () => {
  resetLedgerForTests();
  await resetQueueForTests({ wipe: true });
});

afterEach(() => {
  cleanup();
});

describe("OutOfServiceScreen", () => {
  const now = Math.floor(Date.now() / 1000);
  const record = {
    id: "oos-1",
    fromUtc: new Date((now - 2 * H) * 1000).toISOString(),
    untilUtc: new Date((now + 70 * H) * 1000).toISOString(),
    reason: "Logbook not produced at roadside check — MPI officer 4471",
  };

  it("says do not drive until the local instant, the reason, and the dispatch number", () => {
    install(record);
    render(<OutOfServiceScreen record={record} />);

    const until = whenLabel(now + 70 * H);
    expect(outOfServiceTitle(record)).toBe(`Out of service until ${until} — do not drive`);
    expect(screen.getByText(`Out of service until ${until} — do not drive`)).toBeTruthy();
    expect(screen.getByText(record.reason)).toBeTruthy();
    expect(screen.getByRole("button", { name: `Call dispatch ${COMPANY.dispatchPhone}` })).toBeTruthy();
    expect(screen.getByText(/Driving and Personal Conveyance are unavailable/)).toBeTruthy();
    expect(outOfServiceTitle({ ...record, untilUtc: null })).toBe("Out of service until lifted — do not drive");
  });

  it("View hours shows a read-only Hours whose duty control offers Off/On Duty only, with the reason on screen", () => {
    install(record);
    render(<OutOfServiceScreen record={record} />);

    fireEvent.click(screen.getByRole("button", { name: "View hours" }));

    expect(screen.getByText("Hours of service")).toBeTruthy();
    expect((screen.getByRole("button", { name: /Driving/ }) as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByRole("button", { name: /Personal Conveyance/ }) as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByRole("button", { name: /Off Duty/ }) as HTMLButtonElement).disabled).toBe(false);
    expect((screen.getByRole("button", { name: /On Duty/ }) as HTMLButtonElement).disabled).toBe(false);
    expect(screen.getByText(/Driving unavailable/)).toBeTruthy();
    expect(screen.getByText(/Personal Conveyance unavailable/)).toBeTruthy();
    // Read-only: none of the compliance actions, no certification.
    expect(screen.queryByRole("button", { name: "Record exception" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Report ELD malfunction" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Other carrier hours" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Certify today" })).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Back to the notice" }));
    expect(screen.getByRole("button", { name: "View hours" })).toBeTruthy();
  });
});
