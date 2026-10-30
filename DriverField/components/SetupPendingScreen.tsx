"use client";

import { colors, fonts } from "@/lib/theme";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { BrandScreen, BrandBody } from "@/components/Brandmark";

// Shown when the backend reports that first-run setup is still open — no user exists anywhere
// on the platform yet.
//
// Where Dispatcher offers a create-administrator form here, this app deliberately does not, for
// the same reason Budgeting does not: first-run setup is a one-time global gate that
// permanently closes on first use, and several apps offering it is a race with a confusing
// loser. A driver is also the last person who should be minting the platform's first Owner
// account from a vehicle.

export default function SetupPendingScreen() {
  return (
    <BrandScreen>
      <Panel style={{ padding: "22px 24px 24px" }}>
        <SectionLabel>Platform not set up yet</SectionLabel>
        <BrandBody>
          No accounts exist on this platform yet. The first administrator is created in the
          Dispatch Console — once that is done, and dispatch has linked your account to your
          driver record, sign in here.
        </BrandBody>
        <div
          style={{
            fontFamily: fonts.mono,
            fontSize: 14,
            color: colors.textDim,
            background: colors.inputBg,
            border: `1px solid ${colors.borderSubtle}`,
            borderRadius: 8,
            padding: "12px 14px",
            marginTop: 16,
          }}
        >
          Dispatch Console · localhost:3001
        </div>
      </Panel>
    </BrandScreen>
  );
}
