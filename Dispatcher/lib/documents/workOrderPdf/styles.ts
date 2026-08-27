// Print CSS for the NL-WO-01 work order. US-Letter, navy section bars, bordered
// field grid, page break between page 1 and page 2. Uses system fonts (the print
// tab does not load the app's web fonts).

export const WORK_ORDER_STYLES = `
@page { size: Letter; margin: 12mm 12mm; }
.wo { font-family: "Segoe UI", system-ui, -apple-system, Roboto, sans-serif; color: #1a1a1a; }
.wo .sheet { width: 100%; max-width: 190mm; margin: 0 auto; }
.wo .page2 { page-break-before: always; margin-top: 22px; }

.wo .head { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 4px; }
.wo .brand { font-weight: 800; font-size: 19px; letter-spacing: .2px; color: #102A43; }
.wo .brand .blue { color: #1F6FB2; }
.wo .brand-sub { font-size: 9.5px; color: #444; margin-top: 2px; line-height: 1.4; }
.wo .doc-title { text-align: right; }
.wo .doc-title .t { font-weight: 800; font-size: 17px; color: #1F6FB2; }
.wo .doc-title .s { font-size: 9px; color: #444; margin-top: 2px; }
.wo .rule { height: 2px; background: #102A43; margin: 6px 0 10px; }

.wo .sec {
  background: #102A43; color: #fff; font-weight: 700; font-size: 10.5px;
  letter-spacing: .04em; text-transform: uppercase; padding: 4px 8px; margin-top: 10px;
}
.wo .sec.gold { background: #9C6500; }

.wo .grid { display: grid; grid-template-columns: repeat(4, 1fr); border: 1px solid #333; border-top: 0; }
.wo .fld { border-right: 1px solid #bbb; border-bottom: 1px solid #bbb; padding: 4px 7px 6px; min-height: 40px; }
.wo .fld.wide { grid-column: 1 / -1; }
.wo .fld:last-child { border-right: 0; }
.wo .lbl { font-size: 7.5px; letter-spacing: .04em; text-transform: uppercase; color: #555; margin-bottom: 3px; }
.wo .val { font-size: 11px; font-weight: 600; color: #1a1a1a; min-height: 15px; }
.wo .fld.mono .val { font-family: "Cascadia Mono", Consolas, ui-monospace, monospace; font-weight: 500; }

.wo .chk { display: inline-block; font-size: 10px; margin: 2px 14px 2px 0; color: #222; white-space: nowrap; }
.wo .notice {
  font-size: 9.5px; color: #1a1a1a; line-height: 1.5; margin-top: 8px;
  border: 1px solid #9C6500; border-left-width: 4px; background: #FDF6E7; padding: 5px 8px;
}
.wo .notice b { color: #7A4F00; }
.wo .note { font-size: 9px; color: #555; padding: 5px 8px; border: 1px solid #333; border-top: 0; line-height: 1.5; }

.wo table { width: 100%; border-collapse: collapse; }
.wo table th, .wo table td { border: 1px solid #999; padding: 4px 7px; font-size: 10px; text-align: left; vertical-align: top; }
.wo table th { background: #eef2f6; font-size: 8px; text-transform: uppercase; letter-spacing: .04em; color: #333; }
.wo table td.num { width: 26px; text-align: center; color: #666; }
.wo table td.blank { height: 26px; }

.wo .sign { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-top: 10px; }
.wo .sigline { border-top: 1px solid #333; padding-top: 3px; font-size: 8.5px; color: #555; text-transform: uppercase; letter-spacing: .04em; }

.wo .foot { margin-top: 12px; font-size: 8px; color: #666; line-height: 1.5; border-top: 1px solid #ccc; padding-top: 6px; }
.wo .foot b { color: #333; }
`;
