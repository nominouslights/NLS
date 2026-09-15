"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { gap, radius, touch, type } from "@/lib/tablet";
import { Panel } from "@/components/ui/Panel";
import { TouchButton } from "@/components/ui-tablet/TouchButton";
import { StatusBanner } from "@/components/ui-tablet/StatusBanner";
import { Screen, MockTag, Heading, CardRow, FieldLine, TabletChip, EmptyNote } from "./shared";
import { enqueue } from "@/lib/sync/queue";
import { incidentTypes, incidents } from "@/lib/data";

// Incident & fault reporting — core Driver Field App scope per architecture §6.
//
// THIS ONE HAS NO BACKEND AT ALL. Backend/src/Incidents is a two-file stub: DI is wired, but
// there is no domain, no handler and no endpoint. So unlike the DVIR screen — which is one
// field away from a live endpoint — everything here is inventing a shape nobody has agreed to,
// and the form deliberately stays close to what a driver would tell dispatch over Zello rather
// than guessing at a schema.

export default function Incidents() {
  const [kind, setKind] = useState(incidentTypes[0]);
  const [location, setLocation] = useState("");
  const [narrative, setNarrative] = useState("");
  const [filed, setFiled] = useState(false);

  async function submit() {
    await enqueue("incident.report", { type: kind, location, narrative });
    setFiled(true);
    setLocation("");
    setNarrative("");
  }

  return (
    <Screen eyebrow="Compliance" title="Incidents" right={<MockTag />}>
      <StatusBanner kind="off" title="No incidents API exists yet.">
        The backend&rsquo;s Incidents module is a stub — no endpoint, no schema. This form records
        locally so the screen can be reviewed; the shape it posts is not a contract.
      </StatusBanner>

      {filed && (
        <StatusBanner kind="soon" title="Held on this device.">
          Your report was recorded locally and has not reached anyone. For anything urgent, call
          dispatch.
        </StatusBanner>
      )}

      <Heading>Report something</Heading>
      <Panel style={{ padding: "18px 20px", marginBottom: gap.section }}>
        <div style={{ display: "flex", flexWrap: "wrap", gap: 10, marginBottom: 16 }}>
          {incidentTypes.map((t) => (
            <button
              key={t}
              onClick={() => setKind(t)}
              aria-pressed={kind === t}
              style={{
                minHeight: touch.min,
                padding: "0 16px",
                borderRadius: radius.control,
                border: `1px solid ${kind === t ? colors.borderActive : colors.border}`,
                background: kind === t ? colors.cardBgActive : colors.inputBg,
                color: kind === t ? colors.headingBright : colors.textMuted,
                fontFamily: fonts.body,
                fontSize: 15,
                cursor: "pointer",
              }}
            >
              {t}
            </button>
          ))}
        </div>

        <input
          value={location}
          onChange={(e) => setLocation(e.target.value)}
          placeholder="Where? (e.g. PR 391, km 84)"
          style={inputStyle}
        />
        <textarea
          value={narrative}
          onChange={(e) => setNarrative(e.target.value)}
          placeholder="What happened?"
          rows={4}
          style={{ ...inputStyle, minHeight: 120, paddingTop: 14, resize: "vertical" }}
        />

        <div style={{ display: "flex", gap: 12, marginTop: 4, flexWrap: "wrap" }}>
          <TouchButton
            onClick={() => void submit()}
            disabled={!narrative.trim()}
            disabledReason="Describe what happened before filing."
          >
            File report
          </TouchButton>
          <TouchButton
            variant="secondary"
            disabled
            disabledReason="No attachment endpoint exists on the backend yet."
          >
            Attach photo
          </TouchButton>
        </div>
      </Panel>

      <Heading right={<MockTag />}>Your reports</Heading>
      {incidents.length === 0 ? (
        <EmptyNote>You have not filed anything.</EmptyNote>
      ) : (
        incidents.map((i) => (
          <CardRow key={i.id}>
            <div style={{ flex: "none", width: 170 }}>
              <span style={{ fontFamily: fonts.mono, fontSize: 15, color: colors.textDim }}>
                {i.reportedAt.slice(0, 16).replace("T", " ")}
              </span>
            </div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <FieldLine label={`${i.type} · ${i.location}`} value={i.narrative} />
            </div>
            <div style={{ flex: "none", display: "flex", gap: 10 }}>
              <TabletChip kind={i.ik} label={i.status} />
            </div>
          </CardRow>
        ))
      )}
    </Screen>
  );
}

const inputStyle = {
  width: "100%",
  minHeight: touch.primary,
  padding: "0 16px",
  marginBottom: 12,
  borderRadius: radius.control,
  border: `1px solid ${colors.borderStrong}`,
  background: colors.inputBg,
  color: colors.textPrimary,
  fontFamily: fonts.body,
  fontSize: type.label,
} as const;
