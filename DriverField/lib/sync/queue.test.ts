import { describe, expect, it } from "vitest";
import { drain, enqueue, pending, retry } from "./queue";
import { getSyncState } from "./status";

// The offline seam ships as a no-op, so there is very little behaviour to test — but the two
// things that ARE true today are exactly the two the offline batch depends on, and both are
// cheap to break silently.

describe("command queue", () => {
  it("mints a distinct client-generated id per command", async () => {
    // The single most important property in the offline design: the id travels as the
    // Idempotency-Key header, so a retried sync can never double-submit a DVIR or duplicate a
    // manifest entry. Two commands sharing an id would make a retry drop one of them.
    const ids = await Promise.all([
      enqueue("hos.record", { duty: "Driving" }),
      enqueue("hos.record", { duty: "Driving" }),
      enqueue("dvir.submit", {}),
    ]);

    expect(new Set(ids).size).toBe(ids.length);
    for (const id of ids) expect(id.length).toBeGreaterThan(8);
  });

  it("resolves immediately, so a driver never waits on the network", async () => {
    // Reads and writes are local-first by design. If enqueue ever starts awaiting a request,
    // every screen inherits a loading state it was not written to handle — and a driver in a
    // dead zone inherits a hang.
    const id = await enqueue("incident.report", { type: "Wildlife on roadway" });
    expect(typeof id).toBe("string");
  });

  it("holds what it captured", async () => {
    const before = pending().length;
    await enqueue("vehicle.fault", { note: "washer pump" });
    expect(pending().length).toBe(before + 1);
  });

  it("survives drain and retry while unimplemented", async () => {
    // Screens call these. A no-op that throws would be worse than one that does nothing.
    await expect(drain()).resolves.toBeUndefined();
    await expect(retry("nope")).resolves.toBeUndefined();
  });
});

describe("sync state contract", () => {
  it("reports itself disabled, so the pill cannot claim a sync that is not happening", () => {
    // The honest-pill rule. If this ever returns enabled:true while queue.drain() is still a
    // no-op, the UI starts telling drivers their compliance records reached the server.
    expect(getSyncState().enabled).toBe(false);
  });

  it("satisfies the full SyncState shape the pill renders", () => {
    const state = getSyncState();
    expect(state).toEqual({
      online: expect.any(Boolean),
      pending: expect.any(Number),
      failed: expect.any(Number),
      lastSyncedAt: null,
      enabled: false,
    });
  });
});
