"use client";

import { colors, fonts } from "@/lib/theme";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { BrandScreen } from "@/components/Brandmark";
import { ErrorNotice } from "@/components/ErrorNotice";
import { TouchButton } from "@/components/ui-tablet/TouchButton";
import { DRIVER_ROLES } from "@/lib/roles";
import { logout } from "@/lib/auth";

// What a signed-in user without driver access sees. Two things here are load-bearing:
//
//   1. No TopBar and no rail behind it. A partial shell would leak the shape of the thing the
//      user is being kept out of, and reads as a bug rather than a decision.
//   2. The SIGN OUT button. Without it a valid session with the wrong role is a trap: every
//      reload restores that session and lands right back here, with no way out. On a mounted
//      tablet with no address bar that is not an inconvenience, it is a bricked device.

export default function AccessDeniedScreen({ role }: { role: string | null }) {
  const roleLabel = role && role.trim() ? role : "an account without driver access";

  return (
    <BrandScreen>
      <Panel style={{ padding: "22px 24px 24px" }}>
        <SectionLabel>Access denied</SectionLabel>
        <ErrorNotice
          title="This app is restricted."
          message={`You are signed in as ${roleLabel}. The Driver App is available to ${DRIVER_ROLES.join(", ")} accounts only. Ask dispatch to change your role.`}
          code="Auth.RoleNotPermitted"
        />
        <div style={{ marginTop: 18 }}>
          <TouchButton variant="secondary" onClick={() => void logout()}>
            Sign out
          </TouchButton>
        </div>
      </Panel>

      <div
        style={{
          textAlign: "center",
          marginTop: 16,
          fontFamily: fonts.body,
          fontSize: 14,
          color: colors.textDim,
          lineHeight: 1.6,
        }}
      >
        Dispatch Console · localhost:3001
      </div>
    </BrandScreen>
  );
}
