"use client";

import { colors, fonts, statusMeta } from "@/lib/theme";
import type { HosAlertsToday } from "@/lib/api/hos";
import { alertsTileSpec } from "@/lib/hos/compliance";

// The Dispatch Board's "Compliance alerts" tile, from GET /api/drivers/hos/alerts/today.
// MetricTile's look (components/ui/MetricTile.tsx — a copied design-system file, so not
// extended for this) plus the breakdown sub-line, as a real button. Colour + glyph + the
// breakdown text, never the tint alone. A failed fetch greys the tile to "—" like the
// invoices tile; it never blocks the board.

export function ComplianceAlertsTile({ alerts, failed, onOpen }: { alerts: HosAlertsToday | null; failed: boolean; onOpen: () => void }) {
  const spec = alerts && !failed ? alertsTileSpec(alerts) : null;
  const m = statusMeta(spec ? spec.kind : "off");
  const value = spec ? String(spec.total) : "—";
  const sub = failed ? "Unavailable" : spec ? spec.breakdown : "Loading…";

  return (
    <button
      type="button"
      onClick={onOpen}
      data-testid="compliance-alerts-tile"
      data-kind={spec?.kind ?? "off"}
      aria-label={`Compliance alerts: ${value === "—" ? "unavailable" : value}. ${sub}`}
      style={{
        appearance: "none",
        textAlign: "left",
        padding: "14px 15px",
        borderRadius: 11,
        background: colors.cardBg,
        border: `1px solid ${spec && spec.kind !== "ontime" ? m.bd : colors.borderSubtle}`,
        boxShadow: colors.shadowCard,
        cursor: "pointer",
        minWidth: 0,
      }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: 7, marginBottom: 9 }}>
        <span
          aria-hidden
          style={{
            width: 16,
            height: 16,
            borderRadius: 4,
            background: m.c,
            color: m.bt,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            fontSize: 10,
            fontWeight: 800,
          }}
        >
          {spec ? spec.glyph : m.g}
        </span>
        <span style={{ fontFamily: fonts.semiCondensed, fontSize: 10, letterSpacing: ".1em", textTransform: "uppercase", color: colors.textLabel }}>
          Compliance alerts
        </span>
      </div>
      <div
        data-testid="compliance-alerts-value"
        style={{
          fontFamily: fonts.condensed,
          fontWeight: 700,
          fontSize: 32,
          lineHeight: 1,
          color: spec ? m.t : colors.textDim,
          fontVariantNumeric: "tabular-nums",
        }}
      >
        {value}
      </div>
      <div
        data-testid="compliance-alerts-sub"
        style={{
          fontFamily: fonts.body,
          fontSize: 11,
          color: colors.textDim,
          marginTop: 6,
          whiteSpace: "nowrap",
          overflow: "hidden",
          textOverflow: "ellipsis",
        }}
        title={sub}
      >
        {sub}
      </div>
    </button>
  );
}
