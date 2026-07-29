// Client passenger-request CSV → manifest passenger rows.
//
// The client sends a per-passenger request sheet with the columns:
//   Passenger Name, Contractor, Direction, Date, Pickup/Drop-off Location,
//   Contact Email, Contact Phone Number
// e.g.  "Bighetty, Eldrid",Sigfusson,inbound,2026-07-29,"Leaf Rapids, MB",…
//
// This module turns that sheet into editable PaxRow[] for the manifest editors
// (no backend/contract change — it only fills the existing passenger model).
// Names arrive "Last, First" and are normalised to "First Last"; email + phone
// merge into the one contact field; each Location is matched to one of the
// trip's route stops and assigned as the pickup (Outbound) or drop-off (Inbound)
// end per the trip-direction convention (see lib/api/billing.ts).

import type { ManifestDirection } from "@/lib/api/trips";
import { emptyPax, type PaxRow, type StopOption } from "@/components/manifest/manifestRows";

/** One passenger parsed from a CSV row, before stop assignment. */
export interface ParsedPassenger {
  /** Normalised "First Last". */
  name: string;
  /** As it appeared in the sheet (kept so the preview can flag reformatting). */
  rawName: string;
  /** Email + phone merged into the single manifest contact field. */
  contact: string;
  direction: ManifestDirection | null;
  /** Raw date/contractor cells — surfaced for consistency checks, not stored. */
  date: string;
  contractor: string;
  /** Raw location cell. */
  location: string;
  /** Index into the supplied stop options, or null when no stop matched. */
  matchedStopIdx: number | null;
}

export interface ParsedManifestCsv {
  passengers: ParsedPassenger[];
  /** Consensus direction across the rows (null when absent or mixed). */
  direction: ManifestDirection | null;
  directionMixed: boolean;
  /** Distinct non-empty date cells (for a "these differ from the trip" warning). */
  dates: string[];
  /** Distinct non-empty contractor cells. */
  contractors: string[];
  /** Fatal problem — nothing importable (bad format, no data rows). */
  error: string | null;
}

// ---------------------------------------------------------------------------
// Raw CSV → rows of cells (RFC 4180-ish: quoted fields, embedded commas,
// doubled "" escapes, CRLF or LF line endings, leading UTF-8 BOM).
// ---------------------------------------------------------------------------

export function parseCsvRows(text: string): string[][] {
  const src = text.replace(/^﻿/, ""); // strip UTF-8 BOM
  const rows: string[][] = [];
  let row: string[] = [];
  let field = "";
  let inQuotes = false;

  const pushField = () => {
    row.push(field);
    field = "";
  };
  const pushRow = () => {
    pushField();
    rows.push(row);
    row = [];
  };

  for (let i = 0; i < src.length; i++) {
    const ch = src[i];
    if (inQuotes) {
      if (ch === '"') {
        if (src[i + 1] === '"') {
          field += '"';
          i++;
        } else {
          inQuotes = false;
        }
      } else {
        field += ch;
      }
      continue;
    }
    if (ch === '"') {
      inQuotes = true;
    } else if (ch === ",") {
      pushField();
    } else if (ch === "\r") {
      // swallow; the \n (or EOF) closes the row
    } else if (ch === "\n") {
      pushRow();
    } else {
      field += ch;
    }
  }
  // Trailing field/row with no closing newline.
  if (field.length > 0 || row.length > 0) pushRow();

  // Drop rows that are entirely empty (e.g. a trailing blank line).
  return rows.filter((r) => r.some((c) => c.trim() !== ""));
}

// ---------------------------------------------------------------------------
// Column mapping — by header name (order-independent), positional fallback.
// ---------------------------------------------------------------------------

interface ColumnMap {
  name: number;
  contractor: number;
  direction: number;
  date: number;
  location: number;
  email: number;
  phone: number;
}

const POSITIONAL: ColumnMap = {
  name: 0,
  contractor: 1,
  direction: 2,
  date: 3,
  location: 4,
  email: 5,
  phone: 6,
};

/** Recognise the header row and map columns; null when the first row isn't a
 *  header (no "passenger"/"name" cell) so the caller can fall back to positions. */
function mapColumns(header: string[]): ColumnMap | null {
  const norm = header.map((h) => h.toLowerCase().trim());
  const find = (pred: (h: string) => boolean) => norm.findIndex(pred);

  const name = find((h) => h.includes("name"));
  if (name < 0) return null; // not a header row

  const contractor = find((h) => h.includes("contractor"));
  const direction = find((h) => h.includes("direction"));
  const date = find((h) => h.includes("date"));
  const location = find((h) => h.includes("location") || h.includes("pickup") || h.includes("drop"));
  const email = find((h) => h.includes("email"));
  const phone = find((h) => h.includes("phone"));

  return {
    name,
    contractor: contractor >= 0 ? contractor : POSITIONAL.contractor,
    direction: direction >= 0 ? direction : POSITIONAL.direction,
    date: date >= 0 ? date : POSITIONAL.date,
    location: location >= 0 ? location : POSITIONAL.location,
    email: email >= 0 ? email : POSITIONAL.email,
    phone: phone >= 0 ? phone : POSITIONAL.phone,
  };
}

// ---------------------------------------------------------------------------
// Field normalisers
// ---------------------------------------------------------------------------

