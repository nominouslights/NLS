"use client";

import { useEffect, useRef, useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { ApiError, formatUtcDate } from "@/lib/api";
import {
  listVehicleDefects,
  previewDefectDuplicates,
  resolveInspectionDefect,
  type DefectDuplicateWire,
  type DefectResolutionReasonWire,
  type DefectSeverityWire,
  type ResolvedDefect,
  type VehicleDefectWire,
  type WorkOrderStatusWire,
} from "@/lib/api/maintenance";
import { moreSevereDuplicate } from "@/lib/defectDuplicates";
import {
  isVehicleDefectAttachable,
  settledPendingWorkOrders,
  type PendingWorkOrder,
} from "@/lib/inspectionWorkOrder";
import {
  DEFECT_RESOLUTION_LABEL,
  DEFECT_RESOLUTION_META,
  DEFECT_SEVERITY_LABEL,
  DEFECT_SEVERITY_META,
  DEFECT_SEVERITY_RANK,
  DEFECT_WO_KIND,
  WO_STATUS_LABEL,
  clearedWithText,
  worstSeverity,
  type LinkedReport,
} from "@/lib/workOrderDisplay";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { ModalShell } from "@/components/ui/ModalShell";
import { SelectField, TextAreaField } from "@/components/ui/Field";
import RetiredKeyNote from "@/components/inspection/RetiredKeyNote";
import { notifyDefectsChanged, useDefectsChanged } from "@/lib/defectSync";

// One component, four surfaces: the trip detail, the create-trip wizard, the
// vehicle's Open Defects tab, and (via ResolveDefectModal alone) the inspection
// detail. Shows a vehicle's unrepaired defects wherever a dispatcher is looking
// at that vehicle in the context of a trip — what, how bad, when it was found,
// by whom, whether a repair is underway, and whether the fault has come back
// after being cleared before.
//
// WARN ONLY. There is deliberately no callback reporting "this vehicle has
// defects" to a parent, so no mount point can grow into a block on trip
// creation or vehicle assignment without a considered prop change. The
// cross-surface seam (lib/defectSync) keeps that true: this panel LISTENS for
// "defect state changed somewhere" and refetches — the event carries no
// vehicle, no count, nothing a caller could gate on.
//
// `onCreateWorkOrder` is an ACTION, not a report: when passed (the vehicle's
// Open Defects tab only — never the trip detail), every open row with no open
// work order gets a CREATE WORK ORDER button, plus a checkbox so several open
// defects can go on one work order. A defect is on at most one open work order.

/** No defect id exists — a row is addressed by (inspectionId, item). */
const defectKey = (d: { inspectionId: string; item: string }) => `${d.inspectionId}:${d.item}`;

export default function DefectsPanel({
  vehicleId,
  unit,
  variant = "panel",
  title,
  maxRows = null,
  canResolve = false,
  showResolvedToggle = false,
  refreshKey = 0,
  onReReport,
  onCreateWorkOrder,
  pendingWorkOrders,
  onPendingWorkOrdersSettled,
}: {
  vehicleId: string | null;
  unit?: string | null;
  variant?: "panel" | "inline";
  title?: string;
  maxRows?: number | null;
  canResolve?: boolean;
  showResolvedToggle?: boolean;
  refreshKey?: number;
  onReReport?: (defect: VehicleDefectWire) => void;
  /** Raise one work order against the given open, unattached rows (same vehicle). */
  onCreateWorkOrder?: (rows: VehicleDefectWire[]) => void;
  /** Rows (keyed `inspectionId:item`) a work order was just created for, while
   *  the read model catches up — shown as "work order created", never re-offered. */
  pendingWorkOrders?: ReadonlyMap<string, PendingWorkOrder>;
  /** After every load, the pending marks that load settled (see
   *  settledPendingWorkOrders) — the parent drops them. Reports only keys the
   *  parent itself passed in, never "this vehicle has defects". */
  onPendingWorkOrdersSettled?: (keys: string[]) => void;
}) {
  const [selected, setSelected] = useState<ReadonlySet<string>>(new Set());
  const [includeResolved, setIncludeResolved] = useState(false);
  const [reload, setReload] = useState(0);
  // Both the rows and any error are tagged with the vehicle they came from, so
  // reassigning a trip never paints the old truck's defects — or the old
  // truck's failure — under the new truck's name while the new fetch is still
  // in flight. Tagging also means nothing has to be cleared on a vehicle
  // change: a stale result simply stops matching.
  const [loadError, setLoadError] = useState<{ vehicleId: string; message: string } | null>(null);
  const [fetched, setFetched] = useState<{ vehicleId: string; rows: VehicleDefectWire[] } | null>(null);
  const [resolving, setResolving] = useState<VehicleDefectWire | null>(null);
  // The load effect must not re-run when the marks change, so it reads the
  // latest marks and callback through a ref.
  const pendingRef = useRef({ pendingWorkOrders, onPendingWorkOrdersSettled });
  useEffect(() => {
    pendingRef.current = { pendingWorkOrders, onPendingWorkOrdersSettled };
  });

  useEffect(() => {
    // No vehicle on the trip yet — no request is issued at all.
    if (!vehicleId) return;
    let active = true;
    listVehicleDefects(vehicleId, includeResolved).then(
      (rows) => {
        if (active) {
          setFetched({ vehicleId, rows });
          setLoadError(null);
          const { pendingWorkOrders: marks, onPendingWorkOrdersSettled: settle } = pendingRef.current;
          if (marks && settle) {
            const settled = settledPendingWorkOrders(marks, rows, Date.now(), defectKey);
            if (settled.length) settle(settled);
          }
        }
      },
      (e) => {
        if (active) {
          setLoadError({
            vehicleId,
            message: e instanceof ApiError ? e.message : "Failed to load defects.",
          });
        }
      },
    );
    return () => {
      active = false;
    };
  }, [vehicleId, includeResolved, reload, refreshKey]);

  // A defect changed anywhere — here, the inspection detail, a work order, an
  // inspection entered or amended — so re-read once the projector has run
  // (PROJECTION_RECONCILE_MS after the last change; never immediately). This is
  // also this panel's own reconcile after a resolve: ResolveDefectModal
  // announces the change, so one resolve means exactly one reload.
  useDefectsChanged(() => setReload((n) => n + 1));

  function onResolved(resolved: readonly ResolvedDefect[]) {
    // Optimistic: the projection has not caught up yet — the reconcile reload
    // comes from useDefectsChanged above. Exactly the rows the response lists
    // drop — the addressed one, plus any duplicates a repair actually cleared —
    // and nothing the client merely previewed.
    const cleared = new Set(resolved.map(defectKey));
    setFetched((prev) => (prev ? { ...prev, rows: prev.rows.filter((r) => !cleared.has(defectKey(r))) } : prev));
  }

  // Rows arrive PRE-ORDERED (OutOfService → Major → Minor, newest first within
  // each). Never re-sort them — the server's order is the safety order.
  const rows = fetched?.vehicleId === vehicleId ? fetched.rows : null;
  const open = rows?.filter((r) => r.resolvedAtUtc === null) ?? [];
  const shown = rows === null ? null : maxRows == null ? rows : rows.slice(0, maxRows);
  const hidden = rows === null ? 0 : rows.length - (shown?.length ?? 0);

  const heading = title ?? "Open defects";

  // The report a duplicate was cleared with, from the rows already loaded (with
  // SHOW RESOLVED on, the resolved report is among them) — never fetched per row.
  const linkedReportOf = (inspectionId: string): LinkedReport | undefined => {
    const r = rows?.find((x) => x.inspectionId === inspectionId);
    return r ? { tripNumber: r.tripNumber, at: r.reportedAt } : undefined;
  };

  const canCreate = (d: VehicleDefectWire) =>
    !!onCreateWorkOrder && isVehicleDefectAttachable(d) && !pendingWorkOrders?.has(defectKey(d));
  // Selection is derived against the CURRENT rows, so a stale key (another
  // vehicle, a row since attached or resolved) silently drops out.
  const selectedRows = (rows ?? []).filter((r) => selected.has(defectKey(r)) && canCreate(r));
  const attachableCount = (shown ?? []).filter(canCreate).length;

  function toggle(d: VehicleDefectWire) {
    const key = defectKey(d);
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  function create(rowsToAttach: VehicleDefectWire[]) {
    if (!onCreateWorkOrder || rowsToAttach.length === 0) return;
    onCreateWorkOrder(rowsToAttach);
    setSelected(new Set());
  }

  const body = (
    <>
      <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 10, flexWrap: "wrap" }}>
        <SectionLabel>{heading}</SectionLabel>
        {rows !== null && rows.length > 0 && (
          <div style={{ marginBottom: 11 }}>
            {/* All-clear is stated as plainly as a problem — teal + ✓ + words,
                never the absence of a chip. */}
            {open.length > 0 ? (
              <SummaryChip open={open} />
            ) : (
              <StatusChip kind="ontime" label={`No open defects${unit ? ` on ${unit}` : ""}`} />
            )}
          </div>
        )}
        {showResolvedToggle && vehicleId && (
          <div style={{ marginLeft: "auto", marginBottom: 11 }}>
            <ActionButton
              variant={includeResolved ? "primary" : "secondary"}
              onClick={() => setIncludeResolved((v) => !v)}
              style={{ fontSize: 11.5, padding: "4px 10px" }}
            >
              {includeResolved ? "HIDE RESOLVED" : "SHOW RESOLVED"}
            </ActionButton>
          </div>
        )}
      </div>

      {!vehicleId ? (
        <div style={dim}>No vehicle assigned — defects are listed once a unit is on this trip.</div>
      ) : loadError?.vehicleId === vehicleId ? (
        // Fail soft: one inline line, never a modal and never anything that
        // could read as a block on the trip.
        <div style={{ ...dim, color: statusMeta("over").t, fontWeight: 600 }}>▲ {loadError.message}</div>
      ) : shown === null ? (
        <div style={dim}>Loading defects…</div>
      ) : shown.length === 0 ? (
        <StatusChip kind="ontime" label={`No open defects${unit ? ` on ${unit}` : ""}`} />
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: 7 }}>
          {onCreateWorkOrder && attachableCount > 1 && (
            <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
              <span style={dim}>
                Tick several open defects to put them on one work order.
              </span>
              <ActionButton
                variant="primary"
                disabled={selectedRows.length === 0}
                onClick={() => create(selectedRows)}
                style={{ marginLeft: "auto", fontSize: 11.5, padding: "4px 10px" }}
              >
                CREATE WORK ORDER FOR {selectedRows.length} SELECTED
              </ActionButton>
            </div>
          )}
          {shown.map((d) => (
            <DefectRow
              key={defectKey(d)}
              defect={d}
              canResolve={canResolve}
              onResolve={() => setResolving(d)}
              onReReport={onReReport}
              linkedReportOf={linkedReportOf}
              pendingWorkOrder={isVehicleDefectAttachable(d) && !!pendingWorkOrders?.has(defectKey(d))}
              onCreateWorkOrder={canCreate(d) ? () => create([d]) : undefined}
              selectable={canCreate(d) && attachableCount > 1}
              selected={selected.has(defectKey(d))}
              onToggle={() => toggle(d)}
            />
          ))}
          {hidden > 0 && (
            <div style={dim}>
              +{hidden} more on this unit — open the vehicle&apos;s Open Defects tab for the full list.
            </div>
          )}
        </div>
      )}

      {resolving && (
        <ResolveDefectModal
          inspectionId={resolving.inspectionId}
          item={resolving.item}
          unit={resolving.unit}
          severity={resolving.severity}
          onClose={() => setResolving(null)}
          onResolved={(resolved) => {
            onResolved(resolved);
            setResolving(null);
          }}
        />
      )}
    </>
  );

  return variant === "panel" ? <Panel style={{ marginBottom: 12 }}>{body}</Panel> : <div>{body}</div>;
}

