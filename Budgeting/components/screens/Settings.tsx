"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ActionButton } from "@/components/ui/Button";
import { DetailRow, Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip, MonoTag } from "@/components/ui/Chip";
import { ErrorNotice } from "@/components/ErrorNotice";
import ProfileForm from "@/components/ProfileForm";
import { getClaims } from "@/lib/auth";
import { updateMyProfile, type MyProfile } from "@/lib/api/identity";
import { BUDGET_ROLES, hasBudgetAccess } from "@/lib/roles";
import { EmptyNote, MockTag, Screen } from "@/components/screens/shared";

// Tab strip mirroring Dispatcher's Settings screen.
//
// Profile leads because it is the one tab a user comes here to *change*; the rest are things to
// look at. It lives here rather than on the nav rail because Settings already answers "who am I"
// — the Session tab below shows the very claims that decided whether this console rendered —
// and splitting the editable half onto its own rail item would put two answers to one question a
// click apart. The rail is a domain rail (PLANNING / PERFORMANCE); a profile belongs to neither.
//
// The Session tab is not decoration: it shows the decoded token claims that decided whether
// this console rendered at all, which makes the role gate inspectable by hand. It is the
// fastest way to confirm that a Dispatcher account really is being rejected for the reason
// you think it is. Note it reads the *token*, while Profile reads the database — which is why
// a name saved a moment ago shows there and not here.

const TABS = ["Profile", "Session", "Thresholds", "Connectors"] as const;
type Tab = (typeof TABS)[number];

export default function Settings({
  profile,
  profileError,
  onRetryProfile,
  onProfileSaved,
}: {
  /** null while loading — Console owns the fetch, so the TopBar can show the name too. */
  profile: MyProfile | null;
  profileError: { message: string; code: string } | null;
  onRetryProfile: () => void;
  onProfileSaved: (profile: MyProfile) => void;
}) {
  const [tab, setTab] = useState<Tab>("Profile");
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

      {tab === "Profile" && (
        <>
          {profileError && (
            <>
              <ErrorNotice
                title="Couldn't load your profile"
                message={profileError.message}
                code={profileError.code}
              />
              <div style={{ marginTop: 10 }}>
                <ActionButton onClick={onRetryProfile}>RETRY</ActionButton>
              </div>
            </>
          )}
          {profile === null && !profileError && <EmptyNote>Loading your profile…</EmptyNote>}
          {profile !== null && !profileError && (
            // Keyed on the user id so the form's state seeds from the profile with no syncing
            // effect — it remounts if the signed-in account ever changes under it.
            <ProfileForm
              key={profile.userId}
              profile={profile}
              onSave={updateMyProfile}
              onSaved={onProfileSaved}
            />
          )}
        </>
      )}

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
