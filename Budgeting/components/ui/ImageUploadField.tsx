// COPIED FROM Dispatcher/components/ui/ImageUploadField.tsx — keep identical below this header.
// Change Dispatcher first, then re-copy. Drift check: see Budgeting/CLAUDE.md.
"use client";

import { useEffect, useRef, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { FieldLabel } from "@/components/ui/Field";

// Real image picker with thumbnail preview (unlike FileField.tsx which is mock).
// Uploads directly to the API via FormData. Accepts JPEG, PNG, HEIC only.

export function ImageUploadField({
  label,
  value,
  onChange,
  hint,
}: {
  label: string;
  value: File | null;
  onChange: (f: File | null) => void;
  hint?: React.ReactNode;
}) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [preview, setPreview] = useState<string | null>(null);

  // Revoke preview URL when file changes or component unmounts (prevent memory leak)
  useEffect(() => {
    return () => {
      if (preview) URL.revokeObjectURL(preview);
    };
  }, [preview]);

  const handleFile = (file: File | null) => {
    onChange(file);
    if (preview) URL.revokeObjectURL(preview);
    if (file) {
      setPreview(URL.createObjectURL(file));
    } else {
      setPreview(null);
    }
  };

  return (
    <div>
      <FieldLabel hint={hint}>{label}</FieldLabel>
      <input
        ref={inputRef}
        type="file"
        accept="image/jpeg,image/png,image/heic"
        style={{ display: "none" }}
        onChange={(e) => {
          const file = e.target.files?.[0] || null;
          handleFile(file);
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
          {preview && (
            <img
              src={preview}
              alt="Preview"
              style={{
                width: 28,
                height: 28,
                flex: "none",
                borderRadius: 5,
                objectFit: "cover",
              }}
            />
          )}
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
          <span
            onClick={() => {
              handleFile(null);
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
          <span style={{ fontSize: 13, color: colors.skyBlue }}>📷</span>
          <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted }}>
            Choose a photo…
          </span>
        </div>
      )}
    </div>
  );
}
