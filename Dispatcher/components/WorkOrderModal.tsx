"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import type { WorkOrderPrefill, WorkOrderPriority } from "@/lib/types";
import { addWorkOrder, linkInspectionWorkOrder, shops, useMaintenanceStore } from "@/lib/maintenanceStore";
import { ModalShell } from "@/components/ui/ModalShell";
import { NumberField, SelectField, TextAreaField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

const PRIORITIES: WorkOrderPriority[] = ["Low", "Medium", "High", "Critical"];

// Create a work order — manually from the interface, or prefilled from a
// pre/post-trip inspection's defects (WorkOrderPrefill). A registered shop can be
// attached so its details auto-fill the printable NL-WO-01 work order.

export default function WorkOrderModal({
  units,
  defaultUnit,
  prefill,
  onClose,
  onSaved,
}: {
  units: { value: string; label: string }[];
  defaultUnit?: string;
  prefill?: WorkOrderPrefill;
  onClose: () => void;
  onSaved: () => void;
}) {
  useMaintenanceStore();
  const [unit, setUnit] = useState(defaultUnit ?? units[0]?.value ?? "");
  const [title, setTitle] = useState(prefill?.title ?? "");
  const [priority, setPriority] = useState<WorkOrderPriority>(prefill?.priority ?? "Medium");
  const [description, setDescription] = useState(prefill?.description ?? "");
  const [lineItems, setLineItems] = useState((prefill?.lineItems ?? []).join("\n"));
  const [shopId, setShopId] = useState("");
  const [limit, setLimit] = useState("");
  const [budgetCode, setBudgetCode] = useState("");
  const [assignedTo, setAssignedTo] = useState("");
  const [dueDate, setDueDate] = useState("");
  const [error, setError] = useState<string | null>(null);

  const fromInspection = !!prefill?.inspectionId;
  const shopOptions = [{ value: "", label: "— none —" }, ...shops.map((s) => ({ value: s.id, label: s.name }))];

  function submit() {
    if (!unit) return setError("Select the vehicle this work order is for.");
    if (!title.trim()) return setError("Enter a work order title.");

    const shop = shops.find((s) => s.id === shopId);
    const wo = addWorkOrder({
      unit,
      title: title.trim(),
      description: description.trim(),
      status: "Open",
      priority,
      source: prefill?.source ?? "Manual",
      sourceRef: prefill?.sourceRef,
      createdBy: "Dispatch",
      assignedTo: assignedTo.trim() || shop?.name || undefined,
      dueDate: dueDate.trim() || undefined,
      dateRequiredOrOos: dueDate.trim() || undefined,
      lineItems: lineItems.split("\n").map((s) => s.trim()).filter(Boolean),
      shopId: shopId || undefined,
      authorizedLimitCad: limit ? parseFloat(limit) : undefined,
      budgetCode: budgetCode.trim() || undefined,
    });

    if (prefill?.inspectionId) linkInspectionWorkOrder(prefill.inspectionId, wo.id);

    onSaved();
    onClose();
  }

  return (
    <ModalShell
      eyebrow={`Fleet & Maintenance · Work orders${prefill && prefill.source !== "Manual" ? ` · from ${prefill.source}` : ""}`}
      title="New Work Order"
      onClose={onClose}
      error={error}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit}>
            CREATE WORK ORDER
          </ActionButton>
        </>
      }
    >
      {prefill && prefill.source !== "Manual" && (
        <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, marginBottom: 14 }}>
          Generated from <span style={{ fontWeight: 600 }}>{prefill.source}</span>
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
          value={unit}
          onChange={setUnit}
          disabled={fromInspection}
          options={units.length ? units : [{ value: unit, label: unit || "—" }]}
        />
        <SelectField
          label="Priority"
          value={priority}
          onChange={(v) => setPriority(v as WorkOrderPriority)}
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
