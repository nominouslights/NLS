"use client";

import { useState } from "react";
import { colors, fonts, statusMeta, type StatusKind } from "@/lib/theme";
import { gap, radius, type } from "@/lib/tablet";
import { Panel } from "@/components/ui/Panel";
import { ModalShell } from "@/components/ui/ModalShell";
import { TouchTile } from "@/components/ui-tablet/TouchTile";
import { StatusBanner } from "@/components/ui-tablet/StatusBanner";
import { TouchButton } from "@/components/ui-tablet/TouchButton";
import { DutyControl } from "@/components/ui-tablet/DutyControl";
import { DutyChip } from "@/components/ui-tablet/DutyChip";
import { Screen, Heading, CardRow, FieldLine, Num, TabletChip, EmptyNote } from "./shared";
import { formatDurationH } from "@/lib/data";
import type { DutyDaySummary, EventSourceWire } from "@/lib/api/hos";
import { HOS_LIMITS, HosWire, type DateOnly } from "@/lib/hos/engine";
import { band, clockLabel, dayBand, dayLabel, durationLabel, dutyStateOf, ruleShort } from "@/lib/hos/display";
import { isCertifyCommandPayload, isDutyCommandPayload } from "@/lib/hos/dutyCommand";
import { CALENDAR, HISTORY_DAYS, type LedgerSnapshot } from "@/lib/hos/ledger";
import { useHosLedger } from "@/lib/hos/useHosLedger";
import { allCommands, discard, enqueue, failedCommands, retry } from "@/lib/sync/queue";
import { useSyncState } from "@/lib/sync/useSyncState";
import type { QueuedCommand } from "@/lib/sync/types";

// Hours of service, LIVE. Every figure here is `evaluate(ledger, asOf, calendar)` from
// lib/hos/engine.ts — the byte-identical mirror of the server's rule set — on the ledger store
// (lib/hos/ledger.ts), which holds the server's last 14 days (GET /api/drivers/{id}/hos/days)
// plus this tablet's not-yet-confirmed duty changes. The four tiles, the window, the next
// required off-duty and the 7-day bar share ONE band set (lib/hos/display.ts). There is no
// local threshold anywhere on this screen, and no MockTag: the numbers are the driver's.
//
// The banner is not decoration. The record of duty status lives on the bus's ELD and keeping
// two records is a severe violation, so the platform is a planning ledger and ELD mirror and
// says so on every figure. The line under it says WHICH copy the figures are built on (live,
// cached on the device, or none yet) because a dead zone must be visible, not silent.
//
// Three things the driver can do here, and all three go through the queue:
//   - change duty status (DutyControl → hos.duty)
//   - certify a day (the attestation sheet → hos.certify) — the driver's own act under s.84,
//     which no dispatcher can do for them, so the sheet says what is being signed
//   - deal with a change the server refused: RETRY or DISCARD, with the server's message
//
// The Source column is load-bearing and is why lib/wire.test.ts exists: "Driver App" and
// "Manual (paper backup)" are the exact strings HosWire emits.

/** How a source reads on a badge: colour + glyph + the wire string, never the tint alone. */
const SOURCE_BADGE: Record<EventSourceWire, { kind: StatusKind; glyph: string }> = {
  "Driver App": { kind: "info", glyph: "●" },
  "ELD transcription": { kind: "off", glyph: "≡" },
  "Manual (paper backup)": { kind: "soon", glyph: "✎" },
  "Other carrier (declared)": { kind: "off", glyph: "◇" },
  System: { kind: "off", glyph: "⚙" },
};

