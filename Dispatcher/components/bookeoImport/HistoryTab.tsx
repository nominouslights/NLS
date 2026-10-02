"use client";

import { useEffect, useState } from "react";
import { colors } from "@/lib/theme";
import { bookeoErrorMessage, listBookeoImportBatches, type BookeoImportBatch } from "@/lib/api/bookeoImport";
import { formatUtcDateTime } from "@/lib/bookeoImport";
import { StatusChip } from "@/components/ui/Chip";
import { ModalError } from "@/components/ui/ModalShell";
import { muted, td, th } from "./shared";

// The last 20 uploads. A batch that was only previewed (never confirmed) is
// listed too — every upload is a batch — so "Preview only" is a neutral state,
// not a failure.

export function HistoryTab({ refreshKey }: { refreshKey: number }) {
  const [batches, setBatches] = useState<{ key: number; rows: BookeoImportBatch[] } | null>(null);
  const [error, setError] = useState<{ key: number; message: string } | null>(null);

  useEffect(() => {
    let active = true;
    listBookeoImportBatches(20).then(
      (rows) => {
        if (active) setBatches({ key: refreshKey, rows });
      },
      (e) => {
        if (active) setError({ key: refreshKey, message: bookeoErrorMessage(e) });
      },
    );
    return () => {
      active = false;
    };
  }, [refreshKey]);

  const rows = batches?.key === refreshKey ? batches.rows : null;
  const err = error?.key === refreshKey ? error.message : null;

  if (err) return <ModalError message={err} />;
  if (rows === null) return <div style={muted}>Loading…</div>;
  if (rows.length === 0) return <div style={muted}>No Bookeo reports have been uploaded yet.</div>;

  return (
    <div style={{ overflowX: "auto" }}>
      <table style={{ width: "100%", borderCollapse: "collapse", minWidth: 760 }}>
        <thead>
          <tr>
            <th style={th}>Uploaded</th>
            <th style={th}>File</th>
            <th style={th}>Status</th>
            <th style={th}>Bookings</th>
            <th style={th}>Trips</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((b) => {
            const s = b.summary;
            return (
              <tr key={b.batchId}>
                <td style={{ ...td, whiteSpace: "nowrap" }}>
                  <div>{formatUtcDateTime(b.uploadedAtUtc)}</div>
                  <div style={{ ...muted, fontSize: 11 }}>{b.uploadedBy}</div>
                </td>
                <td style={{ ...td, wordBreak: "break-all" }}>{b.fileName}</td>
                <td style={td}>
                  {b.committedAtUtc ? (
                    <div style={{ display: "flex", flexDirection: "column", gap: 3, alignItems: "flex-start" }}>
                      <StatusChip kind="ontime" label="Applied" />
                      <span style={{ ...muted, fontSize: 11 }}>
                        {formatUtcDateTime(b.committedAtUtc)}
                        {b.committedBy ? ` · ${b.committedBy}` : ""}
                      </span>
                    </div>
                  ) : (
                    <StatusChip kind="off" label="Preview only" />
                  )}
                </td>
                <td style={{ ...td, fontSize: 11.5, color: colors.textSecondary }}>
                  {s
                    ? `${s.rows} rows · ${s.new} new · ${s.changed} changed · ${s.cancelled} cancelled · ${s.unchanged} unchanged`
                    : "—"}
                </td>
                <td style={{ ...td, fontSize: 11.5, color: colors.textSecondary }}>
                  {s
                    ? `${s.tripsToCreate} create · ${s.tripsToUpdate} update · ${s.tripsToCancel} cancel${
                        s.blockedGroups > 0 ? ` · ${s.blockedGroups} blocked` : ""
                      }`
                    : "—"}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
