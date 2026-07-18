// NL-TM-01 section builders. Each returns an HTML fragment for one block of the
// Trip Manifest & Vehicle Inspection Report. ONE code path: every builder takes
// `m: TripManifest | null` — null renders the blank paper form (empty fields,
// all checkboxes open, no provenance strip); a manifest renders the filled form.
// Reuses the shared HTML helpers from the work order module.

import type { ManifestPreTripItem, TripManifest } from "@/lib/api";
import type { CompanyInfo } from "@/lib/company";
import {
  ATTESTATIONS,
  MAX_CARGO_ROWS,
  MAX_PASSENGER_ROWS,
  POST_TRIP_ITEMS,
  PRE_TRIP_GROUPS,
  type PreTripGroupId,
} from "@/lib/tripManifestChecklist";
import { box, check, esc, field, grid, sectionBar } from "../workOrderPdf/html";

// ---- local display mapping (wire enums → paper labels) ----------------------

const FUEL_LEVELS: { key: NonNullable<TripManifest["fuelLevel"]>; label: string }[] = [
  { key: "Full", label: "Full" },
  { key: "ThreeQuarters", label: "3/4" },
  { key: "Half", label: "1/2" },
  { key: "Quarter", label: "1/4" },
];

const WEATHER_OPTIONS: { key: TripManifest["weather"][number]; label: string }[] = [
  { key: "Clear", label: "Clear" },
  { key: "Cloudy", label: "Cloudy" },
  { key: "Rain", label: "Rain" },
  { key: "Snow", label: "Snow" },
  { key: "Fog", label: "Fog" },
  { key: "ExtremeCold", label: "Extreme Cold" },
];

const ROAD_OPTIONS: { key: TripManifest["roadConditions"][number]; label: string }[] = [
  { key: "Dry", label: "Dry" },
  { key: "Wet", label: "Wet" },
  { key: "Icy", label: "Icy" },
  { key: "SnowCovered", label: "Snow-covered" },
  { key: "Muddy", label: "Muddy" },
];

const VISIBILITY_OPTIONS: NonNullable<TripManifest["visibility"]>[] = ["Good", "Reduced", "Poor"];

const SEVERITY_LABEL: Record<NonNullable<ManifestPreTripItem["severity"]>, string> = {
  Minor: "Minor",
  Major: "Major",
  OutOfService: "Out of service",
};

const cadFmt = new Intl.NumberFormat("en-CA", { style: "currency", currency: "CAD" });

function cad(v: number | null | undefined): string {
  return v == null ? "" : cadFmt.format(v);
}

function km(v: number | null | undefined): string {
  return v == null ? "" : v.toLocaleString("en-CA");
}

