import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { statusMeta } from "@/lib/theme";
import { PersonalConveyancePanel } from "./PersonalConveyancePanel";
import { dutyDay } from "./hosFixtures";

// Personal conveyance per day: the verdict bands at 59.9 / 60 / 74.9 / 75 km, each with its own
// glyph; days without PC are left out; a window with none says so.

afterEach(cleanup);

describe("PersonalConveyancePanel", () => {
  it("renders one verdict row per day with PC, newest first, banded at 60 and 75 km", () => {
    render(
      <PersonalConveyancePanel
        days={[
          dutyDay({ date: "2026-10-06", personalConveyanceKm: 59.9 }),
          dutyDay({ date: "2026-10-07", personalConveyanceKm: 60 }),
          dutyDay({ date: "2026-10-08", personalConveyanceKm: 74.9 }),
          dutyDay({ date: "2026-10-09", personalConveyanceKm: 75 }),
          dutyDay({ date: "2026-10-05", personalConveyanceKm: 0 }),
          dutyDay({ date: "2026-10-04", hasRecords: false, personalConveyanceKm: 0 }),
        ]}
      />,
    );

    const rows = screen.getAllByTestId("pc-day-row");
    expect(rows).toHaveLength(4);
    expect(rows.map((r) => r.textContent)).toEqual([
      expect.stringContaining("Oct 9"),
      expect.stringContaining("Oct 8"),
      expect.stringContaining("Oct 7"),
      expect.stringContaining("Oct 6"),
    ]);
    expect(rows[0].textContent).toContain("▲");
    expect(rows[0].textContent).toContain("75.0 km — 75 km reached, remainder counts as on-duty");
    expect(rows[1].textContent).toContain(statusMeta("soon").g);
    expect(rows[1].textContent).toContain("74.9 km — 0.1 km left");
    expect(rows[2].textContent).toContain("60.0 km — 15.0 km left");
    expect(rows[3].textContent).toContain(statusMeta("ontime").g);
    expect(rows[3].textContent).toContain("59.9 km");
    expect(screen.getByText(/75 km reached on at least one day/)).toBeTruthy();
  });

  it("says so when no day has personal conveyance, and loads when days are null", () => {
    const { unmount } = render(<PersonalConveyancePanel days={[dutyDay({ personalConveyanceKm: 0 })]} />);
    expect(screen.getByText("No personal conveyance recorded in the last 14 days")).toBeTruthy();
    unmount();
    render(<PersonalConveyancePanel days={null} />);
    expect(screen.getByText("Loading…")).toBeTruthy();
  });
});
