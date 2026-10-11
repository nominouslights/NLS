"use client";

import type { ReactNode } from "react";
import { colors, fonts } from "@/lib/theme";
import type {
  BudgetItemVendorShareRecord,
  VendorRecord,
  VendorShareDraft,
} from "@/lib/api/budgeting";
import {
  MAX_VENDOR_SHARES,
  PENDING_VENDOR_NAME,
  centsToFieldText,
  remainingCents,
  shareAmountTextError,
  splitEvenly,
} from "@/lib/vendorShares";
import { formatCadPrecise, toHundredths } from "@/lib/money";
import { NumberField, SelectField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";

// The budget-item modal's "Vendors" section, for an EXPENSE item: who is paid, and how much of
// the item each one gets. A controlled component — the modal owns the rows and the inline
// "+ New vendor…" overlay (a sibling of the modal's shell, never a child: the shell's
// backdrop-filter would trap a nested fixed overlay).
//
// The rules it keeps visible (all the server's — lib/vendorShares.ts mirrors them):
//   - Shares are DOLLARS, and when there are any they cover the item's amount exactly. The live
//     line under the rows says "Remaining $X" / "Over by $X" / "Fully assigned" — a StatusChip,
//     so glyph + written label, never colour alone. It judges each share by the submit check's
//     own exact-cents rule (shareAmountTextError), never by rounding: a share with more than two
//     decimals — or zero, negative, or over the ceiling — puts the server's message under its row
//     and turns the line to "Invalid amount", so 50.005 + 49.995 on $100.00 is never shown as
//     fully assigned only to be refused on save.
//   - Nothing rebalances by itself. Changing the item's amount only moves the live line; the two
//     shortcuts ("Assign remaining" on a row, "Split evenly") run only when clicked.
//   - The picker offers ACTIVE vendors, each once across the rows. A vendor the item already had
//     that has been retired since is kept, selectable and marked: the server accepts a share the
//     item already carried (BudgetItemVendorRule only looks up ADDED vendors). "Already carried"
//     means the SAVED item: removing its row and re-adding it within the same edit is still
//     offered and accepted; only once a save goes through without it is it gone for good.
//   - No rows at all is "No vendor" — legal, and how the item lands in that bucket.

/** The picker option that opens the inline vendor modal instead of selecting anything. */
export const NEW_VENDOR_OPTION = "__new_vendor__";

/** Live text → number for the arithmetic only: blank or not-a-number counts as no amount yet. */
function liveAmount(text: string): number | null {
  if (text.trim() === "") return null;
  const n = Number(text);
  return Number.isFinite(n) ? n : null;
}

interface VendorChoice {
  value: string;
  label: string;
  /** Selectable only because the item already carried it — retired, or not (yet) in the register. */
  kept: "retired" | "pending" | null;
}

/**
 * The options for one row: the row's own vendor (whatever it is), active vendors no other row has
 * taken, and the item's original vendors that are retired or not yet readable — kept only for the
 * row that holds them or could take them back.
 */
function rowChoices(
  rowVendorId: string,
  takenElsewhere: ReadonlySet<string>,
  register: readonly VendorRecord[],
  original: readonly BudgetItemVendorShareRecord[],
): VendorChoice[] {
  const choices: VendorChoice[] = [];
  const listed = new Set<string>();
  for (const v of register) {
    if (takenElsewhere.has(v.id)) continue;
    if (v.isActive) {
      choices.push({ value: v.id, label: v.name, kept: null });
      listed.add(v.id);
    }
  }
  for (const s of original) {
    if (listed.has(s.vendorId) || takenElsewhere.has(s.vendorId)) continue;
    const inRegister = register.find((v) => v.id === s.vendorId);
    const name = inRegister?.name ?? s.vendorName;
    const retired = inRegister ? !inRegister.isActive : s.vendorName !== null && !s.isActive;
    choices.push({
      value: s.vendorId,
      label: name === null ? PENDING_VENDOR_NAME : retired ? `${name} — retired` : name,
      kept: name === null ? "pending" : retired ? "retired" : null,
    });
    listed.add(s.vendorId);
  }
  // A vendor picked in this row that is in neither list — retired since it was picked, or created
  // inline a moment ago — stays visible rather than the select silently showing another value.
  if (rowVendorId && !listed.has(rowVendorId)) {
    const v = register.find((r) => r.id === rowVendorId);
    choices.push({
      value: rowVendorId,
      label: v ? (v.isActive ? v.name : `${v.name} — retired`) : PENDING_VENDOR_NAME,
      kept: v ? (v.isActive ? null : "retired") : "pending",
    });
  }
  return choices;
}

export default function VendorSharesEditor({
  rows,
  onChange,
  amountCad,
  register,
  original,
  onNewVendor,
  disabled,
}: {
  rows: VendorShareDraft[];
  onChange: (rows: VendorShareDraft[]) => void;
  /** The item's amount as the server will store it, or null while it cannot be computed. */
  amountCad: number | null;
  /** The tenant's vendor register, retired included; null while loading or after a failed load. */
  register: VendorRecord[] | null;
  /** The item's shares as loaded (edit), for keeping a since-retired vendor; [] on create. */
  original: BudgetItemVendorShareRecord[];
  /** "+ New vendor…": open the inline modal; the new vendor goes into `rowIndex` (null → a new row). */
  onNewVendor: (rowIndex: number | null) => void;
  disabled: boolean;
}) {
  const reg = register ?? [];
  const parsed = rows.map((r) => ({ amountCad: liveAmount(r.amount) }));
  const rowErrors = rows.map((r) => shareAmountTextError(r.amount));
  const invalid = rowErrors.some((e) => e !== null);
  // Only computed when every typed share is exact cents — so the rounding inside remainingCents
  // never changes a share's value, and the figure is the one the submit check would reach.
  const left = amountCad === null || invalid ? null : remainingCents(amountCad, parsed);

  const update = (index: number, patch: Partial<VendorShareDraft>) =>
    onChange(rows.map((r, i) => (i === index ? { ...r, ...patch } : r)));

  function pick(index: number, value: string) {
    if (value === NEW_VENDOR_OPTION) {
      onNewVendor(index);
      return;
    }
    update(index, { vendorId: value });
  }

  function assignRemainingTo(index: number) {
    if (left === null || left <= 0) return;
    const own = parsed[index].amountCad;
    update(index, { amount: centsToFieldText((own === null ? 0 : toHundredths(own)) + left) });
  }

  function splitAll() {
    if (amountCad === null || rows.length === 0) return;
    const parts = splitEvenly(amountCad, rows.length);
    onChange(rows.map((r, i) => ({ ...r, amount: centsToFieldText(toHundredths(parts[i])) })));
  }

  const canAdd = rows.length < MAX_VENDOR_SHARES;

  return (
    <div data-testid="vendor-shares">
      {register === null && (
        <Note>The vendor register did not load — the picker can only show this item&apos;s own vendors.</Note>
      )}

      {rows.length === 0 ? (
        <Note>
          No vendor — this item counts under &ldquo;No vendor&rdquo; in Expense by vendor. Add a
          vendor to say who is paid; split it across several when more than one is.
        </Note>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: 10, marginTop: 4 }}>
          {rows.map((row, index) => {
            const taken = new Set(rows.filter((_, i) => i !== index).map((r) => r.vendorId).filter(Boolean));
            const choices = rowChoices(row.vendorId, taken, reg, original);
            const chosen = choices.find((c) => c.value === row.vendorId) ?? null;
            const options = [
              ...(row.vendorId === "" ? [{ value: "", label: "Choose a vendor…" }] : []),
              ...choices.map((c) => ({ value: c.value, label: c.label })),
              { value: NEW_VENDOR_OPTION, label: "+ New vendor…" },
            ];
            return (
              <div key={index} data-testid="vendor-share-row">
                <div
                  style={{
                    display: "grid",
                    gridTemplateColumns: "minmax(0, 1.6fr) minmax(0, 1fr) auto",
                    gap: 10,
                    alignItems: "end",
                  }}
                >
                  <SelectField
                    label={`Vendor ${index + 1}`}
                    value={row.vendorId}
                    onChange={(v) => pick(index, v)}
                    options={options}
                    disabled={disabled}
                  />
                  <NumberField
                    label="Share (CAD)"
                    value={row.amount}
                    onChange={(v) => update(index, { amount: v })}
                    min={0}
                    step={0.01}
                    placeholder="e.g. 1200.00"
                    disabled={disabled}
                  />
                  <div style={{ display: "flex", gap: 6, paddingBottom: 4 }}>
                    {left !== null && left > 0 && (
                      <ActionButton
                        onClick={() => assignRemainingTo(index)}
                        disabled={disabled}
                        style={{ padding: "5px 9px", fontSize: 11.5 }}
                      >
                        ASSIGN REMAINING
                      </ActionButton>
                    )}
                    <ActionButton
                      variant="destructive"
                      onClick={() => onChange(rows.filter((_, i) => i !== index))}
                      disabled={disabled}
                      style={{ padding: "5px 9px", fontSize: 11.5 }}
                    >
                      REMOVE
                    </ActionButton>
                  </div>
                </div>
                {rowErrors[index] !== null && (
                  <div
                    data-testid="vendor-share-error"
                    style={{
                      marginTop: 6,
                      fontFamily: fonts.body,
                      fontSize: 11.5,
                      color: colors.textSecondary,
                      lineHeight: 1.5,
                    }}
                  >
                    {rowErrors[index]}
                  </div>
                )}
                {chosen?.kept === "retired" && (
                  <KeptNote chip="Retired">
                    Retired — kept on this item. Once you save without it, it can&apos;t be added
                    back.
                  </KeptNote>
                )}
                {chosen?.kept === "pending" && (
                  <KeptNote chip="Name loading">
                    This vendor&apos;s record has not reached the register yet. It is kept as it is.
                  </KeptNote>
                )}
              </div>
            );
          })}
        </div>
      )}

      <div style={{ display: "flex", gap: 8, flexWrap: "wrap", marginTop: 10 }}>
        {canAdd && (
          <ActionButton
            onClick={() => onChange([...rows, { vendorId: "", amount: "" }])}
            disabled={disabled}
            style={{ padding: "5px 11px", fontSize: 12 }}
          >
            + ADD VENDOR
          </ActionButton>
        )}
        {canAdd && (
          <ActionButton
            onClick={() => onNewVendor(null)}
            disabled={disabled}
            style={{ padding: "5px 11px", fontSize: 12 }}
          >
            + NEW VENDOR…
          </ActionButton>
        )}
        {rows.length >= 2 && amountCad !== null && amountCad > 0 && (
          <ActionButton onClick={splitAll} disabled={disabled} style={{ padding: "5px 11px", fontSize: 12 }}>
            SPLIT EVENLY
          </ActionButton>
        )}
        {!canAdd && <Note>An item can be split across at most {MAX_VENDOR_SHARES} vendors.</Note>}
      </div>

      {rows.length > 0 && <RemainingLine left={left} amountCad={amountCad} invalid={invalid} />}
    </div>
  );
}

