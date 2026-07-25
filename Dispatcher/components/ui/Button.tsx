import type { CSSProperties, ReactNode } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";

type Variant = "primary" | "secondary" | "destructive" | "success" | "amber";

const base: CSSProperties = {
  fontFamily: fonts.condensed,
  fontWeight: 700,
  fontSize: 13,
  letterSpacing: ".03em",
  padding: "8px 15px",
  borderRadius: 8,
  cursor: "pointer",
  border: "1px solid transparent",
  display: "inline-flex",
  alignItems: "center",
  gap: 6,
  userSelect: "none",
};

function variantStyle(variant: Variant): CSSProperties {
  switch (variant) {
    case "primary":
      return { background: colors.blue, color: "#FFFFFF" };
    case "amber":
      return { background: colors.amber, color: colors.navy };
    case "success":
      // #007A59 (not the protected #009E73 chip/badge teal): white button text needs AA contrast
      return { background: "#007A59", color: "#FFFFFF" };
    case "destructive":
      return {
        background: "transparent",
        border: "1px solid rgba(213,94,0,.4)",
        color: statusMeta("over").t,
      };
    case "secondary":
    default:
      return {
        background: "transparent",
        border: `1px solid ${colors.borderActive}`,
        color: colors.textSecondary,
      };
  }
}

export function ActionButton({
  children,
  variant = "secondary",
  onClick,
  disabled,
  style,
}: {
  children: ReactNode;
  variant?: Variant;
  onClick?: () => void;
  disabled?: boolean;
  style?: CSSProperties;
}) {
  // Rendered as a span, so "disabled" is hand-implemented: the click handler is dropped
  // entirely (blocking double-submits), and the dimmed style sits under any caller style.
  return (
    <span
      onClick={disabled ? undefined : onClick}
      aria-disabled={disabled || undefined}
      style={{
        ...base,
        ...variantStyle(variant),
        ...(disabled ? { opacity: 0.6, cursor: "not-allowed" } : undefined),
        ...style,
      }}
    >
      {children}
    </span>
  );
}
