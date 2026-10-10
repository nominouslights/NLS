import type { StatusKind } from "@/lib/theme";
import {
  sumCad,
  type CostCentreInput,
  type CostCentreRecord,
  type CostCentreRollupRow,
} from "@/lib/api/budgeting";

// ---------------------------------------------------------------------------
// The cost-centre register's rules, mirrored client-side — pure, no DOM, no transport, so each
// one is unit-tested against the C# it names (lib/costCentres.test.ts). The server re-checks all
// of it and its answer is the one that counts; these exist so a form never offers an option the
// server will refuse, and so a refusal caught early reads exactly like the server's.
//
// The one rule that differs from budget codes and is easy to get wrong: a cost-centre code is
// TRIMMED ONLY — case is preserved, and every match is ordinal (case-sensitive). Upper-casing
// here (as normalizeBudgetCode does for codes) would make "Ops-01" stop matching its own entry.
// ---------------------------------------------------------------------------

/** Mirrors CostCentre.CodeMaxLength / NameMaxLength / DescriptionMaxLength. */
export const COST_CENTRE_LIMITS = {
  codeMaxLength: 32,
  nameMaxLength: 120,
  descriptionMaxLength: 1000,
} as const;

/**
 * The server's messages, verbatim, keyed by the error code's suffix (every code is prefixed
 * "Budgeting.CostCentre."). Mirrors CostCentreErrors.
 */
export const COST_CENTRE_MESSAGES = {
  NotFound: "The cost centre was not found.",
  CodeRequired: "A cost centre needs a code.",
  CodeTooLong: "The cost-centre code must be 32 characters or fewer.",
  CodeImmutable:
    "A cost centre's code cannot be changed — budget codes carry it by that code. Retire this cost centre and create a new one instead.",
  DuplicateCode: "Another cost centre already uses that code.",
  NameRequired: "A cost centre needs a name.",
  NameTooLong: "The name must be 120 characters or fewer.",
  DescriptionTooLong: "The description must be 1000 characters or fewer.",
  OwnerNotFound: "The owner is not a user of this tenant.",
  ParentNotFound: "The parent cost centre was not found.",
  ParentIsSelf: "A cost centre cannot be its own parent.",
  ParentIsNotTopLevel:
    "That cost centre already rolls up into another. The hierarchy is one level deep, so only a top-level cost centre can be a parent.",
  ParentRetired: "That parent cost centre is retired. Choose an active one, or restore it first.",
  HasChildrenCannotHaveParent:
    "Other cost centres roll up into this one, so it cannot roll up into another. The hierarchy is one level deep.",
  HasActiveChildren:
    "Active cost centres still roll up into this one. Retire them, or move them to another parent, first.",
  HasChildren:
    "Other cost centres roll up into this one and would be left pointing at nothing. Move or delete them first, or retire this cost centre instead.",
  InUse:
    "Budget codes carry this cost centre, so it cannot be deleted. Retire it instead — a retired cost centre stays listed so existing codes keep resolving.",
} as const;

/**
 * The two budget-code refusals the register adds (prefix "Budgeting.Code."). Mirrors
 * BudgetCodeErrors.CostCentreNotFound (400) and CostCentreRetired (409).
 */
export const BUDGET_CODE_COST_CENTRE_MESSAGES = {
  CostCentreNotFound:
    "That cost centre is not in the cost-centre register. Add it to the register first, or choose an existing one.",
  CostCentreRetired:
    "That cost centre is retired and cannot be given to a budget code. Choose an active one, or restore it in the register first.",
} as const;

/**
 * Mirrors CostCentre.NormalizeCode: trim only, case preserved; null and whitespace become "".
 * Exactly how BudgetCode has always normalized its cost-centre string.
 */
export function normalizeCostCentreCode(code: string | null | undefined): string {
  return code?.trim() ?? "";
}

/** Mirrors CostCentre.Normalize for optional text: blank → null, anything else trimmed. */
export function normalizeCostCentreText(value: string | null | undefined): string | null {
  return value == null || value.trim() === "" ? null : value.trim();
}