/**
 * The live balance of the split. A StatusChip carries the state (glyph + written label, the
 * figure in the label itself), and a sentence beside it says what to do — never colour alone.
 * An invalid share wins over every balance: no total built from it would be the one saved.
 */
function RemainingLine({
  left,
  amountCad,
  invalid,
}: {
  left: number | null;
  amountCad: number | null;
  invalid: boolean;
}) {
  const body = invalid ? (
    <>
      <StatusChip kind="over" label="Invalid amount" />
      <span>
        A share above is not a valid amount — fix it (dollars and cents, more than zero) to check
        the split.
      </span>
    </>
  ) : left === null || amountCad === null ? (
      <span>Enter the item&apos;s amount above to check the split.</span>
    ) : left > 0 ? (
      <>
        <StatusChip kind="soon" label={`Remaining ${formatCadPrecise(left / 100)}`} />
        <span>
          The shares must cover the whole {formatCadPrecise(amountCad)} — assign the rest to a
          vendor, or the item cannot be saved.
        </span>
      </>
    ) : left < 0 ? (
      <>
        <StatusChip kind="over" label={`Over by ${formatCadPrecise(-left / 100)}`} />
        <span>
          The shares add up to more than the item&apos;s {formatCadPrecise(amountCad)} — lower a
          share.
        </span>
      </>
    ) : (
      <>
        <StatusChip kind="ontime" label="Fully assigned" />
        <span>The shares cover {formatCadPrecise(amountCad)} exactly.</span>
      </>
    );
  return (
    <div
      aria-live="polite"
      data-testid="share-remaining"
      style={{
        marginTop: 10,
        display: "flex",
        alignItems: "center",
        gap: 10,
        flexWrap: "wrap",
        fontFamily: fonts.body,
        fontSize: 11.5,
        color: colors.textSecondary,
        lineHeight: 1.5,
      }}
    >
      {body}
    </div>
  );
}

function KeptNote({ chip, children }: { chip: string; children: ReactNode }) {
  return (
    <div
      style={{
        display: "flex",
        alignItems: "flex-start",
        gap: 8,
        marginTop: 6,
        fontFamily: fonts.body,
        fontSize: 11.5,
        color: colors.textSecondary,
        lineHeight: 1.5,
      }}
    >
      <StatusChip kind="off" label={chip} />
      <span>{children}</span>
    </div>
  );
}

function Note({ children }: { children: ReactNode }) {
  return (
    <div
      style={{
        marginTop: 6,
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
