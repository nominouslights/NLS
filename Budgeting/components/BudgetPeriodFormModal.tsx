"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import {
  createBudgetPeriod,
  listBudgetPeriods,
  previewPeriod,
  refetchUntil,
  MONTH_NAMES,
  type BudgetPeriodRecord,
  type PeriodGranularity,
} from "@/lib/api/budgeting";
import { ModalShell } from "@/components/ui/ModalShell";
import { NumberField, SelectField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

// Create-only modal for a new budget period — the app's first real mutation.
// The planner picks granularity + year + which month/quarter; the server
// derives the date range and label, so the preview line here is display-only
// (previewPeriod mirrors the derivation but never crosses the wire). A 409
// from the overlap rule surfaces as the backend's own message.

const QUARTER_OPTIONS = [1, 2, 3, 4].map((q) => ({ value: String(q), label: `Q${q}` }));
const MONTH_OPTIONS = MONTH_NAMES.map((name, i) => ({ value: String(i + 1), label: name }));

export default function BudgetPeriodFormModal({
  onClose,
  onSaved,
}: {
  onClose: () => void;
  /** Fresh list (already containing the new row) plus the new period's id. */
  onSaved: (records: BudgetPeriodRecord[], id: string) => void;
}) {
  const [granularity, setGranularity] = useState<PeriodGranularity>("Quarter");
  const [year, setYear] = useState(String(new Date().getFullYear()));
  const [ordinal, setOrdinal] = useState("1");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const preview = previewPeriod(granularity, Number(year), Number(ordinal));

  function switchGranularity(v: string) {
    setGranularity(v as PeriodGranularity);
    setOrdinal("1"); // an ordinal valid for one granularity is meaningless in the other
  }

  async function submit() {
    if (busy) return;
    if (!preview) return setError("Pick a year between 2020 and 2100.");

    setBusy(true);
    setError(null);

    try {
      const id = await createBudgetPeriod({
        granularity,
        year: Number(year),
        ordinal: Number(ordinal),
      });
      const records = await refetchUntil(listBudgetPeriods, (rows) =>
        rows.some((r) => r.id === id),
      );
      onSaved(records, id);
      onClose();
    } catch (e) {
      setError(
        e instanceof ApiError ? e.message : "Failed to create the period — please try again.",
      );
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow="Planning · Budget Periods"
      title="New Period"
      onClose={onClose}
      error={error}
      maxWidth={560}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "CREATING…" : "CREATE PERIOD"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 14 }}>
        <SelectField
          label="Granularity"
          value={granularity}
          onChange={switchGranularity}
          options={[
            { value: "Quarter", label: "Quarter" },
            { value: "Month", label: "Month" },
          ]}
        />
        <NumberField label="Year" value={year} onChange={setYear} min={2020} step={1} />
        <SelectField
          label={granularity === "Quarter" ? "Quarter" : "Month"}
          value={ordinal}
          onChange={setOrdinal}
          options={granularity === "Quarter" ? QUARTER_OPTIONS : MONTH_OPTIONS}
        />
      </div>

      {/* Live preview of what the server will derive. ISO dates on purpose —
          unambiguous, and not a place to re-import display formatting. */}
      <div
        style={{
          marginTop: 16,
          fontFamily: fonts.body,
          fontSize: 12,
          color: colors.textDim,
        }}
      >
        Creates:{" "}
        {preview ? (
          <span style={{ fontFamily: fonts.mono, color: colors.textSecondary }}>
            {preview.label} · {preview.startsOn} — {preview.endsOn}
          </span>
        ) : (
          <span>enter a year between 2020 and 2100</span>
        )}
      </div>
      <div
        style={{
          marginTop: 6,
          fontFamily: fonts.body,
          fontSize: 11.5,
          color: colors.textDim,
        }}
      >
        New periods start as drafts. Zero-based means allocations are entered fresh — nothing
        carries forward from an earlier period.
      </div>
    </ModalShell>
  );
}
