"use client";

import { colors, fonts, rowSurface } from "@/lib/theme";
import type { BudgetCodeCategory } from "@/lib/types";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { formatCadPrecise } from "@/lib/money";
import {
  costBuildUpLabel,
  groupItemsByCode,
  itemCount,
  needsJustification,
  sumCad,
  PRIORITY_GLYPHS,
  PRIORITY_KINDS,
  PRIORITY_LABELS,
  RECURRENCE_LABELS,
  SERVICE_LINE_LABELS,
  SPEND_TYPE_LABELS,
  type BudgetAllocationRecord,
  type ItemCodeGroup,
} from "@/lib/api/budgeting";
import { EmptyNote, Num } from "@/components/screens/shared";

// One category's budget items on the period dashboard — rendered twice, Revenue then Expense.
// A code's budget is the SUM of its items, so the section is grouped by code: a header row per
// code (code tag, name, retired chip, the code's subtotal and item count, "+ ITEM"), with its
// items beneath (title, priority chip, spend type, recurrence, vendor, the "q unit × $u"
// build-up, tags, the justification, the amount). Groups and items keep the server's order —
// by code, then priority (Must first), then creation.
//
// The subtotals here are client-side sums of the items on screen; the dashboard's headline tiles
// stay the server's own period totals.
//
// A retired code's items stay and still count, but the server refuses every update to them
// (CodeRetired), so the code header offers no "+ ITEM" and the item's editor opens with no code
// chosen — it can be moved to an active code or removed. When the period is not editable the
// add, edit and remove controls are all absent; the dashboard explains why in one note above both
// sections.
//
// Codes belong to a period, so a fresh period can have no active code of this category at all.
// Then an item picker would be empty, so the section offers no add button and instead points to
// Budget Codes, where the period's chart is copied from another period, loaded from the starter
// set, or built by hand.
//
// An item copied from an earlier period arrives with every field but its justification
// (BudgetAllocation.CopyInto), so it carries a "Needs justification" chip and placeholder text
// in the justification's place — the gap reads as the work it is, never as a blank.

