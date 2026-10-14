"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import {
  openComplianceFinding,
  updateComplianceFinding,
  type ComplianceFindingRecord,
  type FindingSeverity,
  type FindingStatus,
} from "@/lib/api/hos";
import type { DriverRecord } from "@/lib/api/drivers";
import { FINDING_RULES, FINDING_SEVERITIES, FINDING_STATUS_CHIP, nextFindingStatuses } from "@/lib/hos/compliance";
import { ModalShell } from "@/components/ui/ModalShell";
import { ActionButton } from "@/components/ui/Button";
import { DateField, SelectField, TextAreaField, TextField } from "@/components/ui/Field";
import { SpecChip } from "./shared";

// Opening a finding (POST …/findings) and moving one forward (PUT …/findings/{id}):
// Open → Corrected → Closed, or Open → Closed, never backwards — the server enforces it and
// its message is shown verbatim; this modal only offers the forward moves.

const OTHER_RULE = "__other";

export function OpenFindingModal({
  roster,
  today,
  initialDriverId,
  onClose,
  onSaved,
}: {
  roster: DriverRecord[];
  today: string;
  initialDriverId?: string;
  onClose: () => void;
  onSaved: (record: ComplianceFindingRecord) => void;
}) {
  const [driverId, setDriverId] = useState(initialDriverId ?? roster[0]?.id ?? "");
  const [localDate, setLocalDate] = useState(today);
  const [rule, setRule] = useState<string>(FINDING_RULES[0]);
  const [otherRule, setOtherRule] = useState("");
  const [severity, setSeverity] = useState<FindingSeverity>("Minor");
  const [description, setDescription] = useState("");
  const [followUpNote, setFollowUpNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (busy) return;
    if (!driverId) return setError("Choose the driver the finding is about.");
    if (!localDate) return setError("Enter the date the finding concerns.");
    const ruleName = rule === OTHER_RULE ? otherRule.trim() : rule;
    if (!ruleName) return setError("Name the rule the finding concerns.");
    if (!description.trim()) return setError("Describe the finding.");

    setBusy(true);
    setError(null);
    try {
      const record = await openComplianceFinding(driverId, {
        localDate,
        rule: ruleName,
        severity,
        description: description.trim(),
        followUpNote: followUpNote.trim() || null,
      });
      onSaved(record);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to open the finding — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow="Operations · Compliance · Findings"
      title="Open Finding"
      onClose={onClose}
      error={error}
      maxWidth={600}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "OPENING…" : "OPEN FINDING"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14, marginBottom: 14 }}>
        <SelectField label="Driver" value={driverId} onChange={setDriverId} options={roster.map((d) => ({ value: d.id, label: d.name }))} />
        <DateField label="Date" value={localDate} onChange={setLocalDate} />
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14, marginBottom: 14 }}>
        <SelectField
          label="Rule"
          value={rule}
          onChange={setRule}
          options={[...FINDING_RULES.map((r) => ({ value: r, label: r })), { value: OTHER_RULE, label: "Other (free text)…" }]}
        />
        <SelectField
          label="Severity"
          value={severity}
          onChange={(v) => setSeverity(v as FindingSeverity)}
          options={FINDING_SEVERITIES.map((s) => ({ value: s, label: s }))}
        />
      </div>
      {rule === OTHER_RULE && (
        <div style={{ marginBottom: 14 }}>
          <TextField label="Rule (free text)" value={otherRule} onChange={setOtherRule} placeholder="The rule or obligation breached" />
        </div>
      )}
      <div style={{ marginBottom: 14 }}>
        <TextAreaField label="Description" value={description} onChange={setDescription} placeholder="What the audit or enforcement found" />
      </div>
      <TextAreaField label="Follow-up note" value={followUpNote} onChange={setFollowUpNote} placeholder="Corrective action planned (optional)" />
    </ModalShell>
  );
}

export function UpdateFindingModal({
  finding,
  driverName,
  onClose,
  onSaved,
}: {
  finding: ComplianceFindingRecord;
  driverName: string;
  onClose: () => void;
  onSaved: (record: ComplianceFindingRecord) => void;
}) {
  const moves = nextFindingStatuses(finding.status);
  const [status, setStatus] = useState<FindingStatus | "">(moves[0] ?? "");
  const [note, setNote] = useState(finding.followUpNote ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (busy || !status) return;
    setBusy(true);
    setError(null);
    try {
      const record = await updateComplianceFinding(finding.driverId, finding.id, { status, followUpNote: note.trim() || null });
      onSaved(record);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to update the finding — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Operations · ${driverName} · Compliance`}
      title={`Finding · ${finding.rule}`}
      onClose={onClose}
      error={error}
      maxWidth={560}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          {moves.length > 0 && (
            <ActionButton variant="primary" onClick={submit} disabled={busy}>
              {busy ? "SAVING…" : "UPDATE FINDING"}
            </ActionButton>
          )}
        </>
      }
    >
      <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12, flexWrap: "wrap" }}>
        <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>Now</span>
        <SpecChip spec={FINDING_STATUS_CHIP[finding.status]} />
        <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted }}>
          {finding.localDate} · {finding.severity} · {finding.source}
        </span>
      </div>
      <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textPrimary, lineHeight: 1.55, marginBottom: 14 }}>{finding.description}</div>
      {moves.length === 0 ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>A closed finding is final — it cannot move again.</div>
      ) : (
        <>
          <div style={{ marginBottom: 14 }}>
            <SelectField
              label="Move to"
              value={status}
              onChange={(v) => setStatus(v as FindingStatus)}
              options={moves.map((m) => ({ value: m, label: m }))}
            />
          </div>
          <TextAreaField label="Follow-up note" value={note} onChange={setNote} placeholder="What was done about it" />
        </>
      )}
    </ModalShell>
  );
}
