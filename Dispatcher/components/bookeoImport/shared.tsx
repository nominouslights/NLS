"use client";

import type { CSSProperties, ReactNode } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { StatusChip } from "@/components/ui/Chip";
import { issueChip, sortIssues, type ChipSpec } from "@/lib/bookeoImport";
import type { TripDirection } from "@/lib/api/trips";
import type { ImportIssue, ResidentStopRole } from "@/lib/api/bookeoImport";
import type { RouteRecord } from "@/lib/api/trips";
import type { Vehicle } from "@/lib/api/fleet";

// Pieces shared by the Bookeo import modal's tabs: the chip renderer, the
// issue list, the compact select, and the option lists for the pickers.

export function SpecChip({ spec }: { spec: ChipSpec }) {
  return <StatusChip kind={spec.kind} glyph={spec.glyph} label={spec.label} />;
}

/** Compact chips — one per issue, label from the code, full message on hover. */
export function IssueChips({ issues }: { issues: ImportIssue[] }) {
  if (issues.length === 0) return <span style={{ color: colors.textFaint }}>—</span>;
  return (
    <span style={{ display: "inline-flex", flexWrap: "wrap", gap: 4 }}>
      {sortIssues(issues).map((i, n) => (
        <span key={`${i.code}-${n}`} title={i.message}>
          <SpecChip spec={issueChip(i)} />
        </span>
      ))}
    </span>
  );
}

/** Full-sentence issue rows (colour + icon + label + the backend's message). */
export function IssueList({ issues }: { issues: ImportIssue[] }) {
  if (issues.length === 0) return null;
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 5 }}>
      {sortIssues(issues).map((i, n) => {
        const spec = issueChip(i);
        return (
          <div key={`${i.code}-${n}`} style={{ display: "flex", alignItems: "flex-start", gap: 8 }}>
            <SpecChip spec={spec} />
            <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textSecondary, lineHeight: 1.6 }}>
              {i.message}
            </span>
          </div>
        );
      })}
    </div>
  );
}

/** A banner row — always icon + text + colour (PassengerCsvImport's NoticeRow idiom). */
export function Notice({
  kind,
  children,
  details,
}: {
  kind: "ontime" | "soon" | "over" | "off";
  children: ReactNode;
  details?: string[];
}) {
  const m = statusMeta(kind);
  return (
    <div
      role="status"
      style={{
        display: "flex",
        gap: 9,
        padding: "9px 12px",
        background: m.bg,
        border: `1px solid ${m.bd}`,
        borderRadius: 9,
      }}
    >
      <span style={{ color: m.t, fontSize: 12, fontWeight: 800, flex: "none" }}>{m.g}</span>
      <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: m.t, fontWeight: 600, lineHeight: 1.5 }}>
        {children}
        {details && details.length > 0 && (
          <ul style={{ margin: "4px 0 0", paddingLeft: 18, fontWeight: 500 }}>
            {details.map((d) => (
              <li key={d}>{d}</li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

const selectBase: CSSProperties = {
  width: "100%",
  height: 34,
  boxSizing: "border-box",
  borderRadius: 8,
  background: colors.inputBg,
  border: `1px solid ${colors.borderStrong}`,
  padding: "0 10px",
  fontFamily: fonts.body,
  fontSize: 12.5,
  color: colors.textPrimary,
  outline: "none",
};

/** A labelled native select sized for table rows (SelectField is form-height). */
export function CompactSelect({
  label,
  value,
  onChange,
  options,
  disabled = false,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  options: { value: string; label: string }[];
  disabled?: boolean;
}) {
  return (
    <label style={{ display: "flex", flexDirection: "column", gap: 4, minWidth: 0 }}>
      <span style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textLabel }}>{label}</span>
      <select
        className="nl-input"
        aria-label={label}
        value={value}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
        style={{ ...selectBase, cursor: disabled ? "not-allowed" : "pointer", opacity: disabled ? 0.55 : 1 }}
      >
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
    </label>
  );
}

export const DIRECTION_OPTIONS: { value: "" | TripDirection; label: string }[] = [
  { value: "", label: "Not set" },
  { value: "Outbound", label: "Outbound" },
  { value: "Inbound", label: "Inbound (return)" },
];

export const ROLE_OPTIONS: { value: "" | ResidentStopRole; label: string }[] = [
  { value: "", label: "Choose…" },
  { value: "Pickup", label: "Pickup" },
  { value: "Dropoff", label: "Dropoff" },
];

export const RESIDENT_ROLE_HELP =
  "Shuttle to Thompson → residents are picked up; from Thompson → dropped off.";

export function routeOptions(routes: RouteRecord[], keepId?: string): { value: string; label: string }[] {
  return [
    { value: "", label: routes.length === 0 ? "No routes loaded" : "Choose a route…" },
    ...routes
      .filter((r) => r.active || r.id === keepId)
      .map((r) => ({ value: r.id, label: `${r.name} · ${r.origin} → ${r.destination}` })),
  ];
}

const DISPOSED = new Set(["Retired", "Sold", "Recycled"]);

export function vehicleOptions(vehicles: Vehicle[], keepId?: string): { value: string; label: string }[] {
  return [
    { value: "", label: vehicles.length === 0 ? "No vehicles loaded" : "Choose a vehicle…" },
    ...vehicles
      .filter((v) => !DISPOSED.has(v.status) || v.id === keepId)
      .map((v) => ({
        value: v.id,
        label: `${v.unitNumber} · ${v.make} ${v.model} · ${v.seatingCapacity} seats${v.status === "Active" ? "" : ` (${v.status})`}`,
      })),
  ];
}

export const th: CSSProperties = {
  fontFamily: fonts.semiCondensed,
  fontSize: 9.5,
  letterSpacing: ".12em",
  textTransform: "uppercase",
  color: colors.textLabel,
  textAlign: "left",
  padding: "0 8px 6px",
  whiteSpace: "nowrap",
};

export const td: CSSProperties = {
  fontFamily: fonts.body,
  fontSize: 12.5,
  color: colors.textPrimary,
  padding: "8px 8px",
  borderTop: `1px solid ${colors.borderSubtle}`,
  verticalAlign: "top",
};

export const mono: CSSProperties = { fontFamily: fonts.mono, fontSize: 11.5 };

export const muted: CSSProperties = { fontFamily: fonts.body, fontSize: 12, color: colors.textDim, lineHeight: 1.5 };
