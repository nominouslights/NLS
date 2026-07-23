"use client";

import { useEffect, useState } from "react";
import { colors, fonts, svcMeta } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  BILLING_FREQUENCY_LABELS,
  BILLING_MODEL_LABELS,
  contractRateLabel,
  contractStatusKindFor,
  contractTermLabel,
  listContracts,
  refetchUntil,
  renewalChipFor,
  sortContracts,
  svcForServiceType,
  terminateContract,
  type ClientRecord,
  type ContractInput,
  type ContractRecord,
} from "@/lib/api/clients";
import { Panel, SectionLabel, DetailRow } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import ContractFormModal from "@/components/ContractFormModal";
import ContactRoster from "./ContactRoster";
import InteractionTimeline from "./InteractionTimeline";
import ClientPoDashboard from "./ClientPoDashboard";
import { CLIENT_TABS, isClientType, VENDOR_TABS } from "./shared";

// Unified client detail — one screen per organization combining the contract
// summary + billing config (real Clients API: structured active contract with
// dates, rate, GST, terms, budget code), contract management (create / edit /
// terminate), the PO expiry dashboard (real API CRUD), the contact roster, and
// the interaction timeline. The roster + interaction tabs are Client-type only
// and still run on the prototype-CRM mock store, joined by mockCrmId (see
// ./shared.tsx).

/** True when the projection row reflects the submitted contract body. */
function contractMatches(r: ContractRecord, id: string, input: ContractInput): boolean {
  return (
    r.id === id &&
    r.startDate === input.startDate &&
    (r.endDate ?? null) === (input.endDate ?? null) &&
    r.billingModel === input.billingModel &&
    (r.ratePerRoundTripCad ?? null) === (input.ratePerRoundTripCad ?? null) &&
    r.gstApplicable === input.gstApplicable &&
    (r.budgetCode ?? null) === (input.budgetCode ?? null) &&
    r.billingFrequency === input.billingFrequency &&
    r.netTermsDays === input.netTermsDays &&
    (r.defaultPoNumber ?? null) === (input.defaultPoNumber ?? null)
  );
}

