import type { CSSProperties, ReactNode } from "react";
import { colors, fonts } from "@/lib/theme";

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
      return { background: colors.blue, color: "#04121f" };
    case "amber":
      return { background: colors.amber, color: colors.navy };
    case "success":
      return { background: "#14B88A", color: "#04231a" };
    case "destructive":
      return {
        background: "transparent",
        border: "1px solid rgba(213,94,0,.4)",
        color: "#f0803f",
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
  style,
}: {
  children: ReactNode;
  variant?: Variant;
  onClick?: () => void;
  style?: CSSProperties;
}) {
  return (
    <span onClick={onClick} style={{ ...base, ...variantStyle(variant), ...style }}>
      {children}
    </span>
  );
}
