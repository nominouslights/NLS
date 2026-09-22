"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import {
  generateScheduleTrips,
  previewScheduleTripGeneration,
  shortDateLabel,
  todayIso,
  type ScheduleTemplateRecord,
  type ScheduleTripGenerationResult,
} from "@/lib/api/trips";
import { clampThroughIso, endOfNextMonthIso, maxGenerateThroughIso } from "@/lib/generation";
import { ModalShell } from "@/components/ui/ModalShell";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";
import { DetailRow, Panel, SectionLabel } from "@/components/ui/Panel";
import { DateField } from "@/components/ui/Field";

// Generate Trips dialog (Routes & Schedules → template detail, and auto-opened
// right after a new template is saved). The dispatcher picks a "through" date
// — default end of next month, hard cap 12 months ahead — and sees a live
// preview from GET …/generate/preview before POST …/generate materializes the
// missing occurrences. Both calls run the same backend guards, so a template
// with no default driver/vehicle, a missing route, or an inactive flag fails
// in the PREVIEW, with the backend's message shown verbatim plus a hint about
// which button fixes it. Re-running is idempotent: occurrences that already
// exist are counted as "already generated" and never touched.
//
// Modeled on Billing's GenerateDraftModal (preview keyed by its inputs so a
// stale response simply stops matching) and SendAccrualsEmailModal (the
// post-action result block).

/** The backend's error codes are namespaced ("Trips.ScheduleTemplate.X");
 *  match on the last segment so a namespace rename cannot silently drop a hint. */
function errorSuffix(code: string): string {
  return code.slice(code.lastIndexOf(".") + 1);
}

/** Which in-console action clears the error — the message itself is the backend's. */
function errorHint(code: string): string | null {
  switch (errorSuffix(code)) {
    case "TemplateInactive":
      return "Use ACTIVATE on the template first, then reopen this dialog.";
    case "NoDefaultDriver":
    case "DefaultDriverUnavailable":
    case "NoDefaultVehicle":
    case "DefaultVehicleUnavailable":
    case "RouteMissing":
      return "Fix it with EDIT TEMPLATE, then reopen this dialog.";
    case "InvalidGenerationWindow":
      return "Pick a date from today up to 12 months ahead.";
    default:
      return null;
  }
}

/** Vermillion chip carrying the backend message, plus the fix-it hint when one applies. */
function ErrorBlock({ code, message }: { code: string; message: string }) {
  const hint = errorHint(code);
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      <StatusChip kind="over" label={message} />
      {hint && (
        <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, lineHeight: 1.5 }}>{hint}</span>
      )}
    </div>
  );
}

type Failure = { code: string; message: string };

function failureOf(e: unknown, fallback: string): Failure {
  return e instanceof ApiError ? { code: e.code, message: e.message } : { code: "", message: fallback };
}

