"use client";

import { colors, fonts } from "@/lib/theme";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { BrandScreen } from "@/components/Brandmark";

// Shown when the backend reports that first-run setup is still open — no user exists anywhere
// on the platform yet.
//
// Where Dispatcher offers a create-administrator form here, this console deliberately does not.
// First-run setup is a one-time global gate that permanently closes on first use; two apps both
// offering it is a race with a confusing loser. So this screen explains where to go instead.

export default function SetupPendingScreen() {
  return (
    <BrandScreen>
      <Panel style={{ padding: "20px 22px 22px" }}>
        <SectionLabel>Platform not set up yet</SectionLabel>
        <div
          style={{
            fontFamily: fonts.body,
            fontSize: 12.5,
            color: colors.textSecondary,
            lineHeight: 1.65,
            marginTop: 2,
          }}
        >
          No accounts exist on this platform yet. The first administrator is created in the
          Dispatch Console — once that is done, sign in here with an Owner or Accountant account.
        </div>
        <div
          style={{
            fontFamily: fonts.mono,
            fontSize: 11.5,
            color: colors.textDim,
            background: colors.inputBg,
            border: `1px solid ${colors.borderSubtle}`,
            borderRadius: 8,
            padding: "9px 12px",
            marginTop: 13,
          }}
        >
          Dispatch Console · localhost:3001
        </div>
      </Panel>
    </BrandScreen>
  );
}
