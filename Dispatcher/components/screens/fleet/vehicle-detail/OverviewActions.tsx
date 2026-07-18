"use client";

import { colors, fonts } from "@/lib/theme";
import { canDispose, canTransition, changeVehicleStatus, type Vehicle } from "@/lib/api";
import { ActionButton } from "@/components/ui/Button";
import type { ModalKind, PromptKind } from "./shared";

export interface OverviewActionsProps {
  f: Vehicle;
  prompt: PromptKind;
  readOnly: boolean;
  retired: boolean;
  runAction: (fn: () => Promise<void>, opts?: { detectAutoRetire?: boolean }) => void;
  setPrompt: (p: PromptKind) => void;
  setReasonInput: (v: string) => void;
  setOdoInput: (v: string) => void;
  setPriceInput: (v: string) => void;
  setModal: (m: ModalKind) => void;
  setCertOpen: (b: boolean) => void;
}

// Lifecycle action buttons for the Overview tab, gated by the legal transition
// matrix (return to service, maintenance, out-of-service, odometer, edit, retire,
// and — once retired — view certificate / sell / recycle).
export default function OverviewActions({
  f,
  prompt,
  readOnly,
  retired,
  runAction,
  setPrompt,
  setReasonInput,
  setOdoInput,
  setPriceInput,
  setModal,
  setCertOpen,
}: OverviewActionsProps) {
  if (readOnly) {
    return (
      <div style={{ display: "flex", gap: 9, alignItems: "center", flexWrap: "wrap" }}>
        <ActionButton onClick={() => setCertOpen(true)}>VIEW CERTIFICATE</ActionButton>
        <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
          {f.status} — terminal state; record retained for compliance, no further actions.
        </span>
      </div>
    );
  }

  if (prompt !== null) return null;

  return (
    <div style={{ display: "flex", gap: 9, flexWrap: "wrap" }}>
      {canTransition(f.status, "Active") && (
        <ActionButton variant="success" onClick={() => runAction(() => changeVehicleStatus(f.id, "Active"))}>
          RETURN TO SERVICE
        </ActionButton>
      )}
      {canTransition(f.status, "InMaintenance") && (
        <ActionButton onClick={() => runAction(() => changeVehicleStatus(f.id, "InMaintenance"))}>
          SEND TO MAINTENANCE
        </ActionButton>
      )}
      {canTransition(f.status, "OutOfService") && (
        <ActionButton variant="destructive" onClick={() => { setReasonInput(""); setPrompt("oos"); }}>
          SET OUT OF SERVICE
        </ActionButton>
      )}
      {!retired && (
        <ActionButton onClick={() => { setOdoInput(String(f.odometerKm)); setPrompt("odometer"); }}>
          UPDATE ODOMETER
        </ActionButton>
      )}
      {!retired && <ActionButton onClick={() => setModal("edit")}>EDIT</ActionButton>}
      {canTransition(f.status, "Retired") && (
        <ActionButton variant="destructive" onClick={() => { setReasonInput(""); setPrompt("retire"); }}>
          RETIRE
        </ActionButton>
      )}
      {retired && (
        <>
          <ActionButton variant="primary" onClick={() => setCertOpen(true)}>
            VIEW CERTIFICATE
          </ActionButton>
          {canDispose(f.status) && (
            <>
              <ActionButton onClick={() => { setPriceInput(""); setPrompt("sell"); }}>SELL</ActionButton>
              <ActionButton variant="destructive" onClick={() => setPrompt("recycle")}>RECYCLE</ActionButton>
            </>
          )}
        </>
      )}
    </div>
  );
}
