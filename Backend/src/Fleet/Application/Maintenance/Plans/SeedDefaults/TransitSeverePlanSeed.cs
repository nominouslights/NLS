using NorthernLink.Fleet.Domain.Maintenance;
using static NorthernLink.Fleet.Domain.Maintenance.ComponentTier;
using static NorthernLink.Fleet.Domain.Maintenance.MaintenanceTask;

namespace NorthernLink.Fleet.Application.Maintenance.Plans.SeedDefaults;

/// <summary>
/// The seeded default preventative-maintenance program: "2016 Ford Transit T-150 / severe" —
/// 250 maintenance items and 10 overhauls for unit NL-01, transcribed VERBATIM from the
/// approved plan's Appendix A/B. Severe-service intervals for northern gravel/cold operation
/// (Ford's normal-service intervals are shortened throughout; the notes say where).
/// A km/mo of 0 in the source means "axis not applicable" and becomes a null interval here.
/// Lead overrides are deliberately left null — the plan-wide <see cref="PmSchedule"/>
/// defaults apply. Read directly by
/// <see cref="SeedDefaultMaintenancePlanCommandHandler"/> — plain static data beside its
/// handler, the Budgeting StarterBudgetCodes precedent (it references Domain records only,
/// so the layering holds).
/// </summary>
public static class TransitSeverePlanSeed
{
    public const string PlanName = "2016 Ford Transit T-150 / severe";
    public const string VehicleModel = "2016 Ford Transit T-150";
    public const string ServiceClass = "severe";
    public const string Vin = "1FMZK1YM2GKB16423";
    public const string UnitNumber = "NL-01";

