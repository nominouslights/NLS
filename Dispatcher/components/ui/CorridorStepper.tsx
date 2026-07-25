import { colors, fonts } from "@/lib/theme";

export function CorridorStepper({ stops }: { stops: string[] }) {
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 0,
        padding: "14px 16px",
        background: colors.cardBg,
        border: `1px solid ${colors.borderSubtle}`,
        borderRadius: 10,
        boxShadow: colors.shadowCard,
        marginBottom: 14,
        overflowX: "auto",
      }}
    >
      {stops.map((name, ix) => (
        <div key={`${name}-${ix}`} style={{ display: "flex", alignItems: "center", gap: 9, flex: "none" }}>
          {ix > 0 && <span style={{ color: colors.textFaint, fontSize: 16, margin: "0 4px" }}>→</span>}
          <span
            style={{
              width: 9,
              height: 9,
              borderRadius: "50%",
              background: colors.blue,
              flex: "none",
              boxShadow: "0 0 0 3px rgba(31,111,178,.18)",
            }}
          />
          <span style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textSecondary, whiteSpace: "nowrap" }}>
            {name}
          </span>
        </div>
      ))}
    </div>
  );
}
