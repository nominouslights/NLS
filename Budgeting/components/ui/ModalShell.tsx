// COPIED FROM Dispatcher/components/ui/ModalShell.tsx — keep identical below this header.
// Change Dispatcher first, then re-copy. Drift check: see Budgeting/CLAUDE.md.
"use client";

import type { ReactNode } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";

// Fixed-overlay modal shell (extracted from the VehicleFormModal / CreateTripWizard
// pattern) so the Fleet & Maintenance form modals share one chrome: scrim, card,
// eyebrow+title header with ✕ close, scrollable body, and a footer slot.

export function ModalShell({
  eyebrow,
  title,
  onClose,
  children,
  footer,
  error,
  maxWidth = 680,
}: {
  eyebrow: string;
  title: string;
  onClose: () => void;
  children: ReactNode;
  footer: ReactNode;
  error?: string | null;
  maxWidth?: number;
}) {
  return (
    <div
      className="detailfade"
      style={{
        position: "fixed",
        inset: 0,
        zIndex: 100,
        background: colors.scrim,
        backdropFilter: "blur(3px)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: 26,
      }}
    >
      <div
        style={{
          width: "100%",
          maxWidth,
          maxHeight: "88vh",
          background: colors.mainBg,
          border: `1px solid ${colors.borderStrong}`,
          borderRadius: 16,
          display: "flex",
          flexDirection: "column",
          overflow: "hidden",
          boxShadow: colors.shadowPop,
        }}
      >
        {/* header */}
        <div
          style={{
            flex: "none",
            display: "flex",
            alignItems: "center",
            padding: "18px 24px",
            borderBottom: `1px solid ${colors.border}`,
          }}
        >
          <div>
            <div
              style={{
                fontFamily: fonts.semiCondensed,
                fontSize: 10,
                letterSpacing: ".16em",
                textTransform: "uppercase",
                color: colors.textFaint,
                marginBottom: 2,
              }}
            >
              {eyebrow}
            </div>
            <div
              style={{
                fontFamily: fonts.condensed,
                fontWeight: 700,
                fontSize: 22,
                color: colors.headingBright,
                lineHeight: 1,
              }}
            >
              {title}
            </div>
          </div>
          <div
            onClick={onClose}
            style={{
              marginLeft: "auto",
              width: 34,
              height: 34,
              borderRadius: 8,
              border: `1px solid ${colors.borderStrong}`,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              color: colors.textMuted,
              cursor: "pointer",
              fontSize: 18,
            }}
          >
            ✕
          </div>
        </div>

        {/* body */}
        <div style={{ flex: 1, minHeight: 0, overflowY: "auto", padding: "22px 24px" }}>
          {error && <ModalError message={error} />}
          {children}
        </div>

        {/* footer */}
        <div
          style={{
            flex: "none",
            display: "flex",
            alignItems: "center",
            justifyContent: "flex-end",
            gap: 10,
            padding: "16px 24px",
            borderTop: `1px solid ${colors.border}`,
          }}
        >
          {footer}
        </div>
      </div>
    </div>
  );
}

export function ModalError({ message }: { message: string }) {
  return (
    <div
      style={{
        padding: "12px 15px",
        background: "rgba(213,94,0,.1)",
        border: "1px solid rgba(213,94,0,.4)",
        borderRadius: 10,
        marginBottom: 16,
        display: "flex",
        gap: 10,
        alignItems: "center",
      }}
    >
      <span
        style={{
          width: 20,
          height: 20,
          flex: "none",
          borderRadius: 5,
          background: "#D55E00",
          color: "#fff",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          fontSize: 11,
          fontWeight: 800,
        }}
      >
        ▲
      </span>
      <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: statusMeta("over").t, fontWeight: 600 }}>
        {message}
      </span>
    </div>
  );
}
