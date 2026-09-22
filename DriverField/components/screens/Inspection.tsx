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
  itemsFor,
  NL_PTI_01_CERTIFICATION,
  type InspectionSubGroup,
} from "@/lib/inspectionForm";
import {
  certifiedToday,
  discardDraft,
  getDraft,
  recordCertification,
  setAnswer,
  setDefect,
  setNote,
  setOdometer,
  setStep,
  startDraft,
  storageFailed,
  type DraftDefect,
  type LocalCertification,
} from "@/lib/inspectionStore";
import { useInspectionStoreHydrated } from "@/lib/useInspectionStore";
import {
  checkStatePassed,
  checkStateToWire,
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
// WHY IT IS SHAPED THIS WAY: the previous version rendered every item as one continuous scroll,
// three or four screens deep, with the legal attestation at the bottom. On a dash-mounted
// 10-inch tablet, in northern daylight, with gloves on, a driver loses their place and sees the
// attestation least. That was true of the old 22-item list and is unarguable now the checklist
// is form NL-PTI-01: 67 to 80 rows depending on the unit and the half of the form.
//
// THE CHECKLIST IS NOT MOCK DATA. It comes from lib/inspectionForm.ts, a byte-identical copy of
// Dispatcher/lib/inspectionForm.ts, narrowed by itemsFor(unit, mode). `unit` is the assigned
// vehicle's, `mode` the pre/post-trip half. Those two arguments are the whole reason the
// progress denominator is derived rather than written down — there are four correct answers.
//
// THE DRAFT SURVIVES NAVIGATION AND A RELOAD. Answers used to live in local useState, and
// Console.tsx unmounts screens on nav, so tapping away to Hours destroyed the lot — which this
// repo frames as a compliance failure, not a lost draft. They now live in
// lib/inspectionStore.ts, read here through ONE useSyncExternalStore.
//
// ORDER ON SUBMIT: enqueue → recordCertification → discardDraft. If enqueue throws, the
// driver's whole walk-around survives. See submit() below.
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

  // The unit the form is narrowed for. `assignedVehicle.unit` is "NL-01"/"NL-02"; anything
  // itemsFor() does not recognise gets the FULL superset, which is the fail-safe direction on a
  // compliance form (more questions, never fewer). Never "helpfully" default it.
  const unit = assignedVehicle.unit;

  const draft = hydrated ? getDraft(mode, vehicleId) : null;
  const answers: Record<string, CheckState> = draft?.answers ?? {};
  const notes: Record<string, string> = draft?.notes ?? {};
  const defects: Record<string, DraftDefect> = draft?.defects ?? {};
  const odometerKm = draft?.odometerKm ?? null;

  const groups = itemsFor(unit, mode);
  const items = flatten(groups);

  const steps = buildSteps(answers, unit, mode);
  const step = resolveStep(steps, draft?.stepId ?? null);
  const index = steps.findIndex((s) => s.id === step.id);

  const alreadyCertified = hydrated ? certifiedToday(mode, vehicleId) : null;
  const finished = done ?? (draft === null ? alreadyCertified : null);

  const severities: DefectSeverity[] = Object.entries(defects)
    .filter(([itemId]) => answers[itemId] === "defect")
    .map(([, d]) => d.severity)
    .filter((s): s is DefectSeverity => s !== null);

  const unansweredCount = items.filter((i) => answers[i.key] === undefined).length;
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
    const nextSteps = buildSteps(updated.answers, unit, mode);
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
    //  • `result` IS NOT SENT. VehicleInspection.DeriveResult derives it. deriveResult() runs
    //    here for the banner, the local certification and the boarding gate only.
    //  • `item` IS THE CATALOGUE KEY, NEVER THE LABEL. NL-PTI-01's `key` is the wire value
    //    stored as InspectionChecklistItem.Item, and half of the `(InspectionId, Item)` address
    //    a defect is filed against. Sending the label instead would file defects at addresses
    //    the Dispatch Console cannot resolve — and several labels deliberately differ from
    //    their key (the "(NL-02)" suffixes, and the four "Interior: " rows).
    //  • `state` IS THE TRI-STATE, and N/A is no longer dropped. ChecklistItemState is
    //    `Ok | Defect | NotApplicable`; this app used to OMIT every N/A row and name it in a
    //    client-only `naItems` field, because Passed was a bare bool and `passed: true` for an
    //    unapplicable row is a false attestation. That field is GONE — re-adding it would hide
    //    rows the wire now carries. `passed` still travels because the input record requires
    //    it, derived exactly as the aggregate re-derives it (checkStatePassed).
    //  • `note` per row is NL-PTI-01's Notes column, on EVERY row. Distinct from a defect's
    //    note: one describes the row, the other the fault, and a defect may carry both.
    //  • `certificationStatement` is NL_PTI_01_CERTIFICATION verbatim from the copied
    //    catalogue — the sentence the driver actually signed, which is what
    //    VehicleInspection.CertificationStatement exists to keep.
    //  • `severity` crosses through severityToWire(). InspectionDefectSeverity is
    //    `Minor | Major | OutOfService` — no spaces — and the display string would be rejected
    //    by the default System.Text.Json enum converter. Pinned in lib/wire.test.ts. The form
    //    offers only Minor and Major; OutOfService stays reachable for historical rows.
    //  • `source` is never omitted: the backend defaults Source to Dispatcher, which would
    //    attribute a driver's inspection to a dispatcher who never touched the vehicle.
    //  • `tripNumber`, not a trip id — the wire field is TripNumber, a string.
    //  • NO `enteredBy`. lib/wire.test.ts already pins the analogous HOS rule: a driver-app
    //    submission has no dispatcher, so the field is absent rather than invented.
    //  • NO `recurrenceOfInspectionId` — a driver cannot know a defect is a recurrence; that
    //    pointer belongs to the dispatcher Re-report path.
    //  • `attestations: [true]` is one flag because IReadOnlyList<bool> has no labels defined
    //    anywhere on the backend. A multi-checkbox UI would be inventing a contract.
    //  • Weather, temperature, road conditions, visibility and fuel are omitted: more steps on
    //    a screen that posts nowhere. Vehicle.tsx already says on screen that fuel has no
    //    backend resource.
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
        // EVERY answered row, N/A included. Unanswered rows cannot reach here — the review
        // step blocks Certify on unansweredCount — but the filter is the belt to that braces.
        checklist: items
          .filter((i) => current.answers[i.key] !== undefined)
          .map((i) => {
            const state = current.answers[i.key];
            return {
              group: i.groupKey,
              item: i.key,
              passed: checkStatePassed(state),
              state: checkStateToWire(state),
              note: (current.notes[i.key] ?? "").trim() || null,
            };
          }),
        defects: graded.map((g) => ({
          item: g.itemId,
          severity: severityToWire(g.severity),
          note: g.note.trim() === "" ? null : g.note.trim(),
        })),
        certificationStatement: NL_PTI_01_CERTIFICATION,
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
      // NL-PTI-01 offers Minor and Major only, so this is false for anything certified here.
      // The field stays because LocalCertification is also read for records graded before the
      // form change, and the gate's banner branches on it.
      outOfService: graded.some((g) => g.severity === "Out of Service"),
    };

    // Order matters: enqueue first (above), then record, then discard. If enqueue throws, the
    // draft is still on the device and the driver re-taps Certify rather than re-walking the
    // whole vehicle.
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
      // reports POSITION ("Review · 67 of 67" = you are past every check), not completeness,
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
          area={step.area}
          group={step.group}
          label={step.label}
          checkFor={step.checkFor}
          value={answers[step.itemId] ?? null}
          note={notes[step.itemId] ?? ""}
          onAnswer={(next) => answer(step.itemId, next)}
          onNote={(next) => setNote(mode, vehicleId, step.itemId, next)}
        />
      ) : null}

      {step.kind === "defect" ? (
        <DefectStep
          area={step.area}
          group={step.group}
          label={step.label}
          category={step.category}
          categoryNote={step.categoryNote}
          defect={defects[step.itemId] ?? { severity: null, note: "" }}
          onChange={(next) => setDefect(mode, vehicleId, step.itemId, next)}
        />
      ) : null}

      {step.kind === "review" ? (
        <ReviewStep
          mode={mode}
          unit={unit}
          groups={groups}
          odometerKm={odometerKm}
          answers={answers}
          notes={notes}
          defects={defects}
          result={deriveResult(severities)}
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

/**
 * The rows for this unit and mode, flattened, each carrying its sub-group's wire key.
 *
 * NOT cached at module load, unlike the array this replaces: the list depends on the unit and
 * the half of the form, so a module constant could only ever be right for one of the four
 * combinations. Same reason lib/inspectionSteps.ts has no CHECK_COUNT any more.
 */
function flatten(
  groups: InspectionSubGroup[],
): { key: string; label: string; checkFor: string; groupKey: string }[] {
  return groups.flatMap((g) =>
    g.items.map((i) => ({
      key: i.key,
      label: i.label,
      checkFor: i.checkFor,
      groupKey: g.key,
    })),
  );
}

function modeTitle(mode: InspectionMode): string {
  return mode === "PreTrip" ? "Pre-trip inspection" : "Post-trip inspection";
}