export default function Hours() {
  const snapshot = useHosLedger();
  const { evaluation, days, hydration } = snapshot;
  // The parked list is read off the queue on every render; the sync state is what re-renders
  // this screen when its failed count moves (retry, discard, a drain that parked something).
  const sync = useSyncState();
  const parked = sync.failed > 0 ? failedCommands() : [];
  const [sheet, setSheet] = useState<DateOnly | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const b = band(evaluation);
  const week = evaluation.days.slice(-7);
  const maxDay = Math.max(...week.map((d) => d.onDutySeconds), HOS_LIMITS.dailyOnDutySeconds);
  const mustStop = evaluation.shift.mustStopDrivingByUnix;
  const next = evaluation.nextRequiredOffDuty;
  const today = CALENDAR.dayOf(evaluation.asOfUnix);
  const todayEvents = snapshot.events.filter((e) => CALENDAR.dayOf(e.atUnix) === today);
  // The ledger holds the server's 21-day lookback; the regulation's record window is 14.
  const history = days.slice(-HISTORY_DAYS);
  const todayCertify = certifyState(today, snapshot);

  async function act(run: () => Promise<unknown>) {
    try {
      setActionError(null);
      await run();
    } catch (err) {
      setActionError(err instanceof Error ? err.message : String(err));
    }
  }

  return (
    <Screen eyebrow="Compliance" title="Hours of service" right={<HydrationChip hydration={hydration} />}>
      <StatusBanner kind="info" title="Planning figures — your record of duty status is on the bus's ELD.">
        These figures come from the same rule engine the dispatch console and the server use,
        applied to the server&rsquo;s copy of your last 14 days plus the changes queued on this
        tablet. They plan your day; they are not the record.{" "}
        <HydrationLine hydration={hydration} />
      </StatusBanner>

      {parked.length > 0 ? (
        <ParkedCommands
          commands={parked}
          onRetry={(id) => void act(() => retry(id))}
          onDiscard={(id) => void act(() => discard(id))}
        />
      ) : null}

      {actionError ? (
        <StatusBanner kind="over" title="That did not work on this device.">
          {actionError}
        </StatusBanner>
      ) : null}

      <div style={{ display: "flex", gap: gap.row, marginBottom: gap.section }}>
        <TouchTile
          label={`Driving left today (max ${formatDurationH(HOS_LIMITS.dailyDrivingSeconds / 3600)})`}
          value={durationLabel(evaluation.remainingDrivingTodaySeconds)}
          unit="left"
          kind={b.kind}
          statusLabel={b.label}
        />
        <TouchTile
          label={`On duty left today (max ${formatDurationH(HOS_LIMITS.dailyOnDutySeconds / 3600)})`}
          value={durationLabel(evaluation.remainingOnDutyTodaySeconds)}
          unit="left"
          kind={b.kind}
          statusLabel={b.label}
        />
        <TouchTile
          label={`Shift window (${formatDurationH(HOS_LIMITS.shiftElapsedSeconds / 3600)})`}
          value={mustStop === null ? "—" : clockLabel(mustStop)}
          unit={mustStop === null ? "between shifts" : "must stop driving by"}
          kind={b.kind}
          statusLabel={b.label}
          footnote={
            evaluation.shift.startUnix === null
              ? "No shift open — the window starts with your next on-duty time."
              : `Shift began ${clockLabel(evaluation.shift.startUnix)} · ${durationLabel(evaluation.shift.elapsedSeconds)} elapsed`
          }
        />
        <TouchTile
          label={`Cycle (max ${formatDurationH(HOS_LIMITS.cycleOnDutySeconds / 3600)} / ${HOS_LIMITS.cycleDays} days)`}
          value={durationLabel(evaluation.cycle.remainingSeconds)}
          unit="left"
          kind={b.kind}
          statusLabel={b.label}
          footnote={`${durationLabel(evaluation.cycle.onDutySeconds)} on duty since ${dayLabel(evaluation.cycle.windowStartDate)}`}
        />
      </div>

      {next ? (
        <NextOffDuty
          by={clockLabel(next.mustBeOffByUnix)}
          rule={ruleShort(next.reason)}
          minimum={durationLabel(next.minimumSeconds)}
          shortfall={next.dailyShortfallSeconds > 0 ? durationLabel(next.dailyShortfallSeconds) : null}
          kind={b.kind}
        />
      ) : null}

      <Heading>Duty status</Heading>
      <Panel style={{ padding: "18px 20px", marginBottom: gap.section }}>
        <DutyControl />
      </Panel>

      <Heading
        right={
          todayCertify.certified ? (
            <TabletChip kind="ontime" label="Certified" />
          ) : (
            <TouchButton
              onClick={() => setSheet(today)}
              disabled={todayCertify.reason !== null}
              disabledReason={todayCertify.reason ?? undefined}
            >
              Certify today
            </TouchButton>
          )
        }
      >
        Today · {today}
      </Heading>
      {todayCertify.reason !== null && !todayCertify.certified ? (
        <Reason kind={todayCertify.kind}>
          <strong style={{ fontWeight: 600 }}>Certify today unavailable</strong> — {todayCertify.reason}
        </Reason>
      ) : null}
      {todayEvents.length === 0 ? (
        <EmptyNote>No duty events today yet. Your first duty change starts the day&rsquo;s record.</EmptyNote>
      ) : (
        todayEvents.map((e) => (
          <CardRow key={e.eventId ?? e.clientEventId ?? `${e.atUnix}-${e.sequence}`}>
            <div style={{ flex: "none", width: 90 }}>
              <Num size={type.label} color={colors.headingBright}>
                {clockLabel(e.atUnix)}
              </Num>
            </div>
            <div style={{ flex: "none", width: 220 }}>
              <DutyChip state={dutyStateOf(e.status)} />
            </div>
            <div style={{ flex: "none" }}>
              <SourceBadge source={HosWire.source(e.source) as EventSourceWire} />
            </div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <FieldLine label="Note" value={e.note || "—"} />
            </div>
            <div style={{ flex: "none" }}>
              {e.clientEventId !== null ? (
                <TabletChip kind="soon" label="Queued on this tablet" />
              ) : (
                <TabletChip kind="ontime" label="On file" />
              )}
            </div>
          </CardRow>
        ))
      )}

      <Heading>Last 7 days</Heading>
      <Panel style={{ padding: "18px 20px", marginBottom: gap.section }}>
        <div style={{ display: "flex", alignItems: "flex-end", gap: 10, height: 156 }}>
          {week.map((d) => {
            const db = dayBand(d, evaluation);
            const m = statusMeta(db.kind);
            return (
              <div
                key={d.date}
                style={{ flex: "1 1 0", display: "flex", flexDirection: "column", alignItems: "center", gap: 6 }}
                title={`${d.date} · ${durationLabel(d.onDutySeconds)} on duty · ${durationLabel(d.drivingSeconds)} driving · ${db.label}`}
              >
                <span style={{ fontFamily: fonts.mono, fontSize: 13, color: m.t }}>
                  {d.onDutySeconds > 0 ? durationLabel(d.onDutySeconds) : "—"}
                </span>
                <div
                  style={{
                    width: "100%",
                    height: Math.max(4, (d.onDutySeconds / maxDay) * 84),
                    borderRadius: radius.control,
                    background: m.c,
                  }}
                />
                <span style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textDim }}>
                  {dayLabel(d.date)}
                </span>
                <span style={{ fontFamily: fonts.semiCondensed, fontSize: 12, letterSpacing: ".06em", textTransform: "uppercase", color: m.t }}>
                  <span aria-hidden>{m.g}</span> {db.label}
                </span>
              </div>
            );
          })}
        </div>
        <div
          style={{
            fontFamily: fonts.body,
            fontSize: 14,
            color: colors.textDim,
            marginTop: 12,
            lineHeight: 1.55,
          }}
        >
          Bar height is on-duty hours. Colour, the glyph and the word under each bar say the same
          thing — none is the only signal. A breach is any violation the engine recorded on that
          day; near limit is within {durationLabel(7200)} of a daily limit.
        </div>
      </Panel>

      <Heading>Last {HISTORY_DAYS} days on file</Heading>
      {history.length === 0 ? (
        <EmptyNote>
          {hydration.source === "none"
            ? "Nothing from the server yet. The history fills in once the tablet reaches it."
            : "The server holds no duty days for you in the last 14 days."}
        </EmptyNote>
      ) : (
        [...history].reverse().map((day) => {
          const certify = certifyState(day.date, snapshot);
          const sources = [...new Set(day.events.filter((e) => !e.isVoided).map((e) => e.source))];
          return (
            <div key={day.date} style={{ marginBottom: 10 }}>
              <CardRow>
                <div style={{ flex: "none", width: 130 }}>
                  <Num size={type.label} color={colors.headingBright}>
                    {day.date}
                  </Num>
                </div>
                <div style={{ flex: "none", width: 250 }}>
                  <CertifiedChip day={day} />
                </div>
                <div style={{ flex: "none", width: 220 }}>
                  <FieldLine
                    label="Driving / on duty"
                    value={day.hasRecords ? `${durationLabel(day.totals.drivingSeconds)} / ${durationLabel(day.totals.onDutySeconds)}` : "—"}
                  />
                </div>
                <div style={{ flex: 1, minWidth: 0, display: "flex", gap: 8, flexWrap: "wrap" }}>
                  {sources.length === 0 ? (
                    <span style={{ fontFamily: fonts.body, fontSize: type.label, color: colors.textDim }}>No events on file</span>
                  ) : (
                    sources.map((s) => <SourceBadge key={s} source={s} />)
                  )}
                </div>
                {day.date !== today && !certify.certified && day.hasRecords ? (
                  <div style={{ flex: "none" }}>
                    <TouchButton
                      variant="secondary"
                      onClick={() => setSheet(day.date)}
                      disabled={certify.reason !== null}
                      disabledReason={certify.reason ?? undefined}
                    >
                      Certify
                    </TouchButton>
                  </div>
                ) : null}
              </CardRow>
              {day.violations.length > 0 ? (
                <div style={{ display: "flex", flexDirection: "column", gap: 6, padding: "0 18px 8px" }}>
                  {day.violations.map((v, i) => (
                    <Reason key={`${v.rule}-${i}`} kind={v.suppressed ? "info" : "over"}>
                      <strong style={{ fontWeight: 600 }}>{ruleShort(v.rule)}</strong> — {v.message}
                      {v.suppressed ? " (covered by a declared exception)" : ""}
                    </Reason>
                  ))}
                </div>
              ) : null}
              {certify.reason !== null && !certify.certified && day.hasRecords && day.date !== today ? (
                <div style={{ padding: "0 18px 8px" }}>
                  <Reason kind={certify.kind}>
                    <strong style={{ fontWeight: 600 }}>Certify unavailable</strong> — {certify.reason}
                  </Reason>
                </div>
              ) : null}
            </div>
          );
        })
      )}

      {sheet !== null ? (
        <CertifySheet
          date={sheet}
          snapshot={snapshot}
          onClose={() => setSheet(null)}
          onCertify={() => {
            const date = sheet;
            setSheet(null);
            void act(() => enqueue("hos.certify", { date }));
          }}
        />
      ) : null}
    </Screen>
  );
}

