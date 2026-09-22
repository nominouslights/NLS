"use client";

import { colors, fonts } from "@/lib/theme";
import { gap, type } from "@/lib/tablet";
import { Panel } from "@/components/ui/Panel";
import { TouchButton } from "@/components/ui-tablet/TouchButton";
import { StatusBanner } from "@/components/ui-tablet/StatusBanner";
import { CardRow, FieldLine, Heading, MockTag, TabletChip } from "../shared";
import { checkStepId, defectStepId } from "@/lib/inspectionSteps";
import { severityGlyph, severityKind, type InspectionResultName } from "@/lib/inspectionGate";
import type { DraftDefect } from "@/lib/inspectionStore";
import type { CheckState, DvirSubmission, ChecklistGroup, InspectionMode } from "@/lib/types";

// APP-LOCAL. The last step: everything the driver is about to attest to, then the attestation.
//
// THE ONLY SCROLLING SURFACE IN THE FLOW. WizardFrame deliberately has no scroll container —
// a driver must never be able to leave part of a single question off screen — but 22 rows of
// review is reference material being re-read, not a question being answered, so it owns its own
// overflow here rather than pushing one into the frame.
//
// It carries the two seam admissions on screen as well as in the payload comment, because a
// compliance screen that looks authoritative in a demo is the failure mode this app's MockTag
// convention exists to prevent.

const RESULT_META: Record<InspectionResultName, { kind: "ontime" | "soon" | "over"; label: string }> =
  {
    Pass: { kind: "ontime", label: "Pass" },
    PassWithDefects: { kind: "soon", label: "Pass with defects" },
    Fail: { kind: "over", label: "Fail" },
  };

const ANSWER_META: Record<CheckState, { kind: "ontime" | "over" | "off"; label: string }> = {
  pass: { kind: "ontime", label: "Pass" },
  defect: { kind: "over", label: "Defect" },
  na: { kind: "off", label: "N/A" },
};

