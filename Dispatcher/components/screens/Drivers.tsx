"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, rowSurface, statusMeta, type StatusKind } from "@/lib/theme";
import { ApiError, formatUtcDate } from "@/lib/api";
import {
  changeDriverStatus,
  credentialKindFor,
  listDriverCredentials,
  listDriverClearances,
  listDrivers,
  refetchUntil,
  removeDriverCredential,
  revokeDriverClearance,
  sortClearances,
  sortCredentials,
  type DriverClearanceRecord,
  type DriverCredentialRecord,
  type DriverRecord,
} from "@/lib/api/drivers";
import { getHosStatus, listDutyDays, listHosStatusBatch, type DutyDaySummary, type HosRosterStatus, type HosStatus } from "@/lib/api/hos";
import { leaveOnDate } from "@/lib/driverCompliance";
import { corridorLabel, listTrips, sortTrips, tripChip, tripWindowLabel, type TripRecord } from "@/lib/api/trips";
import { BAND_KIND, blockingShort, blockingText, dutyStateFromWire, formatRemaining } from "@/lib/hos/display";
import { useDriverStore } from "@/lib/driverStore";
import { useTodayIso } from "@/lib/useTodayIso";
import { initials } from "@/lib/format";
import type { LeaveType } from "@/lib/types";
import { PageHeader, Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { MonthGrid } from "@/components/ui/MonthGrid";
import { DutyChip, ExpiryFlag, PermitTag } from "./drivers/chips";
import { CredentialRow } from "./drivers/CredentialRow";
import { ClearanceRow } from "./drivers/ClearanceRow";
import { HoursOfServiceTab } from "./drivers/HoursOfServiceTab";
import { HosStatusGauge } from "./drivers/hos/HosStatusGauge";
import CredentialFormModal from "@/components/CredentialFormModal";
import CredentialImageLightbox from "@/components/CredentialImageLightbox";
import CredentialPhotoModal from "@/components/CredentialPhotoModal";
import DriverFormModal from "@/components/DriverFormModal";
import ClearanceFormModal from "@/components/ClearanceFormModal";
import LeaveFormModal from "@/components/LeaveFormModal";

// Drivers & Compliance — driver records, credentials, client clearances, and
// hours of service (the duty-event ledger: lib/api/hos.ts) come from the real
// Drivers API, and the Calendar tab's trip history comes from the real Trips
// API (GET /api/trips?driverId=). Only leave stays on the frontend mock
// (lib/data.ts): leave is explicitly outside the backend Drivers module's
// scope and is still keyed by a numeric prototype id — joined here by roster
// position (row index + 1, `mockId`).

const TABS = ["Profile", "Licence & certs", "Hours of Service", "Clearances", "Calendar", "History"];

const EYEBROW = "Operations · Roster & compliance engine source";
const TITLE = "Drivers & Compliance";

/** First and last day of a month as DateOnly strings (month is 0-based). */
function monthRange(year: number, month: number): { from: string; to: string } {
  const pad = (n: number) => String(n).padStart(2, "0");
  const last = new Date(Date.UTC(year, month + 1, 0)).getUTCDate();
  return { from: `${year}-${pad(month + 1)}-01`, to: `${year}-${pad(month + 1)}-${pad(last)}` };
}

// Leave type → StatusKind, reusing the app's established color+icon+label
// system instead of inventing new colours for leave.
const LEAVE_KIND: Record<LeaveType, StatusKind> = {
  Vacation: "info",
  Sick: "soon",
  "Leave Without Pay": "over",
};

// Shared frame for the pre-data states so the header stays consistent.
function ScreenFrame({ children, right }: { children: React.ReactNode; right?: React.ReactNode }) {
  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow={EYEBROW} title={TITLE} right={right} />
      </div>
      {children}
    </div>
  );
}

function DriversLoadError({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div style={{ padding: "26px", maxWidth: 560 }}>
      <Panel borderColor="rgba(213,94,0,.4)">
        <div style={{ display: "flex", gap: 11, alignItems: "center", marginBottom: 12 }}>
          <span
            style={{
              width: 20,
              height: 20,
              flex: "none",
              borderRadius: 5,
              background: "#D55E00",
              color: "#fff",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: 11,
              fontWeight: 800,
            }}
          >
            ▲
          </span>
          <span style={{ fontFamily: fonts.body, fontSize: 13.5, fontWeight: 600, color: statusMeta("over").t }}>
            Driver roster unavailable
          </span>
        </div>
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6, marginBottom: 14 }}>
          {message}
        </div>
        <ActionButton variant="primary" onClick={onRetry}>
          RETRY
        </ActionButton>
      </Panel>
    </div>
  );
}

function DriversLoadingSkeleton() {
  return (
    <div style={{ padding: "16px 26px" }}>
      {[0, 1, 2, 3, 4].map((i) => (
        <div
          key={i}
          style={{
            height: 62,
            borderRadius: 9,
            border: `1px solid ${colors.borderSubtle}`,
            background: colors.cardBg,
            marginBottom: 6,
            opacity: 0.55 - i * 0.08,
          }}
        />
      ))}
      <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, marginTop: 10 }}>
        Loading drivers from API…
      </div>
    </div>
  );
}

