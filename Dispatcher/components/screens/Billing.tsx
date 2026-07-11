"use client";

import { fonts, rowSurface, statusMeta, type StatusKind } from "@/lib/theme";
import { invoices } from "@/lib/data";
import { PageHeader } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";

function qboKind(qbo: string): StatusKind {
  if (qbo === "Matched") return "ontime";
  if (qbo === "Unmatched payment") return "over";
  return "off";
}

export default function Billing({ invoiceSel, setInvoiceSel }: { invoiceSel: number; setInvoiceSel: (i: number) => void }) {
  const v = invoices[invoiceSel];
  const overdue = v.status === "Overdue";
  const unmatched = v.qbo === "Unmatched payment";

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
          <PageHeader eyebrow="Business · Invoicing & QBO reconciliation" title="Billing & Invoicing" />
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: 8,
              padding: "7px 13px",
              borderRadius: 9,
              background: "rgba(59,141,212,.09)",
              border: "1px solid rgba(59,141,212,.3)",
            }}
          >
            <span style={{ width: 8, height: 8, borderRadius: "50%", background: "#14B88A" }} />
            <div style={{ lineHeight: 1.3 }}>
              <div style={{ fontFamily: fonts.body, fontSize: 11.5, fontWeight: 600, color: "#7EC8F0" }}>
                QuickBooks Online · read-only book of record
              </div>
              <div style={{ fontFamily: fonts.mono, fontSize: 9.5, color: "#6B8099" }}>Last read 4m ago · no write path</div>
            </div>
          </div>
        </div>
      </div>

      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "44% 1fr", borderTop: "1px solid #1E3350" }}>
        <div style={{ minHeight: 0, overflowY: "auto", padding: "16px 18px", borderRight: "1px solid #1E3350" }}>
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "92px 1fr 100px 120px",
              gap: 11,
              padding: "0 13px 9px",
              fontFamily: fonts.semiCondensed,
              fontSize: 9.5,
              letterSpacing: ".12em",
              textTransform: "uppercase",
              color: "#4d688a",
            }}
          >
            <div>Invoice</div>
            <div>Client / PO</div>
            <div>Amount</div>
            <div>Status</div>
          </div>
          {invoices.map((row, i) => {
            const active = i === invoiceSel;
            const qm = statusMeta(qboKind(row.qbo));
            return (
              <div
                key={row.id}
                onClick={() => setInvoiceSel(i)}
                style={{
                  display: "grid",
                  gridTemplateColumns: "92px 1fr 100px 120px",
                  gap: 11,
                  alignItems: "center",
                  padding: "11px 13px",
                  marginBottom: 5,
                  ...rowSurface(active, "#3B8DD4"),
                }}
              >
                <div>
                  <div style={{ fontFamily: fonts.mono, fontSize: 11.5, color: "#7EC8F0" }}>{row.id}</div>
                  <div
                    style={{
                      fontFamily: fonts.semiCondensed,
                      fontSize: 10,
                      letterSpacing: ".05em",
                      textTransform: "uppercase",
                      color: qm.t,
                    }}
                  >
                    {row.qbo}
                  </div>
                </div>
                <div style={{ minWidth: 0 }}>
                  <div
                    style={{
                      fontFamily: fonts.body,
                      fontSize: 12.5,
                      fontWeight: 600,
                      color: "#E8EEF5",
                      whiteSpace: "nowrap",
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                    }}
                  >
                    {row.client}
                  </div>
                  <div style={{ fontFamily: fonts.mono, fontSize: 10.5, color: "#6B8099" }}>{row.po}</div>
                </div>
                <div style={{ fontFamily: fonts.mono, fontSize: 12.5, color: "#E8EEF5", fontWeight: 500 }}>{row.amt}</div>
                <div>
                  <StatusChip kind={row.sk} label={row.status} />
                </div>
              </div>
            );
          })}
        </div>

        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: "#0C1A2C" }}>
          <div className="detailfade" key={v.id}>
            <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 14 }}>
              <StatusChip kind={v.sk} label={v.status} />
              <span style={{ fontFamily: fonts.mono, fontSize: 14, color: "#7EC8F0" }}>{v.id}</span>
              <span
                style={{
                  marginLeft: "auto",
                  fontFamily: fonts.condensed,
                  fontWeight: 700,
                  fontSize: 26,
                  color: "#F2F6FB",
                  fontVariantNumeric: "tabular-nums",
                }}
              >
                {v.amt}
              </span>
            </div>

            {overdue && (
              <div
                style={{
                  padding: "11px 14px",
                  background: "rgba(213,94,0,.1)",
                  border: "1px solid rgba(213,94,0,.4)",
                  borderRadius: 9,
                  marginBottom: 14,
                  fontFamily: fonts.body,
                  fontSize: 12.5,
                  color: "#f0803f",
                  fontWeight: 600,
                }}
              >
                ▲ Overdue {v.age} · AR aging 60+ · follow-up recommended
              </div>
            )}
            {unmatched && (
              <div
                style={{
                  padding: "11px 14px",
                  background: "rgba(225,176,0,.09)",
                  border: "1px solid rgba(225,176,0,.3)",
                  borderRadius: 9,
                  marginBottom: 14,
                  fontFamily: fonts.body,
                  fontSize: 12.5,
                  color: "#ecc94b",
                  fontWeight: 600,
                }}
              >
                ◐ Unmatched payment in QBO — reconcile manually (read-only)
              </div>
            )}

            <div style={{ padding: "16px 18px", background: "#0F1E33", border: "1px solid #1E3350", borderRadius: 11, marginBottom: 12 }}>
              <div
                style={{
                  fontFamily: fonts.semiCondensed,
                  fontSize: 9.5,
                  letterSpacing: ".14em",
                  textTransform: "uppercase",
                  color: "#8fa6c0",
                  marginBottom: 12,
                }}
              >
                Invoice builder · line items from completed trips
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", padding: "9px 0", borderBottom: "1px solid #152941", fontFamily: fonts.body, fontSize: 12.5 }}>
                <span style={{ color: "#c2d0e0" }}>Corridor run · Thompson → Lynn Lake · 198 km</span>
                <span style={{ fontFamily: fonts.mono, color: "#E8EEF5" }}>$3,660.00</span>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", padding: "9px 0", borderBottom: "1px solid #152941", fontFamily: fonts.body, fontSize: 12.5 }}>
                <span style={{ color: "#c2d0e0" }}>GST (5%) · no PST on transportation</span>
                <span style={{ fontFamily: fonts.mono, color: "#E8EEF5" }}>$182.00</span>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", padding: "11px 0 0", fontFamily: fonts.body, fontSize: 13, fontWeight: 700 }}>
                <span style={{ color: "#E8EEF5" }}>Total (CAD)</span>
                <span style={{ fontFamily: fonts.mono, color: "#38d3a6" }}>{v.amt}</span>
              </div>
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12, marginBottom: 12 }}>
              <div style={{ padding: "13px 15px", background: "#0F1E33", border: "1px solid #1E3350", borderRadius: 10 }}>
                <div style={{ fontFamily: fonts.body, fontSize: 11, color: "#6B8099", marginBottom: 4 }}>PO match</div>
                <div style={{ fontFamily: fonts.mono, fontSize: 12, color: "#c2d0e0" }}>{v.po}</div>
              </div>
              <div style={{ padding: "13px 15px", background: "#0F1E33", border: "1px solid #1E3350", borderRadius: 10 }}>
                <div style={{ fontFamily: fonts.body, fontSize: 11, color: "#6B8099", marginBottom: 4 }}>Budget code (ZBB)</div>
                <div style={{ fontFamily: fonts.mono, fontSize: 12, color: "#c2d0e0" }}>{v.code}</div>
              </div>
              <div style={{ padding: "13px 15px", background: "#0F1E33", border: "1px solid #1E3350", borderRadius: 10 }}>
                <div style={{ fontFamily: fonts.body, fontSize: 11, color: "#6B8099", marginBottom: 4 }}>QBO sync</div>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: "#c2d0e0", fontWeight: 500 }}>{v.qbo}</div>
              </div>
            </div>

            <div style={{ padding: "15px 16px", background: "#0F1E33", border: "1px solid #1E3350", borderRadius: 11, marginBottom: 16 }}>
              <div
                style={{
                  fontFamily: fonts.semiCondensed,
                  fontSize: 9.5,
                  letterSpacing: ".14em",
                  textTransform: "uppercase",
                  color: "#8fa6c0",
                  marginBottom: 12,
                }}
              >
                AR aging
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "repeat(3,1fr)", gap: 12 }}>
                <div>
                  <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 20, color: "#38d3a6", fontVariantNumeric: "tabular-nums" }}>
                    $4,454
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: "#6B8099" }}>Current · 0–30</div>
                </div>
                <div>
                  <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 20, color: "#ecc94b", fontVariantNumeric: "tabular-nums" }}>
                    $612
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: "#6B8099" }}>31–60</div>
                </div>
                <div>
                  <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 20, color: "#f0803f", fontVariantNumeric: "tabular-nums" }}>
                    $2,905
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: "#6B8099" }}>60–90+</div>
                </div>
              </div>
            </div>

            <div style={{ display: "flex", gap: 9 }}>
              <ActionButton variant="primary">REVIEW &amp; SEND</ActionButton>
              <ActionButton>EXPORT PDF</ActionButton>
              <ActionButton>VIEW IN QBO</ActionButton>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
