"use client";

import { useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import {
  ApiError,
  registerVehicle,
  updateVehicle,
  type Vehicle,
  type VehicleInput,
} from "@/lib/api";
import { NumberField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

// Fixed-overlay modal (CreateTripWizard pattern) for Register + Edit vehicle.
// Server-side validation is authoritative — API errors surface in a vermillion banner.

export default function VehicleFormModal({
  vehicle,
  onClose,
  onSaved,
}: {
  vehicle: Vehicle | null; // null → register mode; set → edit mode
  onClose: () => void;
  onSaved: () => void;
}) {
  const editing = vehicle !== null;

  const [unitNumber, setUnitNumber] = useState(vehicle?.unitNumber ?? "");
  const [vin, setVin] = useState(vehicle?.vin ?? "");
  const [make, setMake] = useState(vehicle?.make ?? "");
  const [model, setModel] = useState(vehicle?.model ?? "");
  const [year, setYear] = useState(vehicle ? String(vehicle.year) : "");
  const [seatingCapacity, setSeatingCapacity] = useState(
    vehicle ? String(vehicle.seatingCapacity) : "",
  );
  const [licencePlate, setLicencePlate] = useState(vehicle?.licencePlate ?? "");
  const [requiredLicenceClass, setRequiredLicenceClass] = useState(
    vehicle?.requiredLicenceClass ?? "Class 4",
  );
  const [acquisitionCostCad, setAcquisitionCostCad] = useState(
    vehicle ? String(vehicle.acquisitionCostCad) : "",
  );
  const [endOfLifeKm, setEndOfLifeKm] = useState(vehicle ? String(vehicle.endOfLifeKm) : "");

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const seats = parseInt(seatingCapacity, 10);
  const periodicHint =
    Number.isFinite(seats) && seats >= 11
      ? "11+ seats — six-month NSC-11 periodic inspection will apply"
      : "Under 11 seats — trip-inspection / DVIR only";

  async function submit() {
    if (busy) return;
    setError(null);

    const input: VehicleInput = {
      unitNumber: unitNumber.trim(),
      vin: vin.trim().toUpperCase(),
      make: make.trim(),
      model: model.trim(),
      year: parseInt(year, 10),
      seatingCapacity: parseInt(seatingCapacity, 10),
      licencePlate: licencePlate.trim(),
      requiredLicenceClass: requiredLicenceClass.trim(),
      acquisitionCostCad: parseFloat(acquisitionCostCad),
      endOfLifeKm: parseInt(endOfLifeKm, 10),
    };

    setBusy(true);
    try {
      if (editing && vehicle) {
        await updateVehicle(vehicle.id, input);
      } else {
        await registerVehicle(input);
      }
      onSaved();
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Unexpected error — please try again.");
      setBusy(false);
    }
  }

  return (
    <div
      style={{
        position: "fixed",
        inset: 0,
        zIndex: 100,
        background: colors.scrim,
        backdropFilter: "blur(3px)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: 26,
      }}
      className="detailfade"
    >
      <div
        style={{
          width: "100%",
          maxWidth: 680,
          maxHeight: "88vh",
          background: colors.mainBg,
          border: `1px solid ${colors.borderStrong}`,
          borderRadius: 16,
          display: "flex",
          flexDirection: "column",
          overflow: "hidden",
          boxShadow: colors.shadowPop,
        }}
      >
        {/* header */}
        <div
          style={{
            flex: "none",
            display: "flex",
            alignItems: "center",
            padding: "18px 24px",
            borderBottom: `1px solid ${colors.border}`,
          }}
        >
          <div>
            <div
              style={{
                fontFamily: fonts.semiCondensed,
                fontSize: 10,
                letterSpacing: ".16em",
                textTransform: "uppercase",
                color: colors.textFaint,
                marginBottom: 2,
              }}
            >
              Fleet · Vehicle registry
            </div>
            <div
              style={{
                fontFamily: fonts.condensed,
                fontWeight: 700,
                fontSize: 22,
                color: colors.headingBright,
                lineHeight: 1,
              }}
            >
              {editing ? `Edit ${vehicle?.unitNumber}` : "Register Vehicle"}
            </div>
          </div>
          <div
            onClick={onClose}
            style={{
              marginLeft: "auto",
              width: 34,
              height: 34,
              borderRadius: 8,
              border: `1px solid ${colors.borderStrong}`,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              color: colors.textMuted,
              cursor: "pointer",
              fontSize: 18,
            }}
          >
            ✕
          </div>
        </div>

        {/* body */}
        <div style={{ flex: 1, minHeight: 0, overflowY: "auto", padding: "22px 24px" }}>
          {error && (
            <div
              style={{
                padding: "12px 15px",
                background: "rgba(213,94,0,.1)",
                border: "1px solid rgba(213,94,0,.4)",
                borderRadius: 10,
                marginBottom: 16,
                display: "flex",
                gap: 10,
                alignItems: "center",
              }}
            >
              <span
                style={{
                  width: 20,
                  height: 20,
                  flex: "none",
                  borderRadius: 5,
                  background: "#D55E00",
                  color: "#fff",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontSize: 11,
                  fontWeight: 800,
                }}
              >
                ▲
              </span>
              <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: statusMeta("over").t, fontWeight: 600 }}>
                {error}
              </span>
            </div>
          )}

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
            <TextField label="Unit number" value={unitNumber} onChange={setUnitNumber} mono placeholder="U-07" />
            <TextField
              label="VIN"
              value={vin}
              onChange={setVin}
              mono
              maxLength={17}
              placeholder="17 characters"
              hint={<span style={{ color: colors.textFaint }}>· no I / O / Q</span>}
            />
            <TextField label="Make" value={make} onChange={setMake} placeholder="International" />
            <TextField label="Model" value={model} onChange={setModel} placeholder="3000" />
            <NumberField label="Year" value={year} onChange={setYear} min={1980} step={1} placeholder="2022" />
            <NumberField
              label="Seating capacity"
              value={seatingCapacity}
              onChange={setSeatingCapacity}
              min={1}
              step={1}
              hint={<span style={{ color: colors.textFaint }}>· {periodicHint}</span>}
            />
            <TextField label="Licence plate" value={licencePlate} onChange={setLicencePlate} mono placeholder="MB · XXX 000" />
            <TextField label="Required licence class" value={requiredLicenceClass} onChange={setRequiredLicenceClass} placeholder="Class 4" />
            <NumberField
              label="Acquisition cost (CAD)"
              value={acquisitionCostCad}
              onChange={setAcquisitionCostCad}
              min={0}
              step={100}
              placeholder="185000"
              hint={<span style={{ color: colors.textFaint }}>· straight-line km depreciation</span>}
            />
            <NumberField
              label="End-of-life odometer (km)"
              value={endOfLifeKm}
              onChange={setEndOfLifeKm}
              min={1}
              step={1000}
              placeholder="500000"
              hint={<span style={{ color: colors.textFaint }}>· auto-retires at this reading</span>}
            />
          </div>
        </div>

        {/* footer */}
        <div
          style={{
            flex: "none",
            display: "flex",
            alignItems: "center",
            justifyContent: "flex-end",
            gap: 10,
            padding: "16px 24px",
            borderTop: `1px solid ${colors.border}`,
          }}
        >
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} style={{ opacity: busy ? 0.6 : 1 }}>
            {busy ? "SAVING…" : editing ? "SAVE CHANGES" : "REGISTER VEHICLE"}
          </ActionButton>
        </div>
      </div>
    </div>
  );
}
