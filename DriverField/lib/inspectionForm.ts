// COPIED FROM Dispatcher/lib/inspectionForm.ts — keep identical below this header.
// Change Dispatcher first, then re-copy. Drift check: see DriverField/CLAUDE.md.
// Form NL-PTI-01 — "Daily Pre-Trip & Post-Trip Vehicle Inspection".
//
// This is the owner's single fleet-wide inspection form, transcribed as plain data:
// the catalogue of every row that can be printed on the paper form, shown in the
// Dispatch Console, or rendered on the driver's tablet. It replaces the old
// NL-TM-01 pre/post-trip checklist that used to live in `tripManifestChecklist.ts`
// (a separate, shorter list that had forked from the real form).
//
// Regulatory basis:
//   - National Safety Code Standard 13 (Trip Inspection), as implemented in Manitoba
//     by the Commercial Vehicle Trip Inspection Regulation, Man. Reg. 95/2008, under
//     The Highway Traffic Act, C.C.S.M. c. H60.
//   - NSC Standard 13 Schedule 2 (bus trip inspection defect list), adopted in
//     Manitoba as Schedule B of Man. Reg. 95/2008. The pre-trip rows cover its parts
//     that apply to this fleet (accelerator, hubs, air suspension, passenger floor
//     and racks, accessibility lift, mobility restraints, dangerous goods …).
//   - NSC Standard 10 (Cargo Securement) for the fitted cargo area.
//   - MPI Passenger Vehicle for Hire requirements.
// Rows carrying `basis: "NorthernLink"` are company additions — they are NOT part of
// NSC 13 and exist because the owner asked to fold the previous checklist's
// operational items (survival kit, Starlink, spill kit …) into the one form rather
// than lose them.
//
// WHY THE POST-TRIP IS NOT A CHECKLIST (rev 4). NSC 13 and Man. Reg. 95/2008 require
// no full post-trip inspection: the trip inspection is done once per 24 h, before the
// first trip (s.7), and the end-of-day duty is to RECORD defects found en route and
// report them (s.12(4), s.17(2)). Rev 2 re-asked 21 "can change while driving" rows
// plus one "Defects noticed while driving" row; by the owner's decision (2026-10) the
// post-trip now asks only for NEW defects — each filed against the pre-trip row it
// concerns, so work orders and recurrence tracking still match on the item — plus the
// six Close-Out checks. Every vehicle row is therefore "PreTripOnly"; Close-Out is
// "PostTripOnly". The withdrawn en-route row is reserved in `WITHDRAWN_KEYS`.
//
// WHY THE PRE-TRIP IS SHORTER (rev 3). The owner asked for a faster pre-trip with NO
// change to NSC 13 Schedule 2 / Schedule B or SFC coverage. The rule applied: every
// row that maps to a Schedule 2 part stays its own row with its key unchanged; only
// rows that are NOT NSC requirements, plus exact duplicates, were combined.
//   - Engine Bay: Schedule 2 has NO engine-fluid, belt, hose, radiator, battery or
//     wiring part (its only fluid check is hydraulic brake fluid, 18.1 / 18.6M, which
//     stays its own row). Rev 2 had mis-tagged ten such rows `basis: "NSC13"`; they
//     are now three company rows (`basis: "NorthernLink"`). The fresh-leak check
//     ("Ground beneath the vehicle") and the fuel/water separator stay as they were.
//   - Lights & Signals — Interior: removed. Its four rows were the same lamps the
//     Area B exterior walk already verifies — an exact duplicate.
//   - Emergency Equipment: the five company winter/remote items are one "Remote /
//     winter kit" row. Fire extinguisher, first aid kit, warning triangles (NSC
//     Part 10), passenger seatbelts and the spill kit (its own Major site-entry rule)
//     stay separate.
//   - Communications & Navigation: four company rows are one.
// Every removed key is listed in `RETIRED_KEYS` below with the row that replaced it.
//
// Dependency-free by design: plain data and a few pure functions, no imports, no side
// effects. It is imported by the console, the printable form composer and (via copy)
// the Driver Field App, so it must stay safe to pull into any of them.
//
// THE `key` FIELD IS A WIRE VALUE. It is stored verbatim as
// `InspectionChecklistItem.Item` in the backend's jsonb payload and forms half of the
// `(InspectionId, Item)` address a defect is filed against. Two rows sharing a key
// collide onto one defect address and make `Enter` fail with `DuplicateDefectItem`,
// so keys are unique across the WHOLE catalogue, not just within a sub-group.
// Conventions that keep them unique and readable:
//   - The key is the label text where that is already unique.
//   - "(NL-02)" / "(NL-02, diesel)" suffixes are stripped from the key so wire values
//     stay clean — e.g. label "Fuel / water separator (NL-02, diesel)" has the key
//     "Fuel / water separator".
//   - Revs 1 and 2 had four Area C interior light checks that repeated the names of
//     Area B's exterior light checks (the same lamps verified from the driver's seat),
//     so every row in "Lights & Signals — Interior" was prefixed "Interior: ". That
//     group was RETIRED in rev 3 as a duplicate of the exterior walk. The four
//     "Interior: …" keys remain reserved — stored inspections and defects still carry
//     them, so they must never be reused by a new row (see `RETIRED_KEYS`).
//   - A key that leaves the catalogue is never deleted from memory: it moves to
//     `RETIRED_KEYS` (or `WITHDRAWN_KEYS` when no row replaces it), and no current
//     row may ever take it again. Reusing one would
//     silently re-attach old answers and open defects to a different check.
//
// SEVERITY IS DISPLAY TEXT, NEVER COMPUTED. `category` is the form's default and
// `categoryNote` is the form's own qualifier reproduced verbatim ("Major if leaking",
// "Major Nov–Apr", …). Nothing here evaluates a season, a date or a route: the driver
// picks the severity when logging a defect, exactly as the existing "Major if leaking"
// rows already work. No date logic belongs in this file.

