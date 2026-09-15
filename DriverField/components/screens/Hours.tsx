"use client";

import { colors, fonts, statusMeta } from "@/lib/theme";
import { gap, radius, type } from "@/lib/tablet";
import { Panel } from "@/components/ui/Panel";
import { TouchTile } from "@/components/ui-tablet/TouchTile";
import { DutyControl } from "@/components/ui-tablet/DutyControl";
import { Screen, MockTag, Heading, CardRow, FieldLine, TabletChip, Num } from "./shared";
import type { DutyState } from "@/lib/types";
import {
  MAX_CYCLE_H_PER_7_DAYS,
  MAX_DRIVING_H_PER_DAY,
  MAX_ON_DUTY_H_PER_DAY,
  formatDurationH,
  hosEntries,
  hosRemaining,
} from "@/lib/data";

// Hours of service, per CVDHS. The limits come from lib/data.ts as named constants rather than
// being inlined here, so a future report and this screen cannot disagree about what "remaining"
// means.
//
// The Source column is load-bearing and is why lib/wire.test.ts exists: "Driver App" and
// "Manual (paper backup)" are the exact strings HosDisplay emits. Until very recently the API
// hardcoded every submission to the manual value regardless of where it came from, which would
// have mislabelled every entry this app ever makes.

export default function Hours({
  duty,
  onDutyChange,
}: {
  duty: DutyState;
  onDutyChange: (next: DutyState) => void;
}) {
  const hos = hosRemaining();
  const week = hosEntries.slice(0, 7);
  const maxDay = Math.max(...week.map((e) => e.onDutyH), MAX_ON_DUTY_H_PER_DAY);

  return (
    <Screen eyebrow="Compliance" title="Hours of service" right={<MockTag />}>
      <div style={{ display: "flex", gap: gap.row, marginBottom: gap.section }}>
        <TouchTile
          label={`Driving (max ${MAX_DRIVING_H_PER_DAY}h)`}
          value={formatDurationH(hos.drivingRemainingH)}
          unit="left"
          kind={hos.hk}
          statusLabel={hos.hk === "over" ? "At limit" : hos.hk === "soon" ? "Low" : "OK"}
        />
        <TouchTile
          label={`On duty (max ${MAX_ON_DUTY_H_PER_DAY}h)`}
          value={formatDurationH(hos.onDutyRemainingH)}
          unit="left"
          kind={hos.onDutyRemainingH < 1 ? "over" : hos.onDutyRemainingH < 3 ? "soon" : "ontime"}
          statusLabel="Today"
        />
        <TouchTile
          label={`Cycle (max ${MAX_CYCLE_H_PER_7_DAYS}h / 7 days)`}
          value={formatDurationH(hos.cycleRemainingH)}
          unit="left"
          kind={hos.cycleRemainingH < 4 ? "over" : hos.cycleRemainingH < 10 ? "soon" : "ontime"}
          statusLabel="Rolling"
        />
      </div>

      <Heading>Duty status</Heading>
      <Panel style={{ padding: "18px 20px", marginBottom: gap.section }}>
        <DutyControl duty={duty} onChange={onDutyChange} />
      </Panel>

      <Heading>Last 7 days</Heading>
      <Panel style={{ padding: "18px 20px", marginBottom: gap.section }}>
        <div style={{ display: "flex", alignItems: "flex-end", gap: 10, height: 130 }}>
          {[...week].reverse().map((e) => {
            const kind = e.onDutyH >= MAX_ON_DUTY_H_PER_DAY ? "over" : e.onDutyH >= 9 ? "soon" : "ontime";
            const m = statusMeta(kind);
            return (
              <div
                key={e.id}
                style={{ flex: "1 1 0", display: "flex", flexDirection: "column", alignItems: "center", gap: 6 }}
                title={`${e.date} · ${formatDurationH(e.onDutyH)} on duty`}
              >
                <span style={{ fontFamily: fonts.mono, fontSize: 13, color: m.t }}>
                  {e.onDutyH > 0 ? formatDurationH(e.onDutyH) : "—"}
                </span>
                <div
                  style={{
                    width: "100%",
                    height: Math.max(4, (e.onDutyH / maxDay) * 84),
                    borderRadius: radius.control,
                    background: m.c,
                  }}
                />
                <span style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textDim }}>
                  {e.date.slice(5)}
                </span>
              </div>
            );
          })}
        </div>
        <div
          style={{
            fontFamily: fonts.body,
            fontSize: 14,
            color: colors.textDim,
            marginTop: 12,
            lineHeight: 1.55,
          }}
        >
          Bar height is on-duty hours. Colour and the figure above each bar say the same thing —
          neither is the only signal.
        </div>
      </Panel>

      <Heading right={<MockTag />}>Entries</Heading>
      {hosEntries.map((e) => (
        <CardRow key={e.id}>
          <div style={{ flex: "none", width: 130 }}>
            <Num size={type.label} color={colors.headingBright}>
              {e.date}
            </Num>
          </div>
          <div style={{ flex: "none", width: 150 }}>
            <TabletChip
              kind={e.duty === "Driving" ? "ontime" : e.duty === "On Duty" ? "soon" : "off"}
              label={e.duty}
            />
          </div>
          <div style={{ flex: "none", width: 220 }}>
            <FieldLine
              label="Driving / on duty"
              value={`${formatDurationH(e.drivingH)} / ${formatDurationH(e.onDutyH)}`}
            />
          </div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <FieldLine
              label={e.source}
              value={e.enteredBy ? `Entered by ${e.enteredBy}${e.note ? ` — ${e.note}` : ""}` : e.note || "—"}
            />
          </div>
        </CardRow>
      ))}
    </Screen>
  );
}
