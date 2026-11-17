import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, renderHook } from "@testing-library/react";
import {
  DEFECTS_CHANGED_EVENT,
  PROJECTION_RECONCILE_MS,
  notifyDefectsChanged,
  useDefectsChanged,
} from "./defectSync";

// The cross-surface seam: a resolve on one surface reaches every other mounted
// surface — but only AFTER the projector has run. An immediate re-read would
// see the stale projection and resurrect a row DefectsPanel just dropped
// optimistically, so "never immediately" is as load-bearing as "exactly once".

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

describe("defectSync", () => {
  it("dispatches one namespaced window event per notify", () => {
    const seen = vi.fn();
    window.addEventListener(DEFECTS_CHANGED_EVENT, seen);
    notifyDefectsChanged();
    expect(seen).toHaveBeenCalledTimes(1);
    window.removeEventListener(DEFECTS_CHANGED_EVENT, seen);
  });

  it("never calls back immediately — only once the projector has certainly run", () => {
    const cb = vi.fn();
    renderHook(() => useDefectsChanged(cb));

    notifyDefectsChanged();
    expect(cb).not.toHaveBeenCalled();
    vi.advanceTimersByTime(PROJECTION_RECONCILE_MS - 1);
    expect(cb).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1);
    expect(cb).toHaveBeenCalledTimes(1);
  });

  it("debounces a burst into ONE call, timed from the last event", () => {
    const cb = vi.fn();
    renderHook(() => useDefectsChanged(cb));

    notifyDefectsChanged();
    vi.advanceTimersByTime(3000);
    notifyDefectsChanged();
    vi.advanceTimersByTime(3000);
    notifyDefectsChanged();
    // 6s after the FIRST event — the burst is still settling.
    expect(cb).not.toHaveBeenCalled();
    vi.advanceTimersByTime(PROJECTION_RECONCILE_MS);
    expect(cb).toHaveBeenCalledTimes(1);
    // Nothing left queued.
    vi.advanceTimersByTime(PROJECTION_RECONCILE_MS * 3);
    expect(cb).toHaveBeenCalledTimes(1);
  });

  it("calls the LATEST callback, so callers need not memoize", () => {
    const first = vi.fn();
    const second = vi.fn();
    const { rerender } = renderHook(({ cb }) => useDefectsChanged(cb), { initialProps: { cb: first } });

    notifyDefectsChanged();
    rerender({ cb: second });
    vi.advanceTimersByTime(PROJECTION_RECONCILE_MS);
    expect(first).not.toHaveBeenCalled();
    expect(second).toHaveBeenCalledTimes(1);
  });

  it("clears a pending timer and stops listening on unmount", () => {
    const cb = vi.fn();
    const { unmount } = renderHook(() => useDefectsChanged(cb));

    notifyDefectsChanged();
    unmount();
    vi.advanceTimersByTime(PROJECTION_RECONCILE_MS * 2);
    expect(cb).not.toHaveBeenCalled();

    notifyDefectsChanged();
    vi.advanceTimersByTime(PROJECTION_RECONCILE_MS * 2);
    expect(cb).not.toHaveBeenCalled();
  });

  it("reaches every mounted listener", () => {
    const a = vi.fn();
    const b = vi.fn();
    renderHook(() => useDefectsChanged(a));
    renderHook(() => useDefectsChanged(b));

    notifyDefectsChanged();
    vi.advanceTimersByTime(PROJECTION_RECONCILE_MS);
    expect(a).toHaveBeenCalledTimes(1);
    expect(b).toHaveBeenCalledTimes(1);
  });
});
