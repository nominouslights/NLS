import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import Inspection from "./Inspection";
import { certifiedToday, getDraft } from "@/lib/inspectionStore";
import { dvirChecklist } from "@/lib/data";

// The DVIR wizard, end to end through the DOM.
//
// lib/inspectionSteps.test.ts proves the step model and lib/inspectionStore.test.ts the draft;
// this proves the screen actually applies them, and it pins the four payload rules that no type
// can enforce:
//
//   • `result` is NOT sent — the server derives it (VehicleInspection.DeriveResult).
//   • an N/A item is OMITTED from `checklist`, never sent as `passed: true`. Marking an
//     unapplicable item "passed" is a false attestation in a compliance record.
//   • a severity crosses through severityToWire — "Out of Service" → "OutOfService".
//   • `source` is "DriverApp", explicitly, because the backend defaults it to Dispatcher.
//
// And the one behavioural guarantee that costs a driver 22 questions if it breaks: if enqueue
// throws, the draft survives.

const { enqueue } = vi.hoisted(() => ({
  enqueue: vi.fn<(kind: string, payload: unknown) => Promise<string>>(),
}));

vi.mock("@/lib/sync/queue", () => ({
  enqueue,
  pending: () => [],
  drain: async () => {},
  retry: async () => {},
}));

type Payload = {
  type: string;
  source: string;
  odometerKm: number | null;
  checklist: { group: string; item: string; passed: boolean }[];
  defects: { item: string; severity: string; note: string | null }[];
  naItems: string[];
};

const ITEM_COUNT = dvirChecklist.reduce((n, g) => n + g.items.length, 0);
const FIRST_LABEL = dvirChecklist[0].items[0].label;

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

  it("shows one question at a time and counts out of 22, never 23", () => {
    renderWizard();
    setOdometer("184920");

    expect(screen.getByText(FIRST_LABEL)).toBeTruthy();
    expect(screen.getByText("Check 1 of 22")).toBeTruthy();

    tap("Pass");
    expect(screen.getByText("Check 2 of 22")).toBeTruthy();
    expect(screen.queryByText(FIRST_LABEL)).toBeNull();
  });

  it("injects a follow-up on Defect, under the parent's number", () => {
    renderWizard();
    setOdometer("184920");
    tap("Pass"); // item 1
    tap("Defect"); // item 2 → follow-up

    expect(screen.getByText("Follow-up · check 2 of 22")).toBeTruthy();
    expect(
      (screen.getByRole("button", { name: /^Continue/ }) as HTMLButtonElement).disabled,
    ).toBe(true);

    fireEvent.click(screen.getByRole("button", { name: /^Major/ }));
    expect(
      (screen.getByRole("button", { name: /^Continue/ }) as HTMLButtonElement).disabled,
    ).toBe(false);
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

  it("keeps the draft across an unmount, which is what navigating away does", () => {
    const first = renderWizard();
    setOdometer("184920");
    tap("Pass");
    first.unmount();

    renderWizard();
    // Resumed on the step pointer, not on step 1 — and the pointer is an id, so an injected
    // defect step could not have shifted it.
    expect(screen.getByText("Check 2 of 22")).toBeTruthy();
    expect(screen.queryByLabelText(/Odometer reading/)).toBeNull();
    expect(getDraft("PreTrip", "VEH-11")?.odometerKm).toBe(184_920);
  });
});

describe("the submit payload", () => {
  function answerAll(firstAnswer: "Pass" | "N/A" = "Pass") {
    renderWizard();
    setOdometer("184920");
    tap(firstAnswer);
    for (let i = 1; i < ITEM_COUNT; i += 1) tap("Pass");
    fireEvent.click(screen.getByRole("button", { name: /Certify & submit/i }));
  }

  it("goes through the queue with every wire string spelled correctly", () => {
    answerAll();
    const p = payload();

    expect(p.type).toBe("PreTrip");
    expect(p.source).toBe("DriverApp");
    expect(p.odometerKm).toBe(184_920);
    expect(p.checklist).toHaveLength(ITEM_COUNT);
    expect(p.checklist.every((c) => c.passed)).toBe(true);
    expect(p.checklist.every((c) => c.group.trim().length > 0)).toBe(true);
  });

  it("does not send `result` — the server derives it", () => {
    answerAll();
    expect(payload()).not.toHaveProperty("result");
  });

  it("does not send `enteredBy` or a recurrence pointer", () => {
    answerAll();
    expect(payload()).not.toHaveProperty("enteredBy");
    expect(payload().defects).toEqual([]);
  });

  it("OMITS an N/A item from the checklist and names it separately", () => {
    // Never `passed: true` for an item that does not apply.
    answerAll("N/A");
    const p = payload();

    expect(p.checklist).toHaveLength(ITEM_COUNT - 1);
    expect(p.checklist.some((c) => c.item === FIRST_LABEL)).toBe(false);
    expect(p.naItems).toEqual([FIRST_LABEL]);
  });

  it("sends an Out of Service defect as `OutOfService`", () => {
    renderWizard();
    setOdometer("184920");
    tap("Defect");
    fireEvent.click(screen.getByRole("button", { name: /^Out of Service/ }));
    fireEvent.change(screen.getByLabelText(/Note about the/), {
      target: { value: "Brake line weeping." },
    });
    for (let i = 1; i < ITEM_COUNT; i += 1) {
      fireEvent.click(screen.getByRole("button", { name: /^Continue/ }));
      tap("Pass");
    }
    fireEvent.click(screen.getByRole("button", { name: /Certify & submit/i }));

    const p = payload();
    expect(p.defects).toEqual([
      { item: FIRST_LABEL, severity: "OutOfService", note: "Brake line weeping." },
    ]);
    expect(p.checklist.find((c) => c.item === FIRST_LABEL)?.passed).toBe(false);
  });
});

describe("when the queue rejects the capture", () => {
  it("keeps all 22 answers, records nothing, and says so", async () => {
    // enqueue → recordCertification → discardDraft, in that order. Reversing it would lose a
    // driver's whole inspection to a transient failure.
    enqueue.mockRejectedValue(new Error("storage full"));

    renderWizard();
    setOdometer("184920");
    for (let i = 0; i < ITEM_COUNT; i += 1) tap("Pass");
    fireEvent.click(screen.getByRole("button", { name: /Certify & submit/i }));

    expect(await screen.findByText("This inspection was not captured.")).toBeTruthy();
    expect(certifiedToday("PreTrip", "VEH-11")).toBeNull();

    const draft = getDraft("PreTrip", "VEH-11");
    expect(draft).not.toBeNull();
    expect(Object.keys(draft?.answers ?? {})).toHaveLength(ITEM_COUNT);
  });
});
