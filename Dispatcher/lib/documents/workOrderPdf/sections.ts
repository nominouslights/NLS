// NL-WO-01 section builders. Each returns an HTML fragment for one block of the
// form. Page 1 (§1–§6) is prefilled from data; page 2 (§7–§13) is left blank for
// the repair shop to complete and certify.

import type { Vehicle } from "@/lib/api";
import type { Shop, WorkOrder } from "@/lib/types";
import type { CompanyInfo } from "@/lib/company";
import { check, esc, field, grid, sectionBar } from "./html";

export function header(company: CompanyInfo): string {
  return `
  <div class="head">
    <div>
      <div class="brand">NORTHERN LINK <span class="blue">SHUTTLE &amp; CARGO</span></div>
      <div class="brand-sub">${esc(company.address)} &nbsp;•&nbsp; ${esc(company.phone)}<br/>${esc(company.email)}</div>
    </div>
    <div class="doc-title">
      <div class="t">VEHICLE WORK ORDER</div>
      <div class="s">Authorization • Repair Record • Certification of Repairs<br/>Form NL-WO-01</div>
    </div>
  </div>
  <div class="rule"></div>`;
}

export function topStrip(wo: WorkOrder): string {
  return grid([
    field("Work Order No.", wo.id, { mono: true }),
    field("Date Issued", wo.createdAt, { mono: true }),
    field("Date Required / Out-of-Service Until", wo.dateRequiredOrOos ?? wo.dueDate, { mono: true }),
    field("Budget Code (Internal)", wo.budgetCode, { mono: true }),
  ]);
}

export function carrierBlock(company: CompanyInfo): string {
  return (
    sectionBar("1. Carrier (Customer)") +
    grid([
      field("Legal Carrier Name", company.legalName, { wide: true }),
    ]) +
    grid([
      field("NSC No.", company.nscNo, { mono: true }),
      field("Safety Fitness Certificate No.", company.sfcNo, { mono: true }),
      field("Authorized By (print)", company.authorizedBy),
    ], 3)
  );
}

export function shopBlock(shop: Shop | null): string {
  const mpi = !shop ? "☐ Yes ☐ No" : shop.mpiAccredited ? "☒ Yes ☐ No" : "☐ Yes ☒ No";
  return (
    sectionBar("2. Repair Facility (Shop)") +
    grid([
      field("Shop / Business Name", shop?.name),
      field("Contact Name", shop?.contactName),
      field("Phone", shop?.phone, { mono: true }),
      field("Email", shop?.email),
    ]) +
    grid([
      field("Address", shop?.address, { wide: true }),
    ]) +
    grid([
      field("GST / Business No.", shop?.gstBusinessNo, { mono: true }),
      `<div class="fld"><div class="lbl">MPI Accredited Repair Shop?</div><div class="val">${mpi}</div></div>`,
      field("MB Vehicle Inspection Station No.", shop?.inspectionStationNo, { mono: true }),
    ], 3)
  );
}

export function vehicleBlock(vehicle: Vehicle): string {
  const sfc = vehicle.requiresPeriodicInspection
    ? `${check(true, "Yes (bus)")} ${check(false, "No (Transit — under 11 seats / 4,500 kg)")}`
    : `${check(false, "Yes (bus)")} ${check(true, "No (Transit — under 11 seats / 4,500 kg)")}`;
  return (
    sectionBar("3. Vehicle Identification") +
    grid([
      field("Unit No.", vehicle.unitNumber, { mono: true }),
      field("Year / Make / Model", `${vehicle.year} ${vehicle.make} ${vehicle.model}`),
      field("VIN", vehicle.vin, { mono: true }),
      field("Plate No.", vehicle.licencePlate, { mono: true }),
    ]) +
    grid([
      field("Odometer In (km)", vehicle.odometerKm.toLocaleString("en-CA"), { mono: true }),
      field("Odometer Out (km)", ""),
      field("Engine Hours (if equipped)", ""),
      `<div class="fld"><div class="lbl">Operated under SFC as a motor carrier?</div><div class="val">${sfc}</div></div>`,
    ])
  );
}

const SOURCE_CHECKS: { key: string; label: string; matches: WorkOrder["source"][] }[] = [
  { key: "pm", label: "Scheduled preventive maintenance (PM interval)", matches: ["PM Reminder"] },
  { key: "dvir", label: "Driver defect report (DVIR)", matches: ["Pre-Trip Inspection", "Post-Trip Inspection"] },
  { key: "periodic", label: "Annual / periodic mandatory safety inspection", matches: [] },
  { key: "roadside", label: "Roadside inspection defect", matches: [] },
  { key: "tires", label: "Tires / seasonal changeover", matches: [] },
  { key: "breakdown", label: "Breakdown / road call", matches: ["DTC Alert"] },
  { key: "other", label: "Other", matches: ["Manual"] },
];

