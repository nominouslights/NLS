import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

// lib/inspectionForm.ts IS A COPY, AND THIS IS THE BACKSTOP AGAINST IT DRIFTING.
//
// The Driver Field App holds byte-identical copies of files owned by Dispatcher (the manifest
// and the one-command drift loop are in DriverField/CLAUDE.md). For the design-system files,
// drift is a visible product bug. For THIS file it is worse than that: it is the NL-PTI-01
// catalogue — the legal inspection form itself — and every row's `key` is a WIRE VALUE, stored
// verbatim as InspectionChecklistItem.Item and forming half of the `(InspectionId, Item)`
// address a defect is filed against.
//
// So a drift here does not make the tablet look different from the Dispatch Console. It makes
// the two COLLECT DIFFERENT LEGAL FORMS: a row the driver answers that the console cannot
// address, a defect filed against a string no dispatcher can resolve, and two versions of a
// National Safety Code Standard 13 document in one company. The drift loop in CLAUDE.md catches
// this too, but that loop is a command somebody has to remember to run; this runs on every
// `npm test` and on every pre-push hook.
//
// THE FIX IS NEVER TO EDIT THE COPY. Change Dispatcher/lib/inspectionForm.ts, then re-copy —
// into Budgeting too if it ever takes the file, which it does not today. The direction is
// one-way and the header is the reminder.

const REPO_ROOT = join(import.meta.dirname, "..", "..");
const SOURCE = join(REPO_ROOT, "Dispatcher", "lib", "inspectionForm.ts");
const COPY = join(import.meta.dirname, "inspectionForm.ts");

/** The exact two lines every copied file in this app carries, in this order. */
const HEADER = [
  "// COPIED FROM Dispatcher/lib/inspectionForm.ts — keep identical below this header.",
  "// Change Dispatcher first, then re-copy. Drift check: see DriverField/CLAUDE.md.",
];

function read(path: string): string {
  return readFileSync(path, "utf8");
}

describe("lib/inspectionForm.ts is a copy of Dispatcher's", () => {
  it("carries the fixed two-line header the drift check skips", () => {
    // CLAUDE.md's loop is `diff <(tail -n +3 …) …`. Two lines, no more and no fewer — a third
    // line of commentary here would shift every subsequent line and fail the loop for a reason
    // that has nothing to do with the form.
    expect(read(COPY).split("\n").slice(0, 2)).toEqual(HEADER);
  });

  it("is byte-identical to the source below that header", () => {
    // Read from disk, both of them, exactly as the drift loop does — not imported and compared
    // as data. A structural comparison would pass while a comment explaining WHY a row is
    // classified Major had silently diverged, and on a compliance form the prose is the point.
    const copied = read(COPY).split("\n").slice(2).join("\n");
    expect(copied).toBe(read(SOURCE));
  });
});
