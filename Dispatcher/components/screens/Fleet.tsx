"use client";

import { fonts, rowSurface } from "@/lib/theme";
import { fleet } from "@/lib/data";
import { PageHeader, Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";

export default function Fleet({ fleetSel, setFleetSel }: { fleetSel: number; setFleetSel: (i: number) => void }) {
  const f = fleet[fleetSel];
  const oos = f.status === "Out of service";
  const periodicText = f.periodic
    ? "Six-month NSC Standard 11 periodic inspection (11+ seats)"
    : "Trip-inspection / DVIR only — under 11 seats, no periodic requirement";

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow="Operations · Vehicle status & inspection compliance" title="Fleet" />
      </div>
      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "40% 1fr", borderTop: "1px solid #1E3350" }}>
        <div style={{ minHeight: 0, overflowY: "auto", padding: "16px 18px", borderRight: "1px solid #1E3350" }}>
          {fleet.map((row, i) => {
            const active = i === fleetSel;
            const rowOos = row.status === "Out of service";
            return (
              <div
                key={row.id}
                onClick={() => setFleetSel(i)}
                style={{
                  display: "grid",
                  gridTemplateColumns: "56px 1fr 130px",
                  gap: 11,
                  alignItems: "center",
                  padding: "11px 13px",
                  marginBottom: 5,
                  opacity: rowOos ? 0.72 : 1,
                  ...rowSurface(active, "#3B8DD4"),
                }}
              >
                <div
                  style={{
                    width: 56,
                    height: 40,
                    borderRadius: 8,
                    background: "#0A1729",
                    border: "1px solid #1E3350",
                    display: "flex",
                    flexDirection: "column",
                    alignItems: "center",
                    justifyContent: "center",
                  }}
                >
                  <span style={{ fontFamily: fonts.mono, fontSize: 12, color: "#7EC8F0", fontWeight: 500 }}>{row.unit}</span>
                </div>
                <div style={{ minWidth: 0 }}>
                  <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: "#E8EEF5" }}>{row.model}</div>
                  <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: "#6B8099" }}>
                    {row.seats}-seat · {row.plate}
                  </div>
                </div>
                <div>
                  <StatusChip kind={row.sk} label={row.status} />
                </div>
              </div>
            );
          })}
        </div>

        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: "#0C1A2C" }}>
          <div className="detailfade" key={f.unit}>
            <div style={{ display: "flex", alignItems: "center", gap: 14, marginBottom: 6 }}>
              <StatusChip kind={f.sk} label={f.status} />
              <span style={{ fontFamily: fonts.mono, fontSize: 13, color: "#7EC8F0", marginLeft: "auto" }}>{f.unit}</span>
            </div>
            <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 26, lineHeight: 1.05, color: "#F2F6FB", margin: "6px 0 4px" }}>
              {f.model}
            </h2>
            <div style={{ fontFamily: fonts.mono, fontSize: 12, color: "#9fb2c8", marginBottom: 16 }}>
              {f.seats}-seat · {f.licReq} · {f.plate} · VIN {f.vin}
            </div>

            {oos && (
              <div
                style={{
                  padding: "13px 15px",
                  background: "rgba(213,94,0,.1)",
                  border: "1px solid rgba(213,94,0,.4)",
                  borderRadius: 10,
                  marginBottom: 14,
                  display: "flex",
                  gap: 11,
                  alignItems: "center",
                }}
              >
                <span
                  style={{
                    width: 22,
                    height: 22,
                    flex: "none",
                    borderRadius: 6,
                    background: "#D55E00",
                    color: "#fff",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    fontSize: 12,
                    fontWeight: 800,
                  }}
                >
                  ▲
                </span>
                <div style={{ fontFamily: fonts.body, fontSize: 13, color: "#f0803f", fontWeight: 600 }}>
                  Out of service — removed from dispatch eligibility until cleared. Reason: {f.insp}.
                </div>
              </div>
            )}

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 12 }}>
              <Panel>
                <SectionLabel>Trip inspection · DVIR</SectionLabel>
                <StatusChip kind={f.dk} label={f.dvir} />
                <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: "#6B8099", marginTop: 9, lineHeight: 1.5 }}>
                  NSC Standard 13 — per-trip inspection logged from the Driver Field App.
                </div>
              </Panel>
              <Panel>
                <SectionLabel>Periodic inspection</SectionLabel>
                <StatusChip kind={f.ik} label={f.insp} />
                <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: "#6B8099", marginTop: 9, lineHeight: 1.5 }}>{periodicText}.</div>
              </Panel>
            </div>

            <Panel style={{ marginBottom: 16 }}>
              <SectionLabel>Fault reports</SectionLabel>
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: "#9fb2c8", lineHeight: 1.6 }}>
                No open critical faults. Field-raised faults appear here with severity by status palette; a critical fault forces Out of
                Service.
              </div>
            </Panel>

            <div style={{ display: "flex", gap: 9 }}>
              <ActionButton>VIEW MAINTENANCE LOG</ActionButton>
              <ActionButton variant="destructive">SET OUT OF SERVICE</ActionButton>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
