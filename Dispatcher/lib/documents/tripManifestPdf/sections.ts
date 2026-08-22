// NL-TM-01 (slim) Trip Manifest section builders. The manifest is now a slim
// passenger + cargo manifest — the pre/post-trip inspection, weather/road,
// completion log and certification sections moved to Fleet VehicleInspection
// records and are out of scope for this document. ONE code path: every builder
// takes `m: TripManifest | null` — null renders the blank paper form.

import type { TripManifest } from "@/lib/api";
import type { CompanyInfo } from "@/lib/company";
import { MAX_CARGO_ROWS, MAX_PASSENGER_ROWS } from "@/lib/tripManifestChecklist";
import { box, check, esc, field, grid, sectionBar } from "../workOrderPdf/html";

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
    hour12: true,
  });
}

/** An inline run of ☐/☒ option checkboxes inside a labelled field cell. */
function checkField(label: string, checks: string): string {
  return `<div class="fld"><div class="lbl">${esc(label)}</div><div class="val">${checks}</div></div>`;
}

// ---- header & provenance ----------------------------------------------------

export function header(company: CompanyInfo, m: TripManifest | null): string {
  return `
  <div class="head">
    <div>
      <div class="brand">NORTHERN LINK <span class="blue">SHUTTLE AND CARGO</span></div>
      <div class="brand-sub">${esc(company.address)} &nbsp;•&nbsp; ${esc(company.phone)}<br/>${esc(company.email)}</div>
    </div>
    <div class="doc-title">
      <div class="t">PASSENGER &amp; CARGO MANIFEST</div>
      <div class="s">Form NL-TM-01</div>
    </div>
  </div>
  <div class="rule"></div>
  <div class="warn">Verify passenger identity and secure all cargo before departure. Pre/post-trip vehicle inspections are recorded separately.</div>
  ${provenanceStrip(m)}`;
}

function provenanceStrip(m: TripManifest | null): string {
  if (!m) return "";
  const text =
    m.source === "Dispatcher"
      ? `<b>Source:</b> Dispatcher entry — ${esc(m.enteredBy ?? "")}${m.enteredAt ? ` on ${esc(fmtDateTime(m.enteredAt))}` : ""}`
      : `<b>Source:</b> Driver App submission`;
  return `<div class="prov">${text}</div>`;
}

// ---- §1 trip information -----------------------------------------------------

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

// ---- §2 passenger manifest ---------------------------------------------------

export function passengerManifestBlock(m: TripManifest | null): string {
  // Render a full blank form (MAX_PASSENGER_ROWS rows) but grow to fit every
  // passenger when a larger unit carries more than the default 8 — otherwise
  // passengers past row 8 would be silently dropped from the printed manifest.
  const rowCount = Math.max(MAX_PASSENGER_ROWS, m?.passengers.length ?? 0);
  const rows = Array.from({ length: rowCount }, (_, i) => {
    const p = m?.passengers[i] ?? null;
    return `<tr>
      <td class="num">${i + 1}</td>
      <td>${esc(p?.name) || "&nbsp;"}</td>
      <td>${esc([p?.email, p?.phone].filter(Boolean).join(" · ")) || "&nbsp;"}</td>
      <td>${esc(p?.pickupStopName) || "&nbsp;"}</td>
      <td>${esc(p?.dropoffStopName) || "&nbsp;"}</td>
      <td class="ck">${box(p?.idVerified ?? false)}</td>
      <td class="ck">${box(p?.boardedOn ?? false)}</td>
      <td class="ck">${box(p?.boardedOff ?? false)}</td>
    </tr>`;
  }).join("");
  const total = m ? String(m.passengers.length) : "";
  const seatbelts = check(m?.allSeatbeltsVerified ?? false, "Yes");
  return (
    sectionBar("2. Passenger Manifest") +
    `<div class="note">Verify passenger identity. Check seatbelt compliance before departure. Pickup / drop-off reference the trip's route stops.</div>
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

// ---- §3 cargo manifest -------------------------------------------------------

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
    sectionBar("3. Cargo Manifest") +
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

// ---- footer -----------------------------------------------------------------

export function footer(company: CompanyInfo): string {
  return `<div class="foot">
    <b>Northern Link Shuttle and Cargo</b> | ${esc(company.phone)} | payments@northernlinkshuttleandcargo.com<br/>
    Retain completed manifests for 2 years. Report safety concerns immediately.
  </div>`;
}
