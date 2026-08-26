"use client";

import { useEffect, useState } from "react";
import { colors, fonts, rowSurface } from "@/lib/theme";
import { ApiError, formatCad, formatKm, formatUtcDate, type Vehicle } from "@/lib/api";
import {
  assignPmPlan,
  getVehiclePm,
  getVehiclePmDue,
  getVehiclePmOverhauls,
  listPmPlans,
  listVehiclePmHistory,
  seedDefaultPmPlan,
  unassignPmPlan,
  type PmCompletionWire,
  type PmDueStateWire,
  type PmEntryStatusWire,
  type PmOverhaulStatusWire,
  type PmPlanSummaryWire,
  type VehiclePmDueWire,
  type VehiclePmOverhaulsWire,
  type VehiclePmStatusWire,
} from "@/lib/api/pm";
import {
  formatDateOnly,
  formatKmDate,
  formatShopMinutes,
  KIND_LABEL,
  PM_STATE_LABEL,
  pmDueLabel,
  pmIntervalLabel,
  pmKind,
  TASK_LABEL,
  TIER_LABEL,
} from "@/lib/pmDisplay";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { SelectField } from "@/components/ui/Field";
import RecordPmCompletionModal from "@/components/RecordPmCompletionModal";
import { dimText, EmptyTabNote, errText, smallBtn } from "@/components/screens/fleet/vehicle-detail/shared";

// Preventive Maintenance tab (live Fleet API — lib/api/pm.ts). Four views as
// sub-tabs (a single stacked page would be unreadable at ~260 schedule rows):
// the due-for-shop-visit package, the full computed schedule, the
// overhaul-early decision view, and the append-only completion history.

const SUB_TABS = ["Due for shop visit", "Full schedule", "Overhauls", "History"] as const;

/** Explicit request size for the history read — the server's hard ceiling
 *  (IPmReadService.MaxHistoryLimit; the implicit default is only 200). A full
 *  page back means older entries exist, and the UI says so. */
const HISTORY_LIMIT = 1000;

interface PmData {
  vehicleId: string;
  status: VehiclePmStatusWire;
  due: VehiclePmDueWire;
  overhauls: VehiclePmOverhaulsWire;
  history: PmCompletionWire[];
  plans: PmPlanSummaryWire[];
}