/** "Bighetty, Eldrid" → "Eldrid Bighetty"; already-natural names pass through. */
export function normalizeName(raw: string): string {
  const v = raw.trim();
  const comma = v.indexOf(",");
  if (comma < 0) return v.replace(/\s+/g, " ");
  const last = v.slice(0, comma).trim();
  const first = v.slice(comma + 1).trim();
  if (!first) return last;
  if (!last) return first;
  return `${first} ${last}`.replace(/\s+/g, " ");
}

function normalizeDirection(raw: string): ManifestDirection | null {
  const v = raw.trim().toLowerCase();
  if (v === "inbound" || v === "in") return "Inbound";
  if (v === "outbound" || v === "out") return "Outbound";
  return null;
}

function mergeContact(email: string, phone: string): string {
  return [email.trim(), phone.trim()].filter(Boolean).join(" · ");
}

/** Loosely compare a CSV location to a stop name — case-insensitive, ignoring a
 *  trailing province suffix (", MB" / ", Manitoba") and collapsed whitespace. */
function normalizeLocation(s: string): string {
  return s
    .toLowerCase()
    .replace(/,?\s*(mb|manitoba)\s*$/i, "")
    .replace(/[.,]/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

/** Best stop-option index for a location, or null. Exact normalised match wins;
 *  otherwise a containment match either direction (handles "Thompson Depot" vs
 *  "Thompson"). */
export function matchStop(location: string, stops: StopOption[]): number | null {
  const loc = normalizeLocation(location);
  if (!loc) return null;
  const normed = stops.map((s) => normalizeLocation(s.name));
  const exact = normed.findIndex((n) => n === loc);
  if (exact >= 0) return exact;
  const contains = normed.findIndex((n) => n && (n.includes(loc) || loc.includes(n)));
  return contains >= 0 ? contains : null;
}

// ---------------------------------------------------------------------------
// Parse
// ---------------------------------------------------------------------------

export function parsePassengerCsv(text: string, stops: StopOption[]): ParsedManifestCsv {
  const empty: ParsedManifestCsv = {
    passengers: [],
    direction: null,
    directionMixed: false,
    dates: [],
    contractors: [],
    error: null,
  };

  const rows = parseCsvRows(text);
  if (rows.length === 0) {
    return { ...empty, error: "The file is empty — no passenger rows found." };
  }

  const cols = mapColumns(rows[0]);
  const dataRows = cols ? rows.slice(1) : rows;
  const map = cols ?? POSITIONAL;

  if (dataRows.length === 0) {
    return { ...empty, error: "No passenger rows found below the header." };
  }

  const at = (r: string[], i: number) => (i >= 0 && i < r.length ? r[i] : "");

  const passengers: ParsedPassenger[] = [];
  for (const r of dataRows) {
    const rawName = at(r, map.name);
    const name = normalizeName(rawName);
    if (!name) continue; // skip nameless rows
    const location = at(r, map.location).trim();
    passengers.push({
      name,
      rawName: rawName.trim(),
      contact: mergeContact(at(r, map.email), at(r, map.phone)),
      direction: normalizeDirection(at(r, map.direction)),
      date: at(r, map.date).trim(),
      contractor: at(r, map.contractor).trim(),
      location,
      matchedStopIdx: matchStop(location, stops),
    });
  }

  if (passengers.length === 0) {
    return { ...empty, error: "No named passengers found in the file." };
  }

  const directions = new Set(passengers.map((p) => p.direction).filter(Boolean) as ManifestDirection[]);
  const directionMixed = directions.size > 1;
  const direction = directions.size === 1 ? [...directions][0] : null;

  const dates = [...new Set(passengers.map((p) => p.date).filter(Boolean))];
  const contractors = [...new Set(passengers.map((p) => p.contractor).filter(Boolean))];

  return { passengers, direction, directionMixed, dates, contractors, error: null };
}

// ---------------------------------------------------------------------------
// Parsed passengers → editable PaxRow[]
// ---------------------------------------------------------------------------

/** Assign the matched stop to the community end and, for a simple two-stop
 *  corridor, the opposite terminal to the other end. Outbound (community →
 *  site): matched stop is the pickup. Inbound (site → community): it's the
 *  drop-off. Unknown direction → treat the matched stop as the pickup. */
export function buildPaxRow(p: ParsedPassenger, stops: StopOption[], direction: ManifestDirection | null): PaxRow {
  const row = emptyPax();
  row.name = p.name;
  row.contact = p.contact;

  const community = p.matchedStopIdx;
  if (community === null) return row;

  const opposite = stops.length === 2 ? (community === 0 ? 1 : 0) : null;
  const communityIdx = String(community);
  const oppositeIdx = opposite === null ? "" : String(opposite);

  if (direction === "Inbound") {
    row.dropoffIdx = communityIdx;
    row.pickupIdx = oppositeIdx;
  } else {
    // Outbound or unknown: community is the origin/pickup.
    row.pickupIdx = communityIdx;
    row.dropoffIdx = oppositeIdx;
  }
  return row;
}

export function buildPaxRows(
  passengers: ParsedPassenger[],
  stops: StopOption[],
  direction: ManifestDirection | null,
): PaxRow[] {
  return passengers.map((p) => buildPaxRow(p, stops, direction));
}