export default function AllocationSection({
  category,
  periodLabel,
  items,
  activeCodeCount,
  editable,
  busy,
  confirmRemoveItemId,
  onOpenCodes,
  onAdd,
  onAddToCode,
  onEdit,
  onRemove,
}: {
  category: BudgetCodeCategory;
  /** The entered period's label, so the remove confirm names the plan it changes. */
  periodLabel: string;
  /** Already filtered to this category, in the server's order. */
  items: BudgetAllocationRecord[];
  /** This period's active codes of this category — 0 means there is nothing to plan against yet. */
  activeCodeCount: number;
  editable: boolean;
  busy: boolean;
  /** The item id whose REMOVE is awaiting its confirming click, if any. */
  confirmRemoveItemId: string | null;
  /** To Budget Codes (same period), when there is no active code of this category. */
  onOpenCodes: () => void;
  /** Section-level add: any active code of the category. */
  onAdd: () => void;
  /** "+ ITEM" on a code header: that code preselected. */
  onAddToCode: (budgetCodeId: string) => void;
  onEdit: (item: BudgetAllocationRecord) => void;
  onRemove: (item: BudgetAllocationRecord) => void;
}) {
  const revenue = category === "Revenue";
  const total = sumCad(items.map((i) => i.amountCad));
  const groups = groupItemsByCode(items);
  const noCodes = activeCodeCount === 0;

  return (
    <div style={{ marginBottom: 18 }}>
      <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 10 }}>
        <div
          style={{
            fontFamily: fonts.semiCondensed,
            fontSize: 9.5,
            letterSpacing: ".14em",
            textTransform: "uppercase",
            color: colors.textLabel,
          }}
        >
          {revenue ? "Revenue items" : "Expense items"}
        </div>
        <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
          {itemCount(items.length)} · {groups.length} {groups.length === 1 ? "code" : "codes"} ·{" "}
          {formatCadPrecise(total)}
        </span>
        {editable && !noCodes && (
          <ActionButton
            variant="primary"
            onClick={onAdd}
            disabled={busy}
            style={{ marginLeft: "auto" }}
          >
            + ADD BUDGET ITEM
          </ActionButton>
        )}
      </div>

      {editable && noCodes && (
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: 10,
            flexWrap: "wrap",
            marginBottom: items.length > 0 ? 10 : 0,
          }}
        >
          <span
            style={{
              flex: "1 1 320px",
              fontFamily: fonts.body,
              fontSize: 11.5,
              color: colors.textSecondary,
              lineHeight: 1.6,
            }}
          >
            {periodLabel} has no active {category.toLowerCase()} codes yet, and every budget item
            is tagged to one. Set up this period&apos;s codes first — copy them from an earlier
            period, load the starter set, or add a code.
          </span>
          <ActionButton onClick={onOpenCodes}>OPEN BUDGET CODES</ActionButton>
        </div>
      )}

      {editable && noCodes && items.length === 0 ? null : items.length === 0 ? (
        <EmptyNote>
          {revenue
            ? "No revenue items yet — add an item for each source of income you expect, each argued from zero."
            : "No expense items yet — add an item for each thing this period's money must buy, each argued from zero."}
        </EmptyNote>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          {groups.map((g) => (
            <CodeGroup
              key={g.budgetCodeId}
              group={g}
              periodLabel={periodLabel}
              editable={editable}
              busy={busy}
              confirmRemoveItemId={confirmRemoveItemId}
              onAddToCode={onAddToCode}
              onEdit={onEdit}
              onRemove={onRemove}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function CodeGroup({
  group,
  periodLabel,
  editable,
  busy,
  confirmRemoveItemId,
  onAddToCode,
  onEdit,
  onRemove,
}: {
  group: ItemCodeGroup;
  periodLabel: string;
  editable: boolean;
  busy: boolean;
  confirmRemoveItemId: string | null;
  onAddToCode: (budgetCodeId: string) => void;
  onEdit: (item: BudgetAllocationRecord) => void;
  onRemove: (item: BudgetAllocationRecord) => void;
}) {
  return (
    <div>
      {/* Code header row. */}
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 9,
          flexWrap: "wrap",
          padding: "0 14px 6px",
          borderBottom: `1px solid ${colors.borderSubtle}`,
          marginBottom: 6,
        }}
      >
        <MonoTag>{group.code}</MonoTag>
        <span
          style={{
            fontFamily: fonts.body,
            fontWeight: 700,
            fontSize: 12.5,
            color: colors.textPrimary,
          }}
        >
          {group.name}
        </span>
        {group.serviceLine && (
          <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
            {SERVICE_LINE_LABELS[group.serviceLine]}
          </span>
        )}
        {!group.isCodeActive && <StatusChip kind="off" label="Retired" />}
        <span style={{ marginLeft: "auto", fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
          {itemCount(group.items.length)}
        </span>
        <Num size={13} weight={600} color={colors.textPrimary}>
          {formatCadPrecise(group.subtotalCad)}
        </Num>
        {editable && group.isCodeActive && (
          <ActionButton
            onClick={() => onAddToCode(group.budgetCodeId)}
            disabled={busy}
            style={{ padding: "4px 9px", fontSize: 11.5 }}
          >
            + ITEM
          </ActionButton>
        )}
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
        {group.items.map((item) => (
          <ItemRow
            key={item.id}
            item={item}
            periodLabel={periodLabel}
            editable={editable}
            busy={busy}
            confirming={confirmRemoveItemId === item.id}
            onEdit={onEdit}
            onRemove={onRemove}
          />
        ))}
      </div>
    </div>
  );
}

function ItemRow({
  item,
  periodLabel,
  editable,
  busy,
  confirming,
  onEdit,
  onRemove,
}: {
  item: BudgetAllocationRecord;
  periodLabel: string;
  editable: boolean;
  busy: boolean;
  confirming: boolean;
  onEdit: (item: BudgetAllocationRecord) => void;
  onRemove: (item: BudgetAllocationRecord) => void;
}) {
  const unargued = needsJustification(item);
  const buildUp = costBuildUpLabel(item);
  const facts = [
    SPEND_TYPE_LABELS[item.spendType],
    RECURRENCE_LABELS[item.recurrence],
    item.vendor,
  ].filter((f): f is string => Boolean(f));

  return (
    <div>
      <div
        onClick={editable ? () => onEdit(item) : undefined}
        style={{
          ...rowSurface(false),
          cursor: editable ? "pointer" : "default",
          padding: "10px 14px",
          display: "flex",
          alignItems: "center",
          gap: 12,
        }}
      >
        <div style={{ flex: "1 1 auto", minWidth: 0 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 9, flexWrap: "wrap" }}>
            <span
              style={{
                fontFamily: fonts.body,
                fontWeight: 600,
                fontSize: 12.5,
                color: colors.textPrimary,
              }}
            >
              {item.title}
            </span>
            {/* Priority: glyph + written label; two priorities share a colour, so the glyph is
                per-priority and the colour is never the carrier. */}
            <StatusChip
              kind={PRIORITY_KINDS[item.priority]}
              glyph={PRIORITY_GLYPHS[item.priority]}
              label={PRIORITY_LABELS[item.priority]}
            />
            {unargued && <StatusChip kind="soon" label="Needs justification" />}
          </div>
          <div
            style={{
              fontFamily: fonts.body,
              fontSize: 11.5,
              color: colors.textDim,
              marginTop: 3,
              lineHeight: 1.5,
            }}
          >
            {facts.join(" · ")}
            {buildUp && <> · {buildUp}</>}
            {item.tags.length > 0 && (
              <>
                {" · "}
                {item.tags.map((t) => (
                  <span key={t} style={{ marginRight: 5 }}>
                    #{t}
                  </span>
                ))}
              </>
            )}
          </div>
          <div
            style={{
              fontFamily: fonts.body,
              fontSize: 11.5,
              color: unargued ? colors.textSecondary : colors.textDim,
              marginTop: 2,
              lineHeight: 1.5,
              fontStyle: unargued ? "italic" : undefined,
            }}
          >
            {/* Always carries text: an unargued item shows what is missing instead of a blank. */}
            {unargued
              ? "Needs justification — carried over from an earlier period; argue this item before it can be saved."
              : item.justification}
          </div>
        </div>
        <div style={{ width: 130, textAlign: "right", flex: "none" }}>
          <Num size={13.5}>{formatCadPrecise(item.amountCad)}</Num>
        </div>
        {editable && (
          <div
            style={{ width: 140, textAlign: "right", flex: "none" }}
            // The row itself opens the editor; a click on the remove cell must not.
            // ActionButton's onClick carries no event, so the cell stops the bubble.
            onClick={(e) => e.stopPropagation()}
          >
            <ActionButton
              variant="destructive"
              disabled={busy}
              onClick={() => onRemove(item)}
              style={{ padding: "5px 10px", fontSize: 12 }}
            >
              {confirming ? "CONFIRM REMOVE" : "REMOVE"}
            </ActionButton>
          </div>
        )}
      </div>
      {confirming && (
        <div
          style={{
            margin: "6px 14px 0",
            fontFamily: fonts.body,
            fontSize: 11.5,
            color: colors.textSecondary,
            lineHeight: 1.6,
          }}
        >
          Removes &ldquo;{item.title}&rdquo; ({item.code}) from {periodLabel}&apos;s plan. Other
          items and other periods are not affected. Click CONFIRM REMOVE to proceed.
        </div>
      )}
    </div>
  );
}
