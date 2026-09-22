import type { NextConfig } from "next";

// The API origin, resolved in priority order:
//   1. services__api__http__0 — injected by the Aspire AppHost via .WithReference(api)
//   2. API_PROXY_TARGET       — manual override, and how the container image is built
//   3. http://localhost:5215  — the port a standalone `dotnet run` binds (launchSettings.json)
//
// IMPORTANT for the container image: rewrites are evaluated by `next build` and written into
// .next/routes-manifest.json, which the production server reads instead of re-running this
// function. So this value BAKES IN at build time — setting API_PROXY_TARGET on a running
// container does nothing. Dockerfile passes it as a build ARG (http://northernlink-api:8080);
// per-environment resolution comes from the deployment's private DNS — on DigitalOcean App
// Platform, a component's name is its hostname on the app's LAN, so the API component must be
// named northernlink-api and listen on 8080.
const apiOrigin =
  process.env.services__api__http__0 ??
  process.env.API_PROXY_TARGET ??
  "http://localhost:5215";

// Mount point, when this app is served under a path prefix rather than its own hostname.
// Empty for local dev; "/driver" in the DigitalOcean image, where the frontends share one
// domain (see .do/app.yaml). Bakes in at build time exactly like the proxy target above, and
// must be paired with NEXT_PUBLIC_API_BASE_URL — see Dispatcher/next.config.ts for the full
// explanation of why both halves are required.
const basePath = process.env.BASE_PATH ?? "";

const nextConfig: NextConfig = {
  // Self-contained server bundle (.next/standalone/server.js) with only the traced
  // dependencies, so the runtime image carries no node_modules tree and no build toolchain.
  //
  // NOTE for this app specifically: `standalone` does NOT copy public/ into .next/standalone.
  // Unlike the other consoles this app HAS a public/ (the service worker and the PWA icons
  // live there), so the Dockerfile's `COPY /app/public ./public` is load-bearing here, not the
  // no-op it is in Budgeting. Remove it and the manifest, the icons and sw.js all 404, and the
  // install prompt silently never appears.
  output: "standalone",

  // Next rejects an empty string here — omit the key entirely when unmounted.
  ...(basePath ? { basePath } : {}),

  // The browser only ever talks to this app's own origin (:3004); Next proxies /api/* to the
  // API server-side. That is why there is no CORS configuration anywhere in the stack — keep
  // it that way rather than adding a CORS policy to the backend.
  async rewrites() {
    return [
      {
        source: `${basePath}/api/:path*`,
        destination: `${apiOrigin}/api/:path*`,
        basePath: false,
      },
    ];
  },

  // A browser will happily pin a stale service worker for as long as its cache headers allow,
  // which turns "ship a fix" into "ship a fix and wait". sw.js must always be revalidated.
  async headers() {
    return [
      {
        source: `${basePath}/sw.js`,
        headers: [
          { key: "Cache-Control", value: "no-cache, no-store, must-revalidate" },
          { key: "Service-Worker-Allowed", value: basePath || "/" },
        ],
      },
    ];
  },
};

export default nextConfig;
