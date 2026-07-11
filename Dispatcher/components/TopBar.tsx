"use client";

import { fonts } from "@/lib/theme";

export default function TopBar({
  onToggleRail,
  onCreateTrip,
}: {
  onToggleRail: () => void;
  onCreateTrip: () => void;
}) {
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 14,
        height: 56,
        flex: "none",
        padding: "0 16px",
        background: "#0F1E33",
        borderBottom: "1px solid #1E3350",
        zIndex: 20,
      }}
    >
      <div
        onClick={onToggleRail}
        style={{
          width: 30,
          height: 30,
          flex: "none",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          border: "1px solid #24405f",
          borderRadius: 6,
          cursor: "pointer",
          color: "#8fa6c0",
        }}
        title="Collapse rail"
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 3 }}>
          <span style={{ width: 14, height: 1.5, background: "currentColor", display: "block" }} />
          <span style={{ width: 14, height: 1.5, background: "currentColor", display: "block" }} />
          <span style={{ width: 14, height: 1.5, background: "currentColor", display: "block" }} />
        </div>
      </div>
      <div style={{ display: "flex", alignItems: "center", gap: 2, flex: "none" }}>
        <span style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 19, letterSpacing: ".02em", color: "#E8EEF5" }}>
          NORTHERN
        </span>
        <span style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 19, letterSpacing: ".02em", color: "#E8A020" }}>
          LINK
        </span>
      </div>
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 9,
          flex: 1,
          maxWidth: 420,
          height: 34,
          padding: "0 12px",
          borderRadius: 8,
          background: "#0A1729",
          border: "1px solid #24405f",
          color: "#5f7c9c",
          cursor: "text",
          minWidth: 0,
        }}
      >
        <span style={{ fontSize: 14 }}>⌕</span>
        <span
          style={{
            fontFamily: fonts.body,
            fontSize: 13.5,
            flex: 1,
            minWidth: 0,
            overflow: "hidden",
            whiteSpace: "nowrap",
            textOverflow: "ellipsis",
          }}
        >
          Search trips, drivers, clients, POs, invoices…
        </span>
        <span
          style={{
            fontFamily: fonts.mono,
            fontSize: 10,
            padding: "2px 6px",
            border: "1px solid #24405f",
            borderRadius: 4,
            color: "#6f8aab",
            flex: "none",
          }}
        >
          ⌘K
        </span>
      </div>
      <div style={{ flex: 1 }} />
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 7,
          flex: "none",
          padding: "5px 12px",
          border: "1px solid #24405f",
          borderRadius: 8,
          cursor: "pointer",
          color: "#c2d0e0",
          fontFamily: fonts.semiCondensed,
          fontSize: 12,
          letterSpacing: ".06em",
        }}
      >
        <span style={{ width: 6, height: 6, borderRadius: "50%", background: "#3B8DD4" }} />
        TODAY · TUE JUL 7
      </div>
      <div
        style={{
          position: "relative",
          width: 34,
          height: 34,
          flex: "none",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          border: "1px solid #24405f",
          borderRadius: 8,
          cursor: "pointer",
          color: "#9fb2c8",
        }}
      >
        <span style={{ fontSize: 16 }}>◔</span>
        <span
          style={{
            position: "absolute",
            top: -5,
            right: -5,
            minWidth: 17,
            height: 17,
            padding: "0 4px",
            borderRadius: 9,
            background: "#D55E00",
            color: "#fff",
            fontFamily: fonts.mono,
            fontSize: 10,
            fontWeight: 500,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            border: "1.5px solid #0F1E33",
          }}
        >
          5
        </span>
      </div>
      <div
        onClick={onCreateTrip}
        style={{
          display: "flex",
          alignItems: "center",
          gap: 7,
          flex: "none",
          padding: "8px 15px",
          borderRadius: 8,
          background: "#3B8DD4",
          color: "#04121f",
          fontFamily: fonts.condensed,
          fontWeight: 700,
          fontSize: 14,
          letterSpacing: ".04em",
          cursor: "pointer",
        }}
      >
        <span style={{ fontSize: 15, lineHeight: 1 }}>+</span> CREATE TRIP
      </div>
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 9,
          flex: "none",
          paddingLeft: 6,
          borderLeft: "1px solid #1E3350",
          cursor: "pointer",
        }}
      >
        <div
          style={{
            width: 34,
            height: 34,
            borderRadius: 8,
            background: "linear-gradient(135deg,#1E3350,#24405f)",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            fontFamily: fonts.condensed,
            fontWeight: 700,
            fontSize: 14,
            color: "#7EC8F0",
          }}
        >
          RK
        </div>
        <div style={{ lineHeight: 1.15 }}>
          <div style={{ fontFamily: fonts.body, fontWeight: 600, fontSize: 12.5, color: "#E8EEF5" }}>R. Kelsey</div>
          <div
            style={{
              fontFamily: fonts.semiCondensed,
              fontSize: 10,
              letterSpacing: ".08em",
              color: "#6B8099",
              textTransform: "uppercase",
            }}
          >
            Owner · Dispatcher
          </div>
        </div>
      </div>
    </div>
  );
}
