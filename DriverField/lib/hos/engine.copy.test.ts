import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

// lib/hos/engine.ts IS A COPY, AND THIS IS THE BACKSTOP AGAINST IT DRIFTING.
//
// The engine is the TypeScript mirror of Backend/src/Shared/HoursOfService — the one rule set
// that decides whether a driver may drive. Dispatcher/lib/hos/engine.ts is the source; this app
// holds a byte-identical copy below the standard two-line header, exactly as it holds the design
// system and the NL-PTI-01 catalogue (lib/inspectionForm.copy.test.ts is the precedent). Drift
// here is worse than a visual bug: the tablet and the console would compute DIFFERENT remaining
// hours from the same ledger, and one of them would disagree with the server that enforces.
//
// THE FIX IS NEVER TO EDIT THE COPY. Change Dispatcher/lib/hos/engine.ts (which itself must
// track the C# — engine.fixtures.test.ts holds it to the shared fixture snapshots), then re-copy.

const REPO_ROOT = join(import.meta.dirname, "..", "..", "..");
const SOURCE = join(REPO_ROOT, "Dispatcher", "lib", "hos", "engine.ts");
const COPY = join(import.meta.dirname, "engine.ts");

/** The exact two lines every copied file in this app carries, in this order. */
const HEADER = [
  "// COPIED FROM Dispatcher/lib/hos/engine.ts — keep identical below this header.",
  "// Change Dispatcher first, then re-copy. Drift check: see DriverField/CLAUDE.md.",
];

function read(path: string): string {
  return readFileSync(path, "utf8");
}

describe("lib/hos/engine.ts is a copy of Dispatcher's", () => {
  it("carries the fixed two-line header the drift check skips", () => {
    // CLAUDE.md's loop is `diff <(tail -n +3 …) …`. Two lines, no more and no fewer.
    expect(read(COPY).split("\n").slice(0, 2)).toEqual(HEADER);
  });

  it("is byte-identical to the source below that header", () => {
    // Read from disk, both of them, exactly as the drift loop does — not imported and compared
    // as behaviour. Two engines can agree on every fixture and still have diverged in a comment
    // that explains a regulation section, and the next transliteration from the C# starts from
    // whichever one somebody opens.
    const copied = read(COPY).split("\n").slice(2).join("\n");
    expect(copied).toBe(read(SOURCE));
  });
});
