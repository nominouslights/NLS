// Northern Link Driver Field App — service worker.
//
// Scope in this scaffold: make the app shell survive a reload with no network. That is all.
// There is no background sync and no command queue here — that is lib/sync/, and it lands in
// the offline batch.
//
// Two rules that must not be relaxed:
//   1. NOTHING under /api/ is ever cached. Serving a stale auth response or a stale manifest
//      would be worse than failing — a driver acting on yesterday's passenger list is a real
//      operational problem, and a cached 200 hides it completely.
//   2. The shell is cache-first, everything else falls through to the network. A driver opening
//      the app in a dead zone must get the UI, not a browser error page.

const VERSION = "v1";
const SHELL_CACHE = `nl-driver-shell-${VERSION}`;

const SHELL_URLS = [
  "./",
  "./icons/icon-192.png",
  "./icons/icon-512.png",
  "./icons/icon-maskable-512.png",
];

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches
      .open(SHELL_CACHE)
      // addAll is atomic — one 404 rejects the whole install. Add individually so a missing
      // icon degrades the cache instead of leaving the app with no worker at all.
      .then((cache) => Promise.all(SHELL_URLS.map((url) => cache.add(url).catch(() => {}))))
      .then(() => self.skipWaiting()),
  );
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) =>
        Promise.all(keys.filter((k) => k !== SHELL_CACHE).map((k) => caches.delete(k))),
      )
      .then(() => self.clients.claim()),
  );
});

self.addEventListener("fetch", (event) => {
  const request = event.request;
  if (request.method !== "GET") return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;

  // Rule 1. Let these fail honestly when offline.
  if (url.pathname.includes("/api/")) return;

  // Build output is content-hashed, so a cache hit is always the right bytes.
  const isStatic =
    url.pathname.includes("/_next/static/") || url.pathname.includes("/icons/");

  if (isStatic) {
    event.respondWith(
      caches.match(request).then(
        (hit) =>
          hit ??
          fetch(request).then((res) => {
            const copy = res.clone();
            caches.open(SHELL_CACHE).then((cache) => cache.put(request, copy));
            return res;
          }),
      ),
    );
    return;
  }

  // Rule 2: a navigation offline renders the cached shell rather than the browser's error page.
  if (request.mode === "navigate") {
    event.respondWith(
      fetch(request).catch(() =>
        caches.match("./").then((hit) => hit ?? Response.error()),
      ),
    );
  }
});
