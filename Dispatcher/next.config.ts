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
// named northernlink-api and listen on 8080. If a genuinely runtime-configurable target is ever needed,
// this rewrite has to become an app/api/[...path]/route.ts handler.
const apiOrigin =
  process.env.services__api__http__0 ??
  process.env.API_PROXY_TARGET ??
  "http://localhost:5215";

// Mount point, when this console is served under a path prefix rather than its own hostname.
// Empty for local dev and for any hostname-per-app deployment; "/dispatch" in the DigitalOcean
// image, where all three frontends share one domain (see .do/app.yaml).
//
// BAKES IN at build time exactly like the proxy target above — basePath is compiled into every
// emitted asset URL, so an image built with BASE_PATH="/dispatch" only works when it is
// actually mounted at /dispatch, and the App Platform ingress rule must set
// preserve_path_prefix: true so the prefix survives to this server.
//
// Two halves have to agree, and NEITHER is optional:
//   * basePath below — makes Next serve /_next/* and its own routes under the prefix
//   * NEXT_PUBLIC_API_BASE_URL (lib/api/transport.ts) — makes browser fetches target
//     /dispatch/api/... instead of /api/..., which would otherwise land on whichever
//     component owns "/" and 404
const basePath = process.env.BASE_PATH ?? "";

const nextConfig: NextConfig = {
  // Self-contained server bundle (.next/standalone/server.js) with only the traced
  // dependencies, so the runtime image carries no node_modules tree and no build toolchain.
  output: "standalone",

  // Next rejects an empty string here — omit the key entirely when unmounted.
  ...(basePath ? { basePath } : {}),

  async rewrites() {
    return [
      {
        // Written out with the prefix explicit and basePath:false rather than letting Next
        // prepend it: the destination is an absolute URL on another host, and leaving the
        // prefixing implicit makes it far too easy to double it or drop it.
        source: `${basePath}/api/:path*`,
        destination: `${apiOrigin}/api/:path*`,
        basePath: false,
      },
    ];
  },
};

export default nextConfig;
