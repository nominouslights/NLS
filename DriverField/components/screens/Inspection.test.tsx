import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import Inspection from "./Inspection";
import {
  certifiedToday,
  draftKey,
  getDraft,
  INSPECTION_STORE_VERSION,
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
//
// And the one behavioural guarantee that costs a driver the whole walk-around if it breaks: if
// enqueue throws, the draft survives.

const { enqueue } = vi.hoisted(() => ({
  enqueue: vi.fn<(kind: string, payload: unknown) => Promise<string>>(),
}));

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
 * A row whose KEY and LABEL differ: the four Area C interior light checks repeat Area B's
 * exterior labels and are kept distinct by an "Interior: " key prefix. If the screen ever sent
 * labels, this is the row that would collide onto another item's defect address.
 */
const PREFIXED = ITEMS.find((i) => i.key !== i.label && i.key.startsWith("Interior: "));

beforeEach(() => {
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

function setOdometer(km: string) {
  fireEvent.change(screen.getByLabelText(/Odometer reading/), { target: { value: km } });
  fireEvent.click(screen.getByRole("button", { name: /^Continue/ }));
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
 * WHY NOT TAP 67 TILES IN EVERY PAYLOAD TEST. One full walk through the DOM is worth having and
 * there is one below; eight of them cost a minute of suite time to re-prove the same navigation
 * and prove nothing about the payload. Seeding the key directly is the same technique
 * lib/inspectionStore.test.ts uses for a reload, and it leaves the submit path — enqueue →
 * recordCertification → discardDraft, read back through getDraft — completely untouched.
 */
function seedComplete(over: {
  answers?: Record<string, CheckState>;
  notes?: Record<string, string>;
  defects?: Record<string, { severity: string; note: string }>;
} = {}) {
  const answers: Record<string, CheckState> = Object.fromEntries(
    ITEMS.map((i) => [i.key, "pass" as CheckState]),
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
    expect(screen.getByText("Odometer")).toBeTruthy();
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
    // 67 for NL-01 pre-trip. Written through checkCount() rather than as a literal, because a
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
    // The mitigation for a 67-to-80-row walk-around. "Ground beneath the vehicle" is not a
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
      // THE full-walk test, and deliberately the only one: 67 taps through jsdom is slow, so
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
    expect(p.checklist).toHaveLength(ITEM_COUNT);
    expect(p.checklist.every((c) => c.state === "Ok" && c.passed)).toBe(true);
    expect(p.checklist.every((c) => c.group.trim().length > 0)).toBe(true);
  });

  it("addresses every row by its catalogue KEY, never its label", () => {
    // Sending labels would collide the four "Interior: " rows onto Area B's exterior lamps and
    // file their defects at an address the Dispatch Console cannot resolve.
    seedComplete();
    renderWizard();
    certify();
    const p = payload();

    expect(p.checklist.every((c) => CATALOGUE_KEYS.has(c.item))).toBe(true);
    expect(new Set(p.checklist.map((c) => c.item)).size).toBe(ITEM_COUNT);

    if (!PREFIXED) throw new Error("the catalogue has no key-differs-from-label row");
    expect(p.checklist.some((c) => c.item === PREFIXED.key)).toBe(true);
    expect(p.checklist.some((c) => c.item === PREFIXED.label)).toBe(false);
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
