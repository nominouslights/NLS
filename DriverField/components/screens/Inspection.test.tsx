import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import Inspection from "./Inspection";
import {
  certifiedToday,
  draftKey,
  getDraft,
  INSPECTION_STORE_VERSION,
  recordCertification,
} from "@/lib/inspectionStore";
import { checkCount, itemsFor, NL_PTI_01, NL_PTI_01_CERTIFICATION } from "@/lib/inspectionForm";
import { today } from "@/lib/data";
import type { CheckState } from "@/lib/types";

// The DVIR wizard, end to end through the DOM, over form NL-PTI-01.
//
// lib/inspectionSteps.test.ts proves the step model and lib/inspectionStore.test.ts the draft;
// this proves the screen actually applies them, and it pins the payload rules that no type can
// enforce:
//
//   • `result` is NOT sent — the server derives it (VehicleInspection.DeriveResult).
//   • a row is addressed by its catalogue KEY, never its label. The key is the wire value
//     (InspectionChecklistItem.Item) and half of the address a defect is filed against; several
//     labels deliberately differ from their key.
//   • an N/A row is CARRIED, with `state: "NotApplicable"` — it used to be omitted, because
//     `Passed` was a bare bool and `passed: true` for an unapplicable row is a false
//     attestation. ChecklistItemState landed, so nothing is dropped and there is no `naItems`.
//   • `passed` is derived the way the aggregate re-derives it (`state !== Defect`).
//   • `certificationStatement` is the catalogue's sentence, verbatim — what was actually signed.
//   • `source` is "DriverApp", explicitly, because the backend defaults it to Dispatcher.
//   • `location` is sent, trimmed, and required — Man. Reg. 95/2008 s.12(1) — even though the
//     wire field is nullable (InspectionRequest.Location).
//   • a row the draft holds but THIS form does not ask is never sent and never blocks Certify.
//
// And the one behavioural guarantee that costs a driver the whole walk-around if it breaks: if
// enqueue throws, the draft survives.

const { enqueue } = vi.hoisted(() => ({
  enqueue: vi.fn<(kind: string, payload: unknown) => Promise<string>>(),
}));

// The assigned vehicle is NL-01 in lib/data.ts. Since rev 3 every NL-01 row has key === label,
// so the "key, never label" pin needs an NL-02 form to bite on — this lets one describe block
// re-point the unit without touching the mock data. Read at render time, so null = the real one.
const { unitOverride } = vi.hoisted(() => ({ unitOverride: { value: null as string | null } }));

vi.mock("@/lib/data", async (importOriginal) => {
  const real = await importOriginal<typeof import("@/lib/data")>();
  return {
    ...real,
    assignedVehicle: {
      ...real.assignedVehicle,
      get unit() {
        return unitOverride.value ?? real.assignedVehicle.unit;
      },
    },
  };
});

vi.mock("@/lib/sync/queue", () => ({
  enqueue,
  pending: () => [],
  drain: async () => {},
  retry: async () => {},
}));

type ChecklistRow = {
  group: string;
  item: string;
  passed: boolean;
  state: string;
  note: string | null;
};

type Payload = {
  type: string;
  source: string;
  odometerKm: number | null;
  location: string | null;
  checklist: ChecklistRow[];
  defects: { item: string; severity: string; note: string | null }[];
  certificationStatement: string;
};

// The assigned vehicle is VEH-11 / NL-01, and Inspection.tsx opens on the pre-trip half.
const ITEM_COUNT = checkCount("NL-01", "PreTrip");
const ITEMS = itemsFor("NL-01", "PreTrip").flatMap((g) => g.items);
const FIRST = ITEMS[0];
const CATALOGUE_KEYS = new Set(NL_PTI_01.flatMap((g) => g.items.map((i) => i.key)));

/**
 * NL-02's rows, for the one block that renders an NL-02 form. Since rev 3 retired the four
 * "Interior: " rows, the rows whose KEY and LABEL differ are the NL-02 ones with a "(NL-02)"
 * suffix on the label only — e.g. "Passenger seats (NL-02)" whose key is "Passenger seats". If the
 * screen ever sent labels, these are the rows that would be filed at an address the Dispatch
 * Console cannot resolve.
 */
const NL02_ITEMS = itemsFor("NL-02", "PreTrip").flatMap((g) => g.items);
const SUFFIXED = NL02_ITEMS.filter((i) => i.key !== i.label);

beforeEach(() => {
  unitOverride.value = null;
  window.localStorage.clear();
  enqueue.mockReset();
  enqueue.mockResolvedValue("cmd-1");
});

afterEach(() => {
  cleanup();
});

function renderWizard() {
  return render(<Inspection mode="PreTrip" />);
}

/** Fills the header step — odometer and location — and continues past it. */
function setOdometer(km: string, location = "Lynn Lake") {
  fireEvent.change(screen.getByLabelText(/Odometer reading/), { target: { value: km } });
  fireEvent.change(screen.getByLabelText("Location (town or highway)"), {
    target: { value: location },
  });
  fireEvent.click(screen.getByRole("button", { name: /^Continue/ }));
}

