import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

// lib/hos/hos-fixtures.json IS A COPY, AND THIS IS THE BACKSTOP AGAINST IT DRIFTING.
//
// The backend's fixture file (Backend/tests/NorthernLink.Shared.Tests/HoursOfService/
// hos-fixtures.json) is the contract between the C# engine and the TypeScript mirror in
// engine.ts: every case carries hand-written expectations AND the canonical snapshot the C#
// produced, and engine.fixtures.test.ts holds the mirror to both. The copy exists only so the
// Vitest suite reads it from inside this app; it is never edited here. Regenerate it on the
// backend (HOS_FIXTURES_UPDATE=1), then re-copy — the whole file, byte for byte, no header
// (JSON has no comment to carry one), into Dispatcher AND here.
//
// Same precedent as lib/inspectionForm.copy.test.ts, minus the two-line header. The repo root
// is resolved from this test file, so the comparison is against the backend's file on disk —
// not against Dispatcher's copy, which could drift in step with this one.

const REPO_ROOT = join(import.meta.dirname, "..", "..", "..");
const SOURCE = join(REPO_ROOT, "Backend", "tests", "NorthernLink.Shared.Tests", "HoursOfService", "hos-fixtures.json");
const COPY = join(import.meta.dirname, "hos-fixtures.json");

describe("lib/hos/hos-fixtures.json is a copy of the backend's", () => {
  it("is byte-identical to the backend fixture file", () => {
    // Read from disk, both of them, and compare as text — not parsed and compared as data. A
    // structural comparison would pass while whitespace or key order had diverged, and the
    // next regeneration would then produce a diff nobody can review.
    expect(readFileSync(COPY, "utf8")).toBe(readFileSync(SOURCE, "utf8"));
  });
});
