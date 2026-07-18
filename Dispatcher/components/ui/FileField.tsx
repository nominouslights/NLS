"use client";

import { useRef } from "react";
import { colors, fonts } from "@/lib/theme";
import { FieldLabel } from "@/components/ui/Field";

// Prototype file picker — captures the chosen file's name + size for the mock
// document store. Nothing is uploaded (no backend document domain yet); the
// bytes never leave the browser. Styled from theme tokens to match Field.tsx.

export interface PickedFile {
  name: string;
  sizeKb: number;
}

export function FileField({
  label,
  value,
  onChange,
  hint,
}: {
  label: string;
  value: PickedFile | null;
  onChange: (f: PickedFile | null) => void;
  hint?: React.ReactNode;
}) {
  const inputRef = useRef<HTMLInputElement>(null);

  return (
    <div>
      <FieldLabel hint={hint}>{label}</FieldLabel>
      <input
        ref={inputRef}
        type="file"
        accept=".pdf,.jpg,.jpeg,.png,.heic"
        style={{ display: "none" }}
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) onChange({ name: file.name, sizeKb: Math.max(1, Math.round(file.size / 1024)) });
        }}
      />
      {value ? (
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: 10,
            height: 40,
            boxSizing: "border-box",
            borderRadius: 9,
            background: colors.inputBg,
            border: `1px solid ${colors.borderStrong}`,
            padding: "0 12px",
          }}
        >
          <span
            style={{
              width: 20,
              height: 20,
              flex: "none",
              borderRadius: 5,
              background: "rgba(0,158,115,.12)",
              color: statusTeal,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: 11,
              fontWeight: 800,
            }}
          >
            ✓
          </span>
          <span
            style={{
              flex: 1,
              minWidth: 0,
              fontFamily: fonts.mono,
              fontSize: 12,
              color: colors.textPrimary,
              whiteSpace: "nowrap",
              overflow: "hidden",
              textOverflow: "ellipsis",
            }}
          >
            {value.name}
          </span>
          <span style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, flex: "none" }}>
            {value.sizeKb} KB
          </span>
          <span
            onClick={() => {
              onChange(null);
              if (inputRef.current) inputRef.current.value = "";
            }}
            style={{ flex: "none", fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, cursor: "pointer" }}
          >
            Remove
          </span>
        </div>
      ) : (
        <div
          onClick={() => inputRef.current?.click()}
          style={{
            display: "flex",
            alignItems: "center",
            gap: 9,
            height: 40,
            boxSizing: "border-box",
            borderRadius: 9,
            background: colors.inputBg,
            border: `1px dashed ${colors.borderStrong}`,
            padding: "0 13px",
            cursor: "pointer",
          }}
        >
          <span style={{ fontSize: 13, color: colors.skyBlue }}>▤</span>
          <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted }}>
            Scan or choose a file…
          </span>
        </div>
      )}
    </div>
  );
}

const statusTeal = "#007A59";
