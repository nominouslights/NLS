"use client";

import { useState } from "react";
import { colors, dutyMeta, fonts, statusMeta } from "@/lib/theme";
import type { DutyDaySummary, DutyEventRecord, DutySegmentRecord } from "@/lib/api/hos";
import {
  DUTY_STATES,
  dutyStateFromWire,
  formatClockUtc,
  formatDateOnly,
  formatRemaining,
  HOS_CALENDAR,
  sourceGlyphFor,
  unixOfUtc,
} from "@/lib/hos/display";
import { StatusChip } from "@/components/ui/Chip";

// One row per duty day from GET /api/drivers/{id}/hos/days, newest first: a 24h strip of
// the day's effective segments coloured by dutyMeta (the legend above the rows carries each
// status's glyph + label, so the strip is never colour alone), the engine's totals, the
// certification state, the sources that fed the day, and the server's violation sentences.
// A row expands into its events — voided ones struck through with the amendment reason —
// each with an AMEND action.

export function DutyDayTimeline({
  days,
  today,
  onAmend,
}: {
  days: DutyDaySummary[];
  today: string;
  onAmend: (day: DutyDaySummary, event: DutyEventRecord) => void;
}) {
  const [open, setOpen] = useState<string | null>(null);
  const ordered = [...days].sort((a, b) => b.date.localeCompare(a.date));

  return (
    <div>
      <div style={{ display: "flex", flexWrap: "wrap", gap: 10, marginBottom: 10 }}>
        {DUTY_STATES.map((s) => {
          const m = dutyMeta(s);
          return (
            <span key={s} style={{ display: "inline-flex", alignItems: "center", gap: 5, fontFamily: fonts.body, fontSize: 11, color: m.text }}>
              <span style={{ width: 12, height: 8, borderRadius: 2, background: m.color, flex: "none" }} />
              <span style={{ fontSize: 9, lineHeight: 1 }}>{m.glyph}</span>
              {s}
            </span>
          );
        })}
      </div>
      {ordered.map((day) => (
        <DayRow key={day.date} day={day} isToday={day.date === today} expanded={open === day.date} onToggle={() => setOpen(open === day.date ? null : day.date)} onAmend={onAmend} />
      ))}
    </div>
  );
}

function certifiedChip(day: DutyDaySummary, isToday: boolean) {
  if (day.recertificationRequired) return <StatusChip kind="over" label="Recertification required" />;
  if (day.certified) return <StatusChip kind="ontime" label={`Certified ${day.certifiedAtUtc ? formatClockUtc(day.certifiedAtUtc) : ""}`.trim()} />;
  return <StatusChip kind="soon" glyph="◷" label={isToday ? "Not yet certified" : "Not certified"} />;
}

function DayRow({
  day,
  isToday,
  expanded,
  onToggle,
  onAmend,
}: {
  day: DutyDaySummary;
  isToday: boolean;
  expanded: boolean;
  onToggle: () => void;
  onAmend: (day: DutyDaySummary, event: DutyEventRecord) => void;
}) {
  const sources = [...new Set(day.events.filter((e) => !e.isVoided).map((e) => e.source))];
  const live = day.violations.filter((v) => !v.suppressed);
  const suppressed = day.violations.filter((v) => v.suppressed);
  const t = day.totals;

  return (
    <div style={{ borderTop: `1px solid ${colors.borderSubtle}`, padding: "10px 0" }}>
      <div onClick={onToggle} style={{ cursor: "pointer" }} aria-expanded={expanded}>
        <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
          <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textSecondary, minWidth: 92 }}>
            {formatDateOnly(day.date)}
            {isToday && <span style={{ color: colors.textDim }}> · today</span>}
          </span>
          {day.hasRecords ? (
            <>
              <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textDim }}>
                Drv {formatRemaining(t.drivingSeconds)} · On {formatRemaining(t.onDutySeconds)} · Off {formatRemaining(t.offDutySeconds)}
                {day.personalConveyanceKm > 0 && ` · PC ${day.personalConveyanceKm.toFixed(1)} km`}
              </span>
              <span style={{ marginLeft: "auto", display: "inline-flex", gap: 6, flexWrap: "wrap" }}>
                {sources.map((s) => (
                  <StatusChip key={s} kind="info" glyph={sourceGlyphFor(s)} label={s} />
                ))}
                {certifiedChip(day, isToday)}
              </span>
            </>
          ) : (
            <span style={{ marginLeft: "auto" }}>
              <StatusChip kind="off" label="No records" />
            </span>
          )}
        </div>
        <Strip date={day.date} segments={day.segments} />
        {(live.length > 0 || suppressed.length > 0) && (
          <div style={{ display: "flex", flexDirection: "column", gap: 5, marginTop: 7 }}>
            {live.map((v, i) => (
              <StatusChip key={`${v.rule}-${i}`} kind="over" label={v.message} />
            ))}
            {suppressed.map((v, i) => (
              <StatusChip key={`s-${v.rule}-${i}`} kind="off" label={`${v.message} — covered by a declared exception`} />
            ))}
          </div>
        )}
      </div>
      {expanded && (
        <div style={{ marginTop: 8, paddingLeft: 6, borderLeft: `2px solid ${colors.borderSubtle}` }}>
          {day.events.length === 0 ? (
            <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim, padding: "6px 0" }}>No duty events on this day.</div>
          ) : (
            [...day.events]
              .sort((a, b) => a.atUtc.localeCompare(b.atUtc) || a.sequence - b.sequence)
              .map((e) => <DutyEventRow key={e.eventId} event={e} events={day.events} onAmend={() => onAmend(day, e)} />)
          )}
        </div>
      )}
    </div>
  );
}