export function ReviewStep({
  mode,
  unit,
  checklist,
  odometerKm,
  answers,
  defects,
  result,
  naItems,
  unansweredCount,
  ungradedCount,
  recent,
  storageFailed,
  submitError,
  onGoToStep,
  onSubmit,
}: {
  mode: InspectionMode;
  unit: string;
  checklist: ChecklistGroup[];
  odometerKm: number | null;
  answers: Record<string, CheckState>;
  defects: Record<string, DraftDefect>;
  result: InspectionResultName;
  naItems: string[];
  unansweredCount: number;
  ungradedCount: number;
  recent: DvirSubmission[];
  storageFailed: boolean;
  /** Set when the queue rejected the capture. The draft is still on the device. */
  submitError: string | null;
  onGoToStep: (stepId: string) => void;
  onSubmit: () => void;
}) {
  const rm = RESULT_META[result];
  const complete = unansweredCount === 0 && ungradedCount === 0 && odometerKm !== null;

  const blockedReason =
    odometerKm === null
      ? "Enter the odometer reading first — an inspection cannot be certified without it."
      : unansweredCount > 0
        ? `${unansweredCount} item(s) still unanswered — an inspection cannot be certified with blanks.`
        : ungradedCount > 0
          ? `${ungradedCount} defect(s) have no severity — a defect without one cannot be graded.`
          : "";

  return (
    <div style={{ flex: 1, minHeight: 0, overflowY: "auto", paddingBottom: gap.page }}>
      {submitError ? (
        <StatusBanner kind="over" title="This inspection was not captured.">
          {submitError} Your answers are still on this device — tap{" "}
          <strong>Certify &amp; submit</strong> again. Nothing has been recorded, so do not
          re-answer the checklist.
        </StatusBanner>
      ) : null}

      {storageFailed ? (
        <StatusBanner kind="soon" title="This draft is not being saved on this device.">
          Local storage is unavailable, so your answers survive navigation but not a reload.
          Finish and certify in this session rather than coming back to it.
        </StatusBanner>
      ) : null}

      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: gap.row,
          flexWrap: "wrap",
          marginBottom: gap.row,
        }}
      >
        <div
          style={{
            fontFamily: fonts.condensed,
            fontWeight: 700,
            fontSize: 34,
            lineHeight: 1.1,
            color: colors.headingBright,
          }}
        >
          {mode === "PreTrip" ? "Pre-trip" : "Post-trip"} review · {unit}
        </div>
        <TabletChip kind={rm.kind} label={rm.label} />
        <span
          style={{
            fontFamily: fonts.mono,
            fontSize: type.value,
            fontVariantNumeric: "tabular-nums",
            color: colors.textSecondary,
          }}
        >
          {odometerKm === null ? "— km" : `${odometerKm.toLocaleString("en-CA")} km`}
        </span>
        <TouchButton variant="secondary" onClick={() => onGoToStep("odometer")}>
          Change
        </TouchButton>
      </div>

      {/* A plain Unicode apostrophe, not &rsquo;: `title` is a string prop, so relying on JSX
          entity decoding in an attribute is a trap worth not setting. */}
      <StatusBanner kind="off" title="The overall result is the server’s to decide.">
        The <strong>{rm.label}</strong> shown above is worked out on this device from the defects
        below, mirroring the backend&rsquo;s own rule (no defects → Pass; only minor defects →
        Pass with defects; any major or out-of-service defect → Fail). It is{" "}
        <strong>not sent</strong> — the server derives it, so the two can never disagree.
      </StatusBanner>

      {naItems.length > 0 ? (
        <StatusBanner
          kind="soon"
          title={`${naItems.length} item(s) marked N/A will not appear in the submission.`}
        >
          The backend&rsquo;s checklist row carries a plain pass/fail flag with no third state, so
          an N/A item is <strong>omitted</strong> rather than sent as passed — marking an
          unapplicable item &ldquo;passed&rdquo; would be a false attestation in a compliance
          record. Omission loses information, which is visible and fixable. Open question for the
          backend: should that flag become a tri-state? Items: {naItems.join(", ")}.
        </StatusBanner>
      ) : null}

      {result === "Fail" ? (
        <StatusBanner kind="over" title="A failing defect does not take the vehicle out of service here.">
          {unit}&rsquo;s out-of-service flag is the server&rsquo;s, and nothing in this build
          writes it — so the Trips screen&rsquo;s vehicle-status rule will keep reading{" "}
          <strong>no open failure</strong> even after you certify this. Boarding is blocked on
          this device, and that is all. Call dispatch.
        </StatusBanner>
      ) : null}

      {checklist.map((group) => (
        <div key={group.group}>
          <Heading>{group.group}</Heading>
          {group.items.map((item) => {
            const answer = answers[item.id] ?? null;
            const meta = answer ? ANSWER_META[answer] : null;
            const defect = answer === "defect" ? defects[item.id] : undefined;

            return (
              <CardRow key={item.id}>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <FieldLine
                    label={item.id}
                    value={
                      defect
                        ? `${item.label} — ${defect.note.trim() || "no note"}`
                        : item.label
                    }
                  />
                </div>
                <div
                  style={{
                    flex: "none",
                    display: "flex",
                    alignItems: "center",
                    gap: 10,
                  }}
                >
                  {meta ? (
                    <TabletChip kind={meta.kind} label={meta.label} />
                  ) : (
                    <TabletChip kind="off" label="Not answered" />
                  )}
                  {defect ? (
                    defect.severity ? (
                      <TabletChip
                        kind={severityKind(defect.severity)}
                        glyph={severityGlyph(defect.severity)}
                        label={defect.severity}
                      />
                    ) : (
                      <TabletChip kind="off" label="No severity" />
                    )
                  ) : null}
                  <TouchButton
                    variant="secondary"
                    onClick={() =>
                      onGoToStep(defect ? defectStepId(item.id) : checkStepId(item.id))
                    }
                  >
                    Change
                  </TouchButton>
                </div>
              </CardRow>
            );
          })}
        </div>
      ))}

      <Heading>Certify</Heading>
      <Panel style={{ padding: "18px 20px" }}>
        <div
          style={{
            fontFamily: fonts.body,
            fontSize: type.label,
            color: colors.textSecondary,
            lineHeight: 1.6,
          }}
        >
          I certify that I have inspected this vehicle in accordance with NSC Standard 11 and
          that the entries above are accurate.
        </div>
        <div style={{ display: "flex", gap: 12, marginTop: 16, flexWrap: "wrap" }}>
          <TouchButton onClick={onSubmit} disabled={!complete} disabledReason={blockedReason}>
            Certify &amp; submit
          </TouchButton>
          <TouchButton
            variant="secondary"
            disabled
            disabledReason="No attachment endpoint exists for inspections or defects on the backend yet."
          >
            Attach photo
          </TouchButton>
        </div>
        {complete ? null : (
          <div
            style={{
              fontFamily: fonts.body,
              fontSize: 15,
              color: colors.textDim,
              marginTop: 12,
              lineHeight: 1.55,
            }}
          >
            {blockedReason}
          </div>
        )}
      </Panel>

      <Heading right={<MockTag />}>Recent inspections</Heading>
      {recent.map((s) => (
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
    </div>
  );
}
