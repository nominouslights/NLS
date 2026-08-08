"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  createWorkOrder,
  listShops,
  type ShopWire,
  type WorkOrderPriorityWire,
} from "@/lib/api/maintenance";
import type { WorkOrderPrefillWire } from "@/lib/inspectionWorkOrder";
import { toUtcIso, WO_SOURCE_LABEL } from "@/lib/workOrderDisplay";
import type { VehicleOption } from "@/components/screens/fleet/vehicle-detail/shared";
import { ModalShell } from "@/components/ui/ModalShell";
import { NumberField, SelectField, TextAreaField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

const PRIORITIES: WorkOrderPriorityWire[] = ["Low", "Medium", "High", "Critical"];

// Create a work order — manually from the interface, or prefilled from a
// pre/post-trip inspection's defects (WorkOrderPrefillWire, which links the WO
// back to the inspection server-side). A registered shop can be attached so its
// details auto-fill the printable NL-WO-01 work order.

export default function WorkOrderModal({
  vehicles,
  defaultVehicleId,
  prefill,
  onClose,
  onSaved,
}: {
  vehicles: VehicleOption[];
  defaultVehicleId?: string;
  prefill?: WorkOrderPrefillWire;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [vehicleId, setVehicleId] = useState(defaultVehicleId ?? vehicles[0]?.id ?? "");
  const [title, setTitle] = useState(prefill?.title ?? "");
  const [priority, setPriority] = useState<WorkOrderPriorityWire>(prefill?.priority ?? "Medium");
  const [description, setDescription] = useState(prefill?.description ?? "");
  const [lineItems, setLineItems] = useState((prefill?.lineItems ?? []).join("\n"));
  const [shops, setShops] = useState<ShopWire[]>([]);
  const [shopId, setShopId] = useState("");
  const [limit, setLimit] = useState("");
  const [budgetCode, setBudgetCode] = useState("");
  const [assignedTo, setAssignedTo] = useState("");
  const [dueDate, setDueDate] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    // Registered shops for the §2 select. Fail-soft: the select just stays "— none —".
    let active = true;
    listShops().then(
      (rows) => {
        if (active) setShops(rows);
      },
      (e) => {
        console.error("Shops unavailable:", e);
      },
    );
    return () => {
      active = false;
    };
  }, []);

  const fromInspection = !!prefill?.inspectionId;
  const sourceLabel = prefill ? WO_SOURCE_LABEL[prefill.source] : "Manual";
  const shopOptions = [{ value: "", label: "— none —" }, ...shops.map((s) => ({ value: s.id, label: s.name }))];
  const vehicleOptions = vehicles.map((v) => ({ value: v.id, label: v.label }));

  async function submit() {
    if (busy) return;
    if (!vehicleId) return setError("Select the vehicle this work order is for.");
    if (!title.trim()) return setError("Enter a work order title.");

    const shop = shops.find((s) => s.id === shopId);
    setBusy(true);
    setError(null);
    try {
      await createWorkOrder({
        vehicleId,
        title: title.trim(),
        description: description.trim() || null,
        priority,
        source: prefill?.source ?? "Manual",
        sourceRef: prefill?.sourceRef ?? null,
        assignedTo: assignedTo.trim() || shop?.name || null,
        dueDate: dueDate.trim() ? toUtcIso(dueDate.trim()) : null,
        lineItems: lineItems.split("\n").map((s) => s.trim()).filter(Boolean),
        shopId: shopId || null,
        authorizedLimitCad: limit ? parseFloat(limit) : null,
        budgetCode: budgetCode.trim() || null,
        dateRequiredOrOos: dueDate.trim() ? toUtcIso(dueDate.trim()) : null,
        inspectionId: prefill?.inspectionId ?? null,
      });
      onSaved();
      onClose();
    } catch (e) {
      setBusy(false);
      setError(e instanceof ApiError ? e.message : "Failed to create the work order — please try again.");
    }
  }

  return (
    <ModalShell
      eyebrow={`Fleet & Maintenance · Work orders${prefill && prefill.source !== "Manual" ? ` · from ${sourceLabel}` : ""}`}
      title="New Work Order"
      onClose={onClose}
      error={error}
      footer={
        <>
          <ActionButton onClick={onClose} disabled={busy}>
            CANCEL
          </ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "CREATING…" : "CREATE WORK ORDER"}
          </ActionButton>
        </>
      }
    >
      {prefill && prefill.source !== "Manual" && (
        <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, marginBottom: 14 }}>
          Generated from <span style={{ fontWeight: 600 }}>{sourceLabel}</span>
          {prefill.sourceRef ? (
            <>
              {" "}
              <span style={{ fontFamily: fonts.mono, color: colors.skyBlue }}>{prefill.sourceRef}</span>
            </>
          ) : null}
          . Defects were carried over as line items below.
        </div>
      )}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <SelectField
          label="Vehicle"
          value={vehicleId}
          onChange={setVehicleId}
          disabled={fromInspection}
          options={vehicleOptions.length ? vehicleOptions : [{ value: vehicleId, label: vehicleId || "—" }]}
        />
        <SelectField
          label="Priority"
          value={priority}
          onChange={(v) => setPriority(v as WorkOrderPriorityWire)}
          options={PRIORITIES.map((p) => ({ value: p, label: p }))}
        />
        <SelectField
          label="Repair shop / partner"
          value={shopId}
          onChange={setShopId}
          options={shopOptions}
          hint={<span style={{ color: colors.textFaint }}>· fills §2 of the printed work order</span>}
        />
        <NumberField
          label="Approved not to exceed (CAD)"
          value={limit}
          onChange={setLimit}
          min={0}
          step={50}
          placeholder="1500"
        />
      </div>
      <div style={{ marginTop: 14, display: "flex", flexDirection: "column", gap: 14 }}>
        <TextField label="Title" value={title} onChange={setTitle} placeholder="Front brake inspection & measure" />
        <TextAreaField label="Description" value={description} onChange={setDescription} rows={2} placeholder="What needs to happen and why" />
        <TextAreaField
          label="Line items / tasks"
          value={lineItems}
          onChange={setLineItems}
          rows={3}
          placeholder={"One task per line\nInspect front brakes\nMeasure pad thickness"}
          hint={<span style={{ color: colors.textFaint }}>· one task per line</span>}
        />
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 14 }}>
          <TextField label="Assigned to (optional)" value={assignedTo} onChange={setAssignedTo} placeholder="Shop name" />
          <TextField label="Date required / OOS until" value={dueDate} onChange={setDueDate} mono placeholder="YYYY-MM-DD" />
          <TextField label="Budget code (optional)" value={budgetCode} onChange={setBudgetCode} placeholder="Fleet Maintenance" />
        </div>
      </div>
    </ModalShell>
  );
}
