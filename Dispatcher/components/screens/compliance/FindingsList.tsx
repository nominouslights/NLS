"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { listComplianceFindings, type ComplianceFindingRecord, type FindingStatus } from "@/lib/api/hos";
import type { DriverRecord } from "@/lib/api/drivers";
import { FINDING_STATUS_CHIP, SEVERITY_CHIP, nextFindingStatuses } from "@/lib/hos/compliance";
import { formatDateOnly, formatDateTimeUtc } from "@/lib/hos/display";
import { ActionButton } from "@/components/ui/Button";
import { Panel } from "@/components/ui/Panel";
import { OpenFindingModal, UpdateFindingModal } from "./FindingModal";
import { bodyRow, DriverLink, EmptyLine, FanOutFailures, headRow, LoadingLine, mono, Pill, RowAction, SpecChip, useRosterFanOut } from "./shared";

// Every driver's compliance findings (GET …/findings per driver — the fan-out), newest first,
// filtered by status. OPEN FINDING records a new one; UPDATE moves one forward.

type Filter = "Active" | FindingStatus | "All";
const FILTERS: { value: Filter; label: string }[] = [
  { value: "Active", label: "Open + corrected" },
  { value: "Open", label: "Open" },
  { value: "Corrected", label: "Corrected" },
  { value: "Closed", label: "Closed" },
  { value: "All", label: "All" },
];

const COLS = "minmax(120px,1fr) 96px minmax(150px,1.2fr) 96px 116px minmax(200px,2fr) 80px";

// Module-level so the fan-out effect's dependency is stable: no range = every finding on file.
const fetchFindings = (d: DriverRecord) => listComplianceFindings(d.id);

export function FindingsList({
  roster,
  today,
  version,
  onChanged,
  onOpenDriver,
}: {
  roster: DriverRecord[];
  today: string;
  version: number;
  onChanged: () => void;
  onOpenDriver: (driverId: string) => void;
}) {
  const [filter, setFilter] = useState<Filter>("Active");
  const [modal, setModal] = useState<null | { kind: "open" } | { kind: "update"; finding: ComplianceFindingRecord; driverName: string }>(null);
  const { rows, failures } = useRosterFanOut(roster, fetchFindings, version);

  const all = (rows ?? []).flatMap(({ driver, value }) => value.map((f) => ({ driver, f })));
  const shown = all
    .filter(({ f }) => (filter === "All" ? true : filter === "Active" ? f.status !== "Closed" : f.status === filter))
    .sort((a, b) => b.f.localDate.localeCompare(a.f.localDate) || b.f.openedAtUtc.localeCompare(a.f.openedAtUtc));

  return (
    <div>
      <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12, flexWrap: "wrap" }}>
        {FILTERS.map((x) => (
          <Pill key={x.value} active={filter === x.value} onClick={() => setFilter(x.value)}>
            {x.label}
          </Pill>
        ))}
        <ActionButton variant="primary" style={{ marginLeft: "auto" }} onClick={() => setModal({ kind: "open" })} disabled={roster.length === 0}>
          + OPEN FINDING
        </ActionButton>
      </div>

      <FanOutFailures failures={failures} what="Findings" />

      {rows === null ? (
        <LoadingLine>Loading findings for {roster.length} drivers (one request each)…</LoadingLine>
      ) : shown.length === 0 ? (
        <Panel>
          <EmptyLine>{all.length === 0 ? "No compliance findings on file." : "No findings match this filter."}</EmptyLine>
        </Panel>
      ) : (
        <Panel style={{ padding: "12px 0 0" }}>
          <div style={{ ...headRow, gridTemplateColumns: COLS }}>
            <div>Driver</div>
            <div>Date</div>
            <div>Rule</div>
            <div>Severity</div>
            <div>Status</div>
            <div>Description</div>
            <div />
          </div>
          {shown.map(({ driver, f }) => (
            <div key={f.id} data-testid="finding-row" style={{ ...bodyRow, gridTemplateColumns: COLS }}>
              <DriverLink driver={driver} onOpen={onOpenDriver} />
              <span style={mono}>{formatDateOnly(f.localDate)}</span>
              <span style={{ ...mono, color: colors.textPrimary }}>{f.rule}</span>
              <span>
                <SpecChip spec={SEVERITY_CHIP[f.severity]} />
              </span>
              <span>
                <SpecChip spec={FINDING_STATUS_CHIP[f.status]} />
              </span>
              <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, lineHeight: 1.5 }}>
                {f.description}
                {f.followUpNote && ` · Follow-up: ${f.followUpNote}`}
                <span style={{ ...mono, display: "block", fontSize: 10.5 }}>
                  {f.source} · opened {formatDateTimeUtc(f.openedAtUtc)}
                  {f.closedAtUtc && ` · closed ${formatDateTimeUtc(f.closedAtUtc)}`}
                </span>
              </span>
              <span style={{ textAlign: "right" }}>
                {nextFindingStatuses(f.status).length > 0 && (
                  <RowAction label={`Update finding ${f.rule} for ${driver.name}`} onClick={() => setModal({ kind: "update", finding: f, driverName: driver.name })}>
                    Update
                  </RowAction>
                )}
              </span>
            </div>
          ))}
        </Panel>
      )}

      {modal?.kind === "open" && <OpenFindingModal roster={roster} today={today} onClose={() => setModal(null)} onSaved={onChanged} />}
      {modal?.kind === "update" && (
        <UpdateFindingModal finding={modal.finding} driverName={modal.driverName} onClose={() => setModal(null)} onSaved={onChanged} />
      )}
    </div>
  );
}