const dim = {
  fontFamily: fonts.body,
  fontSize: 12.5,
  color: colors.textDim,
  lineHeight: 1.5,
} as const;

/** Counts and words, driven by the worst severity present — never a bare dot. */
function SummaryChip({ open }: { open: VehicleDefectWire[] }) {
  const worst = worstSeverity(open);
  if (worst === null) return null;
  const counts = open.reduce<Partial<Record<DefectSeverityWire, number>>>((acc, d) => {
    acc[d.severity] = (acc[d.severity] ?? 0) + 1;
    return acc;
  }, {});
  // The label's own word order (worst first) — the severity KEYS are ordered,
  // never the rows.
  const severities = (Object.keys(counts) as DefectSeverityWire[]).sort(
    (a, b) => DEFECT_SEVERITY_RANK[a] - DEFECT_SEVERITY_RANK[b],
  );
  const meta = DEFECT_SEVERITY_META[worst];
  const label = severities.map((s) => `${counts[s]} ${DEFECT_SEVERITY_LABEL[s].toLowerCase()}`).join(" · ");
  return <StatusChip kind={meta.kind} glyph={meta.glyph} label={label} />;
}

function DefectRow({
  defect: d,
  canResolve,
  onResolve,
  onReReport,
  linkedReportOf,
  pendingWorkOrder = false,
  onCreateWorkOrder,
  selectable = false,
  selected = false,
  onToggle,
}: {
  defect: VehicleDefectWire;
  canResolve: boolean;
  onResolve: () => void;
  onReReport?: (defect: VehicleDefectWire) => void;
  linkedReportOf?: (inspectionId: string) => LinkedReport | undefined;
  pendingWorkOrder?: boolean;
  onCreateWorkOrder?: () => void;
  selectable?: boolean;
  selected?: boolean;
  onToggle?: () => void;
}) {
  const meta = DEFECT_SEVERITY_META[d.severity] ?? DEFECT_SEVERITY_META.Major;
  const resolved = d.resolvedAtUtc !== null;
  const woStatus = d.workOrderStatus as WorkOrderStatusWire | null;
  const resolutionMeta = DEFECT_RESOLUTION_META[d.resolutionReason as DefectResolutionReasonWire] ?? { kind: "ontime" as const };
  const hasActions = (canResolve && !resolved) || (resolved && onReReport) || onCreateWorkOrder;

  return (
    <div
      style={{
        padding: "10px 12px",
        borderRadius: 9,
        border: `1px solid ${resolved ? colors.borderSubtle : statusMeta(meta.kind).bd}`,
        background: resolved ? colors.cardBg : statusMeta(meta.kind).bg,
      }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: 9, flexWrap: "wrap" }}>
        {selectable && (
          <input
            type="checkbox"
            checked={selected}
            onChange={onToggle}
            aria-label={`Select ${d.item} for a work order`}
            style={{ width: 16, height: 16, margin: 0, cursor: "pointer", accentColor: colors.blue }}
          />
        )}
        <StatusChip kind={meta.kind} glyph={meta.glyph} label={DEFECT_SEVERITY_LABEL[d.severity]} />
        <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 700, color: colors.headingBright }}>
          {d.item}
        </span>
        {d.tripNumber && <MonoTag color={colors.skyBlue}>{d.tripNumber}</MonoTag>}
        {pendingWorkOrder && !resolved ? (
          <StatusChip kind="soon" label="Work order created · updating" />
        ) : woStatus && d.workOrderNumber ? (
          <StatusChip
            kind={DEFECT_WO_KIND[woStatus] ?? "info"}
            label={`${d.workOrderNumber} · ${WO_STATUS_LABEL[woStatus] ?? woStatus}`}
          />
        ) : (
          !resolved && <MonoTag>no work order</MonoTag>
        )}
        {hasActions ? (
          <span style={{ marginLeft: "auto", display: "flex", gap: 6 }}>
            {onCreateWorkOrder && (
              <ActionButton onClick={onCreateWorkOrder} style={{ fontSize: 11.5, padding: "4px 10px" }}>
                CREATE WORK ORDER
              </ActionButton>
            )}
            {canResolve && !resolved && (
              <ActionButton onClick={onResolve} style={{ fontSize: 11.5, padding: "4px 10px" }}>
                RESOLVE
              </ActionButton>
            )}
            {resolved && onReReport && (
              <ActionButton
                onClick={() => onReReport(d)}
                style={{ fontSize: 11.5, padding: "4px 10px" }}
              >
                RE-REPORT
              </ActionButton>
            )}
          </span>
        ) : null}
      </div>

      {/* Display only — the defect stays addressed (and is resolved / re-reported)
          by d.item exactly as stored, even when rev 3 retired that row. */}
      <RetiredKeyNote item={d.item} />

      {d.note && (
        <div style={{ ...dim, color: colors.textMuted, marginTop: 5 }}>{d.note}</div>
      )}

      {/* the "when and by whom" */}
      <div style={{ ...dim, fontSize: 11.5, marginTop: 4 }}>
        {d.inspectionType === "PreTrip" ? "Pre-Trip" : "Post-Trip"} · {d.driverName} ·{" "}
        {formatUtcDate(d.reportedAt)}
      </div>

      {d.recurrence && <RecurrenceBanner recurrence={d.recurrence} />}

      {resolved && (
        <div style={{ marginTop: 7 }}>
          <StatusChip
            kind={resolutionMeta.kind}
            glyph={resolutionMeta.glyph}
            label={`${reasonLabel(d.resolutionReason)} · ${d.resolvedBy ?? "Dispatch"} · ${formatUtcDate(d.resolvedAtUtc)}`}
          />
          {d.resolvedWithInspectionId != null && (
            <span style={{ ...dim, fontSize: 11.5, marginLeft: 8 }}>
              · {clearedWithText(linkedReportOf?.(d.resolvedWithInspectionId), d.reportedAt)}
            </span>
          )}
          {d.resolutionNote && <div style={{ ...dim, marginTop: 4 }}>{d.resolutionNote}</div>}
        </div>
      )}
    </div>
  );
}

