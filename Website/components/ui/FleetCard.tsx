import type { FleetVehicle } from "@/lib/types";
import { bodyStyle, cardStyle, chipStyle, colors, fonts } from "@/lib/theme";

function CoachSilhouette() {
  return (
    <svg viewBox="0 0 320 120" role="img" aria-label="Coach bus silhouette" style={{ width: "100%", height: "auto", display: "block" }}>
      <rect x="14" y="26" width="292" height="58" rx="10" fill={colors.tealDark} />
      <rect x="14" y="26" width="292" height="14" rx="7" fill={colors.teal} />
      {[34, 74, 114, 154, 194, 234].map((x) => (
        <rect key={x} x={x} y="42" width="28" height="18" rx="3" fill="#DFF2EB" />
      ))}
      <rect x="272" y="42" width="22" height="34" rx="3" fill="#DFF2EB" />
      <circle cx="70" cy="92" r="14" fill={colors.ink} />
      <circle cx="70" cy="92" r="6" fill="#DCE7E2" />
      <circle cx="248" cy="92" r="14" fill={colors.ink} />
      <circle cx="248" cy="92" r="6" fill="#DCE7E2" />
      <rect x="14" y="70" width="292" height="6" fill={colors.gold} />
    </svg>
  );
}

function VanSilhouette() {
  return (
    <svg viewBox="0 0 320 120" role="img" aria-label="Passenger van silhouette" style={{ width: "100%", height: "auto", display: "block" }}>
      <path
        d="M50 40 Q56 30 70 30 L230 30 Q248 30 258 44 L272 62 Q276 68 276 74 L276 82 Q276 88 270 88 L56 88 Q50 88 50 82 Z"
        fill={colors.tealDark}
      />
      <path d="M50 40 Q56 30 70 30 L230 30 Q240 30 246 36 L52 36 Z" fill={colors.teal} />
      {[78, 118, 158, 198].map((x) => (
        <rect key={x} x={x} y="42" width="30" height="16" rx="3" fill="#DFF2EB" />
      ))}
      <path d="M238 42 L252 42 Q258 46 262 54 L266 60 L238 60 Z" fill="#DFF2EB" />
      <circle cx="96" cy="90" r="13" fill={colors.ink} />
      <circle cx="96" cy="90" r="5.5" fill="#DCE7E2" />
      <circle cx="232" cy="90" r="13" fill={colors.ink} />
      <circle cx="232" cy="90" r="5.5" fill="#DCE7E2" />
      <rect x="50" y="72" width="226" height="5" fill={colors.gold} />
    </svg>
  );
}

export default function FleetCard({ vehicle }: { vehicle: FleetVehicle }) {
  return (
    <div style={{ ...cardStyle(0), overflow: "hidden" }}>
      <div style={{ background: colors.sectionAlt, padding: "28px 36px 20px" }}>
        {vehicle.kind === "coach" ? <CoachSilhouette /> : <VanSilhouette />}
      </div>
      <div style={{ padding: 24 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap", marginBottom: 10 }}>
          <h3
            style={{
              fontFamily: fonts.condensed,
              fontWeight: 700,
              fontSize: 25,
              textTransform: "uppercase",
              color: colors.ink,
              margin: 0,
            }}
          >
            {vehicle.name}
          </h3>
          <span style={chipStyle(colors.tealTint, colors.teal, colors.tealDark)}>
            ⛁ {vehicle.seats} seats
          </span>
        </div>
        <p style={bodyStyle(15.5, colors.textMuted)}>{vehicle.blurb}</p>
        <ul style={{ margin: "14px 0 0", padding: 0, listStyle: "none" }}>
          {vehicle.features.map((f) => (
            <li
              key={f}
              style={{
                ...bodyStyle(15),
                display: "flex",
                gap: 10,
                alignItems: "baseline",
                padding: "4px 0",
              }}
            >
              <span aria-hidden="true" style={{ color: colors.teal, fontWeight: 700 }}>
                ✓
              </span>
              {f}
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}
