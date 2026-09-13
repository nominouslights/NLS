"use client";

import type { AppProps } from "@/components/apps/shell";
import Settings from "@/components/screens/Settings";
import Communications from "@/components/screens/Communications";

// Admin — organization settings, users and roles, communications. Nothing to retain.
export default function AdminApp({ screen }: AppProps) {
  return (
    <>
      {screen === "settings" && <Settings />}
      {screen === "comms" && <Communications />}
    </>
  );
}
