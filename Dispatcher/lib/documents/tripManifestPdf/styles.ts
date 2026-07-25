// Print CSS for the NL-TM-01 Trip Manifest & Vehicle Inspection Report.
// US-Letter, navy section bars, bordered field grid — same letterhead aesthetic
// as the NL-WO-01 work order (styles copied and rescoped to .tm), plus checklist
// tables, lined issue rows, and hard page breaks for the 4-page assembly.
// Uses system fonts (the print tab does not load the app's web fonts).

export const TRIP_MANIFEST_STYLES = `
@page { size: Letter; margin: 12mm 12mm; }
.tm { font-family: "Segoe UI", system-ui, -apple-system, Roboto, sans-serif; color: #1a1a1a; }
.tm .sheet { width: 100%; max-width: 190mm; margin: 0 auto; }
.tm .brk { page-break-before: always; margin-top: 22px; }

.tm .head { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 4px; }
.tm .brand { font-weight: 800; font-size: 19px; letter-spacing: .2px; color: #102A43; }
.tm .brand .blue { color: #1F6FB2; }
.tm .brand-sub { font-size: 9.5px; color: #444; margin-top: 2px; line-height: 1.4; }
.tm .doc-title { text-align: right; }
.tm .doc-title .t { font-weight: 800; font-size: 17px; color: #1F6FB2; }
.tm .doc-title .s { font-size: 9px; color: #444; margin-top: 2px; }
.tm .rule { height: 2px; background: #102A43; margin: 6px 0 10px; }

.tm .warn {
  border: 1.5px solid #333; padding: 5px 9px; margin-bottom: 8px;
  font-size: 9.5px; font-weight: 700; letter-spacing: .02em; color: #1a1a1a;
}
.tm .prov {
  background: #eef2f6; border: 1px solid #bbb; padding: 4px 9px; margin-bottom: 8px;
  font-size: 9px; color: #333;
}
.tm .prov b { color: #102A43; }

.tm .sec {
  background: #102A43; color: #fff; font-weight: 700; font-size: 10.5px;
  letter-spacing: .04em; text-transform: uppercase; padding: 4px 8px; margin-top: 10px;
}
.tm .subsec {
  background: #eef2f6; border: 1px solid #333; border-top: 0; padding: 3px 8px;
  font-weight: 700; font-size: 8.5px; letter-spacing: .05em; text-transform: uppercase; color: #102A43;
}

.tm .grid { display: grid; grid-template-columns: repeat(4, 1fr); border: 1px solid #333; border-top: 0; }
.tm .fld { border-right: 1px solid #bbb; border-bottom: 1px solid #bbb; padding: 4px 7px 6px; min-height: 36px; }
.tm .fld.wide { grid-column: 1 / -1; }
.tm .fld:last-child { border-right: 0; }
.tm .lbl { font-size: 7.5px; letter-spacing: .04em; text-transform: uppercase; color: #555; margin-bottom: 3px; }
.tm .val { font-size: 11px; font-weight: 600; color: #1a1a1a; min-height: 15px; }
.tm .fld.mono .val { font-family: "Cascadia Mono", Consolas, ui-monospace, monospace; font-weight: 500; }

.tm .chk { display: inline-block; font-size: 10px; margin: 2px 12px 2px 0; color: #222; white-space: nowrap; }
.tm .note { font-size: 9px; color: #555; padding: 5px 8px; border: 1px solid #333; border-top: 0; line-height: 1.5; }

.tm table { width: 100%; border-collapse: collapse; }
.tm table th, .tm table td { border: 1px solid #999; padding: 3px 7px; font-size: 9.5px; text-align: left; vertical-align: top; }
.tm table th { background: #eef2f6; font-size: 8px; text-transform: uppercase; letter-spacing: .04em; color: #333; }
.tm table td.num, .tm table th.num { width: 22px; text-align: center; color: #666; }
.tm table td.ck, .tm table th.ck { width: 44px; text-align: center; white-space: nowrap; }
.tm table td.blank { height: 22px; }
.tm td .sev { font-size: 7.5px; font-weight: 700; letter-spacing: .04em; text-transform: uppercase; color: #333; }

.tm .lines { border: 1px solid #333; border-top: 0; padding: 4px 8px 8px; }
.tm .lines .nochk { font-size: 10px; color: #222; padding: 3px 0 5px; }
.tm .line { border-bottom: 1px solid #999; min-height: 19px; font-size: 10px; color: #1a1a1a; padding: 3px 2px 1px; }

.tm .att { font-size: 9.5px; color: #1a1a1a; line-height: 1.55; padding: 2px 0; }
.tm .certintro { font-size: 9.5px; font-weight: 700; color: #1a1a1a; padding: 5px 0 2px; }
.tm .certbox { border: 1px solid #333; border-top: 0; padding: 5px 9px 8px; }

.tm .sign { display: grid; grid-template-columns: 1.6fr 1fr; gap: 20px; margin-top: 14px; }
.tm .sigval { font-size: 12px; font-weight: 600; min-height: 18px; padding-bottom: 2px; }
.tm .sigval .via { font-size: 8.5px; font-weight: 400; color: #555; }
.tm .sigline { border-top: 1px solid #333; padding-top: 3px; font-size: 8.5px; color: #555; text-transform: uppercase; letter-spacing: .04em; }

.tm .foot { margin-top: 12px; font-size: 8px; color: #666; line-height: 1.6; border-top: 1px solid #ccc; padding-top: 6px; text-align: center; }
.tm .foot b { color: #333; }
`;
