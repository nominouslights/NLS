"use client";

import { useState } from "react";
import { colors, fonts, rowSurface, svcMeta } from "@/lib/theme";
import { clients } from "@/lib/data";
import { PageHeader } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import ClientDetail from "@/components/screens/clients/ClientDetail";
import ClientsOverview from "@/components/screens/clients/ClientsOverview";

// Clients & Contracts — Fleet-style master list + selection-driven detail. With
// nothing selected the pane shows the clients overview (upcoming follow-ups +
// cross-client PO expiry); selecting a client shows the unified detail view
// (contract / PO dashboard + contact roster + interaction log).

export default function Clients({
  clientSel,
  setClientSel,
  onCreateTrip,
}: {
  clientSel: number | null;
  setClientSel: (i: number | null) => void;
  onCreateTrip: () => void;
}) {
  const [tab, setTab] = useState(0);
  const selected = clientSel === null ? null : clients.find((c) => c.id === clientSel) ?? null;

  function selectClient(id: number) {
    setClientSel(id);
    setTab(0);
  }

  function openClientTab(id: number, t = 0) {
    setClientSel(id);
    setTab(t);
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow="Business · Client CRM — roster, touchpoints & contract health" title="Clients & Contracts" />
      </div>
      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "38% 1fr", borderTop: `1px solid ${colors.border}` }}>
        {/* master list */}
        <div style={{ minHeight: 0, overflowY: "auto", padding: "16px 18px", borderRight: `1px solid ${colors.border}` }}>
          {/* overview row */}
          <div
            onClick={() => setClientSel(null)}
            style={{
              display: "flex",
              alignItems: "center",
              gap: 11,
              padding: "12px 13px",
              marginBottom: 5,
              ...rowSurface(clientSel === null),
            }}
          >
            <span style={{ width: 9, height: 9, flex: "none", borderRadius: 2, background: colors.blue }} />
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ fontFamily: fonts.body, fontSize: 13.5, fontWeight: 600, color: colors.textPrimary }}>
                Overview
              </div>
              <div
                style={{
                  fontFamily: fonts.semiCondensed,
                  fontSize: 9.5,
                  letterSpacing: ".1em",
                  textTransform: "uppercase",
                  color: colors.textDim,
                }}
              >
                Follow-ups & PO expiry
              </div>
            </div>
          </div>

          {clients.map((row) => {
            const active = row.id === clientSel;
            const rsc = svcMeta(row.svc);
            return (
              <div
                key={row.id}
                onClick={() => selectClient(row.id)}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 11,
                  padding: "12px 13px",
                  marginBottom: 5,
                  ...rowSurface(active, rsc.accent),
                }}
              >
                <span
                  style={{
                    width: 9,
                    height: 9,
                    flex: "none",
                    borderRadius: row.svc === "alamos" ? 2 : "50%",
                    background: rsc.accent,
                  }}
                />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div
                    style={{
                      fontFamily: fonts.body,
                      fontSize: 13.5,
                      fontWeight: 600,
                      color: colors.textPrimary,
                      whiteSpace: "nowrap",
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                    }}
                  >
                    {row.name}
                  </div>
                  <div
                    style={{
                      fontFamily: fonts.semiCondensed,
                      fontSize: 9.5,
                      letterSpacing: ".1em",
                      textTransform: "uppercase",
                      color: colors.textDim,
                    }}
                  >
                    {row.tag}
                  </div>
                </div>
                <StatusChip kind={row.rk} label={row.renew} />
              </div>
            );
          })}
        </div>

        {/* detail pane */}
        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: colors.detailBg }}>
          {selected === null ? (
            <ClientsOverview onOpenClient={openClientTab} />
          ) : (
            <ClientDetail client={selected} tab={tab} setTab={setTab} onCreateTrip={onCreateTrip} />
          )}
        </div>
      </div>
    </div>
  );
}
