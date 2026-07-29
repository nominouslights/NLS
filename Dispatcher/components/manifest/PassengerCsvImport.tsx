"use client";

import { useRef, useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import type { ManifestDirection } from "@/lib/api/trips";
import {
  buildPaxRows,
  parsePassengerCsv,
  type ParsedManifestCsv,
  type ParsedPassenger,
} from "@/lib/manifest/passengerCsv";
import { passengerCapFor, type PaxRow, type StopOption } from "@/components/manifest/manifestRows";
import { ModalShell } from "@/components/ui/ModalShell";
import { ActionButton } from "@/components/ui/Button";

// "Import CSV" affordance for the passenger manifest: reads the client's
// passenger-request sheet, then shows a preview & confirm modal (parsed rows,
// their pickup/drop-off stop mapping, and any warnings) before the editable
// rows are applied. Shared by the Create Trip wizard and the inline manifest
// editor. The row cap tracks the assigned unit's seat capacity.

interface TripContext {
  clientName?: string | null;
  serviceDate?: string | null;
  direction?: ManifestDirection | null;
}

/** A preview warning — always icon + label + colour, never colour alone. */
interface Notice {
  kind: "over" | "soon" | "info";
  text: string;
}

export default function PassengerCsvImport({
  stops,
  capacity,
  existingCount,
  trip,
  onApply,
}: {
  stops: StopOption[];
  /** Assigned unit's seat capacity (null when unknown → falls back to 8). */
  capacity: number | null;
  /** Passenger rows already carrying content (name/contact). */
  existingCount: number;
  trip: TripContext;
  onApply: (rows: PaxRow[], detected: { direction: ManifestDirection | null }) => void;
}) {
  const fileRef = useRef<HTMLInputElement>(null);
  const [parsed, setParsed] = useState<ParsedManifestCsv | null>(null);
  const [fileName, setFileName] = useState("");

  const cap = passengerCapFor(capacity);
  const remaining = Math.max(0, cap - existingCount);

  async function onFile(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = ""; // allow re-selecting the same file
    if (!file) return;
    let text: string;
    try {
      text = await file.text();
    } catch {
      setParsed({
        passengers: [],
        direction: null,
        directionMixed: false,
        dates: [],
        contractors: [],
        error: "Could not read the file.",
      });
      setFileName(file.name);
      return;
    }
    setFileName(file.name);
    setParsed(parsePassengerCsv(text, stops));
  }

  function close() {
    setParsed(null);
    setFileName("");
  }

  function apply() {
    if (!parsed || parsed.error) return;
    const toImport = parsed.passengers.slice(0, remaining);
    const rows = buildPaxRows(toImport, stops, parsed.direction);
    onApply(rows, { direction: parsed.direction });
    close();
  }

  const importCount = parsed ? Math.min(parsed.passengers.length, remaining) : 0;
  const overflow = parsed ? parsed.passengers.length - importCount : 0;

  return (
    <>
      <input
        ref={fileRef}
        type="file"
        accept=".csv,text/csv"
        onChange={onFile}
        style={{ display: "none" }}
      />
      <ActionButton onClick={() => fileRef.current?.click()}>⭱ IMPORT CSV</ActionButton>

      {parsed && (
        <ImportPreviewModal
          parsed={parsed}
          fileName={fileName}
          stops={stops}
          trip={trip}
          cap={cap}
          existingCount={existingCount}
          importCount={importCount}
          overflow={overflow}
          onClose={close}
          onImport={apply}
        />
      )}
    </>
  );
}

// ---------------------------------------------------------------------------
// Preview modal
// ---------------------------------------------------------------------------

function ImportPreviewModal({
  parsed,
  fileName,
  stops,
  trip,
  cap,
  existingCount,
  importCount,
  overflow,
  onClose,
  onImport,
}: {
  parsed: ParsedManifestCsv;
  fileName: string;
  stops: StopOption[];
  trip: TripContext;
  cap: number;
  existingCount: number;
  importCount: number;
  overflow: number;
  onClose: () => void;
  onImport: () => void;
}) {
  const notices = buildNotices({ parsed, stops, trip, cap, existingCount, importCount, overflow });

  return (
    <ModalShell
      eyebrow={`Manifest · Import passengers${fileName ? ` · ${fileName}` : ""}`}
      title="Import passengers from CSV"
      onClose={onClose}
      error={parsed.error}
      maxWidth={860}
      footer={
        <>
          <span style={{ marginRight: "auto", fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
            {parsed.error
              ? "Nothing to import"
              : `${importCount} of ${parsed.passengers.length} passenger${
                  parsed.passengers.length === 1 ? "" : "s"
                }`}
            {parsed.direction ? ` · ${parsed.direction}` : ""}
          </span>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={onImport} disabled={!!parsed.error || importCount === 0}>
            {importCount > 0 ? `IMPORT ${importCount} PASSENGER${importCount === 1 ? "" : "S"}` : "IMPORT"}
          </ActionButton>
        </>
      }
    >
      {!parsed.error && (
        <>
          {notices.length > 0 && (
            <div style={{ display: "flex", flexDirection: "column", gap: 7, marginBottom: 16 }}>
              {notices.map((n, i) => (
                <NoticeRow key={i} notice={n} />
              ))}
            </div>
          )}
          <PreviewTable parsed={parsed} stops={stops} importCount={importCount} />
        </>
      )}
    </ModalShell>
  );
}

function NoticeRow({ notice }: { notice: Notice }) {
  const meta = statusMeta(notice.kind);
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 9,
        padding: "9px 12px",
        background: meta.bg,
        border: `1px solid ${meta.bd}`,
        borderRadius: 9,
      }}
    >
      <span style={{ color: meta.t, fontSize: 12, fontWeight: 800, flex: "none" }}>{meta.g}</span>
      <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: meta.t, fontWeight: 600 }}>{notice.text}</span>
    </div>
  );
}

