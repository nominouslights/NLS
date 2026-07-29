// Print CSS for the invoice PREP SHEET (NL-INV-PREP). US-Letter, navy section
// bars, bordered field grid + bordered line-item table — same letterhead
// aesthetic as the NL-WO-01 work order / NL-TM-01 manifest, rescoped to .inv.
// This is a prep sheet to KEY INTO QuickBooks, not a customer-facing invoice.
// Uses system fonts (the print tab does not load the app's web fonts).

export const INVOICE_PREP_STYLES = `
@page { size: Letter; margin: 12mm 12mm; }
.inv { font-family: "Segoe UI", system-ui, -apple-system, Roboto, sans-serif; color: #1a1a1a; }
.inv .sheet { width: 100%; max-width: 190mm; margin: 0 auto; }

.inv .head { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 4px; }
.inv .brand { font-weight: 800; font-size: 19px; letter-spacing: .2px; color: #102A43; }
.inv .brand .blue { color: #1F6FB2; }
.inv .brand-sub { font-size: 9.5px; color: #444; margin-top: 2px; line-height: 1.4; }
.inv .doc-title { text-align: right; }
.inv .doc-title .t { font-weight: 800; font-size: 17px; color: #1F6FB2; }
.inv .doc-title .s { font-size: 9px; color: #444; margin-top: 2px; }
.inv .rule { height: 2px; background: #102A43; margin: 6px 0 10px; }

.inv .warn {
  border: 1.5px solid #9C6500; background: #fbf3e0; padding: 6px 10px; margin-bottom: 10px;
  font-size: 10px; font-weight: 700; letter-spacing: .02em; color: #7a4f00;
}

.inv .sec {
  background: #102A43; color: #fff; font-weight: 700; font-size: 10.5px;
  letter-spacing: .04em; text-transform: uppercase; padding: 4px 8px; margin-top: 10px;
}

.inv .grid { display: grid; grid-template-columns: repeat(4, 1fr); border: 1px solid #333; border-top: 0; }
.inv .fld { border-right: 1px solid #bbb; border-bottom: 1px solid #bbb; padding: 4px 7px 6px; min-height: 40px; }
.inv .fld.wide { grid-column: 1 / -1; }
.inv .fld:last-child { border-right: 0; }
.inv .lbl { font-size: 7.5px; letter-spacing: .04em; text-transform: uppercase; color: #555; margin-bottom: 3px; }
.inv .val { font-size: 11px; font-weight: 600; color: #1a1a1a; min-height: 15px; }
.inv .fld.mono .val { font-family: "Cascadia Mono", Consolas, ui-monospace, monospace; font-weight: 500; }

.inv table { width: 100%; border-collapse: collapse; margin-top: -1px; }
.inv table th, .inv table td { border: 1px solid #999; padding: 4px 7px; font-size: 10px; text-align: left; vertical-align: top; }
.inv table th { background: #eef2f6; font-size: 8px; text-transform: uppercase; letter-spacing: .04em; color: #333; }
.inv table td.num { width: 26px; text-align: center; color: #666; }
.inv table td.amt, .inv table th.amt { text-align: right; white-space: nowrap; font-family: "Cascadia Mono", Consolas, ui-monospace, monospace; }
.inv table td.ref { font-family: "Cascadia Mono", Consolas, ui-monospace, monospace; color: #444; white-space: nowrap; }
.inv table tfoot td { font-weight: 700; }
.inv table tfoot td.lbl2 { text-align: right; text-transform: uppercase; font-size: 8.5px; letter-spacing: .04em; color: #333; }
.inv table tfoot tr.total td { font-size: 11px; color: #102A43; background: #eef2f6; }

.inv .foot { margin-top: 12px; font-size: 8px; color: #666; line-height: 1.5; border-top: 1px solid #ccc; padding-top: 6px; }
.inv .foot b { color: #333; }
`;
