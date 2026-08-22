"use client";

import { useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import Wordmark from "@/components/ui/Wordmark";
import Button from "@/components/ui/Button";
import { NAV_ITEMS } from "@/lib/nav";
import { colors, fonts } from "@/lib/theme";

function isActive(pathname: string, href: string): boolean {
  if (href === "/") return pathname === "/";
  return pathname === href || pathname.startsWith(`${href}/`);
}

export default function Header() {
  const pathname = usePathname();
  const [open, setOpen] = useState(false);

  const linkStyle = (active: boolean) => ({
    fontFamily: fonts.semiCondensed,
    fontWeight: 600,
    fontSize: 15.5,
    letterSpacing: "0.03em",
    textTransform: "uppercase" as const,
    color: active ? colors.tealDark : colors.text,
    padding: "8px 12px",
    borderRadius: 6,
    background: active ? colors.tealTint : "transparent",
  });

  return (
    <header
      style={{
        position: "sticky",
        top: 0,
        zIndex: 50,
        background: "rgba(255,255,255,.96)",
        backdropFilter: "blur(6px)",
        borderBottom: `1px solid ${colors.border}`,
      }}
    >
      <div
        style={{
          maxWidth: 1120,
          margin: "0 auto",
          padding: "14px 24px",
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 16,
        }}
      >
        <Link href="/" aria-label="Northern Link Shuttle & Cargo — home" onClick={() => setOpen(false)}>
          <Wordmark />
        </Link>

        <nav aria-label="Main" className="nl-desktop-nav">
          {NAV_ITEMS.map((item) => (
            <Link key={item.href} href={item.href} style={linkStyle(isActive(pathname, item.href))}>
              {item.label}
            </Link>
          ))}
          <span style={{ marginLeft: 10 }}>
            <Button href="/booking">Get a Quote</Button>
          </span>
        </nav>

        <button
          type="button"
          className="nl-mobile-toggle"
          aria-expanded={open}
          aria-controls="mobile-menu"
          aria-label={open ? "Close menu" : "Open menu"}
          onClick={() => setOpen((v) => !v)}
          style={{
            alignItems: "center",
            justifyContent: "center",
            gap: 8,
            background: "transparent",
            border: `1px solid ${colors.border}`,
            borderRadius: 8,
            padding: "8px 14px",
            cursor: "pointer",
            fontFamily: fonts.semiCondensed,
            fontWeight: 600,
            fontSize: 15,
            color: colors.ink,
          }}
        >
          <span aria-hidden="true" style={{ fontSize: 17, lineHeight: 1 }}>
            {open ? "✕" : "☰"}
          </span>
          Menu
        </button>
      </div>

      {open && (
        <nav
          id="mobile-menu"
          aria-label="Mobile"
          className="fadein"
          style={{
            borderTop: `1px solid ${colors.border}`,
            background: "#FFFFFF",
            padding: "12px 24px 20px",
            display: "flex",
            flexDirection: "column",
            gap: 4,
          }}
        >
          {NAV_ITEMS.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              onClick={() => setOpen(false)}
              style={{
                ...linkStyle(isActive(pathname, item.href)),
                display: "block",
                padding: "12px 12px",
                fontSize: 17,
              }}
            >
              {item.label}
            </Link>
          ))}
          <div style={{ marginTop: 10 }}>
            <Button href="/booking" style={{ width: "100%" }}>
              Get a Quote
            </Button>
          </div>
        </nav>
      )}
    </header>
  );
}
