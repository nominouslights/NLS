import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  certifiedToday,
  discardDraft,
  draftKey,
  getDraft,
  getServerSnapshot,
  getSnapshot,
  hasDraft,
  INSPECTION_STORE_VERSION,
  recordCertification,
  setAnswer,
  setDefect,
  setNote,
  setOdometer,
  setStep,
  startDraft,
  storageFailed,
  type InspectionDraft,
  type LocalCertification,
} from "./inspectionStore";
import { today } from "./data";
import { itemsFor } from "./inspectionForm";

// The DVIR draft store.
//
// What is being protected here is a driver's whole NL-PTI-01 walk-around — 67 to 80 answers
// depending on the unit and the half of the form — and the honesty of a compliance record.
// Every test below is a failure mode somebody would otherwise hit in a vehicle: yesterday's
// pre-trip resuming into today's certification, a vehicle reassignment silently re-attributing
// answers, a retired form row resurrecting from storage into a payload, or storage quietly
// failing while the screen says the draft is saved.
//
// NOTE ON ORDER: the hostile-storage block runs LAST on purpose. storageFailed() is a sticky
// module flag — once storage has failed, this session says so forever — so a test that trips it
// cannot run before the tests that assert it is clear.

// Real NL-PTI-01 wire keys, taken from the catalogue rather than typed out: the key is the
// value stored as InspectionChecklistItem.Item, and a made-up one would be dropped on load by
// the very validation several of these tests are about.
const NL01_PRE = itemsFor("NL-01", "PreTrip");
const ITEM_A = NL01_PRE[0].items[0].key;
const ITEM_B = NL01_PRE[1].items[0].key;

function seed(key: string, value: unknown): void {
  window.localStorage.setItem(key, JSON.stringify(value));
}

function validDraft(over: Partial<InspectionDraft> = {}): InspectionDraft {
  return {
    v: INSPECTION_STORE_VERSION,
    mode: "PreTrip",
    vehicleId: "VEH-11",
    startedOn: today,
    startedAt: `${today}T06:02:00.000Z`,
    answers: { [ITEM_A]: "pass" },
    notes: {},
    defects: {},
    odometerKm: 184_930,
    stepId: `check:${ITEM_A}`,
    ...over,
  };
}

const certification: LocalCertification = {
  commandId: "11111111-2222-3333-4444-555555555555",
  mode: "PreTrip",
  vehicleId: "VEH-11",
  onDate: today,
  certifiedAt: `${today}T06:12:00.000Z`,
  result: "Pass",
  defectCount: 0,
  outOfService: false,
};

beforeEach(() => {
  window.localStorage.clear();
});

describe("draftKey", () => {
  it("pins the nl.driverfield.* convention lib/auth.ts established", () => {
    // Same prefix as REFRESH_TOKEN_KEY, because the apps share a hostname under path prefixes
    // in the DigitalOcean image — an unprefixed key would collide with Dispatcher's.
    expect(draftKey("PreTrip", "VEH-11")).toBe("nl.driverfield.inspectionDraft.PreTrip.VEH-11");
    expect(draftKey("PostTrip", "VEH-16")).toBe(
      "nl.driverfield.inspectionDraft.PostTrip.VEH-16",
    );
  });
});

describe("draft isolation", () => {
  it("keeps a PreTrip and a PostTrip draft for one vehicle apart", () => {
    setAnswer("PreTrip", "VEH-11", ITEM_A, "pass");
    setAnswer("PostTrip", "VEH-11", ITEM_B, "defect");

    expect(getDraft("PreTrip", "VEH-11")?.answers).toEqual({ [ITEM_A]: "pass" });
    expect(getDraft("PostTrip", "VEH-11")?.answers).toEqual({ [ITEM_B]: "defect" });
  });

  it("never returns another vehicle's draft", () => {
    // A mid-shift reassignment must orphan the old draft, not re-attribute 22 answers about
    // one vehicle to a different one.
    setAnswer("PreTrip", "VEH-14", ITEM_A, "pass");
    expect(getDraft("PreTrip", "VEH-11")).toBeNull();
    expect(getDraft("PreTrip", "VEH-14")).not.toBeNull();
  });

  it("discardDraft removes exactly one key", () => {
    startDraft("PreTrip", "VEH-11");
    startDraft("PostTrip", "VEH-11");

    discardDraft("PreTrip", "VEH-11");

    expect(window.localStorage.getItem(draftKey("PreTrip", "VEH-11"))).toBeNull();
    expect(window.localStorage.getItem(draftKey("PostTrip", "VEH-11"))).not.toBeNull();
  });
});

