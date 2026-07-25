"use client";

import { useEffect, useState } from "react";

/**
 * Free public UTC time sources, tried in order. Each returns an ISO-8601 UTC
 * timestamp; we only ever read UTC and let the browser do the zone conversion,
 * so a dispatcher's date is right even if their machine clock has drifted.
 */
const UTC_SOURCES: { url: string; read: (json: any) => string | undefined }[] = [
  { url: "https://worldtimeapi.org/api/timezone/Etc/UTC", read: (j) => j?.utc_datetime },
  { url: "https://timeapi.io/api/Time/current/zone?timeZone=UTC", read: (j) => j?.dateTime },
];

async function fetchUtcMillis(signal: AbortSignal): Promise<number | null> {
  for (const source of UTC_SOURCES) {
    try {
      const res = await fetch(source.url, { signal, cache: "no-store" });
      if (!res.ok) continue;
      const raw = source.read(await res.json());
      if (!raw) continue;
      // timeapi.io omits the zone designator; worldtimeapi includes an offset.
      const ms = Date.parse(/[Zz]|[+-]\d\d:?\d\d$/.test(raw) ? raw : `${raw}Z`);
      if (!Number.isNaN(ms)) return ms;
    } catch {
      if (signal.aborted) return null;
    }
  }
  return null;
}

/**
 * Today's date as seen in the browser's own timezone, sourced from a network
 * UTC clock when reachable and falling back to the local clock when not.
 * Returns e.g. `{ label: "TUE JUL 7", time: "14:32:07", zone: "CDT", synced: true }`.
 * `time` ticks once a second.
 */
export function useToday(): { label: string; time: string; zone: string; synced: boolean } {
  // Skew between the authoritative UTC clock and this machine's clock, in ms.
  const [skewMs, setSkewMs] = useState<number | null>(null);
  const [tick, setTick] = useState(0);
  // The server renders in its own timezone, so hold the label back until we're
  // in the browser and can use the viewer's zone.
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
    const controller = new AbortController();

    const sync = async () => {
      const utcMillis = await fetchUtcMillis(controller.signal);
      if (utcMillis !== null) setSkewMs(utcMillis - Date.now());
    };

    void sync();
    // Tick every second to drive the running clock (and, at local midnight,
    // roll the date over). Re-sync hourly so long-lived sessions on a dispatch
    // room screen don't slowly inherit machine clock drift.
    const second = setInterval(() => setTick((t) => t + 1), 1_000);
    const hour = setInterval(() => void sync(), 3_600_000);

    return () => {
      controller.abort();
      clearInterval(second);
      clearInterval(hour);
    };
  }, []);

  // Rendered from the browser's resolved timezone — no zone is hardcoded.
  const now = new Date(Date.now() + (skewMs ?? 0));
  void tick;

  const label = new Intl.DateTimeFormat("en-CA", {
    weekday: "short",
    month: "short",
    day: "numeric",
  })
    .format(now)
    .replace(/,/g, "")
    .toUpperCase();

  // 24-hour with seconds — unambiguous for dispatch/HOS work, where a misread
  // AM/PM is a real operational error.
  const time = new Intl.DateTimeFormat("en-CA", {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false,
  }).format(now);

  // Short zone name (e.g. "CDT") so a dispatcher reading over someone's
  // shoulder in another region knows whose clock this is.
  const zone =
    new Intl.DateTimeFormat("en-CA", { timeZoneName: "short" })
      .formatToParts(now)
      .find((p) => p.type === "timeZoneName")?.value ?? "";

  return {
    label: mounted ? label : "—",
    time: mounted ? time : "--:--:--",
    zone: mounted ? zone : "",
    synced: skewMs !== null,
  };
}
