"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { DetailRow, Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip, MonoTag } from "@/components/ui/Chip";
import { getClaims } from "@/lib/auth";
import { BUDGET_ROLES, hasBudgetAccess } from "@/lib/roles";
import { EmptyNote, MockTag, Screen } from "@/components/screens/shared";

// Tab strip mirroring Dispatcher's Settings screen.
//
// The Session tab is not decoration: it shows the decoded token claims that decided whether
// this console rendered at all, which makes the role gate inspectable by hand. It is the
// fastest way to confirm that a Dispatcher account really is being rejected for the reason
// you think it is.

const TABS = ["Session", "Thresholds", "Connectors"] as const;
type Tab = (typeof TABS)[number];

export default function Settings() {
  const [tab, setTab] = useState<Tab>("Session");
  const claims = getClaims();

  return (
    <Screen eyebrow="Configuration" title="Settings">
      <div
        style={{
          display: "flex",
          gap: 6,
          marginBottom: 16,
          borderBottom: `1px solid ${colors.border}`,
          paddingBottom: 10,
          flexWrap: "wrap",
        }}
      >
        {TABS.map((t) => {
          const active = t === tab;
          return (
            <button
              key={t}
              onClick={() => setTab(t)}
              style={{
                padding: "5px 11px",
                borderRadius: 7,
                border: `1px solid ${active ? colors.borderActive : colors.borderStrong}`,
                background: active ? colors.cardBgActive : colors.cardBg,
                color: active ? colors.headingBright : colors.textMuted,
                fontFamily: fonts.semiCondensed,
                fontSize: 11.5,
                letterSpacing: ".06em",
                textTransform: "uppercase",
                cursor: "pointer",
              }}
            >
              {t}
            </button>
          );
        })}
      </div>

      {tab === "Session" && (
        <>
          <Panel style={{ marginBottom: 12 }}>
            <SectionLabel>Signed-in account</SectionLabel>
            {claims ? (
              <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                <DetailRow label="Email" value={claims.email || "—"} />
                <DetailRow
                  label="Role"
                  value={
                    <StatusChip
                      kind={hasBudgetAccess(claims.role) ? "ontime" : "over"}
                      label={claims.role || "—"}
                    />
                  }
                />
                <DetailRow label="Tenant type" value={claims.tenantType || "—"} />
                <DetailRow label="Tenant id" value={<MonoTag>{claims.tenantId || "—"}</MonoTag>} />
                <DetailRow label="User id" value={<MonoTag>{claims.sub || "—"}</MonoTag>} />
                <DetailRow
                  label="Token expires"
                  value={
                    claims.exp
                      ? new Date(claims.exp * 1000).toLocaleString("en-CA")
                      : "—"
                  }
                />
              </div>
            ) : (
              <EmptyNote>No readable session claims.</EmptyNote>
            )}
          </Panel>

          <Panel>
            <SectionLabel>Access rule</SectionLabel>
            <div
              style={{
                fontFamily: fonts.body,
                fontSize: 12.5,
                color: colors.textSecondary,
                lineHeight: 1.65,
              }}
            >
              This console is limited to {BUDGET_ROLES.join(" and ")} accounts. The check you see
              above runs in the browser against the access token, so it decides what renders — not
              what the API will serve. Once the budgeting endpoints land they carry the server-side
              <code style={{ fontFamily: fonts.mono, fontSize: 11.5 }}> BudgetAccess </code>
              policy, which is the boundary that actually protects data.
            </div>
          </Panel>
        </>
      )}

      {tab === "Thresholds" && (
        <Panel>
          <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 11 }}>
            <SectionLabel>Variance thresholds</SectionLabel>
            <MockTag />
          </div>
          <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
            <DetailRow label="On plan" value="±5%" />
            <DetailRow label="Watch" value="±5% to ±15%" />
            <DetailRow label="Over threshold" value="Beyond ±15%" />
          </div>
          <div
            style={{
              fontFamily: fonts.body,
              fontSize: 11.5,
              color: colors.textDim,
              lineHeight: 1.6,
              marginTop: 12,
            }}
          >
            Fixed in lib/data.ts for now. They become tenant settings when the Budgeting API
            lands in Stage 6.1.
          </div>
        </Panel>
      )}

      {tab === "Connectors" && (
        <Panel>
          <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 11 }}>
            <SectionLabel>QuickBooks Online</SectionLabel>
            <StatusChip kind="off" label="Stage 6.1" />
          </div>
          <div
            style={{
              fontFamily: fonts.body,
              fontSize: 12.5,
              color: colors.textSecondary,
              lineHeight: 1.65,
            }}
          >
            Actuals will be reconciled from QuickBooks, which stays read-only from the platform&rsquo;s
            side. This platform remains the source of truth for budget codes, and every
            transaction is tagged at creation rather than reconciled after the fact.
          </div>
        </Panel>
      )}
    </Screen>
  );
}
