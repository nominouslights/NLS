"use client";

import { useState, type ReactNode } from "react";
import { fonts } from "@/lib/theme";

const STEP_LABELS = [
  "Service & client",
  "Corridor & schedule",
  "Vehicle & mode",
  "Passengers / demand",
  "Billing",
  "Review & create",
];

function Field({ label, value, mono = false, hint }: { label: string; value: string; mono?: boolean; hint?: ReactNode }) {
  return (
    <div>
      <label style={{ display: "block", fontFamily: fonts.body, fontSize: 11.5, color: "#8fa6c0", marginBottom: 5 }}>
        {label} {hint}
      </label>
      <div
        style={{
          height: 40,
          borderRadius: 9,
          background: "#0A1729",
          border: "1px solid #24405f",
          display: "flex",
          alignItems: "center",
          padding: "0 13px",
          fontFamily: mono ? fonts.mono : fonts.body,
          fontSize: mono ? 13 : 13.5,
          color: "#E8EEF5",
        }}
      >
        {value}
      </div>
    </div>
  );
}

const serviceOptions = [
  { glyph: "▲", label: "Alamos / Corporate", active: true, color: "#E8A020" },
  { glyph: "●", label: "Community", active: false, color: "#3B8DD4" },
  { glyph: "✚", label: "NIHB Medical", active: false, color: "#7EC8F0" },
  { glyph: "★", label: "Charter", active: false, color: "#c2d0e0" },
  { glyph: "▪", label: "Cargo / Parcel", active: false, color: "#8ba4c2" },
  { glyph: "◗", label: "Grocery", active: false, color: "#7EC8F0" },
];

