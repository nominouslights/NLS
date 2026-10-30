import type { StatusKind } from "./theme";
import type {
  BookeoGroupAction,
  BookeoImportGroup,
  BookeoImportPreview,
  BookeoImportRow,
  BookeoImportSummary,
  BookeoRowAction,
  BookeoVehicleMatch,
  ImportIssue,
  ImportSeverity,
  ResidentStopRole,
} from "./api/bookeoImport";

// Pure derivations for the Bookeo import modal — the confirm button's state,
// the chip for each issue/action, and the rows-under-groups nesting. Kept out
// of the component so the rules are testable without rendering.

/** What a chip shows: always a status colour + an icon + a text label. */
export interface ChipSpec {
  kind: StatusKind;
  glyph: string;
  label: string;
}

// ---------------------------------------------------------------------------
// Issues
// ---------------------------------------------------------------------------

/** "Trips.BookeoImport.PaymentDue" → "PaymentDue". */
export function issueCodeSuffix(code: string): string {
  return code.slice(code.lastIndexOf(".") + 1);
}

/** "VehicleDoubleBooked" → "Vehicle double booked". */
export function humanizeCode(code: string): string {
  const words = issueCodeSuffix(code)
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/([A-Z])([A-Z][a-z])/g, "$1 $2")
    .toLowerCase();
  return words.charAt(0).toUpperCase() + words.slice(1);
}

const SEVERITY_CHIP: Record<ImportSeverity, { kind: StatusKind; glyph: string }> = {
  Block: { kind: "over", glyph: "▲" }, // vermillion
  Warning: { kind: "soon", glyph: "◐" }, // gold
  Info: { kind: "off", glyph: "i" }, // neutral
};

export function severityChip(severity: ImportSeverity): { kind: StatusKind; glyph: string } {
  return SEVERITY_CHIP[severity] ?? SEVERITY_CHIP.Info;
}

/** Block = vermillion, Warning = gold, Info = neutral — colour + icon + label. */
export function issueChip(issue: ImportIssue): ChipSpec {
  return { ...severityChip(issue.severity), label: humanizeCode(issue.code) };
}

const SEVERITY_RANK: Record<ImportSeverity, number> = { Block: 0, Warning: 1, Info: 2 };

/** Blocks first, then warnings, then info — the order the dispatcher must act in. */
export function sortIssues(issues: ImportIssue[]): ImportIssue[] {
  return [...issues].sort((a, b) => (SEVERITY_RANK[a.severity] ?? 3) - (SEVERITY_RANK[b.severity] ?? 3));
}

// ---------------------------------------------------------------------------
// Actions
// ---------------------------------------------------------------------------

const GROUP_ACTION_CHIP: Record<BookeoGroupAction, ChipSpec> = {
  Create: { kind: "ontime", glyph: "+", label: "Create" },
  Update: { kind: "info", glyph: "↻", label: "Update" },
  Unchanged: { kind: "off", glyph: "=", label: "Unchanged" },
  Cancel: { kind: "soon", glyph: "✕", label: "Cancel" },
  Blocked: { kind: "over", glyph: "▲", label: "Blocked" },
};

export function groupActionChip(action: BookeoGroupAction): ChipSpec {
  return GROUP_ACTION_CHIP[action] ?? { kind: "off", glyph: "?", label: action };
}

const ROW_ACTION_CHIP: Record<BookeoRowAction, ChipSpec> = {
  New: { kind: "ontime", glyph: "+", label: "New" },
  Changed: { kind: "info", glyph: "↻", label: "Changed" },
  Cancelled: { kind: "soon", glyph: "✕", label: "Cancelled" },
  Unchanged: { kind: "off", glyph: "=", label: "Unchanged" },
  Skipped: { kind: "off", glyph: "–", label: "Skipped" },
};

export function rowActionChip(action: BookeoRowAction): ChipSpec {
  return ROW_ACTION_CHIP[action] ?? { kind: "off", glyph: "?", label: action };
}

const VEHICLE_MATCH_CHIP: Record<BookeoVehicleMatch, ChipSpec> = {
  Matched: { kind: "ontime", glyph: "✓", label: "Matched" },
  KeptExisting: { kind: "ontime", glyph: "✓", label: "Kept existing" },
  Unmatched: { kind: "soon", glyph: "◐", label: "Unmatched" },
  Ambiguous: { kind: "soon", glyph: "◐", label: "Ambiguous" },
  Blank: { kind: "off", glyph: "—", label: "No unit" },
};

export function vehicleMatchChip(match: BookeoVehicleMatch): ChipSpec {
  return VEHICLE_MATCH_CHIP[match] ?? { kind: "off", glyph: "?", label: match };
}

// ---------------------------------------------------------------------------
// Confirm button
// ---------------------------------------------------------------------------

/** Groups whose Confirm actually writes something. */
const APPLICABLE: readonly BookeoGroupAction[] = ["Create", "Update", "Cancel"];