/** Area of the walk-around: A = engine bay, B = exterior circuit, C = in-cab. */
export type InspectionArea = "A" | "B" | "C";

/** NSC 13 defect classification. Minor is recorded; Major takes the unit out of service. */
export type ItemCategory = "Minor" | "Major";

/** Which units a row applies to. "NL02Only" rows are absent from NL-01 entirely. */
export type UnitScope = "All" | "NL02Only";

/** Whether the row is an NSC 13 requirement or a Northern Link company addition. */
export type ItemBasis = "NSC13" | "NorthernLink";

/**
 * Which run the row is checked on: the start of the day (the full NSC 13
 * inspection) or the end of the day (Close-Out). Rev 4 retired "Both" — see the
 * header comment.
 */
export type ItemMode = "PreTripOnly" | "PostTripOnly";

/** Which half of the form is being filled in. */
export type InspectionFormMode = "PreTrip" | "PostTrip";

export interface InspectionItem {
  /** Wire value — stored as `InspectionChecklistItem.Item`. Unique across the catalogue. */
  key: string;
  /** Row label as printed on the form. */
  label: string;
  /** The form's "Check For" column. */
  checkFor: string;
  /** The form's default category for this row. */
  category: ItemCategory;
  /** The form's qualifier, verbatim, where it has one. Display text only. */
  categoryNote?: string;
  scope: UnitScope;
  basis: ItemBasis;
  mode: ItemMode;
}

export interface InspectionSubGroup {
  /** Stable id for the sub-group. Same text as `title` on this form. */
  key: string;
  /** Heading printed above the rows. */
  title: string;
  area: InspectionArea;
  items: InspectionItem[];
}

