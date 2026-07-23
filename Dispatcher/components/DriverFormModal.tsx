"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import { registerDriver, updateDriver, type DriverRecord } from "@/lib/api/drivers";
import { ModalShell } from "@/components/ui/ModalShell";
import { DateField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

// Register or edit a driver — POST /api/drivers and PUT /api/drivers/{id}
// (Drivers module). Both take the same body (RegisterDriverRequest /
// UpdateDriverRequest), so one dual-mode form covers them, the same way
// VehicleFormModal covers register/edit for Fleet.
//
// Name, licence class and source are the domain's required fields
// (DriverErrors.NameRequired / LicenceClassRequired / SourceRequired); phone and
// licence expiry are nullable and send null rather than "" when blank. The
// backend trims and normalizes blank phone → null, so trimming here is cosmetic.

export default function DriverFormModal({
  driver,
  onClose,
  onSaved,
}: {
  driver: DriverRecord | null;
  onClose: () => void;
  onSaved: (newId?: string) => void;
}) {
  const editing = driver !== null;

  const [name, setName] = useState(driver?.name ?? "");
  const [phone, setPhone] = useState(driver?.phone ?? "");
  const [licenceClass, setLicenceClass] = useState(driver?.licenceClass ?? "");
  const [licenceExpiry, setLicenceExpiry] = useState(driver?.licenceExpiry ?? "");
  const [source, setSource] = useState(driver?.source ?? "Northern Link");
  const [hasWorkPermit, setHasWorkPermit] = useState(driver?.hasWorkPermit ?? false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (busy) return;
    if (!name.trim()) return setError("Enter the driver's full name.");
    if (!licenceClass.trim()) return setError("Enter a licence class (e.g. \"Class 2\", \"Class 4\").");
    if (!source.trim()) return setError("Enter a source — \"Northern Link\" for in-house drivers.");

    setBusy(true);
    setError(null);
    const input = {
      name: name.trim(),
      phone: phone.trim() || null,
      licenceClass: licenceClass.trim(),
      licenceExpiry: licenceExpiry || null,
      source: source.trim(),
      hasWorkPermit,
    };
    try {
      if (editing && driver) {
        await updateDriver(driver.id, input);
        onSaved();
      } else {
        const newId = await registerDriver(input);
        onSaved(newId);
      }
      onClose();
    } catch (e) {
      setError(
        e instanceof ApiError
          ? e.message
          : editing
            ? "Failed to save the driver — please try again."
            : "Failed to register the driver — please try again.",
      );
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow="Operations · Roster & compliance"
      title={editing && driver ? `Edit ${driver.name}` : "Register Driver"}
      onClose={onClose}
      error={error}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} style={busy ? { opacity: 0.6, cursor: "wait" } : undefined}>
            {busy ? "SAVING…" : editing ? "SAVE CHANGES" : "REGISTER DRIVER"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <TextField label="Full name" value={name} onChange={setName} placeholder="J. Spence" />
        <TextField label="Phone (optional)" value={phone} onChange={setPhone} mono placeholder="204-555-0100" />
        <TextField label="Licence class" value={licenceClass} onChange={setLicenceClass} placeholder="Class 2" />
        <DateField label="Licence expiry (optional)" value={licenceExpiry} onChange={setLicenceExpiry} />
      </div>
      <div style={{ marginTop: 14 }}>
        <TextField
          label="Source"
          value={source}
          onChange={setSource}
          placeholder="Northern Link"
          hint={
            <span style={{ color: colors.textDim }}>
              — &ldquo;Northern Link&rdquo; for in-house drivers, otherwise the partner carrier&rsquo;s name
            </span>
          }
        />
      </div>

      {/* work-permit toggle */}
      <div style={{ marginTop: 16, display: "flex", alignItems: "center", gap: 11 }}>
        <span
          onClick={() => setHasWorkPermit((v) => !v)}
          style={{
            width: 40,
            height: 22,
            flex: "none",
            borderRadius: 999,
            background: hasWorkPermit ? colors.blue : colors.borderStrong,
            position: "relative",
            cursor: "pointer",
            transition: "background .15s",
          }}
        >
          <span
            style={{
              position: "absolute",
              top: 2,
              left: hasWorkPermit ? 20 : 2,
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
            Works under a foreign-worker permit
          </div>
          <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
            Flags the driver on the roster so permit expiry stays visible alongside their credentials.
          </div>
        </div>
      </div>
    </ModalShell>
  );
}
