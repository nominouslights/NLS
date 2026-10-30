import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import CopyCodesPanel from "@/components/screens/codes/CopyCodesPanel";
import type { BudgetPeriod } from "@/lib/types";
import type { BudgetCodeCopyResult } from "@/lib/api/budgeting";

// The Copy codes panel takes its request as a prop (onCopy), so this drives it with vi.fn()s —
// the PeriodChooser / ProfileForm shape, no transport stub. The wire shape of the copy itself is
// pinned in lib/api/budgeting.requests.test.ts.

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

function makePeriod(id: string, label: string, startsOn: string, state: BudgetPeriod["state"]): BudgetPeriod {
  return {
    id,
    label,
    startsOn,
    endsOn: startsOn,
    state,
    pk: "info",
    plannedRevenue: 0,
    plannedExpense: 0,
  };
}

const Q2 = makePeriod("q2", "Q2 2026", "2026-04-01", "Closed");
const Q3 = makePeriod("q3", "Q3 2026", "2026-07-01", "Draft");

function renderPanel(over: Partial<Parameters<typeof CopyCodesPanel>[0]> = {}) {
  const props = {
    period: Q3,
    periods: [Q2, Q3],
    busy: false,
    confirming: false,
    onRequestConfirm: vi.fn(),
    onCancelConfirm: vi.fn(),
    onCopy: vi.fn<(id: string) => Promise<BudgetCodeCopyResult | null>>(),
    ...over,
  };
  const utils = render(<CopyCodesPanel {...props} />);
  return { ...props, ...utils };
}

describe("CopyCodesPanel", () => {
  it("pre-selects last period — a Closed one — and names the target on the button", () => {
    renderPanel();
    expect((screen.getByRole("combobox") as HTMLSelectElement).value).toBe("q2");
    expect(screen.getByText("COPY CODES INTO Q3 2026")).toBeTruthy();
  });

  it("asks for confirmation on the first click and does not copy yet", () => {
    const { onRequestConfirm, onCopy } = renderPanel();
    fireEvent.click(screen.getByText("COPY CODES INTO Q3 2026"));
    expect(onRequestConfirm).toHaveBeenCalledTimes(1);
    expect(onCopy).not.toHaveBeenCalled();
  });

  it("names both periods while confirming, then copies from the chosen source", async () => {
    const onCopy = vi.fn(async () => ({
      copied: 9,
      skippedExisting: 3,
      skippedRetired: 2,
      sourceCodeCount: 14,
    }));
    const { container } = renderPanel({ confirming: true, onCopy });

    const text = container.textContent ?? "";
    expect(text).toContain("This brings Q2 2026's active budget codes into Q3 2026");
    expect(text).toContain("Nothing changes in Q2 2026");

    await act(async () => {
      fireEvent.click(screen.getByText("CONFIRM COPY CODES INTO Q3 2026"));
    });

    expect(onCopy).toHaveBeenCalledWith("q2");
    expect(screen.getByText("Copied")).toBeTruthy();
    expect(container.textContent).toContain(
      "Copied 9 codes as active codes, hierarchy included — skipped 3 codes already in this period, left untouched and 2 codes retired there, not copied. 14 codes in the source period.",
    );
  });

  it("reports nothing when the copy was refused (the screen's banner carries the message)", async () => {
    const onCopy = vi.fn(async () => null);
    const { container } = renderPanel({ confirming: true, onCopy });

    await act(async () => {
      fireEvent.click(screen.getByText("CONFIRM COPY CODES INTO Q3 2026"));
    });

    expect(onCopy).toHaveBeenCalledTimes(1);
    expect(screen.queryByText("Copied")).toBeNull();
    expect(container.textContent).not.toContain("source period");
  });

  it("says there is nothing to copy from when the entered period is the only one", () => {
    renderPanel({ periods: [Q3] });
    expect(
      screen.getByText("There is no other budget period to copy codes from yet — this is the only one."),
    ).toBeTruthy();
    expect(screen.queryByText("COPY CODES INTO Q3 2026")).toBeNull();
  });
});
