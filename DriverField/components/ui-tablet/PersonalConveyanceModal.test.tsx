import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { PC_ATTESTATIONS, PersonalConveyanceModal } from "./PersonalConveyanceModal";

// The personal-conveyance sheet as a driver meets it: four attestations that ALL gate Confirm,
// the end-odometer prompt refusing a reading below the start on the tablet, and today's
// kilometres read through the one band set on both sides of 60 and 75 km.

afterEach(() => {
  cleanup();
});

function numberInput(): HTMLInputElement {
  return document.querySelector('input[type="number"]') as HTMLInputElement;
}

describe("start mode", () => {
  it("asks four attestations and lights Confirm only when every one is on", () => {
    const onConfirm = vi.fn();
    render(<PersonalConveyanceModal mode={{ mode: "start", suggestedKm: 184920 }} todayPcDecikm={0} onClose={() => undefined} onConfirm={onConfirm} />);

    expect(PC_ATTESTATIONS).toEqual(["No passengers, parcels or trailer", "Not performing any work", "Not a Leaf Rapids ↔ Thompson or shop move", "Bus is empty"]);
    const confirm = screen.getByRole("button", { name: "Confirm personal conveyance" }) as HTMLButtonElement;
    expect(numberInput().value).toBe("184920");
    expect(confirm.disabled).toBe(true);
    expect(screen.getByText("Every attestation must be on.")).toBeTruthy();

    const tiles = PC_ATTESTATIONS.map((label) => screen.getByRole("button", { name: label }));
    for (const tile of tiles.slice(0, 3)) fireEvent.click(tile);
    expect(tiles.slice(0, 3).every((t) => t.getAttribute("aria-pressed") === "true")).toBe(true);
    expect(confirm.disabled).toBe(true);

    fireEvent.click(tiles[3]);
    expect(confirm.disabled).toBe(false);
    fireEvent.click(confirm);
    expect(onConfirm).toHaveBeenCalledWith(184920);

    // Turning one off again re-locks it.
    fireEvent.click(tiles[1]);
    expect((screen.getByRole("button", { name: "Confirm personal conveyance" }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("needs an odometer reading, refuses a negative one, and says where today stands", () => {
    render(<PersonalConveyanceModal mode={{ mode: "start", suggestedKm: null }} todayPcDecikm={125} onClose={() => undefined} onConfirm={() => undefined} />);
    expect(screen.getByText("Enter the odometer reading now.")).toBeTruthy();
    fireEvent.change(numberInput(), { target: { value: "-1" } });
    expect(screen.getByText("Enter the odometer reading now.")).toBeTruthy();
    expect(screen.getByText(/PC today: 12\.5 km/)).toBeTruthy();
    expect(screen.getByText(/62\.5 km left before it counts as on-duty/)).toBeTruthy();
    expect(screen.getByText(/Kilometres are the odometer readings you enter here, not a live feed/)).toBeTruthy();
  });
});

describe("end mode", () => {
  it("refuses an end reading below the start, on screen, and confirms one at or above it", () => {
    const onConfirm = vi.fn();
    render(<PersonalConveyanceModal mode={{ mode: "end", startKm: 184920 }} todayPcDecikm={0} onClose={() => undefined} onConfirm={onConfirm} />);
    const confirm = () => screen.getByRole("button", { name: "Record end of personal conveyance" }) as HTMLButtonElement;
    expect(confirm().disabled).toBe(true);

    fireEvent.change(numberInput(), { target: { value: "184900" } });
    expect(confirm().disabled).toBe(true);
    expect(screen.getByText(/lower than the 184920\.0 km that started the segment/)).toBeTruthy();

    fireEvent.change(numberInput(), { target: { value: "184920" } });
    expect(confirm().disabled).toBe(false);
    fireEvent.change(numberInput(), { target: { value: "184932.5" } });
    expect(screen.getByText(/PC today: 12\.5 km/)).toBeTruthy();
    fireEvent.click(confirm());
    expect(onConfirm).toHaveBeenCalledWith(184932.5);
  });

  it("bands the projected total: 59.9 teal, 60 gold ◐, 74.9 gold, 75 vermillion ▲ 'on-duty time'", () => {
    const cases: [prior: number, end: string, total: string, text: RegExp, glyph: string][] = [
      [0, "1059.9", "59.9 km", /15\.1 km left before it counts as on-duty/, "✓"],
      [0, "1060", "60.0 km", /Nearing the 75 km allowance — 15\.0 km left/, "◐"],
      [500, "1024.9", "74.9 km", /Nearing the 75 km allowance — 0\.1 km left/, "◐"],
      [500, "1025", "75.0 km", /Over 75 km — today's personal conveyance is on-duty time/, "▲"],
    ];
    for (const [prior, end, total, text, glyph] of cases) {
      render(<PersonalConveyanceModal mode={{ mode: "end", startKm: 1000 }} todayPcDecikm={prior} onClose={() => undefined} onConfirm={() => undefined} />);
      fireEvent.change(numberInput(), { target: { value: end } });
      expect(screen.getByText(`PC today: ${total}`), `${prior} + ${end}`).toBeTruthy();
      const line = screen.getByText(text, { exact: false });
      expect(line.parentElement?.textContent, `${prior} + ${end}`).toContain(glyph);
      cleanup();
    }
  });
});
