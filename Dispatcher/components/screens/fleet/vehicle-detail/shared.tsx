import type { CSSProperties } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { MonoTag } from "@/components/ui/Chip";

// Shared bits for the vehicle-detail sub-tree (tab list, prompt kinds, small
// presentational helpers) so Fleet.tsx, VehicleDetail, and OverviewTab agree.

export const TABS = [
  "Overview",
  "Documents & Compliance",
  "Service History",
  "Work Orders",
  "Inspections",
  "DTC Alerts",
  "Parts",
  "Fuel & Route",
];

export const tabIndex = (name: string) => {
  const i = TABS.indexOf(name);
  return i < 0 ? 0 : i;
};

/** One selectable vehicle: real id for API calls, unit + label for display. */
export interface VehicleOption {
  id: string;
  unit: string;
  label: string;
}

export type PromptKind = "oos" | "odometer" | "retire" | "sell" | "recycle" | null;

export type ModalKind = "register" | "edit" | "workorder" | "shops" | null;

export const errBadge: CSSProperties = {
  width: 22,
  height: 22,
  flex: "none",
  borderRadius: 6,
  background: "#D55E00",
  color: "#fff",
  display: "flex",
  alignItems: "center",
  justifyContent: "center",
  fontSize: 12,
  fontWeight: 800,
};

export const warnBanner: CSSProperties = {
  padding: "13px 15px",
  background: "rgba(213,94,0,.1)",
  border: "1px solid rgba(213,94,0,.4)",
  borderRadius: 10,
  marginBottom: 14,
  display: "flex",
  gap: 11,
  alignItems: "center",
};

export function PreviewCaption({ children }: { children?: string }) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
      <MonoTag color={statusMeta("soon").t}>MOCK</MonoTag>
      <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
        {children ?? "Preview — not yet wired to backend."}
      </span>
    </div>
  );
}

export function EmptyTabNote({ children }: { children: string }) {
  return (
    <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 2px" }}>
      {children}
    </div>
  );
}