/** "This has come back." Gold caution + icon + the full sentence — the chip
 *  primitive is nowrap, so this wraps in its own banner rather than running off
 *  the edge of the inline variant. */
function RecurrenceBanner({ recurrence }: { recurrence: NonNullable<VehicleDefectWire["recurrence"]> }) {
  const m = statusMeta("soon");
  return (
    <div
      style={{
        display: "flex",
        gap: 8,
        alignItems: "flex-start",
        marginTop: 7,
        padding: "7px 10px",
        borderRadius: 8,
        background: m.bg,
        border: `1px solid ${m.bd}`,
      }}
    >
      <span
        style={{
          width: 16,
          height: 16,
          flex: "none",
          borderRadius: 4,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          fontSize: 10,
          fontWeight: 800,
          background: m.c,
          color: m.bt,
        }}
      >
        {m.g}
      </span>
      <span style={{ fontFamily: fonts.body, fontSize: 11.5, fontWeight: 600, color: m.t, lineHeight: 1.45 }}>
        {recurrence.explicit ? "Re-reported by dispatch" : "Reported before"} — resolved{" "}
        {formatUtcDate(recurrence.resolvedAtUtc)} as {reasonLabel(recurrence.resolutionReason)}
        {recurrence.workOrderNumber ? `, ${recurrence.workOrderNumber}` : ""}
      </span>
    </div>
  );
}

