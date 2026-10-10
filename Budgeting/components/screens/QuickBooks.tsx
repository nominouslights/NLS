"use client";

import { useCallback, useEffect, useState, type ReactNode } from "react";
import { colors, fonts } from "@/lib/theme";
import { ActionButton } from "@/components/ui/Button";
import { DetailRow, Panel, SectionLabel } from "@/components/ui/Panel";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { ErrorNotice } from "@/components/ErrorNotice";
import { EmptyNote, Screen } from "@/components/screens/shared";
import { ApiError } from "@/lib/api/transport";
import {
  authorizeQbo,
  disconnectQbo,
  getQboConnection,
  isLiveQboConnection,
  QBO_STATUS_DISPLAY,
  reconnectWarningDue,
  type QboConnection,
} from "@/lib/api/qbo";

// The tenant's QuickBooks Online connection — one company per tenant, read-only (the platform
// never writes to QuickBooks). Not period-scoped: it renders with or without an entered period.
//
// CONNECT / RECONNECT asks the API for Intuit's authorize URL and sends the browser there;
// Intuit comes back to app/qbo/callback, which finishes the connection and lands back here. The
// API is not public, so Intuit never talks to it directly. Tokens never reach this screen.
//
// Every refusal is shown verbatim — notably 503 Budgeting.Qbo.NotConfigured, whose message tells
// the planner who has to add the Intuit credentials. DISCONNECT is two-click, like every other
// destructive action in the console.
//
// The requests come in through an `api` prop (defaulting to the real client), as
// BudgetItemFormModal does, so the test injects vi.fn()s instead of stubbing the transport.

export interface QuickBooksApi {
  getConnection: () => Promise<QboConnection>;
  authorize: () => Promise<string>;
  disconnect: () => Promise<void>;
}

const DEFAULT_API: QuickBooksApi = {
  getConnection: getQboConnection,
  authorize: authorizeQbo,
  disconnect: disconnectQbo,
};

type Failure = { message: string; code: string };

function toFailure(e: unknown, fallback: string): Failure {
  return e instanceof ApiError
    ? { message: e.message, code: e.code }
    : { message: fallback, code: "Unknown" };
}