describe("mutations", () => {
  it("records answers, defects, odometer and the step pointer", () => {
    setOdometer("PreTrip", "VEH-11", 184_930);
    setAnswer("PreTrip", "VEH-11", ITEM_A, "defect");
    setDefect("PreTrip", "VEH-11", ITEM_A, { severity: "Major", note: "Tread down to bars." });
    setStep("PreTrip", "VEH-11", `defect:${ITEM_A}`);

    const draft = getDraft("PreTrip", "VEH-11");
    expect(draft?.odometerKm).toBe(184_930);
    expect(draft?.answers[ITEM_A]).toBe("defect");
    expect(draft?.defects[ITEM_A]).toEqual({ severity: "Major", note: "Tread down to bars." });
    expect(draft?.stepId).toBe(`defect:${ITEM_A}`);
  });

  it("seeds an empty defect record the moment an item is answered 'defect'", () => {
    setAnswer("PreTrip", "VEH-11", ITEM_A, "defect");
    expect(getDraft("PreTrip", "VEH-11")?.defects[ITEM_A]).toEqual({
      severity: null,
      note: "",
    });
  });

  it("prunes the defect when the answer moves away from 'defect'", () => {
    // In the same mutation, so buildSteps drops the injected follow-up and no orphan defect can
    // reach the payload. A defect on an item since marked Pass is a false compliance entry.
    setAnswer("PreTrip", "VEH-11", ITEM_A, "defect");
    setDefect("PreTrip", "VEH-11", ITEM_A, { severity: "Minor", note: "weep" });
    setAnswer("PreTrip", "VEH-11", ITEM_A, "pass");

    expect(getDraft("PreTrip", "VEH-11")?.defects).toEqual({});
  });

  it("prunes on 'na' as well as 'pass'", () => {
    setAnswer("PreTrip", "VEH-11", ITEM_A, "defect");
    setAnswer("PreTrip", "VEH-11", ITEM_A, "na");
    expect(getDraft("PreTrip", "VEH-11")?.defects).toEqual({});
  });

  it("records a per-row note independently of the answer", () => {
    // NL-PTI-01 has a Notes column on EVERY row, so an Ok or an N/A can carry a remark without
    // being a defect. It rides the wire as ChecklistItemInput.Note.
    setAnswer("PreTrip", "VEH-11", ITEM_A, "na");
    setNote("PreTrip", "VEH-11", ITEM_A, "Not fitted to this unit.");

    const draft = getDraft("PreTrip", "VEH-11");
    expect(draft?.answers[ITEM_A]).toBe("na");
    expect(draft?.notes[ITEM_A]).toBe("Not fitted to this unit.");
    expect(draft?.defects).toEqual({});
  });

  it("does NOT prune the note when the answer changes, unlike the defect", () => {
    // The asymmetry is deliberate. A defect on an item since marked Pass is a false entry in a
    // compliance record; a note is the driver's own words about the row and survives them
    // changing their mind about the box.
    setAnswer("PreTrip", "VEH-11", ITEM_A, "defect");
    setNote("PreTrip", "VEH-11", ITEM_A, "Weeping at the seam.");
    setDefect("PreTrip", "VEH-11", ITEM_A, { severity: "Major", note: "x" });
    setAnswer("PreTrip", "VEH-11", ITEM_A, "pass");

    const draft = getDraft("PreTrip", "VEH-11");
    expect(draft?.defects).toEqual({});
    expect(draft?.notes[ITEM_A]).toBe("Weeping at the seam.");
  });

  it("starts a draft on the first mutation, so no caller has to remember to", () => {
    expect(hasDraft("PreTrip", "VEH-11")).toBe(false);
    setAnswer("PreTrip", "VEH-11", ITEM_A, "pass");
    expect(hasDraft("PreTrip", "VEH-11")).toBe(true);
  });
});

