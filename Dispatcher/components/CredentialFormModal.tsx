"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import { addDriverCredential, uploadDriverCredentialImage, CREDENTIAL_TYPES } from "@/lib/api/drivers";
import { ModalShell } from "@/components/ui/ModalShell";
import { DateField, SelectField, TextField } from "@/components/ui/Field";
import { ImageUploadField } from "@/components/ui/ImageUploadField";
import { ActionButton } from "@/components/ui/Button";

// Add a driver credential (licence, cert, background check) — POST
// /api/drivers/{driverId}/credentials (Drivers module). First Aid and Work
// Permit are the two optional credential types; that flag is derived from
// type, not user-input. The wire contract has no proof-document fields, so
// the old prototype file picker is gone.

export default function CredentialFormModal({
  driverId,
  driverName,
  onClose,
  onSaved,
}: {
  driverId: string;
  driverName: string;
  onClose: () => void;
  onSaved: (newId: string) => void;
}) {
  const [type, setType] = useState<string>(CREDENTIAL_TYPES[0]);
  const [label, setLabel] = useState("");
  const [issued, setIssued] = useState("");
  const [expiry, setExpiry] = useState("");
  const [noExpiry, setNoExpiry] = useState(false);
  const [note, setNote] = useState("");
  const [image, setImage] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const optional = type === "First Aid" || type === "Work Permit";

  async function submit() {
    if (busy) return;
    if (!label.trim()) return setError("Enter a label (e.g. \"Class 4\", \"Standard First Aid + CPR-C\").");
    if (!noExpiry && !expiry) return setError("Enter an expiry date, or turn on \"No fixed expiry\".");

    setBusy(true);
    setError(null);
    try {
      const newId = await addDriverCredential(driverId, {
        type,
        label: label.trim(),
        issued: issued || null,
        expiry: noExpiry ? null : expiry,
        optional,
        note: note.trim() || null,
      });
      onSaved(newId);

      // If an image was selected, upload it as a follow-up. If it fails, the credential
      // was already created successfully; surface the upload error but don't roll back.
      if (image) {
        try {
          await uploadDriverCredentialImage(driverId, newId, image);
        } catch (uploadErr) {
          setError(uploadErr instanceof ApiError ? uploadErr.message : "Credential added, but image upload failed.");
          setBusy(false);
          return;
        }
      }

      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to add the credential — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Operations · ${driverName} · Licence & certs`}
      title="Add Credential"
      onClose={onClose}
      error={error}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} style={busy ? { opacity: 0.6, cursor: "wait" } : undefined}>
            {busy ? "ADDING…" : "ADD CREDENTIAL"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <SelectField
          label="Type"
          value={type}
          onChange={setType}
          options={CREDENTIAL_TYPES.map((t) => ({ value: t, label: t }))}
        />
        <TextField label="Label" value={label} onChange={setLabel} placeholder="Class 4" />
        <DateField label="Issued (optional — defaults to today)" value={issued} onChange={setIssued} />
        <DateField label="Expiry" value={expiry} onChange={setExpiry} disabled={noExpiry} />
      </div>
      <div style={{ marginTop: 14 }}>
        <TextField label="Note (optional)" value={note} onChange={setNote} placeholder="Sponsor, restrictions, etc." />
      </div>
      <div style={{ marginTop: 14 }}>
        <ImageUploadField
          label="Photo (optional)"
          value={image}
          onChange={setImage}
          hint="Scan or photo of the credential (JPEG, PNG, HEIC only)."
        />
      </div>

      {/* no-expiry toggle */}
      <div style={{ marginTop: 16, display: "flex", alignItems: "center", gap: 11 }}>
        <span
          onClick={() => setNoExpiry((v) => !v)}
          style={{
            width: 40,
            height: 22,
            flex: "none",
            borderRadius: 999,
            background: noExpiry ? colors.blue : colors.borderStrong,
            position: "relative",
            cursor: "pointer",
            transition: "background .15s",
          }}
        >
          <span
            style={{
              position: "absolute",
              top: 2,
              left: noExpiry ? 20 : 2,
              width: 18,
              height: 18,
              borderRadius: "50%",
              background: "#FFFFFF",
              boxShadow: colors.shadowCard,
              transition: "left .15s",
            }}
          />
        </span>
        <div>
          <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
            No fixed expiry
          </div>
          <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
            Turn on for credentials that don&apos;t lapse (e.g. some background checks).
          </div>
        </div>
      </div>
    </ModalShell>
  );
}
