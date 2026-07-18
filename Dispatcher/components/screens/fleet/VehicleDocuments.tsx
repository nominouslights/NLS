"use client";

import { useState, type CSSProperties } from "react";
import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import type { DocumentType } from "@/lib/types";
import { documents, useMaintenanceStore } from "@/lib/maintenanceStore";
import { formatUtcDate } from "@/lib/api";
import { StatusChip, MonoTag } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import DocumentUploadModal from "@/components/DocumentUploadModal";

// Per-vehicle compliance workspace — scan/upload documents and see, at a glance,
// what's valid, expiring, expired, or missing. Mock (no backend document domain).

export default function VehicleDocuments({
  unit,
  requiresPeriodic,
}: {
  unit: string;
  requiresPeriodic: boolean;
}) {
  useMaintenanceStore();
  const [uploadType, setUploadType] = useState<DocumentType | null>(null);
  const [open, setOpen] = useState(false);

  const rows = documents.filter((d) => d.unit === unit);
  const valid = rows.filter((d) => d.k === "ontime").length;
  const expiring = rows.filter((d) => d.k === "soon").length;
  const expired = rows.filter((d) => d.k === "over").length;

  const requiredTypes: DocumentType[] = [
    "Registration",
    "Insurance / MPI",
    ...(requiresPeriodic ? (["NSC Safety Certificate"] as DocumentType[]) : []),
  ];
  const missing = requiredTypes.filter((t) => !rows.some((d) => d.type === t));

  function startUpload(type?: DocumentType) {
    setUploadType(type ?? null);
    setOpen(true);
  }

  return (
    <div>
      {/* compliance summary */}
      <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 14, flexWrap: "wrap" }}>
        <StatusChip kind="ontime" label={`${valid} valid`} />
        <StatusChip kind="soon" label={`${expiring} expiring`} />
        <StatusChip kind="over" label={`${expired} expired`} />
        {missing.length > 0 ? (
          <StatusChip kind="over" label={`${missing.length} required missing`} />
        ) : (
          <MonoTag color={statusMeta("ontime").t}>ALL REQUIRED ON FILE</MonoTag>
        )}
        <ActionButton variant="primary" style={{ marginLeft: "auto" }} onClick={() => startUpload()}>
          + SCAN / UPLOAD DOCUMENT
        </ActionButton>
      </div>

      {/* missing required prompts */}
      {missing.map((t) => (
        <div
          key={t}
          style={{
            display: "flex",
            alignItems: "center",
            gap: 10,
            padding: "10px 13px",
            marginBottom: 6,
            borderRadius: 9,
            background: "rgba(213,94,0,.07)",
            border: "1px solid rgba(213,94,0,.4)",
          }}
        >
          <span style={{ ...badge, background: "#D55E00" }}>▲</span>
          <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: statusMeta("over").t, fontWeight: 600 }}>
            Required document missing — {t}
          </span>
          <ActionButton style={{ marginLeft: "auto" }} onClick={() => startUpload(t)}>
            SCAN {t.toUpperCase()}
          </ActionButton>
        </div>
      ))}

      {rows.length === 0 && missing.length === 0 && (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 2px" }}>
          No documents on file for {unit}.
        </div>
      )}

      {/* document list */}
      {rows.map((d) => (
        <div
          key={d.id}
          style={{
            display: "grid",
            gridTemplateColumns: "180px 1fr 150px 150px",
            gap: 12,
            alignItems: "center",
            padding: "11px 13px",
            marginBottom: 5,
            ...rowSurface(false),
            cursor: "default",
          }}
        >
          <span style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary }}>
            {d.type}
          </span>
          <div style={{ minWidth: 0 }}>
            <div
              style={{
                fontFamily: fonts.mono,
                fontSize: 11.5,
                color: colors.skyBlue,
                whiteSpace: "nowrap",
                overflow: "hidden",
                textOverflow: "ellipsis",
              }}
            >
              {d.fileName}
            </div>
            <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim }}>
              {d.fileSizeKb} KB · uploaded {formatUtcDate(d.uploadedAt)} · {d.uploadedBy}
            </div>
          </div>
          <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
            {d.expiry ? `Expires ${formatUtcDate(d.expiry)}` : "No expiry"}
          </span>
          <StatusChip
            kind={d.k}
            label={d.k === "over" ? "Expired" : d.k === "soon" ? "Expiring soon" : "Valid"}
          />
        </div>
      ))}

      {open && (
        <DocumentUploadModal
          unit={unit}
          defaultType={uploadType ?? undefined}
          onClose={() => setOpen(false)}
          onSaved={() => undefined}
        />
      )}
    </div>
  );
}

const badge: CSSProperties = {
  width: 20,
  height: 20,
  flex: "none",
  borderRadius: 5,
  color: "#fff",
  display: "flex",
  alignItems: "center",
  justifyContent: "center",
  fontSize: 11,
  fontWeight: 800,
};