describe("reload", () => {
  it("reads a draft written by a previous page load", () => {
    // What a reload actually does: the module cache is gone and the only survivor is the
    // storage key. Seeded directly rather than through a test-only reset export.
    seed(draftKey("PreTrip", "VEH-11"), validDraft());

    const draft = getDraft("PreTrip", "VEH-11");
    expect(draft?.answers[ITEM_A]).toBe("pass");
    expect(draft?.odometerKm).toBe(184_930);
    expect(draft?.stepId).toBe(`check:${ITEM_A}`);
    expect(storageFailed()).toBe(false);
  });

  it("survives a mutation, a reseed and a re-read without serving a stale cache", () => {
    setAnswer("PreTrip", "VEH-11", ITEM_A, "pass");
    seed(draftKey("PreTrip", "VEH-11"), validDraft({ answers: { [ITEM_B]: "na" } }));
    expect(getDraft("PreTrip", "VEH-11")?.answers).toEqual({ [ITEM_B]: "na" });
  });
});

describe("draft rejection", () => {
  it("discards a draft from an older store version, and removes the key", () => {
    // Discarded, never migrated — a half-migrated legal attestation is worse than re-answering.
    // v1 is the concrete case: its answers are keyed by the invented 22-item list's ids, which
    // address rows form NL-PTI-01 does not have. There is no correspondence to migrate.
    seed(draftKey("PreTrip", "VEH-11"), validDraft({ v: 1 }));

    expect(getDraft("PreTrip", "VEH-11")).toBeNull();
    expect(window.localStorage.getItem(draftKey("PreTrip", "VEH-11"))).toBeNull();
  });

  it("discards yesterday's draft, and removes the key", () => {
    // THE compliance one: yesterday's pre-trip must never resume into today's certification.
    seed(draftKey("PreTrip", "VEH-11"), validDraft({ startedOn: "2026-09-11" }));

    expect(getDraft("PreTrip", "VEH-11")).toBeNull();
    expect(window.localStorage.getItem(draftKey("PreTrip", "VEH-11"))).toBeNull();
  });

  it("returns null for corrupt JSON without throwing, and without claiming storage failed", () => {
    window.localStorage.setItem(draftKey("PreTrip", "VEH-11"), "{not json");

    expect(() => getDraft("PreTrip", "VEH-11")).not.toThrow();
    expect(getDraft("PreTrip", "VEH-11")).toBeNull();
    expect(storageFailed()).toBe(false);
  });

  it("discards a draft whose stored mode or vehicle disagrees with the key", () => {
    seed(draftKey("PreTrip", "VEH-11"), validDraft({ mode: "PostTrip" }));
    expect(getDraft("PreTrip", "VEH-11")).toBeNull();

    seed(draftKey("PreTrip", "VEH-11"), validDraft({ vehicleId: "VEH-16" }));
    expect(getDraft("PreTrip", "VEH-11")).toBeNull();
  });

  it("drops answers, notes and defects for item keys that are not in the catalogue", () => {
    // A row retired from the form must not be able to resurrect from storage into a compliance
    // payload — the exact hazard of replacing a 22-item list with an 80-row one. The whole
    // draft is kept; only the unknown rows go.
    seed(
      draftKey("PreTrip", "VEH-11"),
      validDraft({
        answers: { [ITEM_A]: "pass", "CHK-UH-1": "pass" },
        notes: { [ITEM_A]: "topped up", "CHK-UH-1": "from a previous build" },
        defects: { "CHK-UH-1": { severity: "Major", note: "from a previous build" } },
      }),
    );

    const draft = getDraft("PreTrip", "VEH-11");
    expect(draft?.answers).toEqual({ [ITEM_A]: "pass" });
    expect(draft?.notes).toEqual({ [ITEM_A]: "topped up" });
    expect(draft?.defects).toEqual({});
  });

  it("drops an answer or severity that is not one of the known literals", () => {
    seed(
      draftKey("PreTrip", "VEH-11"),
      validDraft({
        answers: { [ITEM_A]: "maybe" as never, [ITEM_B]: "pass" },
        defects: { [ITEM_B]: { severity: "Catastrophic" as never, note: "x" } },
      }),
    );

    const draft = getDraft("PreTrip", "VEH-11");
    expect(draft?.answers).toEqual({ [ITEM_B]: "pass" });
    // The defect row survives but ungraded, so the review step can still demand a severity
    // rather than silently submitting a made-up one.
    expect(draft?.defects[ITEM_B]).toEqual({ severity: null, note: "x" });
  });
});

