import { colors, fonts } from "@/lib/theme";

// The NORTHERN/LINK wordmark and its eyebrow. Dispatcher inlines this markup separately in
// AuthGate and LoginScreen; here it appears on four full-screen states, so it is one component
// rendering byte-identical output rather than four copies drifting apart. Sizes match
// Dispatcher exactly: 24px on the restoring splash, 26px on the card screens.

export function Wordmark({ size = 26 }: { size?: number }) {
  const wordStyle = {
    fontFamily: fonts.condensed,
    fontWeight: 700,
    fontSize: size,
    letterSpacing: ".02em",
  } as const;

  return (
    <div style={{ display: "inline-flex", alignItems: "center", gap: 2 }}>
      <span style={{ ...wordStyle, color: colors.headingBright }}>NORTHERN</span>
      <span style={{ ...wordStyle, color: colors.amberText }}>LINK</span>
    </div>
  );
}

/** The uppercase eyebrow under the wordmark — "FINANCE · BUDGETING CONSOLE". */
export function BrandEyebrow({ children }: { children: React.ReactNode }) {
  return (
    <div
      style={{
        fontFamily: fonts.semiCondensed,
        fontSize: 10.5,
        letterSpacing: ".16em",
        textTransform: "uppercase",
        color: colors.textDim,
        marginTop: 2,
      }}
    >
      {children}
    </div>
  );
}

/**
 * The centred 380px column every signed-out state uses — sign in, access denied, setup
 * pending. Same geometry as Dispatcher's LoginScreen so the two apps' entry screens are
 * recognisably one product.
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
        padding: 20,
      }}
    >
      <div style={{ width: 380, maxWidth: "100%" }}>
        <div style={{ textAlign: "center", marginBottom: 18 }}>
          <Wordmark />
          <BrandEyebrow>Finance · Budgeting Console</BrandEyebrow>
        </div>
        {children}
      </div>
    </div>
  );
}