function formatDateTime(iso: string | null): string {
  if (!iso) return "—";
  return new Date(iso).toLocaleString("en-CA", {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString("en-CA", { year: "numeric", month: "short", day: "numeric" });
}

export default function QuickBooks({
  api = DEFAULT_API,
  navigate = (url: string) => window.location.assign(url),
  now,
}: {
  api?: QuickBooksApi;
  /** Where CONNECT sends the browser. Injected by the test; a full navigation in the app. */
  navigate?: (url: string) => void;
  /** The clock the reconnect warning reads. Injected by the test; mount time in the app. */
  now?: Date;
}) {
  // null = still loading.
  const [connection, setConnection] = useState<QboConnection | null>(null);
  const [loadError, setLoadError] = useState<Failure | null>(null);
  const [actionError, setActionError] = useState<Failure | null>(null);
  const [busy, setBusy] = useState<"connect" | "disconnect" | null>(null);
  const [confirmingDisconnect, setConfirmingDisconnect] = useState(false);
  const [clock] = useState<Date>(() => now ?? new Date());

  const applyLoaded = useCallback((c: QboConnection) => {
    setConnection(c);
    setLoadError(null);
  }, []);
  const applyLoadError = useCallback((e: unknown) => {
    setLoadError(toFailure(e, "Failed to load the QuickBooks connection."));
  }, []);

  const load = useCallback(() => {
    api.getConnection().then(applyLoaded, applyLoadError);
  }, [api, applyLoaded, applyLoadError]);

  useEffect(() => {
    let active = true;
    api.getConnection().then(
      (c) => {
        if (active) applyLoaded(c);
      },
      (e) => {
        if (active) applyLoadError(e);
      },
    );
    return () => {
      active = false;
    };
  }, [api, applyLoaded, applyLoadError]);

  function connect() {
    if (busy) return;
    setBusy("connect");
    setActionError(null);
    setConfirmingDisconnect(false);
    api.authorize().then(
      (url) => {
        // Leaves the page; busy stays set so a second click cannot start a second flow.
        navigate(url);
      },
      (e) => {
        setActionError(toFailure(e, "Could not start connecting to QuickBooks."));
        setBusy(null);
      },
    );
  }

  function disconnect() {
    if (busy) return;
    if (!confirmingDisconnect) {
      setConfirmingDisconnect(true);
      setActionError(null);
      return;
    }
    setBusy("disconnect");
    setActionError(null);
    api
      .disconnect()
      // GET reads the write table, so the refetch already shows Disconnected — no polling.
      .then(() => api.getConnection())
      .then(
        (c) => {
          applyLoaded(c);
          setConfirmingDisconnect(false);
          setBusy(null);
        },
        (e) => {
          setActionError(toFailure(e, "Could not disconnect QuickBooks."));
          setConfirmingDisconnect(false);
          setBusy(null);
        },
      );
  }

  let body: ReactNode;
  if (loadError) {
    body = (
      <>
        <ErrorNotice
          title="Couldn't load the QuickBooks connection"
          message={loadError.message}
          code={loadError.code}
        />
        <div style={{ marginTop: 10 }}>
          <ActionButton onClick={load}>RETRY</ActionButton>
        </div>
      </>
    );
  } else if (connection === null) {
    body = <EmptyNote>Loading the QuickBooks connection…</EmptyNote>;
  } else {
    body = (
      <ConnectionCard
        connection={connection}
        clock={clock}
        busy={busy}
        confirmingDisconnect={confirmingDisconnect}
        actionError={actionError}
        onConnect={connect}
        onDisconnect={disconnect}
        onCancelDisconnect={() => setConfirmingDisconnect(false)}
      />
    );
  }

  return (
    <Screen eyebrow="Connections" title="QuickBooks Online">
      {body}
      <Panel style={{ marginTop: 12 }}>
        <SectionLabel>Expense sync</SectionLabel>
        <Prose>
          Importing expenses from QuickBooks — Purchases, Bills and Vendor Credits — arrives in the
          next release, along with assigning each line to a budget item. Connecting now only
          proves the link works; nothing is read from your books yet. The platform never writes to
          QuickBooks.
        </Prose>
      </Panel>
    </Screen>
  );
}

function ConnectionCard({
  connection: c,
  clock,
  busy,
  confirmingDisconnect,
  actionError,
  onConnect,
  onDisconnect,
  onCancelDisconnect,
}: {
  connection: QboConnection;
  clock: Date;
  busy: "connect" | "disconnect" | null;
  confirmingDisconnect: boolean;
  actionError: Failure | null;
  onConnect: () => void;
  onDisconnect: () => void;
  onCancelDisconnect: () => void;
}) {
  const display = QBO_STATUS_DISPLAY[c.status];
  const live = isLiveQboConnection(c.status);
  const warn = live && reconnectWarningDue(c.refreshTokenExpiresAtUtc, clock);
  const connectedBy = c.connectedByName?.trim() || c.connectedByEmail || null;

  return (
    <Panel>
      <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12 }}>
        <SectionLabel>Connection</SectionLabel>
        <div style={{ marginBottom: 11 }}>
          <StatusChip kind={display.kind} glyph={display.glyph} label={display.label} />
        </div>
      </div>

      {c.status === "NotConnected" ? (
        <Prose>
          No QuickBooks company is connected. Connecting signs in to QuickBooks Online with
          read-only access to one company, whose home currency must be Canadian dollars.
        </Prose>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
          <DetailRow label="Company" value={c.companyName ?? "—"} />
          <DetailRow label="Environment" value={<EnvironmentTag environment={c.environment} />} />
          <DetailRow label="Connected by" value={connectedBy ?? "—"} />
          <DetailRow label="Connected on" value={formatDateTime(c.connectedAtUtc)} />
          {c.realmId && <DetailRow label="Company id" value={<MonoTag>{c.realmId}</MonoTag>} />}
        </div>
      )}

      {c.status === "NeedsReconnect" && (
        <Notice>
          QuickBooks stopped accepting this connection. Reconnect to the same company to restore
          it.
          {c.lastErrorCode && (
            <span style={{ fontFamily: fonts.mono, fontSize: 10.5, color: colors.textDim }}>
              {" "}
              {c.lastErrorCode}
            </span>
          )}
        </Notice>
      )}

      {warn && c.refreshTokenExpiresAtUtc && (
        <div style={{ display: "flex", alignItems: "center", gap: 10, marginTop: 12, flexWrap: "wrap" }}>
          <StatusChip
            kind="soon"
            glyph="!"
            label={`Reconnect by ${formatDate(c.refreshTokenExpiresAtUtc)}`}
          />
          <Prose>
            QuickBooks access lapses then unless the connection is used or renewed — reconnect
            before that date.
          </Prose>
        </div>
      )}

      {c.status === "Disconnected" && (
        <Notice>
          Disconnected. This workspace can only be connected to {c.companyName ?? "the same company"}{" "}
          again.
        </Notice>
      )}

      <div style={{ display: "flex", gap: 8, marginTop: 14, flexWrap: "wrap", alignItems: "center" }}>
        <ActionButton
          variant={live ? "secondary" : "primary"}
          onClick={onConnect}
          disabled={busy !== null}
        >
          {busy === "connect" ? "OPENING QUICKBOOKS…" : live ? "RECONNECT" : "CONNECT"}
        </ActionButton>
        {live && (
          <ActionButton variant="destructive" onClick={onDisconnect} disabled={busy !== null}>
            {busy === "disconnect"
              ? "DISCONNECTING…"
              : confirmingDisconnect
                ? "CONFIRM DISCONNECT"
                : "DISCONNECT"}
          </ActionButton>
        )}
        {live && confirmingDisconnect && busy === null && (
          <ActionButton onClick={onCancelDisconnect}>CANCEL</ActionButton>
        )}
      </div>

      {live && confirmingDisconnect && (
        <div style={{ marginTop: 8 }}>
          <Prose>
            Disconnecting revokes this console&rsquo;s access to {c.companyName ?? "the company"} and
            deletes the stored sign-in. Click CONFIRM DISCONNECT to proceed.
          </Prose>
        </div>
      )}

      {actionError && (
        <div style={{ marginTop: 12 }}>
          <ErrorNotice
            title="QuickBooks refused the request"
            message={actionError.message}
            code={actionError.code}
          />
        </div>
      )}
    </Panel>
  );
}

function EnvironmentTag({ environment }: { environment: QboConnection["environment"] }) {
  if (environment === "Sandbox") {
    return (
      <span style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
        <MonoTag color={colors.amberText}>SANDBOX</MonoTag>
        <span>Test company — not real books</span>
      </span>
    );
  }
  if (environment === "Production") return <MonoTag>PRODUCTION</MonoTag>;
  return <>—</>;
}

function Prose({ children }: { children: ReactNode }) {
  return (
    <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary, lineHeight: 1.65 }}>
      {children}
    </div>
  );
}

function Notice({ children }: { children: ReactNode }) {
  return (
    <div
      style={{
        marginTop: 12,
        fontFamily: fonts.body,
        fontSize: 12,
        color: colors.textSecondary,
        lineHeight: 1.6,
        padding: "8px 11px",
        borderRadius: 8,
        background: colors.inputBg,
        border: `1px solid ${colors.borderSubtle}`,
      }}
    >
      {children}
    </div>
  );
}