describe("certifications", () => {
  it("returns a certification for the mode, vehicle and day it was made for", () => {
    recordCertification(certification);

    expect(certifiedToday("PreTrip", "VEH-11")?.commandId).toBe(certification.commandId);
    expect(certifiedToday("PreTrip", "VEH-11")?.result).toBe("Pass");
  });

  it("does not answer for the other mode, another vehicle, or another day", () => {
    recordCertification(certification);

    expect(certifiedToday("PostTrip", "VEH-11")).toBeNull();
    expect(certifiedToday("PreTrip", "VEH-14")).toBeNull();
    expect(certifiedToday("PreTrip", "VEH-11", "2026-09-13")).toBeNull();
  });

  it("survives a reload", () => {
    // The reason this is persisted rather than in memory: a driver who certifies a pre-trip and
    // then reloads the tab must not be blocked from boarding again. lib/sync/queue.ts's queue
    // is in-memory and would lose it.
    recordCertification(certification);
    expect(window.localStorage.getItem("nl.driverfield.inspectionCertified")).not.toBeNull();
    expect(certifiedToday("PreTrip", "VEH-11")).not.toBeNull();
  });

  it("returns the most recent when a mode is certified twice in a day", () => {
    recordCertification(certification);
    recordCertification({
      ...certification,
      commandId: "99999999-2222-3333-4444-555555555555",
      result: "Fail",
      defectCount: 1,
      outOfService: true,
      certifiedAt: `${today}T18:40:00.000Z`,
    });

    expect(certifiedToday("PreTrip", "VEH-11")?.result).toBe("Fail");
  });

  it("ignores a stored certification list from an older store version", () => {
    seed("nl.driverfield.inspectionCertified", { v: 0, items: [certification] });
    expect(certifiedToday("PreTrip", "VEH-11")).toBeNull();
  });

  it("ignores corrupt certification storage without throwing", () => {
    window.localStorage.setItem("nl.driverfield.inspectionCertified", "]]not json");
    expect(() => certifiedToday("PreTrip", "VEH-11")).not.toThrow();
    expect(certifiedToday("PreTrip", "VEH-11")).toBeNull();
  });
});

describe("the React seam", () => {
  it("gives the server a snapshot the client can never produce", () => {
    // getServerSnapshot is used for the server render AND the hydrating client render, so a
    // caller can gate its localStorage read on `!== null` and never mismatch. The client value
    // is a number, so the two can never collide.
    expect(getServerSnapshot()).toBeNull();
    expect(typeof getSnapshot()).toBe("number");
  });

  it("changes the snapshot on every mutation", () => {
    const before = getSnapshot();
    setAnswer("PreTrip", "VEH-11", ITEM_A, "pass");
    expect(getSnapshot()).not.toBe(before);
  });
});

// LAST — storageFailed() is sticky, so this cannot run before the tests above.
describe("hostile storage", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("never throws, and says so instead of lying", () => {
    // A tablet in a private window, or with site data blocked, or over quota. A driver mid-DVIR
    // must not see a thrown error from a convenience feature — but the screen must not claim
    // the draft is saved either. Silent-and-honest, never silent-and-lying.
    // Storage.prototype, not the instance: jsdom's `localStorage` is a Proxy, and spying on
    // the object itself is silently ignored — the stub never runs and the test passes for the
    // wrong reason.
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("QuotaExceededError");
    });
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("SecurityError");
    });
    vi.spyOn(Storage.prototype, "removeItem").mockImplementation(() => {
      throw new Error("SecurityError");
    });

    expect(() => startDraft("PreTrip", "VEH-11")).not.toThrow();
    expect(() => setAnswer("PreTrip", "VEH-11", ITEM_A, "pass")).not.toThrow();
    expect(() => getDraft("PreTrip", "VEH-11")).not.toThrow();
    expect(() => recordCertification(certification)).not.toThrow();
    expect(() => certifiedToday("PreTrip", "VEH-11")).not.toThrow();
    expect(() => discardDraft("PreTrip", "VEH-11")).not.toThrow();

    expect(storageFailed()).toBe(true);
  });
});
