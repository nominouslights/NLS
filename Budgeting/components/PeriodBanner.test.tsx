import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import PeriodBanner from "@/components/PeriodBanner";
import type { BudgetPeriod } from "@/lib/types";

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const Q3: BudgetPeriod = {
  id: "q3",
  label: "Q3 2026",
  startsOn: "2026-07-01",
  endsOn: "2026-09-30",
  state: "Finalized",
  pk: "soon",
  plannedRevenue: 0,
  plannedExpense: 0,
};

function renderBanner(over: Partial<Parameters<typeof PeriodBanner>[0]> = {}) {
  const props = {
    period: Q3 as BudgetPeriod | null,
    scoped: true,
    held: false,
    onSwitch: vi.fn(),
    onChoose: vi.fn(),
    ...over,
  };
  const { container } = render(<PeriodBanner {...props} />);
  return { ...props, container };
}

describe("PeriodBanner", () => {
  it("shows the period's label, dates, state and editability", () => {
    const { container } = renderBanner();
    const text = container.textContent ?? "";
    expect(text).toContain("Working in");
    expect(text).toContain("Q3 2026");
    expect(text).toContain("2026");
    expect(screen.getByText("Finalized")).toBeTruthy();
    // Finalized freezes the plan (BudgetPeriod.AllowsPlanChanges).
    expect(screen.getByText("Plan read-only")).toBeTruthy();
  });

  it("switches period when not held", () => {
    const { onSwitch } = renderBanner();
    const button = screen.getByText("SWITCH PERIOD");
    expect(button.getAttribute("aria-disabled")).toBeNull();
    fireEvent.click(button);
    expect(onSwitch).toHaveBeenCalledTimes(1);
  });

  it("refuses to switch while held, and says why", () => {
    const { onSwitch } = renderBanner({ held: true });
    const button = screen.getByText("SWITCH PERIOD");
    expect(button.getAttribute("aria-disabled")).toBe("true");
    fireEvent.click(button);
    expect(onSwitch).not.toHaveBeenCalled();
    expect(screen.getByText("Finishing a change to Q3 2026…")).toBeTruthy();
  });

  it("marks a screen that is not tied to a period", () => {
    renderBanner({ scoped: false });
    expect(
      screen.getByText("This screen isn't tied to a period — it applies to every period."),
    ).toBeTruthy();
    expect(screen.queryByText("Plan read-only")).toBeNull();
  });

  it("offers the chooser when nothing is entered on a global screen", () => {
    const { onChoose } = renderBanner({ period: null, scoped: false });
    expect(screen.getByText("No period entered")).toBeTruthy();
    fireEvent.click(screen.getByText("CHOOSE A PERIOD"));
    expect(onChoose).toHaveBeenCalledTimes(1);
  });

  it("renders nothing on a period-scoped screen with nothing entered (the chooser is there)", () => {
    const { container } = renderBanner({ period: null, scoped: true });
    expect(container.textContent).toBe("");
  });
});
