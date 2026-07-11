"use client";

import { fonts } from "@/lib/theme";
import { NAV_GROUPS, type ScreenId } from "@/lib/nav";

export default function NavRail({
  screen,
  collapsed,
  onSelect,
}: {
  screen: ScreenId;
  collapsed: boolean;
  onSelect: (id: ScreenId) => void;
}) {
  return (
    <div
      style={{
        width: collapsed ? 72 : 236,
        flex: "none",
        background: "#0A1729",
        borderRight: "1px solid #1E3350",
        display: "flex",
        flexDirection: "column",
        transition: "width .18s ease",
        overflow: "hidden",
      }}
    >
      <div style={{ flex: 1, overflowY: "auto", padding: "14px 0" }}>
        {NAV_GROUPS.map((grp) => (
          <div key={grp.label} style={{ marginBottom: 6 }}>
            <div
              style={{
                fontFamily: fonts.semiCondensed,
                fontSize: 9.5,
                letterSpacing: ".2em",
                color: "#4d688a",
                padding: "10px 18px 6px",
                whiteSpace: "nowrap",
                overflow: "hidden",
              }}
            >
              {collapsed ? grp.collapsedLabel : grp.label}
            </div>
            {grp.items.map((it) => {
              const active = screen === it.id;
              return (
                <div
                  key={it.id}
                  onClick={() => onSelect(it.id)}
                  title={it.label}
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: 11,
                    padding: collapsed ? "8px 0" : "8px 18px",
                    justifyContent: collapsed ? "center" : undefined,
                    margin: "1px 8px",
                    borderRadius: 8,
                    cursor: "pointer",
                    position: "relative",
                    background: active
                      ? "linear-gradient(90deg,rgba(232,160,32,.16),rgba(232,160,32,.02))"
                      : undefined,
                    boxShadow: active ? "inset 2px 0 0 #E8A020" : undefined,
                  }}
                >
                  <span
                    style={{
                      width: 26,
                      height: 26,
                      flex: "none",
                      borderRadius: 7,
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                      fontFamily: fonts.mono,
                      fontSize: 10,
                      fontWeight: 500,
                      background: active ? "#E8A020" : "#0F1E33",
                      color: active ? "#08192D" : "#7d93ad",
                      border: active ? undefined : "1px solid #1E3350",
                    }}
                  >
                    {it.code}
                  </span>
                  <span
                    style={{
                      fontFamily: fonts.body,
                      fontSize: 13,
                      fontWeight: active ? 600 : 500,
                      whiteSpace: "nowrap",
                      overflow: "hidden",
                      flex: 1,
                      display: collapsed ? "none" : "block",
                      color: active ? "#F2F6FB" : "#9fb2c8",
                    }}
                  >
                    {it.label}
                  </span>
                  {it.badge && (
                    <span
                      style={{
                        display: collapsed ? "none" : "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        minWidth: 20,
                        height: 18,
                        padding: "0 5px",
                        borderRadius: 9,
                        fontFamily: fonts.mono,
                        fontSize: 10,
                        background: "rgba(213,94,0,.16)",
                        color: "#f0803f",
                        border: "1px solid rgba(213,94,0,.35)",
                      }}
                    >
                      {it.badge}
                    </span>
                  )}
                </div>
              );
            })}
          </div>
        ))}
      </div>
      {/* rail footer: health + tenant */}
      <div style={{ flex: "none", borderTop: "1px solid #1E3350", padding: "12px 16px" }}>
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: 8,
            marginBottom: 10,
            justifyContent: collapsed ? "center" : undefined,
          }}
        >
          <span
            style={{
              width: 8,
              height: 8,
              borderRadius: "50%",
              background: "#14B88A",
              flex: "none",
              boxShadow: "0 0 0 3px rgba(20,184,138,.16)",
            }}
          />
          {!collapsed && (
            <span
              style={{
                fontFamily: fonts.mono,
                fontSize: 10,
                color: "#7d93ad",
                whiteSpace: "nowrap",
                overflow: "hidden",
              }}
            >
              API live · QBO synced 4m ago
            </span>
          )}
        </div>
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: 9,
            whiteSpace: "nowrap",
            overflow: "hidden",
            justifyContent: collapsed ? "center" : undefined,
          }}
        >
          <div
            style={{
              width: 26,
              height: 26,
              flex: "none",
              borderRadius: 7,
              background: "#0F1E33",
              border: "1px solid #1E3350",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontFamily: fonts.condensed,
              fontWeight: 700,
              fontSize: 11,
              color: "#E8A020",
            }}
          >
            NL
          </div>
          {!collapsed && (
            <div style={{ lineHeight: 1.2, overflow: "hidden" }}>
              <div style={{ fontFamily: fonts.body, fontSize: 11.5, fontWeight: 600, color: "#c2d0e0" }}>
                Northern Link
              </div>
              <div
                style={{
                  fontFamily: fonts.semiCondensed,
                  fontSize: 9,
                  letterSpacing: ".1em",
                  textTransform: "uppercase",
                  color: "#6B8099",
                }}
              >
                Internal Tenant
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
