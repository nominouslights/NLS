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
//   - NSC Standard 10 (Cargo Securement) for the fitted cargo area.
//   - MPI Passenger Vehicle for Hire requirements.
// Rows carrying `basis: "NorthernLink"` are company additions — they are NOT part of
// NSC 13 and exist because the owner asked to fold the previous checklist's
// operational items (survival kit, Starlink, spill kit …) into the one form rather
// than lose them.
//
// Dependency-free by design: plain data and two pure functions, no imports, no side
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
//   - The four Area C interior light checks repeat the names of Area B's exterior
//     light checks (they are the same lamps verified from the driver's seat), so every
//     row in "Lights & Signals — Interior" is prefixed "Interior: ". That prefix is
//     applied to all four consistently, never case-by-case.
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

/** Whether the row is checked on both runs, or only at the end of the day. */
export type ItemMode = "Both" | "PostTripOnly";

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
        key: "Engine oil",
        label: "Engine oil",
        checkFor: "Level at/near full mark on dipstick; not milky or burnt-smelling",
        category: "Major",
        categoryNote: "Major if critically low",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Coolant",
        label: "Coolant",
        checkFor: 'At "full cold" mark in reservoir; no rust colour or oily sheen',
        category: "Major",
        categoryNote: "Major if low or leaking",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Power steering fluid (if equipped)",
        label: "Power steering fluid (if equipped)",
        checkFor: "At marked level; no visible leak",
        category: "Minor",
        categoryNote: "Minor / Major if leaking",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Brake fluid reservoir (hydraulic)",
        label: "Brake fluid reservoir (hydraulic)",
        checkFor: 'At or above "MIN" line, cap seated, no fluid around master cylinder',
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Washer fluid",
        label: "Washer fluid",
        checkFor: "Reservoir topped up",
        category: "Minor",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Drive belts",
        label: "Drive belts",
        checkFor: "No cracking, fraying or glazing; proper tension, no squeal on start-up",
        category: "Major",
        categoryNote: "Major if slipping or cracked through",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Hoses (coolant / heater)",
        label: "Hoses (coolant / heater)",
        checkFor: "No cracks, bulges, wet spots or chafing against other parts",
        category: "Major",
        categoryNote: "Major if leaking",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Battery & terminals",
        label: "Battery & terminals",
        checkFor: "Securely mounted; terminals clean and tight; case not swollen or cracked",
        category: "Major",
        categoryNote: "Major if loose or heavily corroded",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Wiring / harness",
        label: "Wiring / harness",
        checkFor: "Secured and insulated; no bare copper visible near heat or moving parts",
        category: "Major",
        categoryNote: "Major if exposed near a hazard",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Fuel / water separator",
        label: "Fuel / water separator (NL-02, diesel)",
        checkFor: "Drained; no water or fuel present at the drain",
        category: "Major",
        categoryNote: "Major if leaking",
        scope: "NL02Only",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Radiator / condenser",
        label: "Radiator / condenser",
        checkFor: "Clear of debris; fins not crushed or blocked",
        category: "Minor",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
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
        mode: "Both",
      },
      {
        key: "Block heater cord (winter)",
        label: "Block heater cord (winter)",
        checkFor: "Cord and plug intact; insulation unbroken, no exposed conductor",
        category: "Minor",
        categoryNote: "Major if conductor exposed",
        scope: "All",
        basis: "NorthernLink",
        mode: "Both",
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
        mode: "Both",
      },
      {
        key: "Shock absorbers",
        label: "Shock absorbers",
        checkFor: "No visible fluid leak; mounts tight",
        category: "Minor",
        categoryNote: "Minor / Major if leaking badly",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "U-bolts & spring hangers",
        label: "U-bolts & spring hangers",
        checkFor: "Tight, not cracked, none missing",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Axles & mounting",
        label: "Axles & mounting",
        checkFor: "Secure; no visible cracks or misalignment",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Wheel bearings",
        label: "Wheel bearings",
        checkFor: "No excess play on a careful hand-check at the wheel",
        category: "Major",
        categoryNote: "Major if loose",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
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
        mode: "Both",
      },
      {
        key: "Tire condition",
        label: "Tire condition",
        checkFor: "No cuts, bulges, exposed cord, or uneven/cupped wear",
        category: "Major",
        categoryNote: "Major if cord exposed",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Tire pressure",
        label: "Tire pressure",
        checkFor: "Matches the vehicle's placard rating",
        category: "Minor",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Wheel nuts / studs",
        label: "Wheel nuts / studs",
        checkFor: "All present; no rust streaks (a sign of loosening); rim not cracked",
        category: "Major",
        categoryNote: "MAJOR if any missing or loose",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Valve stems & caps",
        label: "Valve stems & caps",
        checkFor: "Present and undamaged",
        category: "Minor",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
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
        mode: "Both",
      },
      {
        key: "Body panels & doors (exterior)",
        label: "Body panels & doors (exterior)",
        checkFor: "Secure; latch and lock properly",
        category: "Minor",
        categoryNote: "Minor, unless a door won't secure (Major)",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Windshield & windows",
        label: "Windshield & windows",
        checkFor: "No crack in the driver's direct sightline; wiper arms intact",
        category: "Major",
        categoryNote: "Major if sightline obstructed",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Exterior mirrors (both sides)",
        label: "Exterior mirrors (both sides)",
        checkFor: "Intact, securely mounted, adjustable",
        category: "Major",
        categoryNote: "Major if missing or loose",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Exhaust system",
        label: "Exhaust system",
        checkFor: "Securely mounted; no leaks; tailpipe clear",
        category: "Major",
        categoryNote: "Major if leaking toward the cab/interior",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Fuel tank & cap",
        label: "Fuel tank & cap",
        checkFor: "Secure and sealed; no leaks",
        category: "Major",
        categoryNote: "Major if leaking",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Entry steps / passenger door",
        label: "Entry steps / passenger door (NL-02)",
        checkFor: "Secure, non-slip surface intact, opens and closes fully",
        category: "Major",
        categoryNote: "Major if unsafe to board",
        scope: "NL02Only",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Safety beacon & whip flag",
        label: "Safety beacon & whip flag",
        checkFor: "Beacon mounted and flashing; whip flag upright and undamaged",
        category: "Minor",
        categoryNote: "Major if the day's site requires it and it is inoperative",
        scope: "All",
        basis: "NorthernLink",
        mode: "Both",
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
        mode: "Both",
      },
      {
        key: "Tail lights",
        label: "Tail lights",
        checkFor: "Both sides functioning",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Brake lights (incl. centre high-mount if equipped)",
        label: "Brake lights (incl. centre high-mount if equipped)",
        checkFor: "All functioning — confirm with a helper or by reflection",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Turn signals — front & rear, both sides",
        label: "Turn signals — front & rear, both sides",
        checkFor: "All four functioning at a normal flash rate",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Hazard (4-way) lights",
        label: "Hazard (4-way) lights",
        checkFor: "All four corners flash together",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Marker / clearance lights (roof & side)",
        label: "Marker / clearance lights (roof & side, NL-02)",
        checkFor: "All functioning",
        category: "Minor",
        categoryNote: "Minor / Major depending on which",
        scope: "NL02Only",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Reflectors",
        label: "Reflectors",
        checkFor: "Present and unbroken, both sides and rear",
        category: "Minor",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Back-up lights & reverse alarm (if equipped)",
        label: "Back-up lights & reverse alarm (if equipped)",
        checkFor: "Functioning",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Licence plate light",
        label: "Licence plate light",
        checkFor: "Functioning",
        category: "Minor",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
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
        mode: "Both",
      },
      {
        key: "Steering",
        label: "Steering",
        checkFor: "No excessive free play; no grinding or binding when turned",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Horn",
        label: "Horn",
        checkFor: "Audible and functioning",
        category: "Minor",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Gauges (oil pressure, temperature, volt/ammeter, fuel)",
        label: "Gauges (oil pressure, temperature, volt/ammeter, fuel)",
        checkFor: "Normal readings; warning lights extinguish after the start-up self-test",
        category: "Major",
        categoryNote: "Major if a warning light stays on",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Wipers & washers",
        label: "Wipers & washers",
        checkFor: "Clear the windshield fully; both speeds work",
        category: "Major",
        categoryNote: "Major if inoperative",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Defrost / heater",
        label: "Defrost / heater",
        checkFor: "Functions — critical for northern Manitoba winter operation",
        category: "Major",
        categoryNote: "Major in winter conditions",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Interior mirrors",
        label: "Interior mirrors",
        checkFor: "Adjusted; give the required rear/side visibility",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Doors (from inside)",
        label: "Doors (from inside)",
        checkFor: "Open, close and latch securely",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Emergency exits / windows",
        label: "Emergency exits / windows (NL-02)",
        checkFor: "Clearly marked, release mechanism works freely, path unobstructed",
        category: "Major",
        scope: "NL02Only",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Interior / step lighting",
        label: "Interior / step lighting",
        checkFor: "Functioning",
        category: "Minor",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Interior clean & clear of debris",
        label: "Interior clean & clear of debris",
        checkFor: "Cab and aisle clear; nothing loose that can slide under the pedals",
        category: "Minor",
        categoryNote: "MAJOR if anything can foul the pedals",
        scope: "All",
        basis: "NorthernLink",
        mode: "Both",
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
        checkFor: "Firm; no excessive travel; no fade when held under load",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Parking brake",
        label: "Parking brake",
        checkFor: "Holds the vehicle fully on a grade; releases completely",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Brake warning light",
        label: "Brake warning light",
        checkFor: "Off after the start-up self-test",
        category: "Major",
        categoryNote: "MAJOR if it stays on",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
    ],
  },
  {
    // Every row here is prefixed "Interior: " in its key — see the header comment.
    // These are the same lamps as the Area B exterior rows, verified from the driver's
    // seat, so the labels repeat and only the prefix keeps the wire keys distinct.
    key: "Lights & Signals — Interior",
    title: "Lights & Signals — Interior",
    area: "C",
    items: [
      {
        key: "Interior: Headlights (dash switch, low & high)",
        label: "Headlights (dash switch, low & high)",
        checkFor: "Dash indicator confirms activation",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Interior: Turn signals (left / right)",
        label: "Turn signals (left / right)",
        checkFor: "Dash indicator flashes with audible clicker",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Interior: Hazard lights",
        label: "Hazard lights",
        checkFor: "Activates all four corners",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Interior: Brake lights",
        label: "Brake lights",
        checkFor: "Press the pedal; confirm with a helper or by reflection in a wall/window",
        category: "Major",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
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
        mode: "Both",
      },
      {
        key: "First aid kit",
        label: "First aid kit",
        checkFor: "Present and stocked",
        category: "Minor",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Warning triangles / reflectors (3)",
        label: "Warning triangles / reflectors (3)",
        checkFor: "Present",
        category: "Minor",
        scope: "All",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Passenger seatbelts, all fitted positions",
        label: "Passenger seatbelts, all fitted positions (NL-02)",
        checkFor: "Present and functional where fitted",
        category: "Major",
        scope: "NL02Only",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Spill kit (mine requirement)",
        label: "Spill kit (mine requirement)",
        checkFor: "Present, sealed and complete for the site being entered",
        category: "Major",
        categoryNote: "Site entry is refused without it",
        scope: "All",
        basis: "NorthernLink",
        mode: "Both",
      },
      {
        key: "Survival kit",
        label: "Survival kit",
        checkFor: "Present and stocked for the season — blankets, heat source, rations",
        category: "Major",
        categoryNote: "Major Nov–Apr",
        scope: "All",
        basis: "NorthernLink",
        mode: "Both",
      },
      {
        key: "Traction aids",
        label: "Traction aids",
        checkFor: "Chains, sand or traction mats carried and reachable",
        category: "Minor",
        categoryNote: "Major Nov–Apr",
        scope: "All",
        basis: "NorthernLink",
        mode: "Both",
      },
      {
        key: "Extra fuel",
        label: "Extra fuel",
        checkFor: "Approved can carried, filled and secured",
        category: "Minor",
        categoryNote: "Major if the run has no fuel stop",
        scope: "All",
        basis: "NorthernLink",
        mode: "Both",
      },
      {
        key: "Jumper cables / booster pack",
        label: "Jumper cables / booster pack",
        checkFor: "Present; booster pack holding a charge",
        category: "Minor",
        scope: "All",
        basis: "NorthernLink",
        mode: "Both",
      },
      {
        key: "Reflective safety vest",
        label: "Reflective safety vest",
        checkFor: "Present in the cab and wearable",
        category: "Minor",
        scope: "All",
        basis: "NorthernLink",
        mode: "Both",
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
        scope: "NL02Only",
        basis: "NSC13",
        mode: "Both",
      },
      {
        key: "Fitted cargo area — partition & tie-downs",
        label: "Fitted cargo area — partition & tie-downs (NL-02)",
        checkFor: "Secure; cargo restrained per NSC Standard 10, Cargo Securement",
        category: "Major",
        categoryNote: "Major if a load can shift",
        scope: "NL02Only",
        basis: "NSC13",
        mode: "Both",
      },
    ],
  },
  {
    key: "Communications & Navigation",
    title: "Communications & Navigation",
    area: "C",
    items: [
      {
        key: "Cell phone charged",
        label: "Cell phone charged",
        checkFor: "Charged, and a charger is in the vehicle",
        category: "Minor",
        scope: "All",
        basis: "NorthernLink",
        mode: "Both",
      },
      {
        key: "Starlink / satellite comm connected",
        label: "Starlink / satellite comm connected",
        checkFor: "Terminal powers up and reports a connection",
        category: "Minor",
        categoryNote: "Major if the run leaves cell coverage",
        scope: "All",
        basis: "NorthernLink",
        mode: "Both",
      },
      {
        key: "GPS / navigation",
        label: "GPS / navigation",
        checkFor: "Powers up; the day's route is loaded",
        category: "Minor",
        scope: "All",
        basis: "NorthernLink",
        mode: "Both",
      },
      {
        key: "Emergency contacts list",
        label: "Emergency contacts list",
        checkFor: "Present in the cab and current",
        category: "Minor",
        scope: "All",
        basis: "NorthernLink",
        mode: "Both",
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
 *    Close-Out group cannot be answered before the run.
 *
 * 4. A sub-group whose items were all filtered out is dropped entirely, so no
 *    empty heading is ever printed or rendered (NL-01 pre-trip loses both
 *    "Seating & Cargo" and "Close-Out" this way).
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