function continueButton(): HTMLButtonElement {
  return screen.getByRole("button", { name: /^Continue/ }) as HTMLButtonElement;
}

/** Taps one answer tile on the current check step. */
function tap(answer: "Pass" | "Defect" | "N/A") {
  fireEvent.click(screen.getByRole("button", { name: new RegExp(`^${answer.replace("/", "\\/")}`) }));
}

function payload(): Payload {
  expect(enqueue).toHaveBeenCalledWith("dvir.submit", expect.anything());
  return enqueue.mock.calls[0][1] as Payload;
}

/**
 * A complete draft, written straight to storage — what a reload of a finished walk-around
 * actually leaves behind.
 *
 * WHY NOT TAP 53 TILES IN EVERY PAYLOAD TEST. One full walk through the DOM is worth having and
 * there is one below; eight of them cost a minute of suite time to re-prove the same navigation
 * and prove nothing about the payload. Seeding the key directly is the same technique
 * lib/inspectionStore.test.ts uses for a reload, and it leaves the submit path — enqueue →
 * recordCertification → discardDraft, read back through getDraft — completely untouched.
 */
function seedComplete(over: {
  answers?: Record<string, CheckState>;
  notes?: Record<string, string>;
  defects?: Record<string, { severity: string; note: string }>;
  location?: string;
  items?: { key: string }[];
} = {}) {
  const answers: Record<string, CheckState> = Object.fromEntries(
    (over.items ?? ITEMS).map((i) => [i.key, "pass" as CheckState]),
  );
  window.localStorage.setItem(
    draftKey("PreTrip", "VEH-11"),
    JSON.stringify({
      v: INSPECTION_STORE_VERSION,
      mode: "PreTrip",
      vehicleId: "VEH-11",
      startedOn: today,
      startedAt: `${today}T06:02:00.000Z`,
      answers: { ...answers, ...(over.answers ?? {}) },
      notes: over.notes ?? {},
      defects: over.defects ?? {},
      odometerKm: 184_920,
      location: over.location ?? "Lynn Lake",
      stepId: "review",
    }),
  );
}

function certify() {
  fireEvent.click(screen.getByRole("button", { name: /Certify & submit/i }));
}

