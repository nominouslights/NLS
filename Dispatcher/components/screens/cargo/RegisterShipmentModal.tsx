"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  SHIPMENT_KIND_LABELS,
  SHIPMENT_KINDS,
  type ShipmentInput,
  type ShipmentKind,
  type ShipmentPaymentMethod,
  type ShipmentRecord,
} from "@/lib/api/shipments";
import { listClients, type ClientRecord } from "@/lib/api/clients";
import { listStops, sortStops, type StopRecord } from "@/lib/api/stops";
import { ModalShell } from "@/components/ui/ModalShell";
import { ActionButton } from "@/components/ui/Button";
import { SectionLabel } from "@/components/ui/Panel";
import { DateField, NumberField, SelectField, TextField } from "@/components/ui/Field";

// Register / edit a shipment — POST /api/trips/shipments · PUT /{id}.
// Origin/destination come from the Stops catalog (snapshotting id + name) with
// a free-text fallback for places that aren't catalog stops. The client is the
// shipment's own payer; its NAME is never sent — the backend snapshots it from
// the client lookup replica.

const PAYMENT_METHODS: { value: ShipmentPaymentMethod | ""; label: string }[] = [
  { value: "", label: "— not set —" },
  { value: "Cash", label: "Cash" },
  { value: "Online", label: "Online" },
  { value: "Waived", label: "Waived (deliberately free)" },
];

const FREE_TEXT = ""; // stop-select sentinel

function numOrNull(v: string): number | null {
  return v.trim() === "" ? null : Number(v);
}

