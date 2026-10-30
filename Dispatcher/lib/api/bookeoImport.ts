import { ApiError, request } from "./transport";
import type { TripDirection } from "./trips";

// ---------------------------------------------------------------------------
// Bookeo booking-report import — contract owned by Backend/ (Trips module,
// all under /api/trips/imports/bookeo, DispatchAccess). Shapes mirror the
// spec's BookeoImportPreview / BookeoImportCommitResult / BookeoProductMapping
// exactly. Do not invent fields — extend only when the backend contract changes.
//
// The client never parses the spreadsheet: the file goes up as multipart and
// the server returns the plan. Nothing is written before commit.
// The money fields are Bookeo's gross (tax-inclusive), paid and due — the
// platform never computes or displays tax.
// ---------------------------------------------------------------------------

const BASE = "/api/trips/imports/bookeo";

export type ImportSeverity = "Block" | "Warning" | "Info";

export interface ImportIssue {
  code: string;
  severity: ImportSeverity;
  message: string;
}

export type BookeoGroupAction = "Create" | "Update" | "Unchanged" | "Cancel" | "Blocked";
export type BookeoRowAction = "New" | "Changed" | "Cancelled" | "Unchanged" | "Skipped";
export type BookeoVehicleMatch = "Matched" | "Unmatched" | "Ambiguous" | "Blank" | "KeptExisting";
export type ResidentStopRole = "Pickup" | "Dropoff";

export interface BookeoImportSummary {
  rows: number;
  new: number;
  changed: number;
  cancelled: number;
  unchanged: number;
  skipped: number;
  tripsToCreate: number;
  tripsToUpdate: number;
  tripsToCancel: number;
  blockedGroups: number;
  warnings: number;
}

export interface BookeoUnmappedProduct {
  productCode: string;
  productName: string;
  destination: string | null;
  rowCount: number;
}

export interface BookeoUnmatchedUnit {
  unitText: string;
  rowCount: number;
}

export interface BookeoGroupVehicle {
  unitText: string | null;
  vehicleId: string | null;
  vehicleUnit: string | null;
  match: BookeoVehicleMatch;
}

export interface BookeoImportGroup {
  key: string;
  routeId: string | null;
  routeName: string | null;
  /** TripDirection wire name ("Outbound" | "Inbound") or null. */
  direction: TripDirection | null;
  serviceDate: string; // yyyy-MM-dd
  windowStart: string; // HH:mm
  windowEnd: string | null;
  action: BookeoGroupAction;
  existingTripId: string | null;
  existingTripNumber: string | null;
  vehicle: BookeoGroupVehicle;
  driverName: string | null;
  passengersBefore: number;
  passengersAfter: number;
  seatsCapacity: number | null;
  bookingNumbers: string[];
  issues: ImportIssue[];
}

export interface BookeoImportRow {
  bookingNumber: string;
  customerName: string;
  productName: string;
  destination: string | null;
  serviceDate: string | null;
  windowStart: string | null;
  participants: number;
  bookeoStatus: string;
  /** Bookeo "Total gross" — tax-inclusive. */
  totalGrossCad: number;
  totalPaidCad: number;
  totalDueCad: number;
  action: BookeoRowAction;
  changedFields: string[];
  groupKey: string | null;
  issues: ImportIssue[];
}

export interface BookeoImportPreview {
  batchId: string;
  fileName: string;
  uploadedAtUtc: string;
  planHash: string;
  summary: BookeoImportSummary;
  unmappedProducts: BookeoUnmappedProduct[];
  unmatchedUnits: BookeoUnmatchedUnit[];
  groups: BookeoImportGroup[];
  rows: BookeoImportRow[];
}

export interface BookeoImportCommitResult {
  batchId: string;
  tripsCreated: number;
  tripsUpdated: number;
  tripsCancelled: number;
  bookingsImported: number;
  bookingsCancelled: number;
  groupsSkippedBlocked: number;
  createdTripNumbers: string[];
}

export interface BookeoProductMapping {
  id: string;
  productCode: string;
  productName: string;
  destination: string | null;
  routeId: string;
  routeName: string;
  direction: TripDirection | null;
  residentStopRole: ResidentStopRole;
}

export interface BookeoProductMappingInput {
  productCode: string;
  productName: string;
  destination: string | null;
  routeId: string;
  direction: TripDirection | null;
  residentStopRole: ResidentStopRole;
}

export interface BookeoUnitMapping {
  id: string;
  unitText: string;
  vehicleId: string;
  vehicleUnit: string;
}

export interface BookeoUnitMappingInput {
  unitText: string;
  vehicleId: string;
}

export interface BookeoImportBatch {
  batchId: string;
  fileName: string;
  uploadedBy: string;
  uploadedAtUtc: string;
  committedAtUtc: string | null;
  committedBy: string | null;
  /** BookeoImportSummary? on the wire — null when the batch carries no stored
   *  summary. Render "—" rather than reading through it. */
  summary: BookeoImportSummary | null;
}

// ---------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------

/** POST /preview — multipart, field `file`. The transport leaves the
 *  Content-Type unset for FormData so the browser adds the boundary. */
export function previewBookeoImport(file: File): Promise<BookeoImportPreview> {
  const form = new FormData();
  form.append("file", file);
  return request<BookeoImportPreview>(`${BASE}/preview`, { method: "POST", body: form });
}

