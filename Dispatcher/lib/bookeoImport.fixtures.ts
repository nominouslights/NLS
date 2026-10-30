import type {
  BookeoImportGroup,
  BookeoImportPreview,
  BookeoImportRow,
  BookeoImportSummary,
} from "./api/bookeoImport";

// Test-only builders for Bookeo import previews (synthetic people only — the
// owner's real export holds customer PII and never enters the repo).

export function summary(over: Partial<BookeoImportSummary> = {}): BookeoImportSummary {
  return {
    rows: 0,
    new: 0,
    changed: 0,
    cancelled: 0,
    unchanged: 0,
    skipped: 0,
    tripsToCreate: 0,
    tripsToUpdate: 0,
    tripsToCancel: 0,
    blockedGroups: 0,
    warnings: 0,
    ...over,
  };
}

export function group(over: Partial<BookeoImportGroup> = {}): BookeoImportGroup {
  return {
    key: "g1",
    routeId: "route-1",
    routeName: "Lynn Lake – Thompson",
    direction: "Inbound",
    serviceDate: "2026-10-05",
    windowStart: "08:00",
    windowEnd: "12:30",
    action: "Create",
    existingTripId: null,
    existingTripNumber: null,
    vehicle: { unitText: null, vehicleId: null, vehicleUnit: null, match: "Blank" },
    driverName: null,
    passengersBefore: 0,
    passengersAfter: 2,
    seatsCapacity: null,
    bookingNumbers: [],
    issues: [],
    ...over,
  };
}

export function row(over: Partial<BookeoImportRow> = {}): BookeoImportRow {
  return {
    bookingNumber: "0000000000000001",
    customerName: "Test Person",
    productName: "Shuttle from Thompson",
    destination: null,
    serviceDate: "2026-10-05",
    windowStart: "08:00",
    participants: 1,
    bookeoStatus: "normal",
    totalGrossCad: 120,
    totalPaidCad: 120,
    totalDueCad: 0,
    action: "New",
    changedFields: [],
    groupKey: "g1",
    issues: [],
    ...over,
  };
}

export function preview(over: Partial<BookeoImportPreview> = {}): BookeoImportPreview {
  return {
    batchId: "batch-1",
    fileName: "bookeo_sample.xls",
    uploadedAtUtc: "2026-10-02T15:00:00Z",
    planHash: "hash-1",
    summary: summary(),
    unmappedProducts: [],
    unmatchedUnits: [],
    groups: [],
    rows: [],
    ...over,
  };
}
