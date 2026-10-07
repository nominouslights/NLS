"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import type { ServiceCategory } from "@/lib/types";
import { addService } from "@/lib/maintenanceStore";
import { ApiError, changeVehicleStatus, statusLabelFor, type VehicleStatus } from "@/lib/api";
import {
  completeWorkOrder,
  listVehicleDefects,
  type DefectOutcomeInputWire,
  type WorkOrderDefectLineWire,
} from "@/lib/api/maintenance";
import { CATEGORY_WIRE } from "@/lib/workOrderDisplay";
import {
  defectKeyOf,
  remainingBlockingDefects,
  validateDefectOutcomes,
  type DefectOutcomeDraft,
} from "@/lib/workOrderCompletion";
import { ModalShell } from "@/components/ui/ModalShell";
import { NumberField, SelectField, TextAreaField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";
import DefectOutcomesSection from "@/components/fleet/DefectOutcomesSection";

const CATEGORIES: ServiceCategory[] = ["Preventive", "Repair", "Inspection Fix", "Recall"];

/** Statuses from which a completed repair may OFFER a return to service. */
const OFFLINE_STATUSES: VehicleStatus[] = ["OutOfService", "InMaintenance"];

// Log a service record — WHO did the job, WHAT was changed, WHY it was changed.
// When opened from a work order (closeWorkOrder), the live API completes that
// work order and creates the resolving service record in one call; standalone
// (Service History tab), it writes to the mock store as before.
//
// A work order raised against defects also records one outcome per defect.
// Completion NEVER changes the vehicle's status: when the truck is out of
// service / in maintenance and no Major or Out-of-Service defect is left open,
// the modal OFFERS "Return to service" — a dispatcher's click, never automatic.

export default function ServiceRecordModal({
  unit,
  odometerKm,
  closeWorkOrder,
  vehicle,
  onVehicleChanged,
  onClose,
  onSaved,
}: {
  unit: string;
  odometerKm: number;
  closeWorkOrder?: { id: string; number: string; title: string; defects?: WorkOrderDefectLineWire[] };
  /** The vehicle being serviced — only needed for the return-to-service offer. */
  vehicle?: { id: string; status: VehicleStatus };
  /** Called after the offer changed the vehicle's status, so the parent refetches it. */
  onVehicleChanged?: () => void;
  onClose: () => void;
  onSaved: () => void;
}) {
  const lines = closeWorkOrder?.defects ?? [];
  const [performedBy, setPerformedBy] = useState("");
  const [category, setCategory] = useState<ServiceCategory>(closeWorkOrder ? "Repair" : "Preventive");
  const [odo, setOdo] = useState(String(odometerKm));
  const [items, setItems] = useState("");
  const [reason, setReason] = useState("");
  const [parts, setParts] = useState("");
  const [laborHours, setLaborHours] = useState("");
  const [cost, setCost] = useState("");
  const [drafts, setDrafts] = useState<DefectOutcomeDraft[]>(() =>
    lines.map((l) => ({ inspectionId: l.inspectionId, item: l.item, outcome: null, note: "" })),
  );
  const [outcomeIssues, setOutcomeIssues] = useState<Map<string, string>>(new Map());
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // Set once the work order is completed and the return-to-service offer applies.
  const [offer, setOffer] = useState<{ status: VehicleStatus } | null>(null);

  async function submit() {
    if (busy) return;
    const who = performedBy.trim();
    const itemsChanged = items.split("\n").map((s) => s.trim()).filter(Boolean);
    if (!who) return setError("Enter who performed the service.");
    if (itemsChanged.length === 0) return setError("List at least one item that was changed.");
    if (!reason.trim()) return setError("Enter the reason the work was done.");

    let defectOutcomes: DefectOutcomeInputWire[] = [];
    if (lines.length > 0) {
      const checked = validateDefectOutcomes(lines, drafts);
      if (!checked.ok) {
        setOutcomeIssues(new Map(checked.issues.map((i) => [defectKeyOf(i), i.message])));
        return setError(
          `Every defect needs an outcome — ${checked.issues.length} still to fix in “Defect outcomes”.`,
        );
      }
      setOutcomeIssues(new Map());
      defectOutcomes = checked.defectOutcomes;
    }

    const partsUsed = parts
      .split("\n")
      .map((s) => s.trim())
      .filter(Boolean)
      .map((sku) => ({ sku, qty: 1 }));

    if (closeWorkOrder) {
      setBusy(true);
      setError(null);
      try {
        await completeWorkOrder(closeWorkOrder.id, {
          performedBy: who,
          category: CATEGORY_WIRE[category],
          odometerKm: parseInt(odo, 10) || odometerKm,
          itemsChanged,
          reason: reason.trim(),
          partsUsed,
          laborHours: laborHours ? parseFloat(laborHours) : null,
          costCad: cost ? parseFloat(cost) : null,
          // Omitted (not []) when the work order has no defect lines.
          ...(defectOutcomes.length ? { defectOutcomes } : {}),
        });
      } catch (e) {
        setBusy(false);
        setError(e instanceof ApiError ? e.message : "Failed to complete the work order — please try again.");
        return;
      }

      onSaved();
      if (vehicle && OFFLINE_STATUSES.includes(vehicle.status)) {
        // Fail-soft: if the defect list cannot be read, no offer is made — the
        // Overview tab's own "return to service" action is always there.
        try {
          const open = await listVehicleDefects(vehicle.id);
          if (remainingBlockingDefects(open, defectOutcomes).length === 0) {
            setBusy(false);
            setOffer({ status: vehicle.status });
            return;
          }
        } catch (e) {
          console.error("Defects unavailable for the return-to-service check:", e);
        }
      }
      onClose();
      return;
    }

    addService({
      unit,
      date: new Date().toISOString().slice(0, 10),
      performedBy: who,
      category,
      odometerKm: parseInt(odo, 10) || odometerKm,
      itemsChanged,
      reason: reason.trim(),
      partsUsed,
      laborHours: laborHours ? parseFloat(laborHours) : undefined,
      costCad: cost ? parseFloat(cost) : undefined,
    });
    onSaved();
    onClose();
  }

  async function returnToService() {
    if (busy || !vehicle) return;
    setBusy(true);
    setError(null);
    try {
      await changeVehicleStatus(vehicle.id, "Active");
      onVehicleChanged?.();
      onClose();
    } catch (e) {
      setBusy(false);
      setError(e instanceof ApiError ? e.message : "Failed to return the vehicle to service — please try again.");
    }
  }

  if (offer && closeWorkOrder) {
    return (
      <ModalShell
        eyebrow={`Fleet & Maintenance · ${unit} · Work orders`}
        title="Return to Service?"
        onClose={onClose}
        error={error}
        maxWidth={520}
        footer={
          <>
            <ActionButton onClick={onClose} disabled={busy}>
              KEEP {statusLabelFor(offer.status).toUpperCase()}
            </ActionButton>
            <ActionButton variant="success" onClick={returnToService} disabled={busy}>
              {busy ? "UPDATING…" : "RETURN TO SERVICE"}
            </ActionButton>
          </>
        }
      >
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap", marginBottom: 12 }}>
          <StatusChip kind="ontime" label={`${closeWorkOrder.number} completed`} />
          <StatusChip kind="ontime" label="No Major or Out-of-Service defects open" />
        </div>
        <div style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textSecondary, lineHeight: 1.55 }}>
          {unit} is still <b>{statusLabelFor(offer.status).toLowerCase()}</b>. Completing a work order never
          changes a vehicle&apos;s status on its own — return it to service only once the repairs are certified.
        </div>
      </ModalShell>
    );
  }

  return (
    <ModalShell
      eyebrow={`Fleet & Maintenance · ${unit} · Service history`}
      title={closeWorkOrder ? "Complete Work Order — Log Service" : "Log Service"}
      onClose={onClose}
      error={error}
      footer={
        <>
          <ActionButton onClick={onClose} disabled={busy}>
            CANCEL
          </ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {closeWorkOrder ? (busy ? "CLOSING…" : "LOG SERVICE & CLOSE WO") : "LOG SERVICE"}
          </ActionButton>
        </>
      }
    >
      {closeWorkOrder && (
        <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, marginBottom: 14 }}>
          Closing <span style={{ fontFamily: fonts.mono, color: colors.skyBlue }}>{closeWorkOrder.number}</span>
          {closeWorkOrder.title ? ` — ${closeWorkOrder.title}` : ""}.
        </div>
      )}
      {lines.length > 0 && (
        <DefectOutcomesSection
          lines={lines}
          drafts={drafts}
          onChange={setDrafts}
          issues={outcomeIssues}
          disabled={busy}
        />
      )}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <TextField
          label="Performed by (technician / vendor)"
          value={performedBy}
          onChange={setPerformedBy}
          placeholder="M. Cardinal · Thompson shop"
        />
        <SelectField
          label="Category"
          value={category}
          onChange={(v) => setCategory(v as ServiceCategory)}
          options={CATEGORIES.map((c) => ({ value: c, label: c }))}
        />
        <NumberField label="Odometer (km)" value={odo} onChange={setOdo} min={0} step={1} />
        <NumberField label="Labour hours" value={laborHours} onChange={setLaborHours} min={0} step={0.5} placeholder="2.5" />
      </div>
      <div style={{ marginTop: 14, display: "flex", flexDirection: "column", gap: 14 }}>
        <TextAreaField
          label="What was changed"
          value={items}
          onChange={setItems}
          rows={3}
          placeholder={"One item per line\nEngine oil & filter\nAir filter"}
          hint={<span style={{ color: colors.textFaint }}>· one item per line</span>}
        />
        <TextAreaField
          label="Why (reason)"
          value={reason}
          onChange={setReason}
          rows={2}
          placeholder="Scheduled 15,000 km preventive service"
        />
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
          <TextAreaField
            label="Parts used (SKU per line, optional)"
            value={parts}
            onChange={setParts}
            rows={2}
            placeholder={"FLT-OIL-15W40\nFLT-AIRFLT"}
          />
          <NumberField label="Cost (CAD, optional)" value={cost} onChange={setCost} min={0} step={10} placeholder="480" />
        </div>
      </div>
    </ModalShell>
  );
}
