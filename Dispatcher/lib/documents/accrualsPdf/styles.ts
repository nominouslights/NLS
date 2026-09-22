// Print CSS for the ACCRUALS REPORT (NL-ACC-01). US-Letter, navy section bars,
// bordered field grid + bordered tables — the NL-INV-PREP letterhead aesthetic
// (lib/documents/invoicePdf/styles.ts), rescoped to .acc with four additions:
// .blk keeps each bucket whole across a page break; the flag/miss cells carry
// the half-rate / manual-line / amount-unavailable call-outs as text on a
// tinted ground; .hl* is the two-figure headline block; and .sub/.ind give the
// summary its section subtotals and nested bucket rows (the sheet prints
// monochrome-safe — never colour alone, and never weight alone either).
// Uses system fonts (the print tab does not load the app's web fonts).

export const ACCRUALS_REPORT_STYLES = `
@page { size: Letter; margin: 12mm 12mm; }
.acc { font-family: "Segoe UI", system-ui, -apple-system, Roboto, sans-serif; color: #1a1a1a; }
.acc .sheet { width: 100%; max-width: 190mm; margin: 0 auto; }

.acc .head { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 4px; }
.acc .brand { font-weight: 800; font-size: 19px; letter-spacing: .2px; color: #102A43; }
.acc .brand .blue { color: #1F6FB2; }
.acc .brand-sub { font-size: 9.5px; color: #444; margin-top: 2px; line-height: 1.4; }
.acc .doc-title { text-align: right; }
.acc .doc-title .t { font-weight: 800; font-size: 17px; color: #1F6FB2; }
.acc .doc-title .s { font-size: 9px; color: #444; margin-top: 2px; }
.acc .rule { height: 2px; background: #102A43; margin: 6px 0 10px; }

.acc .warn {
  border: 1.5px solid #9C6500; background: #fbf3e0; padding: 6px 10px; margin-bottom: 10px;
  font-size: 10px; font-weight: 700; letter-spacing: .02em; color: #7a4f00;
}

.acc .sec {
  background: #102A43; color: #fff; font-weight: 700; font-size: 10.5px;
  letter-spacing: .04em; text-transform: uppercase; padding: 4px 8px; margin-top: 10px;
}

.acc .grid { display: grid; grid-template-columns: repeat(4, 1fr); border: 1px solid #333; border-top: 0; }
.acc .fld { border-right: 1px solid #bbb; border-bottom: 1px solid #bbb; padding: 4px 7px 6px; min-height: 40px; }
.acc .fld.wide { grid-column: 1 / -1; }
.acc .fld:last-child { border-right: 0; }
.acc .lbl { font-size: 7.5px; letter-spacing: .04em; text-transform: uppercase; color: #555; margin-bottom: 3px; }
.acc .val { font-size: 11px; font-weight: 600; color: #1a1a1a; min-height: 15px; }
.acc .fld.mono .val { font-family: "Cascadia Mono", Consolas, ui-monospace, monospace; font-weight: 500; }

.acc table { width: 100%; border-collapse: collapse; margin-top: -1px; }
.acc table th, .acc table td { border: 1px solid #999; padding: 4px 7px; font-size: 10px; text-align: left; vertical-align: top; }
.acc table th { background: #eef2f6; font-size: 8px; text-transform: uppercase; letter-spacing: .04em; color: #333; }
.acc table td.amt, .acc table th.amt { text-align: right; white-space: nowrap; font-family: "Cascadia Mono", Consolas, ui-monospace, monospace; }
.acc table td.ref { font-family: "Cascadia Mono", Consolas, ui-monospace, monospace; color: #444; white-space: nowrap; }
.acc table td.note { font-style: italic; color: #555; }
.acc table tfoot td { font-weight: 700; }
.acc table tfoot td.lbl2 { text-align: right; text-transform: uppercase; font-size: 8.5px; letter-spacing: .04em; color: #333; }
.acc table tfoot tr.total td { font-size: 11px; color: #102A43; background: #eef2f6; }

/* Section subtotal rows in the summary carry weight + a tint; the bucket rows
   nested under them are indented. Printed monochrome the indent is what still
   reads as hierarchy, so it is not decoration. */
.acc table tbody tr.sub td { font-weight: 700; background: #f4f7fa; }
.acc table tbody td.ind { padding-left: 20px; color: #333; }

/* The two headline figures, above Summary — upcoming expenses then monies owed.
   Boxes rather than a table: they are the answer, the tables are the evidence.
   Amounts keep their EST. marking, so an estimate never reads as settled money. */
.acc .hl-row { display: flex; gap: 8px; border: 1px solid #333; border-top: 0; padding: 8px; }
.acc .hl { flex: 1 1 0; border: 1px solid #bbb; background: #f7f9fb; padding: 6px 9px 7px; }
.acc .hl-lbl { font-size: 8px; letter-spacing: .05em; text-transform: uppercase; color: #555; }
.acc .hl-amt {
  font-family: "Cascadia Mono", Consolas, ui-monospace, monospace;
  font-weight: 700; font-size: 16px; color: #102A43; margin-top: 3px;
}
.acc .hl-det { font-size: 9px; color: #555; margin-top: 2px; }

/* Amount call-outs — spelled out in words on a tinted ground: a half-rate
   (unpaired) estimate or a manual line is an expected gap (gold tint), a
   missing invoice is a data problem (vermillion tint). The words carry the
   meaning when printed in grayscale — .flag now sits BESIDE a real figure as
   often as it replaces one. */
.acc table td.flag { font-weight: 700; color: #7a4f00; background: #fbf3e0; text-align: right; white-space: nowrap; }
.acc table td.miss { font-weight: 700; color: #8a2c00; background: #fdeee3; text-align: right; white-space: nowrap; }

/* Each bucket (header bar + its table) stays whole across a page break: a
   detail table split from its own section bar is worse than a short page. */
.acc .blk { break-inside: avoid; page-break-inside: avoid; }

.acc .foot { margin-top: 12px; font-size: 8px; color: #666; line-height: 1.5; border-top: 1px solid #ccc; padding-top: 6px; }
.acc .foot b { color: #333; }
`;