// --- certification ------------------------------------------------------------

interface CertifyState {
  certified: boolean;
  /** Why the button is disabled, or null when the driver may certify. */
  reason: string | null;
  kind: StatusKind;
}

/**
 * Whether `date` may be certified from this tablet right now, and if not, why — on screen,
 * never only in a tooltip. Mirrors DutyDay.Certify's refusals (PcSegmentOpen, NothingToCertify,
 * DayInFuture) so the driver is told here rather than by a parked command later, and adds the
 * two this device knows about: a certification already queued, and one the server refused.
 */
export function certifyState(date: DateOnly, snapshot: LedgerSnapshot): CertifyState {
  const day = snapshot.days.find((d) => d.date === date);
  const certified = day !== undefined && day.certified && !day.recertificationRequired;
  if (certified) return { certified: true, reason: null, kind: "ontime" };

  const commands = allCommands().filter((c) => c.kind === "hos.certify" && isCertifyCommandPayload(c.payload) && c.payload.date === date);
  if (commands.some((c) => c.status === "pending" || c.status === "sending" || c.status === "sent")) {
    return { certified: false, reason: "Certification queued — it reaches the server with the next sync", kind: "soon" };
  }
  const refused = commands.find((c) => c.status === "failed");
  if (refused !== undefined) {
    return { certified: false, reason: refused.error ?? "The server refused the last certification", kind: "over" };
  }

  const today = CALENDAR.dayOf(snapshot.asOfUnix);
  if (date > today) return { certified: false, reason: "A day cannot be certified before it starts", kind: "off" };

  const lastToday = snapshot.events.filter((e) => CALENDAR.dayOf(e.atUnix) === date).at(-1);
  if (lastToday?.status === "PersonalConveyance") {
    return { certified: false, reason: "Personal conveyance is still open — end the segment first", kind: "soon" };
  }

  const hasEvents = (day?.hasRecords ?? false) || snapshot.events.some((e) => CALENDAR.dayOf(e.atUnix) === date);
  if (!hasEvents) return { certified: false, reason: "No duty events recorded for this day yet", kind: "off" };

  return { certified: false, reason: null, kind: "ontime" };
}

