"use client";

import { useState } from "react";
import { FAQS } from "@/lib/data";
import type { Faq } from "@/lib/types";
import { bodyStyle, cardStyle, colors, fonts } from "@/lib/theme";

const GROUPS: Faq["group"][] = ["Booking", "Routes", "Cargo", "Medical Travel"];

export default function FaqAccordion() {
  const [openKey, setOpenKey] = useState<string | null>(null);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 40 }}>
      {GROUPS.map((group) => (
        <div key={group}>
          <h2
            style={{
              fontFamily: fonts.condensed,
              fontWeight: 700,
              fontSize: 26,
              textTransform: "uppercase",
              color: colors.ink,
              margin: "0 0 16px",
            }}
          >
            {group}
          </h2>
          <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
            {FAQS.filter((f) => f.group === group).map((f) => {
              const key = `${group}:${f.q}`;
              const open = openKey === key;
              const panelId = `faq-${key.replace(/[^a-z0-9]/gi, "-")}`;
              return (
                <div key={key} style={{ ...cardStyle(0), overflow: "hidden" }}>
                  <button
                    type="button"
                    aria-expanded={open}
                    aria-controls={panelId}
                    onClick={() => setOpenKey(open ? null : key)}
                    style={{
                      width: "100%",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "space-between",
                      gap: 16,
                      padding: "16px 20px",
                      background: open ? colors.tealTint : "transparent",
                      border: "none",
                      cursor: "pointer",
                      textAlign: "left",
                      fontFamily: fonts.semiCondensed,
                      fontWeight: 600,
                      fontSize: 17,
                      color: colors.ink,
                    }}
                  >
                    {f.q}
                    <span
                      aria-hidden="true"
                      style={{ color: colors.tealDark, fontSize: 20, fontWeight: 700, flex: "none" }}
                    >
                      {open ? "−" : "+"}
                    </span>
                  </button>
                  {open && (
                    <div
                      id={panelId}
                      className="fadein"
                      style={{ padding: "14px 20px 18px", borderTop: `1px solid ${colors.border}` }}
                    >
                      <p style={bodyStyle(15.5)}>{f.a}</p>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      ))}
    </div>
  );
}