describe("the wizard", () => {
  it("opens on the odometer step, with a MockTag and a progress line", () => {
    renderWizard();
    expect(screen.getByText("What does the odometer read?")).toBeTruthy();
    expect(screen.getByText("MOCK")).toBeTruthy();
    expect(screen.getByText("Odometer & location")).toBeTruthy();
  });

  it("asks for the location on the header step, and blocks Continue until it is filled", () => {
    renderWizard();
    fireEvent.change(screen.getByLabelText(/Odometer reading/), { target: { value: "184920" } });

    // A good odometer alone is not enough.
    expect(continueButton().disabled).toBe(true);
    expect(screen.getByText(/the report must say where the inspection was done/)).toBeTruthy();

    // Whitespace is blank.
    fireEvent.change(screen.getByLabelText("Location (town or highway)"), {
      target: { value: "   " },
    });
    expect(continueButton().disabled).toBe(true);

    fireEvent.change(screen.getByLabelText("Location (town or highway)"), {
      target: { value: "Lynn Lake" },
    });
    expect(continueButton().disabled).toBe(false);
    expect(getDraft("PreTrip", "VEH-11")?.location).toBe("Lynn Lake");
  });

  it("blocks Continue over 200 characters, with the reason on screen", () => {
    renderWizard();
    fireEvent.change(screen.getByLabelText(/Odometer reading/), { target: { value: "184920" } });
    fireEvent.change(screen.getByLabelText("Location (town or highway)"), {
      target: { value: "a".repeat(201) },
    });

    expect(continueButton().disabled).toBe(true);
    expect(screen.getByText("That location is too long.")).toBeTruthy();

    fireEvent.change(screen.getByLabelText("Location (town or highway)"), {
      target: { value: "a".repeat(200) },
    });
    expect(continueButton().disabled).toBe(false);
  });

  it("blocks Continue on a rolled-back odometer, with the reason on screen", () => {
    renderWizard();
    fireEvent.change(screen.getByLabelText(/Odometer reading/), { target: { value: "1000" } });

    expect((screen.getByRole("button", { name: /^Continue/ }) as HTMLButtonElement).disabled).toBe(
      true,
    );
    expect(screen.getByText(/cannot be lower than the last recorded/)).toBeTruthy();
  });

  it("shows one question at a time and counts out of the derived denominator", () => {
    // 53 for NL-01 pre-trip (rev 3). Written through checkCount() rather than as a literal, because a
    // literal here is the same bug the step model was rewritten to remove.
    renderWizard();
    setOdometer("184920");

    expect(screen.getByText(FIRST.label)).toBeTruthy();
    expect(screen.getByText(`Check 1 of ${ITEM_COUNT}`)).toBeTruthy();

    tap("Pass");
    expect(screen.getByText(`Check 2 of ${ITEM_COUNT}`)).toBeTruthy();
    expect(screen.queryByText(FIRST.label)).toBeNull();
  });

  it("shows the form's Check For text and the area + sub-group line", () => {
    // The mitigation for a walk-around of up to 64 rows. "Ground beneath the vehicle" is not a
    // question a driver can answer without its Check For column.
    renderWizard();
    setOdometer("184920");

    expect(screen.getByText(FIRST.checkFor)).toBeTruthy();
    expect(screen.getByText(/^Area A · /)).toBeTruthy();
  });

  it("injects a follow-up on Defect, under the parent's number", () => {
    renderWizard();
    setOdometer("184920");
    tap("Pass"); // item 1
    tap("Defect"); // item 2 → follow-up

    expect(screen.getByText(`Follow-up · check 2 of ${ITEM_COUNT}`)).toBeTruthy();
    expect(
      (screen.getByRole("button", { name: /^Continue/ }) as HTMLButtonElement).disabled,
    ).toBe(true);

    fireEvent.click(screen.getByRole("button", { name: /^Major/ }));
    expect(
      (screen.getByRole("button", { name: /^Continue/ }) as HTMLButtonElement).disabled,
    ).toBe(false);
  });

  it("offers Minor and Major only — NL-PTI-01 has no third box", () => {
    // "Out of Service" stays in DefectSeverity and severityToWire for historical rows, but the
    // form does not offer it: under NSC 13 a Major IS the out-of-service condition.
    renderWizard();
    setOdometer("184920");
    tap("Defect");

    expect(screen.getByRole("button", { name: /^Minor/ })).toBeTruthy();
    expect(screen.getByRole("button", { name: /^Major/ })).toBeTruthy();
    expect(screen.queryByRole("button", { name: /^Out of Service/ })).toBeNull();
  });

  it("drops the follow-up and the defect when the answer flips back to Pass", () => {
    renderWizard();
    setOdometer("184920");
    tap("Defect");
    fireEvent.click(screen.getByRole("button", { name: /^Minor/ }));

    // Back onto the check, then change the answer.
    fireEvent.click(screen.getByRole("button", { name: /Back/ }));
    tap("Pass");

    expect(screen.queryByText(/Follow-up/)).toBeNull();
    expect(getDraft("PreTrip", "VEH-11")?.defects).toEqual({});
  });

  it("keeps a per-row note, which an Ok or N/A row may carry", () => {
    renderWizard();
    setOdometer("184920");
    fireEvent.change(screen.getByLabelText(`Note about ${FIRST.label}`), {
      target: { value: "Topped up before departure." },
    });
    tap("Pass");

    expect(getDraft("PreTrip", "VEH-11")?.notes[FIRST.key]).toBe("Topped up before departure.");
  });

  it(
    "walks the whole form, one question at a time, and reaches the attestation",
    () => {
      // THE full-walk test, and deliberately the only one: 53 taps through jsdom is slow, so
      // the payload tests below seed a finished draft instead. This is what proves the wizard
      // actually gets from question 1 to the attestation without a dead end.
      renderWizard();
      setOdometer("184920");
      for (let i = 0; i < ITEM_COUNT; i += 1) tap("Pass");

      expect(screen.getByText(`Review · ${ITEM_COUNT} of ${ITEM_COUNT}`)).toBeTruthy();
      expect(screen.getByText(NL_PTI_01_CERTIFICATION)).toBeTruthy();
      expect(
        (screen.getByRole("button", { name: /Certify & submit/i }) as HTMLButtonElement).disabled,
      ).toBe(false);
      expect(Object.keys(getDraft("PreTrip", "VEH-11")?.answers ?? {})).toHaveLength(ITEM_COUNT);
    },
    30_000,
  );

  it("keeps the draft across an unmount, which is what navigating away does", () => {
    const first = renderWizard();
    setOdometer("184920");
    tap("Pass");
    first.unmount();

    renderWizard();
    // Resumed on the step pointer, not on step 1 — and the pointer is an id, so an injected
    // defect step could not have shifted it.
    expect(screen.getByText(`Check 2 of ${ITEM_COUNT}`)).toBeTruthy();
    expect(screen.queryByLabelText(/Odometer reading/)).toBeNull();
    expect(getDraft("PreTrip", "VEH-11")?.odometerKm).toBe(184_920);
    expect(getDraft("PreTrip", "VEH-11")?.location).toBe("Lynn Lake");
  });
});

