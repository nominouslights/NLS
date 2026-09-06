"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, statusMeta, type StatusKind } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import { listRoutes, shortDateLabel, todayIso, type RouteRecord, type TripStop } from "@/lib/api/trips";
import {
  bookingStatusKind,
  cancelBooking,
  confirmBooking,
  createBooking,
  createCustomer,
  dayStatusKind,
  getCalendarMonth,
  getDayDetail,
  guaranteeDay,
  listCorridors,
  locationLabel,
  PAYMENT_METHOD_LABELS,
  PAYMENT_STATUS_LABELS,
  searchCustomers,
  setDayOverrides,
  type BookingLocationInput,
  type BookingPassengerInput,
  type BookingPaymentMethod,
  type BookingRecord,
  type CalendarDaySummary,
  type CorridorRecord,
  type CustomerRecord,
  type DayDetail,
} from "@/lib/api/booking";
import { getRole } from "@/lib/claims";
import { PageHeader, Panel, SectionLabel, DetailRow } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { MonthGrid } from "@/components/ui/MonthGrid";
import { NumberField, SelectField, TextAreaField, TextField, FieldLabel } from "@/components/ui/Field";

// Booking Calendar (US-B.6–8) — community seat bookings per corridor per date.
// Left: corridor selector + month grid with a per-day demand badge. Right: the
// selected date's numbers, its booking list (confirm/cancel), and the inline
// create-booking form.
//
// Day-badge language (colour + icon + text, never colour alone — statusMeta):
//   no activity            → Neutral gray  — · "—"
//   day Reverted           → Vermillion    ▲ · "sold/min"   (needs seats — wins over the math)
//   sold+pending > capacity→ Vermillion    ▲ · "taken/cap"  (overbooked)
//   day Confirmed / min met→ Teal          ✓ · "sold/min"   (confirmed)
//   sold < minimum         → Gold          ◐ · "sold/min"   (pending)
//
// Corridors are a replica of Trips routes (corridorId === routeId), so the
// pickup/dropoff stop pickers reuse the Trips routes API — frontend
// composition, no invented endpoints. Mutations are same-module transactional
// reads: plain refetch, no refetchUntil.

const FREE_STOP = "__free";

interface DayBadge {
  kind: StatusKind;
  label: string;
  /** Long-form for the legend / tooltip. */
  title: string;
}

function dayBadge(s: CalendarDaySummary | undefined): DayBadge | null {
  if (!s) return null; // absent from the calendar response = no activity
  // A Reverted day is a problem regardless of the sold/min math — cancellations
  // dropped it below minimum and its trip is at risk.
  if (s.status === "Reverted") {
    return {
      kind: "over",
      label: `${s.sold}/${s.passengerMinimum}`,
      title: `Reverted — needs ${s.neededToConfirm} more seat(s) to re-confirm`,
    };
  }
  const taken = s.sold + s.pending;
  if (taken > s.capacity) {
    return { kind: "over", label: `${taken}/${s.capacity}`, title: "Overbooked — sold + holds exceed capacity" };
  }
  // A Confirmed day stays teal even if sold has slipped below minimum inside
  // the cancellation window (the day still runs — the panel chip agrees).
  if (s.status === "Confirmed" || s.sold >= s.passengerMinimum) {
    return { kind: "ontime", label: `${s.sold}/${s.passengerMinimum}`, title: "Confirmed — minimum met" };
  }
  return { kind: "soon", label: `${s.sold}/${s.passengerMinimum}`, title: "Below the passenger minimum" };
}

/** Small colour+glyph square (the StatusChip badge, sized for a day cell). */
function CellBadge({ kind, size = 12 }: { kind: StatusKind; size?: number }) {
  const m = statusMeta(kind);
  return (
    <span
      style={{
        width: size,
        height: size,
        flex: "none",
        borderRadius: 3,
        background: m.c,
        color: m.bt,
        display: "inline-flex",
        alignItems: "center",
        justifyContent: "center",
        fontSize: size <= 12 ? 8 : 9,
        fontWeight: 800,
      }}
    >
      {m.g}
    </span>
  );
}

function monthKeyOf(year: number, month: number, corridorId: string): string {
  return `${corridorId}·${year}-${month}`;
}

// ---------------------------------------------------------------------------
// Screen
// ---------------------------------------------------------------------------

