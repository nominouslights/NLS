import type { DriverRecord } from "@/lib/api/drivers";
import type { ComplianceFindingRecord, HosAuditDay, OutOfServiceDeclaration } from "@/lib/api/hos";
import { dayTotals } from "@/components/screens/drivers/hos/hosFixtures";

// Test fixtures for the Compliance screen's tabs, shaped like the Drivers module's responses.

export function driver(overrides: Partial<DriverRecord> = {}): DriverRecord {
  return {
    id: "driver-1",
    name: "K. Beardy",
    phone: null,
    licenceClass: "Class 2",
    licenceExpiry: null,
    source: "Employee",
    hasWorkPermit: false,
    status: "Active",
    credentialCount: 0,
    soonestCredentialExpiry: null,
    latestDutyStatus: null,
    latestDrivingHours: null,
    registeredAtUtc: "2026-01-01T00:00:00Z",
    updatedAtUtc: "2026-01-01T00:00:00Z",
    ...overrides,
  };
}

export function auditDay(overrides: Partial<HosAuditDay> = {}): HosAuditDay {
  return {
    driverId: "driver-1",
    date: "2026-10-09",
    hasRecords: true,
    eventCount: 4,
    sources: ["Driver App"],
    certified: true,
    certifiedAtUtc: "2026-10-10T01:00:00Z",
    recertificationRequired: false,
    totals: dayTotals(),
    personalConveyanceKm: 0,
    violations: [],
    exceptions: [],
    outOfService: null,
    eldMalfunctionOpen: false,
    otherCarrier: [],
    priorRecordsAttested: true,
    findings: [],
    label: "Planning figures",
    ...overrides,
  };
}

export function finding(overrides: Partial<ComplianceFindingRecord> = {}): ComplianceFindingRecord {
  return {
    id: "f-1",
    driverId: "driver-1",
    localDate: "2026-10-09",
    rule: "DailyDriving13h",
    severity: "Major",
    status: "Open",
    source: "Audit",
    description: "Drove 13h40m",
    followUpNote: null,
    openedAtUtc: "2026-10-10T15:00:00Z",
    openedByUserId: "user-co",
    closedAtUtc: null,
    closedByUserId: null,
    ...overrides,
  };
}

export function oosRecord(overrides: Partial<OutOfServiceDeclaration> = {}): OutOfServiceDeclaration {
  return {
    id: "oos-1",
    driverId: "driver-1",
    status: "Active",
    active: true,
    declaredAtUtc: "2026-10-09T11:00:00Z",
    recordedAtUtc: "2026-10-09T11:05:00Z",
    recordedByUserId: "user-co",
    untilUtc: null,
    effectiveUntilUtc: null,
    reason: "Roadside inspection — hours exceeded",
    evidence: null,
    liftedAtUtc: null,
    liftedByUserId: null,
    liftReason: null,
    ...overrides,
  };
}
