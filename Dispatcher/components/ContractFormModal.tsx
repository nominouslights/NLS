"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  BILLING_FREQUENCY_LABELS,
  BILLING_MODEL_LABELS,
  createContract,
  updateContract,
  type ActiveContractSummary,
  type BillingFrequency,
  type BillingModel,
  type ContractInput,
} from "@/lib/api/clients";
import { ModalShell } from "@/components/ui/ModalShell";
import { DateField, NumberField, SelectField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

// Create or edit a client contract — POST/PUT /api/clients/{id}/contracts
// (Clients module). This is where the billing configuration is entered: the
// invoice draft generator feeds off the client contract on this profile
// (rate per round trip, GST, budget code, net terms, default PO). The backend
// enforces non-overlapping contract periods — a 409
// Clients.Contract.OverlappingPeriod surfaces inline here.

function ToggleRow({
  on,
  onToggle,
  title,
  hint,
}: {
  on: boolean;
  onToggle: () => void;
  title: string;
  hint: string;
}) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 11 }}>
      <span
        onClick={onToggle}
        style={{
          width: 40,
          height: 22,
          flex: "none",
          borderRadius: 999,
          background: on ? colors.blue : colors.borderStrong,
          position: "relative",
          cursor: "pointer",
          transition: "background .15s",
        }}
      >
        <span
          style={{
            position: "absolute",
            top: 2,
            left: on ? 20 : 2,
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
          {title}
        </div>
        <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>{hint}</div>
      </div>
    </div>
  );
}

export default function ContractFormModal({
  clientId,
  clientName,
  existing,
  onClose,
  onSaved,
}: {
  clientId: string;
  clientName: string;
  /** When set, edits this active contract (PUT); otherwise creates (POST). */
  existing?: ActiveContractSummary | null;
  onClose: () => void;
  /** The saved contract id + the submitted body (for refetchUntil predicates). */
  onSaved: (contractId: string, input: ContractInput) => void;
}) {
  const editing = existing != null;

  const [startDate, setStartDate] = useState(existing?.startDate ?? "");
  const [endDate, setEndDate] = useState(existing?.endDate ?? "");
  const [evergreen, setEvergreen] = useState(editing ? existing.endDate == null : false);
  const [billingModel, setBillingModel] = useState<BillingModel>(existing?.billingModel ?? "RoundTripRate");
  const [rate, setRate] = useState(existing?.ratePerRoundTripCad != null ? String(existing.ratePerRoundTripCad) : "");
  const [gstApplicable, setGstApplicable] = useState(existing?.gstApplicable ?? true);
  const [budgetCode, setBudgetCode] = useState(existing?.budgetCode ?? "");
  const [billingFrequency, setBillingFrequency] = useState<BillingFrequency>(existing?.billingFrequency ?? "Monthly");
  const [netTermsDays, setNetTermsDays] = useState(String(existing?.netTermsDays ?? 30));
  const [defaultPoNumber, setDefaultPoNumber] = useState(existing?.defaultPoNumber ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const roundTrip = billingModel === "RoundTripRate";

  async function submit() {
    if (busy) return;
    if (!startDate) return setError("Enter a contract start date.");
    if (!evergreen && !endDate) return setError("Enter an end date, or turn on \"Evergreen\" for an open-ended contract.");
    if (!evergreen && endDate < startDate) return setError("The end date must be on or after the start date.");
    const rateNum = rate === "" ? null : Number(rate);
    if (roundTrip && (rateNum == null || Number.isNaN(rateNum) || rateNum <= 0)) {
      return setError("Enter the rate per round trip (CAD) for per-round-trip billing.");
    }
    const terms = Number(netTermsDays);
    if (!Number.isInteger(terms) || terms < 0) return setError("Net terms must be a whole number of days.");

    const input: ContractInput = {
      startDate,
      endDate: evergreen ? null : endDate,
      billingModel,
      ratePerRoundTripCad: roundTrip ? rateNum : null,
      gstApplicable,
      budgetCode: budgetCode.trim() || null,
      billingFrequency,
      netTermsDays: terms,
      defaultPoNumber: defaultPoNumber.trim() || null,
    };

    setBusy(true);
    setError(null);
    try {
      if (editing) {
        await updateContract(clientId, existing.activeContractId, input);
        onSaved(existing.activeContractId, input);
      } else {
        const newId = await createContract(clientId, input);
        onSaved(newId, input);
      }
      onClose();
    } catch (e) {
      if (e instanceof ApiError) {
        setError(
          e.code === "Clients.Contract.OverlappingPeriod"
            ? `${e.message} Adjust the dates — contract periods for a client cannot overlap.`
            : e.message,
        );
      } else {
        setError("Failed to save the contract — please try again.");
      }
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Business · ${clientName} · Contract`}
      title={editing ? "Edit Contract" : "New Contract"}
      onClose={onClose}
      error={error}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "SAVING…" : editing ? "SAVE CONTRACT" : "CREATE CONTRACT"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <DateField label="Start date" value={startDate} onChange={setStartDate} />
        <DateField label="End date" value={endDate} onChange={setEndDate} disabled={evergreen} />
        <SelectField
          label="Billing model"
          value={billingModel}
          onChange={(v) => setBillingModel(v as BillingModel)}
          options={(Object.keys(BILLING_MODEL_LABELS) as BillingModel[]).map((m) => ({
            value: m,
            label: BILLING_MODEL_LABELS[m],
          }))}
        />
        <NumberField
          label="Rate per round trip (CAD)"
          value={rate}
          onChange={setRate}
          min={0}
          step={0.01}
          placeholder="128.00"
          disabled={!roundTrip}
          hint={roundTrip ? undefined : <span style={{ color: colors.textFaint }}>— manual billing</span>}
        />
        <SelectField
          label="Billing frequency"
          value={billingFrequency}
          onChange={(v) => setBillingFrequency(v as BillingFrequency)}
          options={(Object.keys(BILLING_FREQUENCY_LABELS) as BillingFrequency[]).map((f) => ({
            value: f,
            label: BILLING_FREQUENCY_LABELS[f],
          }))}
        />
        <NumberField label="Net terms (days)" value={netTermsDays} onChange={setNetTermsDays} min={0} step={1} />
        <TextField label="Budget code (optional)" value={budgetCode} onChange={setBudgetCode} mono placeholder="ZBB-CREW-01" />
        <TextField label="Default PO number (optional)" value={defaultPoNumber} onChange={setDefaultPoNumber} mono placeholder="PO-AG-2310" />
      </div>

      <div style={{ marginTop: 18, display: "flex", flexDirection: "column", gap: 14 }}>
        <ToggleRow
          on={evergreen}
          onToggle={() => setEvergreen((v) => !v)}
          title="Evergreen (no end date)"
          hint="Open-ended contract — renewal chips derive from the end date, so evergreen contracts never flag."
        />
        <ToggleRow
          on={gstApplicable}
          onToggle={() => setGstApplicable((v) => !v)}
          title="GST applicable"
          hint="5% GST line is added to draft invoices when on."
        />
      </div>
    </ModalShell>
  );
}
