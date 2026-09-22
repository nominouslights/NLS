// Form NL-PTI-01 section builders — the Daily Pre-Trip / Post-Trip Vehicle
// Inspection, as a printable sheet.
//
// ONE code path, exactly like NL-TM-01: every builder takes
// `insp: VehicleInspection | null`. A record renders its answers; `null` renders
// the blank paper form a driver carries as the device-failure fallback. A
// missing inspection is a blank page to fill in by hand, never an omitted one.
//
// `itemsFor(unit, mode)` decides which rows print, so an NL-01 sheet has no
// bus-only rows and a pre-trip sheet has no Close-Out group — see the filtering
// rules in lib/inspectionForm.ts (an UNKNOWN unit deliberately gets every row).
//
// Two things this file must never do:
//   1. Invent an answer. A row the record does not carry prints with all three
//      boxes empty, the same as on a blank form.
//   2. Drop a recorded answer. Items stored under a retired form revision are
//      not in today's catalogue; they print in a trailing block of their own
//      rather than vanishing, mirroring the console's RetiredFormChecklist.

import type { CompanyInfo } from "@/lib/company";
import type {
  ChecklistItemStateWire,
  InspectionChecklistItemWire,
  VehicleInspection,
} from "@/lib/api/maintenance";
import {
  NL_PTI_01,
  NL_PTI_01_CERTIFICATION,
  PRE_TRIP_VALIDITY_HOURS,
  itemsFor,
  type InspectionArea,
  type InspectionFormMode,
  type InspectionItem,
  type InspectionSubGroup,
} from "@/lib/inspectionForm";
import { DEFECT_SEVERITY_LABEL } from "@/lib/workOrderDisplay";
import { box, check, esc, field, grid, sectionBar } from "../workOrderPdf/html";

/** What the sheet needs that the inspection record does not carry (or does not
 *  carry yet — a blank form has no record at all). */
export interface InspectionPrintContext {
  /** Vehicle unit the form is for. `null` prints every row; see `itemsFor`. */
  unit: string | null;
  mode: InspectionFormMode;
  tripNumber?: string | null;
}

/** Blank defect-log rows on a form with fewer defects than this (or none). */
const MIN_DEFECT_ROWS = 6;

const AREA_TITLES: Record<InspectionArea, string> = {
  A: "Area A — Engine Bay",
  B: "Area B — Exterior Walk-Around",
  C: "Area C — In-Cab",
};

const SCOPE_LEGEND =
  "NL = Northern Link operational addition beyond Manitoba Reg 95/2008 Schedule B. " +
  "All unmarked rows are the NSC Standard 13 requirement.";

const NL02_LEGEND = "NL-02 = applies to the bus (NL-02) only.";

// ---- small formatters -------------------------------------------------------
// Local, and deliberately blank-on-missing: a blank form prints an empty ruled
// cell, never an em dash placeholder that looks like a recorded "nothing".