    /// <summary>
    /// The 250 maintenance items. Count check by code prefix (verbatim from the source):
    /// E 32, C 13, F 9, EE 12, ISC 14, TD 17, RAD 9, S 32, B 24, WT 14, EL 22, H 11,
    /// BDG 18, ISP 16, CR 7 = 250.
    /// </summary>
    public static List<MaintenanceItem> BuildItems() =>
    [
        // ── Engine (PM-E, 32) ────────────────────────────────────────────────────────────
        Item("PM-E-001", "Engine", "Engine oil & filter", Primary, Replace, 10000, 6, 45, "Motorcraft 5W-20 synthetic blend; severe-service interval for gravel/cold"),
        Item("PM-E-002", "Engine", "Oil level & condition (between changes)", Primary, Inspect, 5000, 3, 10, "Check for coolant/fuel dilution, metal sheen"),
        Item("PM-E-003", "Engine", "Oil drain plug & gasket", Secondary, Inspect, 10000, 6, 5, "Replace crush washer each change"),
        Item("PM-E-004", "Engine", "Oil filter housing / seal", Secondary, Inspect, 10000, 6, 5),
        Item("PM-E-005", "Engine", "Oil pan & gasket leaks", Primary, Inspect, 10000, 6, 10),
        Item("PM-E-006", "Engine", "Valve cover gaskets", Secondary, Inspect, 20000, 12, 10, "3.7L V6 known weep point"),
        Item("PM-E-007", "Engine", "Timing chain & tensioners (noise/rattle check)", Primary, Inspect, 40000, 24, 15, "Cold-start rattle indicates tensioner wear"),
        Item("PM-E-008", "Engine", "Timing chain guides & VCT phasers", Primary, Inspect, 80000, 48, 30, "Ti-VCT; listen for startup chatter"),
        Item("PM-E-009", "Engine", "Front crankshaft seal", Secondary, Inspect, 40000, 24, 10),
        Item("PM-E-010", "Engine", "Rear main seal", Secondary, Inspect, 40000, 24, 10, "Check bellhousing weep"),
        Item("PM-E-011", "Engine", "Engine mounts (all)", Primary, Inspect, 40000, 24, 15, "Check for separation, excessive movement"),
        Item("PM-E-012", "Engine", "Engine mounts", Primary, Replace, 160000, 96, 120, "Overhaul-adjacent; replace as set"),
        Item("PM-E-013", "Engine", "Accessory drive belt", Primary, Inspect, 20000, 12, 10, "Cracks, glazing, fraying"),
        Item("PM-E-014", "Engine", "Accessory drive belt", Primary, Replace, 100000, 60, 45),
        Item("PM-E-015", "Engine", "Belt tensioner & idler pulleys", Primary, Inspect, 40000, 24, 15, "Bearing noise, tensioner travel"),
        Item("PM-E-016", "Engine", "Belt tensioner & idler pulleys", Primary, Replace, 160000, 96, 60),
        Item("PM-E-017", "Engine", "Harmonic balancer / crank pulley", Secondary, Inspect, 40000, 24, 10, "Wobble, rubber separation"),
        Item("PM-E-018", "Engine", "PCV valve & hoses", Secondary, Inspect, 40000, 24, 10),
        Item("PM-E-019", "Engine", "PCV valve", Secondary, Replace, 100000, 60, 20),
        Item("PM-E-020", "Engine", "Engine air filter", Primary, Inspect, 10000, 6, 5, "Northern gravel dust - check every service"),
        Item("PM-E-021", "Engine", "Engine air filter", Primary, Replace, 30000, 12, 10, "Ford: 48k normal; shortened for gravel"),
        Item("PM-E-022", "Engine", "Air filter housing & seals", Secondary, Inspect, 30000, 12, 5),
        Item("PM-E-023", "Engine", "Intake ducting & MAF sensor", Secondary, Inspect, 40000, 24, 15, "Clean MAF with approved cleaner only"),
        Item("PM-E-024", "Engine", "Throttle body", Secondary, Service, 80000, 48, 30, "Clean; relearn idle"),
        Item("PM-E-025", "Engine", "Intake manifold gaskets / runner control", Secondary, Inspect, 80000, 48, 15),
        Item("PM-E-026", "Engine", "Compression test (all cylinders)", Primary, Test, 100000, 60, 90, "Baseline at 100k, then each 50k"),
        Item("PM-E-027", "Engine", "Cylinder leak-down test", Primary, Test, 160000, 96, 120, "Pre-overhaul decision point"),
        Item("PM-E-028", "Engine", "Engine oil pressure test (gauge)", Primary, Test, 80000, 48, 30, "Compare to spec hot/cold"),
        Item("PM-E-029", "Engine", "Engine oil analysis (lab sample)", Secondary, Test, 40000, 24, 10, "Trend wear metals; flags bearing wear early"),
        Item("PM-E-030", "Engine", "Oil cooler & lines", Secondary, Inspect, 40000, 24, 10),
        Item("PM-E-031", "Engine", "Engine wiring harness & connectors", Secondary, Inspect, 40000, 24, 20, "Rodent damage common in northern storage"),
        Item("PM-E-032", "Engine", "Crankshaft position / cam sensors", Secondary, Inspect, 80000, 48, 10, "Check codes, wiring"),

        // ── Cooling (PM-C, 13) ───────────────────────────────────────────────────────────
        Item("PM-C-001", "Cooling", "Coolant level & concentration (freeze point)", Primary, Inspect, 10000, 6, 10, "Target -45C protection for Leaf Rapids winters"),
        Item("PM-C-002", "Cooling", "Coolant (Motorcraft Orange)", Primary, Replace, 160000, 60, 90, "Ford: 240k/10yr first; shortened for severe service"),
        Item("PM-C-003", "Cooling", "Radiator (core, fins, leaks)", Primary, Inspect, 20000, 12, 10, "Bug/road debris cleaning"),
        Item("PM-C-004", "Cooling", "Radiator hoses (upper/lower/heater)", Primary, Inspect, 20000, 12, 10, "Squeeze test, clamps"),
        Item("PM-C-005", "Cooling", "Radiator & heater hoses", Primary, Replace, 160000, 96, 120),
        Item("PM-C-006", "Cooling", "Thermostat & housing", Primary, Inspect, 40000, 24, 10, "Slow warm-up = failed thermostat"),
        Item("PM-C-007", "Cooling", "Thermostat", Primary, Replace, 160000, 96, 60),
        Item("PM-C-008", "Cooling", "Water pump (weep hole, bearing play)", Primary, Inspect, 40000, 24, 10),
        Item("PM-C-009", "Cooling", "Water pump", Primary, Replace, 160000, 96, 150, "3.7L: external pump"),
        Item("PM-C-010", "Cooling", "Cooling fan (electric) & controller", Primary, Inspect, 20000, 12, 10, "Verify fan engages at temp and with A/C"),
        Item("PM-C-011", "Cooling", "Pressure cap & degas bottle", Primary, Test, 40000, 24, 10, "Pressure-test cap to spec"),
        Item("PM-C-012", "Cooling", "Cooling system pressure test", Primary, Test, 40000, 24, 30),
        Item("PM-C-013", "Cooling", "Coolant temperature sensor", Secondary, Inspect, 80000, 48, 10),

        // ── Fuel (PM-F, 9) ───────────────────────────────────────────────────────────────
        Item("PM-F-001", "Fuel", "Fuel filter (in-tank, integral to pump module)", Primary, Inspect, 80000, 48, 15, "No separate serviceable filter; inspect fuel pressure"),
        Item("PM-F-002", "Fuel", "Fuel pressure test", Primary, Test, 80000, 48, 30, "Compare to spec, check for bleed-down"),
        Item("PM-F-003", "Fuel", "Fuel pump (noise, delivery)", Primary, Inspect, 80000, 48, 15),
        Item("PM-F-004", "Fuel", "Fuel pump module", Primary, Replace, 240000, 144, 240, "Replace at overhaul interval or on pressure failure"),
        Item("PM-F-005", "Fuel", "Fuel injectors (flow/leak, spray)", Primary, Inspect, 100000, 60, 30, "Injector cleaning service at 100k"),
        Item("PM-F-006", "Fuel", "Fuel lines, rails & connections", Primary, Inspect, 20000, 12, 15, "Chafing, corrosion from road salt"),
        Item("PM-F-007", "Fuel", "Fuel tank, straps & shields", Primary, Inspect, 20000, 12, 10, "Strap corrosion is a Transit concern in salt areas"),
        Item("PM-F-008", "Fuel", "Fuel filler neck, cap & hose", Secondary, Inspect, 20000, 12, 5, "EVAP leak source"),
        Item("PM-F-009", "Fuel", "EVAP canister, purge & vent valves", Secondary, Inspect, 80000, 48, 20),

        // ── Exhaust & Emissions (PM-EE, 12) ──────────────────────────────────────────────
        Item("PM-EE-001", "Exhaust & Emissions", "Exhaust manifolds & gaskets", Primary, Inspect, 20000, 12, 10, "Ticking at cold start = leak"),
        Item("PM-EE-002", "Exhaust & Emissions", "Catalytic converters (physical, heat shields)", Primary, Inspect, 20000, 12, 10),
        Item("PM-EE-003", "Exhaust & Emissions", "Catalyst efficiency (monitor readiness / O2 data)", Secondary, Test, 80000, 48, 30, "Scan tool: upstream vs downstream O2"),
        Item("PM-EE-004", "Exhaust & Emissions", "Upstream O2 sensors (2)", Secondary, Replace, 160000, 96, 60),
        Item("PM-EE-005", "Exhaust & Emissions", "Downstream O2 sensors (2)", Secondary, Replace, 160000, 96, 60),
        Item("PM-EE-006", "Exhaust & Emissions", "Exhaust pipes, flex joints & clamps", Primary, Inspect, 20000, 12, 10),
        Item("PM-EE-007", "Exhaust & Emissions", "Muffler & resonator", Secondary, Inspect, 20000, 12, 5, "Rust-through on salted roads"),
        Item("PM-EE-008", "Exhaust & Emissions", "Exhaust hangers & isolators", Secondary, Inspect, 20000, 12, 5),
        Item("PM-EE-009", "Exhaust & Emissions", "Exhaust heat shields", Secondary, Inspect, 20000, 12, 5, "Rattles"),
        Item("PM-EE-010", "Exhaust & Emissions", "Emissions warranty / OBD readiness monitors", Secondary, Test, 10000, 6, 10, "Record readiness status each service"),
        Item("PM-EE-011", "Exhaust & Emissions", "Exhaust leak smoke test", Primary, Test, 80000, 48, 30, "Passenger vehicle: CO intrusion safety"),
        Item("PM-EE-012", "Exhaust & Emissions", "Cabin CO spot check", Primary, Test, 20000, 12, 10, "Handheld CO meter, cabin at idle with heater on"),

        // ── Ignition, Starting & Charging (PM-ISC, 14) ───────────────────────────────────
        Item("PM-ISC-001", "Ignition, Starting & Charging", "Spark plugs", Primary, Inspect, 80000, 48, 30, "Gap, electrode wear, oil fouling"),
        Item("PM-ISC-002", "Ignition, Starting & Charging", "Spark plugs", Primary, Replace, 160000, 96, 90, "3.7L: 160k interval; iridium"),
        Item("PM-ISC-003", "Ignition, Starting & Charging", "Ignition coils (COP)", Secondary, Inspect, 80000, 48, 15, "Boot cracks, misfire history"),
        Item("PM-ISC-004", "Ignition, Starting & Charging", "Ignition coils", Secondary, Replace, 240000, 144, 90, "Replace as set at overhaul"),
        Item("PM-ISC-005", "Ignition, Starting & Charging", "Battery state of health (conductance test)", Primary, Test, 10000, 6, 10, "Critical at -40C; replace below 70% CCA"),
        Item("PM-ISC-006", "Ignition, Starting & Charging", "Battery terminals, cables & hold-down", Primary, Inspect, 10000, 6, 10, "Clean, protect, torque"),
        Item("PM-ISC-007", "Ignition, Starting & Charging", "Battery", Primary, Replace, 0, 48, 30, "Time-based: 4 years max in northern climate"),
        Item("PM-ISC-008", "Ignition, Starting & Charging", "Auxiliary / second battery (if fitted)", Secondary, Test, 10000, 6, 10),
        Item("PM-ISC-009", "Ignition, Starting & Charging", "Alternator output test (load)", Primary, Test, 20000, 12, 15, "Voltage & ripple under load"),
        Item("PM-ISC-010", "Ignition, Starting & Charging", "Alternator", Primary, Replace, 240000, 144, 90),
        Item("PM-ISC-011", "Ignition, Starting & Charging", "Starter motor (draw test)", Primary, Test, 40000, 24, 15),
        Item("PM-ISC-012", "Ignition, Starting & Charging", "Starter motor", Primary, Replace, 240000, 144, 90),
        Item("PM-ISC-013", "Ignition, Starting & Charging", "Charging & starting circuit voltage drop", Secondary, Test, 40000, 24, 20, "Cable & ground resistance"),
        Item("PM-ISC-014", "Ignition, Starting & Charging", "Chassis & engine ground straps", Primary, Inspect, 20000, 12, 10, "Corrosion = intermittent electrical faults"),

        // ── Transmission & Driveline (PM-TD, 17) ─────────────────────────────────────────
        Item("PM-TD-001", "Transmission & Driveline", "Transmission fluid level & condition", Primary, Inspect, 20000, 12, 15, "6R80: check via fill plug at temp; burnt smell/colour"),
        Item("PM-TD-002", "Transmission & Driveline", "Transmission fluid & filter (drain/fill)", Primary, Replace, 60000, 36, 90, "Mercon LV; Ford 240k normal - shortened for severe/towing"),
        Item("PM-TD-003", "Transmission & Driveline", "Transmission pan gasket & leaks", Secondary, Inspect, 20000, 12, 10),
        Item("PM-TD-004", "Transmission & Driveline", "Transmission cooler & lines", Primary, Inspect, 20000, 12, 10),
        Item("PM-TD-005", "Transmission & Driveline", "Transmission cooler lines", Secondary, Replace, 160000, 96, 60),
        Item("PM-TD-006", "Transmission & Driveline", "Transmission mount", Primary, Inspect, 40000, 24, 10),
        Item("PM-TD-007", "Transmission & Driveline", "Shift quality road test (all gears, lockup)", Primary, Test, 20000, 12, 20, "Record flare/slip/harsh shifts"),
        Item("PM-TD-008", "Transmission & Driveline", "Transmission adaptive relearn", Secondary, Service, 60000, 36, 30, "After fluid service"),
        Item("PM-TD-009", "Transmission & Driveline", "Torque converter (shudder test)", Primary, Test, 40000, 24, 15),
        Item("PM-TD-010", "Transmission & Driveline", "Driveshaft U-joints & slip yoke", Primary, Inspect, 20000, 12, 15, "Play, grease, clunk on take-up"),
        Item("PM-TD-011", "Transmission & Driveline", "Driveshaft U-joints", Primary, Replace, 160000, 96, 120),
        Item("PM-TD-012", "Transmission & Driveline", "Driveshaft centre support bearing", Primary, Inspect, 20000, 12, 10, "Two-piece shaft on 130\" WB"),
        Item("PM-TD-013", "Transmission & Driveline", "Driveshaft centre support bearing", Primary, Replace, 160000, 96, 90),
        Item("PM-TD-014", "Transmission & Driveline", "Driveshaft balance & runout", Secondary, Test, 80000, 48, 30),
        Item("PM-TD-015", "Transmission & Driveline", "Driveshaft guard / safety loop", Primary, Inspect, 20000, 12, 5, "Passenger safety"),
        Item("PM-TD-016", "Transmission & Driveline", "Transmission wiring & range sensor", Secondary, Inspect, 40000, 24, 10),
        Item("PM-TD-017", "Transmission & Driveline", "Shifter cable & linkage", Primary, Inspect, 40000, 24, 10, "Column shift cable bushing wear"),

        // ── Rear Axle & Differential (PM-RAD, 9) ─────────────────────────────────────────
        Item("PM-RAD-001", "Rear Axle & Differential", "Rear differential fluid level", Primary, Inspect, 20000, 12, 10, "Check for water contamination"),
        Item("PM-RAD-002", "Rear Axle & Differential", "Rear differential fluid", Primary, Replace, 60000, 36, 60, "75W-140 synthetic; shortened for towing/gravel"),
        Item("PM-RAD-003", "Rear Axle & Differential", "Differential cover gasket / leaks", Secondary, Inspect, 20000, 12, 5),
        Item("PM-RAD-004", "Rear Axle & Differential", "Pinion seal", Primary, Inspect, 20000, 12, 5),
        Item("PM-RAD-005", "Rear Axle & Differential", "Pinion seal", Primary, Replace, 160000, 96, 120),
        Item("PM-RAD-006", "Rear Axle & Differential", "Axle shaft seals & bearings", Primary, Inspect, 40000, 24, 15, "Oil on brake backing plate"),
        Item("PM-RAD-007", "Rear Axle & Differential", "Axle shaft bearings", Primary, Replace, 160000, 96, 180),
        Item("PM-RAD-008", "Rear Axle & Differential", "Ring & pinion backlash / bearing preload", Primary, Test, 160000, 96, 90, "Pre-overhaul measurement"),
        Item("PM-RAD-009", "Rear Axle & Differential", "Axle breather vent", Secondary, Inspect, 20000, 12, 3, "Keep clear; water ingress otherwise"),

        // ── Steering (PM-S-001..012) ─────────────────────────────────────────────────────
        Item("PM-S-001", "Steering", "Power steering fluid level & condition", Primary, Inspect, 10000, 6, 5, "Hydraulic PS on 2016 Transit"),
        Item("PM-S-002", "Steering", "Power steering fluid", Primary, Replace, 80000, 48, 45, "Flush"),
        Item("PM-S-003", "Steering", "Power steering pump & belt", Primary, Inspect, 20000, 12, 10, "Whine, leaks"),
        Item("PM-S-004", "Steering", "Power steering pump", Primary, Replace, 240000, 144, 120),
        Item("PM-S-005", "Steering", "Power steering hoses & cooler", Primary, Inspect, 20000, 12, 10),
        Item("PM-S-006", "Steering", "Steering gear / rack (leaks, play)", Primary, Inspect, 20000, 12, 10),
        Item("PM-S-007", "Steering", "Steering rack", Primary, Replace, 240000, 144, 300),
        Item("PM-S-008", "Steering", "Tie rod ends (inner & outer)", Primary, Inspect, 20000, 12, 15, "Play check on lift"),
        Item("PM-S-009", "Steering", "Tie rod ends", Primary, Replace, 120000, 72, 120),
        Item("PM-S-010", "Steering", "Steering column, intermediate shaft & U-joints", Primary, Inspect, 40000, 24, 10, "Binding, clunk"),
        Item("PM-S-011", "Steering", "Steering wheel free play", Primary, Test, 10000, 6, 5),
        Item("PM-S-012", "Steering", "Front wheel alignment", Primary, Service, 40000, 24, 60, "Also after any suspension part replacement"),

        // ── Suspension (PM-S-013..032) ───────────────────────────────────────────────────
        Item("PM-S-013", "Suspension", "Front struts (leaks, bushings, mounts)", Primary, Inspect, 20000, 12, 15),
        Item("PM-S-014", "Suspension", "Front struts & mounts", Primary, Replace, 120000, 72, 240, "Replace in pairs; align after"),
        Item("PM-S-015", "Suspension", "Front coil springs", Primary, Inspect, 20000, 12, 5, "Cracks, sag, corrosion"),
        Item("PM-S-016", "Suspension", "Front lower control arms & ball joints", Primary, Inspect, 20000, 12, 15),
        Item("PM-S-017", "Suspension", "Lower ball joints", Primary, Replace, 120000, 72, 180),
        Item("PM-S-018", "Suspension", "Control arm bushings", Primary, Replace, 160000, 96, 180),
        Item("PM-S-019", "Suspension", "Front stabilizer bar links", Primary, Inspect, 20000, 12, 10, "Clunk over bumps"),
        Item("PM-S-020", "Suspension", "Front stabilizer bar links", Primary, Replace, 80000, 48, 45),
        Item("PM-S-021", "Suspension", "Front stabilizer bar bushings", Secondary, Inspect, 20000, 12, 5),
        Item("PM-S-022", "Suspension", "Front stabilizer bar bushings", Secondary, Replace, 120000, 72, 60),
        Item("PM-S-023", "Suspension", "Subframe bolts & mounts", Primary, Inspect, 40000, 24, 10),
        Item("PM-S-024", "Suspension", "Rear leaf springs (cracks, sag, centre bolt)", Primary, Inspect, 20000, 12, 10),
        Item("PM-S-025", "Suspension", "Rear leaf spring bushings & shackles", Primary, Inspect, 20000, 12, 10),
        Item("PM-S-026", "Suspension", "Rear leaf spring bushings & shackles", Primary, Replace, 160000, 96, 180),
        Item("PM-S-027", "Suspension", "Rear leaf springs", Primary, Replace, 240000, 144, 240, "Replace in pairs"),
        Item("PM-S-028", "Suspension", "Rear shock absorbers", Primary, Inspect, 20000, 12, 10),
        Item("PM-S-029", "Suspension", "Rear shock absorbers", Primary, Replace, 100000, 60, 90, "Replace in pairs"),
        Item("PM-S-030", "Suspension", "U-bolts & spring plates (torque)", Primary, Inspect, 40000, 24, 15),
        Item("PM-S-031", "Suspension", "Rear stabilizer bar & links (if equipped)", Secondary, Inspect, 20000, 12, 10),
        Item("PM-S-032", "Suspension", "Ride height measurement", Secondary, Test, 40000, 24, 10, "Record all four corners"),

        // ── Brakes (PM-B, 24) ────────────────────────────────────────────────────────────
        Item("PM-B-001", "Brakes", "Front brake pads (thickness)", Primary, Inspect, 10000, 6, 10, "Record mm; replace at 3mm"),
        Item("PM-B-002", "Brakes", "Front brake pads", Primary, Replace, 60000, 36, 90),
        Item("PM-B-003", "Brakes", "Front brake rotors (thickness, runout, scoring)", Primary, Inspect, 10000, 6, 10),
        Item("PM-B-004", "Brakes", "Front brake rotors", Primary, Replace, 120000, 72, 120, "Replace with every second pad set"),
        Item("PM-B-005", "Brakes", "Front brake calipers (slide pins, boots, piston)", Primary, Service, 20000, 12, 45, "Clean & lube slide pins - critical in salt"),
        Item("PM-B-006", "Brakes", "Front brake calipers", Primary, Replace, 160000, 96, 120),
        Item("PM-B-007", "Brakes", "Rear brake pads (thickness)", Primary, Inspect, 10000, 6, 10),
        Item("PM-B-008", "Brakes", "Rear brake pads", Primary, Replace, 60000, 36, 90),
        Item("PM-B-009", "Brakes", "Rear brake rotors", Primary, Inspect, 10000, 6, 10),
        Item("PM-B-010", "Brakes", "Rear brake rotors", Primary, Replace, 120000, 72, 120),
        Item("PM-B-011", "Brakes", "Rear brake calipers (slide pins, boots)", Primary, Service, 20000, 12, 45),
        Item("PM-B-012", "Brakes", "Rear brake calipers", Primary, Replace, 160000, 96, 120),
        Item("PM-B-013", "Brakes", "Brake fluid (moisture/boiling point test)", Primary, Test, 10000, 6, 5, "Test strip; replace >3% moisture"),
        Item("PM-B-014", "Brakes", "Brake fluid", Primary, Replace, 0, 36, 60, "Time-based: 3 years (DOT 4 LV)"),
        Item("PM-B-015", "Brakes", "Brake lines (steel, corrosion)", Primary, Inspect, 10000, 6, 15, "Salt corrosion - most common safety fail"),
        Item("PM-B-016", "Brakes", "Brake hoses (flex)", Primary, Inspect, 10000, 6, 10, "Cracks, bulges"),
        Item("PM-B-017", "Brakes", "Brake hoses (flex)", Primary, Replace, 160000, 96, 90),
        Item("PM-B-018", "Brakes", "Master cylinder & booster (leak, pedal sink)", Primary, Test, 20000, 12, 10),
        Item("PM-B-019", "Brakes", "Brake booster vacuum check", Primary, Test, 20000, 12, 10),
        Item("PM-B-020", "Brakes", "ABS module, sensors & tone rings", Primary, Inspect, 20000, 12, 15, "Scan codes, check sensor gaps"),
        Item("PM-B-021", "Brakes", "Parking brake (adjustment, cables, shoes)", Primary, Service, 20000, 12, 20, "Cable freeze-up risk; lube"),
        Item("PM-B-022", "Brakes", "Parking brake shoes", Primary, Replace, 120000, 72, 90),
        Item("PM-B-023", "Brakes", "Brake pedal travel & reserve", Primary, Test, 10000, 6, 5),
        Item("PM-B-024", "Brakes", "Brake performance road test / decel", Primary, Test, 20000, 12, 15, "Pull, pulsation, noise"),

        // ── Wheels & Tires (PM-WT, 14) ───────────────────────────────────────────────────
        Item("PM-WT-001", "Wheels & Tires", "Tire pressure (cold) & valve stems", Primary, Inspect, 5000, 3, 10, "Set to door placard; check TPMS"),
        Item("PM-WT-002", "Wheels & Tires", "Tire tread depth (all positions)", Primary, Inspect, 10000, 6, 10, "Record mm; winter min 4mm"),
        Item("PM-WT-003", "Wheels & Tires", "Tire condition (cuts, sidewall, cupping)", Primary, Inspect, 10000, 6, 10, "Gravel road damage"),
        Item("PM-WT-004", "Wheels & Tires", "Tire rotation", Primary, Service, 10000, 6, 30),
        Item("PM-WT-005", "Wheels & Tires", "Tires (set of 4)", Primary, Replace, 60000, 36, 120, "Or at 4/32 in winter service; LT-rated only"),
        Item("PM-WT-006", "Wheels & Tires", "Wheel balance", Secondary, Service, 20000, 12, 30),
        Item("PM-WT-007", "Wheels & Tires", "Wheel nuts torque (re-torque after service)", Primary, Service, 10000, 6, 10, "Re-torque 100 km after any wheel removal"),
        Item("PM-WT-008", "Wheels & Tires", "Wheel studs & nuts condition", Primary, Inspect, 10000, 6, 5),
        Item("PM-WT-009", "Wheels & Tires", "Wheel rims (cracks, bends, corrosion)", Primary, Inspect, 20000, 12, 10),
        Item("PM-WT-010", "Wheels & Tires", "Wheel bearings / hubs (play, noise)", Primary, Inspect, 20000, 12, 15),
        Item("PM-WT-011", "Wheels & Tires", "Front hub bearing assemblies", Primary, Replace, 160000, 96, 180),
        Item("PM-WT-012", "Wheels & Tires", "TPMS sensors & battery", Secondary, Test, 20000, 12, 10, "Sensor batteries ~7-10 yr"),
        Item("PM-WT-013", "Wheels & Tires", "Spare tire (pressure, condition, winch)", Primary, Inspect, 10000, 6, 10, "Underbody winch cable corrosion"),
        Item("PM-WT-014", "Wheels & Tires", "Winter/summer tire changeover", Primary, Service, 0, 6, 60, "Seasonal: Oct & Apr"),

        // ── Electrical & Lighting (PM-EL, 22) ────────────────────────────────────────────
        Item("PM-EL-001", "Electrical & Lighting", "Headlights (low/high, aim)", Primary, Test, 10000, 6, 10),
        Item("PM-EL-002", "Electrical & Lighting", "Headlight aim adjustment", Primary, Service, 40000, 24, 20),
        Item("PM-EL-003", "Electrical & Lighting", "Daytime running lights", Primary, Test, 10000, 6, 3),
        Item("PM-EL-004", "Electrical & Lighting", "Turn signals & hazards (all)", Primary, Test, 10000, 6, 5),
        Item("PM-EL-005", "Electrical & Lighting", "Brake lights & CHMSL", Primary, Test, 10000, 6, 5),
        Item("PM-EL-006", "Electrical & Lighting", "Tail, marker, clearance & licence lamps", Primary, Test, 10000, 6, 5),
        Item("PM-EL-007", "Electrical & Lighting", "Reverse lights & backup camera", Secondary, Test, 10000, 6, 5),
        Item("PM-EL-008", "Electrical & Lighting", "Instrument cluster warning lamps (bulb check)", Primary, Test, 10000, 6, 5, "All lamps illuminate at key-on"),
        Item("PM-EL-009", "Electrical & Lighting", "Horn", Primary, Test, 10000, 6, 2),
        Item("PM-EL-010", "Electrical & Lighting", "Wiper motor, linkage & blades", Primary, Inspect, 10000, 6, 10),
        Item("PM-EL-011", "Electrical & Lighting", "Wiper blades", Primary, Replace, 0, 6, 10, "Twice yearly; winter blades Oct"),
        Item("PM-EL-012", "Electrical & Lighting", "Washer pump, nozzles & fluid", Primary, Inspect, 10000, 6, 5, "-45C fluid in winter"),
        Item("PM-EL-013", "Electrical & Lighting", "Fuses, relays & junction boxes", Secondary, Inspect, 20000, 12, 10, "Corrosion, water intrusion"),
        Item("PM-EL-014", "Electrical & Lighting", "Body/chassis wiring harness & grommets", Secondary, Inspect, 40000, 24, 20),
        Item("PM-EL-015", "Electrical & Lighting", "Trailer wiring connector & module", Secondary, Test, 20000, 12, 10),
        Item("PM-EL-016", "Electrical & Lighting", "Power window, lock & mirror motors", Secondary, Test, 20000, 12, 10),
        Item("PM-EL-017", "Electrical & Lighting", "Diagnostic scan - all modules (DTCs)", Primary, Test, 10000, 6, 15, "Record & clear with notes"),
        Item("PM-EL-018", "Electrical & Lighting", "PCM/BCM software updates (TSB check)", Secondary, Service, 40000, 24, 30, "Check Ford TSBs/recalls each visit"),
        Item("PM-EL-019", "Electrical & Lighting", "Key fobs & batteries", Secondary, Replace, 0, 24, 5),
        Item("PM-EL-020", "Electrical & Lighting", "Block heater cord, plug & element", Primary, Test, 0, 6, 10, "Test resistance before first freeze (Oct)"),
        Item("PM-EL-021", "Electrical & Lighting", "Block heater element", Primary, Replace, 0, 60, 60, "Recall history on Transit block heaters - verify"),
        Item("PM-EL-022", "Electrical & Lighting", "Battery blanket / oil pan heater (if fitted)", Secondary, Test, 0, 12, 10),

        // ── HVAC (PM-H, 11) ──────────────────────────────────────────────────────────────
        Item("PM-H-001", "HVAC", "Cabin air filter", Secondary, Replace, 20000, 12, 10, "Dust from gravel roads"),
        Item("PM-H-002", "HVAC", "Heater output & blend door operation", Primary, Test, 10000, 6, 10, "Passenger comfort at -40C"),
        Item("PM-H-003", "HVAC", "Rear heater / auxiliary HVAC unit", Primary, Test, 10000, 6, 10, "Passenger van: rear heat critical"),
        Item("PM-H-004", "HVAC", "Heater core (leaks, smell, fogging)", Primary, Inspect, 20000, 12, 5),
        Item("PM-H-005", "HVAC", "Blower motor & resistor (all speeds)", Secondary, Test, 10000, 6, 5),
        Item("PM-H-006", "HVAC", "A/C performance (vent temp, pressures)", Secondary, Test, 20000, 12, 20, "Spring"),
        Item("PM-H-007", "HVAC", "A/C compressor & clutch", Secondary, Inspect, 20000, 12, 10),
        Item("PM-H-008", "HVAC", "A/C condenser & lines", Secondary, Inspect, 20000, 12, 10),
        Item("PM-H-009", "HVAC", "A/C refrigerant & oil (evacuate/recharge)", Secondary, Service, 80000, 48, 90),
        Item("PM-H-010", "HVAC", "Defroster & defog performance", Primary, Test, 0, 12, 10, "Pre-winter"),
        Item("PM-H-011", "HVAC", "Drain tubes (evaporator, cowl)", Secondary, Service, 20000, 12, 10, "Leaf debris clogs"),

        // ── Body, Doors & Glass (PM-BDG, 18) ─────────────────────────────────────────────
        Item("PM-BDG-001", "Body, Doors & Glass", "Frame / unibody rails (rust, cracks)", Primary, Inspect, 20000, 12, 20, "Salt corrosion"),
        Item("PM-BDG-002", "Body, Doors & Glass", "Underbody rust-proofing (oil spray)", Primary, Service, 0, 12, 90, "Annual, September"),
        Item("PM-BDG-003", "Body, Doors & Glass", "Underbody shields & splash guards", Secondary, Inspect, 10000, 6, 5),
        Item("PM-BDG-004", "Body, Doors & Glass", "Side sliding door (track, rollers, latch)", Primary, Service, 10000, 6, 20, "Passenger ingress; lube & adjust"),
        Item("PM-BDG-005", "Body, Doors & Glass", "Sliding door rollers & track", Primary, Replace, 120000, 72, 120),
        Item("PM-BDG-006", "Body, Doors & Glass", "Rear cargo doors (hinges, latches, check straps)", Primary, Service, 10000, 6, 15),
        Item("PM-BDG-007", "Body, Doors & Glass", "Front doors (hinges, latches, strikers)", Secondary, Service, 20000, 12, 10),
        Item("PM-BDG-008", "Body, Doors & Glass", "Door seals & weatherstrips", Secondary, Inspect, 20000, 12, 10, "Wind noise, water/cold ingress"),
        Item("PM-BDG-009", "Body, Doors & Glass", "Door locks, child locks & remote", Primary, Test, 10000, 6, 5),
        Item("PM-BDG-010", "Body, Doors & Glass", "Hood latch, hinges & release cable", Primary, Service, 20000, 12, 10),
        Item("PM-BDG-011", "Body, Doors & Glass", "Windshield (chips, cracks in wiper sweep)", Primary, Inspect, 10000, 6, 5, "Repair chips before they spread in cold"),
        Item("PM-BDG-012", "Body, Doors & Glass", "Side & rear glass, seals", Secondary, Inspect, 20000, 12, 5),
        Item("PM-BDG-013", "Body, Doors & Glass", "Mirrors (exterior, interior, heated function)", Primary, Test, 10000, 6, 5),
        Item("PM-BDG-014", "Body, Doors & Glass", "Bumpers, grille & body panels", Secondary, Inspect, 20000, 12, 10),
        Item("PM-BDG-015", "Body, Doors & Glass", "Running boards / steps", Primary, Inspect, 10000, 6, 5, "Passenger fall risk; anti-slip condition"),
        Item("PM-BDG-016", "Body, Doors & Glass", "Roof, drip rails & seams (leaks)", Secondary, Inspect, 20000, 12, 10),
        Item("PM-BDG-017", "Body, Doors & Glass", "Mud flaps", Secondary, Inspect, 10000, 6, 3, "Gravel: protects passengers' luggage/rear glass"),
        Item("PM-BDG-018", "Body, Doors & Glass", "Livery, decals & vehicle markings", Secondary, Inspect, 20000, 12, 5, "Northern Link branding & required signage"),

        // ── Interior, Safety & Passenger (PM-ISP, 16) ────────────────────────────────────
        Item("PM-ISP-001", "Interior, Safety & Passenger", "Seat belts (webbing, buckles, retractors) - all 7", Primary, Test, 10000, 6, 15, "Pull test every belt"),
        Item("PM-ISP-002", "Interior, Safety & Passenger", "Seat belt pretensioners & anchors", Primary, Inspect, 40000, 24, 10),
        Item("PM-ISP-003", "Interior, Safety & Passenger", "Seats (frames, tracks, latches, anchors)", Primary, Inspect, 20000, 12, 15, "Torque seat-to-floor bolts"),
        Item("PM-ISP-004", "Interior, Safety & Passenger", "Seat upholstery & headrests", Secondary, Inspect, 20000, 12, 5),
        Item("PM-ISP-005", "Interior, Safety & Passenger", "Airbag system (warning lamp, codes)", Primary, Test, 10000, 6, 5),
        Item("PM-ISP-006", "Interior, Safety & Passenger", "First aid kit (contents, expiry)", Primary, Inspect, 0, 6, 5),
        Item("PM-ISP-007", "Interior, Safety & Passenger", "Fire extinguisher (charge, inspection tag)", Primary, Inspect, 0, 6, 5, "Annual professional inspection"),
        Item("PM-ISP-008", "Interior, Safety & Passenger", "Emergency triangles / flares", Primary, Inspect, 0, 6, 3),
        Item("PM-ISP-009", "Interior, Safety & Passenger", "Winter survival kit (blankets, candles, shovel)", Primary, Inspect, 0, 6, 5, "Oct - replenish"),
        Item("PM-ISP-010", "Interior, Safety & Passenger", "Floor mats, flooring & step lighting", Secondary, Inspect, 10000, 6, 5, "Trip hazards"),
        Item("PM-ISP-011", "Interior, Safety & Passenger", "Grab handles & assist straps", Primary, Inspect, 10000, 6, 5),
        Item("PM-ISP-012", "Interior, Safety & Passenger", "Instrument cluster, gauges & odometer", Primary, Test, 10000, 6, 5),
        Item("PM-ISP-013", "Interior, Safety & Passenger", "Pedal pads & pedal free play", Primary, Inspect, 10000, 6, 3),
        Item("PM-ISP-014", "Interior, Safety & Passenger", "Dash warning / chime functions", Secondary, Test, 20000, 12, 5),
        Item("PM-ISP-015", "Interior, Safety & Passenger", "Cargo tie-downs & partition", Primary, Inspect, 10000, 6, 5, "Cargo securement for combined loads"),
        Item("PM-ISP-016", "Interior, Safety & Passenger", "Driver tablet mount & charging", Secondary, Inspect, 10000, 6, 5, "Dispatcher app hardware"),

        // ── Compliance & Records (PM-CR, 7) ──────────────────────────────────────────────
        Item("PM-CR-001", "Compliance & Records", "Manitoba annual safety inspection (MPI)", Primary, Test, 0, 12, 60, "Commercial passenger vehicle requirement"),
        Item("PM-CR-002", "Compliance & Records", "Insurance, registration & permits in cab", Primary, Inspect, 0, 6, 5),
        Item("PM-CR-003", "Compliance & Records", "Pre-trip inspection log review", Primary, Inspect, 5000, 3, 10, "Audit driver logs for unresolved defects"),
        Item("PM-CR-004", "Compliance & Records", "Ford recall / TSB check (VIN)", Primary, Inspect, 0, 6, 10, "VIN 1FMZK1YM2GKB16423"),
        Item("PM-CR-005", "Compliance & Records", "Maintenance record update (platform)", Secondary, Service, 5000, 3, 10, "Close work orders, update odometer"),
        Item("PM-CR-006", "Compliance & Records", "Odometer & hour meter reading verification", Secondary, Inspect, 5000, 3, 3),
        Item("PM-CR-007", "Compliance & Records", "Mine-site readiness checklist (Alamos)", Primary, Test, 0, 6, 30, "Client compliance items"),
    ];

