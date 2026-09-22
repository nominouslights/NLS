// Print CSS for Form NL-PTI-01, the Daily Pre-Trip / Post-Trip Inspection.
// US-Letter, navy section bars, bordered field grid — the same letterhead
// aesthetic as NL-WO-01 and NL-TM-01, copied and rescoped to `.pti`, plus the
// tri-state checklist columns (OK / Defect / N/A), the scope column and the
// ruled hand-completion cells of the defect log.
// Uses system fonts (the print tab does not load the app's web fonts).
//
// The `@page` rule is deliberately IDENTICAL to every sibling document's
// (Letter, 12mm): the driver package concatenates several of these stylesheets
// into one print tab, and `@page` cannot be class-scoped — a document that
// changed size or margin would silently re-page the whole bundle.

export const INSPECTION_STYLES = `
@page { size: Letter; margin: 12mm 12mm; }
.pti { font-family: "Segoe UI", system-ui, -apple-system, Roboto, sans-serif; color: #1a1a1a; }
.pti .sheet { width: 100%; max-width: 190mm; margin: 0 auto; }

.pti .head { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 4px; }
.pti .brand { font-weight: 800; font-size: 19px; letter-spacing: .2px; color: #102A43; }
.pti .brand .blue { color: #1F6FB2; }
.pti .brand-sub { font-size: 9.5px; color: #444; margin-top: 2px; line-height: 1.4; }
.pti .doc-title { text-align: right; }
.pti .doc-title .t { font-weight: 800; font-size: 15px; color: #1F6FB2; }
.pti .doc-title .s { font-size: 9px; color: #444; margin-top: 2px; }
.pti .rule { height: 2px; background: #102A43; margin: 6px 0 10px; }

.pti .warn {
  border: 1.5px solid #333; padding: 5px 9px; margin-bottom: 8px;
  font-size: 9.5px; font-weight: 700; letter-spacing: .02em; color: #1a1a1a;
}
.pti .prov {
  background: #eef2f6; border: 1px solid #bbb; padding: 4px 9px; margin-bottom: 8px;
  font-size: 9px; color: #333;
}
.pti .prov b { color: #102A43; }

.pti .sec {
  background: #102A43; color: #fff; font-weight: 700; font-size: 10.5px;
  letter-spacing: .04em; text-transform: uppercase; padding: 4px 8px; margin-top: 10px;
}
.pti .subsec {
  background: #eef2f6; border: 1px solid #333; border-top: 0; padding: 3px 8px;
  font-weight: 700; font-size: 8.5px; letter-spacing: .05em; text-transform: uppercase; color: #102A43;
}

.pti .grid { display: grid; grid-template-columns: repeat(4, 1fr); border: 1px solid #333; border-top: 0; }
.pti .fld { border-right: 1px solid #bbb; border-bottom: 1px solid #bbb; padding: 4px 7px 6px; min-height: 36px; }
.pti .fld.wide { grid-column: 1 / -1; }
.pti .fld:last-child { border-right: 0; }
.pti .lbl { font-size: 7.5px; letter-spacing: .04em; text-transform: uppercase; color: #555; margin-bottom: 3px; }
.pti .val { font-size: 11px; font-weight: 600; color: #1a1a1a; min-height: 15px; }
.pti .fld.mono .val { font-family: "Cascadia Mono", Consolas, ui-monospace, monospace; font-weight: 500; }

.pti .chk { display: inline-block; font-size: 10px; margin: 2px 12px 2px 0; color: #222; white-space: nowrap; }
.pti .note { font-size: 9px; color: #555; padding: 5px 8px; border: 1px solid #333; border-top: 0; line-height: 1.5; }
.pti .legend {
  font-size: 8px; color: #444; padding: 4px 8px; border: 1px solid #333; border-top: 0;
  background: #f7f9fb; line-height: 1.45;
}
.pti .legend b { color: #102A43; }

.pti table { width: 100%; border-collapse: collapse; }
.pti table th, .pti table td { border: 1px solid #999; padding: 3px 6px; font-size: 9px; text-align: left; vertical-align: top; }
.pti table th { background: #eef2f6; font-size: 7.5px; text-transform: uppercase; letter-spacing: .04em; color: #333; }
.pti table td.item { width: 26%; font-weight: 600; }
.pti table td.ck, .pti table th.ck { width: 26px; text-align: center; white-space: nowrap; font-size: 11px; }
.pti table td.notes, .pti table th.notes { width: 20%; }
.pti table td.scope, .pti table th.scope { width: 40px; text-align: center; white-space: nowrap; }
.pti table td.scope span {
  font-family: "Cascadia Mono", Consolas, ui-monospace, monospace;
  font-size: 7.5px; font-weight: 700; color: #7A4F00;
}
.pti table td.blank { height: 21px; }
.pti table td.sev { width: 58px; white-space: nowrap; font-weight: 600; }

.pti .certintro { font-size: 9.5px; font-weight: 700; color: #1a1a1a; padding: 5px 0 2px; }
.pti .certbox { border: 1px solid #333; border-top: 0; padding: 6px 9px 9px; font-size: 9.5px; line-height: 1.55; }

.pti .sign { display: grid; grid-template-columns: 1.6fr 1fr; gap: 20px; margin-top: 14px; }
.pti .sigval { font-size: 12px; font-weight: 600; min-height: 18px; padding-bottom: 2px; }
.pti .sigline { border-top: 1px solid #333; padding-top: 3px; font-size: 8.5px; color: #555; text-transform: uppercase; letter-spacing: .04em; }

.pti .rules { border: 1px solid #333; border-top: 0; padding: 6px 9px; font-size: 8.5px; color: #1a1a1a; line-height: 1.6; }
.pti .rules b { color: #102A43; }
.pti .rules ul { margin: 0; padding-left: 15px; }

.pti .foot { margin-top: 12px; font-size: 8px; color: #666; line-height: 1.6; border-top: 1px solid #ccc; padding-top: 6px; text-align: center; }
.pti .foot b { color: #333; }
`;
