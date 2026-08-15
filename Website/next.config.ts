import type { NextConfig } from "next";

// No rewrites yet — this site is a marketing prototype with no API calls. When the
// forms are wired to the real backend, add a /api/* proxy here mirroring
// Dispatcher/next.config.ts (the browser only ever talks to this server's own origin;
// Next.js proxies server-side to the .NET API — no CORS anywhere in the stack).
const nextConfig: NextConfig = {
  // Self-contained server bundle (.next/standalone/server.js) with only the traced
  // dependencies, so the runtime image carries no node_modules tree and no build toolchain.
  output: "standalone",
};

export default nextConfig;
