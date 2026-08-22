import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Northern Link — Budgeting",
  description: "Northern Link Shuttle & Cargo — zero-based budgeting console",
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
            the whole app in a way that is very hard to spot side by side. */}
        <link rel="preconnect" href="https://fonts.googleapis.com" />
        <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="anonymous" />
        <link
          href="https://fonts.googleapis.com/css2?family=Barlow+Condensed:wght@500;600;700&family=Barlow:ital,wght@0,400;0,500;0,600;0,700;1,400;1,500&family=Barlow+Semi+Condensed:wght@500;600&family=JetBrains+Mono:wght@400;500&display=swap"
          rel="stylesheet"
        />
      </head>
      <body>{children}</body>
    </html>
  );
}