describe("the submit payload", () => {
  it("goes through the queue with every wire string spelled correctly", () => {
    seedComplete();
    renderWizard();
    certify();
    const p = payload();

    expect(p.type).toBe("PreTrip");
    expect(p.source).toBe("DriverApp");
    expect(p.odometerKm).toBe(184_920);
    expect(p.location).toBe("Lynn Lake");
    expect(p.checklist).toHaveLength(ITEM_COUNT);
    expect(p.checklist.every((c) => c.state === "Ok" && c.passed)).toBe(true);
    expect(p.checklist.every((c) => c.group.trim().length > 0)).toBe(true);
  });

  it("addresses every row by its catalogue KEY, never its label", () => {
    // Rendered as NL-02, the form that still has rows whose label differs from their key (the
    // "(NL-02)" suffixes). Sending labels would file those rows, and any defect on them, at an
    // address the Dispatch Console cannot resolve.
    unitOverride.value = "NL-02";
    seedComplete({ items: NL02_ITEMS });
    renderWizard();
    certify();
    const p = payload();

    expect(p.checklist.every((c) => CATALOGUE_KEYS.has(c.item))).toBe(true);
    expect(p.checklist).toHaveLength(checkCount("NL-02", "PreTrip"));
    expect(new Set(p.checklist.map((c) => c.item)).size).toBe(checkCount("NL-02", "PreTrip"));

    expect(SUFFIXED.length).toBeGreaterThan(0);
    for (const row of SUFFIXED) {
      expect(p.checklist.some((c) => c.item === row.key)).toBe(true);
      expect(p.checklist.some((c) => c.item === row.label)).toBe(false);
    }
  });

  it("sends the certification statement verbatim, and no derived result", () => {
    seedComplete();
    renderWizard();
    certify();

    expect(payload().certificationStatement).toBe(NL_PTI_01_CERTIFICATION);
    expect(payload()).not.toHaveProperty("result");
  });

  it("does not send `enteredBy` or a recurrence pointer", () => {
    seedComplete();
    renderWizard();
    certify();

    expect(payload()).not.toHaveProperty("enteredBy");
    expect(payload().defects).toEqual([]);
  });

  it("CARRIES an N/A row as NotApplicable rather than omitting it", () => {
    // The old behaviour omitted it and named it in a client-only `naItems` field, because
    // `Passed` was a bare bool. The tri-state landed; nothing is dropped, and `naItems` is gone.
    seedComplete({ answers: { [FIRST.key]: "na" } });
    renderWizard();
    certify();
    const p = payload();

    expect(p.checklist).toHaveLength(ITEM_COUNT);
    const row = p.checklist.find((c) => c.item === FIRST.key);
    expect(row?.state).toBe("NotApplicable");
    // `passed: true` is only honest BECAUSE `state` travels with it.
    expect(row?.passed).toBe(true);
    expect(p).not.toHaveProperty("naItems");
  });

  it("carries the row's own note alongside its state, trimmed, empty as null", () => {
    seedComplete({ answers: { [FIRST.key]: "na" }, notes: { [FIRST.key]: "  Spare carried.  " } });
    renderWizard();
    certify();
    const p = payload();

    expect(p.checklist.find((c) => c.item === FIRST.key)?.note).toBe("Spare carried.");
    // An empty note is null, not "" — ChecklistItemInput.Note is `string?`.
    expect(p.checklist.filter((c) => c.note === "")).toHaveLength(0);
    expect(p.checklist.filter((c) => c.note === null)).toHaveLength(ITEM_COUNT - 1);
  });

  it("sends a Major defect against the item KEY, with the fault note", () => {
    seedComplete({
      answers: { [FIRST.key]: "defect" },
      defects: { [FIRST.key]: { severity: "Major", note: "Brake line weeping." } },
    });
    renderWizard();
    certify();
    const p = payload();

    expect(p.defects).toEqual([
      { item: FIRST.key, severity: "Major", note: "Brake line weeping." },
    ]);

    const row = p.checklist.find((c) => c.item === FIRST.key);
    expect(row?.state).toBe("Defect");
    expect(row?.passed).toBe(false);
  });
});

describe("the location", () => {
  it("is sent trimmed", () => {
    // VehicleInspection.Normalize trims server-side too; sending it trimmed means the 200-char
    // check here and there measure the same string.
    seedComplete({ location: "   PTH 391, km 42 north of Thompson  " });
    renderWizard();
    certify();

    expect(payload().location).toBe("PTH 391, km 42 north of Thompson");
  });

  it("is shown on the review step", () => {
    seedComplete({ location: "  Leaf Rapids " });
    renderWizard();

    expect(screen.getByText("Location (town or highway)")).toBeTruthy();
    expect(screen.getByText("Leaf Rapids")).toBeTruthy();
  });

  it("is required: a blank one disables Certify and nothing is enqueued", () => {
    seedComplete({ location: "  " });
    renderWizard();

    expect(screen.getByText("Not entered — required")).toBeTruthy();
    const button = screen.getByRole("button", { name: /Certify & submit/i }) as HTMLButtonElement;
    expect(button.disabled).toBe(true);
    certify();
    expect(enqueue).not.toHaveBeenCalled();
  });

  it("is required: over 200 characters disables Certify", () => {
    seedComplete({ location: "a".repeat(201) });
    renderWizard();

    expect(
      (screen.getByRole("button", { name: /Certify & submit/i }) as HTMLButtonElement).disabled,
    ).toBe(true);
  });
});

