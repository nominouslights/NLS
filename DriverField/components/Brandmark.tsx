import { colors, fonts } from "@/lib/theme";
import { type } from "@/lib/tablet";

// ADAPTED FROM Budgeting/components/Brandmark.tsx — same markup, tablet sizing.
// Sizes come from lib/tablet.ts, never from theme.ts. The signed-out screens are the one place
// a driver is typing rather than tapping, so the card is wider than the consoles' 380px but
// still a single centred column.

export function Wordmark({ size = 38 }: { size?: number }) {
  const wordStyle = {
    fontFamily: fonts.condensed,
    fontWeight: 700,
    fontSize: size,
    letterSpacing: ".02em",
  } as const;

  return (
    <div style={{ display: "inline-flex", alignItems: "center", gap: 3 }}>
      <span style={{ ...wordStyle, color: colors.headingBright }}>NORTHERN</span>
      <span style={{ ...wordStyle, color: colors.amberText }}>LINK</span>
    </div>
  );
}

/** The uppercase eyebrow under the wordmark — "FIELD · DRIVER APP". */
export function BrandEyebrow({ children }: { children: React.ReactNode }) {
  return (
    <div
      style={{
        fontFamily: fonts.semiCondensed,
        fontSize: 13,
        letterSpacing: ".16em",
        textTransform: "uppercase",
        color: colors.textDim,
        marginTop: 4,
      }}
    >
      {children}
    </div>
  );
}

/**
 * The centred column every signed-out state uses — sign in, access denied, setup pending.
 * 520px rather than the consoles' 380px: this is a 1280px landscape tablet operated at arm's
 * length, and a 380px card reads as a phone dialog dropped onto a dashboard.
 */
export function BrandScreen({ children }: { children: React.ReactNode }) {
  return (
    <div
      style={{
        height: "100vh",
        width: "100%",
        background: colors.pageBg,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: 24,
      }}
    >
      <div style={{ width: 520, maxWidth: "100%" }}>
        <div style={{ textAlign: "center", marginBottom: 24 }}>
          <Wordmark />
          <BrandEyebrow>Field · Driver App</BrandEyebrow>
        </div>
        {children}
      </div>
    </div>
  );
}

/** Body copy at tablet size — used by the signed-out screens. */
export function BrandBody({ children }: { children: React.ReactNode }) {
  return (
    <div
      style={{
        fontFamily: fonts.body,
        fontSize: type.label,
        color: colors.textSecondary,
        lineHeight: 1.65,
        marginTop: 4,
      }}
    >
      {children}
    </div>
  );
}