export default function VehiclePm({
  vehicle,
  onOpenWorkOrders,
}: {
  vehicle: Vehicle;
  onOpenWorkOrders: () => void;
}) {
  const vehicleId = vehicle.id;
  const unit = vehicle.unitNumber;

  const [fetched, setFetched] = useState<PmData | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [reload, setReload] = useState(0);
  const [subTab, setSubTab] = useState(0);

  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [planSel, setPlanSel] = useState("");
  const [changingPlan, setChangingPlan] = useState(false);
  const [confirmUnassign, setConfirmUnassign] = useState(false);
  const [recording, setRecording] = useState<{ code: string } | "general" | null>(null);

  const [sysFilter, setSysFilter] = useState("all");
  const [stateFilter, setStateFilter] = useState<"all" | PmDueStateWire>("all");

  // Data is tagged with the vehicle it was fetched for (VehicleWorkOrders
  // idiom) so switching vehicles never shows another unit's schedule.
  const data = fetched?.vehicleId === vehicleId ? fetched : null;

  useEffect(() => {
    let active = true;
    // All five reads in one shot (admin console, small payloads) — one error
    // state, one skeleton, and every mutation refetches the lot via `reload`.
    Promise.all([
      getVehiclePm(vehicleId),
      getVehiclePmDue(vehicleId),
      getVehiclePmOverhauls(vehicleId),
      listVehiclePmHistory(vehicleId, HISTORY_LIMIT),
      listPmPlans(),
    ]).then(
      ([status, due, overhauls, history, plans]) => {
        if (active) {
          setFetched({ vehicleId, status, due, overhauls, history, plans });
          setLoadError(null);
        }
      },
      (e) => {
        if (active) setLoadError(e instanceof ApiError ? e.message : "Failed to load preventive maintenance.");
      },
    );
    return () => {
      active = false;
    };
  }, [vehicleId, reload]);

  async function runAction(fn: () => Promise<void>) {
    if (busy) return;
    setBusy(true);
    setActionError(null);
    try {
      await fn();
      setConfirmUnassign(false);
      setChangingPlan(false);
      setPlanSel("");
      setReload((n) => n + 1);
    } catch (e) {
      setActionError(e instanceof ApiError ? e.message : "Action failed — please try again.");
    } finally {
      setBusy(false);
    }
  }

  if (loadError) {
    return (
      <div style={{ ...errText, padding: "6px 2px" }}>
        ▲ {loadError}{" "}
        <ActionButton style={{ ...smallBtn, marginLeft: 8 }} onClick={() => setReload((n) => n + 1)}>
          RETRY
        </ActionButton>
      </div>
    );
  }

  if (data === null) {
    return <div style={{ ...dimText, padding: "6px 2px" }}>Loading preventive maintenance…</div>;
  }

  const { status, due, overhauls, history, plans } = data;

  // A plan change (or a plan edit) can leave the schedule's system filter
  // pointing at a system no schedule entry carries any more — the select would
  // hold a value absent from its options and the list would render empty with
  // no visible cause. Snap back to "All" (render-phase state adjustment).
  if (sysFilter !== "all" && !status.entries.some((e) => e.system === sysFilter)) {
    setSysFilter("all");
  }

  const planOptions = [
    { value: "", label: "Select a maintenance plan…" },
    ...plans.map((p) => ({
      value: p.id,
      label: `${p.name} · ${p.vehicleModel} (${p.itemCount} items, ${p.overhaulCount} overhauls)`,
    })),
  ];

  // -------------------------------------------------------------------------
  // No plan assigned yet
  // -------------------------------------------------------------------------

  if (!status.assigned) {
    return (
      <div>
        <SectionLabel>Preventive maintenance</SectionLabel>
        {actionError && <div style={{ ...errText, marginBottom: 10 }}>▲ {actionError}</div>}
        <Panel>
          <EmptyTabNote>
            {`${unit} has no maintenance plan assigned. Assign a plan to compute the PM schedule — due items, overhaul windows, and shop-visit packages — from this vehicle's odometer and service history.`}
          </EmptyTabNote>
          {plans.length === 0 ? (
            <div style={{ marginTop: 12 }}>
              <div style={{ ...dimText, marginBottom: 10 }}>
                No maintenance plans exist yet. Seed the default plan to start, or build one via PM PLANS in the header.
              </div>
              <ActionButton
                variant="primary"
                disabled={busy}
                onClick={() =>
                  runAction(async () => {
                    await seedDefaultPmPlan();
                  })
                }
              >
                {busy ? "SEEDING…" : "SEED DEFAULT PLAN"}
              </ActionButton>
            </div>
          ) : (
            <div style={{ marginTop: 12 }}>
              <div style={{ maxWidth: 420 }}>
                <SelectField label="Maintenance plan" value={planSel} onChange={setPlanSel} options={planOptions} />
              </div>
              <div style={{ marginTop: 12 }}>
                <ActionButton
                  variant="primary"
                  disabled={busy || !planSel}
                  onClick={() => runAction(() => assignPmPlan(vehicleId, planSel))}
                >
                  {busy ? "ASSIGNING…" : "ASSIGN PLAN"}
                </ActionButton>
              </div>
            </div>
          )}
        </Panel>
      </div>
    );
  }

  // -------------------------------------------------------------------------
  // Assigned — header + sub-tabs
  // -------------------------------------------------------------------------

  const dueCount = due.groups.reduce((n, g) => n + g.entries.length, 0);
  // A full page back from the history read means the server truncated at its
  // ceiling — the count label and HistoryView both say so.
  const historyTruncated = history.length >= HISTORY_LIMIT;
  const subTabCounts: (string | number)[] = [
    dueCount + due.notYetRecorded.length,
    status.entries.length,
    overhauls.overhauls.length,
    historyTruncated ? `${HISTORY_LIMIT.toLocaleString("en-CA")}+` : history.length,
  ];

  return (
    <div>
      {actionError && <div style={{ ...errText, marginBottom: 10 }}>▲ {actionError}</div>}

      {/* plan header */}
      <Panel style={{ marginBottom: 12 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
          <div style={{ minWidth: 0 }}>
            <SectionLabel>Assigned maintenance plan</SectionLabel>
            <div style={{ fontFamily: fonts.body, fontSize: 14, fontWeight: 700, color: colors.headingBright, marginBottom: 2 }}>
              {status.planName}
            </div>
            <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
              Assigned {formatUtcDate(status.assignedAtUtc)} · Odometer{" "}
              {status.currentOdometerKm != null ? formatKm(status.currentOdometerKm) : "—"}
            </div>
          </div>
          <div style={{ marginLeft: "auto", display: "flex", gap: 8, flexWrap: "wrap" }}>
            <ActionButton variant="primary" onClick={() => setRecording("general")}>
              + RECORD SERVICE
            </ActionButton>
            <ActionButton
              onClick={() => {
                setChangingPlan((v) => !v);
                setConfirmUnassign(false);
              }}
            >
              CHANGE PLAN
            </ActionButton>
            <ActionButton
              variant="destructive"
              onClick={() => {
                setConfirmUnassign(true);
                setChangingPlan(false);
              }}
            >
              UNASSIGN
            </ActionButton>
          </div>
        </div>

        {changingPlan && (
          <div style={{ marginTop: 13, paddingTop: 12, borderTop: `1px solid ${colors.borderSubtle}` }}>
            <div style={{ maxWidth: 420 }}>
              <SelectField label="New maintenance plan" value={planSel} onChange={setPlanSel} options={planOptions} />
            </div>
            <div style={{ display: "flex", gap: 9, marginTop: 12 }}>
              <ActionButton
                variant="primary"
                disabled={busy || !planSel}
                onClick={() => runAction(() => assignPmPlan(vehicleId, planSel))}
              >
                {busy ? "ASSIGNING…" : "ASSIGN PLAN"}
              </ActionButton>
              <ActionButton onClick={() => setChangingPlan(false)}>CANCEL</ActionButton>
            </div>
          </div>
        )}

        {confirmUnassign && (
          <div style={{ marginTop: 13, paddingTop: 12, borderTop: "1px solid rgba(213,94,0,.4)" }}>
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6, marginBottom: 12 }}>
              Unassigning the plan removes {unit}&apos;s computed PM schedule and due tracking. Logged
              completions are kept and re-apply if a plan is assigned again.
            </div>
            <div style={{ display: "flex", gap: 9 }}>
              <ActionButton variant="destructive" disabled={busy} onClick={() => runAction(() => unassignPmPlan(vehicleId))}>
                {busy ? "REMOVING…" : "CONFIRM UNASSIGN"}
              </ActionButton>
              <ActionButton onClick={() => setConfirmUnassign(false)}>CANCEL</ActionButton>
            </div>
          </div>
        )}
      </Panel>

      {/* sub-tab bar */}
      <div style={{ display: "flex", gap: 2, borderBottom: `1px solid ${colors.border}`, marginBottom: 14, flexWrap: "wrap" }}>
        {SUB_TABS.map((t, i) => (
          <span
            key={t}
            onClick={() => setSubTab(i)}
            style={{
              fontFamily: fonts.body,
              fontWeight: subTab === i ? 600 : 500,
              fontSize: 12,
              padding: "7px 12px",
              color: subTab === i ? colors.headingBright : colors.textDim,
              borderBottom: subTab === i ? `2px solid ${colors.blue}` : undefined,
              marginBottom: -1,
              cursor: "pointer",
              whiteSpace: "nowrap",
            }}
          >
            {t} · {subTabCounts[i]}
          </span>
        ))}
      </div>

      {subTab === 0 && <DueView unit={unit} due={due} onRecord={(code) => setRecording({ code })} />}
      {subTab === 1 && (
        <ScheduleView
          entries={status.entries}
          sysFilter={sysFilter}
          setSysFilter={setSysFilter}
          stateFilter={stateFilter}
          setStateFilter={setStateFilter}
          onRecord={(code) => setRecording({ code })}
        />
      )}
      {subTab === 2 && <OverhaulsView unit={unit} overhauls={overhauls.overhauls} onRecord={(code) => setRecording({ code })} />}
      {subTab === 3 && <HistoryView unit={unit} rows={history} truncated={historyTruncated} onOpenWorkOrders={onOpenWorkOrders} />}

      {recording && (
        <RecordPmCompletionModal
          vehicleId={vehicleId}
          unit={unit}
          currentOdometerKm={status.currentOdometerKm}
          entries={status.entries}
          prefillCode={recording === "general" ? undefined : recording.code}
          onClose={() => setRecording(null)}
          onSaved={() => {
            setRecording(null);
            setReload((n) => n + 1);
          }}
        />
      )}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Shared entry row
// ---------------------------------------------------------------------------

function EntryRow({
  e,
  onRecord,
  metaLine,
}: {
  e: PmEntryStatusWire;
  onRecord: () => void;
  metaLine: string;
}) {
  return (
    <div
      style={{
        display: "grid",
        gridTemplateColumns: "64px 1fr 150px auto",
        gap: 10,
        alignItems: "center",
        padding: "9px 12px",
        marginBottom: 5,
        ...rowSurface(false),
        cursor: "default",
      }}
    >
      <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.skyBlue }}>{e.code}</span>
      <div style={{ minWidth: 0 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 7, minWidth: 0 }}>
          <span
            style={{
              fontFamily: fonts.body,
              fontSize: 12.5,
              fontWeight: 600,
              color: colors.textPrimary,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
            }}
          >
            {e.component}
          </span>
          {e.tier && <MonoTag>{TIER_LABEL[e.tier].toUpperCase()}</MonoTag>}
          {e.kind === "Overhaul" && <MonoTag color={colors.amberText}>{KIND_LABEL.Overhaul.toUpperCase()}</MonoTag>}
        </div>
        <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim }}>{metaLine}</div>
      </div>
      <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-start", gap: 3 }}>
        <StatusChip kind={pmKind(e.state)} label={PM_STATE_LABEL[e.state]} />
        <span style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim }}>{pmDueLabel(e)}</span>
      </div>
      <ActionButton style={smallBtn} onClick={onRecord}>
        RECORD
      </ActionButton>
    </div>
  );
}