function CertifySheet({
  date,
  snapshot,
  onClose,
  onCertify,
}: {
  date: DateOnly;
  snapshot: LedgerSnapshot;
  onClose: () => void;
  onCertify: () => void;
}) {
  const totals = snapshot.evaluation.days.find((d) => d.date === date);
  const queued = snapshot.events.filter((e) => CALENDAR.dayOf(e.atUnix) === date && e.clientEventId !== null).length;
  return (
    <ModalShell
      eyebrow="Record of duty status · s.84"
      title={`Certify ${date}`}
      onClose={onClose}
      footer={
        <>
          <TouchButton variant="secondary" onClick={onClose}>
            Cancel
          </TouchButton>
          <TouchButton onClick={onCertify}>Certify</TouchButton>
        </>
      }
    >
      <div style={{ fontFamily: fonts.body, fontSize: type.value, color: colors.textPrimary, lineHeight: 1.45, marginBottom: gap.section }}>
        I certify these duty figures are true and complete for {date}.
      </div>
      <div style={{ display: "flex", gap: gap.row, marginBottom: gap.section }}>
        <FieldLine label="Driving" value={totals ? durationLabel(totals.drivingSeconds) : "—"} />
        <FieldLine label="On duty" value={totals ? durationLabel(totals.onDutySeconds) : "—"} />
        <FieldLine label="Off duty" value={totals ? durationLabel(totals.offDutySeconds) : "—"} />
      </div>
      <div style={{ fontFamily: fonts.body, fontSize: type.label, color: colors.textSecondary, lineHeight: 1.5 }}>
        Certifying is your own act under the regulation; dispatch cannot do it for you. The
        certification is queued on this tablet and reaches the server with the next sync
        {queued > 0 ? `, after the ${queued} duty ${queued === 1 ? "change" : "changes"} still queued for this day` : ""}.
        Your record of duty status remains the one on the bus&rsquo;s ELD.
      </div>
    </ModalShell>
  );
}