export default function Bookings({ onOpenTrip }: { onOpenTrip: (id: string) => void }) {
  const now = new Date();
  const today = todayIso();
  const isOwner = getRole() === "Owner";

  // --- corridors ---
  const [corridors, setCorridors] = useState<CorridorRecord[] | null>(null);
  const [corridorsError, setCorridorsError] = useState<string | null>(null);
  const [corridorId, setCorridorId] = useState<string | null>(null);

  // --- routes (stop pickers — corridorId equals the Trips routeId) ---
  const [routes, setRoutes] = useState<RouteRecord[] | null>(null);

  // --- calendar ---
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth()); // 0-based
  const [monthData, setMonthData] = useState<{ key: string; rows: CalendarDaySummary[] } | null>(null);
  const [monthError, setMonthError] = useState<string | null>(null);

  // --- selected day ---
  const [selDate, setSelDate] = useState<string>(today);
  const [day, setDay] = useState<{ key: string; detail: DayDetail } | null>(null);
  const [dayError, setDayError] = useState<string | null>(null);

  // Plain function (not useCallback): the functional setCorridorId update
  // confuses the React Compiler's dependency inference, and RETRY does not
  // need a stable identity.
  async function loadCorridors() {
    try {
      const rows = await listCorridors();
      setCorridors(rows);
      setCorridorsError(null);
      setCorridorId((cur) => cur ?? (rows.find((c) => c.active) ?? rows[0])?.corridorId ?? null);
    } catch (e) {
      setCorridors(null);
      setCorridorsError(e instanceof ApiError ? e.message : "Failed to load corridors.");
    }
  }

  // Mount fetch inlined with an `active` guard (house pattern — the
  // useCallback above stays for the RETRY button).
  useEffect(() => {
    let active = true;
    listCorridors().then(
      (rows) => {
        if (!active) return;
        setCorridors(rows);
        setCorridorsError(null);
        setCorridorId((cur) => cur ?? (rows.find((c) => c.active) ?? rows[0])?.corridorId ?? null);
      },
      (e) => {
        if (!active) return;
        setCorridors(null);
        setCorridorsError(e instanceof ApiError ? e.message : "Failed to load corridors.");
      },
    );
    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    let active = true;
    listRoutes().then(
      (rows) => {
        if (active) setRoutes(rows);
      },
      () => {
        if (active) setRoutes([]); // best-effort — the form falls back to free-text pickup/dropoff
      },
    );
    return () => {
      active = false;
    };
  }, []);

  const monthKey = corridorId ? monthKeyOf(year, month, corridorId) : null;
  const loadMonth = useCallback(async () => {
    if (!corridorId) return;
    const key = monthKeyOf(year, month, corridorId);
    try {
      const rows = await getCalendarMonth(year, month + 1, corridorId);
      setMonthData({ key, rows });
      setMonthError(null);
    } catch (e) {
      setMonthData({ key, rows: [] });
      setMonthError(e instanceof ApiError ? e.message : "Failed to load the booking calendar.");
    }
  }, [year, month, corridorId]);

  useEffect(() => {
    if (!corridorId) return;
    let active = true;
    const key = monthKeyOf(year, month, corridorId);
    getCalendarMonth(year, month + 1, corridorId).then(
      (rows) => {
        if (active) {
          setMonthData({ key, rows });
          setMonthError(null);
        }
      },
      (e) => {
        if (active) {
          setMonthData({ key, rows: [] });
          setMonthError(e instanceof ApiError ? e.message : "Failed to load the booking calendar.");
        }
      },
    );
    return () => {
      active = false;
    };
  }, [year, month, corridorId]);

  const dayKey = corridorId ? `${corridorId}·${selDate}` : null;
  const loadDay = useCallback(async () => {
    if (!corridorId) return;
    const key = `${corridorId}·${selDate}`;
    try {
      const detail = await getDayDetail(selDate, corridorId);
      setDay({ key, detail });
      setDayError(null);
    } catch (e) {
      setDay(null);
      setDayError(e instanceof ApiError ? e.message : "Failed to load the selected date.");
    }
  }, [selDate, corridorId]);

  useEffect(() => {
    if (!corridorId) return;
    let active = true;
    const key = `${corridorId}·${selDate}`;
    getDayDetail(selDate, corridorId).then(
      (detail) => {
        if (active) {
          setDay({ key, detail });
          setDayError(null);
        }
      },
      (e) => {
        if (active) {
          setDay(null);
          setDayError(e instanceof ApiError ? e.message : "Failed to load the selected date.");
        }
      },
    );
    return () => {
      active = false;
    };
  }, [selDate, corridorId]);

  /** After any booking mutation: the day panel and the month badges both move. */
  const refreshAll = useCallback(async () => {
    await Promise.all([loadDay(), loadMonth()]);
  }, [loadDay, loadMonth]);

  const summaryByDate = new Map<string, CalendarDaySummary>();
  if (monthData && monthData.key === monthKey) {
    for (const s of monthData.rows) summaryByDate.set(s.date, s);
  }

  const corridor = corridors?.find((c) => c.corridorId === corridorId) ?? null;
  const route = corridorId ? routes?.find((r) => r.id === corridorId) ?? null : null;
  const dayDetail = day && day.key === dayKey ? day.detail : null;

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader
          eyebrow="Business · Community seat bookings per corridor"
          title="Booking Calendar"
        />
      </div>

      <div
        style={{
          flex: 1,
          minHeight: 0,
          display: "grid",
          gridTemplateColumns: "minmax(0, 1fr) minmax(360px, 44%)",
          borderTop: `1px solid ${colors.border}`,
        }}
      >
        {/* LEFT — corridor selector + month grid */}
        <div style={{ minHeight: 0, overflowY: "auto", padding: "18px 22px", borderRight: `1px solid ${colors.border}` }}>
          {corridorsError && (
            <Panel borderColor="rgba(213,94,0,.4)" style={{ marginBottom: 14 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
                <StatusChip kind="over" label={`Corridors unavailable — ${corridorsError}`} />
                <ActionButton variant="primary" onClick={loadCorridors}>
                  RETRY
                </ActionButton>
              </div>
            </Panel>
          )}

          {corridors !== null && corridors.length === 0 && (
            <Panel style={{ marginBottom: 14 }}>
              <SectionLabel>No corridors synced yet</SectionLabel>
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6 }}>
                Booking corridors mirror the community routes in Routes &amp; Schedules. None have synced across
                yet — open each community route there and re-save it once, and it will appear here.
              </div>
            </Panel>
          )}

          {corridors !== null && corridors.length > 0 && (
            <div style={{ display: "flex", flexWrap: "wrap", gap: 8, marginBottom: 16 }}>
              {[...corridors]
                .sort((a, b) => Number(b.active) - Number(a.active) || a.name.localeCompare(b.name))
                .map((c) => {
                  const selected = c.corridorId === corridorId;
                  return (
                    <span
                      key={c.corridorId}
                      onClick={() => setCorridorId(c.corridorId)}
                      title={`${c.origin} → ${c.destination}`}
                      style={{
                        display: "inline-flex",
                        alignItems: "center",
                        gap: 7,
                        padding: "6px 13px",
                        borderRadius: 8,
                        cursor: "pointer",
                        userSelect: "none",
                        fontFamily: fonts.body,
                        fontSize: 12.5,
                        fontWeight: selected ? 700 : 500,
                        background: selected ? colors.cardBgActive : colors.cardBg,
                        border: `1px solid ${selected ? colors.borderActive : colors.borderSubtle}`,
                        boxShadow: selected ? `inset 3px 0 0 ${colors.blue}, ${colors.shadowCard}` : colors.shadowCard,
                        color: selected ? colors.headingBright : colors.textSecondary,
                        opacity: c.active ? 1 : 0.62,
                      }}
                    >
                      {c.name}
                      {!c.active && <StatusChip kind="off" label="Inactive" />}
                    </span>
                  );
                })}
            </div>
          )}

          {corridors === null && !corridorsError && (
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, marginBottom: 14 }}>
              Loading corridors…
            </div>
          )}

          {monthError && corridorId && (
            <Panel borderColor="rgba(213,94,0,.4)" style={{ marginBottom: 12 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
                <StatusChip kind="over" label={`Calendar unavailable — ${monthError}`} />
                <ActionButton variant="primary" onClick={loadMonth}>
                  RETRY
                </ActionButton>
              </div>
            </Panel>
          )}

          {corridorId && (
            <>
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
                  const summary = inMonth ? summaryByDate.get(dateIso) : undefined;
                  const badge = inMonth ? dayBadge(summary) : null;
                  const selected = selDate === dateIso;
                  const past = dateIso < today;
                  return (
                    <div
                      onClick={inMonth ? () => setSelDate(dateIso) : undefined}
                      title={badge?.title}
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
                        opacity: !inMonth ? 0.35 : past ? 0.55 : 1,
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
                        {summary?.minimumGuaranteed && (
                          <span
                            title="Gift-a-Seat — minimum guaranteed, the day never reverts"
                            style={{
                              marginLeft: "auto",
                              fontFamily: fonts.mono,
                              fontSize: 8.5,
                              fontWeight: 700,
                              color: statusMeta("soon").t,
                            }}
                          >
                            GT
                          </span>
                        )}
                        {summary?.hasOverrides && (
                          <span
                            title="Per-date override on minimum or capacity"
                            style={{
                              marginLeft: summary.minimumGuaranteed ? 0 : "auto",
                              fontFamily: fonts.mono,
                              fontSize: 8.5,
                              color: colors.textFaint,
                            }}
                          >
                            OV
                          </span>
                        )}
                      </div>
                      {inMonth &&
                        (badge ? (
                          <span
                            style={{
                              display: "inline-flex",
                              alignItems: "center",
                              gap: 4,
                              fontFamily: fonts.mono,
                              fontSize: 9,
                              fontWeight: 700,
                              color: statusMeta(badge.kind).t,
                              lineHeight: 1,
                            }}
                          >
                            <CellBadge kind={badge.kind} />
                            {badge.label}
                          </span>
                        ) : (
                          // No activity — neutral gray, glyph + label so the
                          // state is explicit, not just an empty cell.
                          <span
                            style={{
                              display: "inline-flex",
                              alignItems: "center",
                              gap: 4,
                              fontFamily: fonts.mono,
                              fontSize: 9,
                              color: colors.textFaint,
                              lineHeight: 1,
                              opacity: 0.7,
                            }}
                          >
                            <CellBadge kind="off" />
                          </span>
                        ))}
                    </div>
                  );
                }}
              />

              {/* legend — colour + icon + text, matching the day-cell badges */}
              <div style={{ display: "flex", flexWrap: "wrap", gap: 14, alignItems: "center", marginTop: 12 }}>
                {(
                  [
                    { kind: "off" as StatusKind, label: "No bookings" },
                    { kind: "soon" as StatusKind, label: "Below minimum (sold/min)" },
                    { kind: "ontime" as StatusKind, label: "Minimum met" },
                    { kind: "over" as StatusKind, label: "Overbooked / Reverted" },
                  ] as const
                ).map((l) => (
                  <span key={l.kind} style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
                    <CellBadge kind={l.kind} size={14} />
                    <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: statusMeta(l.kind).t, fontWeight: 600 }}>
                      {l.label}
                    </span>
                  </span>
                ))}
              </div>
            </>
          )}
        </div>

        {/* RIGHT — selected-date panel */}
        <div style={{ minHeight: 0, overflowY: "auto", padding: "18px 22px", background: colors.detailBg }}>
          {!corridorId ? (
            <Panel>
              <SectionLabel>Select a corridor</SectionLabel>
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6 }}>
                Pick a corridor on the left to see its booking calendar and take bookings for a date.
              </div>
            </Panel>
          ) : (
            <DayPanel
              key={`${corridorId}·${selDate}`}
              date={selDate}
              corridor={corridor}
              detail={dayDetail}
              loadError={dayError}
              onRetry={loadDay}
              onChanged={refreshAll}
              onOpenTrip={onOpenTrip}
              routeStops={route?.stops ?? []}
              isOwner={isOwner}
              past={selDate < today}
            />
          )}
        </div>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Day panel — numbers, booking list, per-date overrides, inline create form
