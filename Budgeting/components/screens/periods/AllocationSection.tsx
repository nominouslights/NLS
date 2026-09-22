"use client";

import { colors, fonts, rowSurface } from "@/lib/theme";
import type { BudgetCodeCategory } from "@/lib/types";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { formatCad } from "@/lib/api/format";
import {
  needsJustification,
  SERVICE_LINE_LABELS,
  type BudgetAllocationRecord,
} from "@/lib/api/budgeting";
import { EmptyNote, Num, TableHead } from "@/components/screens/shared";

// One category's lines on the period dashboard — rendered twice, Revenue then Expense. Rows are
// the wire records rendered directly (code · name · service line · justification · amount). A
// retired code shows its own "off" chip: the line still counts, but cannot be re-set until the
// code is restored. When the period is not editable the add and remove controls are simply
// absent — the dashboard explains why in one note above both sections, so this component does
// not repeat it.
//
// A line copied from an earlier period arrives with its amount and an EMPTY justification
// (BudgetAllocation.CopyInto) — a value nothing in this app rendered before the copy existed,
// and one that used to produce a dangling " · " and a blank second line. It now gets a
// "Needs justification" chip plus placeholder text, so the gap reads as the work it is.

export default function AllocationSection({
  category,
  lines,
  editable,
  busy,
  confirmRemoveCodeId,
  onAdd,
  onEdit,
  onRemove,
}: {
  category: BudgetCodeCategory;
  /** Already filtered to this category, in the server's order (by code). */
  lines: BudgetAllocationRecord[];
  editable: boolean;
  busy: boolean;
  /** The code id whose REMOVE is awaiting its confirming click, if any. */
  confirmRemoveCodeId: string | null;
  onAdd: () => void;
  onEdit: (line: BudgetAllocationRecord) => void;
  onRemove: (line: BudgetAllocationRecord) => void;
}) {
  const revenue = category === "Revenue";
  const total = lines.reduce((sum, l) => sum + l.amountCad, 0);

  const columns: { label: string; align?: "right" }[] = [
    { label: "Code" },
    { label: "Amount", align: "right" },
  ];
  if (editable) columns.push({ label: "", align: "right" });

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
          {revenue ? "Revenue lines" : "Expense lines"}
        </div>
        <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
          {lines.length} · {formatCad(total)}
        </span>
        {editable && (
          <ActionButton
            variant="primary"
            onClick={onAdd}
            disabled={busy}
            style={{ marginLeft: "auto" }}
          >
            {revenue ? "+ SET REVENUE" : "+ SET BUDGET"}
          </ActionButton>
        )}
      </div>

      {lines.length === 0 ? (
        <EmptyNote>
          {revenue
            ? "No revenue planned yet — set a line per revenue code you expect to earn on."
            : "No expense budget yet — set a line per expense code, each justified from zero."}
        </EmptyNote>
      ) : (
        <>
          <TableHead columns={columns} />
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {lines.map((l) => {
              const confirming = confirmRemoveCodeId === l.budgetCodeId;
              const unargued = needsJustification(l);
              return (
                <div
                  key={l.id}
                  onClick={editable ? () => onEdit(l) : undefined}
                  style={{
                    ...rowSurface(false),
                    cursor: editable ? "pointer" : "default",
                    padding: "11px 14px",
                    display: "flex",
                    alignItems: "center",
                    gap: 12,
                  }}
                >
                  <div style={{ flex: "1 1 auto", minWidth: 0 }}>
                    <div
                      style={{ display: "flex", alignItems: "center", gap: 9, flexWrap: "wrap" }}
                    >
                      <MonoTag>{l.code}</MonoTag>
                      <span
                        style={{
                          fontFamily: fonts.body,
                          fontWeight: 600,
                          fontSize: 12.5,
                          color: colors.textPrimary,
                        }}
                      >
                        {l.name}
                      </span>
                      {!l.isCodeActive && <StatusChip kind="off" label="Retired" />}
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
                      {l.serviceLine ? `${SERVICE_LINE_LABELS[l.serviceLine]} · ` : ""}
                      {/* The justification slot always carries text, so the separator above can
                          never dangle: an unargued line shows what is missing instead of a blank. */}
                      <span style={unargued ? { fontStyle: "italic" } : undefined}>
                        {unargued
                          ? "Carried over from an earlier period — argue this line before it can be saved."
                          : l.justification}
                      </span>
                    </div>
                  </div>
                  <div style={{ width: 150, textAlign: "right", flex: "none" }}>
                    <Num size={13.5}>{formatCad(l.amountCad)}</Num>
                  </div>
                  {editable && (
                    <div
                      style={{ width: 150, textAlign: "right", flex: "none" }}
                      // The row itself opens the editor; a click on the remove cell must not.
                      // ActionButton's onClick carries no event, so the cell stops the bubble.
                      onClick={(e) => e.stopPropagation()}
                    >
                      <ActionButton
                        variant="destructive"
                        disabled={busy}
                        onClick={() => onRemove(l)}
                        style={{ padding: "5px 10px", fontSize: 12 }}
                      >
                        {confirming ? "CONFIRM REMOVE" : "REMOVE"}
                      </ActionButton>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </>
      )}
    </div>
  );
}
