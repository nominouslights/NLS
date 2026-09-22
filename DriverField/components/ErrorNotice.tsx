import { colors, fonts, statusMeta } from "@/lib/theme";
import { gap, radius, type } from "@/lib/tablet";

// ADAPTED FROM Budgeting/components/ErrorNotice.tsx — same structure, tablet sizing.
//
// Colour never stands alone: the glyph and the title both survive a grayscale or deuteranopia
// pass, which is the whole reason StatusMeta bundles a glyph with every hex. That matters more
// here than anywhere else on the platform — a dash-mounted tablet in daylight is the
// highest-glare, lowest-attention surface we ship.

export function ErrorNotice({
  title,
  message,
  code,
}: {
  title: string;
  message: string;
  code: string;
}) {
  const over = statusMeta("over");

  return (
    <div
      role="alert"
      style={{
        display: "flex",
        gap: gap.tight + 4,
        padding: "14px 16px",
        borderRadius: radius.control,
        background: over.bg,
        border: `1px solid ${over.bd}`,
      }}
    >
      <span
        style={{ color: over.t, fontSize: type.label, fontWeight: 800, lineHeight: "24px" }}
        aria-hidden
      >
        {over.g}
      </span>
      <div style={{ minWidth: 0 }}>
        <div style={{ fontFamily: fonts.body, fontWeight: 600, fontSize: 18, color: over.t }}>
          {title}
        </div>
        <div
          style={{
            fontFamily: fonts.body,
            fontSize: type.label,
            color: colors.textSecondary,
            marginTop: 3,
            lineHeight: 1.55,
          }}
        >
          {message}
        </div>
        <div
          style={{ fontFamily: fonts.mono, fontSize: 13, color: colors.textDim, marginTop: 6 }}
        >
          {code}
        </div>
      </div>
    </div>
  );
}
