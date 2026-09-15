"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { gap, type } from "@/lib/tablet";
import { Panel } from "@/components/ui/Panel";
import { TouchButton } from "@/components/ui-tablet/TouchButton";
import { ThreeStateControl } from "@/components/ui-tablet/ThreeStateControl";
import { StatusBanner } from "@/components/ui-tablet/StatusBanner";
import { Screen, MockTag, Heading, CardRow, FieldLine, TabletChip } from "./shared";
import { enqueue } from "@/lib/sync/queue";
import { assignedVehicle, dvirChecklist, dvirSubmissions } from "@/lib/data";
import type { CheckState } from "@/lib/types";

// Driver Vehicle Inspection Report — NSC Standard 11, grouped the way the standard is written.
//
// This is the closest thing in the app to a live endpoint: POST /api/fleet/inspections already
// exists, and its InspectionRequest already carries Source (DriverApp | Dispatcher), Type,
// Checklist, Defects with severity, weather, road conditions, fuel and attestations. It is
// genuinely one `source: "DriverApp"` away from being wired. It is not wired here because this
// batch is shell-and-mock by decision — see DriverField/CLAUDE.md.
//
// Photo attachment is rendered DISABLED WITH ITS REASON rather than omitted. There is no
// attachment endpoint for inspections or defects anywhere on the backend (only driver
// credentials have object storage), and a field app that silently lacks defect photos is a gap
// somebody should be able to see.

export default function Inspection() {
  const [answers, setAnswers] = useState<Record<string, CheckState>>({});
  const [submitted, setSubmitted] = useState(false);

  const total = dvirChecklist.reduce((n, g) => n + g.items.length, 0);
  const answered = Object.keys(answers).length;
  const defects = Object.values(answers).filter((a) => a === "defect").length;
  const complete = answered === total;

  async function submit() {
    await enqueue("dvir.submit", {
      vehicleId: assignedVehicle.id,
      type: "PreTrip",
      // The backend defaults Source to Dispatcher; a driver submission must say so explicitly
      // or the inspection is attributed to the wrong place in the compliance record.
      source: "DriverApp",
      checklist: answers,
    });
    setSubmitted(true);
  }

  return (
    <Screen
      eyebrow={`${assignedVehicle.unit} · ${assignedVehicle.description}`}
      title="Pre-trip inspection"
      right={
        <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
          <TabletChip
            kind={defects > 0 ? "soon" : complete ? "ontime" : "info"}
            label={`${answered} of ${total} checked`}
          />
          <MockTag />
        </div>
      }
    >
      {submitted && (
        <StatusBanner kind="soon" title="Held on this device.">
          Your inspection was recorded locally. Sync is not enabled in this build, so it has not
          reached the server and does not yet satisfy a compliance record.
        </StatusBanner>
      )}

      {dvirChecklist.map((group) => (
        <div key={group.group}>
          <Heading>{group.group}</Heading>
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {group.items.map((item) => (
              <ThreeStateControl
                key={item}
                item={item}
                value={answers[item] ?? null}
                onChange={(next) => setAnswers((a) => ({ ...a, [item]: next }))}
              />
            ))}
          </div>
        </div>
      ))}

      <Heading>Certify</Heading>
      <Panel style={{ padding: "18px 20px" }}>
        <div
          style={{
            fontFamily: fonts.body,
            fontSize: type.label,
            color: colors.textSecondary,
            lineHeight: 1.6,
          }}
        >
          I certify that I have inspected this vehicle in accordance with NSC Standard 11 and
          that the entries above are accurate.
        </div>
        <div style={{ display: "flex", gap: 12, marginTop: 16, flexWrap: "wrap" }}>
          <TouchButton
            onClick={() => void submit()}
            disabled={!complete}
            disabledReason={`${total - answered} item(s) still unanswered — an inspection cannot be certified with blanks.`}
          >
            Certify &amp; submit
          </TouchButton>
          <TouchButton
            variant="secondary"
            disabled
            disabledReason="No attachment endpoint exists for inspections or defects on the backend yet."
          >
            Attach photo
          </TouchButton>
        </div>
      </Panel>

      <Heading right={<MockTag />}>Recent inspections</Heading>
      {dvirSubmissions.map((s) => (
        <CardRow key={s.id}>
          <div style={{ flex: "none", width: 180 }}>
            <span style={{ fontFamily: fonts.mono, fontSize: 15, color: colors.textDim }}>
              {s.performedAt.slice(0, 16).replace("T", " ")}
            </span>
          </div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <FieldLine
              label={`${s.type} · ${s.unit}`}
              value={`${s.odometerKm.toLocaleString("en-CA")} km${s.defectCount > 0 ? ` · ${s.defectCount} defect(s)` : ""}`}
            />
          </div>
          <div style={{ flex: "none" }}>
            <TabletChip kind={s.rk} label={s.result} />
          </div>
        </CardRow>
      ))}

      <div style={{ height: gap.page }} />
    </Screen>
  );
}