export default function CreateTripWizard({ onClose }: { onClose: () => void }) {
  const [step, setStep] = useState(1);

  return (
    <div
      style={{
        position: "fixed",
        inset: 0,
        zIndex: 100,
        background: "rgba(4,10,20,.7)",
        backdropFilter: "blur(3px)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: 26,
      }}
      className="detailfade"
    >
      <div
        style={{
          width: "100%",
          maxWidth: 1080,
          height: "88vh",
          background: "#0B1626",
          border: "1px solid #24405f",
          borderRadius: 16,
          display: "flex",
          flexDirection: "column",
          overflow: "hidden",
          boxShadow: "0 24px 64px rgba(0,0,0,.5)",
        }}
      >
        {/* header */}
        <div style={{ flex: "none", display: "flex", alignItems: "center", padding: "18px 24px", borderBottom: "1px solid #1E3350" }}>
          <div>
            <div
              style={{
                fontFamily: fonts.semiCondensed,
                fontSize: 10,
                letterSpacing: ".16em",
                textTransform: "uppercase",
                color: "#4d688a",
                marginBottom: 2,
              }}
            >
              Full-screen wizard
            </div>
            <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 22, color: "#F2F6FB", lineHeight: 1 }}>Create Trip</div>
          </div>
          <div
            onClick={onClose}
            style={{
              marginLeft: "auto",
              width: 34,
              height: 34,
              borderRadius: 8,
              border: "1px solid #24405f",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              color: "#9fb2c8",
              cursor: "pointer",
              fontSize: 18,
            }}
          >
            ✕
          </div>
        </div>

        {/* progress */}
        <div style={{ flex: "none", display: "flex", alignItems: "center", gap: 0, padding: "16px 24px", borderBottom: "1px solid #1E3350", overflowX: "auto" }}>
          {STEP_LABELS.map((label, i) => {
            const n = i + 1;
            return (
              <div key={label} style={{ display: "flex", alignItems: "center", gap: 9, flex: "none" }}>
                {n > 1 && <span style={{ width: 26, height: 1.5, background: "#1E3350", margin: "0 4px" }} />}
                <div onClick={() => setStep(n)} style={{ display: "flex", alignItems: "center", gap: 8, cursor: "pointer", paddingRight: 14 }}>
                  <span
                    style={{
                      width: 26,
                      height: 26,
                      flex: "none",
                      borderRadius: "50%",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                      fontFamily: fonts.condensed,
                      fontWeight: 700,
                      fontSize: 12,
                      background: n < step ? "#14B88A" : n === step ? "#3B8DD4" : "#0F1E33",
                      color: n < step ? "#04231a" : n === step ? "#04121f" : "#6B8099",
                      border: n > step ? "1px solid #1E3350" : undefined,
                    }}
                  >
                    {n}
                  </span>
                  <span
                    style={{
                      fontFamily: fonts.body,
                      fontSize: 12,
                      fontWeight: n === step ? 600 : 500,
                      color: n === step ? "#F2F6FB" : n < step ? "#9fb2c8" : "#6B8099",
                      whiteSpace: "nowrap",
                    }}
                  >
                    {label}
                  </span>
                </div>
              </div>
            );
          })}
        </div>

        {/* body */}
        <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "1fr 300px" }}>
          <div style={{ minHeight: 0, overflowY: "auto", padding: "26px 30px" }}>
            {step === 1 && (
              <div className="detailfade">
                <h3 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 22, color: "#F2F6FB", margin: "0 0 4px" }}>
                  Service type &amp; client
                </h3>
                <p style={{ fontFamily: fonts.body, fontSize: 13, color: "#9fb2c8", margin: "0 0 18px" }}>
                  Alamos requires a client and PO number; NIHB opens the voucher field.
                </p>
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 10, marginBottom: 18 }}>
                  {serviceOptions.map((o) => (
                    <div
                      key={o.label}
                      style={{
                        padding: 14,
                        borderRadius: 10,
                        background: o.active ? `${o.color}1A` : "#0F1E33",
                        border: `1px solid ${o.active ? o.color : "#1E3350"}`,
                        cursor: "pointer",
                      }}
                    >
                      <div style={{ color: o.color, fontWeight: 800, marginBottom: 6 }}>{o.glyph}</div>
                      <div style={{ fontFamily: fonts.body, fontWeight: 600, fontSize: 13, color: o.active ? "#E8EEF5" : "#c2d0e0" }}>
                        {o.label}
                      </div>
                    </div>
                  ))}
                </div>
                <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
                  <Field label="Client" value="Alamos Gold" />
                  <Field label="PO number" hint={<span style={{ color: "#E8A020" }}>· required for Alamos</span>} value="PO-AG-2261" mono />
                </div>
              </div>
            )}

            {step === 2 && (
              <div className="detailfade">
                <h3 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 22, color: "#F2F6FB", margin: "0 0 4px" }}>
                  Corridor &amp; schedule
                </h3>
                <p style={{ fontFamily: fonts.body, fontSize: 13, color: "#9fb2c8", margin: "0 0 18px" }}>
                  Corridor-aware picker limited to the five communities. HOS feasibility checked here.
                </p>
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 14 }}>
                  <Field label="Origin" value="Thompson" />
                  <Field label="Destination" value="Lynn Lake" />
                  <Field label="Date" value="2026-07-08" mono />
                  <Field label="Departure window" value="06:30 → 09:55" mono />
                </div>
                <div
                  style={{
                    padding: "12px 15px",
                    background: "rgba(20,184,138,.09)",
                    border: "1px solid rgba(20,184,138,.3)",
                    borderRadius: 10,
                    fontFamily: fonts.body,
                    fontSize: 12.5,
                    color: "#38d3a6",
                    fontWeight: 600,
                  }}
                >
                  ✓ HOS feasible · 198 km within cycle. 220 km corridor — 160 km short-haul exemption does not apply.
                </div>
              </div>
            )}

            {step === 3 && (
              <div className="detailfade">
                <h3 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 22, color: "#F2F6FB", margin: "0 0 4px" }}>
                  Vehicle &amp; mode
                </h3>
                <p style={{ fontFamily: fonts.body, fontSize: 13, color: "#9fb2c8", margin: "0 0 18px" }}>
                  Pick a vehicle, then Open (claimable) or Assigned (eligibility-filtered driver).
                </p>
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10, marginBottom: 18 }}>
                  <div style={{ padding: 14, borderRadius: 10, background: "#16283F", border: "1px solid #2f557d", cursor: "pointer" }}>
                    <div style={{ fontFamily: fonts.body, fontWeight: 600, fontSize: 13.5, color: "#E8EEF5" }}>International 3000</div>
                    <div style={{ fontFamily: fonts.body, fontSize: 12, color: "#9fb2c8" }}>24-seat · U-02</div>
                  </div>
                  <div style={{ padding: 14, borderRadius: 10, background: "#0F1E33", border: "1px solid #1E3350", cursor: "pointer" }}>
                    <div style={{ fontFamily: fonts.body, fontWeight: 600, fontSize: 13.5, color: "#c2d0e0" }}>Ford Transit T-150</div>
                    <div style={{ fontFamily: fonts.body, fontSize: 12, color: "#9fb2c8" }}>7-seat · U-04</div>
                  </div>
                </div>
                <div style={{ display: "flex", gap: 10, marginBottom: 18 }}>
                  <div style={{ flex: 1, padding: 14, borderRadius: 10, background: "rgba(232,160,32,.1)", border: "1px solid #E8A020", cursor: "pointer" }}>
                    <div style={{ fontFamily: fonts.body, fontWeight: 700, fontSize: 13.5, color: "#E8A020" }}>Assigned</div>
                    <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: "#9fb2c8", marginTop: 3 }}>Locks to a named driver (client-mandated)</div>
                  </div>
                  <div style={{ flex: 1, padding: 14, borderRadius: 10, background: "#0F1E33", border: "1px solid #1E3350", cursor: "pointer" }}>
                    <div style={{ fontFamily: fonts.body, fontWeight: 700, fontSize: 13.5, color: "#c2d0e0" }}>Open</div>
                    <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: "#9fb2c8", marginTop: 3 }}>Claimable by any eligible driver</div>
                  </div>
                </div>
                <label style={{ display: "block", fontFamily: fonts.body, fontSize: 11.5, color: "#8fa6c0", marginBottom: 5 }}>
                  Driver · eligibility-filtered
                </label>
                <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 10, padding: "10px 13px", borderRadius: 9, background: "#0F1E33", border: "1px solid #2f557d" }}>
                    <span style={{ width: 8, height: 8, borderRadius: "50%", background: "#14B88A" }} />
                    <span style={{ fontFamily: fonts.body, fontSize: 13, color: "#E8EEF5", fontWeight: 500 }}>D. Chartrand</span>
                    <span style={{ fontFamily: fonts.mono, fontSize: 11, color: "#38d3a6", marginLeft: "auto" }}>Alamos ✓ · HOS ✓</span>
                  </div>
                  <div style={{ display: "flex", alignItems: "center", gap: 10, padding: "10px 13px", borderRadius: 9, background: "#0A1729", border: "1px solid #152941", opacity: 0.6 }}>
                    <span style={{ width: 8, height: 8, borderRadius: "50%", background: "#D55E00" }} />
                    <span style={{ fontFamily: fonts.body, fontSize: 13, color: "#9fb2c8" }}>R. Flett</span>
                    <span style={{ fontFamily: fonts.mono, fontSize: 11, color: "#f0803f", marginLeft: "auto" }}>no active Alamos clearance</span>
                  </div>
                </div>
              </div>
            )}

            {step === 4 && (
              <div className="detailfade">
                <h3 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 22, color: "#F2F6FB", margin: "0 0 4px" }}>
                  Passengers / demand
                </h3>
                <p style={{ fontFamily: fonts.body, fontSize: 13, color: "#9fb2c8", margin: "0 0 18px" }}>
                  Community runs set the 4-passenger threshold. Gift-a-Seat and NIHB voucher suppress the minimum.
                </p>
                <div style={{ padding: 16, background: "#0F1E33", border: "1px solid #1E3350", borderRadius: 11, marginBottom: 12 }}>
                  <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: "#9fb2c8", marginBottom: 9 }}>
                    Alamos crew manifest — attached from site source
                  </div>
                  <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 24, color: "#E8EEF5" }}>18 crew · capacity 24</div>
                </div>
                <div
                  style={{
                    padding: "12px 15px",
                    background: "rgba(59,141,212,.07)",
                    border: "1px solid rgba(59,141,212,.25)",
                    borderRadius: 10,
                    fontFamily: fonts.body,
                    fontSize: 12.5,
                    color: "#9fb2c8",
                    lineHeight: 1.5,
                  }}
                >
                  This is a corporate crew run — the 4-passenger community minimum does not apply. Mixing rule active: community passengers
                  cannot be added while crew are aboard.
                </div>
              </div>
            )}

            {step === 5 && (
              <div className="detailfade">
                <h3 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 22, color: "#F2F6FB", margin: "0 0 4px" }}>Billing</h3>
                <p style={{ fontFamily: fonts.body, fontSize: 13, color: "#9fb2c8", margin: "0 0 18px" }}>
                  Rate basis auto-fills from client/contract. Assign a budget code; confirm GST.
                </p>
                <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
                  <div style={{ display: "flex", justifyContent: "space-between", padding: "12px 15px", background: "#0F1E33", border: "1px solid #1E3350", borderRadius: 10 }}>
                    <span style={{ fontFamily: fonts.body, fontSize: 13, color: "#9fb2c8" }}>Rate basis</span>
                    <span style={{ fontFamily: fonts.body, fontSize: 13, color: "#E8EEF5", fontWeight: 500 }}>Alamos contract · crew shuttle</span>
                  </div>
                  <div style={{ display: "flex", justifyContent: "space-between", padding: "12px 15px", background: "#0F1E33", border: "1px solid #1E3350", borderRadius: 10 }}>
                    <span style={{ fontFamily: fonts.body, fontSize: 13, color: "#9fb2c8" }}>Budget code (ZBB)</span>
                    <span style={{ fontFamily: fonts.mono, fontSize: 12.5, color: "#E8EEF5" }}>ZBB-CREW-01</span>
                  </div>
                  <div style={{ display: "flex", justifyContent: "space-between", padding: "12px 15px", background: "#0F1E33", border: "1px solid #1E3350", borderRadius: 10 }}>
                    <span style={{ fontFamily: fonts.body, fontSize: 13, color: "#9fb2c8" }}>GST</span>
                    <span style={{ fontFamily: fonts.body, fontSize: 13, color: "#38d3a6", fontWeight: 500 }}>5% · no PST on transportation</span>
                  </div>
                </div>
              </div>
            )}

            {step === 6 && (
              <div className="detailfade">
                <h3 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 22, color: "#F2F6FB", margin: "0 0 4px" }}>
                  Review &amp; create
                </h3>
                <p style={{ fontFamily: fonts.body, fontSize: 13, color: "#9fb2c8", margin: "0 0 18px" }}>Full summary. Create as Scheduled.</p>
                <div style={{ padding: 18, background: "#0F1E33", border: "1px solid #1E3350", borderRadius: 12, display: "flex", flexDirection: "column", gap: 11 }}>
                  {[
                    ["Service", "Alamos / Corporate crew"],
                    ["Corridor", "Thompson → Leaf Rapids → Lynn Lake"],
                    ["Schedule", "2026-07-08 · 06:30 → 09:55"],
                    ["Vehicle · mode", "INT-3000 U-02 · Assigned"],
                    ["Driver", "D. Chartrand · eligible ✓"],
                    ["Billing", "PO-AG-2261 · ZBB-CREW-01"],
                  ].map(([label, value], i) => (
                    <div key={label} style={{ display: "flex", justifyContent: "space-between", fontFamily: fonts.body, fontSize: 13 }}>
                      <span style={{ color: "#6B8099" }}>{label}</span>
                      <span
                        style={{
                          color: i === 4 ? "#38d3a6" : "#E8EEF5",
                          fontWeight: 500,
                          fontFamily: i === 2 || i === 5 ? fonts.mono : fonts.body,
                          fontSize: i === 2 || i === 5 ? 12 : 13,
                        }}
                      >
                        {value}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>

          {/* summary rail */}
          <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 20px", background: "#0A1729", borderLeft: "1px solid #1E3350" }}>
            <div
              style={{
                fontFamily: fonts.semiCondensed,
                fontSize: 9.5,
                letterSpacing: ".14em",
                textTransform: "uppercase",
                color: "#4d688a",
                marginBottom: 12,
              }}
            >
              Live summary
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
              <div>
                <div style={{ fontFamily: fonts.body, fontSize: 11, color: "#6B8099", marginBottom: 2 }}>Service</div>
                <div style={{ fontFamily: fonts.body, fontSize: 13, color: "#E8A020", fontWeight: 600 }}>Alamos / Corporate</div>
              </div>
              <div>
                <div style={{ fontFamily: fonts.body, fontSize: 11, color: "#6B8099", marginBottom: 2 }}>Corridor</div>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: "#E8EEF5" }}>Thompson → Lynn Lake · 198 km</div>
              </div>
              <div>
                <div style={{ fontFamily: fonts.body, fontSize: 11, color: "#6B8099", marginBottom: 2 }}>Vehicle</div>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: "#E8EEF5" }}>INT-3000 · U-02</div>
              </div>
              <div>
                <div style={{ fontFamily: fonts.body, fontSize: 11, color: "#6B8099", marginBottom: 2 }}>Driver</div>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: "#38d3a6" }}>D. Chartrand · ✓</div>
              </div>
              <div style={{ paddingTop: 12, borderTop: "1px solid #1E3350" }}>
                <div style={{ fontFamily: fonts.body, fontSize: 11, color: "#6B8099", marginBottom: 2 }}>Est. billable (CAD)</div>
                <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 24, color: "#F2F6FB", fontVariantNumeric: "tabular-nums" }}>
                  $3,842.00
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* footer */}
        <div style={{ flex: "none", display: "flex", alignItems: "center", justifyContent: "space-between", padding: "16px 24px", borderTop: "1px solid #1E3350" }}>
          <span
            onClick={() => setStep((s) => Math.max(1, s - 1))}
            style={{
              fontFamily: fonts.condensed,
              fontWeight: 700,
              fontSize: 13,
              letterSpacing: ".04em",
              padding: "9px 18px",
              borderRadius: 9,
              background: "transparent",
              border: "1px solid #2f557d",
              color: "#c2d0e0",
              cursor: "pointer",
            }}
          >
            BACK
          </span>
          <div style={{ display: "flex", gap: 10 }}>
            <span
              onClick={onClose}
              style={{
                fontFamily: fonts.condensed,
                fontWeight: 700,
                fontSize: 13,
                letterSpacing: ".04em",
                padding: "9px 18px",
                borderRadius: 9,
                background: "transparent",
                color: "#6B8099",
                cursor: "pointer",
              }}
            >
              CANCEL
            </span>
            <span
              onClick={() => setStep((s) => Math.min(6, s + 1))}
              style={{
                fontFamily: fonts.condensed,
                fontWeight: 700,
                fontSize: 13,
                letterSpacing: ".04em",
                padding: "9px 22px",
                borderRadius: 9,
                background: "#3B8DD4",
                color: "#04121f",
                cursor: "pointer",
              }}
            >
              CONTINUE →
            </span>
          </div>
        </div>
      </div>
    </div>
  );
}