/** POST /{batchId}/commit — `{ planHash }`. 409 PreviewStale / AlreadyCommitted, 404 not found. */
export function commitBookeoImport(batchId: string, planHash: string): Promise<BookeoImportCommitResult> {
  return request<BookeoImportCommitResult>(`${BASE}/${encodeURIComponent(batchId)}/commit`, {
    method: "POST",
    body: JSON.stringify({ planHash }),
  });
}

export function listBookeoProductMappings(): Promise<BookeoProductMapping[]> {
  return request<BookeoProductMapping[]>(`${BASE}/product-mappings`);
}

/** PUT — upsert by (productCode, destination); returns the full list. */
export function saveBookeoProductMappings(mappings: BookeoProductMappingInput[]): Promise<BookeoProductMapping[]> {
  return request<BookeoProductMapping[]>(`${BASE}/product-mappings`, {
    method: "PUT",
    body: JSON.stringify({ mappings }),
  });
}

export function deleteBookeoProductMapping(id: string): Promise<void> {
  return request<void>(`${BASE}/product-mappings/${encodeURIComponent(id)}`, { method: "DELETE" });
}

export function listBookeoUnitMappings(): Promise<BookeoUnitMapping[]> {
  return request<BookeoUnitMapping[]>(`${BASE}/unit-mappings`);
}

/** PUT — upsert by unitText; returns the full list. */
export function saveBookeoUnitMappings(mappings: BookeoUnitMappingInput[]): Promise<BookeoUnitMapping[]> {
  return request<BookeoUnitMapping[]>(`${BASE}/unit-mappings`, {
    method: "PUT",
    body: JSON.stringify({ mappings }),
  });
}

export function deleteBookeoUnitMapping(id: string): Promise<void> {
  return request<void>(`${BASE}/unit-mappings/${encodeURIComponent(id)}`, { method: "DELETE" });
}

export function listBookeoImportBatches(take = 20): Promise<BookeoImportBatch[]> {
  return request<BookeoImportBatch[]>(`${BASE}/batches?take=${take}`);
}

// ---------------------------------------------------------------------------
// Errors — the backend namespaces its codes ("Trips.BookeoImport.X"); match
// on the last segment so a namespace rename cannot silently drop a message.
// ---------------------------------------------------------------------------

export type BookeoImportErrorKind =
  | "FileRequired"
  | "FileTooLarge"
  | "UnsupportedFile"
  | "HeaderNotRecognized"
  | "PreviewStale"
  | "AlreadyCommitted"
  | "NotFound";

const KNOWN: readonly BookeoImportErrorKind[] = [
  "FileRequired",
  "FileTooLarge",
  "UnsupportedFile",
  "HeaderNotRecognized",
  "PreviewStale",
  "AlreadyCommitted",
];

/** Which Bookeo-import failure this is, or null for anything else. */
export function bookeoErrorKind(e: unknown): BookeoImportErrorKind | null {
  if (!(e instanceof ApiError)) return null;
  const suffix = e.code.slice(e.code.lastIndexOf(".") + 1);
  if ((KNOWN as readonly string[]).includes(suffix)) return suffix as BookeoImportErrorKind;
  // A request body over the server's size limit can surface as a bare 413.
  if (e.status === 413) return "FileTooLarge";
  if (e.status === 404) return "NotFound";
  return null;
}

/** The server's own message when `e` carries the Trips.BookeoImport.<kind>
 *  code itself (not a status-only fallback such as a bare 413), else null. */
function codedServerMessage(e: unknown, kind: BookeoImportErrorKind): string | null {
  if (!(e instanceof ApiError)) return null;
  if (e.code.slice(e.code.lastIndexOf(".") + 1) !== kind) return null;
  const message = e.message.trim();
  return message ? message : null;
}

/** A dispatcher-facing sentence for any failure from this client. */
export function bookeoErrorMessage(e: unknown): string {
  const kind = bookeoErrorKind(e);
  switch (kind) {
    case "FileRequired":
      return "Choose a Bookeo booking report (.xls or .xlsx) first.";
    case "FileTooLarge": {
      // One code, two causes: over 5 MB, or over 2,000 booking rows. The
      // server's message says which (and the row count); a bare 413 from the
      // host's body-size limit has no such message, so it keeps the fixed text.
      const server = codedServerMessage(e, "FileTooLarge");
      return server
        ? `That report is too big to import. ${server}`
        : "That file is larger than 5 MB. Export a shorter date range from Bookeo and try again.";
    }
    case "UnsupportedFile":
      return "That file isn't a readable Excel workbook. Export the Bookeo booking report as .xls or .xlsx and try again.";
    case "HeaderNotRecognized": {
      // The missing column names exist only inside the server's message.
      const server = codedServerMessage(e, "HeaderNotRecognized");
      return server
        ? `Couldn't read the header row. ${server}`
        : "This doesn't look like a Bookeo booking report — the header row is missing columns.";
    }
    case "PreviewStale":
      return "Bookings or trips changed since this preview was made, so nothing was applied.";
    case "AlreadyCommitted":
      return "This import has already been applied — see History.";
    case "NotFound":
      return "That import no longer exists on the server. Upload the file again.";
    default:
      if (e instanceof ApiError) return e.message;
      return "Something went wrong talking to the server — please try again.";
  }
}