export default function RegisterShipmentModal({
  existing,
  onClose,
  onSaved,
}: {
  existing: ShipmentRecord | null;
  onClose: () => void;
  onSaved: (input: ShipmentInput, existingId: string | null) => Promise<void>;
}) {
  const editing = existing !== null;

  const [description, setDescription] = useState(existing?.description ?? "");
  const [kind, setKind] = useState<ShipmentKind>(existing?.kind ?? "Parcel");
  const [pieces, setPieces] = useState(existing ? String(existing.pieces) : "1");
  const [weightKg, setWeightKg] = useState(existing?.weightKg != null ? String(existing.weightKg) : "");
  const [lengthCm, setLengthCm] = useState(existing?.lengthCm != null ? String(existing.lengthCm) : "");
  const [widthCm, setWidthCm] = useState(existing?.widthCm != null ? String(existing.widthCm) : "");
  const [heightCm, setHeightCm] = useState(existing?.heightCm != null ? String(existing.heightCm) : "");
  const [hazmat, setHazmat] = useState(existing?.hazmat ?? false);
  const [declaredValue, setDeclaredValue] = useState(
    existing?.declaredValueCad != null ? String(existing.declaredValueCad) : "",
  );
  const [specialHandling, setSpecialHandling] = useState(existing?.specialHandling ?? "");

  const [consignorName, setConsignorName] = useState(existing?.consignorName ?? "");
  const [consignorContact, setConsignorContact] = useState(existing?.consignorContact ?? "");
  const [consigneeName, setConsigneeName] = useState(existing?.consigneeName ?? "");
  const [consigneeContact, setConsigneeContact] = useState(existing?.consigneeContact ?? "");

  const [originStopId, setOriginStopId] = useState(existing?.originStopId ?? FREE_TEXT);
  const [originName, setOriginName] = useState(existing?.originName ?? "");
  const [destinationStopId, setDestinationStopId] = useState(existing?.destinationStopId ?? FREE_TEXT);
  const [destinationName, setDestinationName] = useState(existing?.destinationName ?? "");

  const [readyDate, setReadyDate] = useState(existing?.readyDate ?? "");
  const [requiredByDate, setRequiredByDate] = useState(existing?.requiredByDate ?? "");

  const [clientId, setClientId] = useState(existing?.clientId ?? "");
  const [poNumber, setPoNumber] = useState(existing?.poNumber ?? "");
  const [chargeCad, setChargeCad] = useState(existing?.chargeCad != null ? String(existing.chargeCad) : "");
  const [paymentMethod, setPaymentMethod] = useState<ShipmentPaymentMethod | "">(existing?.paymentMethod ?? "");

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [stops, setStops] = useState<StopRecord[] | null>(null);
  const [clients, setClients] = useState<ClientRecord[] | null>(null);

  useEffect(() => {
    let active = true;
    listStops().then(
      (rows) => {
        if (active) setStops(sortStops(rows.filter((s) => s.active)));
      },
      () => {
        if (active) setStops([]); // free-text fallback still works
      },
    );
    listClients().then(
      (rows) => {
        if (active) setClients(rows);
      },
      () => {
        if (active) setClients([]);
      },
    );
    return () => {
      active = false;
    };
  }, []);

  const stopOptions = [
    { value: FREE_TEXT, label: "— free text —" },
    ...(stops ?? []).map((s) => ({ value: s.id, label: s.name })),
  ];
  const originStop = stops?.find((s) => s.id === originStopId) ?? null;
  const destinationStop = stops?.find((s) => s.id === destinationStopId) ?? null;

  async function submit() {
    if (busy) return;
    if (!description.trim()) return setError("Describe the shipment.");
    const pc = Number(pieces);
    if (!Number.isInteger(pc) || pc < 1) return setError("Pieces must be a whole number of at least 1.");
    for (const [label, v] of [
      ["Weight", weightKg],
      ["Length", lengthCm],
      ["Width", widthCm],
      ["Height", heightCm],
      ["Declared value", declaredValue],
      ["Charge", chargeCad],
    ] as const) {
      if (v.trim() !== "" && (Number.isNaN(Number(v)) || Number(v) < 0)) {
        return setError(`${label} must be a non-negative number.`);
      }
    }
    const oName = originStop ? originStop.name : originName.trim();
    const dName = destinationStop ? destinationStop.name : destinationName.trim();
    if (!oName || !dName) return setError("Enter the origin and destination (a catalog stop or free text).");

    const input: ShipmentInput = {
      description: description.trim(),
      kind,
      pieces: pc,
      weightKg: numOrNull(weightKg),
      lengthCm: numOrNull(lengthCm),
      widthCm: numOrNull(widthCm),
      heightCm: numOrNull(heightCm),
      hazmat,
      declaredValueCad: numOrNull(declaredValue),
      specialHandling: specialHandling.trim() || null,
      consignorName: consignorName.trim() || null,
      consignorContact: consignorContact.trim() || null,
      consigneeName: consigneeName.trim() || null,
      consigneeContact: consigneeContact.trim() || null,
      originStopId: originStop?.id ?? null,
      originName: oName,
      destinationStopId: destinationStop?.id ?? null,
      destinationName: dName,
      readyDate: readyDate || null,
      requiredByDate: requiredByDate || null,
      clientId: clientId || null,
      poNumber: poNumber.trim() || null,
      chargeCad: numOrNull(chargeCad),
      paymentMethod: paymentMethod || null,
      source: "Dispatcher",
      enteredBy: "Dispatch",
    };

    setBusy(true);
    setError(null);
    try {
      await onSaved(input, existing?.id ?? null);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to save the shipment — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow="Cargo & Grocery · Shipments"
      title={editing ? `Edit Shipment ${existing.shipmentNumber}` : "Register Shipment"}
      onClose={onClose}
      error={error}
      maxWidth={760}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "SAVING…" : editing ? "SAVE SHIPMENT" : "REGISTER SHIPMENT"}
          </ActionButton>
        </>
      }
    >
      <SectionLabel>What is moving</SectionLabel>
      <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr 1fr", gap: 14 }}>
        <TextField label="Description" value={description} onChange={setDescription} placeholder="Grocery totes — Northern Store order" />
        <SelectField
          label="Kind"
          value={kind}
          onChange={(v) => setKind(v as ShipmentKind)}
          options={SHIPMENT_KINDS.map((k) => ({ value: k, label: SHIPMENT_KIND_LABELS[k] }))}
        />
        <NumberField label="Pieces" value={pieces} onChange={setPieces} min={1} step={1} />
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr 1fr 1fr", gap: 14, marginTop: 14 }}>
        <NumberField label="Weight (kg)" value={weightKg} onChange={setWeightKg} min={0} />
        <NumberField label="L (cm)" value={lengthCm} onChange={setLengthCm} min={0} />
        <NumberField label="W (cm)" value={widthCm} onChange={setWidthCm} min={0} />
        <NumberField label="H (cm)" value={heightCm} onChange={setHeightCm} min={0} />
        <NumberField label="Declared value ($)" value={declaredValue} onChange={setDeclaredValue} min={0} />
      </div>
      <div style={{ display: "flex", alignItems: "center", gap: 18, marginTop: 12 }}>
        <label style={{ display: "flex", alignItems: "center", gap: 8, cursor: "pointer" }}>
          <input
            type="checkbox"
            checked={hazmat}
            onChange={(e) => setHazmat(e.target.checked)}
            style={{ accentColor: colors.blue, cursor: "pointer" }}
          />
          <span style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary }}>
            Hazmat / dangerous goods
          </span>
        </label>
      </div>
      <div style={{ marginTop: 12 }}>
        <TextField label="Special handling (optional)" value={specialHandling} onChange={setSpecialHandling} placeholder="Keep frozen · fragile" />
      </div>

      <div style={{ marginTop: 18 }}>
        <SectionLabel>Where — origin &amp; destination</SectionLabel>
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          <SelectField label="Origin stop" value={originStopId} onChange={setOriginStopId} options={stopOptions} />
          {!originStop && (
            <TextField label="Origin (free text)" value={originName} onChange={setOriginName} placeholder="Thompson depot" />
          )}
        </div>
        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          <SelectField label="Destination stop" value={destinationStopId} onChange={setDestinationStopId} options={stopOptions} />
          {!destinationStop && (
            <TextField label="Destination (free text)" value={destinationName} onChange={setDestinationName} placeholder="Lynn Lake" />
          )}
        </div>
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14, marginTop: 14 }}>
        <DateField label="Ready date (optional)" value={readyDate} onChange={setReadyDate} />
        <DateField label="Required by (optional)" value={requiredByDate} onChange={setRequiredByDate} />
      </div>

      <div style={{ marginTop: 18 }}>
        <SectionLabel>Who — consignor, consignee &amp; payer</SectionLabel>
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <TextField label="Consignor / shipper (optional)" value={consignorName} onChange={setConsignorName} />
        <TextField label="Consignor contact (optional)" value={consignorContact} onChange={setConsignorContact} placeholder="204-555-0113" />
        <TextField label="Consignee / recipient (optional)" value={consigneeName} onChange={setConsigneeName} />
        <TextField label="Consignee contact (optional)" value={consigneeContact} onChange={setConsigneeContact} />
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr 1fr", gap: 14, marginTop: 14 }}>
        <SelectField
          label="Bills to (optional · name snapshotted)"
          value={clientId}
          onChange={setClientId}
          options={[{ value: "", label: "— no client / walk-up —" }, ...(clients ?? []).map((c) => ({ value: c.id, label: c.name }))]}
        />
        <TextField label="PO (optional)" value={poNumber} onChange={setPoNumber} mono />
        <NumberField label="Charge (CAD, optional)" value={chargeCad} onChange={setChargeCad} min={0} step={0.01} />
        <SelectField
          label="Payment method"
          value={paymentMethod}
          onChange={(v) => setPaymentMethod(v as ShipmentPaymentMethod | "")}
          options={PAYMENT_METHODS.map((p) => ({ value: p.value, label: p.label }))}
        />
      </div>
    </ModalShell>
  );
}