function CertifiedChip({ day }: { day: DutyDaySummary }) {
  if (!day.hasRecords) return <TabletChip kind="off" label="No records" />;
  if (day.recertificationRequired) return <TabletChip kind="over" label="Recertification required" />;
  if (day.certified) return <TabletChip kind="ontime" label="Certified" />;
  return <TabletChip kind="soon" glyph="◷" label="Not certified" />;
}

function SourceBadge({ source }: { source: EventSourceWire }) {
  const badge = SOURCE_BADGE[source] ?? { kind: "off" as StatusKind, glyph: "—" };
  return <TabletChip kind={badge.kind} glyph={badge.glyph} label={source} />;
}

// --- the server's refusals ------------------------------------------------------

function describeCommand(command: QueuedCommand): string {
  if (command.kind === "hos.duty" && isDutyCommandPayload(command.payload)) {
    const at = Date.parse(command.payload.atUtc);
    return `Duty change to ${command.payload.duty}${Number.isFinite(at) ? ` at ${clockLabel(Math.floor(at / 1000))}` : ""}`;
  }
  if (command.kind === "hos.certify" && isCertifyCommandPayload(command.payload)) {
    return `Certification of ${command.payload.date}`;
  }
  return command.kind;
}

function ParkedCommands({
  commands,
  onRetry,
  onDiscard,
}: {
  commands: QueuedCommand[];
  onRetry: (id: string) => void;
  onDiscard: (id: string) => void;
}) {
  const m = statusMeta("over");
  return (
    <div style={{ marginBottom: gap.section }}>
      <StatusBanner kind="over" title={`${commands.length} queued ${commands.length === 1 ? "change" : "changes"} the server refused.`}>
        Each one is held on this tablet with the server&rsquo;s reason. Retry once the reason is
        fixed, or discard it — the figures above already leave it out.
      </StatusBanner>
      {commands.map((c) => (
        <CardRow key={c.id} accent={m.c}>
          <div style={{ flex: 1, minWidth: 0 }}>
            <FieldLine label={describeCommand(c)} value={c.error ?? "The server gave no reason."} />
            {c.errorCode ? (
              <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textDim, marginTop: 4 }}>{c.errorCode}</div>
            ) : null}
          </div>
          <div style={{ flex: "none", display: "flex", gap: 10 }}>
            <TouchButton variant="secondary" onClick={() => onRetry(c.id)}>
              Retry
            </TouchButton>
            <TouchButton variant="danger" onClick={() => onDiscard(c.id)}>
              Discard
            </TouchButton>
          </div>
        </CardRow>
      ))}
    </div>
  );
}

