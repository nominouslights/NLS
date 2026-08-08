"use client";

import { useEffect, useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import { createShop, listShops, updateShop, type ShopInput, type ShopWire } from "@/lib/api/maintenance";
import { ModalShell } from "@/components/ui/ModalShell";
import { SelectField, TextAreaField, TextField } from "@/components/ui/Field";
import { MonoTag } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";

// Register shops & partners once (repair facilities, parts suppliers) so their
// details auto-fill work orders and the NL-WO-01 PDF instead of being retyped.

export default function ShopRegistryModal({ onClose }: { onClose: () => void }) {
  const [shops, setShops] = useState<ShopWire[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [reload, setReload] = useState(0);
  const [editing, setEditing] = useState<ShopWire | "new" | null>(null);

  useEffect(() => {
    let active = true;
    listShops().then(
      (rows) => {
        if (active) {
          setShops(rows);
          setLoadError(null);
        }
      },
      (e) => {
        if (active) setLoadError(e instanceof ApiError ? e.message : "Failed to load shops.");
      },
    );
    return () => {
      active = false;
    };
  }, [reload]);

  if (editing) {
    return (
      <ShopForm
        initial={editing === "new" ? null : editing}
        onCancel={() => setEditing(null)}
        onSaved={() => {
          setEditing(null);
          setReload((n) => n + 1);
        }}
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
      {loadError ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: statusMeta("over").t, fontWeight: 600 }}>
          ▲ {loadError}
        </div>
      ) : shops === null ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading shops…</div>
      ) : shops.length === 0 ? (
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
  initial: ShopWire | null;
  onCancel: () => void;
  onSaved: () => void;
}) {
  const [f, setF] = useState({
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
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const set = <K extends keyof typeof f>(key: K, value: (typeof f)[K]) =>
    setF((prev) => ({ ...prev, [key]: value }));

  async function submit() {
    if (busy) return;
    if (!f.name.trim()) return setError("Enter the shop or partner name.");
    const input: ShopInput = {
      name: f.name.trim(),
      contactName: f.contactName.trim() || null,
      phone: f.phone.trim() || null,
      email: f.email.trim() || null,
      address: f.address.trim() || null,
      gstBusinessNo: f.gstBusinessNo.trim() || null,
      mpiAccredited: f.mpiAccredited,
      inspectionStationNo: f.inspectionStationNo.trim() || null,
      suppliesParts: f.suppliesParts,
      notes: f.notes.trim() || null,
    };
    setBusy(true);
    setError(null);
    try {
      if (initial) await updateShop(initial.id, input);
      else await createShop(input);
      onSaved();
    } catch (e) {
      setBusy(false);
      setError(e instanceof ApiError ? e.message : "Failed to save the shop — please try again.");
    }
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
          <ActionButton onClick={onCancel} disabled={busy}>
            CANCEL
          </ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "SAVING…" : initial ? "SAVE CHANGES" : "ADD SHOP / PARTNER"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <TextField label="Shop / business name" value={f.name} onChange={(v) => set("name", v)} placeholder="Thompson Certified Shop" />
        <TextField label="Contact name" value={f.contactName} onChange={(v) => set("contactName", v)} />
        <TextField label="Phone" value={f.phone} onChange={(v) => set("phone", v)} mono placeholder="(204) 000-0000" />
        <TextField label="Email" value={f.email} onChange={(v) => set("email", v)} />
        <TextField label="GST / Business No." value={f.gstBusinessNo} onChange={(v) => set("gstBusinessNo", v)} mono />
        <TextField label="MB inspection station no." value={f.inspectionStationNo} onChange={(v) => set("inspectionStationNo", v)} mono placeholder="MB-0000" />
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
        <TextField label="Address" value={f.address} onChange={(v) => set("address", v)} placeholder="Street, Town, MB  Postal" />
        <TextAreaField label="Notes (optional)" value={f.notes} onChange={(v) => set("notes", v)} rows={2} />
      </div>
    </ModalShell>
  );
}
