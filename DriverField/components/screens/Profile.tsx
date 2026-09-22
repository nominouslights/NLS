"use client";

import { colors, fonts } from "@/lib/theme";
import { gap, type } from "@/lib/tablet";
import { Panel } from "@/components/ui/Panel";
import { Screen, MockTag, Heading, CardRow, FieldLine, TabletChip } from "./shared";
import { clearances, clientContracts, credentials, currentDriver } from "@/lib/data";

// The driver's own record: licence, credentials, client clearances, and the narrow read-only
// Client & Contract slice architecture §6 allows — "which client is this trip for, so I load
// the right manifest template", and nothing more.
//
// Credential and clearance expiry banding comes from credentialKind() in lib/data.ts, shared
// with everything else that expires, so a lapsed licence and a lapsed Alamos clearance are
// never rendered as differently urgent at the same number of days. The Alamos clearance is the
// one that traces to a real June 2026 incident, and it is also eligibility rule 5.

export default function Profile() {
  return (
    <Screen
      eyebrow={`${currentDriver.homeBase} · ${currentDriver.employeeNumber}`}
      title={currentDriver.name}
      right={<MockTag />}
    >
      <Panel style={{ padding: "18px 20px", marginBottom: gap.section }}>
        <div style={{ display: "flex", gap: gap.section, flexWrap: "wrap" }}>
          <FieldLine label="Licence class" value={`Class ${currentDriver.licenceClass}`} />
          <FieldLine label="Licence expires" value={currentDriver.licenceExpiresOn} />
          <FieldLine label="Phone" value={currentDriver.phone} />
          <FieldLine label="Home base" value={currentDriver.homeBase} />
        </div>
      </Panel>

      <Heading right={<MockTag />}>Credentials</Heading>
      {credentials.map((c) => (
        <CardRow key={c.id}>
          <div style={{ flex: 1, minWidth: 0 }}>
            <FieldLine label={c.reference} value={c.kind} />
          </div>
          <div style={{ flex: "none", width: 160 }}>
            <span style={{ fontFamily: fonts.mono, fontSize: 15, color: colors.textDim }}>
              {c.expiresOn}
            </span>
          </div>
          <div style={{ flex: "none" }}>
            <TabletChip
              kind={c.ck}
              label={c.ck === "over" ? "Expired" : c.ck === "soon" ? "Expiring" : "Valid"}
            />
          </div>
        </CardRow>
      ))}

      <Heading right={<MockTag />}>Client clearances</Heading>
      {clearances.map((c) => (
        <CardRow key={c.id}>
          <div style={{ flex: 1, minWidth: 0 }}>
            <FieldLine label={c.client} value={c.kind} />
          </div>
          <div style={{ flex: "none", width: 160 }}>
            <span style={{ fontFamily: fonts.mono, fontSize: 15, color: colors.textDim }}>
              {c.expiresOn}
            </span>
          </div>
          <div style={{ flex: "none" }}>
            <TabletChip
              kind={c.ck}
              label={c.ck === "over" ? "Expired" : c.ck === "soon" ? "Expiring" : "Active"}
            />
          </div>
        </CardRow>
      ))}
      <div
        style={{
          fontFamily: fonts.body,
          fontSize: 14,
          color: colors.textDim,
          marginTop: 4,
          lineHeight: 1.55,
        }}
      >
        A lapsed clearance removes the matching trips from your Open list — eligibility rule 5.
      </div>

      <Heading right={<MockTag />}>Clients you run for</Heading>
      {clientContracts.map((c) => (
        <CardRow key={c.id}>
          <div style={{ flex: "none", width: 220 }}>
            <span
              style={{
                fontFamily: fonts.body,
                fontSize: type.value,
                fontWeight: 600,
                color: colors.headingBright,
              }}
            >
              {c.client}
            </span>
          </div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <FieldLine label={c.manifestTemplate} value={c.note} />
          </div>
          <div style={{ flex: "none" }}>
            <TabletChip
              kind={c.requiresClearance ? "soon" : "off"}
              label={c.requiresClearance ? "Clearance required" : "Open"}
            />
          </div>
        </CardRow>
      ))}
    </Screen>
  );
}
