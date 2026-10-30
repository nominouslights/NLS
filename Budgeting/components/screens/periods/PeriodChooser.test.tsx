import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import PeriodChooser from "@/components/screens/periods/PeriodChooser";
import type { BudgetPeriod } from "@/lib/types";

// The chooser takes every callback as a prop, so this drives it with vi.fn()s — the same shape
// as ProfileForm.test.tsx, and no transport stub.

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const Q2: BudgetPeriod = {
  id: "q2",
  label: "Q2 2026",
  startsOn: "2026-04-01",
  endsOn: "2026-06-30",
  state: "Closed",
  pk: "off",
  plannedRevenue: 1000,
  plannedExpense: 900,
};
const Q3: BudgetPeriod = {
  id: "q3",
  label: "Q3 2026",
  startsOn: "2026-07-01",
  endsOn: "2026-09-30",
  state: "Draft",
  pk: "info",
  plannedRevenue: 0,
  plannedExpense: 0,
};

function renderChooser(over: Partial<Parameters<typeof PeriodChooser>[0]> = {}) {
  const props = {
    periods: [Q2, Q3],
    error: null,
    lost: false,
    returning: false,
    todayIso: "2026-08-15",
    forScreen: "periods" as const,
    onEnter: vi.fn(),
    onCreate: vi.fn(),
    onRetry: vi.fn(),
    ...over,
  };
  render(<PeriodChooser {...props} />);
  return props;
}

describe("PeriodChooser", () => {
  it("enters the period whose row was clicked", () => {
    const { onEnter } = renderChooser();
    fireEvent.click(screen.getByRole("button", { name: "Enter Q2 2026" }));
    expect(onEnter).toHaveBeenCalledTimes(1);
    expect(onEnter).toHaveBeenCalledWith("q2");
  });

  it("shows each row's state as a written label", () => {
    renderChooser();
    const q2 = screen.getByRole("button", { name: "Enter Q2 2026" });
    const q3 = screen.getByRole("button", { name: "Enter Q3 2026" });
    expect(q2.textContent).toContain("Closed");
    expect(q3.textContent).toContain("Draft");
  });

  it("tags and focuses the period containing today", () => {
    renderChooser();
    const q3 = screen.getByRole("button", { name: "Enter Q3 2026" });
    expect(q3.textContent).toContain("INCLUDES TODAY");
    expect(screen.getByRole("button", { name: "Enter Q2 2026" }).textContent).not.toContain(
      "INCLUDES TODAY",
    );
    expect(document.activeElement).toBe(q3);
  });

  it("tags the latest period when none contains today", () => {
    renderChooser({ todayIso: "2027-03-01" });
    expect(screen.getByRole("button", { name: "Enter Q3 2026" }).textContent).toContain("LATEST");
  });

  it("names the screen the planner was heading for", () => {
    renderChooser({ forScreen: "variance" });
    expect(screen.getByText("Choose a period to open Variance")).toBeTruthy();
  });

  it("offers to create a period when there are none", () => {
    const { onCreate } = renderChooser({ periods: [] });
    expect(screen.getByText(/No budget periods yet/)).toBeTruthy();
    fireEvent.click(screen.getByText("CREATE THE FIRST PERIOD"));
    expect(onCreate).toHaveBeenCalledTimes(1);
  });

  it("shows a load error verbatim and retries", () => {
    const { onRetry } = renderChooser({
      periods: [],
      error: { message: "The budgeting service is unreachable.", code: "Network.Unreachable" },
    });
    expect(screen.getByText("The budgeting service is unreachable.")).toBeTruthy();
    fireEvent.click(screen.getByText("RETRY"));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it("says so when the remembered period is gone", () => {
    renderChooser({ lost: true });
    expect(
      screen.getByText("The period you were working in is no longer available — choose another."),
    ).toBeTruthy();
    expect(screen.getByText("Period unavailable")).toBeTruthy();
  });

  it("says it is returning while a remembered period loads", () => {
    renderChooser({ periods: null, returning: true });
    expect(screen.getByText("Returning to your period…")).toBeTruthy();
  });
});
