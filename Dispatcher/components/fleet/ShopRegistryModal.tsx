"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import type { Shop } from "@/lib/types";
import { addShop, shops, updateShop, useMaintenanceStore } from "@/lib/maintenanceStore";
import { ModalShell } from "@/components/ui/ModalShell";
import { SelectField, TextAreaField, TextField } from "@/components/ui/Field";
import { MonoTag } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";

// Register shops & partners once (repair facilities, parts suppliers) so their
// details auto-fill work orders and the NL-WO-01 PDF instead of being retyped.

export default function ShopRegistryModal({ onClose }: { onClose: () => void }) {
  useMaintenanceStore();
  const [editing, setEditing] = useState<Shop | "new" | null>(null);

  if (editing) {
    return (
      <ShopForm
        initial={editing === "new" ? null : editing}
        onCancel={() => setEditing(null)}
        onSaved={() => setEditing(null)}
      />
    );
  }

  return (
    <ModalShell
      eyebrow="Fleet & Maintenance · Reusable partners"
      title="Shops & Partners"
      onClose={onClose}
      maxWidth={620}
      footer={
        <>
          <ActionButton onClick={onClose}>CLOSE</ActionButton>
          <ActionButton variant="primary" onClick={() => setEditing("new")}>
            + ADD SHOP / PARTNER
          </ActionButton>
        </>
      }
    >
      {shops.length === 0 ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
          No shops or partners registered yet.
        </div>
      ) : (
        shops.map((s) => (
          <div
            key={s.id}
            style={{
              display: "grid",
              gridTemplateColumns: "1fr auto",
              gap: 10,
              alignItems: "center",
              padding: "11px 13px",
              marginBottom: 6,
              borderRadius: 9,
              border: `1px solid ${colors.borderSubtle}`,
              background: colors.cardBg,
            }}
          >
            <div style={{ minWidth: 0 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 2 }}>
                <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
                  {s.name}
                </span>
                {s.mpiAccredited && <MonoTag color={colors.skyBlue}>MPI</MonoTag>}
                {s.suppliesParts && <MonoTag>PARTS</MonoTag>}
              </div>
              <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
                {[s.contactName, s.phone, s.address].filter(Boolean).join(" · ") || "—"}
              </div>
            </div>
            <ActionButton onClick={() => setEditing(s)}>EDIT</ActionButton>
          </div>
        ))
      )}
    </ModalShell>
  );
}

function ShopForm({
  initial,
  onCancel,
  onSaved,
}: {
  initial: Shop | null;
  onCancel: () => void;
  onSaved: () => void;
}) {
  const [f, setF] = useState<Omit<Shop, "id">>({
    name: initial?.name ?? "",
    contactName: initial?.contactName ?? "",
    phone: initial?.phone ?? "",
    email: initial?.email ?? "",
    address: initial?.address ?? "",
    gstBusinessNo: initial?.gstBusinessNo ?? "",
    mpiAccredited: initial?.mpiAccredited ?? false,
    inspectionStationNo: initial?.inspectionStationNo ?? "",
    suppliesParts: initial?.suppliesParts ?? false,
    notes: initial?.notes ?? "",
  });
  const [error, setError] = useState<string | null>(null);

  const set = <K extends keyof Omit<Shop, "id">>(key: K, value: Omit<Shop, "id">[K]) =>
    setF((prev) => ({ ...prev, [key]: value }));

  function submit() {
    if (!f.name.trim()) return setError("Enter the shop or partner name.");
    const clean: Omit<Shop, "id"> = {
      ...f,
      name: f.name.trim(),
      contactName: f.contactName?.trim() || undefined,
      phone: f.phone?.trim() || undefined,
      email: f.email?.trim() || undefined,
      address: f.address?.trim() || undefined,
      gstBusinessNo: f.gstBusinessNo?.trim() || undefined,
      inspectionStationNo: f.inspectionStationNo?.trim() || undefined,
      notes: f.notes?.trim() || undefined,
    };
    if (initial) updateShop(initial.id, clean);
    else addShop(clean);
    onSaved();
  }

  const yesNo = [
    { value: "no", label: "No" },
    { value: "yes", label: "Yes" },
  ];

  return (
    <ModalShell
      eyebrow="Fleet & Maintenance · Reusable partners"
      title={initial ? `Edit ${initial.name}` : "Add Shop / Partner"}
      onClose={onCancel}
      error={error}
      maxWidth={620}
      footer={
        <>
          <ActionButton onClick={onCancel}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit}>
            {initial ? "SAVE CHANGES" : "ADD SHOP / PARTNER"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <TextField label="Shop / business name" value={f.name} onChange={(v) => set("name", v)} placeholder="Thompson Certified Shop" />
        <TextField label="Contact name" value={f.contactName ?? ""} onChange={(v) => set("contactName", v)} />
        <TextField label="Phone" value={f.phone ?? ""} onChange={(v) => set("phone", v)} mono placeholder="(204) 000-0000" />
        <TextField label="Email" value={f.email ?? ""} onChange={(v) => set("email", v)} />
        <TextField label="GST / Business No." value={f.gstBusinessNo ?? ""} onChange={(v) => set("gstBusinessNo", v)} mono />
        <TextField label="MB inspection station no." value={f.inspectionStationNo ?? ""} onChange={(v) => set("inspectionStationNo", v)} mono placeholder="MB-0000" />
        <SelectField
          label="MPI accredited repair shop?"
          value={f.mpiAccredited ? "yes" : "no"}
          onChange={(v) => set("mpiAccredited", v === "yes")}
          options={yesNo}
        />
        <SelectField
          label="Supplies parts?"
          value={f.suppliesParts ? "yes" : "no"}
          onChange={(v) => set("suppliesParts", v === "yes")}
          options={yesNo}
        />
      </div>
      <div style={{ marginTop: 14, display: "flex", flexDirection: "column", gap: 14 }}>
        <TextField label="Address" value={f.address ?? ""} onChange={(v) => set("address", v)} placeholder="Street, Town, MB  Postal" />
        <TextAreaField label="Notes (optional)" value={f.notes ?? ""} onChange={(v) => set("notes", v)} rows={2} />
      </div>
    </ModalShell>
  );
}