function entryMetaLine(e: PmEntryStatusWire): string {
  const parts: string[] = [];
  if (e.task) parts.push(TASK_LABEL[e.task]);
  parts.push(`every ${pmIntervalLabel(e)}`);
  if (e.lastDoneKm != null || e.lastDoneDate) {
    parts.push(`last ${formatKmDate(e.lastDoneKm, e.lastDoneDate)}`);
  } else {
    parts.push("never recorded");
  }
  if (e.nextDueKm != null || e.nextDueDate) {
    parts.push(`next ${formatKmDate(e.nextDueKm, e.nextDueDate)}`);
  }
  parts.push(formatShopMinutes(e.shopMinutes));
  return parts.join(" · ");
}

// ---------------------------------------------------------------------------
// Due for shop visit
// ---------------------------------------------------------------------------

function DueView({
  unit,
  due,
  onRecord,
}: {
  unit: string;
  due: VehiclePmDueWire;
  onRecord: (code: string) => void;
}) {
  const dueCount = due.groups.reduce((n, g) => n + g.entries.length, 0);

  if (dueCount === 0 && due.notYetRecorded.length === 0) {
    return <EmptyTabNote>{`Nothing is due or coming due for ${unit} — no shop visit needed.`}</EmptyTabNote>;
  }

  return (
    <div>
      {dueCount > 0 && (
        <Panel style={{ marginBottom: 12 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 14, flexWrap: "wrap" }}>
            <div>
              <SectionLabel>Estimated shop time — all due items</SectionLabel>
              <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 26, color: colors.headingBright }}>
                {formatShopMinutes(due.totalShopMinutes)}
              </div>
            </div>
            <div style={{ marginLeft: "auto", fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, textAlign: "right" }}>
              {dueCount} item{dueCount === 1 ? "" : "s"} due soon or overdue
              <br />
              bundle into one shop visit where possible
            </div>
          </div>
        </Panel>
      )}

      {due.groups.map((g) => (
        <div key={g.system} style={{ marginBottom: 14 }}>
          <SectionLabel>{g.system}</SectionLabel>
          {g.entries.map((e) => (
            <EntryRow key={e.code} e={e} metaLine={entryMetaLine(e)} onRecord={() => onRecord(e.code)} />
          ))}
        </div>
      ))}

      {due.notYetRecorded.length > 0 && (
        <Panel style={{ marginTop: 4 }}>
          <SectionLabel>Not yet recorded — no due date can be computed</SectionLabel>
          <div style={{ ...dimText, marginBottom: 10 }}>
            These plan lines have never been logged for {unit}. Record the last known service (or do the work
            and log it) so the schedule can track them.
          </div>
          {due.notYetRecorded.map((e) => (
            <EntryRow key={e.code} e={e} metaLine={entryMetaLine(e)} onRecord={() => onRecord(e.code)} />
          ))}
        </Panel>
      )}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Full schedule
