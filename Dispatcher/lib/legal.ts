// The owner's Privacy Policy + End-User Licence Agreement — one static page shipped in
// public/legal/policies.html (a byte-identical copy shared by the four frontends; never edit it
// here). It is a public URL submitted to Intuit and the app stores, so it must stay reachable
// without signing in: it is served straight from public/, outside the client-side AuthGate,
// and nothing in next.config.ts rewrites or guards it.
//
// public/ files are served under basePath, but a plain <a href> is NOT prefixed by Next — so the
// prefix is applied here. NEXT_PUBLIC_BASE_PATH is set from BASE_PATH in next.config.ts and
// bakes in at build time exactly like basePath itself.

export function basePath(): string {
  return (process.env.NEXT_PUBLIC_BASE_PATH ?? "").replace(/\/+$/, "");
}

export function policiesHref(anchor: "privacy" | "eula"): string {
  return `${basePath()}/legal/policies.html#${anchor}`;
}

export const LEGAL_LINKS = [
  { label: "Privacy Policy", anchor: "privacy" },
  { label: "Licence Agreement", anchor: "eula" },
] as const;
