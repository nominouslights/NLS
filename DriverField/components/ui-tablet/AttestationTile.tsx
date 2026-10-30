"use client";

import { fonts, statusMeta } from "@/lib/theme";
import { radius, touch, type } from "@/lib/tablet";

// APP-LOCAL. A 56px attestation toggle: a statement the driver turns on to assert it. Colour +
// glyph + label in both states (teal ✓ on, grey — off), toggled by one tap, read back through
// aria-pressed. Used by the personal-conveyance sheet (four of them) and the exception sheet
// (the s.76 knowability attestation). Not a StatusButton: that one carries an action, this one
// carries a statement, and a glance must tell them apart.

export function AttestationTile({ label, on, onToggle }: { label: string; on: boolean; onToggle: () => void }) {
  const m = statusMeta(on ? "ontime" : "off");
  return (
    <button
      type="button"
      onClick={onToggle}
      aria-pressed={on}
      style={{
        flex: "1 1 320px",
        minHeight: touch.primary,
        padding: "10px 18px",
        borderRadius: radius.control,
        border: `2px solid ${on ? m.c : m.bd}`,
        background: on ? m.c : m.bg,
        color: on ? m.bt : m.t,
        fontFamily: fonts.semiCondensed,
        fontSize: type.label,
        fontWeight: 600,
        letterSpacing: ".04em",
        lineHeight: 1.25,
        display: "inline-flex",
        alignItems: "center",
        gap: 10,
        textAlign: "left",
        cursor: "pointer",
      }}
    >
      <span aria-hidden style={{ fontWeight: 800, flex: "none" }}>
        {m.g}
      </span>
      {label}
    </button>
  );
}
