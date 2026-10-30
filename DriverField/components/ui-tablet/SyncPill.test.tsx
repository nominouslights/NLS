import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { statusMeta } from "@/lib/theme";
import type { SyncState } from "@/lib/sync/types";
import { registerSender, resetQueueForTests } from "@/lib/sync/queue";
import { describe as describePill, SyncPill } from "./SyncPill";

// The pill's wording is the offline requirement's honesty, so it is pinned word for word. The
// rule under all of it: never a green check while any kind of command is parked on the device.

function state(fields: Partial<SyncState>): SyncState {
  return { online: true, pending: 0, failed: 0, lastSyncedAt: null, enabled: false, enabledKinds: [], ...fields };
}

const HOURS = ["hos.certify", "hos.duty"] as const;

beforeEach(async () => {
  await resetQueueForTests({ wipe: true });
});

afterEach(() => {
  cleanup();
});

describe("describe()", () => {
  it("says 'Sync off' in neutral gray while no kind has a sender", () => {
    expect(describePill(state({ pending: 2 }))).toEqual({ kind: "off", label: "Sync off · 2 held" });
    expect(statusMeta("off").g).toBe("—");
  });

  it("says 'Sync · hours only · N held' in gold ◐ while only the hos.* kinds are live", () => {
    expect(describePill(state({ enabledKinds: [...HOURS], pending: 3 }))).toEqual({ kind: "soon", label: "Sync · hours only · 3 held" });
    expect(statusMeta("soon").g).toBe("◐");
  });

  it("says 'N failed — see Hours' in vermillion ▲, ahead of everything else", () => {
    expect(describePill(state({ enabledKinds: [...HOURS], pending: 1, failed: 2 }))).toEqual({ kind: "over", label: "2 failed — see Hours" });
    expect(describePill(state({ enabledKinds: [...HOURS], online: false, failed: 1 })).kind).toBe("over");
    expect(statusMeta("over").g).toBe("▲");
  });

  it("says 'Offline · N held' when the device has no network and senders exist", () => {
    expect(describePill(state({ enabledKinds: [...HOURS], online: false, pending: 4 }))).toEqual({ kind: "soon", label: "Offline · 4 held" });
  });

  it("never shows the green check while any kind is parked, whatever the counts say", () => {
    const combos: Partial<SyncState>[] = [
      { enabledKinds: [...HOURS], pending: 0 },
      { enabledKinds: [...HOURS], pending: 0, lastSyncedAt: "2026-10-10T20:00:00Z" },
      { enabledKinds: ["hos.duty"], pending: 0 },
      { pending: 0 },
    ];
    for (const c of combos) {
      expect(describePill(state(c)).kind, JSON.stringify(c)).not.toBe("ontime");
      expect(describePill(state(c)).label).not.toBe("Synced");
    }
  });

  it("shows Synced / Syncing only once every kind has a sender", () => {
    expect(describePill(state({ enabled: true, enabledKinds: [...HOURS] }))).toEqual({ kind: "ontime", label: "Synced" });
    expect(describePill(state({ enabled: true, enabledKinds: [...HOURS], pending: 2 }))).toEqual({ kind: "soon", label: "Syncing 2" });
  });
});

describe("<SyncPill />", () => {
  it("renders the hours-only wording once the hos.* senders are registered", async () => {
    registerSender("hos.duty", async () => undefined);
    registerSender("hos.certify", async () => undefined);

    render(<SyncPill />);

    expect(await screen.findByText("Sync · hours only · 0 held")).toBeTruthy();
    expect(screen.queryByText("Synced")).toBeNull();
  });
});