export function isApplicableGroup(g: BookeoImportGroup): boolean {
  return APPLICABLE.includes(g.action);
}

export interface ConfirmState {
  disabled: boolean;
  label: string;
  /** Why it is disabled, for the footer; null when enabled. */
  reason: string | null;
  applicable: number;
  blocked: number;
}

export function confirmState(preview: BookeoImportPreview | null, busy = false): ConfirmState {
  if (preview === null) {
    return { disabled: true, label: "Apply", reason: "Upload a Bookeo report to preview it.", applicable: 0, blocked: 0 };
  }
  const applicable = preview.groups.filter(isApplicableGroup).length;
  const blocked = preview.groups.filter((g) => g.action === "Blocked").length;
  const label =
    `Apply ${applicable} group${applicable === 1 ? "" : "s"}` +
    (blocked > 0 ? ` (${blocked} blocked group${blocked === 1 ? "" : "s"} will be skipped)` : "");

  const unmapped = preview.unmappedProducts.length;
  let reason: string | null = null;
  if (unmapped > 0) {
    reason = `Map ${unmapped} product${unmapped === 1 ? "" : "s"} to a route first.`;
  } else if (applicable === 0) {
    reason = blocked > 0 ? "Every group with changes is blocked." : "Nothing to apply — every booking is already up to date.";
  }
  return { disabled: busy || reason !== null, label, reason, applicable, blocked };
}

// ---------------------------------------------------------------------------
// Rows under groups
// ---------------------------------------------------------------------------

export interface GroupedRows {
  /** group key → its bookings, in the group's bookingNumbers order. */
  byGroup: Map<string, BookeoImportRow[]>;
  /** Rows not in any group (unmapped product, unreadable, canceled-never-imported). */
  ungrouped: BookeoImportRow[];
}

export function groupRows(preview: Pick<BookeoImportPreview, "groups" | "rows">): GroupedRows {
  const byNumber = new Map(preview.rows.map((r) => [r.bookingNumber, r]));
  const known = new Set(preview.groups.map((g) => g.key));
  const byGroup = new Map<string, BookeoImportRow[]>();
  const placed = new Set<BookeoImportRow>();

  for (const g of preview.groups) {
    const list: BookeoImportRow[] = [];
    // The group's own list is authoritative for order; fall back to the rows'
    // groupKey for any booking the list omits.
    for (const n of g.bookingNumbers) {
      const r = byNumber.get(n);
      if (r && !placed.has(r)) {
        list.push(r);
        placed.add(r);
      }
    }
    byGroup.set(g.key, list);
  }
  for (const r of preview.rows) {
    if (placed.has(r)) continue;
    if (r.groupKey !== null && known.has(r.groupKey)) {
      byGroup.get(r.groupKey)!.push(r);
      placed.add(r);
    }
  }
  const ungrouped = preview.rows.filter((r) => !placed.has(r));
  return { byGroup, ungrouped };
}

// ---------------------------------------------------------------------------
// Plan changes (after a 409 PreviewStale re-preview)
// ---------------------------------------------------------------------------

const SUMMARY_LABELS: [keyof BookeoImportSummary, string][] = [
  ["tripsToCreate", "trips to create"],
  ["tripsToUpdate", "trips to update"],
  ["tripsToCancel", "trips to cancel"],
  ["blockedGroups", "blocked groups"],
  ["new", "new bookings"],
  ["changed", "changed bookings"],
  ["cancelled", "cancelled bookings"],
  ["unchanged", "unchanged bookings"],
  ["warnings", "warnings"],
];

/** "trips to create 2 → 1" for every summary count that moved. */
export function summaryChanges(before: BookeoImportSummary, after: BookeoImportSummary): string[] {
  return SUMMARY_LABELS.filter(([k]) => before[k] !== after[k]).map(
    ([k, label]) => `${label} ${before[k]} → ${after[k]}`,
  );
}

// ---------------------------------------------------------------------------
// Mapping defaults
// ---------------------------------------------------------------------------

/** Shuttle *to* Thompson → residents are picked up; *from* Thompson → dropped off.
 *  A suggestion only — null when the product name says neither. */
export function suggestResidentRole(productName: string): ResidentStopRole | null {
  if (/\bfrom\b/i.test(productName)) return "Dropoff";
  if (/\bto\b/i.test(productName)) return "Pickup";
  return null;
}

/** Stable identity of an unmapped product (code + destination). */
export function productKey(p: { productCode: string; destination: string | null }): string {
  return `${p.productCode}|${p.destination ?? ""}`;
}

// ---------------------------------------------------------------------------
// Formatting
// ---------------------------------------------------------------------------

const cad2 = new Intl.NumberFormat("en-CA", { style: "currency", currency: "CAD", minimumFractionDigits: 2 });

export function formatCadCents(value: number): string {
  return cad2.format(value);
}

export function formatUtcDateTime(iso: string | null | undefined): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString("en-CA", {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}