export default function Drivers({
  driverSel,
  setDriverSel,
}: {
  driverSel: number;
  setDriverSel: (i: number) => void;
}) {
  useDriverStore(); // re-render when the mock leave store mutates
  // Today's date (yyyy-MM-dd) anchors the calendar's default month/day and the
  // HOS tab; empty until hydrated, so the calendar state below is nullable and
  // falls back to it.
  const todayIso = useTodayIso();
  const todayYear = todayIso ? Number(todayIso.slice(0, 4)) : new Date().getFullYear();
  const todayMonth = todayIso ? Number(todayIso.slice(5, 7)) - 1 : new Date().getMonth();

  const [roster, setRoster] = useState<DriverRecord[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [tab, setTab] = useState(0);
  const [modal, setModal] = useState<null | "credential" | "clearance" | "leave" | "register" | "edit">(null);
  const [calState, setCalState] = useState<{ year: number; month: number } | null>(null);
  const [selectedDayState, setSelectedDay] = useState<string | null>(null);
  const calYear = calState?.year ?? todayYear;
  const calMonth = calState?.month ?? todayMonth;
  const selectedDay = selectedDayState ?? todayIso;

  // Per-selected-driver detail data (real API). Keyed by driverId so switching
  // drivers naturally reads as "loading" until this driver's fetch lands — no
  // synchronous state resets needed inside the effect.
  const [credsState, setCredsState] = useState<{ driverId: string; rows: DriverCredentialRecord[] } | null>(null);
  const [clearState, setClearState] = useState<{ driverId: string; rows: DriverClearanceRecord[] } | null>(null);
  const [tripState, setTripState] = useState<{ driverId: string; rows: TripRecord[] } | null>(null);
  const [detailError, setDetailError] = useState<{ driverId: string; message: string } | null>(null);

  // Hours of service — the ledger's evaluation per driver (gauge on Profile + HOS
  // tabs), the roster batch (one call for every row's band), and the calendar
  // month's duty days (violation / uncertified markers). `hosVersion` bumps after
  // every ledger write so all three reload.
  const [hosVersion, setHosVersion] = useState(0);
  const [hosStatusState, setHosStatusState] = useState<{ driverId: string; status: HosStatus | null; error: string | null } | null>(null);
  const [hosBatch, setHosBatch] = useState<Map<string, HosRosterStatus> | null>(null);
  const [calDaysState, setCalDaysState] = useState<{ driverId: string; year: number; month: number; rows: DutyDaySummary[] } | null>(null);

  const [busy, setBusy] = useState(false);
  const [actionErrorState, setActionErrorState] = useState<{ driverId: string; message: string } | null>(null);

  // Image lightbox — when not null, shows the credential photo for the identified credential
  const [imageLightbox, setImageLightbox] = useState<{ driverId: string; credentialId: string } | null>(null);
  const [photoUploadModal, setPhotoUploadModal] = useState<{ driverId: string; credentialId: string } | null>(null);

  const loadRoster = useCallback(async () => {
    try {
      const fresh = await listDrivers();
      setRoster(fresh);
      setLoadError(null);
    } catch (e) {
      setRoster(null);
      setLoadError(e instanceof ApiError ? e.message : "Failed to load drivers.");
    }
  }, []);

  useEffect(() => {
    // Fetch on mount; setState only inside the async callbacks, with a
    // mounted guard (same convention as Fleet.tsx).
    let active = true;
    listDrivers().then(
      (fresh) => {
        if (active) {
          setRoster(fresh);
          setLoadError(null);
        }
      },
      (e) => {
        if (active) {
          setRoster(null);
          setLoadError(e instanceof ApiError ? e.message : "Failed to load drivers.");
        }
      },
    );
    return () => {
      active = false;
    };
  }, []);

  const selIndex = roster && driverSel < roster.length ? driverSel : 0;
  const d = roster?.[selIndex] ?? null;
  // Numeric prototype key joining the selected driver to the mock LEAVE rows in
  // lib/data.ts (roster position + 1) — leave is the only remaining mock.
  const mockId = selIndex + 1;

  useEffect(() => {
    if (!d?.id) return;
    let active = true;
    const driverId = d.id;
    // Trip history is best-effort — an unreachable Trips API leaves the
    // calendar empty without blocking the compliance records.
    listTrips({ driverId }).then(
      (res) => {
        if (active) setTripState({ driverId, rows: sortTrips(res.items) });
      },
      () => {
        if (active) setTripState({ driverId, rows: [] });
      },
    );
    Promise.all([listDriverCredentials(driverId), listDriverClearances(driverId)]).then(
      ([c, cl]) => {
        if (active) {
          setCredsState({ driverId, rows: sortCredentials(c) });
          setClearState({ driverId, rows: sortClearances(cl) });
          setDetailError(null);
        }
      },
      (e) => {
        if (active) {
          setDetailError({
            driverId,
            message: e instanceof ApiError ? e.message : "Failed to load compliance records.",
          });
        }
      },
    );
    return () => {
      active = false;
    };
  }, [d?.id]);

  // HOS standing for the selected driver — keyed by driverId like the rest, and
  // re-fetched after every ledger write. The error is kept per driver so the
  // tab can say why the gauge is missing without blanking the other records.
  useEffect(() => {
    if (!d?.id) return;
    let active = true;
    const driverId = d.id;
    getHosStatus(driverId).then(
      (status) => {
        if (active) setHosStatusState({ driverId, status, error: null });
      },
      (e) => {
        if (active) setHosStatusState({ driverId, status: null, error: e instanceof ApiError ? e.message : "Failed to load hours of service." });
      },
    );
    return () => {
      active = false;
    };
  }, [d?.id, hosVersion]);

  // The roster batch: every driver's band in one call. Best-effort — an
  // unreachable batch leaves the "HOS left" column as an em dash, never a figure.
  useEffect(() => {
    if (roster === null) return;
    let active = true;
    listHosStatusBatch().then(
      (rows) => {
        if (active) setHosBatch(new Map(rows.map((r) => [r.driverId, r])));
      },
      () => {
        if (active) setHosBatch(new Map());
      },
    );
    return () => {
      active = false;
    };
  }, [roster, hosVersion]);

  // The calendar month's duty days — the server's evaluation per day drives the
  // ▲ (violation) and ◷ (uncertified) markers. Best-effort like trips.
  useEffect(() => {
    if (!d?.id || tab !== 4) return;
    let active = true;
    const driverId = d.id;
    const year = calYear;
    const month = calMonth;
    listDutyDays(driverId, monthRange(year, month)).then(
      (rows) => {
        if (active) setCalDaysState({ driverId, year, month, rows });
      },
      () => {
        if (active) setCalDaysState({ driverId, year, month, rows: [] });
      },
    );
    return () => {
      active = false;
    };
  }, [d?.id, tab, calYear, calMonth, hosVersion]);

  // -------------------------------------------------------------------------
  // Mutations — reads are eventually consistent projections, so refetch with
  // a short retry until the change is visible (refetchUntil).
  // -------------------------------------------------------------------------

  async function runAction(driverId: string, fn: () => Promise<void>) {
    if (busy) return;
    setBusy(true);
    setActionErrorState(null);
    try {
      await fn();
    } catch (e) {
      setActionErrorState({
        driverId,
        message: e instanceof ApiError ? e.message : "Action failed — please try again.",
      });
    } finally {
      setBusy(false);
    }
  }

  async function onCredentialAdded(driverId: string, newId: string) {
    await runAction(driverId, async () => {
      const rows = await refetchUntil(
        () => listDriverCredentials(driverId),
        (r) => r.some((c) => c.id === newId),
      );
      setCredsState({ driverId, rows: sortCredentials(rows) });
      loadRoster(); // credentialCount / soonestCredentialExpiry rollups changed
    });
  }

  async function onRemoveCredential(driverId: string, credentialId: string) {
    await runAction(driverId, async () => {
      await removeDriverCredential(driverId, credentialId);
      const rows = await refetchUntil(
        () => listDriverCredentials(driverId),
        (r) => !r.some((c) => c.id === credentialId),
      );
      setCredsState({ driverId, rows: sortCredentials(rows) });
      loadRoster();
    });
  }

  // A ledger write (transcription / amendment) landed: the status, the roster
  // batch and the calendar all re-read. The ledger is written synchronously by
  // the command, so a plain re-fetch is enough — no refetchUntil needed.
  function onLedgerChanged() {
    setHosVersion((v) => v + 1);
    loadRoster(); // the driver read model's duty rollup follows the ledger
  }

  async function onClearanceAdded(driverId: string, newId: string) {
    await runAction(driverId, async () => {
      const rows = await refetchUntil(
        () => listDriverClearances(driverId),
        (r) => r.some((c) => c.id === newId),
      );
      setClearState({ driverId, rows: sortClearances(rows) });
    });
  }

  async function onRemoveClearance(driverId: string, clearanceId: string) {
    await runAction(driverId, async () => {
      await revokeDriverClearance(driverId, clearanceId);
      const rows = await refetchUntil(
        () => listDriverClearances(driverId),
        (r) => !r.some((c) => c.id === clearanceId),
      );
      setClearState({ driverId, rows: sortClearances(rows) });
    });
  }

  async function onChangeStatus(driverId: string, status: DriverRecord["status"]) {
    await runAction(driverId, async () => {
      await changeDriverStatus(driverId, status);
      const fresh = await refetchUntil(
        () => listDrivers(),
        (rows) => rows.find((r) => r.id === driverId)?.status === status,
      );
      setRoster(fresh);
    });
  }

  // Register / edit save. Unlike Fleet (which stores a selected id), driverSel is
  // a roster *index* and the mock HOS/leave rows are keyed by index + 1 — so a
  // newly registered driver has to be resolved back to an index after the
  // refetch. refetchUntil is required, not defensive: the roster is an
  // eventually-consistent projection fed by a worker polling every ~5s, so the
  // GET right after a 201 can legitimately not contain the new driver yet.
  async function onDriverSaved(newId?: string) {
    if (busy) return;
    setBusy(true);
    setActionErrorState(null);
    try {
      const rows = newId
        ? await refetchUntil(
            () => listDrivers(),
            (r) => r.some((x) => x.id === newId),
          )
        : await listDrivers();
      setRoster(rows);
      setLoadError(null);
      if (newId) {
        const i = rows.findIndex((x) => x.id === newId);
        if (i >= 0) setDriverSel(i);
      }
    } catch (e) {
      // The write itself already succeeded — only the reload failed, so surface
      // it against the currently-shown driver rather than blanking the screen.
      const shownId = newId ?? roster?.[selIndex]?.id;
      if (shownId) {
        setActionErrorState({
          driverId: shownId,
          message: e instanceof ApiError ? e.message : "Driver saved, but the roster failed to reload.",
        });
      }
    } finally {
      setBusy(false);
    }
  }

  const headerActions = (
    <ActionButton variant="primary" onClick={() => setModal("register")}>
      + REGISTER DRIVER
    </ActionButton>
  );

  function prevMonth() {
    setCalState(calMonth === 0 ? { year: calYear - 1, month: 11 } : { year: calYear, month: calMonth - 1 });
  }

  function nextMonth() {
    setCalState(calMonth === 11 ? { year: calYear + 1, month: 0 } : { year: calYear, month: calMonth + 1 });
  }

  // -------------------------------------------------------------------------
  // Load / error / empty states
  // -------------------------------------------------------------------------

  if (loadError) {
    return (
      <ScreenFrame>
        <DriversLoadError message={loadError} onRetry={loadRoster} />
      </ScreenFrame>
    );
  }

  if (roster === null) {
    return (
      <ScreenFrame>
        <DriversLoadingSkeleton />
      </ScreenFrame>
    );
  }

  if (roster.length === 0 || d === null) {
    return (
      <ScreenFrame right={headerActions}>
        <div style={{ padding: "26px", maxWidth: 560 }}>
          <Panel>
            <SectionLabel>No drivers registered</SectionLabel>
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6, marginBottom: 14 }}>
              The driver roster is empty for this tenant. Register a driver to make them available
              for trip assignment, credential tracking, and client clearances.
            </div>
            <ActionButton variant="primary" onClick={() => setModal("register")}>
              + REGISTER DRIVER
            </ActionButton>
          </Panel>
        </div>
        {/* The detail-pane modals below are unreachable while the roster is
            empty, so the register modal is hosted here too (as Fleet does). */}
        {modal === "register" && (
          <DriverFormModal driver={null} onClose={() => setModal(null)} onSaved={onDriverSaved} />
        )}
      </ScreenFrame>
    );
  }

  // -------------------------------------------------------------------------
  // Loaded — roster + detail
  // -------------------------------------------------------------------------

  const deactivated = d.status === "Deactivated";
  const creds = credsState?.driverId === d.id ? credsState.rows : null;
  const clearances = clearState?.driverId === d.id ? clearState.rows : null;
  const detailErrorMessage = detailError?.driverId === d.id ? detailError.message : null;
  const actionError = actionErrorState?.driverId === d.id ? actionErrorState.message : null;
  const hosStatus = hosStatusState?.driverId === d.id ? hosStatusState.status : null;
  const hosStatusError = hosStatusState?.driverId === d.id ? hosStatusState.error : null;
  const expiring = (creds ?? []).filter((c) => {
    const k = credentialKindFor(c.expiry);
    return k === "soon" || k === "over";
  });
  const worstExpiring: StatusKind = expiring.some((c) => credentialKindFor(c.expiry) === "over") ? "over" : "soon";
  const calTrips = tripState?.driverId === d.id ? tripState.rows : [];
  // The month's server-evaluated days: a day with an unsuppressed violation gets ▲, a past
  // day with records the driver has not certified gets ◷.
  const calDays = calDaysState?.driverId === d.id && calDaysState.year === calYear && calDaysState.month === calMonth ? calDaysState.rows : [];
  const calViolations = calDays.flatMap((day) => day.violations.filter((v) => !v.suppressed).map((v) => ({ date: day.date, message: v.message })));
  const calUncertified = new Set(calDays.filter((day) => day.hasRecords && !day.certified && !!todayIso && day.date < todayIso).map((day) => day.date));
  const selectedTrips = calTrips.filter((t) => t.serviceDate === selectedDay);
  const selectedLeave = selectedDay ? leaveOnDate(mockId, selectedDay) : undefined;
  const selectedViolations = calViolations.filter((v) => v.date === selectedDay);
  const selectedDutyDay = calDays.find((day) => day.date === selectedDay) ?? null;

  function statusOrDutyChip(row: DriverRecord) {
    if (row.status === "Deactivated") return <StatusChip kind="over" label="Deactivated" />;
    if (row.status === "Inactive") return <StatusChip kind="off" label="Inactive" />;
    // The batch's current duty is the ledger's; the read-model rollup is the fallback.
    const duty = hosBatch?.get(row.id)?.currentDuty ?? row.latestDutyStatus;
    return <DutyChip status={dutyStateFromWire(duty)} />;
  }

  // The roster's "HOS left" cell: the batch band as colour + glyph + label; a driver
  // who may not drive shows ▲ with the first blocking reason. Em dash until loaded.
  function hosLeftChip(row: DriverRecord) {
    const s = hosBatch?.get(row.id);
    if (!s) return <StatusChip kind="off" label="—" />;
    if (!s.canDriveNow) {
      const first = s.blockingReasons[0];
      return (
        <span title={first ? blockingText(first) : "Cannot drive now"}>
          <StatusChip kind="over" label={first ? blockingShort(first) : "Cannot drive"} />
        </span>
      );
    }
    return <StatusChip kind={BAND_KIND[s.band]} label={formatRemaining(s.tightestRemainingSeconds)} />;
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow={EYEBROW} title={TITLE} right={headerActions} />
      </div>
      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "40% 1fr", borderTop: `1px solid ${colors.border}` }}>
        <div style={{ minHeight: 0, overflowY: "auto", padding: "16px 18px", borderRight: `1px solid ${colors.border}` }}>
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "34px 1fr 120px 110px",
              gap: 11,
              padding: "0 13px 9px",
              fontFamily: fonts.semiCondensed,
              fontSize: 9.5,
              letterSpacing: ".12em",
              textTransform: "uppercase",
              color: colors.textFaint,
            }}
          >
            <div />
            <div>Driver</div>
            <div>HOS left</div>
            <div>Status</div>
          </div>
          {roster.map((row, i) => {
            const active = i === selIndex;
            const rowDeactivated = row.status === "Deactivated";
            return (
              <div
                key={row.id}
                onClick={() => setDriverSel(i)}
                style={{
                  display: "grid",
                  gridTemplateColumns: "34px 1fr 120px 110px",
                  gap: 11,
                  alignItems: "center",
                  padding: "10px 13px",
                  marginBottom: 5,
                  opacity: rowDeactivated ? 0.62 : 1,
                  ...rowSurface(active, colors.blue),
                }}
              >
                <div
                  style={{
                    width: 34,
                    height: 34,
                    borderRadius: 9,
                    background: `linear-gradient(135deg,${colors.cardBgActive},${colors.borderStrong})`,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    fontFamily: fonts.condensed,
                    fontWeight: 700,
                    fontSize: 12,
                    color: colors.skyBlue,
                  }}
                >
                  {initials(row.name)}
                </div>
                <div style={{ minWidth: 0 }}>
                  <div
                    style={{
                      fontFamily: fonts.body,
                      fontSize: 13,
                      fontWeight: 600,
                      color: colors.textPrimary,
                      whiteSpace: "nowrap",
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                    }}
                  >
                    {row.name}
                  </div>
                  <div
                    style={{
                      fontFamily: fonts.semiCondensed,
                      fontSize: 10,
                      letterSpacing: ".06em",
                      textTransform: "uppercase",
                      color: row.source !== "Northern Link" ? colors.amberText : colors.textDim,
                    }}
                  >
                    {row.source}
                  </div>
                  {(credentialKindFor(row.soonestCredentialExpiry) !== "ontime" || row.hasWorkPermit) && (
                    <div style={{ display: "flex", flexWrap: "wrap", gap: 5, marginTop: 4 }}>
                      <ExpiryFlag soonestExpiry={row.soonestCredentialExpiry} />
                      {row.hasWorkPermit && <PermitTag />}
                    </div>
                  )}
                </div>
                <div>{hosLeftChip(row)}</div>
                <div>{statusOrDutyChip(row)}</div>
              </div>
            );
          })}
        </div>

        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: colors.detailBg }}>
          <div className="detailfade" key={d.id}>
            <div style={{ display: "flex", alignItems: "center", gap: 15, marginBottom: 18 }}>
              <div
                style={{
                  width: 56,
                  height: 56,
                  borderRadius: 13,
                  background: `linear-gradient(135deg,${colors.cardBgActive},${colors.borderStrong})`,
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontFamily: fonts.condensed,
                  fontWeight: 700,
                  fontSize: 20,
                  color: colors.skyBlue,
                }}
              >
                {initials(d.name)}
              </div>
              <div>
                <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 26, lineHeight: 1, color: colors.headingBright, margin: "0 0 6px" }}>
                  {d.name}
                </h2>
                <div style={{ display: "flex", alignItems: "center", gap: 10, fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted }}>
                  {statusOrDutyChip(d)}
                  <span>{d.source}</span>
                  <span style={{ color: colors.textFaint }}>·</span>
                  <span style={{ fontFamily: fonts.mono, fontSize: 11.5 }}>{d.phone ?? "—"}</span>
                  <span style={{ color: colors.textFaint }}>·</span>
                  <span>{d.licenceClass}</span>
                </div>
              </div>
              {d.hasWorkPermit && (
                <span style={{ marginLeft: deactivated ? undefined : "auto" }}>
                  <PermitTag />
                </span>
              )}
              {deactivated && (
                <span
                  style={{
                    marginLeft: "auto",
                    fontFamily: fonts.body,
                    fontWeight: 700,
                    fontSize: 12,
                    padding: "6px 13px",
                    borderRadius: 8,
                    background: "rgba(213,94,0,.14)",
                    border: "1px solid rgba(213,94,0,.4)",
                    color: statusMeta("over").t,
                  }}
                >
                  ▲ DEACTIVATED · excluded from eligibility
                </span>
              )}
            </div>

            {/* tab bar */}
            <div style={{ display: "flex", gap: 2, borderBottom: `1px solid ${colors.border}`, marginBottom: 16 }}>
              {TABS.map((label, i) => (
                <span
                  key={label}
                  onClick={() => setTab(i)}
                  style={{
                    fontFamily: fonts.body,
                    fontWeight: tab === i ? 600 : 500,
                    fontSize: 12.5,
                    padding: "9px 14px",
                    color: tab === i ? colors.headingBright : colors.textDim,
                    borderBottom: tab === i ? `2px solid ${colors.blue}` : undefined,
                    marginBottom: -1,
                    cursor: "pointer",
                  }}
                >
                  {label}
                </span>
              ))}
            </div>

            {detailErrorMessage && (
              <Panel borderColor="rgba(213,94,0,.4)" style={{ marginBottom: 12 }}>
                <StatusChip kind="over" label={`Compliance records unavailable — ${detailErrorMessage}`} />
              </Panel>
            )}
            {actionError && (
              <Panel borderColor="rgba(213,94,0,.4)" style={{ marginBottom: 12 }}>
                <StatusChip kind="over" label={actionError} />
              </Panel>
            )}

            {/* ---- Profile ---- */}
            {tab === 0 && (
              <>
                <Panel style={{ marginBottom: 12 }}>
                  <SectionLabel>Compliance summary</SectionLabel>
                  {creds === null ? (
                    <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading credentials…</div>
                  ) : expiring.length === 0 ? (
                    <StatusChip kind="ontime" label="All credentials current" />
                  ) : (
                    <StatusChip
                      kind={worstExpiring}
                      label={`${expiring.length} credential${expiring.length > 1 ? "s" : ""} need attention`}
                    />
                  )}
                  <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, lineHeight: 1.6, marginTop: 10 }}>
                    {d.source}
                    {d.hasWorkPermit && " · works under a foreign-worker permit"}
                    <br />
                    {d.licenceClass}
                    {d.licenceExpiry && ` · licence expires ${d.licenceExpiry}`} · {d.phone ?? "no phone on file"}
                    <br />
                    Registered {formatUtcDate(d.registeredAtUtc)}
                  </div>
                </Panel>
                {hosStatus ? (
                  <HosStatusGauge status={hosStatus} compact />
                ) : (
                  <Panel style={{ marginBottom: 12 }}>
                    {hosStatusError ? (
                      <StatusChip kind="over" label={`Hours of service unavailable — ${hosStatusError}`} />
                    ) : (
                      <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading hours of service…</div>
                    )}
                  </Panel>
                )}
              </>
            )}

            {/* ---- Licence & certs ---- */}
            {tab === 1 && (
              <Panel>
                <div style={{ display: "flex", alignItems: "center", marginBottom: 14 }}>
                  <SectionLabel>Licences &amp; certifications</SectionLabel>
                  <ActionButton variant="primary" style={{ marginLeft: "auto" }} onClick={() => setModal("credential")}>
                    + ADD CREDENTIAL
                  </ActionButton>
                </div>
                {creds === null ? (
                  <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading credentials…</div>
                ) : creds.length === 0 ? (
                  <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>No credentials on file.</div>
                ) : (
                  <div style={{ marginTop: -4 }}>
                    {creds.map((c) => (
                      <CredentialRow
                        key={c.id}
                        c={c}
                        busy={busy}
                        onRemove={() => onRemoveCredential(d.id, c.id)}
                        onViewImage={() => setImageLightbox({ driverId: d.id, credentialId: c.id })}
                        onAddPhoto={() => setPhotoUploadModal({ driverId: d.id, credentialId: c.id })}
                      />
                    ))}
                  </div>
                )}
              </Panel>
            )}

            {/* ---- Hours of Service (real — the duty-event ledger, lib/api/hos.ts) ---- */}
            {tab === 2 && (
              <HoursOfServiceTab
                driverId={d.id}
                driverName={d.name}
                today={todayIso}
                status={hosStatus}
                statusError={hosStatusError}
                version={hosVersion}
                onLedgerChanged={onLedgerChanged}
              />
            )}

            {/* ---- Clearances ---- */}
            {tab === 3 && (
              <Panel>
                <div style={{ display: "flex", alignItems: "center", marginBottom: 14 }}>
                  <SectionLabel>Client clearances</SectionLabel>
                  <ActionButton variant="primary" style={{ marginLeft: "auto" }} onClick={() => setModal("clearance")}>
                    + ADD CLEARANCE
                  </ActionButton>
                </div>
                {clearances === null ? (
                  <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading clearances…</div>
                ) : clearances.length === 0 ? (
                  <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>No client clearances on file</div>
                ) : (
                  <div style={{ marginTop: -4 }}>
                    {clearances.map((c) => (
                      <ClearanceRow key={c.id} c={c} busy={busy} onRemove={() => onRemoveClearance(d.id, c.id)} />
                    ))}
                  </div>
                )}
              </Panel>
            )}

            {/* ---- Calendar (real trips via GET /api/trips?driverId=; leave stays mock) ---- */}
            {tab === 4 && (
              <>
                <Panel style={{ marginBottom: 12 }}>
                  <div style={{ display: "flex", alignItems: "center", marginBottom: 14 }}>
                    <SectionLabel>Trip &amp; leave calendar</SectionLabel>
                    <ActionButton variant="primary" style={{ marginLeft: "auto" }} onClick={() => setModal("leave")}>
                      + ADD LEAVE
                    </ActionButton>
                  </div>
                  <MonthGrid
                    year={calYear}
                    month={calMonth}
                    onPrevMonth={prevMonth}
                    onNextMonth={nextMonth}
                    renderDay={(dateIso, inMonth) => {
                      const dayTrips = calTrips.filter((t) => t.serviceDate === dateIso);
                      const leave = leaveOnDate(mockId, dateIso);
                      const violation = calViolations.some((v) => v.date === dateIso);
                      const uncertified = calUncertified.has(dateIso);
                      const selected = dateIso === selectedDay;
                      const dayNum = Number(dateIso.slice(-2));
                      const leaveMeta = leave ? statusMeta(LEAVE_KIND[leave.type]) : null;
                      return (
                        <div
                          onClick={() => setSelectedDay(dateIso)}
                          style={{
                            minHeight: 52,
                            borderRadius: 8,
                            padding: "5px 6px",
                            cursor: "pointer",
                            opacity: inMonth ? 1 : 0.4,
                            background: selected ? colors.cardBgActive : leaveMeta ? leaveMeta.bg : colors.cardBg,
                            border: `1px solid ${selected ? colors.borderActive : leaveMeta ? leaveMeta.bd : colors.borderSubtle}`,
                          }}
                        >
                          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                            <span style={{ fontFamily: fonts.mono, fontSize: 11, color: inMonth ? colors.textSecondary : colors.textFaint }}>
                              {dayNum}
                            </span>
                            {violation ? (
                              <span style={{ fontSize: 10, color: statusMeta("over").t }} title="HOS violation">
                                {statusMeta("over").g}
                              </span>
                            ) : uncertified ? (
                              <span style={{ fontSize: 10, color: statusMeta("soon").t }} title="Not certified">
                                ◷
                              </span>
                            ) : null}
                          </div>
                          <div style={{ display: "flex", alignItems: "center", gap: 4, marginTop: 5 }}>
                            {dayTrips.length > 0 && (
                              <span
                                style={{ width: 6, height: 6, borderRadius: "50%", background: colors.blue, flex: "none" }}
                                title={`${dayTrips.length} trip${dayTrips.length > 1 ? "s" : ""}`}
                              />
                            )}
                            {leaveMeta && leave && (
                              <span style={{ fontSize: 9, color: leaveMeta.t, lineHeight: 1 }}>{leaveMeta.g}</span>
                            )}
                          </div>
                        </div>
                      );
                    }}
                  />
                </Panel>

                <Panel>
                  <SectionLabel>{formatUtcDate(selectedDay)}</SectionLabel>
                  {selectedLeave && (
                    <div style={{ marginBottom: 10 }}>
                      <StatusChip
                        kind={LEAVE_KIND[selectedLeave.type]}
                        label={`${selectedLeave.type} · ${selectedLeave.startDate} → ${selectedLeave.endDate}`}
                      />
                      {selectedLeave.note && (
                        <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, marginTop: 6 }}>
                          {selectedLeave.note}
                        </div>
                      )}
                    </div>
                  )}
                  {(selectedViolations.length > 0 || (selectedDay && calUncertified.has(selectedDay))) && (
                    <div style={{ display: "flex", flexDirection: "column", gap: 6, marginBottom: 10 }}>
                      {selectedViolations.map((v, i) => (
                        <StatusChip key={i} kind="over" label={v.message} />
                      ))}
                      {selectedDay && calUncertified.has(selectedDay) && selectedDutyDay && (
                        <StatusChip
                          kind={selectedDutyDay.recertificationRequired ? "over" : "soon"}
                          glyph={selectedDutyDay.recertificationRequired ? undefined : "◷"}
                          label={selectedDutyDay.recertificationRequired ? "Recertification required" : "Day not certified by the driver"}
                        />
                      )}
                    </div>
                  )}
                  {selectedTrips.length === 0 ? (
                    !selectedLeave &&
                    selectedViolations.length === 0 &&
                    !(selectedDay && calUncertified.has(selectedDay)) && (
                      <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Nothing scheduled.</div>
                    )
                  ) : (
                    <div style={{ marginTop: -2 }}>
                      {selectedTrips.map((t) => {
                        const chip = tripChip(t);
                        return (
                          <div
                            key={t.id}
                            style={{
                              display: "flex",
                              alignItems: "center",
                              justifyContent: "space-between",
                              gap: 12,
                              padding: "10px 0",
                              borderTop: `1px solid ${colors.borderSubtle}`,
                            }}
                          >
                            <div style={{ minWidth: 0 }}>
                              <div style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.skyBlue }}>
                                {t.tripNumber} · {tripWindowLabel(t)}
                              </div>
                              <div style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary, marginTop: 2 }}>
                                {corridorLabel(t)}
                              </div>
                            </div>
                            <StatusChip kind={chip.kind} label={chip.label} />
                          </div>
                        );
                      })}
                    </div>
                  )}
                </Panel>
              </>
            )}

            {/* ---- History ---- */}
            {tab === 5 && (
              <Panel>
                <SectionLabel>Activity</SectionLabel>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.7 }}>
                  Registered {formatUtcDate(d.registeredAtUtc)} · last updated {formatUtcDate(d.updatedAtUtc)}
                  <br />
                  {d.credentialCount} credential{d.credentialCount === 1 ? "" : "s"} on file
                  <br />
                  {calTrips.length} trip{calTrips.length === 1 ? "" : "s"} on record (Trips API) — see the Calendar tab.
                  Incident log comes with the Incidents domain integration.
                </div>
              </Panel>
            )}

            <div style={{ display: "flex", gap: 9, marginTop: 16 }}>
              <ActionButton>MESSAGE</ActionButton>
              <ActionButton onClick={() => setModal("edit")}>EDIT DRIVER</ActionButton>
              {deactivated ? (
                <ActionButton variant="success" onClick={() => onChangeStatus(d.id, "Active")} disabled={busy}>
                  {busy ? "WORKING…" : "REACTIVATE DRIVER"}
                </ActionButton>
              ) : (
                <ActionButton variant="destructive" onClick={() => onChangeStatus(d.id, "Deactivated")} disabled={busy}>
                  {busy ? "WORKING…" : "DEACTIVATE DRIVER"}
                </ActionButton>
              )}
            </div>

            {modal === "credential" && (
              <CredentialFormModal
                driverId={d.id}
                driverName={d.name}
                onClose={() => setModal(null)}
                onSaved={(newId) => onCredentialAdded(d.id, newId)}
              />
            )}
            {modal === "clearance" && (
              <ClearanceFormModal
                driverId={d.id}
                driverName={d.name}
                onClose={() => setModal(null)}
                onSaved={(newId) => onClearanceAdded(d.id, newId)}
              />
            )}
            {modal === "leave" && (
              <LeaveFormModal driverId={mockId} driverName={d.name} onClose={() => setModal(null)} />
            )}
            {(modal === "register" || modal === "edit") && (
              <DriverFormModal
                driver={modal === "edit" ? d : null}
                onClose={() => setModal(null)}
                onSaved={onDriverSaved}
              />
            )}
          </div>
        </div>
      </div>

      {/* Image lightbox — view credential photo */}
      {imageLightbox && (
        <CredentialImageLightbox
          driverId={imageLightbox.driverId}
          credentialId={imageLightbox.credentialId}
          onClose={() => setImageLightbox(null)}
        />
      )}

      {/* Photo upload modal — add/replace credential image */}
      {photoUploadModal && (
        <CredentialPhotoModal
          driverId={photoUploadModal.driverId}
          credentialId={photoUploadModal.credentialId}
          driverName={d.name}
          onClose={() => setPhotoUploadModal(null)}
          onUploaded={async () => {
            const { driverId, credentialId } = photoUploadModal;
            try {
              // Refetch credentials until the eventually-consistent read model
              // reflects the new image (hasImage flag flips true).
              const fresh = await refetchUntil(
                () => listDriverCredentials(driverId),
                (rows) => rows.find((r) => r.id === credentialId)?.hasImage === true,
              );
              setCredsState({ driverId, rows: sortCredentials(fresh) });
            } catch {
              // The upload itself succeeded — a failed refresh just leaves the
              // list stale until the next visit.
            }
          }}
        />
      )}
    </div>
  );
}