    /// <summary>
    /// The 10 major-component overhauls (OH-01..OH-10), each with the Test items whose
    /// latest measurements inform the overhaul-early decision (Appendix B mapping).
    /// </summary>
    public static List<OverhaulSpec> BuildOverhauls() =>
    [
        Overhaul("OH-01", "Engine (3.7L Ti-VCT V6)", 320000, 180, 40m, 6500m,
            "Teardown inspection: timing chains/guides/tensioners, VCT phasers, main & rod bearings, rings, valve seals, head gaskets, oil pump, water pump, all seals & gaskets. Or replace with reman long-block.",
            "Compression <85% of spec or >15% variance; leak-down >20%; oil analysis trending; oil consumption >1L/1500km",
            "PM-E-026", "PM-E-027", "PM-E-028", "PM-E-029"),
        Overhaul("OH-02", "Transmission (6R80 6-speed auto)", 240000, 144, 18m, 4500m,
            "Remove; replace torque converter, clutches/seals, solenoid body, filter, cooler flush; or reman unit. Relearn adaptives.",
            "Slip/flare on road test; burnt fluid; shudder; DTCs P07xx",
            "PM-TD-007", "PM-TD-009"),
        Overhaul("OH-03", "Rear Differential / Axle", 240000, 144, 12m, 2200m,
            "Ring & pinion bearings, pinion & carrier bearings, seals, axle bearings & seals, limited-slip clutches (if equipped), cover gasket.",
            "Backlash out of spec; bearing noise; metal in fluid",
            "PM-RAD-008"),
        Overhaul("OH-04", "Driveline (driveshaft assembly)", 160000, 96, 6m, 1200m,
            "Replace all U-joints, centre support bearing, slip yoke; balance shaft.",
            "Vibration >60 km/h; U-joint play; CSB sag",
            "PM-TD-014"),
        Overhaul("OH-05", "Front Suspension & Steering", 160000, 96, 14m, 3200m,
            "Struts/mounts, lower ball joints, control arm bushings, sway bar links/bushings, inner & outer tie rods; 4-wheel alignment.",
            "Play at any joint; uneven tire wear; alignment won't hold",
            "PM-S-011", "PM-S-032"),
        Overhaul("OH-06", "Rear Suspension", 240000, 144, 10m, 2800m,
            "Leaf springs (pair), bushings, shackles, U-bolts, shocks, bump stops.",
            "Ride height below spec; cracked leaf; bushing separation",
            "PM-S-032"),
        Overhaul("OH-07", "Brake System (complete)", 160000, 96, 10m, 2400m,
            "Calipers (4), rotors, pads, flex hoses, parking brake shoes & cables, master cylinder, full fluid flush; inspect/replace corroded hard lines.",
            "Hard line corrosion; caliper seizure; pedal sink",
            "PM-B-013", "PM-B-018", "PM-B-019", "PM-B-023", "PM-B-024"),
        Overhaul("OH-08", "Cooling System", 160000, 96, 8m, 1800m,
            "Water pump, thermostat & housing, all hoses, radiator (if >10 yr or core damage), fan assembly, pressure cap, coolant.",
            "Pressure test fail; temp creep; pump weep",
            "PM-C-011", "PM-C-012"),
        Overhaul("OH-09", "Electrical: Starting & Charging", 240000, 144, 5m, 1600m,
            "Alternator, starter, battery, main cables & ground straps, block heater element.",
            "Alternator ripple/output low; starter draw high; repeated cold no-start",
            "PM-ISC-005", "PM-ISC-009", "PM-ISC-011", "PM-ISC-013"),
        Overhaul("OH-10", "Fuel & Ignition", 160000, 96, 6m, 2000m,
            "Fuel pump module, injectors (clean/flow or replace), spark plugs, ignition coils (6), PCV, O2 sensors (4).",
            "Fuel pressure out of spec; misfire counts; rich/lean trims >10%",
            "PM-F-002", "PM-EE-003"),
    ];

