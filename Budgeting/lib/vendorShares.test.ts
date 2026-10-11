import { describe, expect, it } from "vitest";
import {
  MAX_VENDOR_SHARES,
  PENDING_VENDOR_NAME,
  VENDOR_SHARE_AMOUNT_MAX,
  VENDOR_SHARE_MESSAGES,
  assignRemaining,
  centsToFieldText,
  exactCents,
  itemVendorText,
  remainingCad,
  remainingCents,
  roundedCents,
  shareSummary,
  splitEvenly,
  vendorInactiveMessage,
  vendorRegisterError,
  vendorSharesCategoryError,
  vendorSharesError,
  vendorSharesMustSumMessage,
  vendorSharesShapeError,
} from "./vendorShares";
import {
  ALLOCATION_AMOUNT_MAX,
  budgetItemError,
  BUDGET_ITEM_MESSAGES,
  type BudgetItemInput,
  type BudgetItemVendorShareInput,
  type BudgetItemVendorShareRecord,
} from "./api/budgeting";

// Every rule here mirrors a C# rule on the backend — named per block — and the messages are
// BudgetAllocationErrors' text verbatim, so the modal says exactly what the server would have.
// Money is pinned in exact cents: a float sum of 0.1 + 0.2 is not 0.3, and the server compares
// the shares' decimal sum to the amount to the cent.

const A = "vendor-a";
const B = "vendor-b";
const C = "vendor-c";
const share = (vendorId: string | null, amountCad: number | null): BudgetItemVendorShareInput => ({
  vendorId,
  amountCad,
});
const m = VENDOR_SHARE_MESSAGES;

describe("constants mirror BudgetAllocation", () => {
  it("MaxVendorShares is 20 and the share ceiling is AmountMax", () => {
    expect(MAX_VENDOR_SHARES).toBe(20);
    expect(VENDOR_SHARE_AMOUNT_MAX).toBe(ALLOCATION_AMOUNT_MAX);
    expect(VENDOR_SHARE_AMOUNT_MAX).toBe(999_999_999.99);
  });
});

describe("messages — BudgetAllocationErrors, verbatim", () => {
  it("pins every static vendor-share message", () => {
    expect(m).toEqual({
      VendorShareVendorRequired: "Every vendor share must name a vendor from the vendor register.",
      VendorShareAmountInvalid:
        "Each vendor share must be more than zero, in dollars and cents (at most two decimal places), and 999,999,999.99 or less.",
      DuplicateVendorShare:
        "A vendor can appear only once on a budget item. Combine its shares into one amount.",
      TooManyVendorShares: "A budget item can be split across at most 20 vendors.",
      VendorSharesNotAllowedForRevenue:
        "A revenue item has no vendor shares — vendors are who the business pays. Name the payer in the vendor (payer) field instead, and send no vendor shares.",
      PayerNotAllowedForExpense:
        "The free-text vendor is the payer on a revenue item. On an expense item, choose vendors from the vendor register as vendor shares instead, and leave the free-text vendor blank.",
      VendorNotFound:
        "That vendor is not in the vendor register. Add it to the register first, or choose an existing vendor.",
    });
  });

  it("VendorSharesMustSumToAmount names both totals in the invariant #,0.00 format", () => {
    expect(vendorSharesMustSumMessage(1234.5, 2000)).toBe(
      "The vendor shares add up to 1,234.50, but the item's amount is 2,000.00. The shares must cover the whole amount exactly — adjust them, or remove every share to leave the item with no vendor.",
    );
    expect(vendorSharesMustSumMessage(1000000, 0.3)).toContain("add up to 1,000,000.00, but the item's amount is 0.30.");
  });

  it("VendorInactive names the vendor", () => {
    expect(vendorInactiveMessage("Esso Thompson")).toBe(
      '"Esso Thompson" is a retired vendor and cannot be added to a budget item. Choose an active vendor, or restore it in the vendor register first.',
    );
  });
});

describe("exact cents", () => {
  it("is exact where a float multiply is not", () => {
    expect(exactCents(1.005)).toBeNull(); // three decimals — refused, never rounded
    expect(exactCents(10.005)).toBeNull();
    expect(exactCents(1.01)).toBe(101);
    expect(exactCents(0.29)).toBe(29); // 0.29 * 100 is 28.999999999999996
    expect(exactCents(999_999_999.99)).toBe(99_999_999_999);
    expect(exactCents(Number.NaN)).toBeNull();
  });

  it("rounds half away from zero for display sums (BudgetAllocation.Round)", () => {
    expect(roundedCents(1.005)).toBe(101);
    expect(roundedCents(-1.005)).toBe(-101);
    expect(roundedCents(0.004)).toBe(0);
  });

  it("writes cents back as two-decimal field text", () => {
    expect(centsToFieldText(123450)).toBe("1234.50");
    expect(centsToFieldText(5)).toBe("0.05");
    expect(centsToFieldText(0)).toBe("0.00");
  });
});

