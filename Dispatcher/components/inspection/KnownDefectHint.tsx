"use client";

import { colors, fonts } from "@/lib/theme";
import { formatUtcDate } from "@/lib/api/format";
import { normaliseDefectItem } from "@/lib/api/maintenance";
import { openReports, type KnownDefect } from "@/lib/defectDuplicates";
import type { OpenDefectsLoad } from "@/lib/useVehicleOpenDefects";
import { DEFECT_SEVERITY_LABEL, DEFECT_SEVERITY_META, worstSeverity } from "@/lib/workOrderDisplay";
import { StatusChip } from "@/components/ui/Chip";

// "This component already has an open defect on this unit" — beside a checklist
// row on the pre/post-trip forms, so a dispatcher transcribing a DVIR reads a
// known fault as known rather than new.
//
// INFORMATIONAL ONLY. It never ticks, fills, blocks or changes a row: the driver
// still marks Defect / OK themselves, and a fresh Defect mark on a known fault is
// correct NSC practice — it is exactly the duplicate a repair clears with every
// other report of that component. Built on the projection (≈5s behind), which is
// fine for a hint and is why nothing here is a promise about what a resolve will
// clear — ResolveDefectModal asks the server for that. No prop here reports "has
// defects" back up; same warn-only rule as DefectsPanel.

const dim = {
  fontFamily: fonts.body,
  fontSize: 11.5,
  color: colors.textDim,
  lineHeight: 1.45,
} as const;

/** One compact line: severity chip + "Open defect since <date> · <trip>" (+ "+N more
 *  reports"). Wraps only when the row is too narrow. */
export function KnownDefectHint({ known }: { known: KnownDefect | undefined }) {
  if (!known) return null;
  const meta = DEFECT_SEVERITY_META[known.worst.severity] ?? DEFECT_SEVERITY_META.Major;
  const more = known.count - 1;
  return (
    <div
      data-testid="known-defect-hint"
      style={{ display: "flex", alignItems: "center", gap: 7, flexWrap: "wrap", marginTop: 5 }}
    >
      <StatusChip
        kind={meta.kind}
        glyph={meta.glyph}
        label={DEFECT_SEVERITY_LABEL[known.worst.severity] ?? known.worst.severity}
      />
      <span style={dim}>
        Open defect since {formatUtcDate(known.first.reportedAt)}
        {known.first.tripNumber ? ` · ${known.first.tripNumber}` : ""}
        {more > 0 ? ` · +${more} more report${more === 1 ? "" : "s"}` : ""}
      </span>
    </div>
  );
}

/**
 * The line at the top of the form: "<unit> has N open defects", or that it has
 * none, or — failing soft — one dim line saying the check could not be made.
 * Never a block on saving. `formItemKeys` are the item keys the form shows (rows
 * and pickable items); an open defect on anything else is named here, since no
 * row will carry its hint.
 */
export function KnownDefectsSummary({
  unit,
  load,
  excludeInspectionId,
  formItemKeys,
}: {
  unit: string;
  load: OpenDefectsLoad;
  excludeInspectionId?: string | null;
  formItemKeys: readonly string[];
}) {
  if (!unit) return null;
  if (load.status === "none") return null;
  if (load.status === "loading") {
    return <div style={{ ...dim, marginBottom: 12 }}>Checking {unit} for open defects…</div>;
  }
  if (load.status === "error") {
    return (
      <div style={{ ...dim, marginBottom: 12 }} data-testid="known-defects-error">
        Couldn&apos;t load {unit}&apos;s open defects — known faults are not marked on the rows below.
      </div>
    );
  }

  const open = openReports(load.rows, excludeInspectionId);
  const worst = worstSeverity(open);
  if (worst === null) {
    return (
      <div style={{ marginBottom: 12 }}>
        <StatusChip kind="ontime" label={`No open defects on ${unit}`} />
      </div>
    );
  }

  const meta = DEFECT_SEVERITY_META[worst];
  const components = new Set(open.map((d) => normaliseDefectItem(d.item)));
  const onForm = new Set(formItemKeys.map(normaliseDefectItem));
  // Server order, first spelling of each component — never re-sorted.
  const offForm: string[] = [];
  const seen = new Set<string>();
  for (const d of open) {
    const key = normaliseDefectItem(d.item);
    if (onForm.has(key) || seen.has(key)) continue;
    seen.add(key);
    offForm.push(d.item.trim());
  }

  return (
    <div style={{ display: "flex", alignItems: "center", gap: 9, flexWrap: "wrap", marginBottom: 12 }}>
      <StatusChip
        kind={meta.kind}
        glyph={meta.glyph}
        label={`${unit} has ${open.length} open defect${open.length === 1 ? "" : "s"}${
          components.size < open.length ? ` on ${components.size} component${components.size === 1 ? "" : "s"}` : ""
        }`}
      />
      <span style={dim}>
        Known faults are marked on their rows — still mark each row as the driver found it.
        {offForm.length > 0 ? ` Not on any row here: ${offForm.join(", ")}.` : ""}
      </span>
    </div>
  );
}
