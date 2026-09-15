"use client";

import { colors, fonts, statusMeta } from "@/lib/theme";
import { rail, touch, type } from "@/lib/tablet";
import { NAV_GROUPS, type ScreenId } from "@/lib/nav";

// The navigation rail. NEW, not a copy — and that omission is deliberate enough to be worth
// explaining here as well as in DriverField/CLAUDE.md.
//
// components/ui/* and 21 other files are byte-identical copies of Dispatcher's design system,
// verified by a diff. components/NavRail.tsx is NOT among them: its geometry is hardcoded in
// the component (`width: collapsed ? 72 : 236`, 26px code tiles, 13px labels, "8px 18px" row
// padding) with nothing driven from lib/nav.ts. This app needs 64px rows and 44px tiles, which
// is unreachable without editing the copy — the one thing the copy rule forbids. Copying a file
// we would then never render would be dead weight that still has to pass the drift check, and
// a standing invitation for someone to "fix" it.
//
// So: same data contract as NavRail (NAV_GROUPS from lib/nav.ts), different component, sizes
// from lib/tablet.ts. Colours still come from theme.ts — that half of the design system IS
// shared.

export default function DutyRail({
  active,
  onSelect,
  collapsed,
  onToggleCollapsed,
  pendingIncidents,
}: {
  active: ScreenId;
  onSelect: (id: ScreenId) => void;
  collapsed: boolean;
  onToggleCollapsed: () => void;
  pendingIncidents: number;
}) {
  const over = statusMeta("over");

  return (
    <div
      style={{
        width: collapsed ? rail.collapsed : rail.expanded,
        flex: "none",
        background: colors.railBg,
        borderRight: `1px solid ${colors.border}`,
        display: "flex",
        flexDirection: "column",
        transition: "width .18s ease",
      }}
    >
      <div style={{ flex: 1, overflowY: "auto", padding: "12px 0" }}>
        {NAV_GROUPS.map((group) => (
          <div key={group.label} style={{ marginBottom: 10 }}>
            <div
              style={{
                fontFamily: fonts.semiCondensed,
                fontSize: type.group,
                letterSpacing: ".16em",
                color: colors.textLabel,
                padding: collapsed ? "10px 0 6px" : "10px 16px 6px",
                textAlign: collapsed ? "center" : "left",
              }}
            >
              {collapsed ? group.collapsedLabel : group.label}
            </div>

            {group.items.map((item) => {
              const isActive = item.id === active;
              const badge = item.id === "incidents" && pendingIncidents > 0
                ? String(pendingIncidents)
                : item.badge;

              return (
                <button
                  key={item.id}
                  onClick={() => onSelect(item.id)}
                  aria-current={isActive ? "page" : undefined}
                  style={{
                    width: "100%",
                    minHeight: touch.rail,
                    display: "flex",
                    alignItems: "center",
                    gap: 12,
                    padding: collapsed ? "10px 0" : "10px 16px",
                    justifyContent: collapsed ? "center" : "flex-start",
                    border: "none",
                    borderLeft: `3px solid ${isActive ? colors.blue : "transparent"}`,
                    background: isActive ? colors.cardBgActive : "transparent",
                    cursor: "pointer",
                    textAlign: "left",
                  }}
                >
                  <span
                    style={{
                      width: rail.tile,
                      height: rail.tile,
                      flex: "none",
                      borderRadius: 8,
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                      background: isActive ? colors.blue : colors.cardBg,
                      border: `1px solid ${isActive ? colors.blue : colors.border}`,
                      color: isActive ? "#FFFFFF" : colors.textMuted,
                      fontFamily: fonts.mono,
                      fontSize: 15,
                      fontWeight: 500,
                    }}
                  >
                    {item.code}
                  </span>

                  {!collapsed && (
                    <span
                      style={{
                        flex: 1,
                        minWidth: 0,
                        fontFamily: fonts.body,
                        fontSize: type.label,
                        fontWeight: isActive ? 600 : 500,
                        color: isActive ? colors.headingBright : colors.textSecondary,
                      }}
                    >
                      {item.label}
                    </span>
                  )}

                  {!collapsed && badge ? (
                    <span
                      style={{
                        flex: "none",
                        minWidth: 26,
                        height: 24,
                        padding: "0 7px",
                        borderRadius: 6,
                        display: "inline-flex",
                        alignItems: "center",
                        justifyContent: "center",
                        gap: 3,
                        fontFamily: fonts.mono,
                        fontSize: 13,
                        color: over.t,
                        background: over.bg,
                        border: `1px solid ${over.bd}`,
                      }}
                    >
                      <span aria-hidden>{over.g}</span>
                      {badge}
                    </span>
                  ) : null}
                </button>
              );
            })}
          </div>
        ))}
      </div>

      <div style={{ flex: "none", borderTop: `1px solid ${colors.border}`, padding: 10 }}>
        <button
          onClick={onToggleCollapsed}
          style={{
            width: "100%",
            minHeight: touch.min,
            borderRadius: 8,
            border: `1px solid ${colors.border}`,
            background: colors.cardBg,
            color: colors.textMuted,
            fontFamily: fonts.semiCondensed,
            fontSize: 13,
            letterSpacing: ".1em",
            textTransform: "uppercase",
            cursor: "pointer",
          }}
        >
          {collapsed ? "›" : "‹ Collapse"}
        </button>
      </div>
    </div>
  );
}
