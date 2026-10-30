import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  addNewDefect,
  certifiedToday,
  discardDraft,
  draftKey,
  getDraft,
  getServerSnapshot,
  getSnapshot,
  CERTIFIED_STORE_VERSION,
  hasDraft,
  INSPECTION_STORE_VERSION,
  markSectionOk,
  recordCertification,
  removeNewDefect,
  setAnswer,
  setDefect,
  setLocation,
  setNewDefectsFound,
  setNote,
  setOdometer,
  setStep,
  startDraft,
  storageFailed,
  updateNewDefect,
  type InspectionDraft,
  type LocalCertification,
} from "./inspectionStore";
import { today } from "./data";
import { itemsFor, NL_PTI_01, RETIRED_KEYS, WITHDRAWN_KEYS } from "./inspectionForm";

// The DVIR draft store.
//
// What is being protected here is a driver's whole NL-PTI-01 walk-around — 53 to 64 answers on
// a pre-trip, and on a post-trip the six Close-Out answers plus the new defects found since the
// pre-trip (rev 4) — and the honesty of a compliance record.
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

/** Keys on each half of the form, for any unit — what the store's per-mode filter keeps. */
const PRE_KEYS = itemsFor(null, "PreTrip").flatMap((g) => g.items.map((i) => i.key));
const POST_KEYS = itemsFor(null, "PostTrip").flatMap((g) => g.items.map((i) => i.key));
/** A real post-trip row — since rev 4 that means a Close-Out row. */
const POST_ITEM = POST_KEYS[0];
/** The rev 2 post-trip's en-route row, WITHDRAWN in rev 4 with no replacement. */
const WITHDRAWN = "Defects noticed while driving";
/**
 * A row that rev 2 and rev 3 asked on BOTH halves and rev 4 made pre-trip-only — a stale
 * post-trip draft's likeliest leftover. "Tire condition" was a "Both" row through rev 3.
 */