function fmtDate(iso: string | null | undefined): string {
  if (!iso) return "";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleDateString("en-CA", { year: "numeric", month: "short", day: "numeric" });
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

function km(v: number | null | undefined): string {
  return v == null ? "" : `${v.toLocaleString("en-CA")} km`;
}

/** An inline run of ☐/☒ option checkboxes inside a labelled field cell. */
function checkField(label: string, checks: string): string {
  return `<div class="fld"><div class="lbl">${esc(label)}</div><div class="val">${checks}</div></div>`;
}

// ---- answer lookup ----------------------------------------------------------

/**
 * The tri-state answer this record holds for `itemKey`, or null when it holds
 * none (which is every row of a blank form).
 *
 * Derived exactly as the console's `rowsFromRecord` derives it, so the printed
 * sheet and the screen can never disagree: a filed defect settles the row, then
 * the stored `state`, and only then the legacy `passed` boolean of a record
 * written before the tri-state existed.
 */
function stateFor(
  insp: VehicleInspection | null,
  itemKey: string,
): { state: ChecklistItemStateWire | null; note: string } {
  if (!insp) return { state: null, note: "" };
  const saved = insp.checklist.find((c) => c.item === itemKey);
  const defect = insp.defects.find((d) => d.item === itemKey);
  const state: ChecklistItemStateWire | null =
    defect != null ? "Defect" : (saved?.state ?? (saved ? (saved.passed ? "Ok" : "Defect") : null));
  return { state, note: saved?.note ?? defect?.note ?? "" };
}

/** The scope column's markers: NL-02 for a bus-only row, NL for a company addition. */
function scopeMarks(item: InspectionItem): string {
  const marks: string[] = [];
  if (item.scope === "NL02Only") marks.push("NL-02");
  if (item.basis === "NorthernLink") marks.push("NL");
  return marks.map((m) => `<span>${esc(m)}</span>`).join(" ");
}

// ---- header ------------------------------------------------------------------

export function header(
  company: CompanyInfo,
  insp: VehicleInspection | null,
  ctx: InspectionPrintContext,
): string {
  const unit = (ctx.unit ?? "").trim().toLowerCase();
  const unitChecks = check(unit === "nl-01", "NL-01") + check(unit === "nl-02", "NL-02");
  const modeChecks = check(ctx.mode === "PreTrip", "Pre-Trip") + check(ctx.mode === "PostTrip", "Post-Trip");
  const tripNumber = ctx.tripNumber ?? insp?.tripNumber ?? null;

  return `
  <div class="head">
    <div>
      <div class="brand">NORTHERN LINK <span class="blue">SHUTTLE AND CARGO</span></div>
      <div class="brand-sub">${esc(company.address)} &nbsp;•&nbsp; ${esc(company.phone)}<br/>${esc(company.email)}</div>
    </div>
    <div class="doc-title">
      <div class="t">DAILY PRE-TRIP / POST-TRIP INSPECTION</div>
      <div class="s">Form NL-PTI-01</div>
    </div>
  </div>
  <div class="rule"></div>
  <div class="warn">A Major defect takes the vehicle OUT OF SERVICE until it is repaired and re-inspected. Do not operate.</div>
  ${provenanceStrip(insp)}
  ${grid([
    field("Date", fmtDate(insp?.performedAt), { mono: true }),
    field("Driver", insp?.driverName),
    checkField("Unit", unitChecks),
    field("Odometer", km(insp?.odometerKm), { mono: true }),
  ])}
  ${grid(
    [field("Route / Trip #", tripNumber, { mono: true }), checkField("Inspection", modeChecks)],
    2,
  )}`;
}

function provenanceStrip(insp: VehicleInspection | null): string {
  if (!insp) return "";
  const text =
    insp.source === "Dispatcher"
      ? `<b>Source:</b> Dispatcher entry — ${esc(insp.enteredBy ?? "")}${insp.createdAtUtc ? ` on ${esc(fmtDateTime(insp.createdAtUtc))}` : ""}`
      : `<b>Source:</b> Driver App submission`;
  return `<div class="prov">${text}</div>`;
}

// ---- Areas A / B / C ---------------------------------------------------------

function itemRow(item: InspectionItem, insp: VehicleInspection | null): string {
  const { state, note } = stateFor(insp, item.key);
  const categoryNote = item.categoryNote ? ` (${item.categoryNote})` : "";
  return `<tr>
    <td class="item">${esc(item.label)}</td>
    <td>${esc(item.checkFor)}${esc(categoryNote)}</td>
    <td class="ck">${box(state === "Ok")}</td>
    <td class="ck">${box(state === "Defect")}</td>
    <td class="ck">${box(state === "NotApplicable")}</td>
    <td class="notes">${esc(note) || "&nbsp;"}</td>
    <td class="scope">${scopeMarks(item) || "&nbsp;"}</td>
  </tr>`;
}

function subGroupTable(group: InspectionSubGroup, insp: VehicleInspection | null): string {
  return `<div class="subsec">${esc(group.title)}</div>
    <table>
      <thead><tr>
        <th>Item</th><th>Check For</th>
        <th class="ck">OK</th><th class="ck">Def</th><th class="ck">N/A</th>
        <th class="notes">Notes</th><th class="scope">Scope</th>
      </tr></thead>
      <tbody>${group.items.map((item) => itemRow(item, insp)).join("")}</tbody>
    </table>`;
}

/**
 * One area's sub-groups, with the scope legend printed ONCE at the foot of the
 * area. An inspector has to be able to tell an NSC 13 requirement from a
 * Northern Link addition at a glance, so the legend travels with the rows
 * rather than sitting alone on the first page.
 */
function areaBlock(area: InspectionArea, groups: InspectionSubGroup[], insp: VehicleInspection | null): string {
  const inArea = groups.filter((g) => g.area === area);
  if (inArea.length === 0) return "";
  return (
    sectionBar(AREA_TITLES[area]) +
    inArea.map((g) => subGroupTable(g, insp)).join("") +
    `<div class="legend"><b>Scope:</b> ${esc(SCOPE_LEGEND)} ${esc(NL02_LEGEND)}</div>`
  );
}

/** Areas A, B and C for this unit and mode, in form order. */
export function checklistBlocks(insp: VehicleInspection | null, ctx: InspectionPrintContext): string {
  const groups = itemsFor(ctx.unit, ctx.mode);
  return (["A", "B", "C"] as InspectionArea[]).map((area) => areaBlock(area, groups, insp)).join("");
}

// ---- retired-form rows -------------------------------------------------------

/** Every wire key today's catalogue knows, across all units and both halves. */
const CATALOGUE_KEYS: ReadonlySet<string> = new Set(
  NL_PTI_01.flatMap((group) => group.items.map((item) => item.key)),
);

/**
 * Stored answers this sheet's catalogue rows did NOT print — either recorded
 * against a form revision NL-PTI-01 replaced, or against rows this unit/mode
 * filters out (a report entered for NL-02 and reprinted for NL-01).
 *
 * They print verbatim, as the record carries them. Nothing here guesses a
 * correspondence to a current row: the item string is half a defect's address,
 * and a wrong mapping would misreport what the driver actually answered.
 */
export function retiredFormBlock(
  insp: VehicleInspection | null,
  ctx: InspectionPrintContext,
): string {
  if (!insp) return "";
  const printed = new Set(
    itemsFor(ctx.unit, ctx.mode).flatMap((g) => g.items.map((i) => i.key)),
  );
  const leftover = insp.checklist.filter((c) => !printed.has(c.item));
  if (leftover.length === 0) return "";

  const rows = leftover
    .map((c: InspectionChecklistItemWire) => {
      const state: ChecklistItemStateWire = c.state ?? (c.passed ? "Ok" : "Defect");
      const defect = insp.defects.find((d) => d.item === c.item);
      return `<tr>
        <td class="item">${esc(c.group) || "&nbsp;"}</td>
        <td>${esc(c.item)}</td>
        <td class="ck">${box(state === "Ok")}</td>
        <td class="ck">${box(state === "Defect")}</td>
        <td class="ck">${box(state === "NotApplicable")}</td>
        <td class="notes">${esc(c.note ?? defect?.note ?? "") || "&nbsp;"}</td>
      </tr>`;
    })
    .join("");

  const cause = leftover.some((c) => !CATALOGUE_KEYS.has(c.item))
    ? "These rows were answered on a form revision NL-PTI-01 replaced."
    : "These rows were answered on a wider form than this unit and inspection type print.";

  return (
    sectionBar("Recorded under a previous form revision") +
    `<div class="note">${esc(cause)} They are reproduced exactly as recorded — nothing has been mapped onto a current row.</div>
     <table>
       <thead><tr>
         <th>Group</th><th>Item as recorded</th>
         <th class="ck">OK</th><th class="ck">Def</th><th class="ck">N/A</th>
         <th class="notes">Notes</th>
       </tr></thead>
       <tbody>${rows}</tbody>
     </table>`
  );
}

// ---- defect log --------------------------------------------------------------

/**
 * The defect log.
 *
 * "Reported To / When", "Repair Completed (Date)" and "Re-inspected" have NO
 * backing field on `VehicleInspection` — a defect's resolution is derived per
 * vehicle (`VehicleDefectWire.resolvedAtUtc`), not carried on the inspection
 * this sheet prints. All three are therefore blank ruled cells for hand
 * completion. Nothing here fabricates a repair date.
 */
export function defectLogBlock(insp: VehicleInspection | null): string {
  const defects = insp?.defects ?? [];
  const rowCount = Math.max(MIN_DEFECT_ROWS, defects.length);
  const rows = Array.from({ length: rowCount }, (_, i) => {
    const d = defects[i] ?? null;
    const description = d ? [d.item, d.note].filter(Boolean).join(" — ") : "";
    return `<tr>
      <td>${esc(description) || "&nbsp;"}</td>
      <td class="sev">${d ? esc(DEFECT_SEVERITY_LABEL[d.severity]) : "&nbsp;"}</td>
      <td class="blank">&nbsp;</td>
      <td class="blank">&nbsp;</td>
      <td class="blank">&nbsp;</td>
    </tr>`;
  }).join("");

  return (
    sectionBar("Defect Log") +
    `<div class="note">Record every Defect row above. Repair follow-up is completed by hand — the platform does not hold a per-defect repair date on this report.</div>
     <table>
       <thead><tr>
         <th>Description of Defect</th><th class="sev">Category</th>
         <th>Reported To / When</th><th>Repair Completed (Date)</th><th>Re-inspected</th>
       </tr></thead>
       <tbody>${rows}</tbody>
     </table>`
  );
}

// ---- certification -----------------------------------------------------------

/** §10 driver certification. A record keeps the sentence it was signed under;
 *  an older record without one, and a blank form, fall back to today's text so
 *  the sheet is never certified by a missing statement. */
export function certificationBlock(insp: VehicleInspection | null): string {
  const statement = insp?.certificationStatement ?? NL_PTI_01_CERTIFICATION;
  return (
    sectionBar("Driver Certification") +
    `<div class="certbox">
       ${esc(statement)}
       <div class="sign">
         <div>
           <div class="sigval">${esc(insp?.driverSignatureName) || "&nbsp;"}</div>
           <div class="sigline">Driver signature</div>
         </div>
         <div>
           <div class="sigval">${esc(fmtDateTime(insp?.certifiedAt)) || "&nbsp;"}</div>
           <div class="sigline">Date</div>
         </div>
       </div>
     </div>`
  );
}

/** The carrier acknowledgement line — signed when a report carries a Major or
 *  Out-of-Service defect. Blank ruled lines when it has not been signed. */
export function carrierAcknowledgementBlock(insp: VehicleInspection | null): string {
  const note = insp?.carrierAcknowledgementNote;
  return (
    sectionBar("Major defect — carrier acknowledgement") +
    `<div class="certbox">
       The carrier's representative confirms they were shown this report and its Major / Out-of-Service defects.
       ${note ? `<div class="certintro">${esc(note)}</div>` : ""}
       <div class="sign">
         <div>
           <div class="sigval">${esc(insp?.carrierAcknowledgedBy) || "&nbsp;"}</div>
           <div class="sigline">Carrier representative</div>
         </div>
         <div>
           <div class="sigval">${esc(fmtDateTime(insp?.carrierAcknowledgedAtUtc)) || "&nbsp;"}</div>
           <div class="sigline">Date</div>
         </div>
       </div>
     </div>`
  );
}

// ---- process rules + footer --------------------------------------------------

export function processRules(): string {
  return (
    sectionBar("Process Rules") +
    `<div class="rules">
       <ul>
         <li><b>A Major defect takes the vehicle out of service</b> until it has been repaired <b>and</b> re-inspected. Record both.</li>
         <li><b>A pre-trip inspection is valid for ${PRE_TRIP_VALIDITY_HOURS} hours.</b> A run starting after that needs a new pre-trip.</li>
         <li><b>Carry the current and the previous day's report in the vehicle</b>, available on request.</li>
       </ul>
     </div>`
  );
}

export function footer(company: CompanyInfo): string {
  return `<div class="foot">
    <b>Northern Link Shuttle and Cargo</b> | ${esc(company.phone)} | ${esc(company.email)}<br/>
    National Safety Code Standard 13 · Man. Reg. 95/2008 (Commercial Vehicle Trip Inspection) · The Highway Traffic Act, C.C.S.M. c. H60
  </div>`;
}