    /// <summary>One source row: a km/mo of 0 means "axis not applicable" → null interval.</summary>
    private static MaintenanceItem Item(
        string code,
        string system,
        string component,
        ComponentTier tier,
        MaintenanceTask task,
        int km,
        int mo,
        int minutes,
        string? notes = null) => new()
    {
        Code = code,
        System = system,
        Component = component,
        Tier = tier,
        Task = task,
        IntervalKm = km == 0 ? null : km,
        IntervalMonths = mo == 0 ? null : mo,
        ShopMinutes = minutes,
        Notes = notes,
    };

    /// <summary>
    /// One overhaul row: condition triggers arrive as the source's single ";"-separated
    /// string and are split (trimmed) into the list the domain stores.
    /// </summary>
    private static OverhaulSpec Overhaul(
        string code,
        string component,
        int km,
        int mo,
        decimal labourHrs,
        decimal partsCad,
        string scope,
        string conditionTriggers,
        params string[] relatedItemCodes) => new()
    {
        Code = code,
        Component = component,
        IntervalKm = km == 0 ? null : km,
        IntervalMonths = mo == 0 ? null : mo,
        LabourHours = labourHrs,
        PartsCad = partsCad,
        Scope = scope,
        ConditionTriggers = [.. conditionTriggers.Split(';').Select(t => t.Trim())],
        RelatedItemCodes = [.. relatedItemCodes],
    };
}
