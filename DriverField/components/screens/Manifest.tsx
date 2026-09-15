"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { gap, radius, touch, type } from "@/lib/tablet";
import { Panel } from "@/components/ui/Panel";
import { StatusButton } from "@/components/ui-tablet/TouchButton";
import { StatusBanner } from "@/components/ui-tablet/StatusBanner";
import { Screen, MockTag, CardRow, FieldLine, TabletChip, EmptyNote, Heading } from "./shared";
import { enqueue } from "@/lib/sync/queue";
import { activeTrip, badgeIndex, manifestFor, trips } from "@/lib/data";

// The passenger manifest, and the badge-scan panel from architecture §5.2.
//
// §5.2's premise: the driver scans each badge as crew board, which both builds the manifest AND
// fires the "In Transit" status update with zero extra data entry. The scan field here is a
// plain text input, which is exactly right for a hardware HID or Bluetooth scanner — those
// present as a keyboard and type into whatever has focus.
//
// WHAT IS NOT SETTLED, and it needs an answer before the offline batch: if the badges are NFC
// rather than barcode, this input is the wrong mechanism entirely. Web NFC (NDEFReader) is
// Chrome-on-Android only, HTTPS and user-gesture gated; BarcodeDetector is camera-based and
// Chromium-only. Both are real constraints of shipping this as a PWA rather than a native app.
// Confirm the badge technology with whoever runs the tablets. See DriverField/CLAUDE.md.
//
// Also unbuilt on the backend: boarding is a whole-document PUT of the manifest today, not a
// per-passenger action, and there is no no-show flag anywhere. Both are queued as commands here
// so the screen is already shaped for the endpoints it needs rather than the ones that exist.

export default function Manifest({ tripId }: { tripId: string | null }) {
  const trip = trips.find((t) => t.id === tripId) ?? activeTrip;
  const rows = manifestFor(trip.id);

  const [boarded, setBoarded] = useState<Record<string, boolean>>(
    Object.fromEntries(rows.map((r) => [r.id, r.boarded])),
  );
  const [noShow, setNoShow] = useState<Record<string, boolean>>(
    Object.fromEntries(rows.map((r) => [r.id, r.noShow])),
  );
  const [scan, setScan] = useState("");
  const [scanNote, setScanNote] = useState<string | null>(null);

  const boardedCount = rows.filter((r) => boarded[r.id]).length;
  const firstBoard = boardedCount === 0;

  async function board(rowId: string, on: boolean) {
    await enqueue("manifest.board", { tripId: trip.id, manifestRowId: rowId, boarded: on });
    setBoarded((b) => ({ ...b, [rowId]: on }));
    if (on) setNoShow((n) => ({ ...n, [rowId]: false }));
  }

  async function markNoShow(rowId: string, on: boolean) {
    await enqueue("manifest.board", { tripId: trip.id, manifestRowId: rowId, noShow: on });
    setNoShow((n) => ({ ...n, [rowId]: on }));
    if (on) setBoarded((b) => ({ ...b, [rowId]: false }));
  }

  async function submitScan() {
    const badge = badgeIndex.get(scan.trim());
    if (!badge) {
      setScanNote(`No crew member on this manifest carries badge ${scan.trim() || "—"}.`);
      return;
    }
    const row = rows.find((r) => r.badgeId === badge.badgeId);
    if (row) {
      await board(row.id, true);
      setScanNote(`${badge.passenger} boarded.`);
    }
    setScan("");
  }

  return (
    <Screen
      eyebrow={`${trip.tripNumber} · ${trip.clientName}`}
      title={`${trip.origin} → ${trip.destination}`}
      right={
        <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
          <TabletChip
            kind={boardedCount === rows.length ? "ontime" : "info"}
            label={`${boardedCount} of ${rows.length} boarded`}
          />
          <MockTag />
        </div>
      }
    >
      {firstBoard && (
        <StatusBanner kind="info" title="The first boarding starts this trip.">
          Marking anyone aboard sets {trip.tripNumber} to In Transit — architecture §5.2, so a
          driver never has to update status separately. Not wired to the server in this build.
        </StatusBanner>
      )}

      <Heading>Badge scan</Heading>
      <Panel style={{ padding: "18px 20px", marginBottom: gap.section }}>
        <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
          <input
            value={scan}
            onChange={(e) => setScan(e.target.value)}
            onKeyDown={(e) => {
              // A hardware scanner types the code then sends Enter.
              if (e.key === "Enter") {
                e.preventDefault();
                void submitScan();
              }
            }}
            placeholder="Scan or type a badge number"
            autoFocus
            style={{
              flex: 1,
              minHeight: touch.primary,
              padding: "0 16px",
              borderRadius: radius.control,
              border: `1px solid ${colors.borderStrong}`,
              background: colors.inputBg,
              color: colors.textPrimary,
              fontFamily: fonts.mono,
              fontSize: type.value,
            }}
          />
        </div>
        <div
          style={{
            fontFamily: fonts.body,
            fontSize: 14,
            color: colors.textDim,
            marginTop: 10,
            lineHeight: 1.55,
          }}
        >
          {scanNote ??
            "A hardware scanner types into this field and sends Enter. Camera and NFC scanning are not available in this build."}
        </div>
      </Panel>

      <Heading>Passengers</Heading>
      {rows.length === 0 ? (
        <EmptyNote>No passengers on this manifest.</EmptyNote>
      ) : (
        rows.map((row) => {
          const isBoarded = boarded[row.id];
          const isNoShow = noShow[row.id];
          return (
            <CardRow key={row.id} muted={isNoShow}>
              <div style={{ flex: 1, minWidth: 0 }}>
                <FieldLine label={row.employer} value={row.passenger} />
              </div>
              <div style={{ flex: "none", width: 160 }}>
                <span style={{ fontFamily: fonts.mono, fontSize: 15, color: colors.textDim }}>
                  {row.badgeId}
                </span>
              </div>
              <div style={{ flex: "none", display: "flex", gap: 10 }}>
                <StatusButton
                  kind="ontime"
                  label="Board"
                  active={isBoarded}
                  onClick={() => void board(row.id, !isBoarded)}
                />
                <StatusButton
                  kind="over"
                  label="No-show"
                  active={isNoShow}
                  onClick={() => void markNoShow(row.id, !isNoShow)}
                />
              </div>
            </CardRow>
          );
        })
      )}
    </Screen>
  );
}
