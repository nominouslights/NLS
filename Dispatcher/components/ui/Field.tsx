"use client";

import type { CSSProperties, ReactNode } from "react";
import { colors, fonts } from "@/lib/theme";

// First real (editable) input primitives in the app — styled strictly from
// lib/theme.ts tokens, matching the Panel/SectionLabel visual language and the
// read-only Field look in CreateTripWizard.

export function FieldLabel({
  children,
  hint,
  htmlFor,
}: {
  children: ReactNode;
  hint?: ReactNode;
  htmlFor?: string;
}) {
  return (
    <label
      htmlFor={htmlFor}
      style={{
        display: "block",
        fontFamily: fonts.body,
        fontSize: 11.5,
        color: colors.textLabel,
        marginBottom: 5,
      }}
    >
      {children} {hint}
    </label>
  );
}

const inputBase: CSSProperties = {
  width: "100%",
  height: 40,
  boxSizing: "border-box",
  borderRadius: 9,
  background: colors.inputBg,
  border: `1px solid ${colors.borderStrong}`,
  padding: "0 13px",
  fontFamily: fonts.body,
  fontSize: 13.5,
  color: colors.textPrimary,
  outline: "none",
};

export function TextField({
  label,
  value,
  onChange,
  mono = false,
  placeholder,
  hint,
  maxLength,
  disabled = false,
  type = "text",
  autoComplete,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  mono?: boolean;
  placeholder?: string;
  hint?: ReactNode;
  maxLength?: number;
  disabled?: boolean;
  type?: "text" | "email" | "password";
  autoComplete?: string;
}) {
  return (
    <div>
      <FieldLabel hint={hint}>{label}</FieldLabel>
      <input
        type={type}
        autoComplete={autoComplete}
        className="nl-input"
        value={value}
        maxLength={maxLength}
        placeholder={placeholder}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
        style={{
          ...inputBase,
          fontFamily: mono ? fonts.mono : fonts.body,
          fontSize: mono ? 13 : 13.5,
          opacity: disabled ? 0.55 : 1,
          cursor: disabled ? "not-allowed" : undefined,
        }}
      />
    </div>
  );
}

export function NumberField({
  label,
  value,
  onChange,
  hint,
  min,
  step,
  placeholder,
  disabled = false,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  hint?: ReactNode;
  min?: number;
  step?: number;
  placeholder?: string;
  disabled?: boolean;
}) {
  return (
    <div>
      <FieldLabel hint={hint}>{label}</FieldLabel>
      <input
        type="number"
        className="nl-input"
        value={value}
        min={min}
        step={step}
        placeholder={placeholder}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
        style={{
          ...inputBase,
          fontFamily: fonts.mono,
          fontSize: 13,
          fontVariantNumeric: "tabular-nums",
          opacity: disabled ? 0.55 : 1,
          cursor: disabled ? "not-allowed" : undefined,
        }}
      />
    </div>
  );
}

export function DateField({
  label,
  value,
  onChange,
  hint,
  disabled = false,
}: {
  label: string;
  value: string; // ISO YYYY-MM-DD
  onChange: (v: string) => void;
  hint?: ReactNode;
  disabled?: boolean;
}) {
  return (
    <div>
      <FieldLabel hint={hint}>{label}</FieldLabel>
      <input
        type="date"
        className="nl-input"
        value={value}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
        style={{
          ...inputBase,
          fontFamily: fonts.mono,
          fontSize: 13,
          colorScheme: "light",
          opacity: disabled ? 0.55 : 1,
          cursor: disabled ? "not-allowed" : undefined,
        }}
      />
    </div>
  );
}

export function TextAreaField({
  label,
  value,
  onChange,
  placeholder,
  hint,
  rows = 3,
  disabled = false,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
  hint?: ReactNode;
  rows?: number;
  disabled?: boolean;
}) {
  return (
    <div>
      <FieldLabel hint={hint}>{label}</FieldLabel>
      <textarea
        className="nl-input"
        value={value}
        rows={rows}
        placeholder={placeholder}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
        style={{
          ...inputBase,
          height: undefined,
          minHeight: 40,
          padding: "9px 13px",
          lineHeight: 1.5,
          resize: "vertical",
          opacity: disabled ? 0.55 : 1,
          cursor: disabled ? "not-allowed" : undefined,
        }}
      />
    </div>
  );
}

export function SelectField({
  label,
  value,
  onChange,
  options,
  hint,
  disabled = false,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  options: { value: string; label: string }[];
  hint?: ReactNode;
  disabled?: boolean;
}) {
  return (
    <div>
      <FieldLabel hint={hint}>{label}</FieldLabel>
      <select
        className="nl-input"
        value={value}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
        style={{
          ...inputBase,
          appearance: "none",
          WebkitAppearance: "none",
          cursor: disabled ? "not-allowed" : "pointer",
          opacity: disabled ? 0.55 : 1,
          backgroundImage:
            "url(\"data:image/svg+xml;charset=utf-8,%3Csvg xmlns='http://www.w3.org/2000/svg' width='10' height='6'%3E%3Cpath d='M0 0l5 6 5-6z' fill='%23486581'/%3E%3C/svg%3E\")",
          backgroundRepeat: "no-repeat",
          backgroundPosition: "right 13px center",
          paddingRight: 32,
        }}
      >
        {options.map((o) => (
          <option key={o.value} value={o.value} style={{ background: colors.inputBg }}>
            {o.label}
          </option>
        ))}
      </select>
    </div>
  );
}