// ---------------------------------------------------------------------------

function DayPanel({
  date,
  corridor,
  detail,
  loadError,
  onRetry,
  onChanged,
  onOpenTrip,
  routeStops,
  isOwner,
  past,
}: {
  date: string;
  corridor: CorridorRecord | null;
  detail: DayDetail | null;
  loadError: string | null;
  onRetry: () => void;
  onChanged: () => Promise<void>;
  onOpenTrip: (id: string) => void;
  routeStops: TripStop[];
  isOwner: boolean;
  past: boolean;
}) {
  const [busyId, setBusyId] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);

  async function act(id: string, fn: (id: string) => Promise<void>) {
    if (busyId) return;
    setBusyId(id);
    setActionError(null);
    try {
      await fn(id);
      await onChanged();
    } catch (e) {
      setActionError(e instanceof ApiError ? e.message : "The change failed — please try again.");
    } finally {
      setBusyId(null);
    }
  }

  if (loadError) {
    return (
      <Panel borderColor="rgba(213,94,0,.4)">
        <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
          <StatusChip kind="over" label={`Date unavailable — ${loadError}`} />
          <ActionButton variant="primary" onClick={onRetry}>
            RETRY
          </ActionButton>
        </div>
      </Panel>
    );
  }

  if (!detail) {
    return (
      <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
        Loading {shortDateLabel(date)}…
      </div>
    );
  }

  const minOverridden = detail.passengerMinimumOverride !== null;
  const capOverridden = detail.seatCapacityOverride !== null;
  const overrideTag = (
    <span
      title="Per-date override"
      style={{
        fontFamily: fonts.mono,
        fontSize: 9,
        padding: "1px 5px",
        borderRadius: 4,
        marginLeft: 6,
        background: statusMeta("soon").bg,
        border: `1px solid ${statusMeta("soon").bd}`,
        color: statusMeta("soon").t,
        fontWeight: 700,
      }}
    >
      OVERRIDE
    </span>
  );

  return (
    <div className="detailfade">
      <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap", marginBottom: 12 }}>
        <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 24, lineHeight: 1, color: colors.headingBright, margin: 0 }}>
          {shortDateLabel(date)}
        </h2>
        {/* Day lifecycle (US-B.9–11) — colour + glyph + text via statusMeta. */}
        <StatusChip
          kind={dayStatusKind(detail.status)}
          label={
            detail.status === "Confirmed"
              ? "Confirmed"
              : detail.status === "Reverted"
                ? `Reverted — needs ${detail.neededToConfirm}`
                : "Pending"
          }
        />
        {detail.minimumGuaranteed && <StatusChip kind="soon" label="GUARANTEED" />}
        {past && <StatusChip kind="off" label="Past date" />}
        {detail.tripId && (
          <ActionButton onClick={() => onOpenTrip(detail.tripId!)} style={{ marginLeft: "auto" }}>
            OPEN TRIP →
          </ActionButton>
        )}
      </div>

      {actionError && (
        <Panel borderColor="rgba(213,94,0,.4)" style={{ marginBottom: 12 }}>
          <StatusChip kind="over" label={actionError} />
        </Panel>
      )}

      <Panel style={{ marginBottom: 12 }}>
        <SectionLabel>Demand &amp; capacity</SectionLabel>
        <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
          <DetailRow label="Corridor" value={detail.corridorName || corridor?.name || "—"} />
          <DetailRow
            label="Vehicle"
            value={
              detail.tripId ? (
                <span style={{ fontFamily: fonts.mono }}>{detail.tripNumber ?? "Trip created"}</span>
              ) : (
                "— no trip yet"
              )
            }
          />
          <DetailRow label="Seats sold" value={String(detail.sold)} valueStyle={{ fontFamily: fonts.mono }} />
          <DetailRow label="Pending holds" value={String(detail.pending)} valueStyle={{ fontFamily: fonts.mono }} />
          <DetailRow label="Remaining" value={String(detail.remaining)} valueStyle={{ fontFamily: fonts.mono }} />
          <DetailRow
            label="Needed to confirm"
            value={
              detail.neededToConfirm === 0 ? (
                <StatusChip kind="ontime" label="Minimum met" />
              ) : (
                <span style={{ fontFamily: fonts.mono }}>{detail.neededToConfirm}</span>
              )
            }
          />
          <DetailRow
            label="Passenger minimum"
            value={
              <span style={{ fontFamily: fonts.mono }}>
                {detail.passengerMinimum}
                {minOverridden && overrideTag}
              </span>
            }
          />
          <DetailRow
            label="Seat capacity"
            value={
              <span style={{ fontFamily: fonts.mono }}>
                {detail.capacity}
                {capOverridden && overrideTag}
              </span>
            }
          />
        </div>
      </Panel>

      {/* Gift-a-Seat (US-B.11): hidden once guaranteed (the header chip takes
          over), and needs a materialized day (the endpoint 404s otherwise). */}
      {!past && !detail.minimumGuaranteed && detail.bookingDayId && (
        <GuaranteeSection
          bookingDayId={detail.bookingDayId}
          reverted={detail.status === "Reverted"}
          neededToConfirm={detail.neededToConfirm}
          onChanged={onChanged}
        />
      )}

      {isOwner && detail.bookingDayId && (
        <OverridesEditor
          bookingDayId={detail.bookingDayId}
          minimumOverride={detail.passengerMinimumOverride}
          capacityOverride={detail.seatCapacityOverride}
          onChanged={onChanged}
        />
      )}

      <Panel style={{ marginBottom: 12 }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 10, marginBottom: 11 }}>
          <SectionLabel>Bookings · {detail.bookings.length}</SectionLabel>
          <ActionButton variant="primary" onClick={() => setShowCreate((v) => !v)} style={{ marginTop: -8 }}>
            {showCreate ? "CLOSE FORM" : "+ NEW BOOKING"}
          </ActionButton>
        </div>

        {detail.bookings.length === 0 && (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, lineHeight: 1.6 }}>
            No bookings for this date yet.
          </div>
        )}

        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          {detail.bookings.map((b) => (
            <BookingRow key={b.id} booking={b} busy={busyId === b.id} anyBusy={busyId !== null} onAct={act} />
          ))}
        </div>
      </Panel>

      {showCreate && (
        <CreateBookingForm
          date={date}
          corridorId={detail.corridorId}
          routeStops={routeStops}
          onCreated={async () => {
            setShowCreate(false);
            await onChanged();
          }}
        />
      )}
    </div>
  );
}

