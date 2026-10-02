// Print CSS for the En-Route Defect Report — the hand-completion sheet that
// closes the driver package. The same letterhead aesthetic as NL-PTI-01
// (navy section bars, bordered field grid, the boxed warning line), copied and
// rescoped to `.edr`, plus taller ruled rows sized for handwriting.
// Uses system fonts (the print tab does not load the app's web fonts).
//
// The `@page` rule is deliberately IDENTICAL to every sibling document's
// (Letter, 12mm): the driver package concatenates several of these stylesheets
// into one print tab, and `@page` cannot be class-scoped — a document that
// changed size or margin would silently re-page the whole bundle.

export const DEFECT_REPORT_STYLES = `
@page { size: Letter; margin: 12mm 12mm; }
.edr { font-family: "Segoe UI", system-ui, -apple-system, Roboto, sans-serif; color: #1a1a1a; }
.edr .sheet { width: 100%; max-width: 190mm; margin: 0 auto; }

.edr .head { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 4px; }
.edr .brand { font-weight: 800; font-size: 19px; letter-spacing: .2px; color: #102A43; }
.edr .brand .blue { color: #1F6FB2; }
.edr .brand-sub { font-size: 9.5px; color: #444; margin-top: 2px; line-height: 1.4; }
.edr .doc-title { text-align: right; }
.edr .doc-title .t { font-weight: 800; font-size: 15px; color: #1F6FB2; }
.edr .doc-title .s { font-size: 9px; color: #444; margin-top: 2px; }
.edr .rule { height: 2px; background: #102A43; margin: 6px 0 10px; }

.edr .warn {
  border: 1.5px solid #333; padding: 5px 9px; margin-bottom: 8px;
  font-size: 9.5px; font-weight: 700; letter-spacing: .02em; color: #1a1a1a; line-height: 1.5;
}

.edr .sec {
  background: #102A43; color: #fff; font-weight: 700; font-size: 10.5px;
  letter-spacing: .04em; text-transform: uppercase; padding: 4px 8px; margin-top: 10px;
}

.edr .grid { display: grid; grid-template-columns: repeat(4, 1fr); border: 1px solid #333; border-top: 0; }
.edr .fld { border-right: 1px solid #bbb; border-bottom: 1px solid #bbb; padding: 4px 7px 6px; min-height: 36px; }
.edr .fld.wide { grid-column: 1 / -1; }
.edr .fld:last-child { border-right: 0; }
.edr .lbl { font-size: 7.5px; letter-spacing: .04em; text-transform: uppercase; color: #555; margin-bottom: 3px; }
.edr .val { font-size: 11px; font-weight: 600; color: #1a1a1a; min-height: 15px; }
.edr .fld.mono .val { font-family: "Cascadia Mono", Consolas, ui-monospace, monospace; font-weight: 500; }

.edr .none {
  border: 1.5px solid #333; border-top: 0; padding: 8px 10px;
  font-size: 13px; font-weight: 700; color: #1a1a1a;
}
.edr .note { font-size: 9px; color: #555; padding: 5px 8px; border: 1px solid #333; border-top: 0; line-height: 1.5; }

.edr table { width: 100%; border-collapse: collapse; }
.edr table th, .edr table td { border: 1px solid #999; padding: 3px 6px; font-size: 9px; text-align: left; vertical-align: top; }
.edr table th { background: #eef2f6; font-size: 7.5px; text-transform: uppercase; letter-spacing: .04em; color: #333; }
.edr table td { height: 40px; }
.edr table td.time, .edr table th.time { width: 11%; }
.edr table td.loc, .edr table th.loc { width: 17%; }
.edr table td.sys, .edr table th.sys { width: 15%; }
.edr table td.sev, .edr table th.sev { width: 12%; white-space: nowrap; }
.edr table td.sev { font-size: 10px; line-height: 1.7; }
.edr table td.rep, .edr table th.rep { width: 17%; }

.edr .certbox { border: 1px solid #333; border-top: 0; padding: 6px 9px 9px; font-size: 9.5px; line-height: 1.55; }
.edr .sign { display: grid; grid-template-columns: 1.6fr 1fr; gap: 20px; margin-top: 18px; }
.edr .sigval { min-height: 18px; padding-bottom: 2px; }
.edr .sigline { border-top: 1px solid #333; padding-top: 3px; font-size: 8.5px; color: #555; text-transform: uppercase; letter-spacing: .04em; }

.edr .foot { margin-top: 12px; font-size: 8px; color: #666; line-height: 1.6; border-top: 1px solid #ccc; padding-top: 6px; text-align: center; }
.edr .foot b { color: #333; }
`;