// ---------------------------------------------------------------------------

const STATE_FILTER_OPTIONS: { value: "all" | PmDueStateWire; label: string }[] = [
  { value: "all", label: "All states" },
  { value: "Overdue", label: PM_STATE_LABEL.Overdue },
  { value: "DueSoon", label: PM_STATE_LABEL.DueSoon },
  { value: "Ok", label: PM_STATE_LABEL.Ok },
  { value: "NotYetRecorded", label: PM_STATE_LABEL.NotYetRecorded },
];

function ScheduleView({
  entries,
  sysFilter,
  setSysFilter,
  stateFilter,
  setStateFilter,
  onRecord,
}: {
  entries: PmEntryStatusWire[];
  sysFilter: string;
  setSysFilter: (v: string) => void;
  stateFilter: "all" | PmDueStateWire;
  setStateFilter: (v: "all" | PmDueStateWire) => void;
  onRecord: (code: string) => void;
}) {
  const systems = Array.from(new Set(entries.map((e) => e.system)));
  const filtered = entries.filter(
    (e) => (sysFilter === "all" || e.system === sysFilter) && (stateFilter === "all" || e.state === stateFilter),
  );

  return (
    <div>
      {/* Client-side filters — the endpoint returns the whole computed
          schedule (no server paging), so filters are enough; the Pager is
          reserved for server-paged lists. */}
      <div style={{ display: "grid", gridTemplateColumns: "220px 180px 1fr", gap: 10, alignItems: "end", marginBottom: 12 }}>
        <SelectField
          label="System"
          value={sysFilter}
          onChange={setSysFilter}
          options={[{ value: "all", label: "All systems" }, ...systems.map((s) => ({ value: s, label: s }))]}
        />
        <SelectField
          label="State"
          value={stateFilter}
          onChange={(v) => setStateFilter(v as "all" | PmDueStateWire)}
          options={STATE_FILTER_OPTIONS}
        />
        <div style={{ ...dimText, paddingBottom: 11, textAlign: "right" }}>
          {filtered.length} of {entries.length} entries
        </div>
      </div>

      {filtered.length === 0 ? (
        <EmptyTabNote>No schedule entries match the current filters.</EmptyTabNote>
      ) : (
        filtered.map((e) => (
          <EntryRow
            key={e.code}
            e={e}
            metaLine={`${sysFilter === "all" ? `${e.system} · ` : ""}${entryMetaLine(e)}`}
            onRecord={() => onRecord(e.code)}
          />
        ))
      )}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Overhauls — the overhaul-early decision view
// ---------------------------------------------------------------------------

function OverhaulsView({
  unit,
  overhauls,
  onRecord,
}: {
  unit: string;
  overhauls: PmOverhaulStatusWire[];
  onRecord: (code: string) => void;
}) {
  if (overhauls.length === 0) {
    return <EmptyTabNote>{`The assigned plan defines no major-component overhauls for ${unit}.`}</EmptyTabNote>;
  }

  return (
    <div>
      {overhauls.map((o) => (
        <Panel key={o.code} style={{ marginBottom: 12 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap", marginBottom: 6 }}>
            <MonoTag color={colors.skyBlue}>{o.code}</MonoTag>
            <span style={{ fontFamily: fonts.body, fontSize: 13.5, fontWeight: 700, color: colors.headingBright }}>
              {o.component}
            </span>
            <StatusChip kind={pmKind(o.state)} label={PM_STATE_LABEL[o.state]} />
            <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>{pmDueLabel(o)}</span>
            <ActionButton style={{ ...smallBtn, marginLeft: "auto" }} onClick={() => onRecord(o.code)}>
              RECORD
            </ActionButton>
          </div>

          <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginBottom: 8 }}>
            Every {pmIntervalLabel(o)} · {o.labourHours} h labour · parts ≈ {formatCad(o.partsCad)} ·{" "}
            {o.lastDoneKm != null || o.lastDoneDate ? `last ${formatKmDate(o.lastDoneKm, o.lastDoneDate)}` : "never recorded"}
            {o.nextDueKm != null || o.nextDueDate ? ` · next ${formatKmDate(o.nextDueKm, o.nextDueDate)}` : ""}
          </div>

          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary, lineHeight: 1.55, marginBottom: 10 }}>
            {o.scope}
          </div>

          {o.conditionTriggers.length > 0 && (
            <div style={{ marginBottom: 10 }}>
              <SectionLabel>Overhaul-early condition triggers</SectionLabel>
              <ul style={{ margin: 0, paddingLeft: 18 }}>
                {o.conditionTriggers.map((t, i) => (
                  <li key={i} style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, lineHeight: 1.6 }}>
                    {t}
                  </li>
                ))}
              </ul>
            </div>
          )}

          {o.relatedMeasurements.length > 0 && (
            <div
              style={{
                borderTop: `1px solid ${colors.borderSubtle}`,
                paddingTop: 10,
              }}
            >
              <SectionLabel>Condition evidence · latest related test measurements</SectionLabel>
              {o.relatedMeasurements.map((m) => (
                <div
                  key={m.itemCode}
                  style={{
                    display: "grid",
                    gridTemplateColumns: "64px 1fr 170px",
                    gap: 10,
                    alignItems: "center",
                    padding: "6px 2px",
                  }}
                >
                  <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.skyBlue }}>{m.itemCode}</span>
                  <div style={{ minWidth: 0 }}>
                    <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textPrimary, fontWeight: 600 }}>
                      {m.component}
                    </span>
                    <span style={{ fontFamily: fonts.body, fontSize: 12, color: m.measurement ? colors.textSecondary : colors.textDim }}>
                      {" — "}
                      {m.measurement ?? "no measurement logged"}
                    </span>
                  </div>
                  <span style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, textAlign: "right" }}>
                    {m.performedAt
                      ? `${formatDateOnly(m.performedAt)}${m.odometerKm != null ? ` · ${formatKm(m.odometerKm)}` : ""}`
                      : "never logged"}
                  </span>
                </div>
              ))}
            </div>
          )}
        </Panel>
      ))}
    </div>
  );
}

