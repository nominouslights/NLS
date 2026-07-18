"use client";

import { colors, fonts } from "@/lib/theme";
import { changeVehicleStatus, disposeVehicle, formatKm, recordOdometer, type Vehicle } from "@/lib/api";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { ActionButton } from "@/components/ui/Button";
import { NumberField, TextField } from "@/components/ui/Field";
import type { PromptKind } from "./shared";

export interface OverviewPromptsProps {
  f: Vehicle;
  prompt: PromptKind;
  setPrompt: (p: PromptKind) => void;
  reasonInput: string;
  setReasonInput: (v: string) => void;
  odoInput: string;
  setOdoInput: (v: string) => void;
  priceInput: string;
  setPriceInput: (v: string) => void;
  busy: boolean;
  runAction: (fn: () => Promise<void>, opts?: { detectAutoRetire?: boolean }) => void;
}

// The inline confirm-prompts shown on the Overview tab (out-of-service, odometer,
// retire, sell, recycle). Exactly one renders at a time, driven by `prompt`.
export default function OverviewPrompts({
  f,
  prompt,
  setPrompt,
  reasonInput,
  setReasonInput,
  odoInput,
  setOdoInput,
  priceInput,
  setPriceInput,
  busy,
  runAction,
}: OverviewPromptsProps) {
  if (prompt === "oos") {
    return (
      <Panel borderColor="rgba(213,94,0,.4)" style={{ marginBottom: 12 }}>
        <SectionLabel>Set out of service — reason required</SectionLabel>
        <TextField label="Reason" value={reasonInput} onChange={setReasonInput} placeholder="e.g. Steering fault — critical" />
        <div style={{ display: "flex", gap: 9, marginTop: 12 }}>
          <ActionButton variant="destructive" onClick={() => runAction(() => changeVehicleStatus(f.id, "OutOfService", reasonInput.trim()))}>
            {busy ? "SAVING…" : "CONFIRM OUT OF SERVICE"}
          </ActionButton>
          <ActionButton onClick={() => setPrompt(null)}>CANCEL</ActionButton>
        </div>
      </Panel>
    );
  }

  if (prompt === "odometer") {
    return (
      <Panel style={{ marginBottom: 12 }}>
        <SectionLabel>Update odometer</SectionLabel>
        <NumberField
          label="Odometer (km)"
          value={odoInput}
          onChange={setOdoInput}
          min={f.odometerKm}
          step={1}
          placeholder={String(f.odometerKm)}
          hint={
            <span style={{ color: colors.textFaint }}>
              · current {formatKm(f.odometerKm)} — readings cannot decrease; crossing {formatKm(f.endOfLifeKm)} auto-retires the vehicle
            </span>
          }
        />
        <div style={{ display: "flex", gap: 9, marginTop: 12 }}>
          <ActionButton
            variant="primary"
            onClick={() => runAction(() => recordOdometer(f.id, parseInt(odoInput, 10)), { detectAutoRetire: true })}
          >
            {busy ? "SAVING…" : "RECORD READING"}
          </ActionButton>
          <ActionButton onClick={() => setPrompt(null)}>CANCEL</ActionButton>
        </div>
      </Panel>
    );
  }

  if (prompt === "retire") {
    return (
      <Panel borderColor="rgba(213,94,0,.4)" style={{ marginBottom: 12 }}>
        <SectionLabel>Retire vehicle — permanent</SectionLabel>
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6, marginBottom: 12 }}>
          Retiring {f.unitNumber} removes it from service permanently and issues a retirement
          certificate. A retired vehicle can only be sold or recycled — it cannot return to service.
        </div>
        <TextField
          label="Retirement reason (optional)"
          value={reasonInput}
          onChange={setReasonInput}
          placeholder="e.g. Fleet renewal — replaced by U-08"
        />
        <div style={{ display: "flex", gap: 9, marginTop: 12 }}>
          <ActionButton variant="destructive" onClick={() => runAction(() => changeVehicleStatus(f.id, "Retired", reasonInput.trim() || undefined))}>
            {busy ? "SAVING…" : "CONFIRM RETIREMENT"}
          </ActionButton>
          <ActionButton onClick={() => setPrompt(null)}>CANCEL</ActionButton>
        </div>
      </Panel>
    );
  }

  if (prompt === "sell") {
    return (
      <Panel style={{ marginBottom: 12 }}>
        <SectionLabel>Sell vehicle — terminal</SectionLabel>
        <NumberField
          label="Sale price (CAD)"
          value={priceInput}
          onChange={setPriceInput}
          min={0}
          step={100}
          placeholder="12500"
          hint={<span style={{ color: colors.textFaint }}>· recorded on the disposal record; proceeds-to-Budget-Code posting is a Billing follow-up</span>}
        />
        <div style={{ display: "flex", gap: 9, marginTop: 12 }}>
          <ActionButton variant="primary" onClick={() => runAction(() => disposeVehicle(f.id, "Sold", parseFloat(priceInput)))}>
            {busy ? "SAVING…" : "CONFIRM SALE"}
          </ActionButton>
          <ActionButton onClick={() => setPrompt(null)}>CANCEL</ActionButton>
        </div>
      </Panel>
    );
  }

  if (prompt === "recycle") {
    return (
      <Panel borderColor="rgba(213,94,0,.4)" style={{ marginBottom: 12 }}>
        <SectionLabel>Recycle vehicle — terminal</SectionLabel>
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6, marginBottom: 12 }}>
          Recycling {f.unitNumber} is permanent. The vehicle record is retained for compliance
          but the unit leaves the fleet for good.
        </div>
        <div style={{ display: "flex", gap: 9 }}>
          <ActionButton variant="destructive" onClick={() => runAction(() => disposeVehicle(f.id, "Recycled"))}>
            {busy ? "SAVING…" : "CONFIRM RECYCLE"}
          </ActionButton>
          <ActionButton onClick={() => setPrompt(null)}>CANCEL</ActionButton>
        </div>
      </Panel>
    );
  }

  return null;
}