/** The reason travels as its backend enum name; an unmapped value prints as-is
 *  rather than vanishing. */
function reasonLabel(reason: string | null): string {
  if (!reason) return "Resolved";
  return DEFECT_RESOLUTION_LABEL[reason as DefectResolutionReasonWire] ?? reason;
}

/** The three reasons a dispatcher may choose. RepairedUnderWorkOrder and
 *  NoFaultFound are absent deliberately: they are stamped by work-order
 *  completion, where a mechanic actually touched the truck, and picking either
 *  by hand would forge that. */
const MANUAL_REASONS: DefectResolutionReasonWire[] = [
  "PreviouslyRepaired",
  "ReportedInError",
  "AcceptedMonitoring",
];

/** Only a REPAIR clears the other open reports of a component — the same fault
 *  fixed once is fixed for every report of it. "Reported in error" and
 *  "Accepted, monitoring" are judgements about ONE report and clear only that
 *  one; the server rejects a clear list sent with them (400). */
const CLEARS_DUPLICATES: ReadonlySet<DefectResolutionReasonWire> = new Set(["PreviouslyRepaired"]);

/** How long the duplicate preview may take before the modal stops waiting for
 *  it and says it could not check. The preview must never block a resolve. */
export const RESOLVE_PREVIEW_TIMEOUT_MS = 8000;

