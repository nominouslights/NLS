// Print CSS for the TERMINUS SUMMARY (NL-TRM-01). US-Letter, navy section bars,
// bordered field grid + bordered tables — the same letterhead aesthetic as the
// accruals sheet (lib/documents/accrualsPdf/styles.ts), rescoped to .trm.
//
// Two differences from the accruals sheet, both deliberate: there is no money
// call-out treatment (this sheet carries none), and `.na` marks a figure that
// has no basis rather than one that is zero — spelled out in words on a tinted
// ground so it survives a monochrome print, never colour alone.
// Uses system fonts (the print tab does not load the app's web fonts).

export const TERMINUS_REPORT_STYLES = `
@page { size: Letter; margin: 12mm 12mm; }
.trm { font-family: "Segoe UI", system-ui, -apple-system, Roboto, sans-serif; color: #1a1a1a; }
.trm .sheet { width: 100%; max-width: 190mm; margin: 0 auto; }

.trm .head { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 4px; }
.trm .brand { font-weight: 800; font-size: 19px; letter-spacing: .2px; color: #102A43; }
.trm .brand .blue { color: #1F6FB2; }
.trm .brand-sub { font-size: 9.5px; color: #444; margin-top: 2px; line-height: 1.4; }
.trm .doc-title { text-align: right; }
.trm .doc-title .t { font-weight: 800; font-size: 17px; color: #1F6FB2; }
.trm .doc-title .s { font-size: 9px; color: #444; margin-top: 2px; }
.trm .rule { height: 2px; background: #102A43; margin: 6px 0 10px; }

.trm .warn {
  border: 1.5px solid #9C6500; background: #fbf3e0; padding: 6px 10px; margin-bottom: 10px;
  font-size: 10px; font-weight: 700; letter-spacing: .02em; color: #7a4f00;
}
/* A note is an explanation, not an alarm — lighter than the banner above it. */
.trm .note {
  border-left: 3px solid #9C6500; background: #fdf8ee; padding: 5px 9px; margin-bottom: 6px;
  font-size: 9.5px; line-height: 1.45; color: #5f4300;
}

.trm .sec {
  background: #102A43; color: #fff; font-weight: 700; font-size: 10.5px;
  letter-spacing: .04em; text-transform: uppercase; padding: 4px 8px; margin-top: 10px;
}

.trm .grid { display: grid; grid-template-columns: repeat(4, 1fr); border: 1px solid #333; border-top: 0; }
.trm .fld { border-right: 1px solid #bbb; border-bottom: 1px solid #bbb; padding: 4px 7px 6px; min-height: 40px; }
.trm .fld.wide { grid-column: 1 / -1; }
.trm .fld:last-child { border-right: 0; }
.trm .lbl { font-size: 7.5px; letter-spacing: .04em; text-transform: uppercase; color: #555; margin-bottom: 3px; }
.trm .val { font-size: 11px; font-weight: 600; color: #1a1a1a; min-height: 15px; }
.trm .fld.mono .val { font-family: "Cascadia Mono", Consolas, ui-monospace, monospace; font-weight: 500; }

.trm table { width: 100%; border-collapse: collapse; margin-top: -1px; }
.trm table th, .trm table td { border: 1px solid #999; padding: 4px 7px; font-size: 10px; text-align: left; vertical-align: top; }
.trm table th { background: #eef2f6; font-size: 8px; text-transform: uppercase; letter-spacing: .04em; color: #333; }
.trm table td.num, .trm table th.num { text-align: right; white-space: nowrap; font-family: "Cascadia Mono", Consolas, ui-monospace, monospace; }
.trm table td.ref { font-family: "Cascadia Mono", Consolas, ui-monospace, monospace; color: #444; white-space: nowrap; }
.trm table td.sub { font-size: 8.5px; color: #666; }
.trm table td.why { font-style: italic; color: #555; }
.trm table tfoot td { font-weight: 700; }
.trm table tfoot td.lbl2 { text-align: right; text-transform: uppercase; font-size: 8.5px; letter-spacing: .04em; color: #333; }
.trm table tfoot tr.total td { font-size: 11px; color: #102A43; background: #eef2f6; }

/* A measure with no basis — the words carry the meaning in grayscale, and it is
   never printed as a zero, which would read as a real (and much worse) figure. */
.trm table td.na { font-weight: 700; color: #7a4f00; background: #fbf3e0; text-align: right; white-space: nowrap; }

/* Each block (its section bar + its table) stays whole across a page break. */
.trm .blk { break-inside: avoid; page-break-inside: avoid; }

.trm .foot { margin-top: 12px; font-size: 8px; color: #666; line-height: 1.5; border-top: 1px solid #ccc; padding-top: 6px; }
.trm .foot b { color: #333; }
`;
