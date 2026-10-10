import { afterEach, describe, expect, it, vi } from "vitest";
import type { BudgetPeriod } from "@/lib/types";
import {
  enteredPeriodStorageKey,
  localIsoDate,
  readEnteredPeriod,
  resolveEnteredPeriod,
  suggestedPeriodId,
  suggestionReason,
  writeEnteredPeriod,
} from "@/lib/workingPeriod";
import { isPeriodScoped, NAV_GROUPS, type ScreenId } from "@/lib/nav";

// "Enter a period": Console holds one id, and everything about it is derived here. None of this
// mirrors a server rule — the backend scopes every period action by the route id and has no idea
// which period a tab is "in" — so these pin the client's own contract: what the chooser
// suggests, when an entered period counts as lost, and that the choice lives per tab, per user.

function period(id: string, startsOn: string, endsOn: string): BudgetPeriod {
  return {
    id,
    label: id.toUpperCase(),
    startsOn,
    endsOn,
    state: "Draft",
    pk: "info",
    plannedRevenue: 0,
    plannedExpense: 0,
  };
}

const Q1 = period("q1", "2026-01-01", "2026-03-31");
const Q2 = period("q2", "2026-04-01", "2026-06-30");
const Q3 = period("q3", "2026-07-01", "2026-09-30");

afterEach(() => {
  window.sessionStorage.clear();
  window.localStorage.clear();
  vi.restoreAllMocks();
});

describe("localIsoDate", () => {
  it("formats the local calendar date, zero-padded", () => {
    expect(localIsoDate(new Date(2026, 0, 5))).toBe("2026-01-05");
  });
});

describe("suggestedPeriodId", () => {
  it("picks the period today falls strictly inside", () => {
    expect(suggestedPeriodId([Q1, Q2, Q3], "2026-05-15")).toBe("q2");
    expect(suggestionReason([Q1, Q2, Q3], "2026-05-15")).toBe("INCLUDES TODAY");
  });

  it("treats the start date as inside", () => {
    expect(suggestedPeriodId([Q1, Q2, Q3], "2026-04-01")).toBe("q2");
  });

  it("treats the end date as inside", () => {
    expect(suggestedPeriodId([Q1, Q2, Q3], "2026-06-30")).toBe("q2");
  });

  it("falls back to the latest start when no period contains today, whatever the order", () => {
    expect(suggestedPeriodId([Q1, Q2, Q3], "2027-02-01")).toBe("q3");
    expect(suggestedPeriodId([Q3, Q1, Q2], "2027-02-01")).toBe("q3");
    expect(suggestedPeriodId([Q2, Q3, Q1], "2025-01-01")).toBe("q3");
    expect(suggestionReason([Q2, Q3, Q1], "2025-01-01")).toBe("LATEST");
  });

  it("suggests nothing for an empty list", () => {
    expect(suggestedPeriodId([], "2026-05-15")).toBeNull();
    expect(suggestionReason([], "2026-05-15")).toBeNull();
  });
});

describe("resolveEnteredPeriod", () => {
  it("finds the entered period", () => {
    expect(resolveEnteredPeriod([Q1, Q2], "q2", false)).toEqual({ period: Q2, lost: false });
  });

  it("is empty, not lost, when nothing is entered", () => {
    expect(resolveEnteredPeriod([Q1, Q2], null, false)).toEqual({ period: null, lost: false });
  });

  it("is lost when an entered id matches nothing after a good load", () => {
    expect(resolveEnteredPeriod([Q1, Q2], "gone", false)).toEqual({ period: null, lost: true });
  });

  it("is NOT lost when the load failed — the period may exist, we just could not ask", () => {
    expect(resolveEnteredPeriod([], "q2", true)).toEqual({ period: null, lost: false });
  });

  it("is NOT lost while the list is still loading", () => {
    expect(resolveEnteredPeriod(null, "q2", false)).toEqual({ period: null, lost: false });
  });
});

describe("enteredPeriodStorageKey", () => {
  const a = { tenantId: "tenant-a", sub: "user-1" };

  it("names the tenant and the user", () => {
    expect(enteredPeriodStorageKey(a)).toBe("nl.budgeting.enteredPeriod.tenant-a.user-1");
  });

  it("differs by tenant", () => {
    expect(enteredPeriodStorageKey(a)).not.toBe(
      enteredPeriodStorageKey({ ...a, tenantId: "tenant-b" }),
    );
  });

  it("differs by user", () => {
    expect(enteredPeriodStorageKey(a)).not.toBe(enteredPeriodStorageKey({ ...a, sub: "user-2" }));
  });

  it("is null with no claims, or claims missing either part", () => {
    expect(enteredPeriodStorageKey(null)).toBeNull();
    expect(enteredPeriodStorageKey({ tenantId: "", sub: "user-1" })).toBeNull();
    expect(enteredPeriodStorageKey({ tenantId: "tenant-a", sub: "" })).toBeNull();
  });
});

describe("entered period storage", () => {
  const key = "nl.budgeting.enteredPeriod.tenant-a.user-1";

  it("round-trips through sessionStorage", () => {
    writeEnteredPeriod(key, "q2");
    expect(window.sessionStorage.getItem(key)).toBe("q2");
    expect(readEnteredPeriod(key)).toBe("q2");
  });

  it("removes the entry when null is written", () => {
    writeEnteredPeriod(key, "q2");
    writeEnteredPeriod(key, null);
    expect(window.sessionStorage.getItem(key)).toBeNull();
    expect(readEnteredPeriod(key)).toBeNull();
  });

  it("never touches localStorage — a shared machine must not reopen someone's period", () => {
    const set = vi.spyOn(Storage.prototype, "setItem");
    writeEnteredPeriod(key, "q2");
    readEnteredPeriod(key);
    writeEnteredPeriod(key, null);
    expect(window.localStorage.length).toBe(0);
    // Storage.prototype is shared by both stores; every write must have landed on the session one.
    for (const call of set.mock.contexts) expect(call).toBe(window.sessionStorage);
  });

  it("stores nothing and reads nothing without a key", () => {
    writeEnteredPeriod(null, "q2");
    expect(window.sessionStorage.length).toBe(0);
    expect(readEnteredPeriod(null)).toBeNull();
  });

  it("degrades to not-remembered when storage throws", () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("denied");
    });
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("denied");
    });
    expect(() => writeEnteredPeriod(key, "q2")).not.toThrow();
    expect(readEnteredPeriod(key)).toBeNull();
  });
});

describe("isPeriodScoped", () => {
  // Vendors and Cost Centres are deliberately unscoped: both registers are tenant-wide (their
  // routes have no period in them), so they render without an entered period and never remount
  // on a switch. Budget Codes stays scoped — codes moved under the period.
  it("is false for Settings, Vendors and Cost Centres only — Budget Codes is scoped since codes moved under the period", () => {
    const all: ScreenId[] = NAV_GROUPS.flatMap((g) => g.items.map((i) => i.id));
    expect(all.filter((id) => !isPeriodScoped(id)).sort()).toEqual(["costCentres", "settings", "vendors"]);
    expect(all.filter(isPeriodScoped).sort()).toEqual([
      "actuals",
      "codes",
      "periods",
      "reports",
      "variance",
    ]);
  });

  it("lists Vendors in PLANNING with the VN code tile", () => {
    const planning = NAV_GROUPS.find((g) => g.label === "PLANNING");
    expect(planning?.items.find((i) => i.id === "vendors")).toEqual({
      id: "vendors",
      label: "Vendors",
      code: "VN",
    });
  });
});
