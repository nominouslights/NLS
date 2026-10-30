"use client";

import type { CSSProperties } from "react";
import { colors, fonts } from "@/lib/theme";
import { retiredKeyReplacement } from "@/lib/inspectionForm";

// "Now covered by …" for a stored item string that NL-PTI-01 rev 3 retired. DISPLAY
// ONLY: the stored string stays the defect's / row's address, and nothing that
// renders this note may send the replacement key anywhere. Renders nothing for a
// current key or a key the form has never known.

export default function RetiredKeyNote({ item, style }: { item: string; style?: CSSProperties }) {
  const now = retiredKeyReplacement(item);
  if (!now) return null;
  return (
    <div
      style={{
        fontFamily: fonts.body,
        fontSize: 11,
        color: colors.textDim,
        lineHeight: 1.45,
        marginTop: 3,
        ...style,
      }}
    >
      Earlier form row — the current form checks this under &ldquo;{now.label}&rdquo;.
    </div>
  );
}
