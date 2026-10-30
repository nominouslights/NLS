// Print CSS for the Trip Itinerary sheet. Same letterhead aesthetic as NL-WO-01
// / NL-TM-01 / NL-PTI-01, copied and rescoped to `.itin`, plus the stop table's
// ruled hand-completion cells and the freight block.
// Uses system fonts (the print tab does not load the app's web fonts).
//
// PORTRAIT US-LETTER, 12mm, IDENTICAL to every sibling document. `@page` cannot
// be class-scoped, so when the driver package concatenates these stylesheets the
// last `@page` wins for the WHOLE bundle — a landscape itinerary would silently
// re-page the manifest and both inspections too.

export const ITINERARY_STYLES = `
@page { size: Letter; margin: 12mm 12mm; }
.itin { font-family: "Segoe UI", system-ui, -apple-system, Roboto, sans-serif; color: #1a1a1a; }
.itin .sheet { width: 100%; max-width: 190mm; margin: 0 auto; }

.itin .head { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 4px; }
.itin .brand { font-weight: 800; font-size: 19px; letter-spacing: .2px; color: #102A43; }
.itin .brand .blue { color: #1F6FB2; }
.itin .brand-sub { font-size: 9.5px; color: #444; margin-top: 2px; line-height: 1.4; }
.itin .doc-title { text-align: right; }
.itin .doc-title .t { font-weight: 800; font-size: 17px; color: #1F6FB2; }
.itin .doc-title .s { font-size: 9px; color: #444; margin-top: 2px; }
.itin .rule { height: 2px; background: #102A43; margin: 6px 0 10px; }

.itin .warn {
  border: 1.5px solid #333; padding: 5px 9px; margin-bottom: 8px;
  font-size: 9.5px; font-weight: 700; letter-spacing: .02em; color: #1a1a1a;
}

.itin .sec {
  background: #102A43; color: #fff; font-weight: 700; font-size: 10.5px;
  letter-spacing: .04em; text-transform: uppercase; padding: 4px 8px; margin-top: 10px;
}

.itin .grid { display: grid; grid-template-columns: repeat(4, 1fr); border: 1px solid #333; border-top: 0; }
.itin .fld { border-right: 1px solid #bbb; border-bottom: 1px solid #bbb; padding: 4px 7px 6px; min-height: 36px; }
.itin .fld.wide { grid-column: 1 / -1; }
.itin .fld:last-child { border-right: 0; }
.itin .lbl { font-size: 7.5px; letter-spacing: .04em; text-transform: uppercase; color: #555; margin-bottom: 3px; }
.itin .val { font-size: 11px; font-weight: 600; color: #1a1a1a; min-height: 15px; }
.itin .fld.mono .val { font-family: "Cascadia Mono", Consolas, ui-monospace, monospace; font-weight: 500; }

.itin .chk { display: inline-block; font-size: 10px; margin: 2px 12px 2px 0; color: #222; white-space: nowrap; }
.itin .note { font-size: 9px; color: #555; padding: 5px 8px; border: 1px solid #333; border-top: 0; line-height: 1.5; }
.itin .note b { color: #102A43; }

.itin table { width: 100%; border-collapse: collapse; }
.itin table th, .itin table td { border: 1px solid #999; padding: 3px 7px; font-size: 9.5px; text-align: left; vertical-align: top; }
.itin table th { background: #eef2f6; font-size: 8px; text-transform: uppercase; letter-spacing: .04em; color: #333; }
.itin table td.num, .itin table th.num { width: 22px; text-align: center; color: #666; }
.itin table td.time, .itin table th.time {
  width: 54px; text-align: center; white-space: nowrap;
  font-family: "Cascadia Mono", Consolas, ui-monospace, monospace;
}
.itin table td.hand, .itin table th.hand { width: 58px; }
.itin table td.ck, .itin table th.ck { width: 44px; text-align: center; white-space: nowrap; }
.itin table td.blank { height: 24px; }

.itin .foot { margin-top: 12px; font-size: 8px; color: #666; line-height: 1.6; border-top: 1px solid #ccc; padding-top: 6px; text-align: center; }
.itin .foot b { color: #333; }
`;
