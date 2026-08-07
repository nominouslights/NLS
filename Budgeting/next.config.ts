import type { NextConfig } from "next";

// The API origin, resolved in priority order:
//   1. services__api__http__0 — injected by the Aspire AppHost via .WithReference(api)
//   2. API_PROXY_TARGET       — manual override
//   3. http://localhost:5215  — the port a standalone `dotnet run` binds (launchSettings.json)
const apiOrigin =
  process.env.services__api__http__0 ??
  process.env.API_PROXY_TARGET ??
  "http://localhost:5215";

const nextConfig: NextConfig = {
  // The browser only ever talks to this app's own origin (:3003); Next proxies /api/* to the
  // API server-side. That is why there is no CORS configuration anywhere in the stack — keep
  // it that way rather than adding a CORS policy to the backend.
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${apiOrigin}/api/:path*` }];
  },
};

export default nextConfig;