/**
 * The first rule this entry breaks, as the server's own message, or null when it would get past
 * the checks that need no lookup. IN THE SERVER'S ORDER, which differs by mode:
 *
 *   - create (CreateCostCentreCommandHandler → CostCentre.Create): code required, code length,
 *     then Validate — name required, name length, description length;
 *   - edit (UpdateCostCentreCommandHandler): CostCentreParentRule runs BEFORE the aggregate, so
 *     ParentIsSelf comes first, then Validate. The code is never sent on edit, so no code rule.
 *
 * Cross-row rules (DuplicateCode, ParentNotFound/NotTopLevel/Retired, OwnerNotFound) need the
 * register or the user replica; parentCandidates keeps the picker from offering a bad parent.
 */
export function costCentreError(
  input: Pick<CostCentreInput, "code" | "name" | "description" | "parentId">,
  editingId: string | null = null,
): string | null {
  if (editingId !== null && input.parentId === editingId) return COST_CENTRE_MESSAGES.ParentIsSelf;
  if (editingId === null) {
    const code = normalizeCostCentreCode(input.code);
    if (code.length === 0) return COST_CENTRE_MESSAGES.CodeRequired;
    if (code.length > COST_CENTRE_LIMITS.codeMaxLength) return COST_CENTRE_MESSAGES.CodeTooLong;
  }
  if (input.name.trim().length === 0) return COST_CENTRE_MESSAGES.NameRequired;
  if (input.name.trim().length > COST_CENTRE_LIMITS.nameMaxLength) return COST_CENTRE_MESSAGES.NameTooLong;
  if ((input.description?.trim().length ?? 0) > COST_CENTRE_LIMITS.descriptionMaxLength) {
    return COST_CENTRE_MESSAGES.DescriptionTooLong;
  }
  return null;
}

/** Whether any entry (active or retired) rolls up into this one — the server's HasChildrenAsync. */
export function hasChildren(register: Pick<CostCentreRecord, "parentId">[], id: string): boolean {
  return register.some((c) => c.parentId === id);
}

/**
 * The entries that may legally be picked as `editingId`'s parent, mirroring
 * CostCentreParentRule.ValidateAsync — so the picker never offers one the server refuses:
 *
 *   - never the entry itself (ParentIsSelf);
 *   - only a top-level entry (ParentIsNotTopLevel — the hierarchy is one level deep);
 *   - only an active entry, EXCEPT the entry's current parent: keeping a parent retired since is
 *     accepted, choosing a retired one anew is ParentRetired;
 *   - nothing at all when the entry already has children (HasChildrenCannotHaveParent).
 *
 * `register` must be the WHOLE register, retired entries included — the children and
 * current-parent checks need them. On create pass `editingId: null`.
 */
export function parentCandidates(register: CostCentreRecord[], editingId: string | null): CostCentreRecord[] {
  const self = editingId === null ? null : register.find((c) => c.id === editingId) ?? null;
  if (editingId !== null && hasChildren(register, editingId)) return [];
  const currentParentId = self?.parentId ?? null;
  return register.filter(
    (c) =>
      c.id !== editingId &&
      c.parentId === null &&
      (c.isActive || c.id === currentParentId),
  );
}

/** Retire/restore status — colour + glyph + label via StatusChip, the budget-code screen's pairing. */
export function costCentreStatus(isActive: boolean): { kind: StatusKind; label: string } {
  return isActive ? { kind: "ontime", label: "Active" } : { kind: "off", label: "Retired" };
}

/** The register entry carrying exactly this (normalized, ordinal) code, if any. */
export function findCostCentre<T extends Pick<CostCentreRecord, "code">>(
  register: T[],
  code: string | null | undefined,
): T | null {
  const wanted = normalizeCostCentreCode(code);
  if (wanted.length === 0) return null;
  return register.find((c) => c.code === wanted) ?? null;
}

/**
 * The budget code's cost-centre refusal for this value, or null when the server accepts it.
 * Mirrors BudgetCodeCostCentreRule.ValidateAsync: blank → fine; UNCHANGED from the code's stored
 * value (ordinal) → always fine, even if retired or gone from the register; otherwise not in the
 * register → CostCentreNotFound, retired → CostCentreRetired. (Revenue codes and over-length
 * values are left to the aggregate, as on the server.)
 */
