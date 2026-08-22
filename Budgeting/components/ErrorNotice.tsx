import { colors, fonts, statusMeta } from "@/lib/theme";

// The house error block, copied from Dispatcher/components/LoginScreen.tsx's errorBox: a
// vermillion tinted panel carrying the "over" glyph, a bold title, the message, and the
// machine-readable code in mono.
//
// Colour never stands alone here — the glyph and the title both survive a grayscale or
// deuteranopia pass, which is the whole reason StatusMeta bundles a glyph with every hex.

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
        gap: 9,
        padding: "9px 12px",
        borderRadius: 8,
        background: over.bg,
        border: `1px solid ${over.bd}`,
      }}
    >
      <span style={{ color: over.t, fontSize: 12, fontWeight: 800, lineHeight: "17px" }} aria-hidden>
        {over.g}
      </span>
      <div style={{ minWidth: 0 }}>
        <div style={{ fontFamily: fonts.body, fontWeight: 600, fontSize: 12.5, color: over.t }}>
          {title}
        </div>
        <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textSecondary, marginTop: 1 }}>
          {message}
        </div>
        <div style={{ fontFamily: fonts.mono, fontSize: 10, color: colors.textDim, marginTop: 3 }}>
          {code}
        </div>
      </div>
    </div>
  );
}
