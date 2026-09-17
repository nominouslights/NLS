"use client";

import { colors, fonts, statusMeta, type StatusKind } from "@/lib/theme";
import { radius, wizard } from "@/lib/tablet";

// APP-LOCAL. The full-screen answer target in the DVIR wizard: colour, glyph and word, at a
// size a gloved thumb cannot miss in a moving vehicle.
//
// NOT a prop on StatusButton. That control is touch.primary (56px) and padded to sit two in a
// card row; this is a 300×168 tile with a 34px glyph above a word. Different markup for a
// different job — which is exactly the case DriverField/CLAUDE.md describes for ui-tablet
// rather than a fork of components/ui/*.
//
// NO DEFAULT SELECTION, EVER. A pre-trip inspection under NSC Standard 11 is a legal
// attestation, and "not answered" must never be able to look like "passed" — so there is no
// pre-selected tile and no state a driver can reach by not acting. (Inherited verbatim from
// ThreeStateControl, the compact control this replaces.)
//
// THE `glyph` OVERRIDE, and its precedent: components/ui/Chip.tsx's StatusChip carries the same
// optional prop, for the same reason. Two states may legitimately share a colour and must still
// be told apart at a glance — a defect's Major and Out-of-Service are both vermillion, because
// lib/theme.ts is a protected copy and a fifth StatusKind is not an option, so Out-of-Service
// supplies its own ✕ (see severityGlyph in lib/inspectionGate.ts). It overrides the ICON only.
// Never a new colour, and never a substitute for the text label.

export function AnswerButton({
  kind,
  label,
  sublabel,
  glyph,
  selected,
  onClick,
}: {
  kind: StatusKind;
  label: string;
  /** Optional second line — what this answer commits the driver to. */
  sublabel?: string;
  /** Overrides the kind's default icon. Never a new colour. */
  glyph?: string;
  selected?: boolean;
  onClick: () => void;
}) {
  const m = statusMeta(kind);

  return (
    <button
      onClick={onClick}
      aria-pressed={selected ?? false}
      style={{
        flex: 1,
        minWidth: wizard.answerMinW,
        minHeight: wizard.answerH,
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        gap: 10,
        padding: "18px 20px",
        borderRadius: radius.panel,
        border: `2px solid ${selected ? m.c : colors.border}`,
        background: selected ? m.c : m.bg,
        color: selected ? m.bt : m.t,
        boxShadow: selected ? colors.shadowPop : colors.shadowCard,
        cursor: "pointer",
        textAlign: "center",
      }}
    >
      <span aria-hidden style={{ fontSize: 34, lineHeight: 1, fontWeight: 800 }}>
        {glyph ?? m.g}
      </span>
      <span
        style={{
          fontFamily: fonts.semiCondensed,
          fontSize: 26,
          fontWeight: 700,
          letterSpacing: ".06em",
          textTransform: "uppercase",
        }}
      >
        {label}
      </span>
      {sublabel ? (
        <span
          style={{
            fontFamily: fonts.body,
            fontSize: 15,
            fontWeight: 500,
            lineHeight: 1.4,
            color: selected ? m.bt : colors.textSecondary,
            opacity: selected ? 0.92 : 1,
          }}
        >
          {sublabel}
        </span>
      ) : null}
    </button>
  );
}
