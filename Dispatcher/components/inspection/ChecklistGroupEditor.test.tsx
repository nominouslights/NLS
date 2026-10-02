import { useState } from "react";
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import ChecklistGroupEditor, { unansweredItemKeys, type ChecklistRow } from "./ChecklistGroupEditor";
import { rowsFor } from "./checklistRows";
import { itemsFor } from "@/lib/inspectionForm";

// "Mark unanswered OK" is a convenience for transcribing a paper section the driver
// ticked through. On a compliance form the dangerous failure is a bulk action that
// overwrites a deliberate answer — a Defect turned into an OK hides a fault — so the
// tests pin that it touches ONLY unanswered rows, and only in its own sub-group.

afterEach(cleanup);

const GROUP = itemsFor("NL-02", "PreTrip").find((g) => g.key === "Tires & Wheels")!;
const OTHER = itemsFor("NL-02", "PreTrip").find((g) => g.key === "Frame & Body")!;

function seeded(): ChecklistRow[] {
  const rows = rowsFor("NL-02", "PreTrip").filter((r) => r.groupKey === GROUP.key || r.groupKey === OTHER.key);
  const [first, second, third] = rows.filter((r) => r.groupKey === GROUP.key);
  return rows.map((r) =>
    r.itemKey === first.itemKey
      ? { ...r, state: "Defect" as const, severity: "Major" as const, note: "Cord showing" }
      : r.itemKey === second.itemKey
        ? { ...r, state: "NotApplicable" as const, note: "Not fitted" }
        : r.itemKey === third.itemKey
          ? { ...r, state: "Ok" as const }
          : r,
  );
}

/** The editor wired to real state, the way both modals use it. */
function Harness({ initial, readOnly, onRows }: { initial: ChecklistRow[]; readOnly?: boolean; onRows: (r: ChecklistRow[]) => void }) {
  const [rows, setRows] = useState(initial);
  onRows(rows);
  return (
    <ChecklistGroupEditor
      group={GROUP}
      rows={rows.filter((r) => r.groupKey === GROUP.key)}
      readOnly={readOnly}
      onPatch={(key, patch) => setRows((prev) => prev.map((r) => (r.itemKey === key ? { ...r, ...patch } : r)))}
    />
  );
}

describe("unansweredItemKeys", () => {
  it("returns only rows with no state", () => {
    const rows = seeded().filter((r) => r.groupKey === GROUP.key);
    const keys = unansweredItemKeys(rows);
    expect(keys).toHaveLength(GROUP.items.length - 3);
    for (const key of keys) expect(rows.find((r) => r.itemKey === key)!.state).toBeNull();
  });
});

describe("ChecklistGroupEditor — Mark unanswered OK", () => {
  it("sets only the unanswered rows to OK, never a Defect or an N-A", () => {
    let latest: ChecklistRow[] = [];
    const initial = seeded();
    render(<Harness initial={initial} onRows={(r) => (latest = r)} />);

    const unanswered = GROUP.items.length - 3;
    fireEvent.click(screen.getByText(`MARK ${unanswered} UNANSWERED OK`));

    const group = latest.filter((r) => r.groupKey === GROUP.key);
    const before = new Map(initial.map((r) => [r.itemKey, r]));
    for (const row of group) {
      const was = before.get(row.itemKey)!;
      if (was.state === null) expect(row.state, row.itemKey).toBe("Ok");
      else expect(row, row.itemKey).toEqual(was); // Defect / N-A / OK kept exactly, note and severity too
    }
    expect(group.filter((r) => r.state === "Defect")).toHaveLength(1);
    expect(group.filter((r) => r.state === "NotApplicable")).toHaveLength(1);
  });

  it("does not touch another sub-group's rows", () => {
    let latest: ChecklistRow[] = [];
    render(<Harness initial={seeded()} onRows={(r) => (latest = r)} />);
    fireEvent.click(screen.getByText(/^MARK \d+ UNANSWERED OK$/));
    const other = latest.filter((r) => r.groupKey === OTHER.key);
    expect(other.length).toBeGreaterThan(0);
    expect(other.every((r) => r.state === null)).toBe(true);
  });

  it("is disabled once nothing in the sub-group is unanswered", () => {
    let latest: ChecklistRow[] = [];
    render(<Harness initial={seeded()} onRows={(r) => (latest = r)} />);
    fireEvent.click(screen.getByText(/^MARK \d+ UNANSWERED OK$/));

    const button = screen.getByText("MARK UNANSWERED OK");
    expect(button.getAttribute("aria-disabled")).toBe("true");
    const snapshot = latest;
    fireEvent.click(button);
    expect(latest).toBe(snapshot);
  });

  it("is disabled, and does nothing, when the editor is read-only", () => {
    let latest: ChecklistRow[] = [];
    const initial = seeded();
    render(<Harness initial={initial} readOnly onRows={(r) => (latest = r)} />);

    const button = screen.getByText("MARK UNANSWERED OK");
    expect(button.getAttribute("aria-disabled")).toBe("true");
    fireEvent.click(button);
    expect(latest).toEqual(initial);
  });
});