// ---------------------------------------------------------------------------
// History
// ---------------------------------------------------------------------------

function HistoryView({
  unit,
  rows,
  truncated,
  onOpenWorkOrders,
}: {
  unit: string;
  rows: PmCompletionWire[];
  truncated: boolean;
  onOpenWorkOrders: () => void;
}) {
  if (rows.length === 0) {
    return <EmptyTabNote>{`No PM completions logged for ${unit} yet.`}</EmptyTabNote>;
  }

  return (
    <div>
      {truncated && (
        <div style={{ ...dimText, marginBottom: 8 }}>
          Showing the newest {HISTORY_LIMIT.toLocaleString("en-CA")} completions — older entries exist but are not listed.
        </div>
      )}
      {rows.map((c) => (
        <div
          key={c.id}
          style={{
            display: "grid",
            gridTemplateColumns: "92px 64px 1fr 100px auto",
            gap: 10,
            alignItems: "center",
            padding: "9px 12px",
            marginBottom: 5,
            ...rowSurface(false),
            cursor: "default",
          }}
        >
          <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textSecondary }}>
            {formatDateOnly(c.performedAt)}
          </span>
          <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.skyBlue }}>{c.code}</span>
          <div style={{ minWidth: 0 }}>
            <div style={{ display: "flex", alignItems: "center", gap: 7 }}>
              <span style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary }}>
                {c.performedBy}
              </span>
              <MonoTag color={c.kind === "Overhaul" ? colors.amberText : undefined}>{KIND_LABEL[c.kind].toUpperCase()}</MonoTag>
            </div>
            {(c.measurement || c.notes) && (
              <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                {[c.measurement ? `Measured: ${c.measurement}` : null, c.notes].filter(Boolean).join(" · ")}
              </div>
            )}
          </div>
          <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textSecondary, textAlign: "right" }}>
            {formatKm(c.odometerKm)}
          </span>
          {c.workOrderId ? (
            <ActionButton style={smallBtn} onClick={onOpenWorkOrders}>
              WORK ORDER ↗
            </ActionButton>
          ) : (
            <span style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textFaint }}>—</span>
          )}
        </div>
      ))}
    </div>
  );
}
