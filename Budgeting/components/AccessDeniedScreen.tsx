"use client";

import { colors, fonts } from "@/lib/theme";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { ActionButton } from "@/components/ui/Button";
import { BrandScreen } from "@/components/Brandmark";
import { ErrorNotice } from "@/components/ErrorNotice";
import { BUDGET_ROLES } from "@/lib/roles";
import { logout } from "@/lib/auth";

// What a signed-in user without budget access sees. Two things here are load-bearing:
//
//   1. No TopBar and no NavRail behind it. A partial shell would leak the shape of the thing
//      the user is being kept out of, and reads as a bug rather than a decision.
//   2. The SIGN OUT button. Without it a valid session with the wrong role is a trap: every
//      reload restores that session and lands right back here, with no way out.

export default function AccessDeniedScreen({ role }: { role: string | null }) {
  const roleLabel = role && role.trim() ? role : "an account without budget access";

  return (
    <BrandScreen>
      <Panel style={{ padding: "20px 22px 22px" }}>
        <SectionLabel>Access denied</SectionLabel>
        <ErrorNotice
          title="This console is restricted."
          message={`You are signed in as ${roleLabel}. The Budgeting console is available to ${BUDGET_ROLES.join(" and ")} accounts only. Ask the owner to change your role, or use the Dispatch Console.`}
          code="Auth.RoleNotPermitted"
        />
        <div style={{ marginTop: 14 }}>
          <ActionButton variant="secondary" onClick={() => void logout()}>
            SIGN OUT
          </ActionButton>
        </div>
      </Panel>

      <div
        style={{
          textAlign: "center",
          marginTop: 14,
          fontFamily: fonts.body,
          fontSize: 11.5,
          color: colors.textDim,
          lineHeight: 1.6,
        }}
      >
        Dispatch Console · localhost:3001
      </div>
    </BrandScreen>
  );
}
