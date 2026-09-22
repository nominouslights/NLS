"use client";

import { colors, fonts, statusMeta, type StatusKind } from "@/lib/theme";
import { radius, touch, type } from "@/lib/tablet";
import { enqueue } from "@/lib/sync/queue";
import type { DutyState } from "@/lib/types";

// APP-LOCAL. The duty-status switch — the single most-used control in the app.
//
// Three large explicit buttons, not a cycle or a dropdown. Duty status is the record a
// regulator reads; a driver must be able to change it correctly at a glance, with a glove, in
// a vehicle, and must never be able to change it by accident with one stray tap on a control
// that cycles.
//
// The wire strings are exact: "Off Duty" / "On Duty" / "Driving" match HosDisplay's constants
// byte for byte, pinned by lib/wire.test.ts. Do not "tidy" the casing or the spacing.

const OPTIONS: { value: DutyState; kind: StatusKind }[] = [
  { value: "Off Duty", kind: "off" },
  { value: "On Duty", kind: "soon" },
  { value: "Driving", kind: "ontime" },
];

export function DutyControl({
  duty,
  onChange,
}: {
  duty: DutyState;
  onChange: (next: DutyState) => void;
}) {
  async function select(next: DutyState) {
    if (next === duty) return;
    // Every mutation goes through the sync queue — never a direct API call, never an in-place
    // mutation of lib/data.ts. See lib/sync/types.ts for why this rule is what makes the
    // offline batch additive instead of a rewrite.
    await enqueue("hos.record", { duty: next, at: new Date().toISOString() });
    onChange(next);
  }

  return (
    <div
      role="group"
      aria-label="Duty status"
      style={{ display: "flex", gap: 10, flexWrap: "wrap" }}
    >
      {OPTIONS.map((o) => {
        const m = statusMeta(o.kind);
        const active = duty === o.value;
        return (
          <button
            key={o.value}
            onClick={() => void select(o.value)}
            aria-pressed={active}
            style={{
              flex: "1 1 0",
              minWidth: 180,
              minHeight: touch.primary + 12,
              borderRadius: radius.control,
              border: `2px solid ${active ? m.c : colors.border}`,
              background: active ? m.c : colors.cardBg,
              color: active ? m.bt : colors.textMuted,
              fontFamily: fonts.semiCondensed,
              fontSize: type.value,
              fontWeight: 600,
              letterSpacing: ".06em",
              textTransform: "uppercase",
              cursor: "pointer",
              display: "inline-flex",
              alignItems: "center",
              justifyContent: "center",
              gap: 10,
            }}
          >
            <span aria-hidden style={{ fontSize: type.value }}>
              {m.g}
            </span>
            {o.value}
          </button>
        );
      })}
    </div>
  );
}
