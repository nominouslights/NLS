// Print CSS for the Driver Package — the cover sheet, and the ONE page-break
// rule that separates the bundled sub-documents.
//
// TWO COMPOSITION TRAPS THIS FILE EXISTS TO NAVIGATE:
//
// 1. `@page` CANNOT BE CLASS-SCOPED. Every document's styles file declares it at
//    top level, so a concatenated bundle emits it several times and the last one
//    wins for the whole print job. Every Northern Link document therefore
//    declares exactly `size: Letter; margin: 12mm 12mm` — including this one.
//    Never change page size or margin in a sub-document: a landscape itinerary
//    would silently re-page the manifest and both inspections too.
//
// 2. THE BREAK CLASS MUST BE UNSCOPED. `.tm .brk` is scoped under `.tm`, so it
//    can only break INSIDE the trip manifest — it cannot break BETWEEN two
//    sub-documents, because the boundary is outside every sub-document's
//    wrapper. `.nlpkg-brk` below is deliberately top-level. Do not reuse the
//    `.tm`-scoped one.
//
// The cover carries its own `page-break-after`, which is what starts the trip
// manifest on a fresh sheet without needing a fourth `.nlpkg-brk` marker.

export const DRIVER_PACKAGE_STYLES = `
@page { size: Letter; margin: 12mm 12mm; }

.nlpkg-brk { page-break-before: always; break-before: page; height: 0; }

.nlpkg { font-family: "Segoe UI", system-ui, -apple-system, Roboto, sans-serif; color: #1a1a1a; }
.nlpkg.cover { page-break-after: always; break-after: page; }
.nlpkg .sheet { width: 100%; max-width: 190mm; margin: 0 auto; }

.nlpkg .head { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 4px; }
.nlpkg .brand { font-weight: 800; font-size: 19px; letter-spacing: .2px; color: #102A43; }
.nlpkg .brand .blue { color: #1F6FB2; }
.nlpkg .brand-sub { font-size: 9.5px; color: #444; margin-top: 2px; line-height: 1.4; }
.nlpkg .doc-title { text-align: right; }
.nlpkg .doc-title .t { font-weight: 800; font-size: 17px; color: #1F6FB2; }
.nlpkg .doc-title .s { font-size: 9px; color: #444; margin-top: 2px; }
.nlpkg .rule { height: 2px; background: #102A43; margin: 6px 0 10px; }

.nlpkg .warn {
  border: 1.5px solid #333; padding: 5px 9px; margin-bottom: 8px;
  font-size: 9.5px; font-weight: 700; letter-spacing: .02em; color: #1a1a1a;
}

.nlpkg .sec {
  background: #102A43; color: #fff; font-weight: 700; font-size: 10.5px;
  letter-spacing: .04em; text-transform: uppercase; padding: 4px 8px; margin-top: 10px;
}

.nlpkg .grid { display: grid; grid-template-columns: repeat(4, 1fr); border: 1px solid #333; border-top: 0; }
.nlpkg .fld { border-right: 1px solid #bbb; border-bottom: 1px solid #bbb; padding: 5px 8px 7px; min-height: 40px; }
.nlpkg .fld.wide { grid-column: 1 / -1; }
.nlpkg .fld:last-child { border-right: 0; }
.nlpkg .lbl { font-size: 7.5px; letter-spacing: .04em; text-transform: uppercase; color: #555; margin-bottom: 3px; }
.nlpkg .val { font-size: 12px; font-weight: 600; color: #1a1a1a; min-height: 16px; }
.nlpkg .fld.mono .val { font-family: "Cascadia Mono", Consolas, ui-monospace, monospace; font-weight: 500; }

.nlpkg table { width: 100%; border-collapse: collapse; }
.nlpkg table th, .nlpkg table td { border: 1px solid #999; padding: 5px 8px; font-size: 10.5px; text-align: left; vertical-align: top; }
.nlpkg table th { background: #eef2f6; font-size: 8px; text-transform: uppercase; letter-spacing: .04em; color: #333; }
.nlpkg table td.num, .nlpkg table th.num { width: 24px; text-align: center; color: #666; }

/* Filled / blank is told by GLYPH + WORD as well as colour, never by colour
   alone — the cover is printed in black and white on a depot printer as often
   as not. The two hexes are the platform's protected status colours (teal =
   good, vermillion = problem); do not substitute an ad hoc print shade. */
.nlpkg .state { font-weight: 700; white-space: nowrap; }
.nlpkg .state.filled { color: #009E73; }
.nlpkg .state.blank { color: #D55E00; }

.nlpkg .foot { margin-top: 14px; font-size: 8.5px; color: #666; line-height: 1.6; border-top: 1px solid #ccc; padding-top: 6px; text-align: center; }
.nlpkg .foot b { color: #333; }
`;
