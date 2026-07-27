"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, rowSurface, svcMeta } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  listClients,
  refetchUntil,
  renewalChipFor,
  svcForServiceType,
  type ClientRecord,
} from "@/lib/api/clients";
import { PageHeader, Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import ClientDetail from "@/components/screens/clients/ClientDetail";
import ClientsOverview from "@/components/screens/clients/ClientsOverview";
import ClientOnboardingWizard from "@/components/ClientOnboardingWizard";

// Clients & Contracts — Fleet-style master list + selection-driven detail.
// The client roster, contracts, purchase orders, and CRM (contacts,
// interactions, follow-ups) all come from the real Clients API
// (lib/api/clients.ts), keyed by client Guid. With nothing selected the pane
// shows the clients overview (follow-ups + cross-client PO expiry); selecting a
// client shows the unified detail view.

const EYEBROW = "Business · Client CRM — roster, touchpoints & contract health";
const TITLE = "Clients & Contracts";

function ScreenFrame({ children, right }: { children: React.ReactNode; right?: React.ReactNode }) {
  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow={EYEBROW} title={TITLE} right={right} />
      </div>
      {children}
    </div>
  );
}

function ClientsLoadError({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div style={{ padding: "26px", maxWidth: 560 }}>
      <Panel borderColor="rgba(213,94,0,.4)">
        <div style={{ display: "flex", gap: 11, alignItems: "center", marginBottom: 12 }}>
          <span
            style={{
              width: 20,
              height: 20,
              flex: "none",
              borderRadius: 5,
              background: "#D55E00",
              color: "#fff",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: 11,
              fontWeight: 800,
            }}
          >
            ▲
          </span>
          <span style={{ fontFamily: fonts.body, fontSize: 13.5, fontWeight: 600, color: colors.textPrimary }}>
            Client roster unavailable
          </span>
        </div>
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6, marginBottom: 14 }}>
          {message}
        </div>
        <ActionButton variant="primary" onClick={onRetry}>
          RETRY
        </ActionButton>
      </Panel>
    </div>
  );
}

function ClientsLoadingSkeleton() {
  return (
    <div style={{ padding: "16px 26px" }}>
      {[0, 1, 2, 3, 4].map((i) => (
        <div
          key={i}
          style={{
            height: 58,
            borderRadius: 9,
            border: `1px solid ${colors.borderSubtle}`,
            background: colors.cardBg,
            marginBottom: 6,
            opacity: 0.55 - i * 0.08,
          }}
        />
      ))}
      <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, marginTop: 10 }}>
        Loading clients from API…
      </div>
    </div>
  );
}

export default function Clients({
  clientSel,
  setClientSel,
  onCreateTrip,
}: {
  clientSel: string | null;
  setClientSel: (id: string | null) => void;
  onCreateTrip: () => void;
}) {
  const [roster, setRoster] = useState<ClientRecord[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [tab, setTab] = useState(0);
  const [modal, setModal] = useState<null | "new-client">(null);

  const loadRoster = useCallback(
    async (satisfied?: (rows: ClientRecord[]) => boolean) => {
      try {
        const fresh = satisfied
          ? await refetchUntil(listClients, satisfied)
          : await listClients();
        setRoster(fresh);
        setLoadError(null);
      } catch (e) {
        setRoster(null);
        setLoadError(e instanceof ApiError ? e.message : "Failed to load clients.");
      }
    },
    [],
  );

  useEffect(() => {
    // Fetch on mount; setState only inside the async callbacks, with a
    // mounted guard (same convention as Drivers.tsx / Fleet.tsx).
    let active = true;
    listClients().then(
      (fresh) => {
        if (active) {
          setRoster(fresh);
          setLoadError(null);
        }
      },
      (e) => {
        if (active) {
          setRoster(null);
          setLoadError(e instanceof ApiError ? e.message : "Failed to load clients.");
        }
      },
    );
    return () => {
      active = false;
    };
  }, []);

  function selectClient(id: string, t = 0) {
    setClientSel(id);
    setTab(t);
  }

  async function onClientCreated(newClientId: string) {
    setModal(null);
    const fresh = await refetchUntil(listClients, (r) => r.some((c) => c.id === newClientId));
    setRoster(fresh);
    selectClient(newClientId);
  }

  const headerActions = (
    <ActionButton variant="primary" onClick={() => setModal("new-client")}>
      + ADD CLIENT
    </ActionButton>
  );

  if (loadError) {
    return (
      <ScreenFrame>
        <ClientsLoadError message={loadError} onRetry={() => loadRoster()} />
      </ScreenFrame>
    );
  }

  if (roster === null) {
    return (
      <ScreenFrame>
        <ClientsLoadingSkeleton />
      </ScreenFrame>
    );
  }

  if (roster.length === 0) {
    return (
      <ScreenFrame right={headerActions}>
        <div style={{ padding: "26px", maxWidth: 560 }}>
          <Panel>
            <SectionLabel>No clients registered</SectionLabel>
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6 }}>
              The client roster is empty for this tenant. Use + ADD CLIENT to register the first
              client and make it available for contracts, purchase orders, and trip creation.
            </div>
          </Panel>
        </div>
        {modal === "new-client" && (
          <ClientOnboardingWizard onClose={() => setModal(null)} onSaved={onClientCreated} />
        )}
      </ScreenFrame>
    );
  }

  const selIndex = clientSel === null ? -1 : roster.findIndex((c) => c.id === clientSel);
  const selected = selIndex === -1 ? null : roster[selIndex];

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow={EYEBROW} title={TITLE} right={headerActions} />
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
              ...rowSurface(selected === null),
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

          {roster.map((row) => {
            const active = row.id === clientSel;
            const svc = svcForServiceType(row.serviceType);
            const rsc = svcMeta(svc);
            const renewal = renewalChipFor(row.activeContract);
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
                    borderRadius: svc === "alamos" ? 2 : "50%",
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
                <StatusChip kind={renewal.kind} label={renewal.label} />
              </div>
            );
          })}
        </div>

        {/* detail pane */}
        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: colors.detailBg }}>
          {selected === null ? (
            <ClientsOverview roster={roster} onOpenClient={selectClient} />
          ) : (
            <ClientDetail
              client={selected}
              tab={tab}
              setTab={setTab}
              onCreateTrip={onCreateTrip}
              onRosterRefresh={loadRoster}
            />
          )}
        </div>
      </div>
      {modal === "new-client" && (
        <ClientOnboardingWizard onClose={() => setModal(null)} onSaved={onClientCreated} />
      )}
    </div>
  );
}