describe("a draft holding rows this form does not ask", () => {
  it("never sends them and never lets them block Certify", () => {
    // The store filters by HALF of the form on load; the screen filters by the exact UNIT.
    // An NL02Only row on an NL-01 pre-trip draft (a reassignment, or a form revision) is in
    // the catalogue and on the pre-trip, so it survives the store — and must stop here. Graded
    // Major, it would otherwise turn a clean NL-01 pre-trip into a Fail; left ungraded, it
    // would block Certify with a defect the driver cannot see.
    const offForm = itemsFor("NL-02", "PreTrip")
      .flatMap((g) => g.items)
      .find((i) => !ITEMS.some((x) => x.key === i.key));
    if (!offForm) throw new Error("the catalogue has no NL-02-only pre-trip row");

    seedComplete({
      answers: { [offForm.key]: "defect" },
      defects: { [offForm.key]: { severity: "Major", note: "not this unit" } },
    });
    renderWizard();

    expect(
      (screen.getByRole("button", { name: /Certify & submit/i }) as HTMLButtonElement).disabled,
    ).toBe(false);
    certify();
    const p = payload();
    expect(p.defects).toEqual([]);
    expect(p.checklist.some((c) => c.item === offForm.key)).toBe(false);
    expect(p.checklist).toHaveLength(ITEM_COUNT);
  });

  it("does not block on an ungraded defect for an off-form row", () => {
    const offForm = itemsFor("NL-02", "PreTrip")
      .flatMap((g) => g.items)
      .find((i) => !ITEMS.some((x) => x.key === i.key));
    if (!offForm) throw new Error("the catalogue has no NL-02-only pre-trip row");

    seedComplete({ answers: { [offForm.key]: "defect" } });
    renderWizard();

    expect(
      (screen.getByRole("button", { name: /Certify & submit/i }) as HTMLButtonElement).disabled,
    ).toBe(false);
  });

  it("shows a row new to the form as Not answered and blocks Certify, never defaulting it to Ok", () => {
    // What a form revision that ADDS rows looks like to a draft: the answers simply lack them.
    const answers = Object.fromEntries(ITEMS.slice(1).map((i) => [i.key, "pass" as CheckState]));
    window.localStorage.setItem(
      draftKey("PreTrip", "VEH-11"),
      JSON.stringify({
        v: INSPECTION_STORE_VERSION,
        mode: "PreTrip",
        vehicleId: "VEH-11",
        startedOn: today,
        startedAt: `${today}T06:02:00.000Z`,
        answers,
        notes: {},
        defects: {},
        odometerKm: 184_920,
        location: "Lynn Lake",
        stepId: "review",
      }),
    );
    renderWizard();

    expect(screen.getAllByText("Not answered")).toHaveLength(1);
    expect(screen.getByText(/1 item\(s\) still unanswered/)).toBeTruthy();
    expect(
      (screen.getByRole("button", { name: /Certify & submit/i }) as HTMLButtonElement).disabled,
    ).toBe(true);
  });
});

