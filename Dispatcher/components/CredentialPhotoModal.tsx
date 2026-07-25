"use client";

import { useState } from "react";
import { ApiError } from "@/lib/api";
import { uploadDriverCredentialImage } from "@/lib/api/drivers";
import { ModalShell } from "@/components/ui/ModalShell";
import { ImageUploadField } from "@/components/ui/ImageUploadField";
import { ActionButton } from "@/components/ui/Button";

// Add/replace a credential photo for an existing credential — POST
// /api/drivers/{driverId}/credentials/{credentialId}/image (multipart).
// Owns its own file/busy/error state; on success it hands off to the caller's
// onUploaded (which refetches the eventually-consistent credential list) and
// closes.

export default function CredentialPhotoModal({
  driverId,
  credentialId,
  driverName,
  onClose,
  onUploaded,
}: {
  driverId: string;
  credentialId: string;
  driverName: string;
  onClose: () => void;
  onUploaded: () => void;
}) {
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (!file || busy) return;
    setBusy(true);
    setError(null);
    try {
      await uploadDriverCredentialImage(driverId, credentialId, file);
      onUploaded();
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to upload photo — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Operations · ${driverName} · Licence & certs`}
      title="Add Credential Photo"
      onClose={onClose}
      error={error}
      maxWidth={480}
      footer={
        <>
          <ActionButton onClick={onClose} disabled={busy}>
            CANCEL
          </ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy || !file}>
            {busy ? "UPLOADING…" : "UPLOAD"}
          </ActionButton>
        </>
      }
    >
      <ImageUploadField
        label="Photo"
        value={file}
        onChange={(f) => {
          setFile(f);
          setError(null);
        }}
        hint="Scan or photo of the credential (JPEG, PNG, HEIC only)."
      />
    </ModalShell>
  );
}