describe("vendorSharesShapeError — BudgetAllocation.Parse's shares block, in order", () => {
  it("accepts no shares at all (No vendor), on any amount — $0 included", () => {
    expect(vendorSharesShapeError([], 0)).toBeNull();
    expect(vendorSharesShapeError([], 2450)).toBeNull();
  });

  it("accepts shares that sum to the amount to the cent — where floats would not", () => {
    expect(vendorSharesShapeError([share(A, 0.1), share(B, 0.2)], 0.3)).toBeNull();
    expect(vendorSharesShapeError([share(A, 2000), share(B, 450)], 2450)).toBeNull();
    expect(vendorSharesShapeError([share(A, 33.34), share(B, 33.33), share(C, 33.33)], 100)).toBeNull();
  });

  it("checks the count first, before reading any share", () => {
    const tooMany = Array.from({ length: 21 }, (_, i) => share(null, -1 - i));
    expect(vendorSharesShapeError(tooMany, 0)).toBe(m.TooManyVendorShares);
    const twenty = Array.from({ length: 20 }, (_, i) => share(`v${i}`, 1));
    expect(vendorSharesShapeError(twenty, 20)).toBeNull();
  });

  it("requires a vendor on each share (BudgetItemVendorShare.Create)", () => {
    expect(vendorSharesShapeError([share(null, 10)], 10)).toBe(m.VendorShareVendorRequired);
    expect(vendorSharesShapeError([share("", 10)], 10)).toBe(m.VendorShareVendorRequired);
  });

  it("requires a positive two-decimal amount at most AmountMax — each boundary both sides", () => {
    expect(vendorSharesShapeError([share(A, null)], 10)).toBe(m.VendorShareAmountInvalid);
    expect(vendorSharesShapeError([share(A, 0)], 0)).toBe(m.VendorShareAmountInvalid);
    expect(vendorSharesShapeError([share(A, -5)], 10)).toBe(m.VendorShareAmountInvalid);
    expect(vendorSharesShapeError([share(A, 10.005)], 10.01)).toBe(m.VendorShareAmountInvalid);
    expect(vendorSharesShapeError([share(A, 0.01)], 0.01)).toBeNull();
    expect(vendorSharesShapeError([share(A, 999_999_999.99)], 999_999_999.99)).toBeNull();
    expect(vendorSharesShapeError([share(A, 1_000_000_000)], 999_999_999.99)).toBe(m.VendorShareAmountInvalid);
  });

  it("checks the vendor before the amount, share by share", () => {
    expect(vendorSharesShapeError([share(null, -1)], 10)).toBe(m.VendorShareVendorRequired);
    // The first share's bad amount is reported before the second share's missing vendor.
    expect(vendorSharesShapeError([share(A, -1), share(null, 5)], 10)).toBe(m.VendorShareAmountInvalid);
  });

  it("refuses a vendor twice — as soon as it repeats, before later shares are read", () => {
    expect(vendorSharesShapeError([share(A, 5), share(A, 5)], 10)).toBe(m.DuplicateVendorShare);
    expect(vendorSharesShapeError([share(A, 5), share(A, 5), share(null, 1)], 10)).toBe(m.DuplicateVendorShare);
    expect(vendorSharesShapeError([share(A, 5), share(null, 1), share(A, 5)], 10)).toBe(m.VendorShareVendorRequired);
  });

  it("checks the sum last, naming both totals — a partial split is refused, and so is an over-split", () => {
    expect(vendorSharesShapeError([share(A, 2000)], 2450)).toBe(vendorSharesMustSumMessage(2000, 2450));
    expect(vendorSharesShapeError([share(A, 2000), share(B, 451)], 2450)).toBe(
      vendorSharesMustSumMessage(2451, 2450),
    );
    // A $0 item cannot carry a share at all: every share is positive, so the sum never matches.
    expect(vendorSharesShapeError([share(A, 1)], 0)).toBe(vendorSharesMustSumMessage(1, 0));
  });

  it("compares against the amount the server stores (already rounded)", () => {
    // The lump sum 100.005 is stored as 100.01.
    expect(vendorSharesShapeError([share(A, 100.01)], 100.005)).toBeNull();
  });
});

