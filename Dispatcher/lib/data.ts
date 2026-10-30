// MOCK DATA — only the domains with NO backend yet remain here: communities,
// driver leave (no leave domain), the Fleet/Maintenance preview arrays (fleet,
// DTC alerts, parts, fuel stats), and incidents.
// Trips, drivers (incl. hours of service), clients/contracts/POs, invoices,
// riders, and preventive maintenance all come from the real APIs in lib/api/.
import type {
  DriverLeave,
  DtcAlert,
  FleetVehicle,
  FuelRouteStat,
  Incident,
  PartItem,
} from "./types";

export const communities = [
  "Thompson",
  "Leaf Rapids",
  "Lynn Lake",
  "South Indian Lake",
  "Black Sturgeon Falls",
];

// Trips (and driver trip history) now come from the real Trips API (lib/api/trips.ts).

// Leave records (vacation / sick / unpaid) — MOCK ONLY, keyed by the numeric
// prototype driverId, feeds the Calendar tab alongside the real trip history
// (GET /api/trips?driverId=).
export const driverLeaves: DriverLeave[] = [
  { id: "LV-4101", driverId: 2, type: "Vacation", startDate: "2026-06-29", endDate: "2026-07-03", note: "Summer vacation, booked in advance" },
  { id: "LV-4102", driverId: 4, type: "Sick", startDate: "2026-07-08", endDate: "2026-07-08", hours: 8, note: "Flu — single day" },
  { id: "LV-4103", driverId: 6, type: "Leave Without Pay", startDate: "2026-07-01", endDate: "2026-07-02", note: "Personal — unpaid, approved by dispatch" },
  { id: "LV-4104", driverId: 1, type: "Sick", startDate: "2026-06-25", endDate: "2026-06-25", hours: 8, note: "Same-day sick call" },
];

// The driver roster now comes from the real Drivers API (lib/api/drivers.ts).

// Driver credentials, client clearances and hours of service (the duty-event
// ledger) all come from the real Drivers API (lib/api/drivers.ts, lib/api/hos.ts).

export const fleet: FleetVehicle[] = [
  {
    id: 1,
    unit: "U-02",
    model: "International 3000",
    seats: 24,
    status: "Active",
    sk: "info",
    dvir: "Passed today",
    dk: "ontime",
    insp: "NSC-11 due in 34d",
    ik: "ontime",
    plate: "MB · GHT 402",
    vin: "1HVBBAAN…4471",
    licReq: "Class 4",
    periodic: true,
  },
  {
    id: 2,
    unit: "U-03",
    model: "International 3000",
    seats: 24,
    status: "Active",
    sk: "info",
    dvir: "Passed today",
    dk: "ontime",
    insp: "NSC-11 due in 9d",
    ik: "soon",
    plate: "MB · GHT 418",
    vin: "1HVBBAAN…5522",
    licReq: "Class 4",
    periodic: true,
  },
  {
    id: 3,
    unit: "U-04",
    model: "Ford Transit T-150",
    seats: 7,
    status: "Active",
    sk: "info",
    dvir: "Passed today",
    dk: "ontime",
    insp: "Trip-inspection only",
    ik: "ontime",
    plate: "MB · FRT 771",
    vin: "1FTBW2CM…9013",
    licReq: "Class 4",
    periodic: false,
  },
  {
    id: 4,
    unit: "U-05",
    model: "Ford Transit T-150",
    seats: 7,
    status: "Active",
    sk: "info",
    dvir: "Passed today",
    dk: "ontime",
    insp: "Trip-inspection only",
    ik: "ontime",
    plate: "MB · FRT 802",
    vin: "1FTBW2CM…9188",
    licReq: "Class 4",
    periodic: false,
  },
  {
    id: 5,
    unit: "U-06",
    model: "Ford Transit T-150",
    seats: 7,
    status: "In maintenance",
    sk: "soon",
    dvir: "Passed today",
    dk: "ontime",
    insp: "Brake service",
    ik: "soon",
    plate: "MB · FRT 815",
    vin: "1FTBW2CM…9204",
    licReq: "Class 4",
    periodic: false,
  },
  {
    id: 6,
    unit: "U-01",
    model: "International 3000",
    seats: 24,
    status: "Out of service",
    sk: "over",
    dvir: "DVIR FAILED",
    dk: "over",
    insp: "Steering fault — critical",
    ik: "over",
    plate: "MB · GHT 390",
    vin: "1HVBBAAN…3310",
    licReq: "Class 4",
    periodic: true,
  },
];

