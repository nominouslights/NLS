"use client";

import { useEffect, useState } from "react";
import { colors, statusMeta } from "@/lib/theme";
import { driverCredentialImageBlob } from "@/lib/api/drivers";

// Minimal full-screen image viewer for credential photos. Closes on scrim-click or ✕.
// Fetches the image via the proxied API endpoint (no direct bucket access from browser).

export default function CredentialImageLightbox({
  driverId,
  credentialId,
  onClose,
}: {
  driverId: string;
  credentialId: string;
  onClose: () => void;
}) {
  const [imageUrl, setImageUrl] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const loadImage = async () => {
      try {
        const blob = await driverCredentialImageBlob(driverId, credentialId);
        setImageUrl(URL.createObjectURL(blob));
      } catch (err) {
        setError(err instanceof Error ? err.message : "Failed to load image");
      }
    };
    loadImage();
    return () => {
      if (imageUrl) URL.revokeObjectURL(imageUrl);
    };
  }, [driverId, credentialId, imageUrl]);

  return (
    <div
      style={{
        position: "fixed",
        inset: 0,
        backgroundColor: "rgba(0, 0, 0, 0.8)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        zIndex: 999,
      }}
      onClick={onClose}
    >
      <div
        style={{
          position: "relative",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          maxWidth: "90vw",
          maxHeight: "90vh",
        }}
        onClick={(e) => e.stopPropagation()}
      >
        {error ? (
          <div style={{ color: statusMeta("over").t, fontFamily: "monospace", fontSize: 13 }}>
            {error}
          </div>
        ) : imageUrl ? (
          <img
            src={imageUrl}
            alt="Credential"
            style={{
              maxWidth: "100%",
              maxHeight: "100%",
              objectFit: "contain",
            }}
          />
        ) : (
          <div style={{ color: colors.textMuted }}>Loading…</div>
        )}
        <button
          onClick={onClose}
          style={{
            position: "absolute",
            top: 12,
            right: 12,
            width: 36,
            height: 36,
            borderRadius: "50%",
            background: colors.inputBg,
            border: `1px solid ${colors.borderStrong}`,
            color: colors.textPrimary,
            cursor: "pointer",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            fontSize: 18,
            fontWeight: "bold",
          }}
        >
          ✕
        </button>
      </div>
    </div>
  );
}