describe("the per-section All OK shortcut", () => {
  // NL-PTI-01 rev 3's time saver, through the DOM. The rules it must keep: one sub-group only,
  // blanks only, the driver SEES the rows before the one confirming tap, and the wire carries
  // every row individually with nothing that says "section passed".
  const GROUPS = itemsFor("NL-01", "PreTrip");
  const ENGINE = GROUPS[0];
  const NEXT = GROUPS[1];

  function allOkButton(): HTMLElement {
    return screen.getByRole("button", { name: /^All OK — / });
  }

  function confirmAllOk() {
    fireEvent.click(screen.getByRole("button", { name: /Confirm all OK/ }));
  }

  it("is offered on a sub-group's first row as a separate, named, counted button", () => {
    renderWizard();
    setOdometer("184920");

    expect(screen.getAllByRole("button", { name: /^All OK — / })).toHaveLength(1);
    expect(allOkButton().textContent).toContain(
      `All OK — ${ENGINE.title} (${ENGINE.items.length} checks)`,
    );
    // The three answer tiles are still there, unselected — the shortcut is an alternative.
    expect(screen.getByRole("button", { name: /^Pass/ }).getAttribute("aria-pressed")).toBe("false");
  });

  it("shows every row of the sub-group by label before anything is written", () => {
    renderWizard();
    setOdometer("184920");
    fireEvent.click(allOkButton());

    const list = screen.getByRole("list", { name: `Checks in ${ENGINE.title}` });
    const rows = list.querySelectorAll("li");
    expect(rows).toHaveLength(ENGINE.items.length);
    ENGINE.items.forEach((item, i) => expect(rows[i].textContent).toContain(item.label));
    expect(screen.getAllByText("Will be OK")).toHaveLength(ENGINE.items.length);

    // Opening the panel answers nothing.
    expect(getDraft("PreTrip", "VEH-11")?.answers).toEqual({});
  });

  it("writes nothing on Cancel and returns to the row-by-row question", () => {
    renderWizard();
    setOdometer("184920");
    fireEvent.click(allOkButton());
    fireEvent.click(screen.getByRole("button", { name: /^Cancel/ }));

    expect(screen.getByText(FIRST.label)).toBeTruthy();
    expect(screen.getByText(`Check 1 of ${ITEM_COUNT}`)).toBeTruthy();
    expect(getDraft("PreTrip", "VEH-11")?.answers).toEqual({});
  });

  it("marks only that sub-group OK and lands on the next sub-group's first check", () => {
    renderWizard();
    setOdometer("184920");
    fireEvent.click(allOkButton());
    confirmAllOk();

    const answers = getDraft("PreTrip", "VEH-11")?.answers ?? {};
    expect(Object.keys(answers).sort()).toEqual(ENGINE.items.map((i) => i.key).sort());
    expect(Object.values(answers).every((a) => a === "pass")).toBe(true);

    // The progress count is position on the form, unchanged by how the rows were answered.
    expect(screen.getByText(NEXT.items[0].label)).toBeTruthy();
    expect(screen.getByText(`Check ${ENGINE.items.length + 1} of ${ITEM_COUNT}`)).toBeTruthy();
    // And the next sub-group offers its OWN shortcut — never one for the rest of the form.
    expect(allOkButton().textContent).toContain(`${NEXT.title} (${NEXT.items.length} checks)`);
  });

  it("never overwrites a Defect or an N/A, and says so in the list", () => {
    renderWizard();
    setOdometer("184920");
    tap("Defect"); // row 1 → follow-up
    fireEvent.click(screen.getByRole("button", { name: /^Major/ }));
    fireEvent.click(continueButton());
    tap("N/A"); // row 2

    // Row 3 is now the sub-group's first blank row.
    const remaining = ENGINE.items.length - 2;
    expect(allOkButton().textContent).toContain(`(${remaining} checks)`);
    fireEvent.click(allOkButton());
    expect(screen.getByText("Defect — kept")).toBeTruthy();
    expect(screen.getByText("N/A — kept")).toBeTruthy();
    expect(screen.getAllByText("Will be OK")).toHaveLength(remaining);
    confirmAllOk();

    const draft = getDraft("PreTrip", "VEH-11");
    expect(draft?.answers[ENGINE.items[0].key]).toBe("defect");
    expect(draft?.defects[ENGINE.items[0].key]?.severity).toBe("Major");
    expect(draft?.answers[ENGINE.items[1].key]).toBe("na");
    for (const item of ENGINE.items.slice(2)) expect(draft?.answers[item.key]).toBe("pass");
  });

  it("leaves every filled row individually changeable — Back, then a different answer", () => {
    renderWizard();
    setOdometer("184920");
    fireEvent.click(allOkButton());
    confirmAllOk();

    fireEvent.click(screen.getByRole("button", { name: /Back/ }));
    const last = ENGINE.items[ENGINE.items.length - 1];
    expect(screen.getByText(last.label)).toBeTruthy();
    expect(screen.getByRole("button", { name: /^Pass/ }).getAttribute("aria-pressed")).toBe("true");

    tap("Defect");
    expect(getDraft("PreTrip", "VEH-11")?.answers[last.key]).toBe("defect");
    expect(screen.getByText(/^Follow-up · /)).toBeTruthy();
  });

  it(
    "sends every row individually, with its own state, and nothing that says a section passed",
    () => {
      renderWizard();
      setOdometer("184920");
      tap("N/A"); // row 1 answered by hand, so the shortcut must leave it
      // Walk the whole form the fast way: the shortcut wherever it is offered, Pass elsewhere.
      for (let guard = 0; guard < ITEM_COUNT + 1; guard += 1) {
        if (screen.queryByText(NL_PTI_01_CERTIFICATION)) break;
        const shortcut = screen.queryByRole("button", { name: /^All OK — / });
        if (shortcut) {
          fireEvent.click(shortcut);
          confirmAllOk();
        } else {
          tap("Pass");
        }
      }
      expect(screen.getByText(`Review · ${ITEM_COUNT} of ${ITEM_COUNT}`)).toBeTruthy();
      certify();
      const p = payload();

      expect(p.checklist).toHaveLength(ITEM_COUNT);
      expect(new Set(p.checklist.map((c) => c.item))).toEqual(new Set(ITEMS.map((i) => i.key)));
      for (const row of p.checklist) {
        expect(Object.keys(row).sort()).toEqual(["group", "item", "note", "passed", "state"]);
      }
      expect(p.checklist.find((c) => c.item === FIRST.key)?.state).toBe("NotApplicable");
      expect(p.checklist.filter((c) => c.state === "Ok")).toHaveLength(ITEM_COUNT - 1);
      expect(JSON.stringify(p)).not.toMatch(/section/i);
    },
    30_000,
  );
});

// ---------------------------------------------------------------------------
// The post-trip (NL-PTI-01 rev 4): the six Close-Out checks, then "Any defect found after the
// pre-trip?" — No, or Yes and each new defect against the PRE-trip row it concerns. The helpers
// are pinned in lib/newDefects.test.ts; this proves the screen applies them and pins the payload.
// ---------------------------------------------------------------------------

const POST_ITEMS = itemsFor("NL-01", "PostTrip").flatMap((g) => g.items);
const POST_COUNT = checkCount("NL-01", "PostTrip");

function renderPostTrip() {
  return render(<Inspection mode="PostTrip" />);
}

