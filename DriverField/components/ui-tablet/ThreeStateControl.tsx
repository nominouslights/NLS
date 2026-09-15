"use client";

import { colors, fonts, statusMeta } from "@/lib/theme";
import { radius, touch, type } from "@/lib/tablet";
import type { CheckState } from "@/lib/types";

// APP-LOCAL. One DVIR checklist line: Pass / Defect / N-A.
//
// Three explicit buttons rather than a toggle or a dropdown. A pre-trip inspection under NSC
// Standard 11 is a legal attestation, and "not answered" must never be able to look like
// "passed" — so there is no default selection and no state a driver can reach by not acting.
// Each option carries its status glyph and its word, so the answer is readable at a glance in
// daylight and to a colour-blind driver.

const OPTIONS: { value: CheckState; label: string; kind: "ontime" | "over" | "off" }[] = [
  { value: "pass", label: "Pass", kind: "ontime" },
  { value: "defect", label: "Defect", kind: "over" },
  { value: "na", label: "N/A", kind: "off" },
];

export function ThreeStateControl({
  item,
  value,
  onChange,
}: {
  item: string;
  value: CheckState | null;
  onChange: (next: CheckState) => void;
}) {
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 12,
        minHeight: touch.primary,
        padding: "8px 14px",
        borderRadius: radius.control,
        background: colors.cardBg,
        border: `1px solid ${value === null ? colors.borderStrong : colors.borderSubtle}`,
      }}
    >
      <div
        style={{
          flex: 1,
          minWidth: 0,
          fontFamily: fonts.body,
          fontSize: type.label,
          color: colors.textPrimary,
        }}
      >
        {item}
      </div>

      <div style={{ display: "flex", gap: 6, flex: "none" }} role="group" aria-label={item}>
        {OPTIONS.map((o) => {
          const m = statusMeta(o.kind);
          const active = value === o.value;
          return (
            <button
              key={o.value}
              onClick={() => onChange(o.value)}
              aria-pressed={active}
              style={{
                minHeight: touch.min,
                minWidth: 92,
                borderRadius: radius.control,
                border: `1px solid ${active ? m.c : colors.border}`,
                background: active ? m.c : colors.inputBg,
                color: active ? m.bt : colors.textMuted,
                fontFamily: fonts.semiCondensed,
                fontSize: 15,
                fontWeight: 600,
                letterSpacing: ".06em",
                textTransform: "uppercase",
                cursor: "pointer",
                display: "inline-flex",
                alignItems: "center",
                justifyContent: "center",
                gap: 6,
              }}
            >
              <span aria-hidden>{m.g}</span>
              {o.label}
            </button>
          );
        })}
      </div>
    </div>
  );
}
