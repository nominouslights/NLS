"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import type { ServiceCategory } from "@/lib/types";
import { addService } from "@/lib/maintenanceStore";
import { ApiError } from "@/lib/api";
import { completeWorkOrder } from "@/lib/api/maintenance";
import { CATEGORY_WIRE } from "@/lib/workOrderDisplay";
import { ModalShell } from "@/components/ui/ModalShell";
import { NumberField, SelectField, TextAreaField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

const CATEGORIES: ServiceCategory[] = ["Preventive", "Repair", "Inspection Fix", "Recall"];

// Log a service record — WHO did the job, WHAT was changed, WHY it was changed.
// When opened from a work order (closeWorkOrder), the live API completes that
// work order and creates the resolving service record in one call; standalone
// (Service History tab), it writes to the mock store as before.

export default function ServiceRecordModal({
  unit,
  odometerKm,
  closeWorkOrder,
  onClose,
  onSaved,
}: {
  unit: string;
  odometerKm: number;
  closeWorkOrder?: { id: string; number: string; title: string };
  onClose: () => void;
  onSaved: () => void;
}) {
  const [performedBy, setPerformedBy] = useState("");
  const [category, setCategory] = useState<ServiceCategory>(closeWorkOrder ? "Repair" : "Preventive");
  const [odo, setOdo] = useState(String(odometerKm));
  const [items, setItems] = useState("");
  const [reason, setReason] = useState("");
  const [parts, setParts] = useState("");
  const [laborHours, setLaborHours] = useState("");
  const [cost, setCost] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (busy) return;
    const who = performedBy.trim();
    const itemsChanged = items.split("\n").map((s) => s.trim()).filter(Boolean);
    if (!who) return setError("Enter who performed the service.");
    if (itemsChanged.length === 0) return setError("List at least one item that was changed.");
    if (!reason.trim()) return setError("Enter the reason the work was done.");

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
        });
      } catch (e) {
        setBusy(false);
        setError(e instanceof ApiError ? e.message : "Failed to complete the work order — please try again.");
        return;
      }
    } else {
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
    }

    onSaved();
    onClose();
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