export function reasonBlock(wo: WorkOrder): string {
  const dvirRef = wo.source === "Pre-Trip Inspection" || wo.source === "Post-Trip Inspection" ? wo.sourceRef ?? "" : "";
  const sourceChecks = SOURCE_CHECKS.map((c) => {
    const on = c.matches.includes(wo.source);
    const label = c.key === "dvir" ? `${c.label} — Report No. ${dvirRef || "____________"}` : c.label;
    return check(on, label);
  }).join("");

  // Vehicle status at time of request, from priority.
  const oos = wo.priority === "Critical";
  const minor = wo.priority === "High" || wo.priority === "Medium";
  const statusChecks =
    check(!oos && !minor, "In service — no defect affecting safe operation") +
    check(oos, "OUT OF SERVICE — major defect. Do not operate until repaired and certified.") +
    check(minor, "Minor defect — repair scheduled, vehicle may operate");

  return (
    sectionBar("4. Reason for Work — Source of Request") +
    `<div class="grid" style="grid-template-columns:1.3fr 1fr">
       <div class="fld"><div class="lbl">Check all that apply</div><div class="val">${sourceChecks}</div></div>
       <div class="fld"><div class="lbl">Vehicle status at time of request</div><div class="val">${statusChecks}</div></div>
     </div>`
  );
}

export function workRequestedBlock(wo: WorkOrder): string {
  const items = wo.lineItems.length ? wo.lineItems : [wo.title];
  const minRows = Math.max(items.length, 4);
  const rows = Array.from({ length: minRows }, (_, i) => {
    const desc = items[i] ?? "";
    const by = desc ? esc(wo.source === "Manual" ? "Dispatch" : wo.source) : "";
    const date = desc ? esc(wo.createdAt) : "";
    return `<tr><td class="num">${i + 1}</td><td>${esc(desc) || "&nbsp;"}</td><td>${by || "&nbsp;"}</td><td>${date || "&nbsp;"}</td></tr>`;
  }).join("");
  return (
    sectionBar("5. Work Requested / Defects Reported (completed by Northern Link)") +
    `<table>
       <thead><tr><th class="num">#</th><th>Description of defect or work requested</th><th>Reported by</th><th>Date reported</th></tr></thead>
       <tbody>${rows}</tbody>
     </table>`
  );
}

export function spendingLimitPointer(): string {
  return `<div class="notice">&#9888; <b>Spending limit — see &sect;12, page 2.</b> Read the authorization limit and the additional-work instructions before starting work.</div>`;
}

export function partsByCarrierBlock(): string {
  const rows = Array.from({ length: 3 }, () => `<tr><td class="blank">&nbsp;</td><td></td><td></td></tr>`).join("");
  return (
    sectionBar("6. Parts & Materials Supplied by Carrier (if any)") +
    `<table><thead><tr><th>Item</th><th>Part / Serial No.</th><th>Qty</th></tr></thead><tbody>${rows}</tbody></table>`
  );
}

export function footer(): string {
  return `<div class="foot"><b>Purpose of this form.</b> Northern Link Shuttle &amp; Cargo maintains a systematic maintenance program for every vehicle under its control, as required by <b>National Safety Code Standard 11</b> and the Manitoba <i>Highway Traffic Act</i>. A completed copy of this work order — including Page 2 — is filed against the unit's maintenance history and produced on request during a carrier facility audit. Defects recorded on a driver's inspection report must be repaired and certified before the vehicle is returned to service. Please return the completed form with your invoice.</div>`;
}

// ---- Page 2 — shop completion & certification (blank for the shop) ----------

export function authorizationBlock(wo: WorkOrder): string {
  const limit = wo.authorizedLimitCad != null ? `$ ${wo.authorizedLimitCad.toLocaleString("en-CA")} CAD` : "$ ____________ CAD";
  return (
    sectionBar("12. Authorization Limit — Read Before Starting Work") +
    `<div class="grid" style="grid-template-columns:1fr 1.6fr">
       <div class="fld"><div class="lbl">Approved not to exceed (before GST)</div><div class="val mono">${esc(limit)}</div></div>
       <div class="fld"><div class="lbl">Additional work discovered</div><div class="val" style="font-size:9px;font-weight:400">Stop and call (204) 441-7724 for written approval before proceeding. Work beyond this limit without written authorization may not be paid.</div></div>
     </div>
     <div class="grid" style="grid-template-columns:1fr">
       <div class="fld"><div class="lbl">Authorized signature — Northern Link</div><div class="val">X ______________________________________&nbsp;&nbsp;&nbsp;Date: ____________</div></div>
     </div>`
  );
}

