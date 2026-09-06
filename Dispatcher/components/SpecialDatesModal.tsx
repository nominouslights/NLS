"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  createScheduleException,
  deleteScheduleException,
  EXCEPTION_KIND_LABELS,
  hhmm,
  listScheduleExceptions,
  listTrips,
  refetchUntil,
  shortDateLabel,
  todayIso,
  updateScheduleException,
  type ScheduleExceptionInput,
  type ScheduleExceptionKind,
  type ScheduleExceptionRecord,
  type ScheduleTemplateRecord,
  type TripRecord,
} from "@/lib/api/trips";
import { templateOccursOn } from "@/lib/schedule";
import { ModalShell } from "@/components/ui/ModalShell";
import { MonthGrid } from "@/components/ui/MonthGrid";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";
import { SelectField, TextField, TimeField } from "@/components/ui/Field";

// Special Dates — per-template schedule exceptions (Skip / ExtraRun /
// TimeOverride on a date) over a month calendar. Exceptions are consumed at
// GENERATION TIME ONLY: an exception never cancels an already-generated trip,
// so a date whose trip already exists shows a Gold warning with a jump link
// to the Trips screen instead of pretending the Skip will remove it.
//
// Badge language (colour + icon + text, never colour alone):
//   Skip         → Vermillion #D55E00 · ✕ · "Skipped"
//   ExtraRun     → Teal       #009E73 · + · "Extra run"
//   TimeOverride → Gold       #E1B000 · ◷ · "Time change"

const EXCEPTION_KINDS: ScheduleExceptionKind[] = ["Skip", "ExtraRun", "TimeOverride"];

/** Colour + glyph + short label per kind — the protected status hexes via
 *  statusMeta, so the palette can never drift from the design system. */
function kindBadge(kind: ScheduleExceptionKind): { c: string; bt: string; t: string; glyph: string; short: string } {
  switch (kind) {
    case "Skip": {
      const m = statusMeta("over"); // #D55E00
      return { c: m.c, bt: m.bt, t: m.t, glyph: "✕", short: "Skipped" };
    }
    case "ExtraRun": {
      const m = statusMeta("ontime"); // #009E73
      return { c: m.c, bt: m.bt, t: m.t, glyph: "+", short: "Extra run" };
    }
    case "TimeOverride":
    default: {
      const m = statusMeta("soon"); // #E1B000
      return { c: m.c, bt: m.bt, t: m.t, glyph: "◷", short: "Time change" };
    }
  }
}

/** "HH:mm:ss" (wire) → "HH:mm" (time input value). */
function toInputTime(wire: string | null): string {
  return wire ? wire.slice(0, 5) : "";
}

function monthRange(year: number, month: number): { from: string; to: string } {
  const pad = (n: number) => String(n).padStart(2, "0");
  const last = new Date(year, month + 1, 0).getDate();
  return {
    from: `${year}-${pad(month + 1)}-01`,
    to: `${year}-${pad(month + 1)}-${pad(last)}`,
  };
}