// ---------------------------------------------------------------------------
// One booking row
// ---------------------------------------------------------------------------

function BookingRow({
  booking,
  busy,
  anyBusy,
  onAct,
}: {
  booking: BookingRecord;
  busy: boolean;
  anyBusy: boolean;
  onAct: (id: string, fn: (id: string) => Promise<void>) => Promise<void>;
}) {
  const b = booking;
  const cancelled = b.status === "Cancelled";
  return (
    <div
      style={{
        padding: "11px 13px",
        borderRadius: 9,
        border: `1px solid ${colors.borderSubtle}`,
        background: colors.cardBg,
        boxShadow: colors.shadowCard,
        opacity: cancelled ? 0.62 : 1,
      }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
        <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
          {b.customerName}
        </span>
        <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.textDim }}>
          {b.passengers.length} pax
        </span>
        <span style={{ marginLeft: "auto", display: "inline-flex", gap: 6, alignItems: "center" }}>
          <StatusChip kind={bookingStatusKind(b.status)} label={b.status} />
          {b.holdExpired && b.status === "Unconfirmed" && <StatusChip kind="soon" label="hold expired" />}
        </span>
      </div>
      <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginTop: 4 }}>
        {locationLabel(b.pickup)} → {locationLabel(b.dropoff)} · {PAYMENT_METHOD_LABELS[b.paymentMethod]} ·{" "}
        {PAYMENT_STATUS_LABELS[b.paymentStatus]}
      </div>
      {b.notes && (
        <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textMuted, marginTop: 3, lineHeight: 1.5 }}>
          {b.notes}
        </div>
      )}
      {!cancelled && (
        <div style={{ display: "flex", gap: 8, marginTop: 9 }}>
          {b.status === "Unconfirmed" && (
            <ActionButton variant="success" disabled={anyBusy} onClick={() => void onAct(b.id, confirmBooking)}>
              {busy ? "WORKING…" : "CONFIRM"}
            </ActionButton>
          )}
          <ActionButton variant="destructive" disabled={anyBusy} onClick={() => void onAct(b.id, cancelBooking)}>
            {busy ? "WORKING…" : "CANCEL BOOKING"}
          </ActionButton>
        </div>
      )}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Gift-a-Seat guarantee (US-B.11) — a business commitment, so the button asks
