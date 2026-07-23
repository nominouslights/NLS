"use client";

import { colors, fonts } from "@/lib/theme";
import { credentialKindFor, type DriverCredentialRecord } from "@/lib/api/drivers";
import { StatusChip } from "@/components/ui/Chip";
import { credExpiryLabel, RemoveButton } from "./chips";

// One credential row on the Licence & certs tab — extracted verbatim from
// Drivers.tsx. The thumbnail opens the lightbox when a photo exists, or the
// photo-upload modal when it doesn't.

export function CredentialRow({
  driverId,
  c,
  busy,
  onRemove,
  onViewImage,
  onAddPhoto,
}: {
  driverId: string;
  c: DriverCredentialRecord;
  busy: boolean;
  onRemove: () => void;
  onViewImage: () => void;
  onAddPhoto: () => void;
}) {
  const kind = credentialKindFor(c.expiry);
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        gap: 12,
        padding: "11px 0",
        borderTop: `1px solid ${colors.borderSubtle}`,
      }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: 9, minWidth: 0, flex: 1 }}>
        {c.hasImage ? (
          <div
            onClick={onViewImage}
            style={{
              width: 36,
              height: 36,
              flex: "none",
              borderRadius: 5,
              background: colors.inputBg,
              border: `1px solid ${colors.borderStrong}`,
              cursor: "pointer",
              overflow: "hidden",
            }}
          >
            <img
              src={`/api/drivers/${driverId}/credentials/${c.id}/image`}
              alt="Credential"
              style={{ width: "100%", height: "100%", objectFit: "cover" }}
            />
          </div>
        ) : (
          <div
            onClick={onAddPhoto}
            style={{
              width: 36,
              height: 36,
              flex: "none",
              borderRadius: 5,
              background: colors.inputBg,
              border: `1px dashed ${colors.borderStrong}`,
              cursor: "pointer",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: 16,
              color: colors.textMuted,
            }}
          >
            📷
          </div>
        )}
        <div style={{ minWidth: 0 }}>
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: 7,
              fontFamily: fonts.semiCondensed,
              fontSize: 9.5,
              letterSpacing: ".1em",
              textTransform: "uppercase",
              color: colors.textLabel,
              marginBottom: 3,
            }}
          >
            {c.type}
            {c.optional && (
              <span style={{ color: colors.textFaint, letterSpacing: ".04em" }}>· optional</span>
            )}
          </div>
          <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
            {c.label}
          </div>
          {(c.issued || c.note) && (
            <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginTop: 2 }}>
              {c.issued && <span>Issued {c.issued}</span>}
              {c.issued && c.note && <span style={{ color: colors.textFaint }}> · </span>}
              {c.note && <span>{c.note}</span>}
            </div>
          )}
        </div>
      </div>
      <div style={{ flex: "none", textAlign: "right" }}>
        <StatusChip kind={kind} label={credExpiryLabel(kind, c.expiry)} />
        <div>
          <RemoveButton onClick={onRemove} disabled={busy} />
        </div>
      </div>
    </div>
  );
}
