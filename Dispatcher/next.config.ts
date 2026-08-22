import type { NextConfig } from "next";

// The browser only ever talks to this dev server's own origin (localhost:3001) — no CORS
// needed. Next.js proxies /api/* server-side to the real Fleet API, so the cross-origin hop
// happens Node-to-.NET, which browsers don't restrict. `services__api__http__0` is the URL
// Aspire injects when the AppHost wires .WithReference(api) into this resource; the literal
// fallback matches launchSettings.json's standalone port for `dotnet run` without the AppHost.
//
// IMPORTANT for the container image: rewrites are evaluated by `next build` and written into
// .next/routes-manifest.json, which the production server reads instead of re-running this
// function. So this value BAKES IN at build time — setting API_PROXY_TARGET on a running
// container does nothing. Dockerfile passes it as a build ARG (http://northernlink-api:8080);
// per-environment resolution comes from Kubernetes DNS, since a bare Service name resolves
// inside the pod's own namespace. If a genuinely runtime-configurable target is ever needed,
// this rewrite has to become an app/api/[...path]/route.ts handler.
const apiOrigin =
  process.env.services__api__http__0 ??
  process.env.API_PROXY_TARGET ??
  "http://localhost:5215";

const nextConfig: NextConfig = {
  // Self-contained server bundle (.next/standalone/server.js) with only the traced
  // dependencies, so the runtime image carries no node_modules tree and no build toolchain.
  output: "standalone",

  async rewrites() {
    return [
      {
        source: "/api/:path*",
        destination: `${apiOrigin}/api/:path*`,
      },
    ];
  },
};

export default nextConfig;
