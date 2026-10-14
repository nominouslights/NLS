"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import { listDrivers, type DriverRecord } from "@/lib/api/drivers";
import { getHosAlertsToday, type HosAlertsToday } from "@/lib/api/hos";
import { alertKindChip, alertsTileSpec, hosToday, PLANNING_LINE } from "@/lib/hos/compliance";
import type { Period } from "@/lib/period";
import { StatusChip } from "@/components/ui/Chip";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { ActionButton } from "@/components/ui/Button";
import { AuditTable } from "./compliance/AuditTable";
import { FindingsList } from "./compliance/FindingsList";
import { OosRegister } from "./compliance/OosRegister";
import { EldMalfunctions } from "./compliance/EldMalfunctions";
import { DeferralQueue } from "./compliance/DeferralQueue";
import { DriverLink, LoadingLine, SpecChip } from "./compliance/shared";

// Compliance — the Compliance Officer's (and the Owner's) cross-driver view of the hours-of-
// service record: today's alerts, the driver × day audit, findings, the out-of-service
// register, open ELD malfunctions and the deferral/exception queue. Role-gated to
// COMPLIANCE_ROLES in lib/apps.ts (UX only — every endpoint here is ComplianceAccess, except
// the alerts, which are DriverRosterAccess).
//
// Everything but the alerts is per driver on the backend today, so each tab fans out over the
// roster — see the KNOWN COST note and the follow-up endpoints in lib/hos/compliance.ts.

export const COMPLIANCE_TABS = ["Audit", "Findings", "Out of service", "ELD malfunctions", "Deferrals & exceptions"] as const;
export type ComplianceTab = (typeof COMPLIANCE_TABS)[number];

