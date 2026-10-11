import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../api/transport";
import { recordDutyEvent } from "../api/hos";
import {
  allCommands,
  COMMAND_KINDS,
  discard,
  drain,
  DRAIN_INTERVAL_MS,
  enqueue,
  failedCommands,
  onDrained,
  pending,
  ready,
  registerSender,
  resetQueueForTests,
  retry,
  startSync,
  statusOf,
} from "./queue";
import { getSyncState } from "./status";

// The durable queue and its drain — the offline contract, held to on the fake IndexedDB that
// vitest.setup.ts installs. Nothing here knows a URL: the senders are stubs, except the one
// test that runs a real sender through the real transport to pin the Idempotency-Key.

const { getAccessToken, getValidAccessToken, refreshAccessToken } = vi.hoisted(() => ({
  getAccessToken: vi.fn<() => string | null>(),
  getValidAccessToken: vi.fn<() => Promise<string | null>>(),
  refreshAccessToken: vi.fn<() => Promise<string>>(),
}));

vi.mock("../auth", () => ({ getAccessToken, getValidAccessToken, refreshAccessToken }));

beforeEach(async () => {
  await resetQueueForTests({ wipe: true });
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});

const rejected = (code: string, message: string, status: number) => new ApiError(code, message, status);

describe("capture", () => {
  it("mints a distinct client-generated id per command", async () => {
    // The single most important property in the offline design: the id travels as the
    // Idempotency-Key header, so a retried sync can never double-submit a DVIR or duplicate a
    // duty event. Two commands sharing an id would make a retry drop one of them.
    const ids = await Promise.all([
      enqueue("hos.duty", { duty: "Driving" }),
      enqueue("hos.duty", { duty: "Driving" }),
      enqueue("dvir.submit", {}),
    ]);

    expect(new Set(ids).size).toBe(ids.length);
    for (const id of ids) expect(id.length).toBeGreaterThan(8);
  });

  it("hands a payload factory the command's own id", async () => {
    // hos.duty carries its command id inside the payload as clientEventId — the key the ledger
    // store and the server reconcile on. The id is minted in here, so the only way the payload
    // can carry it is to be built in here.
    const id = await enqueue("hos.duty", (commandId) => ({ clientEventId: commandId }));
    const held = pending().find((c) => c.id === id);
    expect(held?.payload).toEqual({ clientEventId: id });
  });

  it("resolves on the local write, never on the network — nothing was fetched", async () => {
    const fetchMock = vi.fn<typeof fetch>();
    vi.stubGlobal("fetch", fetchMock);
    const id = await enqueue("incident.report", { type: "Wildlife on roadway" });
    expect(typeof id).toBe("string");
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("is durable: a simulated reload finds the commands in capture order", async () => {
    // "Held on this device" means IndexedDB, not a JavaScript array. Forget the in-memory
    // mirror (what a reload does) and the held commands come back, in the order they were
    // captured — a duty change must precede the certification of its day.
    const a = await enqueue("hos.duty", { duty: "On Duty" });
    const b = await enqueue("hos.duty", { duty: "Driving" });
    const c = await enqueue("hos.certify", { date: "2026-10-10" });

    await resetQueueForTests();
    expect(pending()).toHaveLength(0);

    await ready();
    expect(pending().map((x) => x.id)).toEqual([a, b, c]);
    expect(pending().every((x) => x.status === "pending")).toBe(true);
    expect(getSyncState().pending).toBe(3);
  });
});

describe("drain", () => {
  it("hands each pending command to its kind's sender with the command id, in capture order, and marks it sent", async () => {
    const sent: string[] = [];
    registerSender("hos.duty", async (payload, id) => {
      sent.push(`${(payload as { duty: string }).duty}:${id}`);
    });
    const a = await enqueue("hos.duty", { duty: "On Duty" });
    const b = await enqueue("hos.duty", { duty: "Driving" });

    const result = await drain();

    expect(sent).toEqual([`On Duty:${a}`, `Driving:${b}`]);
    expect(result).toMatchObject({ sent: 2, failed: 0, held: 0, unreachable: false });
    expect(statusOf(a)).toBe("sent");
    expect(statusOf(b)).toBe("sent");
    expect(pending()).toHaveLength(0);
    expect(getSyncState().pending).toBe(0);
    expect(getSyncState().lastSyncedAt).not.toBeNull();
  });

  it("puts the command id on the wire as the Idempotency-Key through the real transport", async () => {
    // The end-to-end pin: queue id → sender → lib/api/hos.ts → header. A retried sync that
    // reached the server once already must be a replay (200), never a second event.
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      new Response(JSON.stringify({ eventId: "x", dutyDayId: "d", localDate: "2026-10-10", created: true }), { status: 201 }),
    );
    vi.stubGlobal("fetch", fetchMock);
    getValidAccessToken.mockResolvedValue("access-1");
    getAccessToken.mockReturnValue("access-1");
    registerSender("hos.duty", async (payload, id) => {
      await recordDutyEvent("drv-1", { eventId: id, atUtc: (payload as { atUtc: string }).atUtc, status: "Driving", source: "Driver App" });
    });
    const id = await enqueue("hos.duty", { duty: "Driving", atUtc: "2026-10-10T20:00:00.000Z" });

    await drain();

    const headers = (fetchMock.mock.calls[0]?.[1]?.headers ?? {}) as Record<string, string>;
    expect(headers["Idempotency-Key"]).toBe(id);
    expect(JSON.parse(String(fetchMock.mock.calls[0]?.[1]?.body)).eventId).toBe(id);
    expect(statusOf(id)).toBe("sent");
  });

  it("counts a 200 replay as sent, exactly like a 201", async () => {
    // A sender resolves on any 2xx; the queue cannot and must not tell them apart.
    registerSender("hos.duty", async () => undefined);
    const id = await enqueue("hos.duty", { duty: "Driving" });
    await drain();
    expect(statusOf(id)).toBe("sent");
  });

  it("parks a 4xx with the server's message and code, and never retries it on its own", async () => {
    const sender = vi.fn(async () => {
      throw rejected("Drivers.Hos.NoStatusChange", "The driver is already in that status.", 400);
    });
    registerSender("hos.duty", sender);
    const id = await enqueue("hos.duty", { duty: "Driving" });

    const first = await drain();
    const second = await drain();

    expect(first.failed).toBe(1);
    expect(second).toMatchObject({ sent: 0, failed: 0 });
    expect(sender).toHaveBeenCalledTimes(1);
    expect(statusOf(id)).toBe("failed");
    expect(failedCommands()[0]).toMatchObject({ id, error: "The driver is already in that status.", errorCode: "Drivers.Hos.NoStatusChange", attempts: 1 });
    expect(getSyncState().failed).toBe(1);
    expect(getSyncState().pending).toBe(0);
  });

  it("leaves a status-0 failure pending, stops the round, and sends it on the next drain", async () => {
    // The transport's "cannot reach the API" is status 0. A dead zone is not a refusal: the
    // command waits, and so does everything behind it rather than each one timing out in turn.
    let reachable = false;
    const sender = vi.fn(async () => {
      if (!reachable) throw rejected("Network.Unreachable", "Cannot reach the API.", 0);
    });
    registerSender("hos.duty", sender);
    const a = await enqueue("hos.duty", { duty: "On Duty" });
    const b = await enqueue("hos.duty", { duty: "Driving" });

    const offline = await drain();

    expect(offline).toMatchObject({ sent: 0, failed: 0, unreachable: true });
    expect(sender).toHaveBeenCalledTimes(1);
    expect(statusOf(a)).toBe("pending");
    expect(statusOf(b)).toBe("pending");
    expect(getSyncState().pending).toBe(2);
    expect(getSyncState().failed).toBe(0);

    reachable = true;
    const online = await drain();

    expect(online).toMatchObject({ sent: 2, unreachable: false });
    expect(statusOf(a)).toBe("sent");
    expect(statusOf(b)).toBe("sent");
  });

  it("treats a 5xx like a dead zone — pending, not parked", async () => {
    registerSender("hos.certify", async () => {
      throw rejected("Http.503", "Service Unavailable", 503);
    });
    const id = await enqueue("hos.certify", { date: "2026-10-10" });
    await drain();
    expect(statusOf(id)).toBe("pending");
    expect(getSyncState().failed).toBe(0);
  });

  it("holds a kind with no sender: captured, counted, never sent, never failed", async () => {
    registerSender("hos.duty", async () => undefined);
    const dvir = await enqueue("dvir.submit", { items: [] });
    const duty = await enqueue("hos.duty", { duty: "Driving" });

    const result = await drain();

    expect(result).toMatchObject({ sent: 1, held: 1 });
    expect(statusOf(dvir)).toBe("pending");
    expect(statusOf(duty)).toBe("sent");
    expect(getSyncState().pending).toBe(1);
  });

  it("retry re-queues a parked command; discard removes it for good, durably", async () => {
    let refuse = true;
    registerSender("hos.duty", async () => {
      if (refuse) throw rejected("Drivers.Hos.EventInFuture", "The event is in the future.", 400);
    });
    const a = await enqueue("hos.duty", { duty: "Driving" });
    const b = await enqueue("hos.duty", { duty: "On Duty" });
    await drain();
    expect(failedCommands().map((c) => c.id)).toEqual([a, b]);

    await discard(b);
    expect(failedCommands().map((c) => c.id)).toEqual([a]);

    refuse = false;
    await retry(a);
    expect(statusOf(a)).toBe("pending");
    await drain();
    expect(statusOf(a)).toBe("sent");

    await resetQueueForTests();
    await ready();
    expect(allCommands().map((c) => c.id)).toEqual([a]);
    expect(statusOf(a)).toBe("sent");
  });

  it("runs single-flight: a drain requested during a drain joins it", async () => {
    let calls = 0;
    registerSender("hos.duty", async () => {
      calls += 1;
      await new Promise((r) => setTimeout(r, 5));
    });
    await enqueue("hos.duty", { duty: "Driving" });
    const [x, y] = await Promise.all([drain(), drain()]);
    expect(x).toBe(y);
    expect(calls).toBe(1);
  });
});

describe("triggers", () => {
  it("drains on start, on every enqueue while online, on the online event, and on the interval", async () => {
    vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    const rounds: number[] = [];
    onDrained((r) => rounds.push(r.sent));
    registerSender("hos.duty", async () => undefined);

    const stop = startSync();
    await vi.waitFor(() => expect(rounds).toHaveLength(1));

    await enqueue("hos.duty", { duty: "Driving" });
    await vi.waitFor(() => expect(rounds).toHaveLength(2));
    expect(rounds[1]).toBe(1);

    window.dispatchEvent(new Event("online"));
    await vi.waitFor(() => expect(rounds).toHaveLength(3));

    await vi.advanceTimersByTimeAsync(DRAIN_INTERVAL_MS);
    await vi.waitFor(() => expect(rounds).toHaveLength(4));

    stop();
    await vi.advanceTimersByTimeAsync(DRAIN_INTERVAL_MS * 3);
    expect(rounds).toHaveLength(4);
  });

  it("does not drain on enqueue before startSync, or for a kind with no sender", async () => {
    const rounds: number[] = [];
    onDrained((r) => rounds.push(r.sent));
    registerSender("hos.duty", async () => undefined);
    await enqueue("hos.duty", { duty: "Driving" });
    await new Promise((r) => setTimeout(r, 10));
    expect(rounds).toHaveLength(0);
  });
});

describe("the two compliance kinds", () => {
  it("are kinds the queue knows, and a 4xx from either sender parks the command with the server's message", async () => {
    expect(COMMAND_KINDS).toContain("hos.exception");
    expect(COMMAND_KINDS).toContain("hos.eld-malfunction");

    registerSender("hos.exception", async () => {
      throw rejected("Drivers.Hos.AdverseKnowabilityRequired", "Adverse driving conditions can only be declared when they could not reasonably have been known before departure (s.76).", 400);
    });
    registerSender("hos.eld-malfunction", async () => {
      throw rejected("Drivers.Hos.EldCodeRequired", "The malfunction code the ELD displayed is required.", 400);
    });
    const exception = await enqueue("hos.exception", { action: "declare", kind: "Adverse driving conditions" });
    const eld = await enqueue("hos.eld-malfunction", { code: "" });

    const result = await drain();

    expect(result).toMatchObject({ sent: 0, failed: 2, held: 0, unreachable: false });
    expect(failedCommands().map((c) => [c.id, c.errorCode, c.error])).toEqual([
      [exception, "Drivers.Hos.AdverseKnowabilityRequired", "Adverse driving conditions can only be declared when they could not reasonably have been known before departure (s.76)."],
      [eld, "Drivers.Hos.EldCodeRequired", "The malfunction code the ELD displayed is required."],
    ]);
    // Parked is final for the payload: a second round sends neither again.
    expect((await drain()).failed).toBe(0);
    expect(getSyncState().failed).toBe(2);
  });

  it("holds them, counted, while only the first two hos.* senders exist — nothing is sent under a kind with no sender", async () => {
    registerSender("hos.duty", async () => undefined);
    registerSender("hos.certify", async () => undefined);
    await enqueue("hos.exception", { action: "withdraw", exceptionId: "x" });
    const result = await drain();
    expect(result).toMatchObject({ sent: 0, failed: 0, held: 1 });
    expect(getSyncState().enabledKinds).toEqual(["hos.certify", "hos.duty"]);
  });
});

describe("sync state contract", () => {
  it("never reports itself enabled while any kind has no sender — the honest-pill rule", () => {
    // If this ever returns enabled:true while dvir.submit is still held, the pill starts
    // telling drivers their compliance records reached the server.
    registerSender("hos.duty", async () => undefined);
    registerSender("hos.certify", async () => undefined);
    expect(getSyncState().enabled).toBe(false);
    expect(getSyncState().enabledKinds).toEqual(["hos.certify", "hos.duty"]);
    registerSender("hos.exception", async () => undefined);
    registerSender("hos.eld-malfunction", async () => undefined);
    expect(getSyncState().enabled).toBe(false);
    expect(getSyncState().enabledKinds).toEqual(["hos.certify", "hos.duty", "hos.eld-malfunction", "hos.exception"]);
  });

  it("satisfies the full SyncState shape the pill renders", () => {
    expect(getSyncState()).toEqual({
      online: expect.any(Boolean),
      pending: expect.any(Number),
      failed: expect.any(Number),
      lastSyncedAt: null,
      enabled: false,
      enabledKinds: [],
    });
  });
});
