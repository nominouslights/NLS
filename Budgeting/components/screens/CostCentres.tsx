"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, rowSurface } from "@/lib/theme";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { DetailRow, Panel, SectionLabel } from "@/components/ui/Panel";
import { ApiError } from "@/lib/api/transport";
import {
  deleteCostCentre,
  listBudgetOwnerCandidates,
  listCostCentres,
  refetchUntil,
  setCostCentreActive,
  userDisplay,
  type BudgetOwnerOption,
  type CostCentreRecord,
} from "@/lib/api/budgeting";
import { costCentreStatus, hasChildren } from "@/lib/costCentres";
import { ErrorNotice } from "@/components/ErrorNotice";
import CostCentreFormModal from "@/components/CostCentreFormModal";
import { EmptyNote, Screen } from "@/components/screens/shared";

// The cost-centre register — a screen of its own, NOT a tab on Budget Codes, and NOT
// period-scoped (lib/nav.ts leaves "costCentres" out of PERIOD_SCOPED). Budget Codes shows one
// period's chart and remounts on every period switch; the register is tenant-wide and outlives
// every period, so housing it under a period banner would suggest an edit here changes one
// period only. The banner reads "This screen isn't tied to a period." here, as on Settings.
//
// Master/detail like Budget Codes: rows on the left (code, name, owner, parent, status chip),
// the selected entry's detail on the right. The register is always fetched WITH retired entries
// (includeInactive=true) — the parent rule and the children checks need them — and "Show
// retired" filters on the client, so the toggle is instant.
//
// Retiring is the normal end of life (two-click); a retired entry stays listed so the budget
// codes carrying it keep resolving. DELETE (two-click) is for an entry created in error: the
// server answers 409 HasChildren or InUse otherwise, shown verbatim — and for InUse the banner
// offers RETIRE INSTEAD, the alternative the message names.

type PendingConfirm = { kind: "retire" | "delete"; id: string } | null;