function fmtDateTime(iso: string | null | undefined): string {
  if (!iso) return "";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString("en-CA", {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}

/** An inline run of ☐/☒ option checkboxes inside a labelled field cell. */
function checkField(label: string, checks: string): string {
  return `<div class="fld"><div class="lbl">${esc(label)}</div><div class="val">${checks}</div></div>`;
}

// ---- header, provenance & repeat headers ------------------------------------

export function header(company: CompanyInfo, m: TripManifest | null): string {
  return `
  <div class="head">
    <div>
      <div class="brand">NORTHERN LINK <span class="blue">SHUTTLE AND CARGO</span></div>
      <div class="brand-sub">${esc(company.address)} &nbsp;•&nbsp; ${esc(company.phone)}<br/>${esc(company.email)}</div>
    </div>
    <div class="doc-title">
      <div class="t">TRIP MANIFEST &amp; VEHICLE INSPECTION REPORT</div>
      <div class="s">Form NL-TM-01 &nbsp;•&nbsp; Page 1 of 4</div>
    </div>
  </div>
  <div class="rule"></div>
  <div class="warn">This document must be completed before departure. Do not operate if any critical item fails inspection.</div>
  ${provenanceStrip(m)}`;
}

function provenanceStrip(m: TripManifest | null): string {
  if (!m) return "";
  const text =
    m.source === "Paper"
      ? `<b>Source:</b> Paper form — transcribed by ${esc(m.enteredBy ?? "")} on ${esc(fmtDateTime(m.enteredAt))}`
      : `<b>Source:</b> Driver App submission`;
  return `<div class="prov">${text}</div>`;
}

export function slimHeader(m: TripManifest | null, page: number): string {
  const trip = m ? esc(m.tripNumber) : "____________";
  const unit = m ? esc(m.unit) : "________";
  return `
  <div class="head">
    <div>
      <div class="brand">NORTHERN LINK <span class="blue">SHUTTLE AND CARGO</span></div>
      <div class="brand-sub">Trip # ${trip} &nbsp;•&nbsp; Unit ${unit} &nbsp;•&nbsp; Page ${page} of 4</div>
    </div>
    <div class="doc-title"><div class="s">TRIP MANIFEST &amp; VEHICLE INSPECTION REPORT — Form NL-TM-01</div></div>
  </div>
  <div class="rule"></div>`;
}

// ---- §1 & §2 -----------------------------------------------------------------

export function tripInfoBlock(m: TripManifest | null): string {
  const direction =
    check(m?.direction === "Inbound", "Inbound") + check(m?.direction === "Outbound", "Outbound");
  return (
    sectionBar("1. Trip Information") +
    grid([
      field("Date", m?.tripDate, { mono: true }),
      field("Trip #", m?.tripNumber, { mono: true }),
      checkField("Direction", direction),
      field("Client", m?.client),
    ]) +
    grid([field("Route", m?.route, { wide: true })])
  );
}

export function vehicleDriverBlock(m: TripManifest | null): string {
  const fuel = FUEL_LEVELS.map((f) => check(m?.fuelLevel === f.key, f.label)).join("");
  return (
    sectionBar("2. Vehicle & Driver Information") +
    grid([
      field("Vehicle Unit", m?.unit, { mono: true }),
      field("Driver Name", m?.driverName),
      field("Odometer Start", km(m?.odometerStartKm), { mono: true }),
    ], 3) +
    grid([
      field("Licence Plate", m?.licencePlate, { mono: true }),
      field("Driver Licence #", m?.driverLicenceNo, { mono: true }),
      checkField("Fuel Level", fuel),
    ], 3)
  );
}

// ---- §3 pre-trip inspection ---------------------------------------------------

export function preTripBlock(m: TripManifest | null, groups: PreTripGroupId[], continued: boolean): string {
  const bar = continued
    ? sectionBar("3. Pre-Trip Vehicle Inspection (continued)")
    : sectionBar("3. Pre-Trip Vehicle Inspection") +
      `<div class="note">Check each item. Mark ✓ if OK. If any item marked FAIL, document in Section 7 and do not depart until resolved.</div>`;

  const tables = PRE_TRIP_GROUPS.filter((g) => groups.includes(g.group))
    .map((g) => {
      const rows = g.items
        .map((item) => {
          // Filled rows are matched by the backend wire strings (group key +
          // item key); the long form label is what gets printed.
          const rec = m?.preTrip.find((p) => p.group === g.key && p.item === item.key) ?? null;
          const okCell = box(rec?.status === "Ok");
          const failed = rec?.status === "Fail";
          const sev = failed && rec?.severity ? ` <span class="sev">${esc(SEVERITY_LABEL[rec.severity])}</span>` : "";
          return `<tr><td>${esc(item.label)}</td><td class="ck">${okCell}</td><td class="ck">${box(failed)}${sev}</td></tr>`;
        })
        .join("");
      return (
        `<div class="subsec">${esc(g.title)}</div>` +
        `<table>
           <thead><tr><th>Item</th><th class="ck">OK</th><th class="ck">Fail</th></tr></thead>
           <tbody>${rows}</tbody>
         </table>`
      );
    })
    .join("");

  return bar + tables;
}

// ---- §4 weather & road ---------------------------------------------------------

export function weatherRoadBlock(m: TripManifest | null): string {
  const weather = WEATHER_OPTIONS.map((w) => check(m?.weather.includes(w.key) ?? false, w.label)).join("");
  const road = ROAD_OPTIONS.map((r) => check(m?.roadConditions.includes(r.key) ?? false, r.label)).join("");
  const vis = VISIBILITY_OPTIONS.map((v) => check(m?.visibility === v, v)).join("");
  return (
    sectionBar("4. Weather & Road Conditions") +
    `<div class="grid" style="grid-template-columns:2.6fr 1fr">
       ${checkField("Weather", weather)}
       ${field("Temperature", m?.temperatureC, { mono: true })}
     </div>
     <div class="grid" style="grid-template-columns:1.6fr 1fr">
       ${checkField("Road Conditions", road)}
       ${checkField("Visibility", vis)}
     </div>` +
    grid([field("Road Advisories", m?.roadAdvisories, { wide: true })])
  );
}

// ---- §5 passenger manifest ------------------------------------------------------

export function passengerManifestBlock(m: TripManifest | null): string {
  const rows = Array.from({ length: MAX_PASSENGER_ROWS }, (_, i) => {
    const p = m?.passengers[i] ?? null;
    return `<tr>
      <td class="num">${i + 1}</td>
      <td>${esc(p?.name) || "&nbsp;"}</td>
      <td>${esc(p?.contact) || "&nbsp;"}</td>
      <td>${esc(p?.pickup) || "&nbsp;"}</td>
      <td>${esc(p?.dropoff) || "&nbsp;"}</td>
      <td class="ck">${box(p?.idVerified ?? false)}</td>
      <td class="ck">${box(p?.boardedOn ?? false)}</td>
      <td class="ck">${box(p?.boardedOff ?? false)}</td>
    </tr>`;
  }).join("");
  const total = m ? String(m.passengers.length) : "";
  const seatbelts = check(m?.allSeatbeltsVerified ?? false, "Yes");
  return (
    sectionBar("5. Passenger Manifest") +
    `<div class="note">Verify passenger identity. Check seatbelt compliance before departure.</div>
     <table>
       <thead><tr><th class="num">#</th><th>Passenger Name</th><th>Email / Phone</th><th>Pickup</th><th>Drop-off</th><th class="ck">ID</th><th class="ck">On</th><th class="ck">Off</th></tr></thead>
       <tbody>${rows}</tbody>
     </table>` +
    `<div class="grid" style="grid-template-columns:1fr 1.6fr">
       ${field("Total Passengers", total, { mono: true })}
       ${checkField("All Seatbelts Verified", seatbelts)}
     </div>`
  );
}

// ---- §6 cargo manifest -----------------------------------------------------------

export function cargoManifestBlock(m: TripManifest | null): string {
  const rows = Array.from({ length: MAX_CARGO_ROWS }, (_, i) => {
    const c = m?.cargo[i] ?? null;
    const hazmat = c ? `${box(c.hazmat)}Y ${box(!c.hazmat)}N` : "☐Y ☐N";
    return `<tr>
      <td class="num">${i + 1}</td>
      <td>${esc(c?.description) || "&nbsp;"}</td>
      <td>${esc(c?.ownerRecipient) || "&nbsp;"}</td>
      <td>${c?.weightKg != null ? esc(`${km(c.weightKg)} kg`) : "&nbsp;"}</td>
      <td>${esc(cad(c?.chargeCad)) || "&nbsp;"}</td>
      <td class="ck">${hazmat}</td>
      <td class="ck">${box(c?.secured ?? false)}</td>
    </tr>`;
  }).join("");
  const secured =
    check(m?.allCargoSecured === "Yes", "Yes") + check(m?.allCargoSecured === "NotApplicable", "N/A");
  const totalCharges = m ? cad(m.cargo.reduce((sum, c) => sum + (c.chargeCad ?? 0), 0)) : "";
  return (
    sectionBar("6. Cargo Manifest") +
    `<div class="note">All cargo must be secured before departure. Hazmat requires special handling documentation.</div>
     <table>
       <thead><tr><th class="num">#</th><th>Description</th><th>Owner / Recipient</th><th>Weight</th><th>Charge</th><th class="ck">Hazmat</th><th class="ck">Secured</th></tr></thead>
       <tbody>${rows}</tbody>
     </table>` +
    `<div class="grid" style="grid-template-columns:1.6fr 1fr">
       ${checkField("All Cargo Secured", secured)}
       ${field("Total Cargo Charges", totalCharges, { mono: true })}
     </div>`
  );
}

// ---- §7 issues / defects log -------------------------------------------------------

export function issuesLogBlock(m: TripManifest | null): string {
  const MIN_LINES = 4;
  const issues = m?.issues ?? [];
  const lineCount = Math.max(issues.length, MIN_LINES);
  const lines = Array.from(
    { length: lineCount },
    (_, i) => `<div class="line">${esc(issues[i]) || "&nbsp;"}</div>`,
  ).join("");
  return (
    sectionBar("7. Issues / Defects Log") +
    `<div class="note">Document any pre-trip inspection failures, incidents, near-misses, or mechanical issues. Attach additional pages if needed.</div>
     <div class="lines">
       <div class="nochk">${box(m?.noIssues ?? false)} No issues to report</div>
       ${lines}
     </div>`
  );
}

// ---- §8 trip completion log ---------------------------------------------------------

export function completionLogBlock(m: TripManifest | null): string {
  const litres = m?.fuelAdded && m.fuelLitres != null ? String(m.fuelLitres) : "______";
  const fuelAdded = m
    ? `${check(m.fuelAdded, `Yes (Litres: ${litres})`)}${check(!m.fuelAdded, "No")}`
    : `${check(false, "Yes (Litres: ______)")}${check(false, "No")}`;
  return (
    sectionBar("8. Trip Completion Log") +
    grid([
      field("Departure Time", m?.departureTime, { mono: true }),
      field("Arrival Time", m?.arrivalTime, { mono: true }),
      field("Odometer End", km(m?.odometerEndKm), { mono: true }),
      field("Total KM", km(m?.totalKm), { mono: true }),
    ]) +
    `<div class="grid" style="grid-template-columns:1.6fr 1fr">
       ${checkField("Fuel Added", fuelAdded)}
       ${field("Fuel Cost", cad(m?.fuelCostCad), { mono: true })}
     </div>`
  );
}

// ---- §9 post-trip inspection -----------------------------------------------------------

export function postTripBlock(m: TripManifest | null): string {
  const rows = POST_TRIP_ITEMS.map((item) => {
    const rec = m?.postTrip.find((p) => p.item === item) ?? null;
    return `<tr><td>${esc(item)}</td><td class="ck">${box(rec?.ok ?? false)}</td></tr>`;
  }).join("");
  return (
    sectionBar("9. Post-Trip Inspection") +
    `<table>
       <thead><tr><th>Item</th><th class="ck">OK</th></tr></thead>
       <tbody>${rows}</tbody>
     </table>`
  );
}

// ---- §10 driver certification -------------------------------------------------------------

export function certificationBlock(m: TripManifest | null): string {
  const attLines = ATTESTATIONS.map(
    (a, i) => `<div class="att">${box(m?.attestations[i] ?? false)} (${i + 1}) ${esc(a)}</div>`,
  ).join("");

  const signature = m
    ? `X ${esc(m.driverSignatureName)} <span class="via">${
        m.source === "Paper" ? "(as transcribed from paper form)" : "(signed via Driver App)"
      }</span>`
    : "X&nbsp;";
  const when = m ? esc(fmtDateTime(m.certifiedAt)) : "&nbsp;";

  return (
    sectionBar("10. Driver Certification") +
    `<div class="certbox">
       <div class="certintro">I certify that:</div>
       ${attLines}
       <div class="sign">
         <div>
           <div class="sigval">${signature}</div>
           <div class="sigline">Driver Signature</div>
         </div>
         <div>
           <div class="sigval">${when}</div>
           <div class="sigline">Date &amp; Time</div>
         </div>
       </div>
     </div>`
  );
}

// ---- footer -----------------------------------------------------------------------------

export function footer(company: CompanyInfo): string {
  return `<div class="foot">
    <b>Northern Link Shuttle and Cargo</b> | ${esc(company.phone)} | payments@northernlinkshuttleandcargo.com<br/>
    Retain completed manifests for 2 years. Report safety concerns immediately.
  </div>`;
}
