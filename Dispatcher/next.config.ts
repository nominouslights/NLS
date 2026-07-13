import type { NextConfig } from "next";

// The browser only ever talks to this dev server's own origin (localhost:3001) — no CORS
// needed. Next.js proxies /api/* server-side to the real Fleet API, so the cross-origin hop
// happens Node-to-.NET, which browsers don't restrict. `services__api__http__0` is the URL
// Aspire injects when the AppHost wires .WithReference(api) into this resource; the literal
// fallback matches launchSettings.json's standalone port for `dotnet run` without the AppHost.
const apiOrigin =
  process.env.services__api__http__0 ??
  process.env.API_PROXY_TARGET ??
  "http://localhost:5215";

const nextConfig: NextConfig = {
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