type Preview = { status: "loading" } | { status: "error" } | { status: "ready"; duplicates: DefectDuplicateWire[] };

/**
 * Clears one defect, addressed by (inspectionId, item) — there is no defect id.
 * Before the confirm it asks the server which OTHER open reports of the same
 * component exist (`previewDefectDuplicates`, read from the write model with the
 * cascade's own scope — never matched client-side from the lagging projection),
 * and says what the chosen reason will do with them:
 *  - Previously repaired: clears them too. The modal lists them, says plainly
 *    when that includes a more severe report (an Out-of-Service report is never
 *    cleared silently), puts the count on the confirm, and sends EXACTLY the
 *    previewed list as `clearDuplicates` — so the server never clears a report
 *    the dispatcher was not shown.
 *  - Reported in error / Accepted, monitoring: clears this report only, and says
 *    the others stay open. Sends `clearDuplicates: null`.
 * If the preview fails or times out it says so, sends null (this report only),
 * and the resolve stays available — it is a preview, never a gate.
 *
 * Exported on its own so InspectionDetailModal can offer a per-defect resolve
 * beside its "create work order" action without mounting the whole panel.
 */
export function ResolveDefectModal({
  inspectionId,
  item,
  unit,
  severity,
  onClose,
  onResolved,
}: {
  inspectionId: string;
  item: string;
  unit: string;
  severity?: DefectSeverityWire;
  onClose: () => void;
  /** Every defect the resolve ACTUALLY cleared, as the server lists it: the
   *  addressed one first, then any duplicates. */
  onResolved: (resolved: ResolvedDefect[]) => void;
}) {
  const [reason, setReason] = useState("");
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [preview, setPreview] = useState<Preview>({ status: "loading" });

  useEffect(() => {
    let active = true;
    // Settle the preview once: whichever comes first — the answer, a failure, or
    // the timeout — is what the dispatcher sees. A late answer after the timeout
    // is dropped, so the confirm's count never changes under their cursor and
    // nothing is cleared that the modal did not show.
    const settle = (next: Preview) => {
      if (active) setPreview((p) => (p.status === "loading" ? next : p));
    };
    const timer = setTimeout(() => settle({ status: "error" }), RESOLVE_PREVIEW_TIMEOUT_MS);
    previewDefectDuplicates(inspectionId, item).then(
      (duplicates) => settle({ status: "ready", duplicates }),
      () => settle({ status: "error" }),
    );
    return () => {
      active = false;
      clearTimeout(timer);
    };
  }, [inspectionId, item]);

  // Server order (oldest first) — shown as-is, never re-sorted.
  const duplicates = preview.status === "ready" ? preview.duplicates : [];
  const repair = reason !== "" && CLEARS_DUPLICATES.has(reason as DefectResolutionReasonWire);
  const clears = repair ? 1 + duplicates.length : 1;
  const checking = preview.status === "loading";

  async function submit() {
    if (busy || checking) return;
    if (!reason) return setError("Choose why this defect is being cleared.");
    setBusy(true);
    setError(null);
    try {
      const resolved = await resolveInspectionDefect(inspectionId, {
        item,
        reason: reason as DefectResolutionReasonWire,
        note: note.trim() || null,
        resolvedBy: "Dispatch",
        // Exactly what was shown — and only for a repair, only when the preview
        // actually answered. Anything else clears this report alone.
        clearDuplicates:
          repair && preview.status === "ready"
            ? preview.duplicates.map((d) => ({ inspectionId: d.inspectionId, item: d.item }))
            : null,
      });
      onResolved(resolved);
      // The one emit for every resolve, whichever surface opened this modal
      // (DefectsPanel or InspectionDetailModal) — after the parent's own
      // optimistic update, so listeners schedule their re-read from here.
      notifyDefectsChanged();
    } catch (e) {
      setBusy(false);
      setError(e instanceof ApiError ? e.message : "Failed to resolve the defect — please try again.");
    }
  }

  return (
    <ModalShell
      eyebrow={`Fleet & Maintenance · ${unit} · DVIR`}
      title="Resolve Defect"
      onClose={onClose}
      error={error}
      maxWidth={560}
      footer={
        <>
          <ActionButton onClick={onClose} disabled={busy}>
            CANCEL
          </ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy || checking}>
            {busy
              ? "RESOLVING…"
              : checking
                ? "CHECKING…"
                : clears > 1
                  ? `RESOLVE ${clears} REPORTS`
                  : "RESOLVE DEFECT"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "flex", alignItems: "center", gap: 9, marginBottom: 14, flexWrap: "wrap" }}>
        {severity && (
          <StatusChip
            kind={DEFECT_SEVERITY_META[severity].kind}
            glyph={DEFECT_SEVERITY_META[severity].glyph}
            label={DEFECT_SEVERITY_LABEL[severity]}
          />
        )}
        <span style={{ fontFamily: fonts.body, fontSize: 13.5, fontWeight: 700, color: colors.headingBright }}>
          {item}
        </span>
      </div>

      <DuplicatePreview
        preview={preview}
        reason={reason === "" ? null : repair ? "repair" : "single"}
        addressedSeverity={severity}
        item={item}
        unit={unit}
      />

      <div style={{ ...dim, marginBottom: 16 }}>
        Resolution is final — there is no reopen. If this fault comes back, it is recorded as a new
        defect on a later DVIR that cites this one.
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
        <SelectField
          label="Reason"
          value={reason}
          onChange={setReason}
          options={[
            { value: "", label: "— select a reason —" },
            ...MANUAL_REASONS.map((r) => ({ value: r, label: DEFECT_RESOLUTION_LABEL[r] })),
          ]}
          hint={<span style={{ color: colors.textFaint }}>· required</span>}
        />
        <TextAreaField
          label="Note (optional)"
          value={note}
          onChange={setNote}
          rows={3}
          placeholder="What was done, or why this is not a fault"
        />
      </div>
    </ModalShell>
  );
}

