"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import type { DocumentType } from "@/lib/types";
import { addDocument } from "@/lib/maintenanceStore";
import { ModalShell } from "@/components/ui/ModalShell";
import { SelectField, TextField } from "@/components/ui/Field";
import { FileField, type PickedFile } from "@/components/ui/FileField";
import { ActionButton } from "@/components/ui/Button";

const DOC_TYPES: DocumentType[] = [
  "Registration",
  "Insurance / MPI",
  "NSC Safety Certificate",
  "Emissions",
  "Bill of Sale",
  "Warranty",
  "Other",
];

// Scan / upload a compliance document for a vehicle. Prototype: the file is
// captured (name + size) but not uploaded — no backend document domain yet.

export default function DocumentUploadModal({
  unit,
  defaultType,
  onClose,
  onSaved,
}: {
  unit: string;
  defaultType?: DocumentType;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [type, setType] = useState<DocumentType>(defaultType ?? "Registration");
  const [file, setFile] = useState<PickedFile | null>(null);
  const [expiry, setExpiry] = useState("");
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);

  function submit() {
    if (!file) {
      setError("Scan or choose a document file first.");
      return;
    }
    addDocument({
      unit,
      type,
      fileName: file.name,
      fileSizeKb: file.sizeKb,
      uploadedBy: "Dispatch",
      uploadedAt: new Date().toISOString().slice(0, 10),
      expiry: expiry.trim() || null,
      note: note.trim() || undefined,
    });
    onSaved();
    onClose();
  }

  return (
    <ModalShell
      eyebrow={`Fleet & Maintenance · ${unit} · Compliance`}
      title="Scan / Upload Document"
      onClose={onClose}
      error={error}
      maxWidth={560}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit}>
            ADD DOCUMENT
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
        <SelectField
          label="Document type"
          value={type}
          onChange={(v) => setType(v as DocumentType)}
          options={DOC_TYPES.map((t) => ({ value: t, label: t }))}
        />
        <FileField
          label="Document file"
          value={file}
          onChange={setFile}
          hint={<span style={{ color: colors.textFaint }}>· PDF or photo scan · prototype, not stored</span>}
        />
        <TextField
          label="Expiry date"
          value={expiry}
          onChange={setExpiry}
          mono
          placeholder="YYYY-MM-DD"
          hint={<span style={{ color: colors.textFaint }}>· leave blank if the document has no expiry</span>}
        />
        <TextField label="Note (optional)" value={note} onChange={setNote} placeholder="e.g. Renewed after annual inspection" />
      </div>
      <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginTop: 14, lineHeight: 1.5 }}>
        Compliance status is derived from the expiry date — expired documents flag Vermillion,
        those within 60 days flag Gold.
      </div>
    </ModalShell>
  );
}
