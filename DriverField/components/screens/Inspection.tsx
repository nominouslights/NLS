"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { gap, type } from "@/lib/tablet";
import { Panel } from "@/components/ui/Panel";
import { TouchButton } from "@/components/ui-tablet/TouchButton";
import { StatusBanner } from "@/components/ui-tablet/StatusBanner";
import { Screen, MockTag, Heading, CardRow, FieldLine, TabletChip } from "./shared";
import { WizardFrame } from "./inspection/WizardFrame";
import { OdometerStep } from "./inspection/OdometerStep";
import { CheckStep } from "./inspection/CheckStep";
import { DefectStep } from "./inspection/DefectStep";
import { ReviewStep } from "./inspection/ReviewStep";
import { enqueue } from "@/lib/sync/queue";
import {
  buildSteps,
  checkStepId,
  progressFraction,
  progressLabel,
  resolveStep,
} from "@/lib/inspectionSteps";
import {
  certifiedToday,
  discardDraft,
  getDraft,
  recordCertification,
  setAnswer,
  setDefect,
  setOdometer,
  setStep,
  startDraft,
  storageFailed,
  type DraftDefect,
  type LocalCertification,
} from "@/lib/inspectionStore";
import { useInspectionStoreHydrated } from "@/lib/useInspectionStore";
import {
  deriveResult,
  inspectionDue,
  INSPECTION_SOURCE_WIRE,
  odometerError,
  severityToWire,
} from "@/lib/inspectionGate";
import {
  activeTrip,
  assignedVehicle,
  assignedVehicleId,
  currentDriver,
  dvirChecklist,
  dvirSubmissions,
  today,
} from "@/lib/data";
import type { CheckState, DefectSeverity, InspectionMode } from "@/lib/types";

// Driver Vehicle Inspection Report — NSC Standard 11, ONE QUESTION PER SCREEN.
//
// This file is the flow controller and nothing else: it resolves the mode, reads the draft,
// builds the step list, renders the current step inside a WizardFrame, and owns Back and submit.
// The steps themselves live in ./inspection/*, the step model in lib/inspectionSteps.ts, the
// draft in lib/inspectionStore.ts and the rules in lib/inspectionGate.ts — so each of those is
// testable without jsdom and this file stays readable.
//
// WHY IT IS SHAPED THIS WAY: the previous version rendered all 22 items as one continuous
// scroll, three or four screens deep, with the legal attestation at the bottom. On a
// dash-mounted 10-inch tablet, in northern daylight, with gloves on, a driver loses their place
// and sees the attestation least.
//
// THE DRAFT SURVIVES NAVIGATION AND A RELOAD. Answers used to live in local useState, and
// Console.tsx unmounts screens on nav, so tapping away to Hours destroyed all 22 — which this
// repo frames as a compliance failure, not a lost draft. They now live in
// lib/inspectionStore.ts, read here through ONE useSyncExternalStore.
//
// ORDER ON SUBMIT: enqueue → recordCertification → discardDraft. If enqueue throws, the
// driver's 22 answers survive. See submit() below.
//
// Photo attachment is still rendered DISABLED WITH ITS REASON rather than omitted (on the
// review step): there is no attachment endpoint for inspections or defects anywhere on the
// backend, and a field app that silently lacks defect photos is a gap somebody should see.