describe("vendorSharesCategoryError — BudgetAllocation.CheckCategory (as on create)", () => {
  it("refuses shares on a revenue item, allows none", () => {
    expect(vendorSharesCategoryError([share(A, 10)], null, "Revenue")).toBe(m.VendorSharesNotAllowedForRevenue);
    expect(vendorSharesCategoryError([], "Alamos Gold", "Revenue")).toBeNull();
  });

  it("refuses free-text payer on an expense item, allows shares", () => {
    expect(vendorSharesCategoryError([], "Kal Tire", "Expense")).toBe(m.PayerNotAllowedForExpense);
    expect(vendorSharesCategoryError([share(A, 10)], null, "Expense")).toBeNull();
    expect(vendorSharesCategoryError([], "   ", "Expense")).toBeNull(); // blank normalizes to null
  });
});

describe("vendorSharesError — shape first, then category (Validate runs before Create's category rule)", () => {
  it("reports a shape error on revenue shares before the category", () => {
    expect(vendorSharesError([share(A, 5)], 10, "Revenue")).toBe(vendorSharesMustSumMessage(5, 10));
    expect(vendorSharesError([share(A, 10)], 10, "Revenue")).toBe(m.VendorSharesNotAllowedForRevenue);
    expect(vendorSharesError([share(A, 10)], 10, "Expense")).toBeNull();
    expect(vendorSharesError([], 10, "Expense", "Kal Tire")).toBe(m.PayerNotAllowedForExpense);
  });
});

describe("budgetItemError runs the shares rules in the server's place in the order", () => {
  const valid: BudgetItemInput = {
    budgetCodeId: "exp-a",
    title: "Winter tires",
    amountCad: 2450,
    quantity: null,
    unitCostCad: null,
    unit: null,
    justification: "Quote in hand.",
    spendType: "Operating",
    recurrence: "OneTime",
    vendor: null,
    tags: [],
    priority: "ShouldHave",
    assumptions: null,
    consequenceIfUnfunded: null,
    vendorShares: [share(A, 2000), share(B, 450)],
  };
  const err = (over: Partial<BudgetItemInput>, category: "Expense" | "Revenue" | null = null) =>
    budgetItemError({ ...valid, ...over }, category);

  it("accepts a valid split, and checks the sum against a BUILT-UP item's computed amount", () => {
    expect(err({})).toBeNull();
    // 3 × 1.005 → 3 × 1.01 = 3.03 on the server.
    expect(err({ amountCad: null, quantity: 3, unitCostCad: 1.005, vendorShares: [share(A, 3.03)] })).toBeNull();
    expect(err({ amountCad: null, quantity: 3, unitCostCad: 1.005, vendorShares: [share(A, 3.02)] })).toBe(
      vendorSharesMustSumMessage(3.02, 3.03),
    );
  });

  it("an amount change with the old shares is refused, never rebalanced", () => {
    expect(err({ amountCad: 2500 })).toBe(vendorSharesMustSumMessage(2450, 2500));
  });

  it("comes after vendor (too long) and before tags", () => {
    const badShares = { vendorShares: [share(A, 1)] };
    expect(err({ ...badShares, vendor: "x".repeat(121) })).toBe(BUDGET_ITEM_MESSAGES.VendorTooLong);
    expect(err({ ...badShares, tags: [""] })).toBe(vendorSharesMustSumMessage(1, 2450));
    expect(err({ tags: [""] })).toBe(BUDGET_ITEM_MESSAGES.TagInvalid);
  });

  it("applies the category rule last, and only when the category is given", () => {
    expect(err({}, "Revenue")).toBe(m.VendorSharesNotAllowedForRevenue);
    expect(err({ vendorShares: [], vendor: "Kal Tire" }, "Expense")).toBe(m.PayerNotAllowedForExpense);
    expect(err({ vendorShares: [], vendor: "Kal Tire" })).toBeNull();
    expect(err({ vendorShares: [], vendor: "Alamos Gold" }, "Revenue")).toBeNull();
  });
});

