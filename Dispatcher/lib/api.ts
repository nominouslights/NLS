// ---------------------------------------------------------------------------
// Barrel — preserves the original single-file `lib/api` surface after the
// split into lib/api/*. Existing imports from "@/lib/api" keep working
// unchanged; new code may import from the domain modules directly:
//   ./api/transport   — API_BASE, ApiError, request, requestBlob, identity*
//   ./api/format      — formatCad, formatKm, formatUtcDate (cross-domain)
//   ./api/fleet       — vehicles: types, endpoints, display helpers
//   ./api/maintenance — inspections, shops, documents, service records, work orders
//   ./api/trips       — trips/routes/templates AND the trip-manifest contract
// The trip-manifest symbols are re-exported by NAME below (no blanket
// `export * from "./api/trips"` — trips.ts carries additional symbols that
// were never part of lib/api's surface and could collide).
// ---------------------------------------------------------------------------

export * from "./api/transport";
export * from "./api/format";
export * from "./api/fleet";
export * from "./api/maintenance";

export type {
  ManifestSource,
  ManifestDirection,
  ManifestCargoSecured,
  ManifestPassenger,
  ManifestCargo,
  TripManifest,
  TripManifestInput,
  TripActivityEntry,
} from "./api/trips";
export {
  createTripManifest,
  updateTripManifest,
  listTripManifests,
  getTripManifest,
  listTripActivity,
} from "./api/trips";