export default function Inspection({ mode: requested }: { mode: InspectionMode | null }) {
  // One subscription for the whole screen — see lib/useInspectionStore.ts for why this is
  // useSyncExternalStore and not an effect (lint bar, and the SSR hydration mismatch).
  const hydrated = useInspectionStoreHydrated();

  const [done, setDone] = useState<LocalCertification | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const vehicleId = assignedVehicleId;
  // `null` means "whatever inspectionDue() says", so the rail entry and a cold open agree.
  const mode: InspectionMode = requested ?? (hydrated ? inspectionDue(vehicleId).mode : "PreTrip");

  const draft = hydrated ? getDraft(mode, vehicleId) : null;
  const answers: Record<string, CheckState> = draft?.answers ?? {};
  const defects: Record<string, DraftDefect> = draft?.defects ?? {};
  const odometerKm = draft?.odometerKm ?? null;

  const steps = buildSteps(answers);
  const step = resolveStep(steps, draft?.stepId ?? null);
  const index = steps.findIndex((s) => s.id === step.id);

  const alreadyCertified = hydrated ? certifiedToday(mode, vehicleId) : null;
  const finished = done ?? (draft === null ? alreadyCertified : null);

  const severities: DefectSeverity[] = Object.entries(defects)
    .filter(([itemId]) => answers[itemId] === "defect")
    .map(([, d]) => d.severity)
    .filter((s): s is DefectSeverity => s !== null);

  const naItems = Object.entries(answers)
    .filter(([, state]) => state === "na")
    .map(([itemId]) => itemId);

  const unansweredCount = CHECKLIST_ITEMS.filter((i) => answers[i.id] === undefined).length;
  const ungradedCount = Object.entries(answers).filter(
    ([itemId, state]) => state === "defect" && !defects[itemId]?.severity,
  ).length;

  // --- navigation ---------------------------------------------------------

  function goTo(stepId: string) {
    setStep(mode, vehicleId, stepId);
  }

  function goBack() {
    if (index > 0) goTo(steps[index - 1].id);
  }

  function goNext() {
    if (index < steps.length - 1) goTo(steps[index + 1].id);
  }

  /**
   * Answering AUTO-ADVANCES — one tap per question is the whole point. A mis-tap is visible
   * because the next screen names its own item, and Back is always present.
   *
   * The next step is computed from the UPDATED answers, which is what makes a "Defect" answer
   * land straight on its follow-up and a change away from "defect" skip the (now absent) one.
   */
  function answer(itemId: string, next: CheckState) {
    const updated = setAnswer(mode, vehicleId, itemId, next);
    const nextSteps = buildSteps(updated.answers);
    const at = nextSteps.findIndex((s) => s.id === checkStepId(itemId));
    const target = at === -1 ? nextSteps[nextSteps.length - 1] : nextSteps[at + 1];
    goTo((target ?? nextSteps[nextSteps.length - 1]).id);
  }

  // --- submit -------------------------------------------------------------

  async function submit() {
    const current = getDraft(mode, vehicleId);
    if (!current) return;

    const graded: { itemId: string; severity: DefectSeverity; note: string }[] = [];
    for (const [itemId, state] of Object.entries(current.answers)) {
      if (state !== "defect") continue;
      const d = current.defects[itemId];
      if (d?.severity) graded.push({ itemId, severity: d.severity, note: d.note });
    }

    const result = deriveResult(graded.map((g) => g.severity));
    const certifiedAt = new Date().toISOString();

    // THE ONLY WRITE. Every screen mutation in this app goes through queue.enqueue() — never a
    // lib/api/* call, never an in-place mutation of a lib/data.ts array. Hold that and the
    // offline batch changes four files under lib/sync/ and zero screens.
    //
    // SEAMS FLAGGED RATHER THAN INVENTED, matching EnterInspectionCommand in
    // Backend/src/Fleet/Application/Inspections/Enter/:
    //
    //  • `result` IS NOT SENT. VehicleInspection.DeriveResult derives it (VehicleInspection.cs
    //    :421-432). deriveResult() runs here for the banner, the local certification and the
    //    boarding gate only.
    //  • `"na"` HAS NO WIRE REPRESENTATION. ChecklistItemInput.Passed is a `bool`. N/A items are
    //    OMITTED from `checklist` and named in a client-only `naItems` field. `passed: true`
    //    for an unapplicable item would be a false attestation in a compliance record — the
    //    exact failure the "not answered must never look like passed" rule exists to prevent —
    //    and `passed: false` would read as a defect. Omission loses information, which is
    //    visible and fixable; falsification is not. OPEN QUESTION FOR THE BACKEND: should
    //    ChecklistItemInput.Passed become a tri-state?
    //  • `severity` crosses through severityToWire(). InspectionDefectSeverity is
    //    `Minor | Major | OutOfService` — no spaces — and the display string would be rejected
    //    by the default System.Text.Json enum converter. Pinned in lib/wire.test.ts.
    //  • `source` is never omitted: the backend defaults Source to Dispatcher, which would
    //    attribute a driver's inspection to a dispatcher who never touched the vehicle.
    //  • `tripNumber`, not a trip id — the wire field is TripNumber, a string.
    //  • NO `enteredBy`. lib/wire.test.ts already pins the analogous HOS rule: a driver-app
    //    submission has no dispatcher, so the field is absent rather than invented.
    //  • NO `recurrenceOfInspectionId` — a driver cannot know a defect is a recurrence; that
    //    pointer belongs to the dispatcher Re-report path.
    //  • `attestations: [true]` is one flag because IReadOnlyList<bool> has no labels defined
    //    anywhere on the backend. A multi-checkbox UI would be inventing a contract.
    //  • Weather, temperature, road conditions, visibility and fuel are omitted: nine more
    //    steps on a screen that posts nowhere. Vehicle.tsx already says on screen that fuel has
    //    no backend resource.
    let commandId: string;
    try {
      commandId = await enqueue("dvir.submit", {
        vehicleId,
        unit: assignedVehicle.unit,
        tripNumber: activeTrip.tripNumber,
        driverName: currentDriver.name,
        type: mode,
        source: INSPECTION_SOURCE_WIRE,
        performedAt: current.startedAt,
        certifiedAt,
        odometerKm: current.odometerKm,
        // N/A items are OMITTED here and named in naItems below — never sent as passed.
        checklist: CHECKLIST_ITEMS.filter(
          (i) => current.answers[i.id] === "pass" || current.answers[i.id] === "defect",
        ).map((i) => ({
          group: i.group,
          item: i.label,
          passed: current.answers[i.id] === "pass",
        })),
        defects: graded.map((g) => ({
          item: labelOf(g.itemId),
          severity: severityToWire(g.severity),
          note: g.note.trim() === "" ? null : g.note.trim(),
        })),
        // CLIENT-ONLY. See the `"na"` note above — there is no wire field for this yet.
        naItems: Object.entries(current.answers)
          .filter(([, state]) => state === "na")
          .map(([itemId]) => labelOf(itemId)),
        attestations: [true],
        driverSignatureName: currentDriver.name,
      });
    } catch (error) {
      // The draft is untouched — recordCertification and discardDraft are both below this
      // point. The driver re-taps Certify rather than re-answering 22 questions, and they are
      // TOLD rather than left looking at a button that did nothing.
      setSubmitError(
        error instanceof Error
          ? `The inspection could not be captured on this device: ${error.message}`
          : "The inspection could not be captured on this device.",
      );
      return;
    }
    setSubmitError(null);

    const certification: LocalCertification = {
      commandId,
      mode,
      vehicleId,
      onDate: today,
      certifiedAt,
      result,
      defectCount: graded.length,
      outOfService: graded.some((g) => g.severity === "Out of Service"),
    };

    // Order matters: enqueue first (above), then record, then discard. If enqueue throws, the
    // draft is still on the device and the driver re-taps Certify rather than re-answering 22
    // questions.
    recordCertification(certification);
    discardDraft(mode, vehicleId);
    setDone(certification);
  }

  // --- the completed state ------------------------------------------------

  if (finished) {
    return (
      <Screen
        eyebrow={`${assignedVehicle.unit} · ${assignedVehicle.description}`}
        title={`${modeTitle(mode)} certified`}
        right={
          <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
            <TabletChip
              kind={
                finished.result === "Fail"
                  ? "over"
                  : finished.result === "PassWithDefects"
                    ? "soon"
                    : "ontime"
              }
              label={finished.result === "PassWithDefects" ? "Pass with defects" : finished.result}
            />
            <MockTag />
          </div>
        }
      >
        <StatusBanner kind="soon" title="Held on this device.">
          Your inspection was recorded locally at{" "}
          {finished.certifiedAt.slice(11, 16)}. Sync is not enabled in this build, so it has not
          reached the server and does not yet satisfy a compliance record.
        </StatusBanner>

        {finished.result === "Fail" ? (
          <StatusBanner kind="over" title={`${assignedVehicle.unit} failed this inspection.`}>
            Boarding is disabled on this device and another inspection is not the remedy — call
            dispatch. The vehicle&rsquo;s out-of-service flag is the server&rsquo;s and nothing
            here writes it, so the Trips screen will keep reading no open failure.
          </StatusBanner>
        ) : null}

        <Heading>What happens next</Heading>
        <Panel style={{ padding: "18px 20px" }}>
          <div
            style={{
              fontFamily: fonts.body,
              fontSize: type.label,
              color: colors.textSecondary,
              lineHeight: 1.6,
            }}
          >
            {finished.mode === "PreTrip"
              ? "A post-trip inspection is due at the end of the shift. The rail entry will say so."
              : "Nothing further is due today."}
          </div>
          <div style={{ display: "flex", gap: 12, marginTop: 16, flexWrap: "wrap" }}>
            {/* Starting a fresh draft is what leaves this pane: `finished` is derived from
                "certified today AND no draft", so clearing `done` alone would land straight
                back here. The previous certification is untouched — a second inspection on the
                same day is an additional record, not an edit of the first. */}
            <TouchButton
              variant="secondary"
              onClick={() => {
                startDraft(mode, vehicleId);
                setDone(null);
              }}
            >
              Start another {modeTitle(mode).toLowerCase()}
            </TouchButton>
          </div>
        </Panel>

        <Heading right={<MockTag />}>Recent inspections</Heading>
        {dvirSubmissions.map((s) => (
          <CardRow key={s.id}>
            <div style={{ flex: "none", width: 180 }}>
              <span style={{ fontFamily: fonts.mono, fontSize: 15, color: colors.textDim }}>
                {s.performedAt.slice(0, 16).replace("T", " ")}
              </span>
            </div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <FieldLine
                label={`${s.type} · ${s.unit}`}
                value={`${s.odometerKm.toLocaleString("en-CA")} km${s.defectCount > 0 ? ` · ${s.defectCount} defect(s)` : ""}`}
              />
            </div>
            <div style={{ flex: "none" }}>
              <TabletChip kind={s.rk} label={s.result} />
            </div>
          </CardRow>
        ))}

        <div style={{ height: gap.page }} />
      </Screen>
    );
  }

  // --- the wizard ---------------------------------------------------------

  const odoError = odometerError(odometerKm, assignedVehicle.odometerKm);

  return (
    <WizardFrame
      eyebrow={`${assignedVehicle.unit} · ${assignedVehicle.description}`}
      title={modeTitle(mode)}
      stepId={step.id}
      progressLabel={progressLabel(step)}
      progressFraction={progressFraction(step)}
      // The review chip must not show a teal check while anything is blank: `progressLabel`
      // reports POSITION ("Review · 22 of 22" = you are past all 22 checks), not completeness,
      // so the colour and glyph are what have to carry "still something owed". The body names
      // exactly what.
      progressKind={
        step.kind === "defect"
          ? "over"
          : step.kind !== "review"
            ? "info"
            : unansweredCount > 0 || ungradedCount > 0 || odometerKm === null
              ? "soon"
              : deriveResult(severities) === "Fail"
                ? "over"
                : "ontime"
      }
      footer={
        <>
          <TouchButton
            variant="secondary"
            onClick={goBack}
            disabled={index <= 0}
            disabledReason="This is the first step of the inspection."
          >
            ‹ Back
          </TouchButton>

          {step.kind === "odometer" ? (
            <TouchButton onClick={goNext} disabled={odoError !== null} disabledReason={odoError ?? ""}>
              Continue
            </TouchButton>
          ) : null}

          {step.kind === "check" ? (
            <TouchButton
              onClick={goNext}
              disabled={answers[step.itemId] === undefined}
              disabledReason="Answer this check to continue — a blank cannot be certified."
            >
              Continue
            </TouchButton>
          ) : null}

          {step.kind === "defect" ? (
            <TouchButton
              onClick={goNext}
              disabled={!defects[step.itemId]?.severity}
              disabledReason="Choose a severity — a defect without one cannot be graded."
            >
              Continue
            </TouchButton>
          ) : null}

          <div style={{ flex: 1 }} />

          <TouchButton
            variant="secondary"
            onClick={() => goTo("review")}
            disabled={step.kind === "review"}
            disabledReason="You are on the review step."
          >
            Review
          </TouchButton>
        </>
      }
    >
      {step.kind === "odometer" ? (
        <OdometerStep
          unit={assignedVehicle.unit}
          lastReadingKm={assignedVehicle.odometerKm}
          value={odometerKm}
          onChange={(next) => setOdometer(mode, vehicleId, next)}
        />
      ) : null}

      {step.kind === "check" ? (
        <CheckStep
          group={step.group}
          label={step.label}
          value={answers[step.itemId] ?? null}
          onAnswer={(next) => answer(step.itemId, next)}
        />
      ) : null}

      {step.kind === "defect" ? (
        <DefectStep
          group={step.group}
          label={step.label}
          defect={defects[step.itemId] ?? { severity: null, note: "" }}
          onChange={(next) => setDefect(mode, vehicleId, step.itemId, next)}
        />
      ) : null}

      {step.kind === "review" ? (
        <ReviewStep
          mode={mode}
          unit={assignedVehicle.unit}
          checklist={dvirChecklist}
          odometerKm={odometerKm}
          answers={answers}
          defects={defects}
          result={deriveResult(severities)}
          naItems={naItems.map(labelOf)}
          unansweredCount={unansweredCount}
          ungradedCount={ungradedCount}
          recent={dvirSubmissions}
          storageFailed={hydrated && storageFailed()}
          submitError={submitError}
          onGoToStep={goTo}
          onSubmit={() => void submit()}
        />
      ) : null}
    </WizardFrame>
  );
}

/** Flattened once at module load — the checklist is static data, not state. */
const CHECKLIST_ITEMS: { id: string; label: string; group: string }[] = dvirChecklist.flatMap((g) =>
  g.items.map((i) => ({ id: i.id, label: i.label, group: g.group })),
);

function labelOf(itemId: string): string {
  return CHECKLIST_ITEMS.find((i) => i.id === itemId)?.label ?? itemId;
}

function modeTitle(mode: InspectionMode): string {
  return mode === "PreTrip" ? "Pre-trip inspection" : "Post-trip inspection";
}