export default function ClientDetail({
  client,
  mockCrmId,
  tab,
  setTab,
  onCreateTrip,
  onRosterRefresh,
}: {
  client: ClientRecord;
  /** Numeric prototype-CRM id joining the mock contact/interaction store. */
  mockCrmId: number;
  tab: number;
  setTab: (n: number) => void;
  onCreateTrip: () => void;
  /** Reload the roster (activeContract summaries live on ClientResponse). */
  onRosterRefresh: (satisfied?: (rows: ClientRecord[]) => boolean) => Promise<void>;
}) {
  const accent = svcMeta(svcForServiceType(client.serviceType)).accent;
  const clientType = isClientType(client);
  const tabs = clientType ? CLIENT_TABS : VENDOR_TABS;
  const activeTab = tab < tabs.length ? tab : 0;
  const contract = client.activeContract;
  const renewal = renewalChipFor(contract);

  // Contract history (real API), keyed by client id — switching clients reads
  // as "loading" until this client's fetch lands.
  const [contractsState, setContractsState] = useState<{ clientId: string; rows: ContractRecord[] } | null>(null);
  const [contractsError, setContractsError] = useState<{ clientId: string; message: string } | null>(null);

  const [modal, setModal] = useState<null | "new-contract" | "edit-contract">(null);
  const [confirmTerminate, setConfirmTerminate] = useState(false);
  const [busy, setBusy] = useState(false);
  const [actionErrorState, setActionErrorState] = useState<{ clientId: string; message: string } | null>(null);

  useEffect(() => {
    let active = true;
    const clientId = client.id;
    listContracts(clientId).then(
      (rows) => {
        if (active) {
          setContractsState({ clientId, rows: sortContracts(rows) });
          setContractsError(null);
        }
      },
      (e) => {
        if (active) {
          setContractsError({
            clientId,
            message: e instanceof ApiError ? e.message : "Failed to load contracts.",
          });
        }
      },
    );
    return () => {
      active = false;
    };
  }, [client.id]);

  const contracts = contractsState?.clientId === client.id ? contractsState.rows : null;
  const contractsErrorMessage = contractsError?.clientId === client.id ? contractsError.message : null;
  const actionError = actionErrorState?.clientId === client.id ? actionErrorState.message : null;

  async function runAction(fn: () => Promise<void>) {
    if (busy) return;
    setBusy(true);
    setActionErrorState(null);
    try {
      await fn();
    } catch (e) {
      setActionErrorState({
        clientId: client.id,
        message: e instanceof ApiError ? e.message : "Action failed — please try again.",
      });
    } finally {
      setBusy(false);
    }
  }

  // Reads are eventually consistent projections — refetch with a short retry
  // until the change is visible (refetchUntil), then refresh the roster so the
  // embedded activeContract summary / renewal chips catch up.
  async function onContractSaved(contractId: string, input: ContractInput) {
    await runAction(async () => {
      const rows = await refetchUntil(
        () => listContracts(client.id),
        (r) => r.some((c) => contractMatches(c, contractId, input)),
      );
      setContractsState({ clientId: client.id, rows: sortContracts(rows) });
      await onRosterRefresh();
    });
  }

  async function onTerminate(contractId: string) {
    setConfirmTerminate(false);
    await runAction(async () => {
      await terminateContract(client.id, contractId);
      const rows = await refetchUntil(
        () => listContracts(client.id),
        (r) => r.find((c) => c.id === contractId)?.status === "Terminated",
      );
      setContractsState({ clientId: client.id, rows: sortContracts(rows) });
      await onRosterRefresh(
        (roster) => roster.find((c) => c.id === client.id)?.activeContract?.activeContractId !== contractId,
      );
    });
  }

  return (
    <div className="detailfade" key={client.id}>
      {/* header */}
      <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 6 }}>
        <span style={{ width: 14, height: 14, borderRadius: 4, background: accent }} />
        <span
          style={{
            fontFamily: fonts.semiCondensed,
            fontSize: 10,
            letterSpacing: ".12em",
            textTransform: "uppercase",
            color: colors.textLabel,
          }}
        >
          {client.tag}
        </span>
        <span style={{ marginLeft: "auto" }}>
          <StatusChip kind={renewal.kind} label={renewal.label} />
        </span>
      </div>
      <h2
        style={{
          fontFamily: fonts.condensed,
          fontWeight: 700,
          fontSize: 28,
          lineHeight: 1,
          color: colors.headingBright,
          margin: "6px 0 14px",
        }}
      >
        {client.name}
      </h2>

      {/* tab bar */}
      <div style={{ display: "flex", gap: 2, borderBottom: `1px solid ${colors.border}`, marginBottom: 16, flexWrap: "wrap" }}>
        {tabs.map((t, i) => (
          <span
            key={t}
            onClick={() => setTab(i)}
            style={{
              fontFamily: fonts.body,
              fontWeight: activeTab === i ? 600 : 500,
              fontSize: 12.5,
              padding: "9px 14px",
              color: activeTab === i ? colors.headingBright : colors.textDim,
              borderBottom: activeTab === i ? `2px solid ${colors.blue}` : undefined,
              marginBottom: -1,
              cursor: "pointer",
              whiteSpace: "nowrap",
            }}
          >
            {t}
          </span>
        ))}
      </div>

      {/* Overview & POs */}
      {activeTab === 0 && (
        <div>
          {actionError && (
            <Panel borderColor="rgba(213,94,0,.4)" style={{ marginBottom: 12 }}>
              <StatusChip kind="over" label={actionError} />
            </Panel>
          )}

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 12 }}>
            <Panel>
              <SectionLabel>Contract summary</SectionLabel>
              {contract === null ? (
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, marginBottom: 10 }}>
                  No active contract on file.
                </div>
              ) : (
                <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                  <DetailRow
                    label="Term"
                    value={contractTermLabel(contract)}
                    valueStyle={{ fontFamily: fonts.mono, fontSize: 11.5 }}
                  />
                  <DetailRow label="Rate" value={contractRateLabel(contract)} />
                  <DetailRow label="Billing model" value={BILLING_MODEL_LABELS[contract.billingModel]} />
                </div>
              )}
              <div style={{ display: "flex", flexWrap: "wrap", gap: 8, marginTop: 12 }}>
                <ActionButton variant="primary" onClick={() => setModal("new-contract")} disabled={busy}>
                  + NEW CONTRACT
                </ActionButton>
                {contract && (
                  <>
                    <ActionButton onClick={() => setModal("edit-contract")} disabled={busy}>
                      EDIT
                    </ActionButton>
                    {confirmTerminate ? (
                      <>
                        <ActionButton
                          variant="destructive"
                          onClick={() => onTerminate(contract.activeContractId)}
                          disabled={busy}
                        >
                          {busy ? "WORKING…" : "CONFIRM TERMINATE"}
                        </ActionButton>
                        <ActionButton onClick={() => setConfirmTerminate(false)}>KEEP</ActionButton>
                      </>
                    ) : (
                      <ActionButton
                        variant="destructive"
                        onClick={() => setConfirmTerminate(true)}
                        disabled={busy}
                      >
                        TERMINATE
                      </ActionButton>
                    )}
                  </>
                )}
              </div>
            </Panel>
            <Panel>
              <SectionLabel>PO &amp; billing config</SectionLabel>
              {contract === null ? (
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
                  Billing configuration comes from the active contract — create one to set the
                  default PO, GST, budget code, and terms that drive invoice drafting.
                </div>
              ) : (
                <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                  <DetailRow
                    label="Default PO"
                    value={contract.defaultPoNumber ?? "—"}
                    valueStyle={{ fontFamily: fonts.mono, fontSize: 11.5 }}
                  />
                  <DetailRow label="Tax" value={contract.gstApplicable ? "GST 5% applicable" : "No GST"} />
                  <DetailRow
                    label="Budget code"
                    value={contract.budgetCode ?? "—"}
                    valueStyle={{ fontFamily: fonts.mono, fontSize: 11.5 }}
                  />
                  <DetailRow
                    label="Billing"
                    value={`${BILLING_FREQUENCY_LABELS[contract.billingFrequency]} · net ${contract.netTermsDays}d`}
                  />
                </div>
              )}
            </Panel>
          </div>

          {client.notes && (
            <div
              style={{
                padding: "13px 15px",
                background: "rgba(232,160,32,.08)",
                border: "1px solid rgba(232,160,32,.28)",
                borderRadius: 10,
                marginBottom: 12,
                display: "flex",
                gap: 10,
                alignItems: "flex-start",
              }}
            >
              <span style={{ color: colors.amber, fontWeight: 800, fontSize: 13 }}>▲</span>
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, lineHeight: 1.55, color: colors.textSecondary }}>
                {client.notes}
              </div>
            </div>
          )}

          {/* contract history */}
          <Panel style={{ marginBottom: 12 }}>
            <SectionLabel>Contract history</SectionLabel>
            {contractsErrorMessage ? (
              <StatusChip kind="over" label={`Contracts unavailable — ${contractsErrorMessage}`} />
            ) : contracts === null ? (
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading contracts…</div>
            ) : contracts.length === 0 ? (
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
                No contracts on file for this client.
              </div>
            ) : (
              <div style={{ marginTop: -4 }}>
                {contracts.map((c) => (
                  <div
                    key={c.id}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "space-between",
                      gap: 12,
                      padding: "10px 0",
                      borderTop: `1px solid ${colors.borderSubtle}`,
                    }}
                  >
                    <div style={{ minWidth: 0 }}>
                      <div style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textSecondary }}>
                        {contractTermLabel(c)}
                      </div>
                      <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim, marginTop: 2 }}>
                        {contractRateLabel(c)}
                        {c.budgetCode && <span style={{ color: colors.textFaint }}> · {c.budgetCode}</span>}
                      </div>
                    </div>
                    <StatusChip kind={contractStatusKindFor(c.status)} label={c.status} />
                  </div>
                ))}
              </div>
            )}
          </Panel>

          <div style={{ marginBottom: 16 }}>
            <ClientPoDashboard clientId={client.id} clientName={client.name} />
          </div>

          <div style={{ display: "flex", gap: 9 }}>
            <ActionButton variant="amber" onClick={onCreateTrip}>
              CREATE TRIP FOR THIS CLIENT
            </ActionButton>
            <ActionButton>VIEW TRIP HISTORY</ActionButton>
          </div>

          {!clientType && (
            <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginTop: 14 }}>
              Contact roster and interaction log apply to Client-type organizations. This is a Vendor / Partner
              record.
            </div>
          )}
        </div>
      )}

      {/* Contacts (client-type only — real Clients API, keyed by the real client id) */}
      {clientType && activeTab === 1 && <ContactRoster clientId={client.id} clientName={client.name} />}

      {/* Interactions (client-type only — prototype-CRM mock store) */}
      {clientType && activeTab === 2 && <InteractionTimeline clientId={mockCrmId} clientName={client.name} />}

      {modal === "new-contract" && (
        <ContractFormModal
          clientId={client.id}
          clientName={client.name}
          onClose={() => setModal(null)}
          onSaved={onContractSaved}
        />
      )}
      {modal === "edit-contract" && contract && (
        <ContractFormModal
          clientId={client.id}
          clientName={client.name}
          existing={contract}
          onClose={() => setModal(null)}
          onSaved={onContractSaved}
        />
      )}
    </div>
  );
}