export default function Compliance({
  tab,
  setTab,
  period,
  setPeriod,
  onOpenDriver,
}: {
  tab: ComplianceTab;
  setTab: (t: ComplianceTab) => void;
  period: Period;
  setPeriod: (p: Period) => void;
  onOpenDriver: (driverId: string) => void;
}) {
  // The regulation's today, fixed for the screen's life (a lazy initialiser, not a render read).
  const [today] = useState(() => hosToday());
  const [roster, setRoster] = useState<DriverRecord[] | null>(null);
  const [rosterError, setRosterError] = useState<string | null>(null);
  const [alerts, setAlerts] = useState<HosAlertsToday | null>(null);
  const [alertsError, setAlertsError] = useState<string | null>(null);
  // Bumped after every write so the open tab and the alerts reload.
  const [version, setVersion] = useState(0);

  const loadRoster = useCallback(() => {
    listDrivers().then(
      (rows) => {
        setRoster(rows);
        setRosterError(null);
      },
      (e) => setRosterError(e instanceof ApiError ? e.message : "Failed to load drivers."),
    );
  }, []);

  useEffect(() => {
    let active = true;
    listDrivers().then(
      (rows) => {
        if (active) {
          setRoster(rows);
          setRosterError(null);
        }
      },
      (e) => {
        if (active) setRosterError(e instanceof ApiError ? e.message : "Failed to load drivers.");
      },
    );
    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    let active = true;
    getHosAlertsToday().then(
      (a) => {
        if (active) {
          setAlerts(a);
          setAlertsError(null);
        }
      },
      (e) => {
        if (active) setAlertsError(e instanceof ApiError ? e.message : "Failed to load today's alerts.");
      },
    );
    return () => {
      active = false;
    };
  }, [version]);

  // Memoised: the fan-out keys its effect on the array's identity, so a fresh filter every
  // render would refetch forever. Deactivated drivers leave the audit grid (as they leave the
  // alerts); the registers keep them, since their history still stands.
  const auditRoster = useMemo(() => roster?.filter((d) => d.status !== "Deactivated") ?? null, [roster]);

  const onChanged = useCallback(() => setVersion((v) => v + 1), []);
  const spec = alerts ? alertsTileSpec(alerts) : null;

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <div style={{ fontFamily: fonts.semiCondensed, fontSize: 10.5, letterSpacing: ".16em", textTransform: "uppercase", color: colors.textFaint, marginBottom: 3 }}>
          Drivers &amp; Compliance · Hours of service
        </div>
        <h1 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 30, lineHeight: 1, color: colors.headingBright, margin: 0 }}>Compliance</h1>
        <div data-testid="planning-line" style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim, marginTop: 6 }}>
          {PLANNING_LINE}
        </div>
      </div>

      <div style={{ flex: 1, minHeight: 0, overflowY: "auto", padding: "0 26px 26px" }}>
        {/* Today */}
        <Panel style={{ marginBottom: 14 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 8, flexWrap: "wrap" }}>
            <SectionLabel>Today</SectionLabel>
            {spec && <StatusChip kind={spec.kind} glyph={spec.glyph} label={spec.total === 0 ? "All clear" : `${spec.total} alert${spec.total === 1 ? "" : "s"} · ${spec.breakdown}`} />}
          </div>
          {alertsError ? (
            <StatusChip kind="off" label={`Today's alerts unavailable — ${alertsError}`} />
          ) : alerts === null ? (
            <LoadingLine>Loading today&rsquo;s alerts…</LoadingLine>
          ) : (
            alerts.items.map((item, i) => (
              <div key={`${item.driverId}-${item.kind}-${i}`} data-testid="alert-item" style={{ display: "flex", alignItems: "center", gap: 10, padding: "6px 0", borderTop: `1px solid ${colors.borderSubtle}`, flexWrap: "wrap" }}>
                <SpecChip spec={alertKindChip(item.kind)} />
                <DriverLink driver={{ id: item.driverId, name: item.driverName }} onOpen={onOpenDriver} />
                <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted }}>{item.message}</span>
              </div>
            ))
          )}
        </Panel>

        {/* tab bar */}
        <div role="tablist" aria-label="Compliance registers" style={{ display: "flex", gap: 2, borderBottom: `1px solid ${colors.border}`, marginBottom: 16, flexWrap: "wrap" }}>
          {COMPLIANCE_TABS.map((label) => (
            <button
              key={label}
              type="button"
              role="tab"
              aria-selected={tab === label}
              onClick={() => setTab(label)}
              style={{
                appearance: "none",
                background: "none",
                border: "none",
                fontFamily: fonts.body,
                fontWeight: tab === label ? 600 : 500,
                fontSize: 12.5,
                padding: "9px 14px",
                color: tab === label ? colors.headingBright : colors.textDim,
                borderBottom: tab === label ? `2px solid ${colors.blue}` : "2px solid transparent",
                marginBottom: -1,
                cursor: "pointer",
              }}
            >
              {label}
            </button>
          ))}
        </div>

        {rosterError ? (
          <Panel borderColor="rgba(213,94,0,.4)">
            <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
              <StatusChip kind="over" label={`Drivers unavailable — ${rosterError}`} />
              <ActionButton variant="primary" onClick={loadRoster}>
                RETRY
              </ActionButton>
            </div>
          </Panel>
        ) : roster === null ? (
          <LoadingLine>Loading the roster…</LoadingLine>
        ) : (
          <div role="tabpanel" aria-label={tab}>
            {tab === "Audit" && auditRoster && (
              <AuditTable roster={auditRoster} period={period} onPeriodChange={setPeriod} today={today} version={version} onOpenDriver={onOpenDriver} />
            )}
            {tab === "Findings" && <FindingsList roster={roster} today={today} version={version} onChanged={onChanged} onOpenDriver={onOpenDriver} />}
            {tab === "Out of service" && <OosRegister roster={roster} version={version} onChanged={onChanged} onOpenDriver={onOpenDriver} />}
            {tab === "ELD malfunctions" && <EldMalfunctions roster={roster} today={today} version={version} onChanged={onChanged} onOpenDriver={onOpenDriver} />}
            {tab === "Deferrals & exceptions" && <DeferralQueue roster={roster} today={today} version={version} onChanged={onChanged} onOpenDriver={onOpenDriver} />}
          </div>
        )}
      </div>
    </div>
  );
}
