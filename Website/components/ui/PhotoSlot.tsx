import { chipStyle, colors, fonts } from "@/lib/theme";

// Branded placeholder for future real photography — no stock photos on this site.
// The `label` prop self-documents what photo belongs here when assets arrive.
export default function PhotoSlot({
  label,
  height = 220,
}: {
  label: string;
  height?: number;
}) {
  return (
    <div
      role="img"
      aria-label={`Placeholder image: ${label}`}
      style={{
        position: "relative",
        height,
        borderRadius: 12,
        border: `1px solid ${colors.border}`,
        background: "linear-gradient(135deg, #DFF2EB 0%, #FFFFFF 60%, #F2F8F5 100%)",
        overflow: "hidden",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        gap: 10,
        padding: 16,
      }}
    >
      <svg
        aria-hidden="true"
        style={{ position: "absolute", inset: 0, width: "100%", height: "100%", opacity: 0.35 }}
        preserveAspectRatio="none"
        viewBox="0 0 400 200"
      >
        <path d="M-20 170 L90 90 L180 130 L280 60 L420 120" fill="none" stroke={colors.teal} strokeOpacity="0.25" strokeWidth="2" />
        <path d="M-20 190 L110 120 L210 160 L320 90 L420 150" fill="none" stroke={colors.teal} strokeOpacity="0.15" strokeWidth="2" />
        <circle cx="280" cy="60" r="5" fill={colors.gold} fillOpacity="0.5" />
      </svg>
      <span style={chipStyle(colors.goldTint, colors.gold, "#7A6000")}>
        ◔ Photo coming soon
      </span>
      <span
        style={{
          position: "relative",
          fontFamily: fonts.semiCondensed,
          fontWeight: 600,
          fontSize: 13.5,
          color: colors.textMuted,
          textAlign: "center",
        }}
      >
        {label}
      </span>
    </div>
  );
}
