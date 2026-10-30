import { describe, expect, it } from "vitest";
import { activityEntryView } from "./tripActivity";
import type { TripActivityEntry } from "./api/trips";

// One activity-log row. The server nulls reason, detail and enteredBy on trip
// events for driver-role callers; the console must still render the label and
// simply drop the other lines.

const entry = (over: Partial<TripActivityEntry>): TripActivityEntry => ({
  occurredAtUtc: "2026-10-14T12:00:00Z",
  aggregateType: "trip",
  eventType: "trip-driver-corrected",
  source: null,
  enteredBy: "Dana Dispatcher",
  reason: "Swap never entered",
  detail: "Driver: Old → New",
  ...over,
});

describe("activityEntryView", () => {
  it("renders the label only when reason, detail and enteredBy are all null", () => {
    expect(activityEntryView(entry({ reason: null, detail: null, enteredBy: null }))).toEqual({
      isManifest: false,
      label: "Driver corrected",
      detail: null,
      reason: null,
      by: null,
    });
  });

  it("treats the three fields being absent from the wire the same as null", () => {
    const view = activityEntryView({
      occurredAtUtc: "2026-10-14T12:00:00Z",
      aggregateType: "trip",
      eventType: "trip-driver-corrected",
      source: null,
      enteredBy: null,
    });
    expect(view).toMatchObject({ label: "Driver corrected", detail: null, reason: null, by: null });
  });

  it("a Dispatcher-sourced trip event with a null enteredBy reads plain 'Dispatcher'", () => {
    const view = activityEntryView(entry({ source: "Dispatcher", reason: null, detail: null, enteredBy: null }));
    expect(view.label).toBe("Dispatcher · Driver corrected");
    expect(view.by).toBeNull();
  });

  it("shows trimmed detail, reason and author when present", () => {
    expect(activityEntryView(entry({ enteredBy: "  Dana Dispatcher " }))).toEqual({
      isManifest: false,
      label: "Driver corrected",
      detail: "Driver: Old → New",
      reason: "Swap never entered",
      by: "Dana Dispatcher",
    });
  });

  it("blank strings count as absent", () => {
    expect(activityEntryView(entry({ reason: "  ", detail: "\n", enteredBy: " " }))).toMatchObject({
      detail: null,
      reason: null,
      by: null,
    });
  });

  it("manifest events never get a 'by' line — the label already names the author", () => {
    const view = activityEntryView(
      entry({ aggregateType: "trip-manifest", eventType: "trip-manifest-updated", source: "Dispatcher" }),
    );
    expect(view.label).toBe("Dispatcher (Dana Dispatcher) · Manifest updated");
    expect(view.by).toBeNull();
    expect(view.isManifest).toBe(true);
  });

  it("unknown event types fall back to their de-kebabed form", () => {
    expect(activityEntryView(entry({ eventType: "trip-something-new" })).label).toBe("trip something new");
  });
});
