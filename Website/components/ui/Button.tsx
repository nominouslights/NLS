import Link from "next/link";
import type { CSSProperties, ReactNode } from "react";
import { buttonStyle, type ButtonVariant } from "@/lib/theme";

// Deliberate deviation from Dispatcher's <span onClick> convention: this is a
// public site, so CTAs are real <a> (crawlable, keyboard-focusable) and real
// <button> elements for SEO and accessibility.
interface ButtonProps {
  href?: string;
  type?: "button" | "submit";
  onClick?: () => void;
  variant?: ButtonVariant;
  size?: "md" | "lg";
  style?: CSSProperties;
  children: ReactNode;
}

export default function Button({
  href,
  type = "button",
  onClick,
  variant = "primary",
  size = "md",
  style,
  children,
}: ButtonProps) {
  const s = { ...buttonStyle(variant, size), ...style };
  if (href) {
    const internal = href.startsWith("/");
    if (internal) {
      return (
        <Link href={href} className="nl-btn" style={s}>
          {children}
        </Link>
      );
    }
    return (
      <a href={href} className="nl-btn" style={s}>
        {children}
      </a>
    );
  }
  return (
    <button type={type} onClick={onClick} className="nl-btn" style={s}>
      {children}
    </button>
  );
}
