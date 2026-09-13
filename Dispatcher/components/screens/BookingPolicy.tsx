"use client";

import { colors } from "@/lib/theme";
import { PageHeader } from "@/components/ui/Panel";
import BookingPolicyPanel from "@/components/screens/settings/BookingPolicyPanel";

// Booking Policy — a screen of the Community Booking app (it used to be a Settings tab). The
// panel is unchanged: it keeps its own fetch of /api/booking/settings and its Owner-only save
// gate; this file is only the page frame around it.
export default function BookingPolicy() {
  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow="Community booking follows its own rules" title="Booking Policy" />
      </div>
      <div
        style={{
          flex: 1,
          minHeight: 0,
          overflowY: "auto",
          padding: "22px 26px",
          borderTop: `1px solid ${colors.border}`,
          background: colors.detailBg,
        }}
      >
        <BookingPolicyPanel />
      </div>
    </div>
  );
}
