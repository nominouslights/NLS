"use client";

import { useCallback, useEffect, useId, useMemo, useState } from "react";
import { colors, fonts, rowSurface } from "@/lib/theme";
import type { BudgetCode } from "@/lib/types";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { DetailRow, Panel, SectionLabel } from "@/components/ui/Panel";
import { TextField } from "@/components/ui/Field";
import { ApiError } from "@/lib/api/transport";
import {
  budgetCodeCategoryKind,
  budgetCodeGroupKey,
  costCentreApplies,
  deleteBudgetCode,
  groupBudgetCodesByCostCentre,
  listBudgetCodes,
  listBudgetOwnerCandidates,
  matchesBudgetCodeQuery,
  seedStarterBudgetCodes,
  setBudgetCodeActive,
  toBudgetCode,
  userDisplay,
  REVIEW_FREQUENCY_LABELS,
  SERVICE_LINE_LABELS,
  TAX_TREATMENT_LABELS,
  type BudgetCodeGroup,
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
// Console hoists because five screens read them. The period dashboard and Actuals vs Budget fetch
// the chart too (for their pickers, coverage and rollup); the two still-mock screens (Variance,
// Reports) read the lookup in lib/data.ts until their slice lands.
//
// Retiring is a flag flip and is the normal end of a code's life — a retired code stays listed,
// because last period's allocations and actuals reference it by string and must keep resolving.
// Deleting exists only for a code created in error that nothing has ever used, and the server
// answers 409 the moment that stops being true.
//
// **Retiring is no longer unconditional either.** The server refuses a deactivate with 409
// Budgeting.Code.PeriodNotClosed while any period that plans or spends against the code is not
// Closed, and that message is the only thing naming *which* period must close — so a failed
// action carries the server's text under its own heading, rather than the load banner's "Budget
// codes / RETRY", which would invite the user to retry a load they never asked for.
//
// **The list is grouped by cost centre** (groupBudgetCodesByCostCentre in lib/api/budgeting.ts),
// so a growing chart is not one long scroll. Cost centres are free text, so the grouping key is
// case-folded — `Fleet` and `FLEET` are one group, labelled with the most common spelling, and the
// header says "· N spellings" rather than hiding the drift (the server keeps the exact strings).
// Named cost centres come A–Z, then "No cost centre" (expense codes still to be filed), then
// Revenue, which sits in its own bucket because costCentreApplies says a revenue code never has
// one. Groups start collapsed; the selected code's group is always open (derived, not set in an
// effect — so the detail pane always matches a visible card); and typing in the search box opens
// every group. The search matches code or name only, retired codes included, because the
// server's uniqueness check counts them too. Open/closed state is component state only.

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
  /** `retry` distinguishes a failed load (offer RETRY) from a refused action (offer DISMISS). */
  const [error, setError] = useState<{
    title: string;
    message: string;
    code: string;
    retry: boolean;
  } | null>(null);
  const [editing, setEditing] = useState<BudgetCode | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [busy, setBusy] = useState(false);
  /** Two-click delete: holds the id awaiting confirmation, so a stray click cannot destroy a code. */
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);
  /** The code/name search. While it is non-blank every group is forced open. */
  const [query, setQuery] = useState("");
  /** Group keys the user opened. Empty = all collapsed; the selected group opens regardless. */
  const [expanded, setExpanded] = useState<Set<string>>(() => new Set());

  const applyLoaded = useCallback((records: BudgetCodeRecord[]) => {
    setCodes(records.map(toBudgetCode));
    setError(null);
  }, []);

  const applyLoadError = useCallback((e: unknown) => {
    setCodes((prev) => prev ?? []);
    setError(
      e instanceof ApiError
        ? { title: "Budget codes", message: e.message, code: e.code, retry: true }
        : { title: "Budget codes", message: "Failed to load budget codes.", code: "Unknown", retry: true },
    );
  }, []);

  /**
   * A refused action, shown under its own heading with the server's message verbatim. That text
   * is load-bearing for two refusals in particular: PeriodNotClosed names the periods that must
   * close before the code can be retired, and InUse names retirement as the alternative to a
   * delete. Paraphrasing either would leave the user with no way to find the way out.
   */
  const applyActionError = useCallback((title: string, e: unknown) => {
    setError(
      e instanceof ApiError
        ? { title, message: e.message, code: e.code, retry: false }
        : { title, message: "Something went wrong — please try again.", code: "Unknown", retry: false },
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

  const list = useMemo(() => codes ?? [], [codes]);
  const groups = useMemo(() => groupBudgetCodesByCostCentre(list), [list]);
  // selId can point at a code that has since been deleted — or, when Variance jumps here, at a
  // mock id that never existed (Allocations jumps with real ids now). Falling back to the first
  // card of the first group (not list[0], which may sit in a later group) keeps the pane
  // populated with a card that is on screen. Display-only: onSelect is not called, and the
  // fallback is never *named* as selected ("· selected", the hidden-by-search line) — nobody
  // picked it.
  const explicitSelection = list.find((c) => c.id === selId) ?? null;
  const selected = explicitSelection ?? groups[0]?.codes[0] ?? null;
  const selectedKey = selected ? budgetCodeGroupKey(selected) : null;
  const searching = query.trim().length > 0;
  // Search filters inside the full-list groups rather than regrouping the matches, so a group's
  // label, its "· N spellings" note and its "n of total" stay those of the whole group.
  const visibleGroups = useMemo(
    () =>
      searching
        ? groups
            .map((g) => ({ ...g, codes: g.codes.filter((c) => matchesBudgetCodeQuery(c, query)) }))
            .filter((g) => g.codes.length > 0)
        : groups,
    [searching, query, groups],
  );
  const selectedVisible =
    selected !== null && visibleGroups.some((g) => g.codes.some((c) => c.id === selected.id));
  const groupIdPrefix = useId();
  /** Group size before the search, for the "n of total" count. */
  const totalByKey = useMemo(() => new Map(groups.map((g) => [g.key, g.codes.length])), [groups]);

  const isOpen = (key: string) => searching || expanded.has(key) || key === selectedKey;

  function toggleGroup(key: string) {
    // While searching every group is forced open; a click then must not leak into the collapsed
    // state the user gets back when the search is cleared.
    if (searching) return;
    setExpanded((prev) => {
      const next = new Set(prev);
      // The selected group is open by derivation alone; pin it into the set first so toggling a
      // different group never makes it jump shut once the selection moves elsewhere.
      if (selectedKey !== null) next.add(selectedKey);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }
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

  /** Resolves true when the action succeeded, false when it was refused (or already busy). */
  async function runAction(title: string, action: () => Promise<unknown>): Promise<boolean> {
    if (busy) return false;
    setBusy(true);
    setError(null);
    try {
      await action();
      // Re-read rather than patching local state, so the row reflects what the server stored.
      applyLoaded(await listBudgetCodes());
      return true;
    } catch (e) {
      applyActionError(title, e);
      return false;
    } finally {
      setBusy(false);
    }
  }

  // A refused retire is the interesting one: 409 Budgeting.Code.PeriodNotClosed names every
  // period still using the code, and that list is the whole instruction for what to do next.
  const toggleActive = (target: BudgetCode) =>
    runAction(target.active ? "Retire budget code" : "Restore budget code", () =>
      setBudgetCodeActive(target.id, !target.active),
    );

  const seedStarterSet = () => runAction("Starter set", seedStarterBudgetCodes);

  async function confirmDelete(target: BudgetCode) {
    if (confirmDeleteId !== target.id) {
      setConfirmDeleteId(target.id);
      return;
    }
    setConfirmDeleteId(null);
    // A 409 (children, or the code has been used) surfaces with the server's own message, which
    // already names retirement as the alternative.
    // Only a successful delete clears the selection — a refused one keeps the pane on the code
    // the error banner is talking about.
    if (await runAction("Delete budget code", () => deleteBudgetCode(target.id))) onSelect(null);
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
          <ErrorNotice title={error.title} message={error.message} code={error.code} />
          <div style={{ marginTop: 9 }}>
            {error.retry ? (
              <ActionButton onClick={load}>RETRY</ActionButton>
            ) : (
              <ActionButton onClick={() => setError(null)}>DISMISS</ActionButton>
            )}
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
              Every code can then be edited, and retired once the periods using it have closed.
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
              display: "flex",
              flexDirection: "column",
              minHeight: 0,
              borderRight: `1px solid ${colors.border}`,
            }}
          >
            <div style={{ flexShrink: 0, padding: "16px 18px 10px 0" }}>
              <TextField
                label="Find a code"
                value={query}
                onChange={setQuery}
                placeholder="Code or name"
              />
            </div>
            <div
              style={{
                flex: 1,
                minHeight: 0,
                overflowY: "auto",
                padding: "6px 18px 16px 0",
                display: "flex",
                flexDirection: "column",
                gap: 8,
              }}
            >
              {searching && explicitSelection !== null && !selectedVisible && (
                <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
                  {explicitSelection.code} is selected but not in these results.
                </div>
              )}

              {searching && visibleGroups.length === 0 && (
                <EmptyNote>
                  No code or name matches “{query.trim()}” — retired codes included, so it is free
                  to create.
                </EmptyNote>
              )}

              {visibleGroups.map((g, index) => {
                const open = isOpen(g.key);
                // Never derived from the cost centre: free text can hold spaces, quotes or `#`.
                const panelId = `${groupIdPrefix}-group-${index}`;
                return (
                  <div key={g.key} style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                    <GroupHeader
                      group={g}
                      open={open}
                      panelId={panelId}
                      isSelectedGroup={
                        explicitSelection !== null &&
                        g.codes.some((c) => c.id === explicitSelection.id)
                      }
                      locked={searching || g.key === selectedKey}
                      total={searching ? (totalByKey.get(g.key) ?? g.codes.length) : null}
                      onToggle={() => toggleGroup(g.key)}
                    />
                    {open && (
                      <div id={panelId} style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                        {g.codes.map((c) => {
                          const active = selected?.id === c.id;
                          return (
                            <div
                              key={c.id}
                              role="button"
                              tabIndex={0}
                              aria-pressed={active}
                              onClick={() => selectCode(c.id)}
                              onKeyDown={(e) => {
                                if (e.key === "Enter" || e.key === " ") {
                                  e.preventDefault();
                                  selectCode(c.id);
                                }
                              }}
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
                                {/* Keeps the one-level hierarchy visible — a child may sit in a
                                    different cost-centre group than its parent. */}
                                {c.parentCode && ` · ↳ ${c.parentCode}`}
                              </div>
                            </div>
                          );
                        })}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
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
                    ever been used, retire it instead — which the server allows once every period
                    using it is closed. Click anything else to cancel.
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
                    {costCentreApplies(selected.category) && (
                      <DetailRow label="Cost centre" value={selected.costCentre ?? "—"} />
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

                {selected.active ? (
                  <Note>
                    Retiring is the normal end of a code&apos;s life, but not at any moment: the server
                    refuses it while a period that plans or spends against this code is still
                    open, and the refusal names the periods to close first. A Draft or Open period
                    can release the code sooner by removing its line.
                  </Note>
                ) : (
                  <Note>
                    Retired codes stay listed on purpose — allocations and actuals already tagged
                    with this code still resolve to it. A transaction already on this code stays
                    correctable; no new one can be recorded against it.
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

/**
 * A collapsible group's header: a real button (so Tab reaches it and Space/Enter toggle it) with
 * the browser's button styling reset. Deliberately colourless — no status colour and no category
 * chip, even on the Revenue bucket, because colour must never carry meaning on its own.
 * Cost-centre labels keep the user's own casing (no textTransform): the label is one of their
 * spellings, and uppercasing it would hide which one.
 */
function GroupHeader({
  group,
  open,
  panelId,
  isSelectedGroup,
  locked,
  total,
  onToggle,
}: {
  group: BudgetCodeGroup;
  open: boolean;
  panelId: string;
  isSelectedGroup: boolean;
  /** Forced open (a search is typed, or it holds the selected code): it does not toggle, and
   *  says so with aria-disabled rather than claiming to. */
  locked: boolean;
  /** The group's size before the search, or null when not searching. */
  total: number | null;
  onToggle: () => void;
}) {
  const [focused, setFocused] = useState(false);
  const tags = [
    isSelectedGroup && "· selected",
    group.spellings.length > 1 && `· ${group.spellings.length} spellings`,
    group.kind === "category" && "· cost centres don't apply",
  ].filter((t): t is string => typeof t === "string");
  const labelStyle: React.CSSProperties =
    group.kind === "costCentre"
      ? { fontFamily: fonts.body, fontWeight: 600, fontSize: 12, color: colors.textPrimary }
      : {
          // The SectionLabel look, inline — SectionLabel itself carries a marginBottom.
          fontFamily: fonts.semiCondensed,
          fontSize: 9.5,
          letterSpacing: ".14em",
          textTransform: "uppercase",
          color: colors.textLabel,
        };
  return (
    <button
      type="button"
      aria-expanded={open}
      // The panel is only rendered while open, so a collapsed header points at nothing.
      aria-controls={open ? panelId : undefined}
      aria-disabled={locked || undefined}
      onClick={locked ? undefined : onToggle}
      onFocus={() => setFocused(true)}
      onBlur={() => setFocused(false)}
      style={{
        all: "unset",
        boxSizing: "border-box",
        display: "flex",
        alignItems: "center",
        gap: 8,
        width: "100%",
        cursor: locked ? "default" : "pointer",
        padding: "8px 2px 6px",
        borderBottom: `1px solid ${colors.border}`,
        outline: focused ? `2px solid ${colors.borderActive}` : "none",
        outlineOffset: 2,
        borderRadius: focused ? 4 : 0,
      }}
    >
      <span style={labelStyle}>{group.label}</span>
      {tags.map((t) => (
        <span key={t} style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim }}>
          {t}
        </span>
      ))}
      <span
        style={{
          marginLeft: "auto",
          fontFamily: fonts.mono,
          fontSize: 11,
          color: colors.textDim,
          fontVariantNumeric: "tabular-nums",
        }}
      >
        {total === null ? group.codes.length : `${group.codes.length} of ${total}`}
      </span>
      <span aria-hidden="true" style={{ fontSize: 10, color: colors.textDim, width: 10 }}>
        {open ? "▾" : "▸"}
      </span>
    </button>
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