// for an explicit confirm step before calling the API.
// ---------------------------------------------------------------------------

function GuaranteeSection({
  bookingDayId,
  reverted,
  neededToConfirm,
  onChanged,
}: {
  bookingDayId: string;
  reverted: boolean;
  neededToConfirm: number;
  onChanged: () => Promise<void>;
}) {
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function run() {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      await guaranteeDay(bookingDayId);
      await onChanged();
      // On success the panel refetches with minimumGuaranteed=true and this
      // section unmounts — no local state to reset.
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to set the guarantee — please try again.");
      setBusy(false);
    }
  }

  return (
    <Panel style={{ marginBottom: 12 }}>
      <SectionLabel>Gift-a-Seat guarantee</SectionLabel>
      <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6, marginBottom: 10 }}>
        {reverted
          ? `Guaranteeing re-confirms this reverted day: the run departs even if the remaining ${neededToConfirm} seat(s) never sell.`
          : "Guaranteeing pledges the unsold minimum: the day runs even below the passenger minimum and never reverts."}
      </div>
      {!confirming ? (
        <ActionButton variant="primary" onClick={() => setConfirming(true)}>
          GUARANTEE MINIMUM
        </ActionButton>
      ) : (
        <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
          <StatusChip kind="soon" label="This is a business commitment — the seats are covered either way." />
          <ActionButton variant="success" disabled={busy} onClick={() => void run()}>
            {busy ? "WORKING…" : "CONFIRM GUARANTEE"}
          </ActionButton>
          <ActionButton disabled={busy} onClick={() => setConfirming(false)}>
            CANCEL
          </ActionButton>
        </div>
      )}
      {error && (
        <div style={{ marginTop: 9 }}>
          <StatusChip kind="over" label={error} />
        </div>
      )}
    </Panel>
  );
}

