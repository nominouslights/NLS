"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, rowSurface, statusMeta, type StatusKind } from "@/lib/theme";
import { ApiError, formatUtcDate } from "@/lib/api";
import {
  changeDriverStatus,
  credentialKindFor,
  listDriverCredentials,
  listDriverClearances,
  listDriverHosEntries,
  listDrivers,
  refetchUntil,
  removeDriverCredential,
  revokeDriverClearance,
  sortClearances,
  sortCredentials,
  sortHosEntries,
  type DriverClearanceRecord,
  type DriverCredentialRecord,
  type DriverRecord,
  type HosEntryRecord,
} from "@/lib/api/drivers";
import { leaveOnDate } from "@/lib/driverCompliance";
import { corridorLabel, listTrips, sortTrips, tripChip, tripWindowLabel, type TripRecord } from "@/lib/api/trips";
import { hosRemainingFor, hosViolationsFor } from "@/lib/hosRules";
import { useDriverStore } from "@/lib/driverStore";
import { initials } from "@/lib/format";
import type { DutyStatus, LeaveType } from "@/lib/types";
import { PageHeader, Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { MonthGrid } from "@/components/ui/MonthGrid";
import { DutyChip, ExpiryFlag, PermitTag } from "./drivers/chips";
import { CredentialRow } from "./drivers/CredentialRow";
import { ClearanceRow } from "./drivers/ClearanceRow";
import { HosLogRow } from "./drivers/HosLogRow";
import CredentialFormModal from "@/components/CredentialFormModal";
import CredentialImageLightbox from "@/components/CredentialImageLightbox";
import CredentialPhotoModal from "@/components/CredentialPhotoModal";
import DriverFormModal from "@/components/DriverFormModal";
import HosLogEntryModal from "@/components/HosLogEntryModal";
import ClearanceFormModal from "@/components/ClearanceFormModal";
import LeaveFormModal from "@/components/LeaveFormModal";

// Drivers & Compliance — driver records, credentials, client clearances, and
// hours-of-service now come from the real Drivers API (lib/api/drivers.ts), and
// the Calendar tab's trip history comes from the real Trips API
// (GET /api/trips?driverId=). Only leave stays on the frontend mock
// (lib/data.ts): leave is explicitly outside the backend Drivers module's
// scope and is still keyed by a numeric prototype id — joined here by roster
// position (row index + 1, `mockId`).

const TABS = ["Profile", "Licence & certs", "Hours of Service", "Clearances", "Calendar", "History"];

const EYEBROW = "Operations · Roster & compliance engine source";
const TITLE = "Drivers & Compliance";

// "Today" anchor for the Calendar tab's default month/day — matches the
// "Today ≈ 2026-07-17" convention already used by the hosLogs seed comment
// in lib/data.ts (HOS-2020 is dated 2026-07-17).
const TODAY_ISO = "2026-07-17";
const TODAY_YEAR = 2026;
const TODAY_MONTH = 6; // July, 0-based

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
  useDriverStore(); // re-render when the mock HOS/leave store mutates

  const [roster, setRoster] = useState<DriverRecord[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [tab, setTab] = useState(0);
  const [modal, setModal] = useState<null | "credential" | "hos" | "clearance" | "leave" | "register" | "edit">(null);
  const [calYear, setCalYear] = useState(TODAY_YEAR);
  const [calMonth, setCalMonth] = useState(TODAY_MONTH);
  const [selectedDay, setSelectedDay] = useState(TODAY_ISO);

  // Per-selected-driver detail data (real API). Keyed by driverId so switching
  // drivers naturally reads as "loading" until this driver's fetch lands — no
  // synchronous state resets needed inside the effect.
  const [credsState, setCredsState] = useState<{ driverId: string; rows: DriverCredentialRecord[] } | null>(null);
  const [clearState, setClearState] = useState<{ driverId: string; rows: DriverClearanceRecord[] } | null>(null);
  const [hosState, setHosState] = useState<{ driverId: string; rows: HosEntryRecord[] } | null>(null);
  const [tripState, setTripState] = useState<{ driverId: string; rows: TripRecord[] } | null>(null);
  const [detailError, setDetailError] = useState<{ driverId: string; message: string } | null>(null);

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
      (rows) => {
        if (active) setTripState({ driverId, rows: sortTrips(rows) });
      },
      () => {
        if (active) setTripState({ driverId, rows: [] });
      },
    );
    // HOS entries — best-effort like trips; an unreachable fetch leaves the log
    // empty rather than blanking the compliance records.
    listDriverHosEntries(driverId).then(
      (rows) => {
        if (active) setHosState({ driverId, rows: sortHosEntries(rows) });
      },
      () => {
        if (active) setHosState({ driverId, rows: [] });
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

  async function onHosAdded(driverId: string, newId: string) {
    await runAction(driverId, async () => {
      const rows = await refetchUntil(
        () => listDriverHosEntries(driverId),
        (r) => r.some((e) => e.id === newId),
      );
      setHosState({ driverId, rows: sortHosEntries(rows) });
      loadRoster(); // latestDutyStatus / latestDrivingHours rollups changed
    });
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
    setCalMonth((m) => {
      if (m === 0) {
        setCalYear((y) => y - 1);
        return 11;
      }
      return m - 1;
    });
  }

  function nextMonth() {
    setCalMonth((m) => {
      if (m === 11) {
        setCalYear((y) => y + 1);
        return 0;
      }
      return m + 1;
    });
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
  const hosEntries = hosState?.driverId === d.id ? hosState.rows : null;
  const logs = hosEntries ?? [];
  const hosRemaining = hosRemainingFor(hosEntries?.[0]?.drivingH ?? null);
  const hos = statusMeta(hosRemaining.kind);
  const expiring = (creds ?? []).filter((c) => {
    const k = credentialKindFor(c.expiry);
    return k === "soon" || k === "over";
  });
  const worstExpiring: StatusKind = expiring.some((c) => credentialKindFor(c.expiry) === "over") ? "over" : "soon";
  const calTrips = tripState?.driverId === d.id ? tripState.rows : [];
  const calViolations = hosViolationsFor(logs);
  const selectedTrips = calTrips.filter((t) => t.serviceDate === selectedDay);
  const selectedLeave = leaveOnDate(mockId, selectedDay);
  const selectedViolations = calViolations.filter((v) => v.date === selectedDay);

  function statusOrDutyChip(row: DriverRecord) {
    if (row.status === "Deactivated") return <StatusChip kind="over" label="Deactivated" />;
    if (row.status === "Inactive") return <StatusChip kind="off" label="Inactive" />;
    return <DutyChip status={(row.latestDutyStatus ?? "Off Duty") as DutyStatus} />;
  }

  const hosGauge = (
    <Panel style={{ marginBottom: 12, padding: "16px 18px", borderRadius: 11 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 10 }}>
        <div
          style={{
            fontFamily: fonts.semiCondensed,
            fontSize: 9.5,
            letterSpacing: ".14em",
            textTransform: "uppercase",
            color: colors.textLabel,
          }}
        >
          Hours of Service · CVDHS cycle · latest duty day
        </div>
        <div style={{ fontFamily: fonts.mono, fontSize: 13, fontWeight: 500, color: hos.t }}>
          {hosRemaining.label} remaining
        </div>
      </div>
      <div style={{ height: 10, borderRadius: 6, background: colors.inputBg, overflow: "hidden", border: `1px solid ${colors.borderSubtle}` }}>
        <div style={{ height: "100%", width: `${hosRemaining.pct}%`, background: hos.c, borderRadius: 6 }} />
      </div>
      <div style={{ display: "flex", justifyContent: "space-between", fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginTop: 7 }}>
        <span>13h drive · 14h on-duty · 10h off-duty</span>
        <span>220 km corridor — no short-haul exemption</span>
      </div>
    </Panel>
  );

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
              gridTemplateColumns: "34px 1fr 96px 110px",
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
            const rowHosRemaining = hosRemainingFor(row.latestDrivingHours);
            const rowHos = statusMeta(rowHosRemaining.kind);
            const rowDeactivated = row.status === "Deactivated";
            return (
              <div
                key={row.id}
                onClick={() => setDriverSel(i)}
                style={{
                  display: "grid",
                  gridTemplateColumns: "34px 1fr 96px 110px",
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
                <div style={{ fontFamily: fonts.mono, fontSize: 12, fontWeight: 500, color: rowHos.t }}>
                  {rowHosRemaining.label}
                </div>
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
                {hosGauge}
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

            {/* ---- Hours of Service (real — GET/POST /api/drivers/{id}/hos) ---- */}
            {tab === 2 && (
              <>
                <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12 }}>
                  <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted }}>Current duty status</span>
                  {statusOrDutyChip(d)}
                </div>
                {hosGauge}
                <Panel>
                  <div style={{ display: "flex", alignItems: "center", marginBottom: 14 }}>
                    <SectionLabel>Duty log · driver app + paper backup</SectionLabel>
                    <ActionButton variant="primary" style={{ marginLeft: "auto" }} onClick={() => setModal("hos")}>
                      + LOG HOS ENTRY
                    </ActionButton>
                  </div>
                  {hosEntries === null ? (
                    <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading hours of service…</div>
                  ) : logs.length === 0 ? (
                    <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>No hours-of-service entries recorded.</div>
                  ) : (
                    <div style={{ marginTop: -2 }}>
                      {logs.map((entry) => (
                        <HosLogRow key={entry.id} entry={entry} />
                      ))}
                    </div>
                  )}
                </Panel>
              </>
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
                            {violation && (
                              <span style={{ fontSize: 10, color: statusMeta("over").t }}>{statusMeta("over").g}</span>
                            )}
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
                  {selectedViolations.length > 0 && (
                    <div style={{ display: "flex", flexDirection: "column", gap: 6, marginBottom: 10 }}>
                      {selectedViolations.map((v, i) => (
                        <StatusChip key={i} kind="over" label={v.message} />
                      ))}
                    </div>
                  )}
                  {selectedTrips.length === 0 ? (
                    !selectedLeave &&
                    selectedViolations.length === 0 && (
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
            {modal === "hos" && (
              <HosLogEntryModal
                driverId={d.id}
                driverName={d.name}
                onClose={() => setModal(null)}
                onSaved={(newId) => onHosAdded(d.id, newId)}
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