// ---------------------------------------------------------------------------
// Maintenance & Asset Management previews — MOCK ONLY (no backend domain yet).
// Keyed by unit number so per-vehicle tabs in Fleet can slice them.
// ---------------------------------------------------------------------------

export const dtcAlerts: DtcAlert[] = [
  { unit: "U-01", code: "C0051", desc: "Steering wheel position sensor fault", severity: "Critical", k: "over", raised: "2d ago" },
  { unit: "U-06", code: "P0301", desc: "Cylinder 1 misfire detected", severity: "Major", k: "over", raised: "5h ago" },
  { unit: "U-05", code: "P0128", desc: "Coolant thermostat below regulating temp", severity: "Minor", k: "soon", raised: "1d ago" },
  { unit: "U-03", code: "P0456", desc: "EVAP system small leak detected", severity: "Minor", k: "soon", raised: "3d ago" },
  { unit: "U-02", code: "P1000", desc: "OBD readiness monitors incomplete", severity: "Info", k: "ontime", raised: "6d ago" },
];

export const partsInventory: PartItem[] = [
  { sku: "FLT-OIL-15W40", name: "Engine oil 15W-40 (20 L)", onHand: 6, min: 4, k: "ontime", loc: "Thompson shop · A2" },
  { sku: "BRK-PAD-INT30", name: "Brake pad set — International 3000", onHand: 1, min: 2, k: "over", loc: "Thompson shop · B1" },
  { sku: "FLT-AIR-T150", name: "Air filter — Transit T-150", onHand: 3, min: 3, k: "soon", loc: "Thompson shop · A4" },
  { sku: "BLB-HDL-H11", name: "Headlamp bulb H11 (pair)", onHand: 8, min: 4, k: "ontime", loc: "Thompson shop · C3" },
  { sku: "WPR-BLD-26", name: "Wiper blade 26\" winter", onHand: 2, min: 6, k: "over", loc: "Thompson shop · C1" },
  { sku: "COOL-ELC-50", name: "ELC coolant 50/50 (10 L)", onHand: 4, min: 3, k: "ontime", loc: "Thompson shop · A3" },
];

export const fuelStats: FuelRouteStat[] = [
  { unit: "U-01", l100: "38.9 L/100km", idlePct: "21%", routeAdherence: "—", k: "over" },
  { unit: "U-02", l100: "36.2 L/100km", idlePct: "11%", routeAdherence: "97%", k: "ontime" },
  { unit: "U-03", l100: "37.0 L/100km", idlePct: "14%", routeAdherence: "95%", k: "ontime" },
  { unit: "U-04", l100: "14.1 L/100km", idlePct: "9%", routeAdherence: "98%", k: "ontime" },
  { unit: "U-05", l100: "16.8 L/100km", idlePct: "19%", routeAdherence: "91%", k: "soon" },
  { unit: "U-06", l100: "15.2 L/100km", idlePct: "12%", routeAdherence: "96%", k: "ontime" },
];

// The client roster now comes from the real Clients API (lib/api/clients.ts),
// and invoices from the real Billing API (lib/api/billing.ts).

export const incidents: Incident[] = [
  {
    id: "INC-118",
    sev: "Critical",
    sk: "over",
    status: "Investigating",
    date: "Jul 6",
    driver: "A. Nepinak",
    vehicle: "U-01",
    trip: "TR-4790",
    summary:
      "Steering fault reported mid-corridor; vehicle taken out of service.",
  },
  {
    id: "INC-117",
    sev: "Moderate",
    sk: "soon",
    status: "Open",
    date: "Jul 5",
    driver: "R. Flett",
    vehicle: "U-05",
    trip: "TR-4772",
    summary: "Minor delay — road advisory PR-391 near Leaf Rapids.",
  },
  {
    id: "INC-115",
    sev: "Low",
    sk: "info",
    status: "Resolved",
    date: "Jul 2",
    driver: "J. Spence",
    vehicle: "U-04",
    trip: "TR-4740",
    summary: "Passenger no-show; documented for billing.",
  },
  {
    id: "FLT-062",
    sev: "Moderate",
    sk: "soon",
    status: "Open",
    date: "Jul 7",
    driver: "—",
    vehicle: "U-06",
    trip: "—",
    summary: "Brake wear flagged on DVIR; scheduled for service.",
  },
];
