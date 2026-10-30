// NL-TM-01 trip manifest layout constants.
//
// What remains here is the fixed row geometry of the printed manifest — nothing
// more. The inspection checklist that used to live in this file (PRE_TRIP_GROUPS,
// POST_TRIP_ITEMS, ATTESTATIONS) has moved to `lib/inspectionForm.ts`, which holds
// the owner's real fleet-wide form NL-PTI-01.
//
// The old header comment claimed these constants were "the backend wire contract
// verbatim, copied from ManifestChecklist.cs". That is no longer true:
// `Backend/src/Trips/Domain/Manifests/ManifestChecklist.cs` now holds only the two
// row caps below — the checklist strings moved out of the backend entirely, and the
// inspection wire values are the `key` fields in `lib/inspectionForm.ts`. These two
// numbers still need to agree with the backend's, so treat them as mirrored values
// and change both sides together.
//
// Dependency-free: plain data, no imports.

/** §5 passenger manifest always renders this many rows. */
export const MAX_PASSENGER_ROWS = 8;

/** §6 cargo manifest always renders this many rows. */
export const MAX_CARGO_ROWS = 4;