/** The day's effective segments laid across its 24 (23 / 25 on DST days) hours. */
function Strip({ date, segments }: { date: string; segments: DutySegmentRecord[] }) {
  const start = HOS_CALENDAR.dayStartUnix(date);
  const end = HOS_CALENDAR.dayEndUnix(date);
  const length = end - start;
  return (
    <div
      style={{
        position: "relative",
        height: 10,
        marginTop: 7,
        borderRadius: 5,
        background: colors.inputBg,
        border: `1px solid ${colors.borderSubtle}`,
        overflow: "hidden",
      }}
    >
      {segments.map((s) => {
        const from = Math.max(start, unixOfUtc(s.fromUtc));
        const to = Math.min(end, unixOfUtc(s.toUtc));
        if (to <= from) return null;
        const m = dutyMeta(dutyStateFromWire(s.status));
        return (
          <div
            key={`${s.eventId}-${s.fromUtc}`}
            title={`${s.status} ${formatClockUtc(s.fromUtc)}–${formatClockUtc(s.toUtc)} (${formatRemaining(s.durationSeconds)})`}
            style={{
              position: "absolute",
              top: 0,
              bottom: 0,
              left: `${((from - start) / length) * 100}%`,
              width: `${((to - from) / length) * 100}%`,
              background: m.color,
            }}
          />
        );
      })}
    </div>
  );
}

export function DutyEventRow({ event, events, onAmend }: { event: DutyEventRecord; events: DutyEventRecord[]; onAmend: () => void }) {
  const state = dutyStateFromWire(event.status);
  const m = dutyMeta(state);
  // The amendment reason lives on the replacement; the voided original points nowhere.
  const replacement = event.isVoided ? events.find((r) => r.amendsEventId === event.eventId) : undefined;
  const voidReason = replacement?.amendmentReason ?? event.amendmentReason;
  const dim = event.isVoided;
  return (
    <div
      data-testid="duty-event-row"
      data-voided={dim || undefined}
      style={{
        display: "grid",
        gridTemplateColumns: "52px 150px 1fr auto",
        gap: 10,
        alignItems: "center",
        padding: "7px 0 7px 8px",
        borderTop: `1px solid ${colors.borderSubtle}`,
        opacity: dim ? 0.7 : 1,
        textDecoration: dim ? "line-through" : undefined,
      }}
    >
      <div style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textSecondary }}>{formatClockUtc(event.atUtc)}</div>
      <div style={{ display: "flex", alignItems: "center", gap: 5, fontFamily: fonts.body, fontSize: 12, fontWeight: 600, color: m.text }}>
        <span style={{ fontSize: 9, color: m.color }}>{m.glyph}</span>
        {state}
      </div>
      <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, minWidth: 0 }}>
        <span style={{ display: "inline-flex", alignItems: "center", gap: 4 }}>
          <span style={{ fontSize: 10, color: statusMeta("info").t }}>{sourceGlyphFor(event.source)}</span>
          {event.source}
        </span>
        {event.enteredBy && ` · ${event.enteredBy}`}
        {event.carrierName && ` · ${event.carrierName}`}
        {event.odometerStartKm !== null && ` · odo ${event.odometerStartKm}${event.odometerEndKm !== null ? `–${event.odometerEndKm}` : ""} km`}
        {event.note && ` · ${event.note}`}
        {event.isVoided && (
          <div style={{ textDecoration: "none", color: statusMeta("over").t, marginTop: 2 }}>
            <StatusChip kind="over" glyph="✕" label={`Voided${voidReason ? ` — ${voidReason}` : ""}`} />
          </div>
        )}
        {!event.isVoided && event.amendsEventId && (
          <div style={{ marginTop: 2 }}>
            <StatusChip kind="info" glyph="✎" label={`Amendment${event.amendmentReason ? ` — ${event.amendmentReason}` : ""}`} />
          </div>
        )}
      </div>
      <div>
        {!event.isVoided && (
          <span
            onClick={onAmend}
            style={{
              fontFamily: fonts.semiCondensed,
              fontWeight: 600,
              fontSize: 10,
              letterSpacing: ".08em",
              textTransform: "uppercase",
              color: colors.blue,
              cursor: "pointer",
            }}
          >
            Amend
          </span>
        )}
      </div>
    </div>
  );
}
