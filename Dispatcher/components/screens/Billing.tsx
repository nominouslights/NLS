"use client";

import { colors, fonts, rowSurface, statusMeta, type StatusKind } from "@/lib/theme";
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
              background: "rgba(31,111,178,.09)",
              border: "1px solid rgba(31,111,178,.3)",
            }}
          >
            <span style={{ width: 8, height: 8, borderRadius: "50%", background: "#009E73" }} />
            <div style={{ lineHeight: 1.3 }}>
              <div style={{ fontFamily: fonts.body, fontSize: 11.5, fontWeight: 600, color: colors.skyBlue }}>
                QuickBooks Online · read-only book of record
              </div>
              <div style={{ fontFamily: fonts.mono, fontSize: 9.5, color: colors.textDim }}>Last read 4m ago · no write path</div>
            </div>
          </div>
        </div>
      </div>

      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "44% 1fr", borderTop: `1px solid ${colors.border}` }}>
        <div style={{ minHeight: 0, overflowY: "auto", padding: "16px 18px", borderRight: `1px solid ${colors.border}` }}>
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
              color: colors.textFaint,
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
                  ...rowSurface(active, colors.blue),
                }}
              >
                <div>
                  <div style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.skyBlue }}>{row.id}</div>
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
                      color: colors.textPrimary,
                      whiteSpace: "nowrap",
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                    }}
                  >
                    {row.client}
                  </div>
                  <div style={{ fontFamily: fonts.mono, fontSize: 10.5, color: colors.textDim }}>{row.po}</div>
                </div>
                <div style={{ fontFamily: fonts.mono, fontSize: 12.5, color: colors.textPrimary, fontWeight: 500 }}>{row.amt}</div>
                <div>
                  <StatusChip kind={row.sk} label={row.status} />
                </div>
              </div>
            );
          })}
        </div>

        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: colors.detailBg }}>
          <div className="detailfade" key={v.id}>
            <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 14 }}>
              <StatusChip kind={v.sk} label={v.status} />
              <span style={{ fontFamily: fonts.mono, fontSize: 14, color: colors.skyBlue }}>{v.id}</span>
              <span
                style={{
                  marginLeft: "auto",
                  fontFamily: fonts.condensed,
                  fontWeight: 700,
                  fontSize: 26,
                  color: colors.headingBright,
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
                  color: statusMeta("over").t,
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
                  color: statusMeta("soon").t,
                  fontWeight: 600,
                }}
              >
                ◐ Unmatched payment in QBO — reconcile manually (read-only)
              </div>
            )}

            <div style={{ padding: "16px 18px", background: colors.cardBg, border: `1px solid ${colors.border}`, borderRadius: 11, marginBottom: 12, boxShadow: colors.shadowCard }}>
              <div
                style={{
                  fontFamily: fonts.semiCondensed,
                  fontSize: 9.5,
                  letterSpacing: ".14em",
                  textTransform: "uppercase",
                  color: colors.textLabel,
                  marginBottom: 12,
                }}
              >
                Invoice builder · line items from completed trips
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", padding: "9px 0", borderBottom: `1px solid ${colors.borderSubtle}`, fontFamily: fonts.body, fontSize: 12.5 }}>
                <span style={{ color: colors.textSecondary }}>Corridor run · Thompson → Lynn Lake · 198 km</span>
                <span style={{ fontFamily: fonts.mono, color: colors.textPrimary }}>$3,660.00</span>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", padding: "9px 0", borderBottom: `1px solid ${colors.borderSubtle}`, fontFamily: fonts.body, fontSize: 12.5 }}>
                <span style={{ color: colors.textSecondary }}>GST (5%) · no PST on transportation</span>
                <span style={{ fontFamily: fonts.mono, color: colors.textPrimary }}>$182.00</span>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", padding: "11px 0 0", fontFamily: fonts.body, fontSize: 13, fontWeight: 700 }}>
                <span style={{ color: colors.textPrimary }}>Total (CAD)</span>
                <span style={{ fontFamily: fonts.mono, color: statusMeta("ontime").t }}>{v.amt}</span>
              </div>
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12, marginBottom: 12 }}>
              <div style={{ padding: "13px 15px", background: colors.cardBg, border: `1px solid ${colors.border}`, borderRadius: 10, boxShadow: colors.shadowCard }}>
                <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 4 }}>PO match</div>
                <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textSecondary }}>{v.po}</div>
              </div>
              <div style={{ padding: "13px 15px", background: colors.cardBg, border: `1px solid ${colors.border}`, borderRadius: 10, boxShadow: colors.shadowCard }}>
                <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 4 }}>Budget code (ZBB)</div>
                <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textSecondary }}>{v.code}</div>
              </div>
              <div style={{ padding: "13px 15px", background: colors.cardBg, border: `1px solid ${colors.border}`, borderRadius: 10, boxShadow: colors.shadowCard }}>
                <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 4 }}>QBO sync</div>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary, fontWeight: 500 }}>{v.qbo}</div>
              </div>
            </div>

            <div style={{ padding: "15px 16px", background: colors.cardBg, border: `1px solid ${colors.border}`, borderRadius: 11, marginBottom: 16, boxShadow: colors.shadowCard }}>
              <div
                style={{
                  fontFamily: fonts.semiCondensed,
                  fontSize: 9.5,
                  letterSpacing: ".14em",
                  textTransform: "uppercase",
                  color: colors.textLabel,
                  marginBottom: 12,
                }}
              >
                AR aging
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "repeat(3,1fr)", gap: 12 }}>
                <div>
                  <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 20, color: statusMeta("ontime").t, fontVariantNumeric: "tabular-nums" }}>
                    $4,454
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim }}>Current · 0–30</div>
                </div>
                <div>
                  <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 20, color: statusMeta("soon").t, fontVariantNumeric: "tabular-nums" }}>
                    $612
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim }}>31–60</div>
                </div>
                <div>
                  <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 20, color: statusMeta("over").t, fontVariantNumeric: "tabular-nums" }}>
                    $2,905
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim }}>60–90+</div>
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