export function shopCompletionPage2(wo: WorkOrder, vehicle: Vehicle): string {
  const perfRows = Array.from({ length: Math.max(wo.lineItems.length || 0, 6) }, (_, i) =>
    `<tr><td class="num">${i + 1}</td><td class="blank"></td><td></td><td></td><td></td><td>☐ Y ☐ N</td></tr>`,
  ).join("");
  const deferredRows = Array.from({ length: 3 }, () =>
    `<tr><td class="blank"></td><td>☐ Yes — do not operate&nbsp;&nbsp;☐ No</td><td></td></tr>`,
  ).join("");

  return `
  <div class="page2">
    <div class="head">
      <div><div class="brand">NORTHERN LINK <span class="blue">SHUTTLE &amp; CARGO</span></div>
        <div class="brand-sub">Work Order No. ${esc(wo.id)} &nbsp;•&nbsp; Unit ${esc(vehicle.unitNumber)} &nbsp;•&nbsp; VIN ${esc(vehicle.vin)}</div></div>
      <div class="doc-title"><div class="t">SHOP COMPLETION &amp; CERTIFICATION</div><div class="s">To be completed by the repair facility — Page 2 of 2</div></div>
    </div>
    <div class="rule"></div>

    ${sectionBar("7. Work Performed (completed by shop)")}
    <table>
      <thead><tr><th class="num">Ref</th><th>Description of work performed / cause &amp; correction</th><th>Parts used (no. &amp; qty)</th><th>Labour hrs</th><th>Technician</th><th>Repaired?</th></tr></thead>
      <tbody>${perfRows}</tbody>
    </table>

    ${sectionBar("8. Deferred / Recommended Work Not Performed")}
    <table><thead><tr><th>Item</th><th>Safety-related?</th><th>Recommended by (date)</th></tr></thead><tbody>${deferredRows}</tbody></table>

    ${sectionBar("9. Sublet Work (if any)")}
    <table><thead><tr><th>Sublet to (business name)</th><th>Work performed</th><th>Their invoice no.</th></tr></thead><tbody><tr><td class="blank"></td><td></td><td></td></tr></tbody></table>

    ${sectionBar("10. Charges (Canadian dollars)")}
    ${grid([
      field("Parts $", ""),
      field("Labour ( ___ hrs @ $ ___ /hr) $", ""),
      field("Shop supplies / environmental $", ""),
      field("Sublet $", ""),
    ])}
    ${grid([
      field("Subtotal $", ""),
      field("GST (5%) $", ""),
      field("Total Due (CAD) $", ""),
    ], 3)}

    ${sectionBar("11. Inspection (complete only if a mandatory inspection was performed)")}
    ${grid([
      `<div class="fld"><div class="lbl">Inspection type</div><div class="val" style="font-size:9px">☐ Periodic (annual) CVI&nbsp;&nbsp;☐ MB Safety (COI)&nbsp;&nbsp;☐ Other</div></div>`,
      `<div class="fld"><div class="lbl">Result</div><div class="val">☐ Pass&nbsp;&nbsp;☐ Fail</div></div>`,
      field("Decal / Certificate No.", ""),
      field("Next Inspection Due", ""),
    ])}

    ${authorizationBlock(wo)}

    ${sectionBar("13. Certification of Repairs & Return to Service")}
    <div class="note">I certify that the work described was performed by or under my direct supervision; that the parts and materials used are suitable for the vehicle's intended use; and that every defect marked &ldquo;Repaired — Y&rdquo; has been repaired such that the vehicle complies with the Manitoba <i>Highway Traffic Act</i>, its regulations, and National Safety Code Standard 11. Any defect not repaired is listed in Section 8 and identified to the carrier.</div>
    <div class="sign">
      <div class="sigline">Technician / Journeyperson (print) &nbsp; Red Seal / Cert. No. &nbsp; Signature &nbsp; Date</div>
      <div class="sigline">Accepted by — Northern Link (Emelio Campbell) &nbsp; Signature &nbsp; Date returned to service</div>
    </div>
    <div class="foot">Northern Link Shuttle &amp; Cargo — Vehicle Maintenance Work Order | Form NL-WO-01 (Rev. July 2026) | Retain minimum 4 years per NSC Standard 11 / Manitoba <i>Highway Traffic Act</i>. Invoices without a completed and signed Page 2 cannot be filed as a maintenance record and may delay payment.</div>
  </div>`;
}