const FORMER_BOTH = "Tire condition";

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
    location: "Lynn Lake",
    newDefectsFound: null,
    newDefects: [],
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
  defectItems: [],
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
    setAnswer("PostTrip", "VEH-11", POST_ITEM, "defect");

    expect(getDraft("PreTrip", "VEH-11")?.answers).toEqual({ [ITEM_A]: "pass" });
    expect(getDraft("PostTrip", "VEH-11")?.answers).toEqual({ [POST_ITEM]: "defect" });
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

  it("records the location as typed — untrimmed, because trimming happens at submit", () => {
    // Trimming per keystroke would eat the space a driver just typed after "Lynn".
    setLocation("PreTrip", "VEH-11", "Lynn ");
    expect(getDraft("PreTrip", "VEH-11")?.location).toBe("Lynn ");
  });

  it("starts a draft with an empty location, never a default one", () => {
    // A defaulted town would be a false statement on the report (Man. Reg. 95/2008 s.12(1)).
    expect(startDraft("PreTrip", "VEH-11").location).toBe("");
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
    expect(draft?.location).toBe("Lynn Lake");
    expect(draft?.stepId).toBe(`check:${ITEM_A}`);
    expect(storageFailed()).toBe(false);
  });

  it("reads a stored draft with no location as an empty one, not a missing field", () => {
    const { location: _omit, ...withoutLocation } = validDraft();
    void _omit;
    seed(draftKey("PreTrip", "VEH-11"), withoutLocation);
    expect(getDraft("PreTrip", "VEH-11")?.location).toBe("");
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

  it("discards a v2 draft — an answer sheet for NL-PTI-01 rev 1, with no location", () => {
    // Rev 2 moved rows between the halves and added the location. No key was renamed, so a v2
    // draft would LOAD — and that is the reason for discarding it rather than trusting the key
    // filter alone: it is answers to a different revision of a legal form.
    seed(draftKey("PostTrip", "VEH-11"), validDraft({ v: 2, mode: "PostTrip" }));

    expect(getDraft("PostTrip", "VEH-11")).toBeNull();
    expect(window.localStorage.getItem(draftKey("PostTrip", "VEH-11"))).toBeNull();
  });

  it("discards a v3 draft — rev 2/3's re-check post-trip — on BOTH halves", () => {
    // Rev 4 made every vehicle row pre-trip-only, withdrew "Defects noticed while driving" and
    // gave the post-trip its new-defects answer. Same reasoning as v2 → v3: rows moved between
    // the halves and the shape grew, so a v3 draft is answers to a different revision. The
    // pre-trip's rows did not change, but one version covers the store — it is discarded too,
    // not selectively kept.
    expect(INSPECTION_STORE_VERSION).toBe(4);
    seed(
      draftKey("PostTrip", "VEH-11"),
      validDraft({ v: 3, mode: "PostTrip", answers: { [FORMER_BOTH]: "pass", [WITHDRAWN]: "na" } }),
    );
    seed(draftKey("PreTrip", "VEH-11"), validDraft({ v: 3 }));

    expect(getDraft("PostTrip", "VEH-11")).toBeNull();
    expect(getDraft("PreTrip", "VEH-11")).toBeNull();
    expect(window.localStorage.getItem(draftKey("PostTrip", "VEH-11"))).toBeNull();
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

  it("shares no key between the two halves of the form since rev 4", () => {
    // The premise of the next test, pinned on its own: the post-trip checklist is Close-Out
    // alone, so a post-trip draft may hold nothing a pre-trip asks — and a new defect, which is
    // filed against a PRE-trip key, can never collide with a Close-Out checklist defect.
    expect(POST_KEYS.length).toBeGreaterThan(0);
    for (const key of POST_KEYS) expect(PRE_KEYS).not.toContain(key);
    expect(PRE_KEYS).toContain(FORMER_BOTH);
    expect(WITHDRAWN_KEYS.has(WITHDRAWN)).toBe(true);
  });

  it("drops every answer, note and defect for a row not on THIS half of the form", () => {
    // THE stale-draft case, even at the current version (a bump is the first line, this filter
    // the second): a post-trip draft holding a row rev 4 made pre-trip-only, and the withdrawn
    // en-route row. Both are known strings, so a catalogue-wide check alone would keep the
    // first — and a graded defect on it would be filed as a CHECKLIST row on a post-trip that
    // no longer asks the question.
    seed(
      draftKey("PostTrip", "VEH-11"),
      validDraft({
        mode: "PostTrip",
        answers: { [FORMER_BOTH]: "defect", [WITHDRAWN]: "defect", [POST_ITEM]: "na" },
        notes: { [FORMER_BOTH]: "from the old post-trip", [POST_ITEM]: "ok" },
        defects: {
          [FORMER_BOTH]: { severity: "Major", note: "from the old post-trip" },
          [WITHDRAWN]: { severity: "Minor", note: "noticed en route" },
        },
      }),
    );

    const draft = getDraft("PostTrip", "VEH-11");
    expect(draft?.answers).toEqual({ [POST_ITEM]: "na" });
    expect(draft?.notes).toEqual({ [POST_ITEM]: "ok" });
    expect(draft?.defects).toEqual({});
    // Every key that survives is on the current post-trip.
    for (const key of Object.keys(draft?.answers ?? {})) expect(POST_KEYS).toContain(key);
  });

  it("never invents an answer for a row the draft does not hold — a new row reads unanswered", () => {
    // The other half of a form revision: rows ADDED to the form must show as "Not answered"
    // and block Certify, never default to Ok. The store only ever filters; it never fills in.
    seed(draftKey("PreTrip", "VEH-11"), validDraft({ answers: { [ITEM_A]: "pass" } }));

    const draft = getDraft("PreTrip", "VEH-11");
    expect(Object.keys(draft?.answers ?? {})).toEqual([ITEM_A]);
    const unanswered = PRE_KEYS.filter((k) => draft?.answers[k] === undefined);
    expect(unanswered).toHaveLength(PRE_KEYS.length - 1);
  });

  it("drops a draft's RETIRED keys on load, and leaves their replacements blank", () => {
    // NL-PTI-01 rev 3 consolidated the non-NSC pre-trip rows (engine bay 10 → 3, remote/winter
    // kit 5 → 1, comms 4 → 1) and removed the duplicate interior-lights group. That changed no
    // shape, so rev 3 did not bump and a rev 2 draft kept loading on this filter alone. Rev 4's
    // bump (v4) has since discarded every such draft, but the filter is still the defence for
    // any CURRENT-version draft that holds a retired key, so it is pinned at the current
    // version. What it must guarantee:
    //   • no retired key survives load, so none can reach a payload;
    //   • the replacement row reads unanswered — an answer to "Engine oil" is NOT an answer to
    //     "Engine fluid levels", which also covers coolant, power steering and washer fluid.
    //     RETIRED_KEYS is display metadata, never a mapping to carry answers across;
    //   • every surviving key's answer, note and defect is kept.
    const retired = [...RETIRED_KEYS.keys()];
    const replacements = new Set(RETIRED_KEYS.values());
    expect(retired.length).toBeGreaterThan(0);
    for (const key of retired) expect(PRE_KEYS).not.toContain(key);
    for (const key of replacements) expect(PRE_KEYS).toContain(key);

    const kept = PRE_KEYS.filter((k) => !replacements.has(k));
    const rev2Answers: Record<string, string> = Object.fromEntries([
      ...retired.map((k) => [k, "pass"]),
      ...kept.map((k) => [k, "pass"]),
    ]);
    rev2Answers["Survival kit"] = "defect";
    seed(
      draftKey("PreTrip", "VEH-11"),
      validDraft({
        answers: rev2Answers as Record<string, "pass" | "defect">,
        notes: { "Engine oil": "topped up", [ITEM_B]: "fine" },
        defects: {
          "Survival kit": { severity: "Major", note: "no blankets" },
          "Interior: Brake lights": { severity: "Minor", note: "dim" },
        },
      }),
    );

    const draft = getDraft("PreTrip", "VEH-11");
    if (!draft) throw new Error("a current-version draft holding retired keys should still load");
    for (const key of retired) {
      expect(draft.answers[key]).toBeUndefined();
      expect(draft.notes[key]).toBeUndefined();
      expect(draft.defects[key]).toBeUndefined();
    }
    for (const key of replacements) expect(draft.answers[key]).toBeUndefined();
    expect(Object.keys(draft.answers).sort()).toEqual([...kept].sort());
    expect(draft.notes).toEqual({ [ITEM_B]: "fine" });
    expect(draft.defects).toEqual({});
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

describe("markSectionOk — the per-section All OK write", () => {
  // The shortcut's compliance rules are enforced HERE, at the one write, so they hold for any
  // caller. lib/inspectionSteps.test.ts pins when it is offered; Inspection.test.tsx the screen.
  const NL02_PRE = itemsFor("NL-02", "PreTrip");
  const CONTROLS = NL02_PRE.find((g) => g.key === "Controls & Instruments");
  if (!CONTROLS) throw new Error("no Controls & Instruments sub-group");
  const [C0, C1, C2, ...C_REST] = CONTROLS.items.map((i) => i.key);

  it("fills every blank row of the sub-group with Pass", () => {
    const draft = markSectionOk("PreTrip", "VEH-11", "NL-02", CONTROLS.key);
    for (const item of CONTROLS.items) expect(draft.answers[item.key]).toBe("pass");
    expect(Object.keys(draft.answers)).toHaveLength(CONTROLS.items.length);
  });

  it("never overwrites a Defect, an N/A or a Pass, and leaves the defect record and notes alone", () => {
    setAnswer("PreTrip", "VEH-11", C0, "defect");
    setDefect("PreTrip", "VEH-11", C0, { severity: "Major", note: "steering play" });
    setAnswer("PreTrip", "VEH-11", C1, "na");
    setNote("PreTrip", "VEH-11", C1, "not fitted");
    setAnswer("PreTrip", "VEH-11", C2, "pass");

    const draft = markSectionOk("PreTrip", "VEH-11", "NL-02", CONTROLS.key);
    expect(draft.answers[C0]).toBe("defect");
    expect(draft.defects[C0]).toEqual({ severity: "Major", note: "steering play" });
    expect(draft.answers[C1]).toBe("na");
    expect(draft.notes[C1]).toBe("not fitted");
    expect(draft.answers[C2]).toBe("pass");
    for (const key of C_REST) expect(draft.answers[key]).toBe("pass");
  });

  it("touches ONE sub-group only — every other row stays blank", () => {
    const draft = markSectionOk("PreTrip", "VEH-11", "NL-02", CONTROLS.key);
    const inGroup = new Set(CONTROLS.items.map((i) => i.key));
    for (const key of PRE_KEYS) {
      if (!inGroup.has(key)) expect(draft.answers[key]).toBeUndefined();
    }
  });

  it("narrows to the unit: on NL-01 it never marks an NL-02-only row", () => {
    const nl01Keys = new Set(itemsFor("NL-01", "PreTrip").flatMap((g) => g.items.map((i) => i.key)));
    const draft = markSectionOk("PreTrip", "VEH-11", "NL-01", CONTROLS.key);
    const marked = Object.keys(draft.answers);
    expect(marked.length).toBeGreaterThan(0);
    expect(marked.length).toBeLessThan(CONTROLS.items.length);
    for (const key of marked) expect(nl01Keys.has(key)).toBe(true);
  });

  it("fills nothing for an unknown group, or for a group not on this half of the form", () => {
    expect(markSectionOk("PreTrip", "VEH-11", "NL-02", "Everything").answers).toEqual({});
    // Close-Out is post-trip only.
    expect(NL_PTI_01.some((g) => g.key === "Close-Out")).toBe(true);
    expect(markSectionOk("PreTrip", "VEH-11", "NL-02", "Close-Out").answers).toEqual({});
  });

  it("leaves every filled row individually changeable afterwards", () => {
    markSectionOk("PreTrip", "VEH-11", "NL-02", CONTROLS.key);
    const draft = setAnswer("PreTrip", "VEH-11", C1, "defect");
    expect(draft.answers[C1]).toBe("defect");
    expect(draft.defects[C1]).toEqual({ severity: null, note: "" });
    expect(draft.answers[C0]).toBe("pass");
  });

  it("writes nothing but ordinary answers — no section marker anywhere in the draft", () => {
    // Nothing on the wire says "section passed", and nothing in storage does either: the filled
    // rows are indistinguishable from tapped ones.
    const draft = markSectionOk("PreTrip", "VEH-11", "NL-02", CONTROLS.key);
    expect(Object.keys(draft).sort()).toEqual(
      [
        "answers",
        "defects",
        "location",
        "mode",
        "newDefects",
        "newDefectsFound",
        "notes",
        "odometerKm",
        "startedAt",
        "startedOn",
        "stepId",
        "v",
        "vehicleId",
      ].sort(),
    );
    expect(new Set(Object.values(draft.answers))).toEqual(new Set(["pass"]));
  });
});

describe("the post-trip's new defects (rev 4)", () => {
  // "Any defect found after the pre-trip?" and the list of new defects, each filed against a
  // PRE-trip key. The rules lib/newDefects.test.ts pins are about what may be CERTIFIED; these
  // are about what the draft may HOLD, and that nothing in it is ever defaulted.

  it("starts unanswered and empty — never defaulted to No", () => {
    const draft = startDraft("PostTrip", "VEH-11");
    expect(draft.newDefectsFound).toBeNull();
    expect(draft.newDefects).toEqual([]);
  });

  it("seeds one BLANK row on Yes — no item, no severity, no note", () => {
    const draft = setNewDefectsFound("PostTrip", "VEH-11", true);
    expect(draft.newDefectsFound).toBe(true);
    expect(draft.newDefects).toEqual([{ itemKey: "", severity: null, note: "" }]);
  });

  it("does not seed a second row when Yes is tapped again", () => {
    setNewDefectsFound("PostTrip", "VEH-11", true);
    expect(setNewDefectsFound("PostTrip", "VEH-11", true).newDefects).toHaveLength(1);
  });

  it("keeps the entered rows on No — a mis-tap must not destroy them", () => {
    setNewDefectsFound("PostTrip", "VEH-11", true);
    updateNewDefect("PostTrip", "VEH-11", 0, { itemKey: FORMER_BOTH, severity: "Major" });
    const draft = setNewDefectsFound("PostTrip", "VEH-11", false);
    expect(draft.newDefectsFound).toBe(false);
    expect(draft.newDefects).toEqual([{ itemKey: FORMER_BOTH, severity: "Major", note: "" }]);
  });

  it("adds, patches and removes rows by index, and ignores an index that does not exist", () => {
    setNewDefectsFound("PostTrip", "VEH-11", true);
    addNewDefect("PostTrip", "VEH-11");
    updateNewDefect("PostTrip", "VEH-11", 1, { itemKey: "Horn", note: "Silent." });
    updateNewDefect("PostTrip", "VEH-11", 7, { itemKey: "Steering" });
    expect(getDraft("PostTrip", "VEH-11")?.newDefects).toEqual([
      { itemKey: "", severity: null, note: "" },
      { itemKey: "Horn", severity: null, note: "Silent." },
    ]);

    const draft = removeNewDefect("PostTrip", "VEH-11", 0);
    expect(draft.newDefects).toEqual([{ itemKey: "Horn", severity: null, note: "Silent." }]);
  });

  it("leaves the answer at Yes when the last row is removed — an empty list is not a No", () => {
    setNewDefectsFound("PostTrip", "VEH-11", true);
    const draft = removeNewDefect("PostTrip", "VEH-11", 0);
    expect(draft.newDefectsFound).toBe(true);
    expect(draft.newDefects).toEqual([]);
  });

  it("is a no-op on a pre-trip draft — a new defect can never ride a pre-trip", () => {
    expect(setNewDefectsFound("PreTrip", "VEH-11", true).newDefectsFound).toBeNull();
    expect(addNewDefect("PreTrip", "VEH-11").newDefects).toEqual([]);
  });

  it("survives a reload", () => {
    setNewDefectsFound("PostTrip", "VEH-11", true);
    updateNewDefect("PostTrip", "VEH-11", 0, {
      itemKey: FORMER_BOTH,
      severity: "Minor",
      note: "Sidewall scuff, left rear.",
    });
    const raw = window.localStorage.getItem(draftKey("PostTrip", "VEH-11"));
    window.localStorage.clear();
    window.localStorage.setItem(draftKey("PostTrip", "VEH-11"), raw ?? "");

    const draft = getDraft("PostTrip", "VEH-11");
    expect(draft?.newDefectsFound).toBe(true);
    expect(draft?.newDefects).toEqual([
      { itemKey: FORMER_BOTH, severity: "Minor", note: "Sidewall scuff, left rear." },
    ]);
  });

  it("drops a stored row whose item is not a pre-trip key, and keeps an unpicked one", () => {
    // A withdrawn key, a Close-Out key (post-trip half — not something a new defect is filed
    // against), a retired key and an invented one all go. "" is a row the driver has not
    // finished, which must stay so it can block Certify on screen.
    const retired = [...RETIRED_KEYS.keys()][0];
    seed(
      draftKey("PostTrip", "VEH-11"),
      validDraft({
        mode: "PostTrip",
        answers: {},
        newDefectsFound: true,
        newDefects: [
          { itemKey: WITHDRAWN, severity: "Minor", note: "x" },
          { itemKey: POST_ITEM, severity: "Minor", note: "x" },
          { itemKey: retired, severity: "Minor", note: "x" },
          { itemKey: "CHK-UH-1", severity: "Minor", note: "x" },
          { itemKey: "", severity: null, note: "half done" },
          { itemKey: FORMER_BOTH, severity: "Major", note: "kept" },
        ],
      }),
    );

    expect(getDraft("PostTrip", "VEH-11")?.newDefects).toEqual([
      { itemKey: "", severity: null, note: "half done" },
      { itemKey: FORMER_BOTH, severity: "Major", note: "kept" },
    ]);
  });

  it("reads a stored severity outside the form's two boxes as ungraded, never as a grade", () => {
    // "Out of Service" is wire vocabulary the form never offers; a garbage value is garbage.
    seed(
      draftKey("PostTrip", "VEH-11"),
      validDraft({
        mode: "PostTrip",
        answers: {},
        newDefectsFound: true,
        newDefects: [
          { itemKey: FORMER_BOTH, severity: "Out of Service" as never, note: "x" },
          { itemKey: "Horn", severity: "Catastrophic" as never, note: "y" },
        ],
      }),
    );
    expect(getDraft("PostTrip", "VEH-11")?.newDefects.map((d) => d.severity)).toEqual([
      null,
      null,
    ]);
  });

  it("forces a stored PRE-trip draft's new-defect fields empty", () => {
    seed(
      draftKey("PreTrip", "VEH-11"),
      validDraft({
        newDefectsFound: true,
        newDefects: [{ itemKey: FORMER_BOTH, severity: "Major", note: "x" }],
      }),
    );
    const draft = getDraft("PreTrip", "VEH-11");
    expect(draft?.newDefectsFound).toBeNull();
    expect(draft?.newDefects).toEqual([]);
  });
});

describe("certifications", () => {
  it("keeps the defect items a certification filed, across a reload", () => {
    // The post-trip reads a pre-trip certification's items as "already reported".
    recordCertification({ ...certification, defectCount: 1, defectItems: [FORMER_BOTH] });
    expect(certifiedToday("PreTrip", "VEH-11")?.defectItems).toEqual([FORMER_BOTH]);
  });

  it("reads an entry stored before rev 4 (no defectItems) as filing none, and keeps it", () => {
    const { defectItems: _omit, ...legacy } = certification;
    void _omit;
    seed("nl.driverfield.inspectionCertified", { v: 2, items: [legacy] });
    expect(certifiedToday("PreTrip", "VEH-11")?.defectItems).toEqual([]);
  });

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

  it("keeps today's certifications across the DRAFT version bump", () => {
    // The two versions are independent on purpose: bumping the draft for a form revision must
    // not drop a certification made this morning and re-block boarding.
    expect(CERTIFIED_STORE_VERSION).toBe(2);
    seed("nl.driverfield.inspectionCertified", { v: 2, items: [certification] });
    expect(certifiedToday("PreTrip", "VEH-11")?.commandId).toBe(certification.commandId);
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
