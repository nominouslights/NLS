"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, rowSurface } from "@/lib/theme";
import type { BudgetCode } from "@/lib/types";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { DetailRow, Panel, SectionLabel } from "@/components/ui/Panel";
import { ApiError } from "@/lib/api/transport";
import {
  budgetCodeCategoryKind,
  deleteBudgetCode,
  listBudgetCodes,
  listBudgetOwnerCandidates,
  seedStarterBudgetCodes,
  setBudgetCodeActive,
  toBudgetCode,
  REVIEW_FREQUENCY_LABELS,
  SERVICE_LINE_LABELS,
  TAX_TREATMENT_LABELS,
  type BudgetCodeRecord,
  type BudgetOwnerOption,
} from "@/lib/api/budgeting";
import { ErrorNotice } from "@/components/ErrorNotice";
import BudgetCodeFormModal from "@/components/BudgetCodeFormModal";
import { EmptyNote, Screen } from "@/components/screens/shared";

// Master/detail on real data (GET/POST/PUT/DELETE /api/budgeting/codes), following Dispatcher's
// Clients and Trips screens: a left column of rows and a right detail pane on the tinted
// detailBg, split by a CSS grid with a top border.
//
// This screen owns its own fetch rather than taking the list as a prop — unlike periods, which
// Console hoists because five screens read them. Codes are read here and nowhere else; the four
// screens that show a code name still read the mock lookup in lib/data.ts until their own Stage
// 6.1 slices land.
//
// Retiring is a flag flip and is the normal end of a code's life — a retired code stays listed,
// because last period's allocations and actuals reference it by string and must keep resolving.
// Deleting exists only for a code created in error that nothing has ever used, and the server
// answers 409 the moment that stops being true.

export default function BudgetCodes({
  selId,
  onSelect,
}: {
  selId: string | null;
  onSelect: (id: string | null) => void;
}) {
  // null = still loading.
  const [codes, setCodes] = useState<BudgetCode[] | null>(null);
  const [owners, setOwners] = useState<BudgetOwnerOption[]>([]);
  const [error, setError] = useState<{ message: string; code: string } | null>(null);
  const [editing, setEditing] = useState<BudgetCode | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [busy, setBusy] = useState(false);
  /** Two-click delete: holds the id awaiting confirmation, so a stray click cannot destroy a code. */
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);

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
    listBudgetCodes().then(applyLoaded, applyLoadError);
  }, [applyLoaded, applyLoadError]);

  useEffect(() => {
    let active = true;
    listBudgetCodes().then(
      (records) => {
        if (active) applyLoaded(records);
      },
      (e) => {
        if (active) applyLoadError(e);
      },
    );
    // The owner picker's options. A failure here is not worth blocking the screen for — the
    // picker just shows "Unassigned" only, and every other field still works.
    listBudgetOwnerCandidates().then(
      (rows) => {
        if (active) setOwners(rows);
      },
      () => {},
    );
    return () => {
      active = false;
    };
  }, [applyLoaded, applyLoadError]);

  const list = codes ?? [];
  // selId can point at a code that no longer exists — or, when Allocations jumps here, at a mock
  // id that never will. Falling back to the first row keeps the pane populated either way.
  const selected = list.find((c) => c.id === selId) ?? list[0] ?? null;
  const selectedHasChildren = selected !== null && list.some((c) => c.parentCodeId === selected.id);

  function handleSaved(records: BudgetCodeRecord[], id: string) {
    setCodes(records.map(toBudgetCode));
    setError(null);
    onSelect(id);
  }

  function selectCode(id: string) {
    onSelect(id);
    setConfirmDeleteId(null); // a pending confirmation never survives a selection change
  }

  async function runAction(action: () => Promise<unknown>) {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      await action();
      // Re-read rather than patching local state, so the row reflects what the server stored.
      applyLoaded(await listBudgetCodes());
    } catch (e) {
      applyLoadError(e);
    } finally {
      setBusy(false);
    }
  }

  const toggleActive = (target: BudgetCode) =>
    runAction(() => setBudgetCodeActive(target.id, !target.active));

  const seedStarterSet = () => runAction(seedStarterBudgetCodes);

  async function confirmDelete(target: BudgetCode) {
    if (confirmDeleteId !== target.id) {
      setConfirmDeleteId(target.id);
      return;
    }
    setConfirmDeleteId(null);
    // A 409 (children, or the code has been used) surfaces through applyLoadError with the
    // server's own message, which already names retirement as the alternative.
    await runAction(() => deleteBudgetCode(target.id));
    onSelect(null);
  }

  return (
    <Screen
      eyebrow="Planning"
      title="Budget Codes"
      right={
        <ActionButton
          variant="primary"
          onClick={() => {
            setEditing(null);
            setShowForm(true);
          }}
        >
          + NEW CODE
        </ActionButton>
      }
    >
      {error && (
        <div style={{ marginBottom: 12 }}>
          <ErrorNotice title="Budget codes" message={error.message} code={error.code} />
          <div style={{ marginTop: 9 }}>
            <ActionButton onClick={load}>RETRY</ActionButton>
          </div>
        </div>
      )}

      {codes === null && !error && <EmptyNote>Loading budget codes…</EmptyNote>}

      {codes !== null && list.length === 0 && !error && (
        <div>
          <EmptyNote>
            No budget codes yet — create one so allocations and actuals have something to tag.
          </EmptyNote>
          <div style={{ marginTop: 12, display: "flex", alignItems: "center", gap: 10 }}>
            <ActionButton onClick={seedStarterSet} disabled={busy}>
              {busy ? "ADDING…" : "START FROM THE STANDARD SET"}
            </ActionButton>
            <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
              Creates a starter chart covering each service line and the main cost categories.
              Every code can then be edited or retired.
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
                  <ActionButton
                    onClick={() => {
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
                    {busy ? "WORKING…" : selected.active ? "RETIRE" : "RESTORE"}
                  </ActionButton>
                  <ActionButton
                    variant="destructive"
                    onClick={() => confirmDelete(selected)}
                    disabled={busy}
                  >
                    {confirmDeleteId === selected.id ? "CONFIRM DELETE" : "DELETE"}
                  </ActionButton>
                </div>

                {confirmDeleteId === selected.id && (
                  <div
                    style={{
                      marginBottom: 12,
                      fontFamily: fonts.body,
                      fontSize: 11.5,
                      color: colors.textSecondary,
                    }}
                  >
                    Deleting is permanent and is only for a code created in error. If this code has
                    ever been used, retire it instead — click anything else to cancel.
                  </div>
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
                    <DetailRow label="Cost centre" value={selected.costCentre ?? "—"} />
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
                    <DetailRow label="Budget owner" value={selected.budgetOwnerEmail ?? "Unassigned"} />
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
                    <DetailRow label="Created by" value={selected.createdByEmail ?? "—"} />
                    <DetailRow label="Last modified by" value={selected.modifiedByEmail ?? "—"} />
                  </div>
                </Panel>

                {!selected.active && (
                  <Note>
                    Retired codes stay listed on purpose — allocations and actuals already tagged
                    with this code still resolve to it.
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
              <EmptyNote>No budget codes defined for this tenant yet.</EmptyNote>
            )}
          </div>
        </div>
      )}

      {showForm && (
        <BudgetCodeFormModal
          code={editing}
          allCodes={list}
          owners={owners}
          onClose={() => setShowForm(false)}
          onSaved={handleSaved}
        />
      )}
    </Screen>
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