export default function CostCentres() {
  // null = still loading.
  const [register, setRegister] = useState<CostCentreRecord[] | null>(null);
  const [owners, setOwners] = useState<BudgetOwnerOption[]>([]);
  const [error, setError] = useState<{ message: string; code: string } | null>(null);
  const [selId, setSelId] = useState<string | null>(null);
  const [showRetired, setShowRetired] = useState(false);
  const [form, setForm] = useState<{ entry: CostCentreRecord | null } | null>(null);
  const [busy, setBusy] = useState(false);
  const [confirm, setConfirm] = useState<PendingConfirm>(null);

  const applyLoaded = useCallback((records: CostCentreRecord[]) => {
    setRegister(records);
    setError(null);
  }, []);

  const applyError = useCallback((e: unknown) => {
    setRegister((prev) => prev ?? []);
    setError(
      e instanceof ApiError
        ? { message: e.message, code: e.code }
        : { message: "Failed to load the cost-centre register.", code: "Unknown" },
    );
  }, []);

  const load = useCallback(() => {
    listCostCentres({ includeInactive: true }).then(applyLoaded, applyError);
  }, [applyLoaded, applyError]);

  useEffect(() => {
    let active = true;
    listCostCentres({ includeInactive: true }).then(
      (records) => {
        if (active) applyLoaded(records);
      },
      (e) => {
        if (active) applyError(e);
      },
    );
    // The owner picker's options. A failure only narrows the picker to "Unassigned".
    listBudgetOwnerCandidates().then(
      (rows) => {
        if (active) setOwners(rows);
      },
      () => {},
    );
    return () => {
      active = false;
    };
  }, [applyLoaded, applyError]);

  const all = register ?? [];
  const list = showRetired ? all : all.filter((c) => c.isActive);
  const retiredCount = all.length - all.filter((c) => c.isActive).length;
  const selected = list.find((c) => c.id === selId) ?? list[0] ?? null;
  const selectedIsParent = selected !== null && hasChildren(all, selected.id);
  const confirmingRetire = selected !== null && confirm?.kind === "retire" && confirm.id === selected.id;
  const confirmingDelete = selected !== null && confirm?.kind === "delete" && confirm.id === selected.id;

  function select(id: string) {
    setSelId(id);
    setConfirm(null); // a pending confirm never survives a selection change
  }

  async function run(action: () => Promise<unknown>, settled: (rows: CostCentreRecord[]) => boolean) {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      await action();
      applyLoaded(await refetchUntil(() => listCostCentres({ includeInactive: true }), settled));
    } catch (e) {
      // Keep the list; show the server's refusal verbatim.
      applyError(e);
    } finally {
      setBusy(false);
    }
  }

  async function toggleActive(target: CostCentreRecord) {
    if (target.isActive && (confirm?.kind !== "retire" || confirm.id !== target.id)) {
      setConfirm({ kind: "retire", id: target.id });
      return;
    }
    setConfirm(null);
    const next = !target.isActive;
    // A retired entry would vanish from a list that hides retired ones — show them, so the
    // planner sees the result of the click.
    if (!next) setShowRetired(true);
    await run(
      () => setCostCentreActive(target.id, next),
      (rows) => rows.some((r) => r.id === target.id && r.isActive === next),
    );
  }

  async function remove(target: CostCentreRecord) {
    if (confirm?.kind !== "delete" || confirm.id !== target.id) {
      setConfirm({ kind: "delete", id: target.id });
      return;
    }
    setConfirm(null);
    await run(
      () => deleteCostCentre(target.id),
      (rows) => !rows.some((r) => r.id === target.id),
    );
  }

  function handleSaved(records: CostCentreRecord[], id: string) {
    applyLoaded(records);
    setSelId(id);
  }

  return (
    <Screen
      eyebrow="Planning · every period"
      title="Cost Centres"
      right={
        <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
          {retiredCount > 0 && (
            <ActionButton onClick={() => setShowRetired((v) => !v)}>
              {showRetired ? "HIDE RETIRED" : `SHOW RETIRED (${retiredCount})`}
            </ActionButton>
          )}
          <ActionButton
            variant="primary"
            onClick={() => {
              setConfirm(null);
              setForm({ entry: null });
            }}
          >
            + NEW COST CENTRE
          </ActionButton>
        </div>
      }
    >
      <div style={{ marginBottom: 12, fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, lineHeight: 1.6 }}>
        The organisational units and bases cost is attributed to. One register for every period —
        a budget code&apos;s cost centre is picked from here, and the dashboard totals each
        period&apos;s planned expense by it.
      </div>

      {error && (
        <div style={{ marginBottom: 12 }}>
          <ErrorNotice title="Cost centres" message={error.message} code={error.code} />
          <div style={{ marginTop: 9, display: "flex", gap: 10 }}>
            {error.code === "Budgeting.CostCentre.InUse" && selected?.isActive ? (
              <ActionButton variant="amber" onClick={() => void toggleActive(selected)} disabled={busy}>
                {confirmingRetire ? "CONFIRM RETIRE" : "RETIRE INSTEAD"}
              </ActionButton>
            ) : (
              <ActionButton onClick={load}>RETRY</ActionButton>
            )}
          </div>
        </div>
      )}

      {register === null && !error && <EmptyNote>Loading the cost-centre register…</EmptyNote>}

      {register !== null && list.length === 0 && !error && (
        <EmptyNote>
          {all.length === 0
            ? "No cost centres yet. Add the units and bases cost is attributed to — budget codes can then be tagged to them."
            : "Every cost centre is retired. Show retired to see them, or add a new one."}
        </EmptyNote>
      )}

      {list.length > 0 && (
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "38% 1fr",
            borderTop: `1px solid ${colors.border}`,
            height: "100%",
            minHeight: 0,
          }}
        >
          <div
            style={{
              overflowY: "auto",
              padding: "16px 18px 16px 0",
              borderRight: `1px solid ${colors.border}`,
              display: "flex",
              flexDirection: "column",
              gap: 8,
            }}
          >
            {list.map((c) => {
              const status = costCentreStatus(c.isActive);
              return (
                <div
                  key={c.id}
                  data-testid="cost-centre-row"
                  onClick={() => select(c.id)}
                  style={{ ...rowSurface(selected?.id === c.id), padding: "11px 13px" }}
                >
                  <div style={{ display: "flex", alignItems: "center", gap: 9, marginBottom: 4 }}>
                    <MonoTag>{c.code}</MonoTag>
                    <StatusChip kind={status.kind} label={status.label} />
                  </div>
                  <div style={{ fontFamily: fonts.body, fontWeight: 600, fontSize: 12.5, color: colors.textPrimary }}>
                    {c.name}
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginTop: 2 }}>
                    {userDisplay(c.ownerName, c.ownerEmail, "No owner")}
                    {c.parentCode && ` · ↳ ${c.parentCode}`}
                  </div>
                </div>
              );
            })}
          </div>

          <div style={{ overflowY: "auto", padding: "22px 0 22px 26px", background: colors.detailBg }}>
            {selected ? (
              <>
                <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 14, flexWrap: "wrap" }}>
                  <MonoTag>{selected.code}</MonoTag>
                  <div
                    style={{
                      fontFamily: fonts.condensed,
                      fontWeight: 700,
                      fontSize: 22,
                      color: colors.headingBright,
                      flex: "1 1 auto",
                      minWidth: 0,
                    }}
                  >
                    {selected.name}
                  </div>
                  <ActionButton
                    onClick={() => {
                      setConfirm(null);
                      setForm({ entry: selected });
                    }}
                  >
                    EDIT
                  </ActionButton>
                  <ActionButton
                    variant={selected.isActive ? "amber" : "success"}
                    onClick={() => void toggleActive(selected)}
                    disabled={busy}
                  >
                    {busy
                      ? "WORKING…"
                      : !selected.isActive
                        ? "RESTORE"
                        : confirmingRetire
                          ? "CONFIRM RETIRE"
                          : "RETIRE"}
                  </ActionButton>
                  <ActionButton variant="destructive" onClick={() => void remove(selected)} disabled={busy}>
                    {confirmingDelete ? "CONFIRM DELETE" : "DELETE"}
                  </ActionButton>
                </div>

                {confirmingRetire && (
                  <ConfirmNote>
                    Retiring {selected.code} stops it being offered for budget codes in every
                    period. Codes that already carry it keep it, and it stays listed here so they
                    keep resolving. Click CONFIRM RETIRE to proceed; selecting another cost centre
                    cancels.
                  </ConfirmNote>
                )}

                {confirmingDelete && (
                  <ConfirmNote>
                    Deleting {selected.code} is permanent and is only for a cost centre created in
                    error. If any budget code in any period carries it, retire it instead — click
                    anything else to cancel.
                  </ConfirmNote>
                )}

                {selected.description && (
                  <Panel style={{ marginBottom: 12 }}>
                    <SectionLabel>Description</SectionLabel>
                    <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary, lineHeight: 1.65 }}>
                      {selected.description}
                    </div>
                  </Panel>
                )}

                <Panel>
                  <SectionLabel>Details</SectionLabel>
                  <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                    <DetailRow label="Owner" value={userDisplay(selected.ownerName, selected.ownerEmail, "Unassigned")} />
                    <DetailRow
                      label="Parent"
                      value={selected.parentCode ? `${selected.parentCode} · ${selected.parentName ?? ""}` : "Top level"}
                    />
                    <DetailRow
                      label="Status"
                      value={
                        <StatusChip
                          kind={costCentreStatus(selected.isActive).kind}
                          label={costCentreStatus(selected.isActive).label}
                        />
                      }
                    />
                    <DetailRow label="Created by" value={userDisplay(selected.createdByName, selected.createdByEmail, "—")} />
                    <DetailRow
                      label="Last modified by"
                      value={userDisplay(selected.modifiedByName, selected.modifiedByEmail, "—")}
                    />
                  </div>
                </Panel>

                {!selected.isActive && (
                  <Note>
                    Retired cost centres stay listed on purpose — budget codes already tagged with
                    {" "}
                    {selected.code} still resolve to it, and keep it when they are edited.
                  </Note>
                )}

                {selectedIsParent && (
                  <Note>
                    Other cost centres roll up into this one. It cannot be retired while any of them
                    is active, nor deleted while any of them points at it.
                  </Note>
                )}
              </>
            ) : (
              <EmptyNote>No cost centre selected.</EmptyNote>
            )}
          </div>
        </div>
      )}

      {form && (
        <CostCentreFormModal
          entry={form.entry}
          register={all}
          owners={owners}
          onClose={() => setForm(null)}
          onSaved={handleSaved}
        />
      )}
    </Screen>
  );
}

function ConfirmNote({ children }: { children: React.ReactNode }) {
  return (
    <div style={{ marginBottom: 12, fontFamily: fonts.body, fontSize: 11.5, color: colors.textSecondary, lineHeight: 1.6 }}>
      {children}
    </div>
  );
}

function Note({ children }: { children: React.ReactNode }) {
  return (
    <div style={{ marginTop: 10, fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, lineHeight: 1.6 }}>
      {children}
    </div>
  );
}
