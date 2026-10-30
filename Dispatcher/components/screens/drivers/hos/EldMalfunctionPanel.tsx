"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import { listEldMalfunctions, reportEldMalfunction, resolveEldMalfunction, type EldMalfunctionRecord } from "@/lib/api/hos";
import { formatDateTimeUtc, isoOfUnix } from "@/lib/hos/display";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";
import { ModalShell } from "@/components/ui/ModalShell";
import { TextAreaField, TextField } from "@/components/ui/Field";
import { DateTimeFields, nowLocal, unixOf, type LocalDateTime } from "./DateTimeFields";

// ELD malfunction reports (GET/POST …/hos/eld-malfunctions). While one is open the driver
// is in paper-log mode — a warning at dispatch, never a block — and the gauge carries the
// ✎ chip. Resolving is compliance-only (POST …/resolve, ComplianceAccess).

export const PAPER_LOG_CHIP = { kind: "soon", glyph: "✎", label: "Paper-log mode" } as const;

export const ELD_CODE_MAX_LENGTH = 32;

export function EldMalfunctionPanel({
  driverId,
  driverName,
  version,
  canCompliance,
  onChanged,
}: {
  driverId: string;
  driverName: string;
  version: number;
  canCompliance: boolean;
  onChanged: () => void;
}) {
  const [state, setState] = useState<{ driverId: string; rows: EldMalfunctionRecord[] } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [modal, setModal] = useState<null | { kind: "report" } | { kind: "resolve"; row: EldMalfunctionRecord }>(null);

  useEffect(() => {
    let active = true;
    listEldMalfunctions(driverId).then(
      (rows) => {
        if (active) {
          setState({ driverId, rows });
          setError(null);
        }
      },
      (e) => {
        if (active) setError(e instanceof ApiError ? e.message : "Failed to load ELD malfunction reports.");
      },
    );
    return () => {
      active = false;
    };
  }, [driverId, version]);

  const rows = state?.driverId === driverId ? state.rows : null;

  return (
    <Panel style={{ marginBottom: 12 }}>
      <div style={{ display: "flex", alignItems: "center", marginBottom: 12 }}>
        <SectionLabel>ELD malfunctions</SectionLabel>
        <ActionButton variant="primary" style={{ marginLeft: "auto" }} onClick={() => setModal({ kind: "report" })}>
          + REPORT MALFUNCTION
        </ActionButton>
      </div>
      {error ? (
        <StatusChip kind="over" label={`ELD malfunctions unavailable — ${error}`} />
      ) : rows === null ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading ELD malfunction reports…</div>
      ) : rows.length === 0 ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>No ELD malfunction reports on file.</div>
      ) : (
        [...rows]
          .sort((a, b) => b.noticedAtUtc.localeCompare(a.noticedAtUtc))
          .map((row) => (
            <div key={row.id} data-testid="eld-malfunction-row" style={{ borderTop: `1px solid ${colors.borderSubtle}`, padding: "10px 0" }}>
              <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
                <span style={{ fontFamily: fonts.mono, fontWeight: 600, fontSize: 12.5, color: colors.textPrimary }}>{row.code}</span>
                {row.status === "Open" ? (
                  <StatusChip kind={PAPER_LOG_CHIP.kind} glyph={PAPER_LOG_CHIP.glyph} label="Open — paper-log mode" />
                ) : (
                  <StatusChip kind="ontime" label={`Resolved ${row.resolvedAtUtc ? formatDateTimeUtc(row.resolvedAtUtc) : ""}`.trim()} />
                )}
                <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textDim }}>Noticed {formatDateTimeUtc(row.noticedAtUtc)}</span>
                {canCompliance && row.status === "Open" && (
                  <span
                    onClick={() => setModal({ kind: "resolve", row })}
                    style={{ marginLeft: "auto", fontFamily: fonts.semiCondensed, fontWeight: 600, fontSize: 10, letterSpacing: ".08em", textTransform: "uppercase", color: colors.blue, cursor: "pointer" }}
                  >
                    Resolve
                  </span>
                )}
              </div>
              <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, marginTop: 5, lineHeight: 1.5 }}>
                {row.description}
                {row.resolutionNote && ` · Resolution: ${row.resolutionNote}`}
              </div>
            </div>
          ))
      )}

      {modal?.kind === "report" && <EldMalfunctionModal driverId={driverId} driverName={driverName} onClose={() => setModal(null)} onSaved={onChanged} />}
      {modal?.kind === "resolve" && (
        <ResolveEldMalfunctionModal driverId={driverId} driverName={driverName} report={modal.row} onClose={() => setModal(null)} onSaved={onChanged} />
      )}
    </Panel>
  );
}

export function EldMalfunctionModal({
  driverId,
  driverName,
  onClose,
  onSaved,
}: {
  driverId: string;
  driverName: string;
  onClose: () => void;
  onSaved: (record: EldMalfunctionRecord) => void;
}) {
  const [code, setCode] = useState("");
  const [noticedAt, setNoticedAt] = useState<LocalDateTime>(() => nowLocal());
  const [description, setDescription] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (busy) return;
    if (!code.trim()) return setError("Enter the malfunction code the ELD displayed.");
    const noticedUnix = unixOf(noticedAt);
    if (noticedUnix === null) return setError("Enter when the malfunction was noticed (date and local time).");
    if (!description.trim()) return setError("Describe the malfunction.");

    setBusy(true);
    setError(null);
    try {
      const record = await reportEldMalfunction(driverId, { code: code.trim(), noticedAtUtc: isoOfUnix(noticedUnix), description: description.trim() });
      onSaved(record);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to report the malfunction — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Operations · ${driverName} · Hours of Service`}
      title="Report ELD Malfunction"
      onClose={onClose}
      error={error}
      maxWidth={600}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "REPORTING…" : "REPORT MALFUNCTION"}
          </ActionButton>
        </>
      }
    >
      <div style={{ marginBottom: 14 }}>
        <StatusChip kind={PAPER_LOG_CHIP.kind} glyph={PAPER_LOG_CHIP.glyph} label="While open, the driver keeps a paper log — dispatch is warned, never blocked" />
      </div>
      <div style={{ marginBottom: 14 }}>
        <TextField label="Malfunction code" value={code} onChange={setCode} mono maxLength={ELD_CODE_MAX_LENGTH} placeholder="As displayed by the device" />
      </div>
      <div style={{ marginBottom: 14 }}>
        <DateTimeFields label="Noticed" value={noticedAt} onChange={setNoticedAt} />
      </div>
      <TextAreaField label="Description" value={description} onChange={setDescription} placeholder="What the device did, and what the driver was told" />
    </ModalShell>
  );
}

export function ResolveEldMalfunctionModal({
  driverId,
  driverName,
  report,
  onClose,
  onSaved,
}: {
  driverId: string;
  driverName: string;
  report: EldMalfunctionRecord;
  onClose: () => void;
  onSaved: (record: EldMalfunctionRecord) => void;
}) {
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      const record = await resolveEldMalfunction(driverId, report.id, note.trim() || null);
      onSaved(record);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to resolve the malfunction — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Operations · ${driverName} · Hours of Service`}
      title={`Resolve ELD Malfunction · ${report.code}`}
      onClose={onClose}
      error={error}
      maxWidth={520}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "RESOLVING…" : "RESOLVE"}
          </ActionButton>
        </>
      }
    >
      <TextAreaField label="Resolution note" value={note} onChange={setNote} placeholder="Repaired / replaced / reset — and the paper logs transcribed (optional)" />
    </ModalShell>
  );
}
