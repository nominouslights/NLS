import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { statusMeta } from "@/lib/theme";
import { HosMessages } from "@/lib/hos/engine";
import { HosStatusGauge } from "./HosStatusGauge";
import { FIXTURE_DATE, hosStatus } from "./hosFixtures";

// The gauge is the server's evaluation rendered as colour + glyph + label. These pin that
// each of the four figures is banded the way the engine bands, that a blocked driver shows
// the reasons (and today's violation sentences verbatim), that a thin ledger reads as
// "not dispatchable" rather than as hours unused, and that the planning label is always on screen.

afterEach(cleanup);

describe("HosStatusGauge — the four bars", () => {
  it("renders every figure as a StatusChip with the engine's band glyph", () => {
    render(<HosStatusGauge status={hosStatus()} />);

    expect(screen.getByText("8h 00m left")).toBeTruthy(); // driving
    expect(screen.getByText("7h 00m left")).toBeTruthy(); // on duty
    expect(screen.getByText("9h 00m left")).toBeTruthy(); // 16h window: 16h − 7h elapsed
    expect(screen.getByText("40h 00m left")).toBeTruthy(); // cycle
    // Every chip carries the ontime glyph — never colour alone.
    expect(screen.getAllByText(statusMeta("ontime").g).length).toBeGreaterThanOrEqual(4);
  });

  it("bands ≤ 2h as Soon and 0 as Over", () => {
    render(
      <HosStatusGauge
        status={hosStatus({}, { remainingDrivingTodaySeconds: 2 * 3600, cycle: { onDutySeconds: 70 * 3600, limitSeconds: 70 * 3600, remainingSeconds: 0, windowStartDate: FIXTURE_DATE, lastResetEndUnix: null, recordsIncomplete: false } })}
      />,
    );

    const soon = screen.getByText("2h 00m left");
    expect(soon.parentElement?.textContent).toContain(statusMeta("soon").g);
    const over = screen.getByText("0h 00m left");
    expect(over.parentElement?.textContent).toContain(statusMeta("over").g);
  });

  it("shows the must-stop and next-required-off clocks in local time", () => {
    render(<HosStatusGauge status={hosStatus()} />);

    expect(screen.getByText("Must stop driving by 21:00")).toBeTruthy();
    expect(screen.getByText("Next required off: 8h 00m by 21:00")).toBeTruthy();
    expect(screen.getByText(/24h off in last 14 days/)).toBeTruthy();
  });
});

describe("HosStatusGauge — blocked", () => {
  it("lists every blocking reason with ▲ and today's violation sentences verbatim", () => {
    const message = HosMessages.for("DailyDriving13h", 14 * 3600, 13 * 3600);
    render(
      <HosStatusGauge
        status={hosStatus(
          {},
          {
            canDriveNow: false,
            band: "Over",
            blockingReasons: ["DailyDrivingExhausted", "ShiftWindowClosed"],
            violations: [
              { rule: "DailyDriving13h", date: FIXTURE_DATE, atUnix: 0, figureSeconds: 14 * 3600, limitSeconds: 13 * 3600, suppressed: false, suppressedByExceptionId: null, message },
              { rule: "DailyOffDuty10h", date: "2026-10-01", atUnix: 0, figureSeconds: 0, limitSeconds: 0, suppressed: false, suppressedByExceptionId: null, message: "yesterday's problem" },
            ],
          },
        )}
      />,
    );

    expect(screen.getByText("Cannot drive now")).toBeTruthy();
    expect(screen.getByText("13h daily driving used up")).toBeTruthy();
    expect(screen.getByText("16h shift window closed")).toBeTruthy();
    expect(screen.getByText(message)).toBeTruthy();
    // Only today's violations belong on the gauge; the timeline shows the rest.
    expect(screen.queryByText("yesterday's problem")).toBeNull();
    expect(screen.getAllByText(statusMeta("over").g).length).toBeGreaterThanOrEqual(3);
  });
});

describe("HosStatusGauge — records", () => {
  it("reads fewer than 14 days of records as not dispatchable, in the caution kind", () => {
    render(<HosStatusGauge status={hosStatus({ recordsOnFile: false })} />);

    const chip = screen.getByText("Fewer than 14 days of records — not dispatchable");
    expect(chip.parentElement?.textContent).toContain(statusMeta("soon").g);
  });

  it("always shows the server's planning label as the footer, in both modes", () => {
    const status = hosStatus();
    const { unmount } = render(<HosStatusGauge status={status} />);
    expect(screen.getByText(status.label)).toBeTruthy();
    unmount();

    render(<HosStatusGauge status={status} compact />);
    expect(screen.getByText(status.label)).toBeTruthy();
    expect(screen.getByText("Driving today: 8h 00m left")).toBeTruthy();
  });
});