// ---------------------------------------------------------------------------
// Per-date overrides (Owner UX gate; the API enforces AdminOnly)
// ---------------------------------------------------------------------------

function OverridesEditor({
  bookingDayId,
  minimumOverride,
  capacityOverride,
  onChanged,
}: {
  bookingDayId: string;
  minimumOverride: number | null;
  capacityOverride: number | null;
  onChanged: () => Promise<void>;
}) {
  const [min, setMin] = useState(minimumOverride === null ? "" : String(minimumOverride));
  const [cap, setCap] = useState(capacityOverride === null ? "" : String(capacityOverride));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function apply() {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      await setDayOverrides(bookingDayId, {
        passengerMinimum: min.trim() === "" ? null : Number(min),
        seatCapacity: cap.trim() === "" ? null : Number(cap),
      });
      await onChanged();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to save the overrides — please try again.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <Panel style={{ marginBottom: 12 }}>
      <SectionLabel>Per-date overrides (Owner)</SectionLabel>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr auto", gap: 10, alignItems: "end" }}>
        <NumberField
          label="Minimum override"
          value={min}
          onChange={setMin}
          min={0}
          placeholder="corridor default"
          hint={<span style={{ color: colors.textFaint }}>· blank clears</span>}
        />
        <NumberField
          label="Capacity override"
          value={cap}
          onChange={setCap}
          min={0}
          placeholder="corridor default"
          hint={<span style={{ color: colors.textFaint }}>· blank clears</span>}
        />
        <ActionButton variant="primary" onClick={() => void apply()} disabled={busy} style={{ height: 40, boxSizing: "border-box" }}>
          {busy ? "WORKING…" : "APPLY"}
        </ActionButton>
      </div>
      {error && (
        <div style={{ marginTop: 9 }}>
          <StatusChip kind="over" label={error} />
        </div>
      )}
    </Panel>
  );
}

// ---------------------------------------------------------------------------
// Inline create-booking form (US-B.8)
// ---------------------------------------------------------------------------

interface PassengerRow {
  name: string;
  phone: string;
}