/** A post-trip draft with every Close-Out row answered Pass, parked on the review step. */
function seedPost(over: {
  newDefectsFound?: boolean | null;
  newDefects?: { itemKey: string; severity: string | null; note: string }[];
  stepId?: string;
} = {}) {
  window.localStorage.setItem(
    draftKey("PostTrip", "VEH-11"),
    JSON.stringify({
      v: INSPECTION_STORE_VERSION,
      mode: "PostTrip",
      vehicleId: "VEH-11",
      startedOn: today,
      startedAt: `${today}T19:40:00.000Z`,
      answers: Object.fromEntries(POST_ITEMS.map((i) => [i.key, "pass"])),
      notes: {},
      defects: {},
      odometerKm: 185_210,
      location: "Thompson yard",
      newDefectsFound: over.newDefectsFound ?? null,
      newDefects: over.newDefects ?? [],
      stepId: over.stepId ?? "review",
    }),
  );
}

function certifyButton(): HTMLButtonElement {
  return screen.getByRole("button", { name: /Certify & submit/i }) as HTMLButtonElement;
}

describe("the post-trip (rev 4)", () => {
  it("asks the six Close-Out checks, then the new-defects question — nothing defaulted", () => {
    renderPostTrip();
    setOdometer("185210", "Thompson yard");
    expect(POST_COUNT).toBe(6);
    expect(screen.getByText(`Check 1 of ${POST_COUNT}`)).toBeTruthy();
    expect(screen.getByText(POST_ITEMS[0].label)).toBeTruthy();
    // The en-route row is gone.
    expect(screen.queryByText("Defects noticed while driving")).toBeNull();

    for (let i = 0; i < POST_COUNT; i += 1) tap("Pass");

    expect(screen.getByText("Any defect found after the pre-trip?")).toBeTruthy();
    expect(screen.getByText(`New defects · ${POST_COUNT} of ${POST_COUNT} checks done`)).toBeTruthy();
    expect(screen.getAllByText("MOCK").length).toBeGreaterThan(0);
    // Both tiles unselected, and Continue blocked until one is chosen.
    expect(screen.getByRole("button", { name: /^No/ }).getAttribute("aria-pressed")).toBe("false");
    expect(screen.getByRole("button", { name: /^Yes/ }).getAttribute("aria-pressed")).toBe("false");
    expect(continueButton().disabled).toBe(true);
    expect(getDraft("PostTrip", "VEH-11")?.newDefectsFound).toBeNull();
  });

  it("goes straight to Review on No, and sends the Close-Out rows with no defects", () => {
    seedPost({ stepId: "newDefects" });
    renderPostTrip();
    fireEvent.click(screen.getByRole("button", { name: /^No/ }));

    expect(screen.getByText(NL_PTI_01_CERTIFICATION)).toBeTruthy();
    expect(screen.getByText("New defects since the pre-trip")).toBeTruthy();
    expect(certifyButton().disabled).toBe(false);
    certify();
    const p = payload();

    expect(p.type).toBe("PostTrip");
    expect(p.checklist).toHaveLength(POST_COUNT);
    expect(new Set(p.checklist.map((c) => c.item))).toEqual(new Set(POST_ITEMS.map((i) => i.key)));
    expect(p.defects).toEqual([]);
    // No invented field for the No/Yes answer — "No" is an empty new-defect list.
    expect(JSON.stringify(p)).not.toMatch(/newDefect|defectsFound/);
  });

  it("files a Yes defect against the PRE-trip key — as a defect, never as a checklist row", async () => {
    seedPost({ stepId: "newDefects" });
    renderPostTrip();
    fireEvent.click(screen.getByRole("button", { name: /^Yes/ }));

    // Yes opens the first (blank) defect straight away.
    expect(screen.getByText("What did you find?")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Item — the pre-trip row it concerns"), {
      target: { value: "Tire condition" },
    });
    expect(screen.getByText(/Major if cord exposed/)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: /^Major/ }));
    fireEvent.change(screen.getByLabelText("Note about new defect 1"), {
      target: { value: "  Cord showing, right rear.  " },
    });
    fireEvent.click(screen.getByRole("button", { name: /^Done/ }));

    expect(screen.getByRole("list", { name: "New defects" }).textContent).toContain("Tire condition");
    expect(continueButton().disabled).toBe(false);
    fireEvent.click(continueButton());

    certify();
    const p = payload();
    expect(p.defects).toEqual([
      { item: "Tire condition", severity: "Major", note: "Cord showing, right rear." },
    ]);
    expect(p.checklist).toHaveLength(POST_COUNT);
    expect(p.checklist.some((c) => c.item === "Tire condition")).toBe(false);

    // A Major new defect fails the post-trip, and the certification remembers the item. The
    // certification is recorded after `await enqueue`, so wait for the certified pane.
    await screen.findByText("Post-trip inspection certified");
    const cert = certifiedToday("PostTrip", "VEH-11");
    expect(cert?.result).toBe("Fail");
    expect(cert?.defectCount).toBe(1);
    expect(cert?.defectItems).toEqual(["Tire condition"]);
  });

  it("leaves out items today's pre-trip already reported, and says how many", () => {
    recordCertification({
      commandId: "pre-1",
      mode: "PreTrip",
      vehicleId: "VEH-11",
      onDate: today,
      certifiedAt: `${today}T06:12:00.000Z`,
      result: "PassWithDefects",
      defectCount: 1,
      defectItems: ["Tire condition"],
      outOfService: false,
    });
    seedPost({ stepId: "newDefects" });
    renderPostTrip();

    expect(screen.getByText(/1 item already reported on today’s pre-trip is not listed/)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: /^Yes/ }));
    const picker = screen.getByLabelText("Item — the pre-trip row it concerns") as HTMLSelectElement;
    const values = [...picker.options].map((o) => o.value);
    expect(values).not.toContain("Tire condition");
    expect(values).toContain("Tire pressure");
    // Never a Close-Out row: those are the post-trip's own checklist.
    for (const item of POST_ITEMS) expect(values).not.toContain(item.key);
  });

  it("does not offer an item already picked on another new defect", () => {
    seedPost({
      newDefectsFound: true,
      newDefects: [
        { itemKey: "Horn", severity: "Minor", note: "Weak." },
        { itemKey: "", severity: null, note: "" },
      ],
      stepId: "newDefects",
    });
    renderPostTrip();
    fireEvent.click(screen.getAllByRole("button", { name: /^Edit/ })[1]);

    const picker = screen.getByLabelText("Item — the pre-trip row it concerns") as HTMLSelectElement;
    expect([...picker.options].map((o) => o.value)).not.toContain("Horn");
  });

  it("blocks Certify while the question is unanswered, and enqueues nothing", () => {
    seedPost({ newDefectsFound: null });
    renderPostTrip();

    expect(screen.getByText("Not answered — required")).toBeTruthy();
    expect(certifyButton().disabled).toBe(true);
    expect(screen.getAllByText(/Any defect found after the pre-trip\?” — No, or Yes/).length).toBeGreaterThan(0);
    certify();
    expect(enqueue).not.toHaveBeenCalled();
  });

  it("blocks Certify on Yes with an ungraded or note-less defect", () => {
    seedPost({
      newDefectsFound: true,
      newDefects: [{ itemKey: "Horn", severity: null, note: "Weak." }],
    });
    renderPostTrip();
    expect(certifyButton().disabled).toBe(true);
    expect(screen.getAllByText("Grade each new defect Minor or Major.").length).toBeGreaterThan(0);
    cleanup();

    seedPost({
      newDefectsFound: true,
      newDefects: [{ itemKey: "Horn", severity: "Minor", note: "  " }],
    });
    renderPostTrip();
    expect(certifyButton().disabled).toBe(true);
    certify();
    expect(enqueue).not.toHaveBeenCalled();
  });

  it("never sends rows entered before the answer was changed to No", async () => {
    seedPost({
      newDefectsFound: false,
      newDefects: [{ itemKey: "Horn", severity: "Major", note: "Dead." }],
    });
    renderPostTrip();
    expect(certifyButton().disabled).toBe(false);
    certify();
    expect(payload().defects).toEqual([]);
    await screen.findByText("Post-trip inspection certified");
    expect(certifiedToday("PostTrip", "VEH-11")?.result).toBe("Pass");
  });

  it("sends a Close-Out defect and a new defect side by side, each once", async () => {
    const closeOut = POST_ITEMS[0];
    window.localStorage.clear();
    seedPost({
      newDefectsFound: true,
      newDefects: [{ itemKey: "Horn", severity: "Minor", note: "Weak." }],
    });
    // Mark one Close-Out row a defect on top of the seeded draft.
    const raw = JSON.parse(window.localStorage.getItem(draftKey("PostTrip", "VEH-11")) ?? "{}");
    raw.answers[closeOut.key] = "defect";
    raw.defects = { [closeOut.key]: { severity: "Minor", note: "Scraped mirror." } };
    window.localStorage.setItem(draftKey("PostTrip", "VEH-11"), JSON.stringify(raw));

    renderPostTrip();
    certify();
    const p = payload();
    expect(p.defects).toEqual([
      { item: closeOut.key, severity: "Minor", note: "Scraped mirror." },
      { item: "Horn", severity: "Minor", note: "Weak." },
    ]);
    expect(new Set(p.defects.map((x) => x.item)).size).toBe(p.defects.length);
    await screen.findByText("Post-trip inspection certified");
    expect(certifiedToday("PostTrip", "VEH-11")?.result).toBe("PassWithDefects");
  });
});

describe("when the queue rejects the capture", () => {
  it("keeps every answer, records nothing, and says so", async () => {
    // enqueue → recordCertification → discardDraft, in that order. Reversing it would lose a
    // driver's whole walk-around to a transient failure.
    enqueue.mockRejectedValue(new Error("storage full"));

    seedComplete();
    renderWizard();
    certify();

    expect(await screen.findByText("This inspection was not captured.")).toBeTruthy();
    expect(certifiedToday("PreTrip", "VEH-11")).toBeNull();

    const draft = getDraft("PreTrip", "VEH-11");
    expect(draft).not.toBeNull();
    expect(Object.keys(draft?.answers ?? {})).toHaveLength(ITEM_COUNT);
  });
});
