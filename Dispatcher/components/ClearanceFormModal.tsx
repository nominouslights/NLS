"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import { grantDriverClearance } from "@/lib/api/drivers";
import { listClients } from "@/lib/api/clients";
import { ModalShell } from "@/components/ui/ModalShell";
import { DateField, SelectField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

// Grant a client clearance for a driver (site badge, contractor clearance
// list, etc — a client relationship grant, not a licensing/regulatory
// credential) — POST /api/drivers/{driverId}/clearances (Drivers module).
// The wire contract is { title, clientName, expiry? }; the backend stamps
// grantedAtUtc. The client dropdown reads the real Clients API roster;
// clientName is free text on the wire, so if the roster can't load the
// field falls back to free-text entry.

export default function ClearanceFormModal({
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
  const [title, setTitle] = useState("");
  const [client, setClient] = useState("");
  const [clientNames, setClientNames] = useState<string[] | null>(null);
  const [rosterFailed, setRosterFailed] = useState(false);
  const [expiry, setExpiry] = useState("");
  const [noExpiry, setNoExpiry] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    listClients().then(
      (rows) => {
        if (!active) return;
        const names = rows.map((c) => c.name);
        setClientNames(names);
        setClient((prev) => prev || names[0] || "");
      },
      () => {
        if (active) setRosterFailed(true); // free-text fallback below
      },
    );
    return () => {
      active = false;
    };
  }, []);

  async function submit() {
    if (busy) return;
    if (!title.trim()) return setError("Enter a title for the clearance (e.g. \"Contractor Site Clearance\").");
    if (!client) return setError("Select a client.");
    if (!noExpiry && !expiry) return setError("Enter an expiry date, or turn on \"No renewal required\".");

    setBusy(true);
    setError(null);
    try {
      const newId = await grantDriverClearance(driverId, {
        title: title.trim(),
        clientName: client,
        expiry: noExpiry ? null : expiry,
      });
      onSaved(newId);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to grant the clearance — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Operations · ${driverName} · Clearances`}
      title="Add Clearance"
      onClose={onClose}
      error={error}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} style={busy ? { opacity: 0.6, cursor: "wait" } : undefined}>
            {busy ? "ADDING…" : "ADD CLEARANCE"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <TextField label="Title" value={title} onChange={setTitle} placeholder="Contractor Site Clearance" />
        {rosterFailed ? (
          <TextField label="Client" value={client} onChange={setClient} placeholder="Client name" />
        ) : (
          <SelectField
            label="Client"
            value={client}
            onChange={setClient}
            options={
              clientNames === null
                ? [{ value: "", label: "Loading clients…" }]
                : clientNames.map((name) => ({ value: name, label: name }))
            }
            disabled={clientNames === null}
          />
        )}
        <DateField label="Expiry" value={expiry} onChange={setExpiry} disabled={noExpiry} />
      </div>

      {/* no-renewal toggle */}
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
            No renewal required
          </div>
          <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
            Turn off to set an expiry the clearance needs renewing by.
          </div>
        </div>
      </div>
    </ModalShell>
  );
}
