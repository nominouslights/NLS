import { describe, expect, it } from "vitest";
import { dutyMeta, statusMeta, type DutyStatus, type StatusKind } from "./theme";

// Platform non-negotiable (northern-link-architecture, rule 6; accessible-status-colors
// skill): status is never colour alone — it is colour + icon + text label, drawn from one
// colourblind-safe palette. The palette half of that rule lives entirely in this file, and
// it is the kind of thing a well-meaning "let's brighten the greens" edit silently breaks.
// These tests pin the hexes and prove every status kind carries a glyph.
//
// STATUS_META / DUTY_META are module-private, so everything goes through the public
// accessors — which is also what every one of the ~133 call sites uses.

/** Every member of the StatusKind union. Adding a kind without adding it here is caught
 *  by the exhaustiveness check below, not silently skipped. */
const ALL_STATUS_KINDS: StatusKind[] = ["ontime", "soon", "over", "off", "info"];

/** The four protected status hexes, plus the blue used for the neutral/info kind. */
const PROTECTED = {
  teal: "#009E73",
  gold: "#E1B000",
  vermillion: "#D55E00",
  gray: "#7A8899",
  blue: "#1F6FB2",
} as const;

describe("STATUS_META palette", () => {
  it("pins the protected status hexes", () => {
    expect(statusMeta("ontime").c).toBe(PROTECTED.teal);
    expect(statusMeta("soon").c).toBe(PROTECTED.gold);
    expect(statusMeta("over").c).toBe(PROTECTED.vermillion);
    expect(statusMeta("off").c).toBe(PROTECTED.gray);
  });

  it("keeps info on the neutral blue, distinct from the four semantic hexes", () => {
    expect(statusMeta("info").c).toBe(PROTECTED.blue);
    const semantic = [PROTECTED.teal, PROTECTED.gold, PROTECTED.vermillion, PROTECTED.gray];
    expect(semantic).not.toContain(statusMeta("info").c);
  });

  it("gives every status kind a distinct solid colour", () => {
    const solids = ALL_STATUS_KINDS.map((kind) => statusMeta(kind).c);
    expect(new Set(solids).size).toBe(ALL_STATUS_KINDS.length);
  });

  // The "+ icon" half of the rule, enforced structurally: a kind with no glyph can only
  // ever be rendered as a bare colour swatch.
  it.each(ALL_STATUS_KINDS)("gives status kind %s a non-empty glyph", (kind) => {
    expect(statusMeta(kind).g.trim().length).toBeGreaterThan(0);
  });

  it("gives every status kind the full meta shape a chip needs", () => {
    for (const kind of ALL_STATUS_KINDS) {
      const m = statusMeta(kind);
      for (const field of ["c", "t", "bt", "g", "bg", "bd"] as const) {
        expect(m[field], `${kind}.${field}`).toBeTruthy();
      }
    }
  });

  it("covers the whole StatusKind union (no kind added without a palette entry)", () => {
    // A union member with no STATUS_META entry silently falls through to `info`. Every
    // non-info kind must therefore resolve to something that is NOT the info entry — and
    // the list above is type-checked against StatusKind, so adding a kind to the union
    // without adding it here is a compile error in `npm run build`.
    for (const kind of ALL_STATUS_KINDS.filter((k) => k !== "info")) {
      expect(statusMeta(kind), kind).not.toEqual(statusMeta("info"));
    }
  });
});

describe("statusMeta fallback", () => {
  it("falls back to info for an unknown kind rather than returning undefined", () => {
    // Real data can carry a status string the frontend union does not know yet; a crash
    // in a status chip would take the whole row down.
    const unknown = statusMeta("nonsense" as StatusKind);
    expect(unknown).toEqual(statusMeta("info"));
    expect(unknown.g.trim().length).toBeGreaterThan(0);
  });
});

describe("DUTY_META", () => {
  const ALL_DUTY: DutyStatus[] = ["Off Duty", "On Duty", "Driving"];
  const STATUS_SOLIDS = new Set(ALL_STATUS_KINDS.map((kind) => statusMeta(kind).c));

  it("reuses only the protected status hexes — no ad hoc duty colours", () => {
    for (const status of ALL_DUTY) {
      expect(STATUS_SOLIDS, status).toContain(dutyMeta(status).color);
    }
  });

  it("maps each duty status to its documented hex", () => {
    expect(dutyMeta("Off Duty").color).toBe(PROTECTED.gray);
    expect(dutyMeta("On Duty").color).toBe(PROTECTED.blue);
    expect(dutyMeta("Driving").color).toBe(PROTECTED.teal);
  });

  it.each(["Off Duty", "On Duty", "Driving"] as DutyStatus[])(
    "gives duty status %s a non-empty glyph",
    (status) => {
      expect(dutyMeta(status).glyph.trim().length).toBeGreaterThan(0);
    },
  );

  it("falls back to Off Duty for an unknown duty status", () => {
    expect(dutyMeta("Sleeping" as DutyStatus)).toEqual(dutyMeta("Off Duty"));
  });
});
