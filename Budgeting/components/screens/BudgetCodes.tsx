"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, rowSurface } from "@/lib/theme";
import type { BudgetCode, BudgetPeriod } from "@/lib/types";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { DetailRow, Panel, SectionLabel } from "@/components/ui/Panel";
import { ApiError } from "@/lib/api/transport";
import {
  budgetCodeCategoryKind,
  canEditPlan,
  copyBudgetCodes,
  costCentreApplies,
  deleteBudgetCode,
  listBudgetCodes,
  listBudgetOwnerCandidates,
  listCostCentres,
  refetchUntil,
  seedStarterBudgetCodes,
  setBudgetCodeActive,
  toBudgetCode,
  userDisplay,
  PERIOD_STATE_LABELS,
  REVIEW_FREQUENCY_LABELS,
  SERVICE_LINE_LABELS,
  TAX_TREATMENT_LABELS,
  type BudgetCodeCopyResult,
  type BudgetCodeRecord,
  type BudgetOwnerOption,
  type CostCentreRecord,
} from "@/lib/api/budgeting";
import { findCostCentre } from "@/lib/costCentres";
import { usePeriodHold } from "@/lib/periodHold";
import { ErrorNotice } from "@/components/ErrorNotice";
import BudgetCodeFormModal from "@/components/BudgetCodeFormModal";
import CopyCodesPanel from "@/components/screens/codes/CopyCodesPanel";
import { EmptyNote, Screen } from "@/components/screens/shared";

// Master/detail on real data (periods/{id}/codes), following Dispatcher's Clients and Trips
// screens: a left column of rows and a right detail pane on the tinted detailBg, split by a CSS
// grid with a top border.
//
// The chart belongs to the ENTERED PERIOD. Each period has its own codes (the same code string —
// FUEL — exists once per period, as its own row), so this is a period-scoped screen like the
// dashboard: Console renders the chooser until a period is entered and remounts this screen on
// every switch. Every request names the period; every confirm names it too.
//
// The chart follows the period lifecycle (BudgetPeriod.AllowsPlanChanges, mirrored by
// canEditPlan): in Draft or Open it can be built — create, edit, retire, restore, delete, the
// starter set, and a copy from another period. In Finalized, In review and Closed it is
// read-only: every control is absent and one note names the period and its state, so "I can't
// add a code" reads as the rule it is rather than as a bug.
//
// A new period starts with an empty chart, and the empty state offers the three ways out: copy
// from an earlier period (CopyCodesPanel), load the starter set, or add a code by hand.
//
// This screen owns its own fetch rather than taking the list as a prop — unlike periods, which
// Console hoists because several screens read them. The Period Dashboard fetches the same
// period's chart for its picker and coverage.
//
// Retiring is a flag flip and is the normal end of a code's life in a period — a retired code
// stays listed, because the period's items on it must keep resolving. Deleting exists only for a
// code created in error that has no items in this period, and the server answers 409 the moment
// that stops being true.
//
// While a retire, restore, delete, starter set or copy is in flight (`busy`), the screen holds
// the period (lib/periodHold.ts) so SWITCH PERIOD refuses until the request settles; the code
// modal takes its own hold for its save.

/** Exactly one two-click confirm at a time, as on the dashboard. Restore stays one click. */
type PendingConfirm = { kind: "retire" | "delete"; id: string } | { kind: "copy" } | null;

