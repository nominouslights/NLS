// Print CSS for the BOOKING PASS (NL-BP-01). US-Letter, one traveller per page
// so each pass is a hand-able sheet — the NL-ACC-01 letterhead aesthetic
// (lib/documents/accrualsPdf/styles.ts) rescoped to .bp, with the reference
// set large in mono as the thing a driver reads at boarding. Status prints as
// words on a tinted ground (monochrome-safe — never colour alone).
// Uses system fonts (the print tab does not load the app's web fonts).

export const BOOKING_PASS_STYLES = `
@page { size: Letter; margin: 14mm 14mm; }
.bp { font-family: "Segoe UI", system-ui, -apple-system, Roboto, sans-serif; color: #1a1a1a; }
.bp .sheet { width: 100%; max-width: 186mm; margin: 0 auto; }

/* One pass per page — the last page carries no trailing blank. */
.bp .pass { page-break-after: always; break-after: page; padding-bottom: 8mm; }
.bp .pass:last-child { page-break-after: auto; break-after: auto; }

.bp .head { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 4px; }
.bp .brand { font-weight: 800; font-size: 19px; letter-spacing: .2px; color: #102A43; }
.bp .brand .blue { color: #1F6FB2; }
.bp .brand-sub { font-size: 9.5px; color: #444; margin-top: 2px; line-height: 1.4; }
.bp .doc-title { text-align: right; }
.bp .doc-title .t { font-weight: 800; font-size: 17px; color: #1F6FB2; }
.bp .doc-title .s { font-size: 9px; color: #444; margin-top: 2px; }
.bp .rule { height: 2px; background: #102A43; margin: 6px 0 12px; }

/* The reference block — what gets read aloud and matched at boarding. */
.bp .refblock {
  display: flex; justify-content: space-between; align-items: center; gap: 16px;
  border: 2px solid #102A43; border-radius: 6px; padding: 12px 16px; margin-bottom: 12px;
}
.bp .refblock .lbl { font-size: 8px; letter-spacing: .08em; text-transform: uppercase; color: #555; margin-bottom: 4px; }
.bp .refblock .ref {
  font-family: "Cascadia Mono", Consolas, ui-monospace, monospace;
  font-size: 34px; font-weight: 700; letter-spacing: .06em; color: #102A43; line-height: 1;
}
.bp .refblock .seat { text-align: right; }
.bp .refblock .seat .n { font-size: 22px; font-weight: 800; color: #1F6FB2; line-height: 1; }
.bp .refblock .seat .lbl { margin-bottom: 4px; }

.bp .traveller { font-size: 20px; font-weight: 800; color: #1a1a1a; margin: 6px 0 2px; }
.bp .traveller-sub { font-size: 10px; color: #444; margin-bottom: 10px; }

.bp .sec {
  background: #102A43; color: #fff; font-weight: 700; font-size: 10.5px;
  letter-spacing: .04em; text-transform: uppercase; padding: 4px 8px; margin-top: 10px;
}
.bp .grid { display: grid; grid-template-columns: repeat(4, 1fr); border: 1px solid #333; border-top: 0; }
.bp .fld { border-right: 1px solid #bbb; border-bottom: 1px solid #bbb; padding: 4px 7px 6px; min-height: 40px; }
.bp .fld.wide { grid-column: 1 / -1; }
.bp .fld:last-child { border-right: 0; }
.bp .lbl { font-size: 7.5px; letter-spacing: .04em; text-transform: uppercase; color: #555; margin-bottom: 3px; }
.bp .val { font-size: 11.5px; font-weight: 600; color: #1a1a1a; min-height: 15px; }
.bp .fld.mono .val { font-family: "Cascadia Mono", Consolas, ui-monospace, monospace; font-weight: 500; }

/* Payment line — words on a tint. Paid = teal tint, Unpaid = gold tint; the
   text carries the meaning in grayscale. */
.bp .pay { margin-top: 10px; padding: 6px 10px; font-size: 10.5px; font-weight: 700; letter-spacing: .02em; border: 1.5px solid; }
.bp .pay.paid { color: #00543d; background: #e3f4ee; border-color: #009E73; }
.bp .pay.unpaid { color: #7a4f00; background: #fbf3e0; border-color: #9C6500; }

.bp .board {
  margin-top: 14px; border: 1.5px dashed #102A43; padding: 10px 12px;
  font-size: 12px; font-weight: 700; color: #102A43; text-align: center; letter-spacing: .04em;
}
.bp .notes { margin-top: 10px; font-size: 10px; color: #333; line-height: 1.5; }
.bp .notes b { color: #102A43; }

.bp .foot { margin-top: 14px; font-size: 8px; color: #666; line-height: 1.5; border-top: 1px solid #ccc; padding-top: 6px; }
.bp .foot b { color: #333; }
`;
