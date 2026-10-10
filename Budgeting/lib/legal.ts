// Links to the owner's Privacy Policy and End-User Licence Agreement.
//
// The document is a static file, public/legal/policies.html — a byte-identical copy of the same
// file in Website/, Dispatcher/ and DriverField/ (see CLAUDE.md, "Legal policies"). Next serves
// public/ before any app code runs, so the page is reachable signed out: AuthGate is a client
// component inside app/page.tsx and never sees the request, and the only rewrite is /api/*.
//
// Its #privacy and #eula URLs are the ones registered on the Intuit (QuickBooks) app profile, so
// the anchors are a contract with that listing — do not rename them here without the owner.
//
// Plain <a> hrefs do not get Next's basePath, so it is prefixed by hand. BASE_PATH is baked at
// build time (Dockerfile ARG) and exposed to the client through next.config.ts's `env` block.

export type LegalSection = "privacy" | "eula";

export const LEGAL_DOCUMENT_PATH = "/legal/policies.html";

export const LEGAL_LINKS: ReadonlyArray<{ section: LegalSection; label: string }> = [
  { section: "privacy", label: "Privacy Policy" },
  { section: "eula", label: "Licence Agreement" },
];

export function legalHref(
  section: LegalSection,
  basePath: string = process.env.BASE_PATH ?? "",
): string {
  // Tolerate a trailing slash on the prefix so "/budget/" cannot produce "//legal".
  const prefix = basePath.replace(/\/+$/, "");
  return `${prefix}${LEGAL_DOCUMENT_PATH}#${section}`;
}
