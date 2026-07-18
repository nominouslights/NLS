"use client";

import { colors, fonts, rowSurface } from "@/lib/theme";
import { isDisposed, statusKindFor, statusLabelFor, type Vehicle } from "@/lib/api";
import { StatusChip } from "@/components/ui/Chip";

// Left master column of the Fleet & Maintenance screen: a "Fleet overview" row
// (deselect → dashboard) followed by the vehicle registry rows.

export default function FleetMasterList({
  vehicles,
  selId,
  onSelect,
  onOverview,
}: {
  vehicles: Vehicle[];
  selId: string | null;
  onSelect: (id: string) => void;
  onOverview: () => void;
}) {
  return (
    <div style={{ minHeight: 0, overflowY: "auto", padding: "16px 18px", borderRight: `1px solid ${colors.border}` }}>
      <div
        onClick={onOverview}
        style={{
          display: "flex",
          alignItems: "center",
          gap: 10,
          padding: "10px 13px",
          marginBottom: 8,
          ...rowSurface(selId === null, colors.blue),
        }}
      >
        <span style={{ fontSize: 13, color: colors.skyBlue }}>▦</span>
        <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
          Fleet overview
        </span>
        <span style={{ marginLeft: "auto", fontFamily: fonts.body, fontSize: 11, color: colors.textDim }}>
          {vehicles.length} vehicles
        </span>
      </div>

      {vehicles.map((row) => {
        const active = selId !== null && row.id === selId;
        const rowDim = row.status === "OutOfService" || isDisposed(row.status);
        return (
          <div
            key={row.id}
            onClick={() => onSelect(row.id)}
            style={{
              display: "grid",
              gridTemplateColumns: "56px 1fr 130px",
              gap: 11,
              alignItems: "center",
              padding: "11px 13px",
              marginBottom: 5,
              opacity: rowDim ? 0.72 : 1,
              ...rowSurface(active, colors.blue),
            }}
          >
            <div
              style={{
                width: 56,
                height: 40,
                borderRadius: 8,
                background: colors.inputBg,
                border: `1px solid ${colors.border}`,
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                justifyContent: "center",
              }}
            >
              <span style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.skyBlue, fontWeight: 500 }}>
                {row.unitNumber}
              </span>
            </div>
            <div style={{ minWidth: 0 }}>
              <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
                {row.make} {row.model}
              </div>
              <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
                {row.seatingCapacity}-seat · {row.licencePlate}
              </div>
            </div>
            <div>
              <StatusChip kind={statusKindFor(row.status)} label={statusLabelFor(row.status)} />
            </div>
          </div>
        );
      })}
    </div>
  );
}