function PreviewTable({
  parsed,
  stops,
  importCount,
}: {
  parsed: ParsedManifestCsv;
  stops: StopOption[];
  importCount: number;
}) {
  const th: React.CSSProperties = {
    fontFamily: fonts.semiCondensed,
    fontSize: 9.5,
    letterSpacing: ".12em",
    textTransform: "uppercase",
    color: colors.textFaint,
    textAlign: "left",
    padding: "0 8px 6px",
  };
  return (
    <div style={{ overflowX: "auto" }}>
      <table style={{ width: "100%", borderCollapse: "collapse", minWidth: 640 }}>
        <thead>
          <tr>
            <th style={{ ...th, width: 22 }}>#</th>
            <th style={th}>Passenger</th>
            <th style={th}>Contact</th>
            <th style={th}>Location → stop</th>
            <th style={{ ...th, textAlign: "right" }}>Status</th>
          </tr>
        </thead>
        <tbody>
          {parsed.passengers.map((p, i) => (
            <PreviewRow
              key={i}
              index={i}
              p={p}
              stops={stops}
              direction={parsed.direction}
              willImport={i < importCount}
            />
          ))}
        </tbody>
      </table>
    </div>
  );
}

function PreviewRow({
  index,
  p,
  stops,
  direction,
  willImport,
}: {
  index: number;
  p: ParsedPassenger;
  stops: StopOption[];
  direction: ManifestDirection | null;
  willImport: boolean;
}) {
  const matched = p.matchedStopIdx !== null;
  const endLabel = direction === "Inbound" ? "drop-off" : "pickup";
  const stopName = matched ? stops[p.matchedStopIdx as number].name : null;
  const reordered = p.rawName && p.rawName !== p.name;

  const status = !willImport
    ? { kind: "off" as const, text: "Won't fit" }
    : matched
      ? { kind: "ontime" as const, text: "Matched" }
      : { kind: "soon" as const, text: "No stop" };
  const meta = statusMeta(status.kind);

  const cell: React.CSSProperties = {
    fontFamily: fonts.body,
    fontSize: 12.5,
    color: willImport ? colors.textPrimary : colors.textDim,
    padding: "8px 8px",
    borderTop: `1px solid ${colors.borderSubtle}`,
    verticalAlign: "top",
  };

  return (
    <tr style={{ opacity: willImport ? 1 : 0.55 }}>
      <td style={{ ...cell, fontFamily: fonts.mono, color: colors.textDim }}>{index + 1}</td>
      <td style={cell}>
        <div style={{ fontWeight: 500 }}>{p.name}</div>
        {reordered && (
          <div style={{ fontFamily: fonts.mono, fontSize: 10.5, color: colors.textFaint }}>was: {p.rawName}</div>
        )}
      </td>
      <td style={{ ...cell, fontFamily: fonts.mono, fontSize: 11.5, color: willImport ? colors.textSecondary : colors.textDim }}>
        {p.contact || "—"}
      </td>
      <td style={cell}>
        {p.location || "—"}
        {stopName && (
          <div style={{ fontSize: 11.5, color: colors.textMuted }}>
            {statusMeta("info").g} {endLabel}: <span style={{ fontWeight: 600 }}>{stopName}</span>
          </div>
        )}
      </td>
      <td style={{ ...cell, textAlign: "right", whiteSpace: "nowrap" }}>
        <span style={{ color: meta.t, fontWeight: 700, fontSize: 12 }}>
          {meta.g} {status.text}
        </span>
      </td>
    </tr>
  );
}