export default function SpecialDatesModal({
  template,
  onClose,
  onOpenTrip,
}: {
  template: ScheduleTemplateRecord;
  onClose: () => void;
  /** Jump link for "trip already generated" warnings — navigates to Trips. */
  onOpenTrip?: (tripId: string) => void;
}) {
  const now = new Date();
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth()); // 0-based

  const [exceptions, setExceptions] = useState<ScheduleExceptionRecord[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  // Generated trips for the visible month (this template only) — keyed by the
  // month they belong to so a stale fetch is ignored.
  const [monthTrips, setMonthTrips] = useState<{ key: string; rows: TripRecord[] } | null>(null);

  const [selDate, setSelDate] = useState<string | null>(null);
  const [kind, setKind] = useState<ScheduleExceptionKind>("Skip");
  const [departure, setDeparture] = useState("");
  const [returnDeparture, setReturnDeparture] = useState("");
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const templateId = template.id;
  const hasTemplateReturn = template.returnDepartureTime !== null;

  const loadExceptions = useCallback(async () => {
    try {
      const rows = await listScheduleExceptions(templateId);
      setExceptions(rows);
      setLoadError(null);
    } catch (e) {
      setExceptions(null);
      setLoadError(e instanceof ApiError ? e.message : "Failed to load special dates.");
    }
  }, [templateId]);

  useEffect(() => {
    let active = true;
    listScheduleExceptions(templateId).then(
      (rows) => {
        if (active) {
          setExceptions(rows);
          setLoadError(null);
        }
      },
      (e) => {
        if (active) {
          setExceptions(null);
          setLoadError(e instanceof ApiError ? e.message : "Failed to load special dates.");
        }
      },
    );
    return () => {
      active = false;
    };
  }, [templateId]);

  // Generated trips in the visible month — the whole window in one unpaged
  // call, filtered to this template client-side (the list API has no
  // scheduleTemplateId filter).
  const monthKey = `${year}-${month}`;
  useEffect(() => {
    let active = true;
    const { from, to } = monthRange(year, month);
    listTrips({ from, to }).then(
      (page) => {
        if (active) {
          setMonthTrips({ key: monthKey, rows: page.items.filter((t) => t.scheduleTemplateId === templateId) });
        }
      },
      () => {
        if (active) setMonthTrips({ key: monthKey, rows: [] }); // best-effort — only the warning hint is lost
      },
    );
    return () => {
      active = false;
    };
  }, [year, month, monthKey, templateId]);

  const byDate = new Map<string, ScheduleExceptionRecord>();
  for (const ex of exceptions ?? []) byDate.set(ex.date, ex);
  const tripsByDate = new Map<string, TripRecord[]>();
  if (monthTrips?.key === monthKey) {
    for (const t of monthTrips.rows) {
      const list = tripsByDate.get(t.serviceDate) ?? [];
      list.push(t);
      tripsByDate.set(t.serviceDate, list);
    }
  }

  const existing = selDate ? byDate.get(selDate) ?? null : null;
  const selTrips = selDate ? (tripsByDate.get(selDate) ?? []).filter((t) => t.status !== "Cancelled") : [];

  function pickDate(dateIso: string) {
    setSelDate(dateIso);
    setError(null);
    const ex = byDate.get(dateIso) ?? null;
    if (ex) {
      setKind(ex.kind);
      setDeparture(toInputTime(ex.departureTime));
      setReturnDeparture(toInputTime(ex.returnDepartureTime));
      setNote(ex.note ?? "");
    } else {
      // Default kind: a recurrence date is most often skipped or re-timed; a
      // non-recurrence date can only be an extra run.
      setKind(templateOccursOn(template, dateIso) ? "Skip" : "ExtraRun");
      setDeparture("");
      setReturnDeparture("");
      setNote("");
    }
  }

  /** Client-side mirror of the backend's per-kind validation. */
  function validate(): string | null {
    if (kind === "Skip") return null; // times are simply not sent
    if (kind === "ExtraRun") {
      if (!departure) return "An extra run needs a departure time.";
      if (returnDeparture && returnDeparture <= departure)
        return "The extra run's return must be later than its departure (extra runs are same-day only).";
      return null;
    }
    // TimeOverride
    if (!departure && !returnDeparture) return "A time change needs at least one overridden time.";
    if (returnDeparture && !hasTemplateReturn)
      return "This template has no return leg — only the departure time can be overridden.";
    if (!template.returnNextDay) {
      const effDep = departure || toInputTime(template.departureTime);
      const effRet = returnDeparture || toInputTime(template.returnDepartureTime);
      if (hasTemplateReturn && effRet && effRet <= effDep)
        return "The effective return time would be at or before the departure.";
    }
    return null;
  }

  async function save() {
    if (busy || !selDate) return;
    const problem = validate();
    if (problem) {
      setError(problem);
      return;
    }
    const input: ScheduleExceptionInput = {
      date: selDate,
      kind,
      departureTime: kind === "Skip" ? null : departure || null,
      returnDepartureTime: kind === "Skip" ? null : returnDeparture || null,
      note: note.trim() || null,
    };
    setBusy(true);
    setError(null);
    try {
      if (existing) {
        await updateScheduleException(templateId, existing.id, input);
        const fresh = await refetchUntil(
          () => listScheduleExceptions(templateId),
          (rows) => {
            const ex = rows.find((x) => x.id === existing.id);
            return (
              ex !== undefined &&
              ex.kind === input.kind &&
              toInputTime(ex.departureTime) === (input.departureTime ?? "") &&
              toInputTime(ex.returnDepartureTime) === (input.returnDepartureTime ?? "")
            );
          },
        );
        setExceptions(fresh);
      } else {
        const newId = await createScheduleException(templateId, input);
        const fresh = await refetchUntil(
          () => listScheduleExceptions(templateId),
          (rows) => rows.some((x) => x.id === newId),
        );
        setExceptions(fresh);
      }
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to save the special date — please try again.");
    } finally {
      setBusy(false);
    }
  }

  async function remove() {
    if (busy || !existing) return;
    setBusy(true);
    setError(null);
    try {
      await deleteScheduleException(templateId, existing.id);
      const fresh = await refetchUntil(
        () => listScheduleExceptions(templateId),
        (rows) => !rows.some((x) => x.id === existing.id),
      );
      setExceptions(fresh);
      setSelDate(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to remove the special date — please try again.");
    } finally {
      setBusy(false);
    }
  }

  const today = todayIso();

  return (
    <ModalShell
      eyebrow={`Schedule template · ${template.name}`}
      title="Special Dates"
      onClose={onClose}
      error={error}
      maxWidth={860}
      footer={<ActionButton onClick={onClose}>CLOSE</ActionButton>}
    >
      {loadError && (
        <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 14 }}>
          <StatusChip kind="over" label={`Special dates unavailable — ${loadError}`} />
          <ActionButton variant="primary" onClick={loadExceptions}>
            RETRY
          </ActionButton>
        </div>
      )}

      {/* legend — colour + icon + text, matching the day-cell badges */}
      <div style={{ display: "flex", flexWrap: "wrap", gap: 14, alignItems: "center", marginBottom: 14 }}>
        {EXCEPTION_KINDS.map((k) => {
          const b = kindBadge(k);
          return (
            <span key={k} style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
              <span
                style={{
                  width: 15,
                  height: 15,
                  borderRadius: 4,
                  background: b.c,
                  color: b.bt,
                  display: "inline-flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontSize: 9,
                  fontWeight: 800,
                }}
              >
                {b.glyph}
              </span>
              <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: b.t, fontWeight: 600 }}>{b.short}</span>
            </span>
          );
        })}
        <span style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
          <span style={{ width: 7, height: 7, borderRadius: "50%", background: colors.blue, display: "inline-block" }} />
          <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>Scheduled run day</span>
        </span>
      </div>

      <MonthGrid
        year={year}
        month={month}
        onPrevMonth={() => {
          setMonth((m) => (m === 0 ? 11 : m - 1));
          if (month === 0) setYear((y) => y - 1);
        }}
        onNextMonth={() => {
          setMonth((m) => (m === 11 ? 0 : m + 1));
          if (month === 11) setYear((y) => y + 1);
        }}
        renderDay={(dateIso, inMonth) => {
          const ex = inMonth ? byDate.get(dateIso) : undefined;
          const recurs = inMonth && templateOccursOn(template, dateIso);
          const generated = inMonth && (tripsByDate.get(dateIso) ?? []).some((t) => t.status !== "Cancelled");
          const selected = selDate === dateIso;
          const b = ex ? kindBadge(ex.kind) : null;
          return (
            <div
              onClick={inMonth ? () => pickDate(dateIso) : undefined}
              style={{
                minHeight: 56,
                borderRadius: 8,
                border: `1px solid ${selected ? colors.borderActive : inMonth ? colors.borderSubtle : "transparent"}`,
                background: !inMonth ? "transparent" : selected ? colors.cardBgActive : colors.cardBg,
                boxShadow: inMonth ? colors.shadowCard : undefined,
                cursor: inMonth ? "pointer" : "default",
                padding: "5px 6px",
                display: "flex",
                flexDirection: "column",
                gap: 3,
                opacity: inMonth ? 1 : 0.35,
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: 5 }}>
                <span
                  style={{
                    fontFamily: fonts.mono,
                    fontSize: 10.5,
                    color: dateIso === today ? colors.blue : colors.textDim,
                    fontWeight: dateIso === today ? 700 : 500,
                  }}
                >
                  {Number(dateIso.slice(8, 10))}
                </span>
                {recurs && (
                  <span
                    title="Scheduled run day"
                    style={{ width: 6, height: 6, borderRadius: "50%", background: colors.blue, display: "inline-block" }}
                  />
                )}
                {generated && (
                  <span style={{ marginLeft: "auto", fontFamily: fonts.mono, fontSize: 8.5, color: colors.textFaint }}>
                    TR
                  </span>
                )}
              </div>
              {ex && b && (
                <span
                  style={{
                    display: "inline-flex",
                    alignItems: "center",
                    gap: 4,
                    fontFamily: fonts.body,
                    fontSize: 9,
                    fontWeight: 700,
                    color: b.t,
                    lineHeight: 1,
                  }}
                >
                  <span
                    style={{
                      width: 12,
                      height: 12,
                      flex: "none",
                      borderRadius: 3,
                      background: b.c,
                      color: b.bt,
                      display: "inline-flex",
                      alignItems: "center",
                      justifyContent: "center",
                      fontSize: 8,
                      fontWeight: 800,
                    }}
                  >
                    {b.glyph}
                  </span>
                  {b.short}
                </span>
              )}
            </div>
          );
        }}
      />

      {exceptions === null && !loadError && (
        <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim, marginTop: 10 }}>
          Loading special dates…
        </div>
      )}

      {/* editor */}
      {selDate && (
        <div
          style={{
            marginTop: 16,
            padding: "15px 16px",
            background: colors.cardBg,
            border: `1px solid ${colors.border}`,
            borderRadius: 12,
            boxShadow: colors.shadowCard,
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12, flexWrap: "wrap" }}>
            <span style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 17, color: colors.headingBright }}>
              {shortDateLabel(selDate)}
            </span>
            {templateOccursOn(template, selDate) ? (
              <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
                · scheduled run day ({hhmm(template.departureTime)}
                {template.returnDepartureTime ? ` / return ${hhmm(template.returnDepartureTime)}` : ""})
              </span>
            ) : (
              <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
                · not a scheduled run day
              </span>
            )}
            {existing && (
              <StatusChip
                kind={existing.kind === "Skip" ? "over" : existing.kind === "ExtraRun" ? "ontime" : "soon"}
                label={kindBadge(existing.kind).short}
              />
            )}
          </div>

          {/* Generation-time-only warning: the worker never cancels an
              already-generated trip — that has to happen from Trips. */}
          {selTrips.length > 0 && (
            <div style={{ display: "flex", flexDirection: "column", gap: 6, marginBottom: 12 }}>
              {selTrips.map((trip) => (
                <div key={trip.id} style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
                  <StatusChip kind="soon" label={`Trip ${trip.tripNumber} already generated — cancel it from Trips`} />
                  {onOpenTrip && <ActionButton onClick={() => onOpenTrip(trip.id)}>OPEN TRIP</ActionButton>}
                </div>
              ))}
            </div>
          )}

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12 }}>
            <SelectField
              label="Kind"
              value={kind}
              onChange={(v) => {
                setKind(v as ScheduleExceptionKind);
                setError(null);
              }}
              options={EXCEPTION_KINDS.map((k) => ({ value: k, label: EXCEPTION_KIND_LABELS[k] }))}
            />
            {kind !== "Skip" && (
              <TimeField
                label={kind === "ExtraRun" ? "Departure" : "Departure override"}
                value={departure}
                onChange={setDeparture}
                hint={
                  kind === "TimeOverride" ? (
                    <span style={{ color: colors.textFaint }}>· blank keeps {hhmm(template.departureTime)}</span>
                  ) : undefined
                }
              />
            )}
            {kind === "ExtraRun" && (
              <TimeField
                label="Return (optional)"
                value={returnDeparture}
                onChange={setReturnDeparture}
                hint={<span style={{ color: colors.textFaint }}>· same-day, after departure</span>}
              />
            )}
            {kind === "TimeOverride" && hasTemplateReturn && (
              <TimeField
                label="Return override"
                value={returnDeparture}
                onChange={setReturnDeparture}
                hint={
                  <span style={{ color: colors.textFaint }}>
                    · blank keeps {hhmm(template.returnDepartureTime)}
                  </span>
                }
              />
            )}
          </div>

          {kind === "Skip" && (
            <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim, marginTop: 10, lineHeight: 1.5 }}>
              No trip (outbound or return) will be generated for this date. Trips already generated for it are
              untouched — cancel those from the Trips screen.
            </div>
          )}

          <div style={{ marginTop: 12 }}>
            <TextField label="Note (optional)" value={note} onChange={setNote} placeholder="Stat holiday — no run" />
          </div>

          <div style={{ display: "flex", gap: 9, marginTop: 14 }}>
            <ActionButton variant="primary" onClick={save} disabled={busy || exceptions === null}>
              {busy ? "WORKING…" : existing ? "SAVE SPECIAL DATE" : "ADD SPECIAL DATE"}
            </ActionButton>
            {existing && (
              <ActionButton variant="destructive" onClick={remove} disabled={busy}>
                REMOVE
              </ActionButton>
            )}
            <ActionButton onClick={() => setSelDate(null)} disabled={busy}>
              DESELECT
            </ActionButton>
          </div>
        </div>
      )}

      {!selDate && (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, marginTop: 14, lineHeight: 1.6 }}>
          Click a day to add or edit a special date. Special dates apply at <strong style={{ color: colors.textPrimary }}>generation
          time only</strong> — a Skip stops future generation for that date but never cancels a trip that already exists.
        </div>
      )}
    </ModalShell>
  );
}
