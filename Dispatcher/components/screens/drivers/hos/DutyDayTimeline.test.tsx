import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { statusMeta } from "@/lib/theme";
import { HosMessages } from "@/lib/hos/engine";
import { SOURCE_GLYPH } from "@/lib/hos/display";
import { DutyDayTimeline } from "./DutyDayTimeline";
import { dutyDay, dutyEvent, FIXTURE_DATE, FIXTURE_DAY_START, hosException, outOfService } from "./hosFixtures";

describe("DutyDayTimeline — exceptions and out of service on the day", () => {
  it("shows the day's exceptions with their status glyph, and an overlapping out-of-service span with ✕", () => {
    render(
      <DutyDayTimeline
        days={[
          dutyDay({
            exceptions: [hosException(), hosException({ id: "ex-2", kind: "Off-duty deferral (CO approved)", status: "Approved", extraDrivingMinutes: null, deferredOffDutyMinutes: 90 })],
            outOfService: outOfService(),
          }),
        ]}
        today={FIXTURE_DATE}
        onAmend={vi.fn()}
      />,
    );

    const adverse = screen.getByText("Adverse driving conditions · Declared · +60 min");
    expect(adverse.parentElement?.textContent).toContain("◐");
    const deferral = screen.getByText("Off-duty deferral · Approved · 90 min deferred");
    expect(deferral.parentElement?.textContent).toContain(statusMeta("ontime").g);
    const oos = screen.getByText("Out of service Oct 9, 06:00 – Oct 12, 06:00 — Roadside inspection — hours exceeded");
    expect(oos.parentElement?.textContent).toContain("✕");
  });
});

// The timeline is one row per server-evaluated day. These pin the three certification
// states (each a different glyph, not just a colour), the per-source glyphs, that a voided
// event is struck through and carries the amendment reason from its replacement, and that a
// violation sentence is the server's verbatim.

afterEach(cleanup);

const TODAY = "2026-10-10";

describe("DutyDayTimeline — certification", () => {
  it("shows ✓ Certified with the local time, ◷ Not certified, and ▲ Recertification required", () => {
    const certifiedAt = new Date((FIXTURE_DAY_START + 15 * 3600) * 1000).toISOString();
    render(
      <DutyDayTimeline
        today={TODAY}
        onAmend={() => {}}
        days={[
          dutyDay({ date: "2026-10-07", certified: true, certifiedAtUtc: certifiedAt }),
          dutyDay({ date: "2026-10-08" }),
          dutyDay({ date: FIXTURE_DATE, certified: true, recertificationRequired: true }),
        ]}
      />,
    );

    const certified = screen.getByText("Certified 15:00");
    expect(certified.parentElement?.textContent).toContain(statusMeta("ontime").g);
    const not = screen.getByText("Not certified");
    expect(not.parentElement?.textContent).toContain("◷");
    const recert = screen.getByText("Recertification required");
    expect(recert.parentElement?.textContent).toContain(statusMeta("over").g);
  });

  it("marks a day with no records as such instead of drawing an empty day", () => {
    render(<DutyDayTimeline today={TODAY} onAmend={() => {}} days={[dutyDay({ hasRecords: false, events: [], segments: [] })]} />);
    expect(screen.getByText("No records")).toBeTruthy();
    expect(screen.queryByText(/Not certified/)).toBeNull();
  });
});

describe("DutyDayTimeline — sources and totals", () => {
  it("badges every source that fed the day with its own glyph", () => {
    const day = dutyDay({
      events: [
        dutyEvent({ eventId: "a", source: "Driver App" }),
        dutyEvent({ eventId: "b", sequence: 2, source: "ELD transcription" }),
        dutyEvent({ eventId: "c", sequence: 3, source: "Manual (paper backup)" }),
        dutyEvent({ eventId: "d", sequence: 4, source: "Other carrier (declared)" }),
        dutyEvent({ eventId: "e", sequence: 5, source: "System" }),
      ],
    });
    render(<DutyDayTimeline today={TODAY} onAmend={() => {}} days={[day]} />);

    for (const [source, glyph] of [
      ["Driver App", SOURCE_GLYPH.DriverApp],
      ["ELD transcription", SOURCE_GLYPH.EldTranscription],
      ["Manual (paper backup)", SOURCE_GLYPH.PaperLog],
      ["Other carrier (declared)", SOURCE_GLYPH.OtherCarrier],
      ["System", SOURCE_GLYPH.System],
    ] as const) {
      const chip = screen.getByText(source);
      expect(chip.parentElement?.textContent, source).toContain(glyph);
    }
    expect(new Set([SOURCE_GLYPH.DriverApp, SOURCE_GLYPH.EldTranscription, SOURCE_GLYPH.PaperLog, SOURCE_GLYPH.OtherCarrier, SOURCE_GLYPH.System]).size).toBe(5);
  });

  it("shows the engine's totals and the legend with every duty glyph", () => {
    render(<DutyDayTimeline today={TODAY} onAmend={() => {}} days={[dutyDay({ personalConveyanceKm: 12.5 })]} />);
    expect(screen.getByText(/Drv 5h 00m · On 7h 00m · Off 17h 00m · PC 12\.5 km/)).toBeTruthy();
    expect(screen.getByText("Personal Conveyance")).toBeTruthy();
  });
});

describe("DutyDayTimeline — violations and events", () => {
  it("shows the server's violation sentence verbatim as an over chip", () => {
    const message = HosMessages.for("ShiftDriving13h", 13 * 3600 + 600, 13 * 3600);
    render(
      <DutyDayTimeline
        today={TODAY}
        onAmend={() => {}}
        days={[
          dutyDay({
            violations: [{ rule: "ShiftDriving13h", date: FIXTURE_DATE, atUnix: 0, figureSeconds: 0, limitSeconds: 0, suppressed: false, suppressedByExceptionId: null, message }],
          }),
        ]}
      />,
    );
    const chip = screen.getByText(message);
    expect(chip.parentElement?.textContent).toContain(statusMeta("over").g);
  });

  it("expands into events, strikes a voided one through with its replacement's reason, and offers AMEND", () => {
    const onAmend = vi.fn();
    const voided = dutyEvent({ eventId: "orig", status: "Driving", isVoided: true });
    const replacement = dutyEvent({ eventId: "repl", sequence: 2, status: "Driving", amendsEventId: "orig", amendmentReason: "Paper log reads 08:15." });
    const day = dutyDay({ events: [voided, replacement] });
    render(<DutyDayTimeline today={TODAY} onAmend={onAmend} days={[day]} />);

    fireEvent.click(screen.getByText(/Fri, Oct 9/));

    expect(screen.getByText("Voided — Paper log reads 08:15.")).toBeTruthy();
    expect(screen.getByText("Amendment — Paper log reads 08:15.")).toBeTruthy();
    const rows = screen.getAllByTestId("duty-event-row");
    expect(rows).toHaveLength(2);
    const struck = rows.filter((r) => r.style.textDecoration === "line-through");
    expect(struck).toHaveLength(1);
    expect(struck[0].getAttribute("data-voided")).toBe("true");
    // Only the live event can be amended again.
    const amend = screen.getAllByText("Amend");
    expect(amend).toHaveLength(1);
    fireEvent.click(amend[0]);
    expect(onAmend).toHaveBeenCalledWith(day, replacement);
  });
});