// ---------------------------------------------------------------------------
// Warnings — consistency of the sheet against the trip + capacity.
// ---------------------------------------------------------------------------

function buildNotices({
  parsed,
  stops,
  trip,
  cap,
  existingCount,
  importCount,
  overflow,
}: {
  parsed: ParsedManifestCsv;
  stops: StopOption[];
  trip: TripContext;
  cap: number;
  existingCount: number;
  importCount: number;
  overflow: number;
}): Notice[] {
  const notices: Notice[] = [];

  if (overflow > 0) {
    notices.push({
      kind: "over",
      text: `${overflow} passenger${overflow === 1 ? "" : "s"} won't fit — the unit seats ${cap}${
        existingCount > 0 ? ` and ${existingCount} ${existingCount === 1 ? "is" : "are"} already on the manifest` : ""
      }. Put the rest on a second trip.`,
    });
  }

  if (stops.length === 0) {
    notices.push({
      kind: "soon",
      text: "No route stops to match against — pick a route first, or set each pickup / drop-off by hand after importing.",
    });
  } else {
    const unmatched = parsed.passengers.slice(0, importCount).filter((p) => p.matchedStopIdx === null).length;
    if (unmatched > 0) {
      notices.push({
        kind: "soon",
        text: `${unmatched} location${unmatched === 1 ? "" : "s"} didn't match a route stop — set the pickup / drop-off for ${
          unmatched === 1 ? "it" : "those"
        } after importing.`,
      });
    }
  }

  if (parsed.directionMixed) {
    notices.push({
      kind: "soon",
      text: "The sheet mixes Inbound and Outbound rows — pickup / drop-off were left for you to set. Split the file by direction for automatic mapping.",
    });
  } else if (!parsed.direction && stops.length > 0) {
    notices.push({
      kind: "info",
      text: "No direction in the sheet — matched locations were set as the pickup. Flip to drop-off if this is a return leg.",
    });
  } else if (parsed.direction && trip.direction && parsed.direction !== trip.direction) {
    notices.push({
      kind: "soon",
      text: `The sheet is ${parsed.direction} but the manifest direction is ${trip.direction}.`,
    });
  }

  if (trip.clientName) {
    const client = trip.clientName.toLowerCase();
    const mismatch = parsed.contractors.filter(
      (c) => !client.includes(c.toLowerCase()) && !c.toLowerCase().includes(client),
    );
    if (mismatch.length > 0) {
      notices.push({
        kind: "info",
        text: `Sheet contractor ${mismatch.map((c) => `"${c}"`).join(", ")} — the trip client is ${trip.clientName}.`,
      });
    }
  } else if (parsed.contractors.length > 0) {
    notices.push({
      kind: "info",
      text: `Sheet contractor: ${parsed.contractors.join(", ")}.`,
    });
  }

  if (trip.serviceDate) {
    const off = parsed.dates.filter((d) => d !== trip.serviceDate);
    if (off.length > 0) {
      notices.push({
        kind: "soon",
        text: `Sheet date${off.length === 1 ? "" : "s"} ${off.join(", ")} differ${
          off.length === 1 ? "s" : ""
        } from the trip's ${trip.serviceDate}.`,
      });
    }
  }

  return notices;
}
