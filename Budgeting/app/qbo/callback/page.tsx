import type { Metadata } from "next";
import QboCallback from "@/components/QboCallback";

// Intuit's OAuth redirect target: http://localhost:3003/qbo/callback in dev,
// https://budget.<domain>/qbo/callback in production (register both on the Intuit app). Not
// under /api, so next.config.ts's /api/* rewrite never touches it, and basePath — when set —
// prefixes it like any other page.
//
// A server component only so it can export metadata; everything it renders is the client
// component. `referrer: "no-referrer"` keeps the one-time code on the arriving URL out of any
// Referer header, alongside the client stripping the query on mount.

export const metadata: Metadata = {
  title: "Connecting QuickBooks — Northern Link Budgeting",
  referrer: "no-referrer",
  robots: { index: false, follow: false },
};

export default function QboCallbackPage() {
  return <QboCallback />;
}
