"use client";

import { useEffect } from "react";

// Registers public/sw.js.
//
// PRODUCTION ONLY, and that is not a nicety. A service worker in dev caches turbopack chunks
// and then serves them back after you have edited the source — the single most confusing bug
// class in this stack, because the code on screen is not the code on disk and nothing says so.
//
// The worker itself is hand-written (~40 lines) rather than next-pwa or Serwist. This scaffold
// implements no offline behaviour, so a fourth runtime dependency that rewrites the build would
// be paying a permanent cost for a future benefit. Revisit when the offline batch lands and
// there is real caching strategy to express.
export default function ServiceWorkerRegistration() {
  useEffect(() => {
    if (process.env.NODE_ENV !== "production") return;
    if (typeof navigator === "undefined" || !("serviceWorker" in navigator)) return;

    // basePath, when this app is mounted under a path prefix. Derived from the document rather
    // than an env var so it is correct in both builds without a second thing to keep in step.
    const scope = new URL(".", document.baseURI).pathname;

    navigator.serviceWorker.register(`${scope}sw.js`, { scope }).catch(() => {
      // A failed registration costs offline caching, nothing else — the app works either way,
      // and a driver cannot act on this. Do not surface it.
    });
  }, []);

  return null;
}
