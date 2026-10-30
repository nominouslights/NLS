import type { VehicleDefectWire } from "./api/maintenance";

// Shared fixture for the defect/work-order tests (same convention as
// lib/bookeoImport.fixtures.ts).

export function vehicleDefect(over: Partial<VehicleDefectWire> & { item: string }): VehicleDefectWire {
  return {
    inspectionId: "insp-1",
    vehicleId: "veh-1",
    unit: "NL-02",
    inspectionType: "PreTrip",
    tripNumber: "NL-2026-0042",
    driverName: "R. Okimaw",
    reportedAt: "2026-10-01T12:00:00Z",
    severity: "Major",
    note: null,
    workOrderId: null,
    workOrderNumber: null,
    workOrderStatus: null,
    resolutionReason: null,
    resolutionNote: null,
    resolvedBy: null,
    resolvedAtUtc: null,
    resolvedByWorkOrderId: null,
    recurrence: null,
    ...over,
  };
}