export default function BudgetCodes({
  period,
  periods,
  selId,
  onSelect,
}: {
  /** The entered period — the chart shown and changed here is this period's. */
  period: BudgetPeriod;
  /** Every period, for the copy panel's source picker. */
  periods: BudgetPeriod[];
  selId: string | null;
  onSelect: (id: string | null) => void;
}) {
  const periodId = period.id;
  const editable = canEditPlan(period.state);
  const stateLabel = PERIOD_STATE_LABELS[period.state];

  // null = still loading.
  const [codes, setCodes] = useState<BudgetCode[] | null>(null);
  const [owners, setOwners] = useState<BudgetOwnerOption[]>([]);
  /** The tenant-wide cost-centre register (retired included), for the code form's picker. */
  const [costCentres, setCostCentres] = useState<CostCentreRecord[] | null>(null);
  const [error, setError] = useState<{ message: string; code: string } | null>(null);
  const [editing, setEditing] = useState<BudgetCode | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [busy, setBusy] = useState(false);
  /**
   * Whether the copy panel is open on a non-empty chart. On an empty chart it is always shown
   * (it is one of the three ways out), and a copy started there keeps it open afterwards so its
   * outcome stays on screen once the list fills.
   */
  const [showCopy, setShowCopy] = useState(false);
  const [confirmAction, setConfirmAction] = useState<PendingConfirm>(null);

  usePeriodHold(busy);

  const applyLoaded = useCallback((records: BudgetCodeRecord[]) => {
    setCodes(records.map(toBudgetCode));
    setError(null);
  }, []);

  const applyLoadError = useCallback((e: unknown) => {
    setCodes((prev) => prev ?? []);
    setError(
      e instanceof ApiError
        ? { message: e.message, code: e.code }
        : { message: "Failed to load budget codes.", code: "Unknown" },
    );
  }, []);

  /** Retry handler — the mount fetch below uses then-callbacks per the Stops.tsx lint idiom. */
  const load = useCallback(() => {
    listBudgetCodes(periodId).then(applyLoaded, applyLoadError);
  }, [periodId, applyLoaded, applyLoadError]);

  useEffect(() => {
    let active = true;
    listBudgetCodes(periodId).then(
      (records) => {
        if (active) applyLoaded(records);
      },
      (e) => {
        if (active) applyLoadError(e);
      },
    );
    // The owner picker's options (tenant-wide — they are people, not codes). A failure here is
    // not worth blocking the screen for — the picker just shows "Unassigned" only.
    listBudgetOwnerCandidates().then(
      (rows) => {
        if (active) setOwners(rows);
      },
      () => {},
    );
    // The cost-centre register, tenant-wide. A failure leaves the picker offering only a code's
    // current value (the modal says the register did not load) — not worth blocking the chart.
    listCostCentres({ includeInactive: true }).then(
      (rows) => {
        if (active) setCostCentres(rows);
      },
      () => {},
    );
    return () => {
      active = false;
    };
  }, [periodId, applyLoaded, applyLoadError]);

  const list = codes ?? [];
  const loaded = codes !== null;
  const empty = loaded && list.length === 0 && !error;
  // selId can point at a code that has since been deleted — or, when Variance jumps here, at a
  // mock id that never existed (its rows come from lib/data.ts). Falling back to the first row
  // keeps the pane populated either way.
  const selected = list.find((c) => c.id === selId) ?? list[0] ?? null;
  const selectedHasChildren = selected !== null && list.some((c) => c.parentCodeId === selected.id);
  const confirmingRetire =
    selected !== null && confirmAction?.kind === "retire" && confirmAction.id === selected.id;
  const confirmingDelete =
    selected !== null && confirmAction?.kind === "delete" && confirmAction.id === selected.id;
  const copyPanelShown = editable && loaded && (showCopy || list.length === 0);

  function handleSaved(records: BudgetCodeRecord[], id: string) {
    setCodes(records.map(toBudgetCode));
    setError(null);
    onSelect(id);
  }

  function selectCode(id: string) {
    onSelect(id);
    setConfirmAction(null); // a pending confirmation never survives a selection change
  }

  function openCreate() {
    setConfirmAction(null);
    setEditing(null);
    setShowForm(true);
  }

  async function runAction(action: () => Promise<unknown>) {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      await action();
      // Re-read rather than patching local state, so the row reflects what the server stored.
      applyLoaded(await listBudgetCodes(periodId));
    } catch (e) {
      applyLoadError(e);
    } finally {
      setBusy(false);
    }
  }

  async function toggleActive(target: BudgetCode) {
    if (target.active) {
      if (confirmAction?.kind !== "retire" || confirmAction.id !== target.id) {
        setConfirmAction({ kind: "retire", id: target.id });
        return;
      }
      setConfirmAction(null);
    }
    await runAction(() => setBudgetCodeActive(periodId, target.id, !target.active));
  }

  async function seedStarterSet() {
    if (busy) return;
    const before = list.length;
    setConfirmAction(null);
    setBusy(true);
    setError(null);
    try {
      const { created } = await seedStarterBudgetCodes(periodId);
      // A bulk write needs a COUNT predicate — and none at all when nothing was created, since
      // "at least 0 more rows" is satisfied by the stale read anyway.
      const rows = await refetchUntil(
        () => listBudgetCodes(periodId),
        (rows) => rows.length >= before + created,
      );
      applyLoaded(rows);
    } catch (e) {
      applyLoadError(e);
    } finally {
      setBusy(false);
    }
  }

  async function confirmDelete(target: BudgetCode) {
    if (confirmAction?.kind !== "delete" || confirmAction.id !== target.id) {
      setConfirmAction({ kind: "delete", id: target.id });
      return;
    }
    setConfirmAction(null);
    // A 409 (children, or the code has items in this period) surfaces through applyLoadError
    // with the server's own message, which already names retirement as the alternative.
    await runAction(() => deleteBudgetCode(periodId, target.id));
    onSelect(null);
  }

  /**
   * Seed this period's chart from another period's. Returns the server's counts for the panel to
   * report, or null when refused — the banner then carries the server's own message
   * (CopySourceRequired / CopySourceIsTarget / Period.NotFound / PeriodNotEditable /
   * CopySourceNotFound), which names the rule.
   */
  async function runCopy(sourcePeriodId: string): Promise<BudgetCodeCopyResult | null> {
    if (busy) return null;
    const before = list.length;
    setConfirmAction(null);
    setShowCopy(true);
    setBusy(true);
    setError(null);
    try {
      const result = await copyBudgetCodes(periodId, { sourcePeriodId });
      // Skip the refetch when nothing was copied: a count predicate can never be satisfied by a
      // successful no-op, and the button would hang until the retry loop gave up.
      if (result.copied > 0) {
        const rows = await refetchUntil(
          () => listBudgetCodes(periodId),
          (rows) => rows.length >= before + result.copied,
        );
        applyLoaded(rows);
      }
      return result;
    } catch (e) {
      applyLoadError(e);
      return null;
    } finally {
      setBusy(false);
    }
  }

  return (
    <Screen
      eyebrow={`Planning · ${period.label}`}
      title="Budget Codes"
      right={
        editable && list.length > 0 ? (
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <ActionButton
              onClick={() => {
                setConfirmAction(null);
                setShowCopy((v) => !v);
              }}
              disabled={busy}
            >
              {showCopy ? "HIDE COPY" : "COPY FROM A PERIOD"}
            </ActionButton>
            <ActionButton variant="primary" onClick={openCreate}>
              + NEW CODE
            </ActionButton>
          </div>
        ) : undefined
      }
    >
      {!editable && (
        <div style={{ marginBottom: 12 }}>
          <EmptyNote>
            {period.label} is {stateLabel}; its budget codes are read-only. A period&apos;s codes
            can be added, changed, retired or deleted only while it is Draft or Open.
          </EmptyNote>
        </div>
      )}

      {error && (
        <div style={{ marginBottom: 12 }}>
          <ErrorNotice title="Budget codes" message={error.message} code={error.code} />
          <div style={{ marginTop: 9 }}>
            <ActionButton onClick={load}>RETRY</ActionButton>
          </div>
        </div>
      )}

      {codes === null && !error && <EmptyNote>Loading budget codes…</EmptyNote>}

      {empty && (
        <div style={{ marginBottom: 12 }}>
          <EmptyNote>
            {editable
              ? `No budget codes in ${period.label} yet — every budget item is tagged to a code, so the chart comes first. Copy it from an earlier period, load the starter set, or add a code by hand.`
              : `No budget codes in ${period.label}.`}
          </EmptyNote>
        </div>
      )}

      {/* Rendered at one fixed position for both the empty and the filled chart, so a copy made
          from the empty state keeps its outcome on screen once the list fills in. */}
      {copyPanelShown && (
        <CopyCodesPanel
          period={period}
          periods={periods}
          busy={busy}
          confirming={confirmAction?.kind === "copy"}
          onRequestConfirm={() => setConfirmAction({ kind: "copy" })}
          onCancelConfirm={() => setConfirmAction(null)}
          onCopy={runCopy}
        />
      )}

      {empty && editable && (
        <div style={{ display: "flex", flexDirection: "column", gap: 10, marginBottom: 12 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
            <ActionButton onClick={() => void seedStarterSet()} disabled={busy}>
              {busy ? "WORKING…" : `LOAD THE STARTER SET INTO ${period.label.toUpperCase()}`}
            </ActionButton>
            <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
              Creates a starter chart covering each service line and the main cost categories.
              Every code can then be edited or retired.
            </span>
          </div>
          <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
            <ActionButton variant="primary" onClick={openCreate} disabled={busy}>
              + NEW CODE
            </ActionButton>
            <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
              Start from nothing and add each code yourself.
            </span>
          </div>
        </div>
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
              const active = selected?.id === c.id;
              return (
                <div
                  key={c.id}
                  onClick={() => selectCode(c.id)}
                  style={{ ...rowSurface(active), padding: "11px 13px" }}
                >
                  <div style={{ display: "flex", alignItems: "center", gap: 9, marginBottom: 4 }}>
                    <MonoTag>{c.code}</MonoTag>
                    <StatusChip kind={budgetCodeCategoryKind(c.category)} label={c.category} />
                    {!c.active && <StatusChip kind="off" label="Retired" />}
                  </div>
                  <div
                    style={{
                      fontFamily: fonts.body,
                      fontWeight: 600,
                      fontSize: 12.5,
                      color: colors.textPrimary,
                    }}
                  >
                    {c.name}
                  </div>
                  <div
                    style={{
                      fontFamily: fonts.body,
                      fontSize: 11,
                      color: colors.textDim,
                      marginTop: 2,
                    }}
                  >
                    {c.serviceLine ? SERVICE_LINE_LABELS[c.serviceLine] : "No service line"}
                    {/* Makes the one-level hierarchy visible in an otherwise flat list. */}
                    {c.parentCode && ` · ↳ ${c.parentCode}`}
                  </div>
                </div>
              );
            })}
          </div>

          <div
            style={{ overflowY: "auto", padding: "22px 0 22px 26px", background: colors.detailBg }}
          >
            {selected ? (
              <>
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: 10,
                    marginBottom: 14,
                    flexWrap: "wrap",
                  }}
                >
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
                  {editable && (
                    <>
                      <ActionButton
                        onClick={() => {
                          setConfirmAction(null);
                          setEditing(selected);
                          setShowForm(true);
                        }}
                      >
                        EDIT
                      </ActionButton>
                      <ActionButton
                        variant={selected.active ? "amber" : "success"}
                        onClick={() => toggleActive(selected)}
                        disabled={busy}
                      >
                        {busy
                          ? "WORKING…"
                          : !selected.active
                            ? "RESTORE"
                            : confirmingRetire
                              ? "CONFIRM RETIRE"
                              : "RETIRE"}
                      </ActionButton>
                      <ActionButton
                        variant="destructive"
                        onClick={() => confirmDelete(selected)}
                        disabled={busy}
                      >
                        {confirmingDelete ? "CONFIRM DELETE" : "DELETE"}
                      </ActionButton>
                    </>
                  )}
                </div>

                {confirmingRetire && (
                  <ConfirmNote>
                    Retiring {selected.code} in {period.label} changes {period.label}&apos;s chart
                    only — other periods keep their own {selected.code}. Items already planned on
                    it here stay and still count, but no item in {period.label} can be added to it
                    or changed on it until it is restored. Click CONFIRM RETIRE to proceed;
                    selecting another code cancels.
                  </ConfirmNote>
                )}

                {confirmingDelete && (
                  <ConfirmNote>
                    Deleting {selected.code} from {period.label} is permanent and is only for a
                    code created in error; other periods keep their own {selected.code}. If it has
                    budget items in {period.label}, retire it instead — click anything else to
                    cancel.
                  </ConfirmNote>
                )}

                {selected.description && (
                  <Panel style={{ marginBottom: 12 }}>
                    <SectionLabel>Description</SectionLabel>
                    <div
                      style={{
                        fontFamily: fonts.body,
                        fontSize: 12.5,
                        color: colors.textSecondary,
                        lineHeight: 1.65,
                      }}
                    >
                      {selected.description}
                    </div>
                  </Panel>
                )}

                <Panel style={{ marginBottom: 12 }}>
                  <SectionLabel>Classification</SectionLabel>
                  <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                    <DetailRow label="Category" value={selected.category} />
                    <DetailRow
                      label="Service line"
                      value={
                        selected.serviceLine ? SERVICE_LINE_LABELS[selected.serviceLine] : "Unassigned"
                      }
                    />
                    {costCentreApplies(selected.category) && (
                      <DetailRow label="Cost centre" value={costCentreDisplay(selected.costCentre, costCentres)} />
                    )}
                    <DetailRow
                      label="Parent code"
                      value={
                        selected.parentCode
                          ? `${selected.parentCode} · ${selected.parentName ?? ""}`
                          : "Top level"
                      }
                    />
                  </div>
                </Panel>

                <Panel style={{ marginBottom: 12 }}>
                  <SectionLabel>Accounting</SectionLabel>
                  <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                    <DetailRow label="GL account code" value={selected.glAccountCode ?? "—"} />
                    <DetailRow
                      label="Tax treatment"
                      value={
                        selected.taxTreatment ? TAX_TREATMENT_LABELS[selected.taxTreatment] : "—"
                      }
                    />
                  </div>
                </Panel>

                <Panel>
                  <SectionLabel>Governance</SectionLabel>
                  <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                    <DetailRow
                      label="Budget owner"
                      value={userDisplay(
                        selected.budgetOwnerName,
                        selected.budgetOwnerEmail,
                        "Unassigned",
                      )}
                    />
                    <DetailRow
                      label="Review frequency"
                      value={REVIEW_FREQUENCY_LABELS[selected.reviewFrequency]}
                    />
                    <DetailRow
                      label="Status"
                      value={
                        <StatusChip
                          kind={selected.active ? "ontime" : "off"}
                          label={selected.active ? "Active" : "Retired"}
                        />
                      }
                    />
                    <DetailRow
                      label="Created by"
                      value={userDisplay(selected.createdByName, selected.createdByEmail, "—")}
                    />
                    <DetailRow
                      label="Last modified by"
                      value={userDisplay(selected.modifiedByName, selected.modifiedByEmail, "—")}
                    />
                  </div>
                </Panel>

                {!selected.active && (
                  <Note>
                    Retired codes stay listed on purpose — {period.label}&apos;s items already
                    tagged with this code still resolve to it.
                  </Note>
                )}

                {selectedHasChildren && (
                  <Note>
                    Other codes roll up into this one. Retiring it does not retire them, and it
                    cannot be deleted while they point at it.
                  </Note>
                )}
              </>
            ) : (
              <EmptyNote>No code selected.</EmptyNote>
            )}
          </div>
        </div>
      )}

      {showForm && (
        <BudgetCodeFormModal
          periodId={periodId}
          periodLabel={period.label}
          code={editing}
          allCodes={list}
          owners={owners}
          costCentres={costCentres}
          onCostCentresChanged={setCostCentres}
          onClose={() => setShowForm(false)}
          onSaved={handleSaved}
        />
      )}
    </Screen>
  );
}

/**
 * A code's cost centre as "THOMPSON · Thompson base", with the register's name when it resolves
 * (ordinal match, as the server joins) and "(retired)" when that entry is retired.
 */
function costCentreDisplay(value: string | null, register: CostCentreRecord[] | null): string {
  if (!value) return "—";
  const entry = findCostCentre(register ?? [], value);
  if (!entry) return value;
  return `${entry.code} · ${entry.name}${entry.isActive ? "" : " (retired)"}`;
}

/** The text under a pending two-click confirm, naming what the second click will do. */
function ConfirmNote({ children }: { children: React.ReactNode }) {
  return (
    <div
      style={{
        marginBottom: 12,
        fontFamily: fonts.body,
        fontSize: 11.5,
        color: colors.textSecondary,
        lineHeight: 1.6,
      }}
    >
      {children}
    </div>
  );
}

/** A quiet explanatory line under the detail panels. */
function Note({ children }: { children: React.ReactNode }) {
  return (
    <div
      style={{
        marginTop: 10,
        fontFamily: fonts.body,
        fontSize: 11.5,
        color: colors.textDim,
        lineHeight: 1.6,
      }}
    >
      {children}
    </div>
  );
}