export function budgetCodeCostCentreError(
  requested: string | null | undefined,
  current: string | null,
  register: Pick<CostCentreRecord, "code" | "isActive">[],
): string | null {
  const wanted = normalizeCostCentreCode(requested);
  if (wanted.length === 0 || wanted.length > COST_CENTRE_LIMITS.codeMaxLength) return null;
  if (current !== null && wanted === current) return null;
  const entry = findCostCentre(register, wanted);
  if (!entry) return BUDGET_CODE_COST_CENTRE_MESSAGES.CostCentreNotFound;
  return entry.isActive ? null : BUDGET_CODE_COST_CENTRE_MESSAGES.CostCentreRetired;
}

/** One option of the budget-code form's cost-centre picker. */
export interface CostCentreOption {
  /** The code string the budget code will carry — exactly as the register (or the code) spells it. */
  value: string;
  label: string;
  /** "active" for a register entry on offer; the other two only for the code's own current value. */
  status: "active" | "retired" | "unregistered";
}

/**
 * The budget-code form's cost-centre options: every ACTIVE register entry, in register (code)
 * order — plus, when the code being edited already carries a value that is retired or matches no
 * entry, that value, kept selectable and marked. BudgetCodeCostCentreRule accepts an unchanged
 * value unconditionally, so dropping it would force a planner editing the NAME to also change the
 * cost centre. Every option offered is one budgetCodeCostCentreError accepts.
 */
export function costCentreOptions(register: CostCentreRecord[], current: string | null): CostCentreOption[] {
  const options: CostCentreOption[] = register
    .filter((c) => c.isActive)
    .map((c) => ({ value: c.code, label: `${c.code} · ${c.name}`, status: "active" as const }));
  const kept = normalizeCostCentreCode(current);
  if (kept.length > 0 && !options.some((o) => o.value === kept)) {
    const entry = findCostCentre(register, kept);
    options.unshift(
      entry
        ? { value: kept, label: `${kept} · ${entry.name} (retired — kept)`, status: "retired" }
        : { value: kept, label: `${kept} (not in the register — kept)`, status: "unregistered" },
    );
  }
  return options;
}

/**
 * The post-save refetch predicate (the itemReflects pattern): the row with this id is visible and
 * carries the values just written. On edit the row was always there, so existence alone proves
 * nothing.
 */
export function costCentreReflects(
  rows: CostCentreRecord[],
  id: string,
  expected: Pick<CostCentreRecord, "name" | "description" | "ownerUserId" | "parentId">,
): boolean {
  const row = rows.find((r) => r.id === id);
  return (
    row !== undefined &&
    row.name === expected.name &&
    row.description === expected.description &&
    row.ownerUserId === expected.ownerUserId &&
    row.parentId === expected.parentId
  );
}

// ---------------------------------------------------------------------------
// The dashboard's "By cost centre" panel — pure shaping of the server's rollup.
// ---------------------------------------------------------------------------

/** A top-level rollup row with the rows that roll up into it. */
export interface RollupGroup {
  row: CostCentreRollupRow;
  children: CostCentreRollupRow[];
  /** row + children, in integer cents. Equals row.plannedCad when there are no children. */
  subtotalCad: number;
}

/**
 * Rollup rows → groups. A row whose parentId names another row IN THIS ROLLUP is listed under it;
 * every other row (top-level, an unregistered string, or a child whose parent is not in the
 * rollup — a retired parent with nothing planned is omitted server-side) is a group of its own,
 * and the panel names its parentCode. The server's code order is kept, at both levels.
 */
export function groupRollup(rows: CostCentreRollupRow[]): RollupGroup[] {
  const ids = new Set(rows.flatMap((r) => (r.costCentreId === null ? [] : [r.costCentreId])));
  const nested = (r: CostCentreRollupRow) => r.parentId !== null && ids.has(r.parentId);
  return rows
    .filter((r) => !nested(r))
    .map((row) => {
      const children = rows.filter((r) => nested(r) && r.parentId === row.costCentreId);
      return { row, children, subtotalCad: sumCad([row.plannedCad, ...children.map((c) => c.plannedCad)]) };
    });
}
