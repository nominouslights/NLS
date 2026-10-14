import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import TopBar from "./TopBar";
import { eldMalfunctionPayload } from "@/lib/hos/complianceCommand";
import { addDays, type DateOnly, type DutyEvent } from "@/lib/hos/engine";
import { CALENDAR, installLedgerForTests, resetLedgerForTests, syncWithQueue } from "@/lib/hos/ledger";
import { enqueue, resetQueueForTests } from "@/lib/sync/queue";

// The two compliance chips beside the duty chip: ✕ "Out of service" while a declaration stands,
// ✎ "Paper-log mode" while an ELD malfunction is open on the server OR a report is queued here.

const H = 3600;

function install(status: Parameters<typeof installLedgerForTests>[0]["status"] = {}): number {
  const now = Math.floor(Date.now() / 1000);
  const knownFrom = now - 20 * 86400;
  const certifiedDays: DateOnly[] = [];
  for (let d = CALENDAR.dayOf(knownFrom); d < CALENDAR.dayOf(now); d = addDays(d, 1)) certifiedDays.push(d);
  const off = (atUnix: number, sequence: number): DutyEvent => ({ atUnix, status: "OffDuty", source: "DriverApp", sequence, odometerDecikm: null, note: null });
  const events: DutyEvent[] = [off(knownFrom, 1)];
  for (let d = addDays(CALENDAR.dayOf(knownFrom), 1); d < CALENDAR.dayOf(now); d = addDays(d, 1)) events.push(off(CALENDAR.dayStartUnix(d), events.length + 1));
  installLedgerForTests({ knownFromUnix: knownFrom, events, certifiedDays, asOfUnix: now, status });
  return now;
}

beforeEach(async () => {
  resetLedgerForTests();
  await resetQueueForTests({ wipe: true });
});

afterEach(() => {
  cleanup();
});

describe("TopBar compliance chips", () => {
  it("shows neither chip on a quiet status", () => {
    install();
    render(<TopBar />);
    expect(screen.queryByText("Out of service")).toBeNull();
    expect(screen.queryByText("Paper-log mode")).toBeNull();
    expect(screen.getByText("Off Duty")).toBeTruthy();
  });

  it("shows the ✕ Out of service chip while the declaration is in force, and not once it has run out", () => {
    const now = install({ outOfService: { id: "oos-1", fromUtc: new Date((now0() - H) * 1000).toISOString(), untilUtc: new Date((now0() + H) * 1000).toISOString(), reason: "r" } });
    expect(now).toBeGreaterThan(0);
    render(<TopBar />);
    expect(screen.getByText("Out of service")).toBeTruthy();
    cleanup();

    install({ outOfService: { id: "oos-2", fromUtc: new Date((now0() - 3 * H) * 1000).toISOString(), untilUtc: new Date((now0() - H) * 1000).toISOString(), reason: "r" } });
    render(<TopBar />);
    expect(screen.queryByText("Out of service")).toBeNull();
  });

  it("shows the ✎ Paper-log mode chip while the server says an ELD malfunction is open", () => {
    install({ eldMalfunctionOpen: true });
    render(<TopBar />);
    expect(screen.getByText("Paper-log mode")).toBeTruthy();
    expect(screen.getByText("✎")).toBeTruthy();
  });

  it("shows the chip as soon as a report is queued on this tablet, before the server has it", async () => {
    const now = install();
    render(<TopBar />);
    expect(screen.queryByText("Paper-log mode")).toBeNull();
    await enqueue("hos.eld-malfunction", eldMalfunctionPayload({ code: "P", noticedAtUnix: now - 60, description: "Frozen" }, now));
    syncWithQueue();
    expect(await screen.findByText("Paper-log mode")).toBeTruthy();
  });
});

function now0(): number {
  return Math.floor(Date.now() / 1000);
}
