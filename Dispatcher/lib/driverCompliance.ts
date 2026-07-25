import { driverLeaves } from "./data";
import type { DriverLeave } from "./types";

// Pure selectors over the driver-LEAVE mock data (no React). Leave has no
// backend domain (explicitly out of scope), so it stays keyed by the numeric
// mock driverId used in lib/data.ts. Driver records, credentials, clearances,
// and HOS now come from the real Drivers API (lib/api/drivers.ts); trip history
// comes from the real Trips API (GET /api/trips?driverId= — fetched in
// Drivers.tsx, not here).

/** Leave records for a driver, earliest start date first. */
export function leavesFor(driverId: number): DriverLeave[] {
  return driverLeaves
    .filter((l) => l.driverId === driverId)
    .sort((a, b) => a.startDate.localeCompare(b.startDate));
}

/** The leave record (if any) covering a given date — ISO strings sort
 *  lexicographically, so plain string comparison is enough. */
export function leaveOnDate(driverId: number, dateIso: string): DriverLeave | undefined {
  return driverLeaves.find((l) => l.driverId === driverId && l.startDate <= dateIso && dateIso <= l.endDate);
}
