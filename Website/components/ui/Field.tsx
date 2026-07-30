import type { ChangeEvent } from "react";
import { colors, errorTextStyle, inputStyle, labelStyle } from "@/lib/theme";

// Shared form field for the prototype forms. Errors follow the platform rule:
// never color alone — vermillion + ⚠ icon + text label.
interface FieldProps {
  label: string;
  name: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
  required?: boolean;
  placeholder?: string;
  options?: { value: string; label: string }[]; // renders a <select>
  textarea?: boolean;
  error?: string;
}

export default function Field({
  label,
  name,
  value,
  onChange,
  type = "text",
  required = false,
  placeholder,
  options,
  textarea = false,
  error,
}: FieldProps) {
  const hasError = Boolean(error);
  const errorId = `${name}-error`;
  const handle = (
    e: ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>,
  ) => onChange(e.target.value);

  return (
    <div>
      <label htmlFor={name} style={labelStyle()}>
        {label}
        {required && (
          <span aria-hidden="true" style={{ color: colors.tealDark }}>
            {" "}
            *
          </span>
        )}
      </label>
      {options ? (
        <select
          id={name}
          name={name}
          value={value}
          onChange={handle}
          aria-invalid={hasError}
          aria-describedby={hasError ? errorId : undefined}
          style={{ ...inputStyle(hasError), appearance: "auto" }}
        >
          <option value="">{placeholder ?? "Select…"}</option>
          {options.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      ) : textarea ? (
        <textarea
          id={name}
          name={name}
          value={value}
          onChange={handle}
          placeholder={placeholder}
          rows={5}
          aria-invalid={hasError}
          aria-describedby={hasError ? errorId : undefined}
          style={{ ...inputStyle(hasError), resize: "vertical" }}
        />
      ) : (
        <input
          id={name}
          name={name}
          type={type}
          value={value}
          onChange={handle}
          placeholder={placeholder}
          aria-invalid={hasError}
          aria-describedby={hasError ? errorId : undefined}
          style={inputStyle(hasError)}
        />
      )}
      {hasError && (
        <span id={errorId} role="alert" style={errorTextStyle()}>
          <span aria-hidden="true">⚠</span> Error: {error}
        </span>
      )}
    </div>
  );
}
