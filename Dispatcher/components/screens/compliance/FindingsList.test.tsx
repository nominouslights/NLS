import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { statusMeta } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import { FindingsList } from "./FindingsList";
import { driver, finding } from "./complianceFixtures";

// Findings across drivers: filter by status, open one, move it forward. Open → Corrected →
// Closed; a closed finding offers no move. Server messages are shown verbatim.

const { listComplianceFindings, openComplianceFinding, updateComplianceFinding } = vi.hoisted(() => ({
  listComplianceFindings: vi.fn(),
  openComplianceFinding: vi.fn(),
  updateComplianceFinding: vi.fn(),
}));
vi.mock("@/lib/api/hos", () => ({ listComplianceFindings, openComplianceFinding, updateComplianceFinding }));

const ROSTER = [driver(), driver({ id: "driver-2", name: "M. Spence" })];

function renderList(onChanged = vi.fn()) {
  render(<FindingsList roster={ROSTER} today="2026-10-12" version={0} onChanged={onChanged} onOpenDriver={vi.fn()} />);
  return { onChanged };
}

beforeEach(() => {
  listComplianceFindings.mockReset();
  openComplianceFinding.mockReset();
  updateComplianceFinding.mockReset();
});
afterEach(cleanup);

describe("FindingsList", () => {
  it("merges every driver's findings and filters by status (default: open + corrected)", async () => {
    listComplianceFindings.mockImplementation((id: string) =>
      Promise.resolve(
        id === "driver-1"
          ? [finding(), finding({ id: "f-2", status: "Closed", closedAtUtc: "2026-10-11T00:00:00Z" })]
          : [finding({ id: "f-3", driverId: "driver-2", status: "Corrected", severity: "Severe" })],
      ),
    );
    renderList();

    await waitFor(() => expect(screen.getAllByTestId("finding-row")).toHaveLength(2));
    expect(listComplianceFindings).toHaveBeenCalledWith("driver-1");
    expect(listComplianceFindings).toHaveBeenCalledWith("driver-2");

    // Status chips: colour + glyph + label.
    const open = screen.getByText("Open", { selector: "span" });
    expect(open.textContent).toContain(statusMeta("soon").g);
    expect(screen.getByText("Severe").textContent).toContain("▲");

    fireEvent.click(screen.getByRole("button", { name: "Closed" }));
    expect(screen.getAllByTestId("finding-row")).toHaveLength(1);
    // A closed finding offers no Update.
    expect(screen.queryByText("Update")).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "All" }));
    expect(screen.getAllByTestId("finding-row")).toHaveLength(3);
  });

  it("OPEN FINDING requires a description, then posts driver/date/rule/severity", async () => {
    listComplianceFindings.mockResolvedValue([]);
    const { onChanged } = renderList();
    await waitFor(() => expect(screen.getByText("No compliance findings on file.")).toBeTruthy());

    fireEvent.click(screen.getByText("+ OPEN FINDING"));
    fireEvent.click(screen.getByText("OPEN FINDING"));
    expect(openComplianceFinding).not.toHaveBeenCalled();
    expect(screen.getByText("Describe the finding.")).toBeTruthy();

    const selects = screen.getAllByRole("combobox");
    fireEvent.change(selects[0], { target: { value: "driver-2" } }); // driver
    fireEvent.change(selects[1], { target: { value: "__other" } }); // rule → free text
    fireEvent.change(selects[2], { target: { value: "Severe" } }); // severity
    fireEvent.change(screen.getByPlaceholderText("The rule or obligation breached"), { target: { value: "Falsified log entry" } });
    fireEvent.change(screen.getByPlaceholderText("What the audit or enforcement found"), { target: { value: "Paper log disagrees with fuel receipts" } });
    openComplianceFinding.mockResolvedValueOnce(finding({ id: "f-9", driverId: "driver-2" }));
    fireEvent.click(screen.getByText("OPEN FINDING"));

    await waitFor(() => expect(onChanged).toHaveBeenCalled());
    expect(openComplianceFinding).toHaveBeenCalledWith("driver-2", {
      localDate: "2026-10-12",
      rule: "Falsified log entry",
      severity: "Severe",
      description: "Paper log disagrees with fuel receipts",
      followUpNote: null,
    });
  });

  it("UPDATE moves Open → Corrected with a follow-up note, and shows a server refusal verbatim", async () => {
    listComplianceFindings.mockImplementation((id: string) => Promise.resolve(id === "driver-1" ? [finding()] : []));
    const { onChanged } = renderList();
    await waitFor(() => expect(screen.getByText("Update")).toBeTruthy());

    fireEvent.click(screen.getByText("Update"));
    // Only the forward moves are offered.
    const options = Array.from(screen.getByRole("combobox").querySelectorAll("option")).map((o) => o.textContent);
    expect(options).toEqual(["Corrected", "Closed"]);

    fireEvent.change(screen.getByPlaceholderText("What was done about it"), { target: { value: "Coached on 13h rule" } });
    updateComplianceFinding.mockRejectedValueOnce(new ApiError("Drivers.Finding.InvalidTransition", "A finding cannot move backwards.", 409));
    fireEvent.click(screen.getByText("UPDATE FINDING"));
    await waitFor(() => expect(screen.getByText("A finding cannot move backwards.")).toBeTruthy());
    expect(onChanged).not.toHaveBeenCalled();

    updateComplianceFinding.mockResolvedValueOnce(finding({ status: "Corrected" }));
    fireEvent.click(screen.getByText("UPDATE FINDING"));
    await waitFor(() => expect(onChanged).toHaveBeenCalled());
    expect(updateComplianceFinding).toHaveBeenLastCalledWith("driver-1", "f-1", { status: "Corrected", followUpNote: "Coached on 13h rule" });
  });
});