/** "a Major" / "an Out-of-Service" — the article a severity label takes. */
function withArticle(severity: DefectSeverityWire): string {
  const label = DEFECT_SEVERITY_LABEL[severity] ?? severity;
  return `${/^[aeiou]/i.test(label) ? "an" : "a"} ${label}`;
}

const plural = (n: number, one: string, many: string) => (n === 1 ? one : many);

/**
 * What the chosen reason will do with the other open reports, shown before the
 * dispatcher confirms. `reason` is null until one is picked (a neutral count),
 * "repair" for a reason that clears them too, "single" for one that does not.
 */
function DuplicatePreview({
  preview,
  reason,
  addressedSeverity,
  item,
  unit,
}: {
  preview: Preview;
  reason: "repair" | "single" | null;
  addressedSeverity: DefectSeverityWire | undefined;
  item: string;
  unit: string;
}) {
  if (preview.status === "loading") {
    return <div style={{ ...dim, marginBottom: 12 }}>Checking for other open reports of {item} on {unit}…</div>;
  }
  if (preview.status === "error") {
    // Honest, and never a block: with nothing checked, nothing else is cleared.
    const m = statusMeta("soon");
    return (
      <div data-testid="duplicate-preview" style={{ ...dim, color: m.t, fontWeight: 600, marginBottom: 12 }}>
        {m.g} Couldn&apos;t check for other reports — only this report will be cleared.
      </div>
    );
  }
  const duplicates = preview.duplicates;
  if (duplicates.length === 0) {
    return (
      <div data-testid="duplicate-preview" style={{ ...dim, marginBottom: 12 }}>
        No other open reports of {item} on {unit} — this clears this report only.
      </div>
    );
  }

  const n = duplicates.length;
  const others = `${n} other open ${plural(n, "report", "reports")} of ${item} on ${unit}`;
  const moreSevere = reason === "repair" ? moreSevereDuplicate(duplicates, addressedSeverity) : null;
  const sm = moreSevere ? DEFECT_SEVERITY_META[moreSevere] : null;
  return (
    <div
      data-testid="duplicate-preview"
      style={{
        marginBottom: 14,
        padding: "10px 12px",
        borderRadius: 9,
        border: `1px solid ${colors.borderSubtle}`,
        background: colors.cardBg,
      }}
    >
      <div style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 700, color: colors.textPrimary }}>
        {reason === "repair"
          ? `This also clears ${others}`
          : reason === "single"
            ? `Only this report is cleared — ${others} ${plural(n, "stays", "stay")} open`
            : `${others[0].toUpperCase()}${others.slice(1)}`}
      </div>
      {reason === null && (
        <div style={{ ...dim, fontSize: 12, marginTop: 4 }}>
          Previously repaired clears {plural(n, "it", "them")} with this one; the other reasons clear only this
          report.
        </div>
      )}
      {moreSevere && sm && (
        // Plain words on a line of their own: a repair never clears a worse
        // report silently.
        <div
          style={{ fontFamily: fonts.body, fontSize: 12, fontWeight: 700, color: statusMeta(sm.kind).t, marginTop: 5 }}
        >
          {sm.glyph ?? statusMeta(sm.kind).g} Includes {withArticle(moreSevere)} report
          {addressedSeverity
            ? ` — more severe than the ${DEFECT_SEVERITY_LABEL[addressedSeverity]} one being resolved`
            : ""}
          .
        </div>
      )}
      <div style={{ display: "flex", flexDirection: "column", gap: 5, marginTop: 8 }}>
        {duplicates.map((d) => {
          const meta = DEFECT_SEVERITY_META[d.severity] ?? DEFECT_SEVERITY_META.Major;
          return (
            <div
              key={defectKey(d)}
              data-testid="duplicate-report"
              style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}
            >
              <StatusChip kind={meta.kind} glyph={meta.glyph} label={DEFECT_SEVERITY_LABEL[d.severity] ?? d.severity} />
              {d.tripNumber ? <MonoTag color={colors.skyBlue}>{d.tripNumber}</MonoTag> : <MonoTag>no trip</MonoTag>}
              <span style={{ ...dim, fontSize: 11.5 }}>reported {formatUtcDate(d.reportedAt)}</span>
            </div>
          );
        })}
      </div>
    </div>
  );
}