export default function GenerateTripsModal({
  template,
  onClose,
}: {
  template: ScheduleTemplateRecord;
  onClose: () => void;
}) {
  // Captured once — the window must not drift if the dialog straddles midnight.
  const [today] = useState(() => todayIso());
  const maxIso = maxGenerateThroughIso(today);
  const [through, setThrough] = useState(() => endOfNextMonthIso(today));
  const oneWay = template.returnDepartureTime === null;

  // Preview keyed by template + through (+ a nonce so a 409 can force a
  // refetch of the same key); a changed date simply stops matching.
  const [nonce, setNonce] = useState(0);
  const [preview, setPreview] = useState<{ key: string; result: ScheduleTripGenerationResult } | null>(null);
  const [previewError, setPreviewError] = useState<{ key: string; failure: Failure } | null>(null);
  const [busy, setBusy] = useState(false);
  const [submitError, setSubmitError] = useState<Failure | null>(null);
  const [result, setResult] = useState<ScheduleTripGenerationResult | null>(null);

  // No preview once the run has landed — the result block is the truth now.
  const previewKey = result === null ? `${template.id}|${through}|${nonce}` : null;

  useEffect(() => {
    if (!previewKey) return;
    let active = true;
    const [id, thr] = previewKey.split("|");
    previewScheduleTripGeneration(id, thr).then(
      (r) => {
        if (active) setPreview({ key: previewKey, result: r });
      },
      (e) => {
        if (active) setPreviewError({ key: previewKey, failure: failureOf(e, "Failed to load the preview.") });
      },
    );
    return () => {
      active = false;
    };
  }, [previewKey]);

  const previewResult = preview !== null && preview.key === previewKey ? preview.result : null;
  const previewErr = previewError !== null && previewError.key === previewKey ? previewError.failure : null;
  const canGenerate = !busy && previewResult !== null && previewResult.tripCount > 0;

  function pickThrough(v: string) {
    // The shared DateField has no min/max — clamp here instead (lib/generation.ts).
    setThrough(clampThroughIso(today, v));
    setSubmitError(null);
  }

  async function submit() {
    if (!canGenerate) return;
    setBusy(true);
    setSubmitError(null);
    try {
      setResult(await generateScheduleTrips(template.id, through));
    } catch (e) {
      setSubmitError(failureOf(e, "Failed to generate trips — please try again."));
      // Another run landed first (409): the counts on screen are stale, so
      // drop the preview and fetch it again for the same date.
      if (e instanceof ApiError && (e.status === 409 || errorSuffix(e.code) === "GenerationConflict")) {
        setPreview(null);
        setNonce((n) => n + 1);
      }
    } finally {
      setBusy(false);
    }
  }

  const muted = { fontFamily: fonts.body, fontSize: 12, color: colors.textDim, lineHeight: 1.5 } as const;

  return (
    <ModalShell
      eyebrow={`Operations · ${template.name}`}
      title="Generate Trips"
      onClose={onClose}
      maxWidth={600}
      footer={
        result !== null ? (
          <ActionButton variant="primary" onClick={onClose}>
            CLOSE
          </ActionButton>
        ) : (
          <>
            <ActionButton onClick={onClose}>CANCEL</ActionButton>
            <ActionButton variant="primary" onClick={submit} disabled={!canGenerate}>
              {busy
                ? "GENERATING…"
                : previewResult !== null && previewResult.tripCount > 0
                  ? `GENERATE ${previewResult.tripCount} TRIP${previewResult.tripCount === 1 ? "" : "S"}`
                  : "GENERATE TRIPS"}
            </ActionButton>
          </>
        )
      }
    >
      <DateField
        label="Generate through"
        value={through}
        onChange={pickThrough}
        disabled={busy || result !== null}
        hint={
          <span style={{ color: colors.textFaint }}>
            — today through {shortDateLabel(maxIso)} · up to 12 months ahead
          </span>
        }
      />

      {/* live preview — same guards as the real run, nothing persisted */}
      {result === null && (
        <div style={{ marginTop: 18 }}>
          <SectionLabel>Preview</SectionLabel>
          {previewErr ? (
            <ErrorBlock code={previewErr.code} message={previewErr.message} />
          ) : previewResult === null ? (
            <div style={muted}>Counting occurrences through {shortDateLabel(through)}…</div>
          ) : previewResult.tripCount === 0 ? (
            <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              {previewResult.alreadyExisted > 0 ? (
                <StatusChip kind="ontime" label="Everything through this date already exists" />
              ) : (
                <StatusChip kind="soon" label="No occurrences in this range" />
              )}
              {previewResult.alreadyExisted > 0 && (
                <span style={muted}>
                  {previewResult.alreadyExisted} trip{previewResult.alreadyExisted === 1 ? "" : "s"} already generated
                  from {shortDateLabel(previewResult.from)} through {shortDateLabel(previewResult.through)}.
                </span>
              )}
            </div>
          ) : (
            <Panel>
              <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                <DetailRow
                  label="New trips"
                  value={String(previewResult.tripCount)}
                  valueStyle={{ fontFamily: fonts.mono, color: colors.textPrimary }}
                />
                <DetailRow
                  label="Already generated"
                  value={String(previewResult.alreadyExisted)}
                  valueStyle={{ fontFamily: fonts.mono }}
                />
                {!oneWay && (
                  <DetailRow
                    label="Outbound · return"
                    value={`${previewResult.outbound} · ${previewResult.inbound}`}
                    valueStyle={{ fontFamily: fonts.mono }}
                  />
                )}
                <DetailRow
                  label="First – last date"
                  value={
                    previewResult.firstServiceDate && previewResult.lastServiceDate
                      ? `${shortDateLabel(previewResult.firstServiceDate)} – ${shortDateLabel(previewResult.lastServiceDate)}`
                      : "—"
                  }
                  valueStyle={{ fontFamily: fonts.mono }}
                />
              </div>
            </Panel>
          )}
          {submitError && (
            <div style={{ marginTop: 12 }}>
              <ErrorBlock code={submitError.code} message={submitError.message} />
            </div>
          )}
        </div>
      )}

      {/* outcome of the run just performed */}
      {result !== null && (
        <div style={{ marginTop: 18 }}>
          <SectionLabel>Result</SectionLabel>
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            <div>
              <StatusChip kind="ontime" label="Generated" />
            </div>
            <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary, lineHeight: 1.5 }}>
              Created {result.tripCount} trip{result.tripCount === 1 ? "" : "s"} from {shortDateLabel(result.from)}{" "}
              through {shortDateLabel(result.through)} · {result.alreadyExisted} already existed
            </span>
          </div>
        </div>
      )}

      {/* the caveat that outlives this dialog — generated trips are fixed */}
      <div
        style={{
          marginTop: 18,
          padding: "12px 14px",
          borderRadius: 10,
          background: colors.cardBg,
          border: `1px solid ${colors.borderSubtle}`,
          display: "flex",
          flexDirection: "column",
          gap: 8,
        }}
      >
        <div>
          <StatusChip kind="soon" label="Generated trips are fixed" />
        </div>
        <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, lineHeight: 1.55 }}>
          Later template edits and Special Dates only shape dates that have not been generated yet — they do not
          change a trip that already exists. To change or remove one, cancel or edit it from Trips.
        </span>
        {template.clientId && (
          <span style={muted}>Future trips appear in Reports → Client Accruals as upcoming estimates.</span>
        )}
      </div>
    </ModalShell>
  );
}