// --- which copy the figures are built on ------------------------------------------

function HydrationChip({ hydration }: { hydration: LedgerSnapshot["hydration"] }) {
  if (hydration.source === "server" && hydration.error === null) return <TabletChip kind="ontime" label="Live" />;
  if (hydration.source === "none") return <TabletChip kind="off" label="No server copy" />;
  return <TabletChip kind="soon" label={hydration.source === "cache" ? "Device copy" : "Last fetch failed"} />;
}

function HydrationLine({ hydration }: { hydration: LedgerSnapshot["hydration"] }) {
  const at = hydration.atUnix === null ? null : clockLabel(hydration.atUnix);
  if (hydration.source === "none") {
    return (
      <span>
        Nothing from the server yet{hydration.error ? ` — ${hydration.error.message}` : "."}
      </span>
    );
  }
  if (hydration.error !== null) {
    return (
      <span>
        Showing the {hydration.source === "cache" ? "copy held on this device" : "last copy fetched"} ({at}); the server could not be reached — {hydration.error.message}
      </span>
    );
  }
  return <span>{hydration.source === "cache" ? `Showing the copy held on this device from ${at}.` : `Server copy fetched ${at}.`}</span>;
}

// --- small pieces ------------------------------------------------------------------

/** One status line: glyph + words in the kind's colour. */
function Reason({ kind, children }: { kind: StatusKind; children: React.ReactNode }) {
  const m = statusMeta(kind);
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 8,
        fontFamily: fonts.body,
        fontSize: type.label,
        color: m.t,
        marginBottom: 8,
      }}
    >
      <span aria-hidden style={{ fontWeight: 800 }}>
        {m.g}
      </span>
      <span>{children}</span>
    </div>
  );
}

/** The engine's "next required off duty": when, which rule, and for how long. Colour + glyph + words. */
function NextOffDuty({
  by,
  rule,
  minimum,
  shortfall,
  kind,
}: {
  by: string;
  rule: string;
  minimum: string;
  shortfall: string | null;
  kind: StatusKind;
}) {
  const m = statusMeta(kind);
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 10,
        padding: "12px 16px",
        marginBottom: gap.section,
        borderRadius: radius.control,
        background: m.bg,
        border: `1px solid ${m.bd}`,
        fontFamily: fonts.body,
        fontSize: type.label,
        color: m.t,
      }}
    >
      <span aria-hidden style={{ fontWeight: 800 }}>
        {m.g}
      </span>
      <span>
        <strong style={{ fontWeight: 600 }}>Next required off duty by {by}</strong> — {rule}; at least {minimum} off
        {shortfall ? `, and ${shortfall} still owed toward today's 10h` : ""}.
      </span>
    </div>
  );
}
