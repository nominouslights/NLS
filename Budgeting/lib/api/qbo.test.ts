import { describe, expect, it } from "vitest";
import {
  isLiveQboConnection,
  parseQboCallback,
  QBO_STATUS_DISPLAY,
  reconnectWarningDue,
  RECONNECT_WARNING_DAYS,
  type QboConnectionStatus,
} from "./qbo";

// The pure half of the QuickBooks client.

describe("QBO_STATUS_DISPLAY", () => {
  it("covers every status QboConnectionResponse can send, each with colour + glyph + label", () => {
    // NotConnected is QboConnectionResponse.NotConnectedStatus; the rest are QboConnectionStatus.
    const all: QboConnectionStatus[] = ["NotConnected", "Active", "NeedsReconnect", "Disconnected"];
    expect(Object.keys(QBO_STATUS_DISPLAY).sort()).toEqual([...all].sort());
    for (const s of all) {
      expect(QBO_STATUS_DISPLAY[s].glyph).not.toBe("");
      expect(QBO_STATUS_DISPLAY[s].label).not.toBe("");
    }
  });

  it("is teal ✓ Connected / gold ! Reconnect needed / gray Not connected", () => {
    expect(QBO_STATUS_DISPLAY.Active).toEqual({ kind: "ontime", glyph: "✓", label: "Connected" });
    expect(QBO_STATUS_DISPLAY.NeedsReconnect).toEqual({ kind: "soon", glyph: "!", label: "Reconnect needed" });
    expect(QBO_STATUS_DISPLAY.Disconnected.kind).toBe("off");
    expect(QBO_STATUS_DISPLAY.Disconnected.label).toBe("Not connected");
    expect(QBO_STATUS_DISPLAY.NotConnected.kind).toBe("off");
    expect(QBO_STATUS_DISPLAY.NotConnected.label).toBe("Not connected");
  });
});

describe("isLiveQboConnection", () => {
  it("mirrors QboConnection.IsLive (Status != Disconnected), plus NotConnected having no row", () => {
    expect(isLiveQboConnection("Active")).toBe(true);
    expect(isLiveQboConnection("NeedsReconnect")).toBe(true);
    expect(isLiveQboConnection("Disconnected")).toBe(false);
    expect(isLiveQboConnection("NotConnected")).toBe(false);
  });
});

describe("reconnectWarningDue", () => {
  const now = new Date("2026-10-08T12:00:00Z");
  const DAY = 24 * 60 * 60 * 1000;
  const at = (ms: number) => new Date(now.getTime() + ms).toISOString();

  it("is 14 days", () => {
    expect(RECONNECT_WARNING_DAYS).toBe(14);
  });

  it("warns strictly inside 14 days, not at exactly 14", () => {
    expect(reconnectWarningDue(at(14 * DAY), now)).toBe(false);
    expect(reconnectWarningDue(at(14 * DAY - 1), now)).toBe(true);
    expect(reconnectWarningDue(at(100 * DAY), now)).toBe(false);
    expect(reconnectWarningDue(at(1 * DAY), now)).toBe(true);
  });

  it("warns once the expiry has passed", () => {
    expect(reconnectWarningDue(at(-DAY), now)).toBe(true);
  });

  it("never warns without an expiry (NotConnected / Disconnected) or on an unreadable one", () => {
    expect(reconnectWarningDue(null, now)).toBe(false);
    expect(reconnectWarningDue("not a date", now)).toBe(false);
  });

  it("reads the server's offset form", () => {
    expect(reconnectWarningDue("2026-10-15T12:00:00+00:00", now)).toBe(true);
  });
});

describe("parseQboCallback", () => {
  it("reads Intuit's three values", () => {
    expect(parseQboCallback("?code=c-1&state=s-1&realmId=9341452938471234")).toEqual({
      kind: "complete",
      input: { code: "c-1", state: "s-1", realmId: "9341452938471234" },
    });
  });

  it("reads a missing value as null — posted as-is, for the server's CallbackIncomplete", () => {
    expect(parseQboCallback("?code=c-1")).toEqual({
      kind: "complete",
      input: { code: "c-1", state: null, realmId: null },
    });
  });

  it("treats ?error= as a refusal to post, whatever else came with it", () => {
    expect(parseQboCallback("?error=access_denied&state=s-1")).toEqual({
      kind: "error",
      error: "access_denied",
      description: null,
    });
    expect(parseQboCallback("?error=invalid_scope&error_description=Bad+scope")).toEqual({
      kind: "error",
      error: "invalid_scope",
      description: "Bad scope",
    });
  });
});
