import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { StatusBadge, StatusChip } from "./Chip";
import { statusMeta } from "@/lib/theme";

// lib/theme.test.ts proves the palette carries a glyph for every status kind; this proves
// the shared chips actually PUT it on screen. The rule is "status is never colour alone —
// colour + icon + text label", and a chip that dropped its glyph would still look fine to
// a sighted reviewer while failing the rule outright.

afterEach(cleanup);

describe("StatusChip", () => {
  it("renders both the glyph and the label, not colour alone", () => {
    render(<StatusChip kind="over" label="Overdue" />);

    expect(screen.getByText(statusMeta("over").g)).toBeTruthy();
    expect(screen.getByText("Overdue")).toBeTruthy();
  });

  it("paints the glyph badge in the kind's protected hex", () => {
    render(<StatusChip kind="ontime" label="On time" />);

    const glyph = screen.getByText(statusMeta("ontime").g);
    // jsdom may normalize the hex to rgb(); accept either spelling of #009E73.
    expect(["#009e73", "rgb(0, 158, 115)"]).toContain(glyph.style.background.toLowerCase());
  });
});

describe("StatusBadge", () => {
  it("renders the glyph, so the icon-only variant is still not colour alone", () => {
    render(<StatusBadge kind="soon" />);

    expect(screen.getByText(statusMeta("soon").g)).toBeTruthy();
  });
});
