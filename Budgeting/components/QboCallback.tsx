"use client";

import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { colors, fonts } from "@/lib/theme";
import { Panel } from "@/components/ui/Panel";
import { Wordmark } from "@/components/Brandmark";
import { ErrorNotice } from "@/components/ErrorNotice";
import AuthGate from "@/components/AuthGate";
import { ApiError } from "@/lib/api/transport";
import { completeQboConnection, parseQboCallback, type QboCallbackParams } from "@/lib/api/qbo";
import { writeLandingScreen } from "@/lib/landing";

// Body of app/qbo/callback — where Intuit sends the browser after the QuickBooks sign-in. The
// API is never public, so Intuit redirects here and this page relays the three query values to
// POST /api/budgeting/qbo/connection/complete, authenticated as the signed-in planner (the
// `state` is bound to the user who pressed CONNECT; anyone else gets StateInvalid).
//
//   1. On mount, capture the query and strip it from the address bar (history.replaceState), so
//      the one-time code never sits in history, a bookmark or a screenshot. Captured into a ref
//      before AuthGate's session restore can take any time, and kept there through a login.
//   2. Inside the same AuthGate / RoleGate as the console: `?error=` (access_denied when the
//      person declines) is shown and nothing is posted; otherwise post { code, state, realmId }.
//   3. Every outcome leaves a landing hint (lib/landing.ts) so the console opens on the
//      QuickBooks screen. On success the page goes straight to the app root; on a refusal it
//      shows the server's message verbatim with a link back.
//
// The post happens once per page load even under React's dev double-invoked effects: the
// promise is held in a ref and each effect run only subscribes to it. A second POST would be
// refused (the state is single use) and could paint an error over a success.
//
// Navigation goes through next/navigation and next/link, which add next.config.ts's basePath
// themselves — "/" is the console root whatever the mount point.

type Outcome =
  | { kind: "connected" }
  | { kind: "declined"; error: string; description: string | null }
  | { kind: "failed"; message: string; code: string };

function finish(params: QboCallbackParams): Promise<Outcome> {
  if (params.kind === "error") {
    return Promise.resolve({ kind: "declined", error: params.error, description: params.description });
  }
  return completeQboConnection(params.input).then(
    (): Outcome => ({ kind: "connected" }),
    (e: unknown): Outcome =>
      e instanceof ApiError
        ? { kind: "failed", message: e.message, code: e.code }
        : { kind: "failed", message: "Could not finish connecting QuickBooks.", code: "Unknown" },
  );
}

export default function QboCallback() {
  const captured = useRef<QboCallbackParams | null>(null);

  /** Reads and strips the query exactly once; every later call returns the first reading. */
  const capture = useCallback((): QboCallbackParams => {
    if (captured.current === null) {
      captured.current = parseQboCallback(window.location.search);
      window.history.replaceState(null, "", window.location.pathname);
    }
    return captured.current;
  }, []);

  useEffect(() => {
    capture();
  }, [capture]);

  return (
    <AuthGate>
      <QboCallbackFinish capture={capture} />
    </AuthGate>
  );
}

function QboCallbackFinish({ capture }: { capture: () => QboCallbackParams }) {
  const router = useRouter();
  const pending = useRef<Promise<Outcome> | null>(null);
  const [outcome, setOutcome] = useState<Outcome | null>(null);

  useEffect(() => {
    let active = true;
    if (pending.current === null) pending.current = finish(capture());
    pending.current.then((o) => {
      if (!active) return;
      writeLandingScreen("qbo");
      setOutcome(o);
      if (o.kind === "connected") router.replace("/");
    });
    return () => {
      active = false;
    };
  }, [capture, router]);

  let content: ReactNode;
  if (outcome === null) {
    content = <Line>Finishing the QuickBooks connection…</Line>;
  } else if (outcome.kind === "connected") {
    content = <Line>QuickBooks is connected. Opening the console…</Line>;
  } else if (outcome.kind === "declined") {
    content = (
      <>
        <ErrorNotice
          title="QuickBooks was not connected"
          message={
            outcome.error === "access_denied"
              ? "Access was declined in QuickBooks, so nothing was connected."
              : (outcome.description ?? "QuickBooks returned an error, so nothing was connected.")
          }
          code={outcome.error}
        />
        <BackLink />
      </>
    );
  } else {
    content = (
      <>
        <ErrorNotice title="QuickBooks was not connected" message={outcome.message} code={outcome.code} />
        <BackLink />
      </>
    );
  }

  return (
    <div
      style={{
        minHeight: "100vh",
        width: "100%",
        background: colors.pageBg,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: 16,
        boxSizing: "border-box",
      }}
    >
      <Panel style={{ width: "100%", maxWidth: 460 }}>
        <div style={{ marginBottom: 14 }}>
          <Wordmark size={20} />
        </div>
        {content}
      </Panel>
    </div>
  );
}

function Line({ children }: { children: ReactNode }) {
  return (
    <div style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textSecondary }} role="status">
      {children}
    </div>
  );
}

function BackLink() {
  return (
    <div style={{ marginTop: 12, fontFamily: fonts.body, fontSize: 12.5 }}>
      <Link href="/" style={{ color: colors.blue }}>
        Back to the QuickBooks screen
      </Link>
    </div>
  );
}