describe("vendorRegisterError — BudgetItemVendorRule", () => {
  const register = [
    { id: A, name: "Kal Tire", isActive: true },
    { id: B, name: "Esso Thompson", isActive: false },
  ];

  it("refuses an added vendor the register does not have, and a retired one by name", () => {
    expect(vendorRegisterError([share("ghost", 10)], [], register, "Expense")).toBe(m.VendorNotFound);
    expect(vendorRegisterError([share(B, 10)], [], register, "Expense")).toBe(vendorInactiveMessage("Esso Thompson"));
    expect(vendorRegisterError([share(A, 10)], [], register, "Expense")).toBeNull();
  });

  it("looks up only ADDED vendors — a share the item already had passes even when retired since", () => {
    expect(vendorRegisterError([share(B, 10)], [{ vendorId: B }], register, "Expense")).toBeNull();
    expect(vendorRegisterError([share("ghost", 10)], [{ vendorId: "ghost" }], register, "Expense")).toBeNull();
  });

  it("reports in request order", () => {
    expect(vendorRegisterError([share(B, 5), share("ghost", 5)], [], register, "Expense")).toBe(
      vendorInactiveMessage("Esso Thompson"),
    );
    expect(vendorRegisterError([share("ghost", 5), share(B, 5)], [], register, "Expense")).toBe(m.VendorNotFound);
  });

  it("skips revenue (the aggregate refuses its shares), an unloaded register, and blank rows", () => {
    expect(vendorRegisterError([share("ghost", 10)], [], register, "Revenue")).toBeNull();
    expect(vendorRegisterError([share("ghost", 10)], [], null, "Expense")).toBeNull();
    expect(vendorRegisterError([share(null, 10)], [], register, "Expense")).toBeNull();
  });
});

describe("remaining / over, in exact cents", () => {
  it("is the amount minus the shares, blank shares counting as zero", () => {
    expect(remainingCad(2450, [{ amountCad: 2000 }, { amountCad: null }])).toBe(450);
    expect(remainingCad(0.3, [{ amountCad: 0.1 }, { amountCad: 0.2 }])).toBe(0);
    expect(remainingCents(0.3, [{ amountCad: 0.1 }, { amountCad: 0.2 }])).toBe(0);
    expect(remainingCad(100, [{ amountCad: 60 }, { amountCad: 40.01 }])).toBe(-0.01);
  });

  it("splits evenly to the cent, the odd cents going to the first rows", () => {
    expect(splitEvenly(100, 3)).toEqual([33.34, 33.33, 33.33]);
    expect(splitEvenly(0.05, 2)).toEqual([0.03, 0.02]);
    expect(splitEvenly(2450, 2)).toEqual([1225, 1225]);
    expect(splitEvenly(10, 0)).toEqual([]);
    const parts = splitEvenly(1234.57, 7);
    expect(remainingCents(1234.57, parts.map((amountCad) => ({ amountCad })))).toBe(0);
  });

  it("assigns what is left to one row only when asked, and never takes from an over-split", () => {
    const rows = [{ amountCad: 2000 }, { amountCad: null }];
    expect(assignRemaining(2450, rows, 1)).toBe(450);
    expect(assignRemaining(2450, rows, 0)).toBe(2450);
    expect(assignRemaining(0.3, [{ amountCad: 0.1 }, { amountCad: null }], 1)).toBe(0.2);
    expect(assignRemaining(2450, [{ amountCad: 2450 }], 0)).toBeNull();
    expect(assignRemaining(2450, [{ amountCad: 2500 }], 0)).toBeNull();
    expect(assignRemaining(2450, rows, 5)).toBeNull();
  });
});

describe("item row text", () => {
  const rec = (vendorId: string, vendorName: string | null, amountCad: number, isActive = true): BudgetItemVendorShareRecord => ({
    vendorId,
    vendorName,
    isActive,
    amountCad,
  });

  it("shows the payer on revenue", () => {
    expect(itemVendorText("Revenue", "Alamos Gold", [])).toBe("Payer: Alamos Gold");
    expect(itemVendorText("Revenue", null, [])).toBeNull();
  });

  it("shows the first two shares with amounts, retired written out, then +N more", () => {
    const shares = [rec(A, "Kal Tire", 1200), rec(B, "Esso", 300.5, false), rec(C, "Petro", 10), rec("d", "Shell", 5)];
    expect(itemVendorText("Expense", null, shares)).toBe("Kal Tire $1,200 · Esso (retired) $300.50 · +2 more");
    expect(itemVendorText("Expense", null, shares.slice(0, 1))).toBe("Kal Tire $1,200");
  });

  it("names a vendor still loading without calling it retired", () => {
    expect(itemVendorText("Expense", null, [rec(A, null, 10, false)])).toBe(`${PENDING_VENDOR_NAME} $10`);
    expect(shareSummary([rec(A, null, 10, false)]).shown[0].retired).toBe(false);
  });

  it("marks legacy free text on an expense item, and says nothing with neither", () => {
    expect(itemVendorText("Expense", "Kal Tire", [])).toBe("Kal Tire (free text)");
    expect(itemVendorText("Expense", null, [])).toBeNull();
  });
});