function CreateBookingForm({
  date,
  corridorId,
  routeStops,
  onCreated,
}: {
  date: string;
  corridorId: string;
  routeStops: TripStop[];
  onCreated: () => Promise<void>;
}) {
  // --- customer (search-as-you-type, or create-new expansion) ---
  const [custQuery, setCustQuery] = useState("");
  const [custResults, setCustResults] = useState<CustomerRecord[] | null>(null);
  const [customer, setCustomer] = useState<CustomerRecord | null>(null);
  const [newCustomer, setNewCustomer] = useState(false);
  const [newName, setNewName] = useState("");
  const [newPhone, setNewPhone] = useState("");
  const [newEmail, setNewEmail] = useState("");

  // --- passengers (US-B.3/5): the billing customer may or may not travel ---
  const [customerTravelling, setCustomerTravelling] = useState(true);
  const [rows, setRows] = useState<PassengerRow[]>([]);

  // --- pickup / dropoff ---
  const [pickupSel, setPickupSel] = useState<string>(routeStops[0]?.name ?? FREE_STOP);
  const [pickupFree, setPickupFree] = useState("");
  const [pickupAddr, setPickupAddr] = useState("");
  const [dropSel, setDropSel] = useState<string>(routeStops[routeStops.length - 1]?.name ?? FREE_STOP);
  const [dropFree, setDropFree] = useState("");
  const [dropAddr, setDropAddr] = useState("");

  const [paymentMethod, setPaymentMethod] = useState<BookingPaymentMethod>("Square");
  const [notes, setNotes] = useState("");

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Search-as-you-type (250 ms debounce). Blank query returns the full roster,
  // which doubles as the "browse customers" starting point.
  useEffect(() => {
    if (customer) return; // a customer is selected — no live search needed
    let active = true;
    const t = setTimeout(() => {
      searchCustomers(custQuery.trim()).then(
        (found) => {
          if (active) setCustResults(found);
        },
        () => {
          if (active) setCustResults([]);
        },
      );
    }, 250);
    return () => {
      active = false;
      clearTimeout(t);
    };
  }, [custQuery, customer]);

  const stopOptions = [
    ...routeStops.map((s) => ({ value: s.name, label: s.name })),
    { value: FREE_STOP, label: routeStops.length > 0 ? "Other (type below)" : "Free text (no route stops)" },
  ];

  function buildLocation(sel: string, free: string, addr: string): BookingLocationInput | null {
    const addressDetail = addr.trim() || undefined;
    if (sel === FREE_STOP) {
      const stopName = free.trim() || undefined;
      if (!stopName && !addressDetail) return null;
      return { stopName, addressDetail };
    }
    const stop = routeStops.find((s) => s.name === sel);
    if (!stop) return null;
    // stopName is required whenever stopId is set; some routes carry snapshot
    // stops without a catalog stopId — those go across by name only.
    return { stopId: stop.stopId ?? undefined, stopName: stop.name, addressDetail };
  }

  function validate(): string | null {
    if (!customer && !(newCustomer && newName.trim())) {
      return "Pick a customer, or enter a name to create a new one.";
    }
    const travellerCount = (customerTravelling ? 1 : 0) + rows.filter((r) => r.name.trim()).length;
    if (travellerCount === 0) return "A booking needs at least one passenger.";
    if (rows.some((r) => !r.name.trim() && r.phone.trim())) {
      return "Every passenger needs a name (phone is optional).";
    }
    if (!buildLocation(pickupSel, pickupFree, pickupAddr)) return "The pickup needs a stop or a typed location.";
    if (!buildLocation(dropSel, dropFree, dropAddr)) return "The drop-off needs a stop or a typed location.";
    return null;
  }

  async function submit() {
    if (busy) return;
    const problem = validate();
    if (problem) {
      setError(problem);
      return;
    }
    setBusy(true);
    setError(null);
    try {
      let customerId = customer?.id ?? null;
      let customerName = customer?.name ?? "";
      let customerPhone = customer?.phone ?? null;
      if (!customerId) {
        customerId = await createCustomer({
          name: newName.trim(),
          phone: newPhone.trim() || null,
          email: newEmail.trim() || null,
        });
        customerName = newName.trim();
        customerPhone = newPhone.trim() || null;
      }

      const passengers: BookingPassengerInput[] = [];
      if (customerTravelling) {
        passengers.push({ name: customerName, phone: customerPhone, isBillingCustomer: true });
      }
      for (const r of rows) {
        if (!r.name.trim()) continue;
        passengers.push({ name: r.name.trim(), phone: r.phone.trim() || null, isBillingCustomer: false });
      }

      await createBooking({
        customerId,
        corridorId,
        serviceDate: date,
        pickup: buildLocation(pickupSel, pickupFree, pickupAddr)!,
        dropoff: buildLocation(dropSel, dropFree, dropAddr)!,
        passengers,
        paymentMethod,
        notes: notes.trim() || null,
      });
      await onCreated();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to create the booking — please try again.");
    } finally {
      setBusy(false);
    }
  }

  const checkboxRow = (checked: boolean, onToggle: () => void, label: string) => (
    <label
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 8,
        fontFamily: fonts.body,
        fontSize: 12.5,
        color: colors.textSecondary,
        cursor: "pointer",
        userSelect: "none",
      }}
    >
      <input type="checkbox" checked={checked} onChange={onToggle} style={{ accentColor: colors.blue }} />
      {label}
    </label>
  );

  return (
    <Panel style={{ marginBottom: 12 }}>
      <SectionLabel>New booking · {shortDateLabel(date)}</SectionLabel>

      {/* --- customer --- */}
      {customer ? (
        <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap", marginBottom: 12 }}>
          <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
            {customer.name}
          </span>
          {customer.phone && (
            <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textDim }}>{customer.phone}</span>
          )}
          <ActionButton onClick={() => setCustomer(null)}>CHANGE</ActionButton>
        </div>
      ) : newCustomer ? (
        <div style={{ marginBottom: 12 }}>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
            <TextField label="New customer name" value={newName} onChange={setNewName} placeholder="Full name" />
            <TextField label="Phone (optional)" value={newPhone} onChange={setNewPhone} mono placeholder="204-555-0000" />
          </div>
          <div style={{ marginTop: 10 }}>
            <TextField label="Email (optional)" value={newEmail} onChange={setNewEmail} type="email" />
          </div>
          <div style={{ marginTop: 8 }}>
            <ActionButton onClick={() => setNewCustomer(false)}>BACK TO SEARCH</ActionButton>
          </div>
        </div>
      ) : (
        <div style={{ marginBottom: 12 }}>
          <TextField
            label="Customer"
            value={custQuery}
            onChange={setCustQuery}
            placeholder="Search by name or phone…"
          />
          <div style={{ display: "flex", flexDirection: "column", gap: 4, marginTop: 6, maxHeight: 180, overflowY: "auto" }}>
            {(custResults ?? []).slice(0, 8).map((c) => (
              <div
                key={c.id}
                onClick={() => setCustomer(c)}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 10,
                  padding: "7px 11px",
                  borderRadius: 8,
                  border: `1px solid ${colors.borderSubtle}`,
                  background: colors.cardBg,
                  cursor: "pointer",
                }}
              >
                <span style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary }}>
                  {c.name}
                </span>
                {c.phone && <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.textDim }}>{c.phone}</span>}
              </div>
            ))}
            {custResults !== null && custResults.length === 0 && (
              <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim, padding: "4px 2px" }}>
                No matching customer.
              </div>
            )}
          </div>
          <div style={{ marginTop: 8 }}>
            <ActionButton
              onClick={() => {
                setNewCustomer(true);
                setNewName(custQuery.trim());
              }}
            >
              + CREATE NEW CUSTOMER
            </ActionButton>
          </div>
        </div>
      )}

      {/* --- passengers --- */}
      <div style={{ marginBottom: 12 }}>
        <FieldLabel>Passengers</FieldLabel>
        <div style={{ marginBottom: 8 }}>
          {checkboxRow(customerTravelling, () => setCustomerTravelling((v) => !v), "Customer is travelling")}
        </div>
        {rows.map((r, i) => (
          <div key={i} style={{ display: "grid", gridTemplateColumns: "1fr 1fr auto", gap: 8, marginBottom: 8 }}>
            <TextField
              label={`Passenger ${i + 1 + (customerTravelling ? 1 : 0)} name`}
              value={r.name}
              onChange={(v) => setRows((rs) => rs.map((x, j) => (j === i ? { ...x, name: v } : x)))}
            />
            <TextField
              label="Phone (optional)"
              value={r.phone}
              mono
              onChange={(v) => setRows((rs) => rs.map((x, j) => (j === i ? { ...x, phone: v } : x)))}
            />
            <ActionButton
              variant="destructive"
              onClick={() => setRows((rs) => rs.filter((_, j) => j !== i))}
              style={{ alignSelf: "end", height: 40, boxSizing: "border-box" }}
            >
              REMOVE
            </ActionButton>
          </div>
        ))}
        <ActionButton onClick={() => setRows((rs) => [...rs, { name: "", phone: "" }])}>+ ADD PASSENGER</ActionButton>
      </div>

      {/* --- pickup / dropoff --- */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10, marginBottom: 12 }}>
        <div>
          <SelectField label="Pickup" value={pickupSel} onChange={setPickupSel} options={stopOptions} />
          {pickupSel === FREE_STOP && (
            <div style={{ marginTop: 8 }}>
              <TextField label="Pickup location" value={pickupFree} onChange={setPickupFree} placeholder="Community / landmark" />
            </div>
          )}
          <div style={{ marginTop: 8 }}>
            <TextField label="Address detail (optional)" value={pickupAddr} onChange={setPickupAddr} placeholder="House 12, blue door" />
          </div>
        </div>
        <div>
          <SelectField label="Drop-off" value={dropSel} onChange={setDropSel} options={stopOptions} />
          {dropSel === FREE_STOP && (
            <div style={{ marginTop: 8 }}>
              <TextField label="Drop-off location" value={dropFree} onChange={setDropFree} placeholder="Community / landmark" />
            </div>
          )}
          <div style={{ marginTop: 8 }}>
            <TextField label="Address detail (optional)" value={dropAddr} onChange={setDropAddr} placeholder="Clinic entrance" />
          </div>
        </div>
      </div>

      {/* --- payment + notes --- */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 2fr", gap: 10, marginBottom: 12 }}>
        <SelectField
          label="Payment method"
          value={paymentMethod}
          onChange={(v) => setPaymentMethod(v as BookingPaymentMethod)}
          options={(Object.keys(PAYMENT_METHOD_LABELS) as BookingPaymentMethod[]).map((m) => ({
            value: m,
            label: PAYMENT_METHOD_LABELS[m],
          }))}
          hint={<span style={{ color: colors.textFaint }}>· recorded only, no processing yet</span>}
        />
        <TextAreaField label="Notes (optional)" value={notes} onChange={setNotes} rows={2} />
      </div>

      {error && (
        <div style={{ marginBottom: 10 }}>
          <StatusChip kind="over" label={error} />
        </div>
      )}

      <ActionButton variant="primary" onClick={() => void submit()} disabled={busy}>
        {busy ? "WORKING…" : "CREATE BOOKING"}
      </ActionButton>
    </Panel>
  );
}
