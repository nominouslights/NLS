// Where the owner's Privacy Policy and Licence Agreement live.
//
// public/legal/policies.html is a byte-identical copy of one document shared by four apps —
// never edit it here. Next serves public/ at `${basePath}/…`, so the href must carry the
// prefix: "/legal/policies.html" on a local build, "/driver/legal/policies.html" in the image.
//
// A plain <a>, not next/link: this is a static file, not a route, and next/link would try a
// client-side transition (an RSC fetch that 404s) before falling back. And not a RELATIVE href
// either — with trailingSlash off the app is served at "/driver" (no slash), against which
// "legal/policies.html" resolves to "/legal/…" and misses the prefix.
//
// NEXT_PUBLIC_BASE_PATH is set from BASE_PATH in next.config.ts's `env` block, so it bakes in
// at build time from the same single source as `basePath` itself — no second build arg to keep
// in step.

export const POLICY_PATH = "/legal/policies.html";

export type PolicyAnchor = "privacy" | "eula";

export function policyHref(
  anchor: PolicyAnchor,
  basePath: string = process.env.NEXT_PUBLIC_BASE_PATH ?? "",
): string {
  return `${basePath.replace(/\/+$/, "")}${POLICY_PATH}#${anchor}`;
}
