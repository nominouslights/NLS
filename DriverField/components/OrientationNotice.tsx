"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";

// Landscape enforcement, and it is the REAL mechanism — not a belt-and-braces extra.
//
// app/manifest.ts declares `orientation: "landscape"`, but that applies only to the INSTALLED
// PWA. In a browser tab on a tablet whose rotation lock is off, portrait happens, and every
// screen in this app is laid out for a fixed 1280×800 landscape frame (architecture
// non-negotiable #5: company-issued 10-inch tablet, landscape-only, never BYOD). Rather than
// build phone-shaped fallbacks the non-negotiable explicitly forbids, this says "turn it".
//
// Rendered in app/layout.tsx above everything, so it covers the signed-out screens too.

export default function OrientationNotice() {
  const [portrait, setPortrait] = useState(false);

  useEffect(() => {
    if (typeof window === "undefined" || !window.matchMedia) return;
    const query = window.matchMedia("(orientation: portrait)");
    const update = () => setPortrait(query.matches);
    update();
    query.addEventListener("change", update);
    return () => query.removeEventListener("change", update);
  }, []);

  if (!portrait) return null;

  return (
    <div
      role="alert"
      style={{
        position: "fixed",
        inset: 0,
        zIndex: 9999,
        background: colors.navy,
        color: "#FFFFFF",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        gap: 18,
        padding: 32,
        textAlign: "center",
      }}
    >
      <div
        aria-hidden
        style={{
          width: 76,
          height: 112,
          border: `4px solid ${colors.amber}`,
          borderRadius: 10,
          transform: "rotate(-18deg)",
        }}
      />
      <div
        style={{
          fontFamily: fonts.condensed,
          fontWeight: 700,
          fontSize: 34,
          letterSpacing: ".02em",
        }}
      >
        Rotate to landscape
      </div>
      <div
        style={{
          fontFamily: fonts.body,
          fontSize: 17,
          color: "#C9D6E4",
          maxWidth: 420,
          lineHeight: 1.6,
        }}
      >
        The Driver App is built for a mounted 10-inch tablet in landscape. Turn the tablet, or
        switch rotation lock off.
      </div>
    </div>
  );
}
