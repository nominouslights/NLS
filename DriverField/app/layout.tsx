import type { Metadata, Viewport } from "next";
import "./globals.css";
import ServiceWorkerRegistration from "@/components/ServiceWorkerRegistration";
import OrientationNotice from "@/components/OrientationNotice";

export const metadata: Metadata = {
  title: "Northern Link — Driver",
  description: "Northern Link Shuttle & Cargo — driver field app",
  appleWebApp: { capable: true, title: "NL Driver", statusBarStyle: "black-translucent" },
};

// Fixed to the target device rather than the viewer's window. This app runs on one piece of
// hardware — a company-issued 10-inch landscape tablet (architecture non-negotiable #5) — so
// there is no responsive range to describe and no phone breakpoint to honour.
export const viewport: Viewport = {
  width: 1280,
  initialScale: 1,
  viewportFit: "cover",
  themeColor: "#102A43",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <head>
        {/* These four families and their exact weight lists are byte-identical to
            Dispatcher/app/layout.tsx on purpose. Every size, weight and letter-spacing in
            lib/theme.ts was tuned against them, so trimming a weight here shifts type across
            the whole app in a way that is very hard to spot side by side.

            KNOWN DEBT, and it is this app's debt specifically: fetching them from
            fonts.googleapis.com at runtime defeats the offline story. A first run in a dead
            zone renders fallback families, at which point theme.ts's tuned sizes are simply
            wrong. CommunityMobile/ bundles the same families as local assets for exactly this
            reason. Self-hosting them as woff2 under public/fonts/ is a change to this file —
            which is adapted, not a protected copy, so it is legal — and it belongs in the
            offline batch. See DriverField/CLAUDE.md. */}
        <link rel="preconnect" href="https://fonts.googleapis.com" />
        <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="anonymous" />
        <link
          href="https://fonts.googleapis.com/css2?family=Barlow+Condensed:wght@500;600;700&family=Barlow:ital,wght@0,400;0,500;0,600;0,700;1,400;1,500&family=Barlow+Semi+Condensed:wght@500;600&family=JetBrains+Mono:wght@400;500&display=swap"
          rel="stylesheet"
        />
      </head>
      <body>
        <ServiceWorkerRegistration />
        <OrientationNotice />
        {children}
      </body>
    </html>
  );
}