/** The full NL-PTI-01 catalogue, in form order. */
export const NL_PTI_01: InspectionSubGroup[] = [
  // ---------------------------------------------------------------- Area A
  {
    key: "Engine Bay",
    title: "Engine Bay",
    area: "A",
    items: [
      {
        key: "Engine fluid levels",
        label: "Engine fluid levels",
        checkFor:
          "Oil, coolant, power-steering (if equipped) and washer fluid at their marks; no milky oil, rust-coloured coolant or wet spots",
        category: "Major",
        categoryNote: "Major if oil or coolant is critically low or leaking",
        scope: "All",
        basis: "NorthernLink",
        mode: "PreTripOnly",
      },
      {
        key: "Belts, hoses & radiator",
        label: "Belts, hoses & radiator",
        checkFor:
          "Belts not cracked, frayed or glazed and properly tensioned; hoses free of cracks, bulges and leaks; radiator fins clear",
        category: "Major",
        categoryNote: "Major if a belt is slipping or cracked through, or a hose is leaking",
        scope: "All",
        basis: "NorthernLink",
        mode: "PreTripOnly",
      },
      {
        key: "Battery, wiring & block-heater cord",
        label: "Battery, wiring & block-heater cord",
        checkFor:
          "Battery secure, terminals clean and tight, case intact; wiring secured and insulated; block-heater cord and plug intact (winter)",
        category: "Major",
        categoryNote: "Major if loose, heavily corroded, or a conductor is exposed",
        scope: "All",
        basis: "NorthernLink",
        mode: "PreTripOnly",
      },
      {
        key: "Brake fluid reservoir (hydraulic)",
        label: "Brake fluid reservoir (hydraulic)",
        checkFor: 'At or above "MIN" line, cap seated, no fluid around master cylinder',
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Fuel / water separator",
        label: "Fuel / water separator (NL-02, diesel)",
        checkFor: "Drained; no water or fuel present at the drain",
        category: "Major",
        categoryNote: "Major if leaking",
        scope: "NL02Only",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Ground beneath the vehicle",
        label: "Ground beneath the vehicle",
        checkFor:
          "No fresh oil, coolant, fuel, brake fluid or transmission fluid on the ground",
        category: "Major",
        categoryNote: "MAJOR if any fluid is actively dripping",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
    ],
  },

  // ---------------------------------------------------------------- Area B
  {
    key: "Suspension & Undercarriage",
    title: "Suspension & Undercarriage",
    area: "B",
    items: [
      {
        key: "Springs (leaf / coil)",
        label: "Springs (leaf / coil)",
        checkFor: "No cracks, shifted leaves, or missing/loose clamps",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Air suspension (if equipped)",
        label: "Air suspension (if equipped)",
        checkFor: "No audible air leak; air bags intact, inflated and securely mounted",
        category: "Minor",
        categoryNote: "Major if an air bag is damaged or deflated",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Shock absorbers",
        label: "Shock absorbers",
        checkFor: "No visible fluid leak; mounts tight",
        category: "Minor",
        categoryNote: "Minor / Major if leaking badly",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "U-bolts & spring hangers",
        label: "U-bolts & spring hangers",
        checkFor: "Tight, not cracked, none missing",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Axles & mounting",
        label: "Axles & mounting",
        checkFor: "Secure; no visible cracks or misalignment",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Wheel bearings",
        label: "Wheel bearings",
        checkFor: "No excess play on a careful hand-check at the wheel",
        category: "Major",
        categoryNote: "Major if loose",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
    ],
  },
  {
    key: "Tires & Wheels",
    title: "Tires & Wheels",
    area: "B",
    items: [
      {
        key: "Tread depth",
        label: "Tread depth",
        checkFor: "At least 2/32″ (1.6 mm) on all tires — legal minimum; no bald patches",
        category: "Major",
        categoryNote: "Major if below minimum",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Tire condition",
        label: "Tire condition",
        checkFor:
          "No cuts, bulges, exposed cord, flat or audible leak; not touching another tire or the body",
        category: "Major",
        categoryNote: "Major if cord exposed",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Tire pressure",
        label: "Tire pressure",
        checkFor: "Matches the vehicle's placard rating",
        category: "Minor",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Wheel nuts / studs",
        label: "Wheel nuts / studs",
        checkFor: "All present; no rust streaks (a sign of loosening); rim not cracked",
        category: "Major",
        categoryNote: "MAJOR if any missing or loose",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Hubs & wheel seals",
        label: "Hubs & wheel seals",
        checkFor:
          "No oil or grease leaking at the wheel seal; hub oil visible in the sight glass if fitted",
        category: "Minor",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Valve stems & caps",
        label: "Valve stems & caps",
        checkFor: "Present and undamaged",
        category: "Minor",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
    ],
  },
  {
    key: "Frame & Body",
    title: "Frame & Body",
    area: "B",
    items: [
      {
        key: "Frame rails / crossmembers",
        label: "Frame rails / crossmembers",
        checkFor: "No visible cracks or through-rust",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Body panels & doors (exterior)",
        label: "Body panels & doors (exterior)",
        checkFor: "Secure; latch and lock properly",
        category: "Minor",
        categoryNote: "Minor, unless a door won't secure (Major)",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Windshield & windows",
        label: "Windshield & windows",
        checkFor: "No crack in the driver's direct sightline; wiper arms intact",
        category: "Major",
        categoryNote: "Major if sightline obstructed",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Exterior mirrors (both sides)",
        label: "Exterior mirrors (both sides)",
        checkFor: "Intact, securely mounted, adjustable",
        category: "Major",
        categoryNote: "Major if missing or loose",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Exhaust system",
        label: "Exhaust system",
        checkFor: "Securely mounted; no leaks; tailpipe clear",
        category: "Major",
        categoryNote: "Major if leaking toward the cab/interior",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Fuel tank & cap",
        label: "Fuel tank & cap",
        checkFor: "Secure and sealed; no leaks",
        category: "Major",
        categoryNote: "Major if leaking, insecure, or cap missing",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Entry steps / passenger door",
        label: "Entry steps / passenger door (NL-02)",
        checkFor: "Secure, non-slip surface intact, opens and closes fully",
        category: "Major",
        categoryNote: "Major if unsafe to board",
        scope: "NL02Only",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Safety beacon & whip flag",
        label: "Safety beacon & whip flag",
        checkFor: "Beacon mounted and flashing; whip flag upright and undamaged",
        category: "Minor",
        categoryNote: "Major if the day's site requires it and it is inoperative",
        scope: "All",
        basis: "NorthernLink",
        mode: "PreTripOnly",
      },
    ],
  },
  {
    key: "Lights & Signals — Exterior",
    title: "Lights & Signals — Exterior",
    area: "B",
    items: [
      {
        key: "Headlights — low & high beam, both sides",
        label: "Headlights — low & high beam, both sides",
        checkFor: "Both sides functioning at both settings",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Tail lights",
        label: "Tail lights",
        checkFor: "Both sides functioning",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Brake lights (incl. centre high-mount if equipped)",
        label: "Brake lights (incl. centre high-mount if equipped)",
        checkFor: "All functioning — confirm with a helper or by reflection",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Turn signals — front & rear, both sides",
        label: "Turn signals — front & rear, both sides",
        checkFor: "All four functioning at a normal flash rate",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Hazard (4-way) lights",
        label: "Hazard (4-way) lights",
        checkFor: "All four corners flash together",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Marker / clearance lights (roof & side)",
        label: "Marker / clearance lights (roof & side, NL-02)",
        checkFor: "All functioning",
        category: "Minor",
        categoryNote: "Minor / Major depending on which",
        scope: "NL02Only",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Reflectors",
        label: "Reflectors",
        checkFor: "Present and unbroken, both sides and rear",
        category: "Minor",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Back-up lights & reverse alarm (if equipped)",
        label: "Back-up lights & reverse alarm (if equipped)",
        checkFor: "Functioning",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Licence plate light",
        label: "Licence plate light",
        checkFor: "Functioning",
        category: "Minor",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
    ],
  },

  // ---------------------------------------------------------------- Area C
  {
    key: "Controls & Instruments",
    title: "Controls & Instruments",
    area: "C",
    items: [
      {
        key: "Driver's seat & seatbelt",
        label: "Driver's seat & seatbelt",
        checkFor: "Seat adjusts and locks; belt latches securely, no fraying or cuts",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Steering",
        label: "Steering",
        checkFor: "No excessive free play; no grinding or binding when turned",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Accelerator pedal",
        label: "Accelerator pedal",
        checkFor: "Moves freely; engine returns to idle when released",
        category: "Major",
        categoryNote: "Major when carrying passengers",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Horn",
        label: "Horn",
        checkFor: "Audible and functioning",
        category: "Minor",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Gauges (oil pressure, temperature, volt/ammeter, fuel)",
        label: "Gauges (oil pressure, temperature, volt/ammeter, fuel)",
        checkFor:
          "Normal readings; warning lights extinguish after the start-up self-test; turn-signal and high-beam indicators work",
        category: "Major",
        categoryNote: "Major if a warning light stays on",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Wipers & washers",
        label: "Wipers & washers",
        checkFor: "Clear the windshield fully; both speeds work",
        category: "Major",
        categoryNote: "Major if inoperative",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Defrost / heater",
        label: "Defrost / heater",
        checkFor:
          "Clears the windshield; passenger compartment holds at least 10 °C — critical for northern Manitoba winter operation",
        category: "Major",
        categoryNote: "Major if the windshield cannot be cleared",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Interior mirrors",
        label: "Interior mirrors",
        checkFor: "Adjusted; give the required rear/side visibility",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Doors (from inside)",
        label: "Doors (from inside)",
        checkFor: "Open, close and latch securely",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Emergency exits / windows",
        label: "Emergency exits / windows (NL-02)",
        checkFor:
          "Clearly marked, release mechanism works freely, path unobstructed; exit alarm sounds",
        category: "Major",
        scope: "NL02Only",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Interior / step lighting",
        label: "Interior / step lighting",
        checkFor: "Functioning",
        category: "Minor",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Interior clean & clear of debris",
        label: "Interior clean & clear of debris",
        checkFor: "Cab and aisle clear; nothing loose that can slide under the pedals",
        category: "Minor",
        categoryNote: "MAJOR if anything can foul the pedals",
        scope: "All",
        basis: "NorthernLink",
        mode: "PreTripOnly",
      },
    ],
  },
  {
    key: "Brakes — Functional Test",
    title: "Brakes — Functional Test",
    area: "C",
    items: [
      {
        key: "Service brake pedal",
        label: "Service brake pedal",
        checkFor: "Firm; no excessive travel; no fade when held under load; power assist working",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Parking brake",
        label: "Parking brake",
        checkFor: "Holds the vehicle fully on a grade; releases completely",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Brake warning light",
        label: "Brake warning light",
        checkFor: "Off after the start-up self-test",
        category: "Major",
        categoryNote: "MAJOR if it stays on",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
    ],
  },
  {
    key: "Emergency Equipment",
    title: "Emergency Equipment",
    area: "C",
    items: [
      {
        key: "Fire extinguisher (min. 5 lb, BC-rated)",
        label: "Fire extinguisher (min. 5 lb, BC-rated)",
        checkFor: "Present, gauge in the charged/green zone, pin and seal intact",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "First aid kit",
        label: "First aid kit",
        checkFor: "Present and stocked",
        category: "Minor",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Warning triangles / reflectors (3)",
        label: "Warning triangles / reflectors (3)",
        checkFor: "Present",
        category: "Minor",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Passenger seatbelts, all fitted positions",
        label: "Passenger seatbelts, all fitted positions (NL-02)",
        checkFor: "Present and functional where fitted",
        category: "Major",
        scope: "NL02Only",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Spill kit (mine requirement)",
        label: "Spill kit (mine requirement)",
        checkFor: "Present, sealed and complete for the site being entered",
        category: "Major",
        categoryNote: "Site entry is refused without it",
        scope: "All",
        basis: "NorthernLink",
        mode: "PreTripOnly",
      },
      {
        key: "Remote / winter kit",
        label: "Remote / winter kit",
        checkFor:
          "Survival kit stocked for the season (blankets, heat source, rations); traction aids; approved spare fuel can filled and secured; jumper cables or a charged booster pack; reflective vest",
        category: "Minor",
        categoryNote: "Major Nov–Apr, or if the run has no fuel stop",
        scope: "All",
        basis: "NorthernLink",
        mode: "PreTripOnly",
      },
    ],
  },
  {
    key: "Seating & Cargo",
    title: "Seating & Cargo",
    area: "C",
    items: [
      {
        key: "Passenger seats",
        label: "Passenger seats (NL-02)",
        checkFor: "Securely mounted; no damage",
        category: "Major",
        categoryNote: "Major when the seat is occupied",
        scope: "NL02Only",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Fitted cargo area — partition & tie-downs",
        label: "Fitted cargo area — partition & tie-downs (NL-02)",
        checkFor: "Secure; cargo restrained per NSC Standard 10, Cargo Securement",
        category: "Major",
        categoryNote: "Major if a load can shift",
        scope: "NL02Only",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Passenger floor, steps & stanchion padding",
        label: "Passenger floor, steps & stanchion padding (NL-02)",
        checkFor: "Floor and steps undamaged; stanchion padding intact",
        category: "Minor",
        scope: "NL02Only",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Overhead racks / luggage compartments (if equipped)",
        label: "Overhead racks / luggage compartments (if equipped, NL-02)",
        checkFor: "Secure and undamaged; doors latch",
        category: "Minor",
        scope: "NL02Only",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Accessibility lift / ramp & kneeling (if equipped)",
        label: "Accessibility lift / ramp & kneeling (if equipped, NL-02)",
        checkFor:
          "Alarm sounds, interlock works, lift/ramp retracts fully, vehicle returns to ride height after kneeling",
        category: "Major",
        categoryNote: "Device may not be used",
        scope: "NL02Only",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Wheelchair / mobility-device restraints (if equipped)",
        label: "Wheelchair / mobility-device restraints (if equipped, NL-02)",
        checkFor: "Present and functional at every fitted position",
        category: "Minor",
        categoryNote: "Major when that position is occupied",
        scope: "NL02Only",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
      {
        key: "Dangerous goods documents & placards (if carried)",
        label: "Dangerous goods documents & placards (if carried)",
        checkFor:
          "Shipping documents in the cab, placards/labels correct and the load meets TDG requirements",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "PreTripOnly",
      },
    ],
  },
  {
    key: "Communications & Navigation",
    title: "Communications & Navigation",
    area: "C",
    items: [
      {
        key: "Comms & navigation",
        label: "Comms & navigation",
        checkFor:
          "Cell phone charged with a charger aboard; Starlink/satellite terminal connected; GPS on with the day's route loaded; emergency contacts list in the cab and current",
        category: "Minor",
        categoryNote: "Major if the run leaves cell coverage and satellite comms are down",
        scope: "All",
        basis: "NorthernLink",
        mode: "PreTripOnly",
      },
    ],
  },
  {
    // POST-TRIP ONLY. These six labels are carried over from the previous checklist
    // VERBATIM: they are already on the wire as stored `Item` values, so rewording one
    // would orphan every historical defect filed against it.
    key: "Close-Out",
    title: "Close-Out",
    area: "C",
    items: [
      {
        key: "Vehicle exterior — no new damage",
        label: "Vehicle exterior — no new damage",
        checkFor: "Walk the vehicle; compare against this morning's condition",
        category: "Major",
        scope: "All",
        basis: "NorthernLink",
        mode: "PostTripOnly",
      },
      {
        key: "All passengers disembarked safely",
        label: "All passengers disembarked safely",
        checkFor: "Cabin walked end to end; nobody left on board",
        category: "Major",
        scope: "All",
        basis: "NorthernLink",
        mode: "PostTripOnly",
      },
      {
        key: "Vehicle secured / plugged in",
        label: "Vehicle secured / plugged in",
        checkFor: "Locked; block heater plugged in when parked in the cold",
        category: "Minor",
        scope: "All",
        basis: "NorthernLink",
        mode: "PostTripOnly",
      },
      {
        key: "Interior cleaned & checked",
        label: "Interior cleaned & checked",
        checkFor: "Litter removed; lost property collected and logged",
        category: "Minor",
        scope: "All",
        basis: "NorthernLink",
        mode: "PostTripOnly",
      },
      {
        key: "All cargo delivered / accounted for",
        label: "All cargo delivered / accounted for",
        checkFor: "Every waybill item signed for or returned to the depot",
        category: "Major",
        scope: "All",
        basis: "NorthernLink",
        mode: "PostTripOnly",
      },
      {
        key: "Keys returned / secured",
        label: "Keys returned / secured",
        checkFor: "Handed in, or locked in the key box",
        category: "Minor",
        scope: "All",
        basis: "NorthernLink",
        mode: "PostTripOnly",
      },
    ],
  },
];

/**
 * Every key that has left the catalogue, mapped to the CURRENT key of the row that
 * now covers the same check. Rev 3 retired these (see the header comment).
 *
 * THIS IS DISPLAY METADATA, NEVER A WIRE MAPPING. Stored inspections and open
 * defects keep the old string forever — the backend addresses a defect by
 * `(InspectionId, Item)` with `Item` exactly as stored. So:
 *   - a saved record holding any key not in today's catalogue opens read-only and
 *     prints its rows verbatim; it is never rebuilt onto the replacement rows;
 *   - an open defect filed against a retired key is resolved, and re-reported, under
 *     that same old key;
 *   - the replacement may be shown as "now covered by …" text and nothing more.
 * A retired key may never be reused by a current row (pinned by a test).
 */
export const RETIRED_KEYS: ReadonlyMap<string, string> = new Map([
  // Engine Bay — not Schedule 2 parts; folded into three company rows.
  ["Engine oil", "Engine fluid levels"],
  ["Coolant", "Engine fluid levels"],
  ["Power steering fluid (if equipped)", "Engine fluid levels"],
  ["Washer fluid", "Engine fluid levels"],
  ["Drive belts", "Belts, hoses & radiator"],
  ["Hoses (coolant / heater)", "Belts, hoses & radiator"],
  ["Radiator / condenser", "Belts, hoses & radiator"],
  ["Battery & terminals", "Battery, wiring & block-heater cord"],
  ["Wiring / harness", "Battery, wiring & block-heater cord"],
  ["Block heater cord (winter)", "Battery, wiring & block-heater cord"],
  // Lights & Signals — Interior — the same lamps as the exterior walk.
  ["Interior: Headlights (dash switch, low & high)", "Headlights — low & high beam, both sides"],
  ["Interior: Turn signals (left / right)", "Turn signals — front & rear, both sides"],
  ["Interior: Hazard lights", "Hazard (4-way) lights"],
  ["Interior: Brake lights", "Brake lights (incl. centre high-mount if equipped)"],
  // Emergency Equipment — company winter/remote items.
  ["Survival kit", "Remote / winter kit"],
  ["Traction aids", "Remote / winter kit"],
  ["Extra fuel", "Remote / winter kit"],
  ["Jumper cables / booster pack", "Remote / winter kit"],
  ["Reflective safety vest", "Remote / winter kit"],
  // Communications & Navigation — company items.
  ["Cell phone charged", "Comms & navigation"],
  ["Starlink / satellite comm connected", "Comms & navigation"],
  ["GPS / navigation", "Comms & navigation"],
  ["Emergency contacts list", "Comms & navigation"],
]);

/**
 * Keys that left the catalogue with NO current row covering them (rev 4). Reserved
 * exactly like `RETIRED_KEYS` — stored inspections still carry them, so no current
 * row may ever take one — but there is no replacement to name.
 *   - "Defects noticed while driving": the one En-Route Observations row. Rev 4 files
 *     each en-route defect against the item it actually concerns ("Tire condition",
 *     "Parking brake" …) in the post-trip's "New defects" section instead.
 */
export const WITHDRAWN_KEYS: ReadonlySet<string> = new Set(["Defects noticed while driving"]);

/**
 * The current row that covers a retired key's check, for "now covered by …" display
 * text. `null` for a current key or a key this file has never known. Display only —
 * never send the result as a defect's or checklist row's `item`.
 */
export function retiredKeyReplacement(key: string): InspectionItem | null {
  const target = RETIRED_KEYS.get(key);
  if (target == null) return null;
  for (const group of NL_PTI_01) {
    for (const item of group.items) if (item.key === target) return item;
  }
  return null;
}

/** Units this form knows how to narrow for. Anything else gets the full superset. */
const NL_01 = "nl-01";

/**
 * The rows that apply to `unit` on the `mode` half of the form, in form order.
 *
 * Filtering rules — all four are load-bearing:
 *
 * 1. `scope: "NL02Only"` rows are dropped ONLY when `unit` is recognisably NL-01
 *    (trimmed, compared case-insensitively). NL-01 does not have a fitted cargo
 *    area, passenger door, emergency exits or a diesel fuel/water separator, so
 *    those rows would be dead N/A ink on its form.
 *
 * 2. `unit === null`, `""`, or ANY unrecognised unit returns EVERY row, NL02Only
 *    ones included. `TripRecord.vehicleUnit` is `string | null`, so an unassigned
 *    trip genuinely has no unit — and a blank printed form's header reads
 *    `Unit: ☐ NL-01 ☐ NL-02`, where the driver ticks the unit and marks the
 *    inapplicable rows N/A. The fail-safe direction on a compliance form is always
 *    MORE questions: filtering is a convenience for a KNOWN unit, never a silent
 *    narrowing when we do not know what is being inspected. Do not "helpfully"
 *    default an unknown unit to NL-01.
 *
 * 3. `mode: "PostTripOnly"` rows are dropped when `mode` is `"PreTrip"` — the
 *    Close-Out group cannot be answered before the run. `mode: "PreTripOnly"` rows
 *    are dropped when `mode` is `"PostTrip"` — the post-trip asks for new defects
 *    instead (see the header comment), so its checklist is Close-Out alone.
 *
 * 4. A sub-group whose items were all filtered out is dropped entirely, so no
 *    empty heading is ever printed or rendered (pre-trip loses "Close-Out" this
 *    way; post-trip loses every vehicle group).
 *
 * Pure: no imports, no mutation of `NL_PTI_01`, no side effects.
 */
export function itemsFor(
  unit: string | null,
  mode: InspectionFormMode,
): InspectionSubGroup[] {
  const isNl01 = (unit ?? "").trim().toLowerCase() === NL_01;
  const groups: InspectionSubGroup[] = [];

  for (const group of NL_PTI_01) {
    const items = group.items.filter((item) => {
      if (isNl01 && item.scope === "NL02Only") return false;
      if (mode === "PreTrip" && item.mode === "PostTripOnly") return false;
      if (mode === "PostTrip" && item.mode === "PreTripOnly") return false;
      return true;
    });
    if (items.length === 0) continue;
    groups.push({ ...group, items });
  }

  return groups;
}

/**
 * How many checks the driver actually has to answer for this unit and mode.
 * Derived from `itemsFor` so the two can never disagree — the Driver Field App
 * shows this as a progress denominator and must never hardcode a number.
 */
export function checkCount(unit: string | null, mode: InspectionFormMode): number {
  return itemsFor(unit, mode).reduce((total, group) => total + group.items.length, 0);
}

/** §10 driver certification, signed once per completed inspection. Verbatim. */
export const NL_PTI_01_CERTIFICATION =
  "I certify that I have inspected this vehicle in accordance with Northern Link Shuttle & Cargo's Daily Pre-Trip & Post-Trip Vehicle Inspection Process (Form NL-PTI-01) and National Safety Code Standard 13, and that the information above is accurate.";

/** A pre-trip inspection stays valid for this many hours (NSC 13 / Man. Reg. 95/2008). */
export const PRE_TRIP_VALIDITY_HOURS = 24;
