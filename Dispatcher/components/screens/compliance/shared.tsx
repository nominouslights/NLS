"use client";

import { useEffect, useState, type CSSProperties, type ReactNode } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import type { DriverRecord } from "@/lib/api/drivers";
import type { ChipSpec } from "@/lib/hos/display";
import { fanOut } from "@/lib/hos/compliance";
import { StatusChip } from "@/components/ui/Chip";
import { Panel } from "@/components/ui/Panel";

// Pieces every Compliance tab shares: the chip-from-spec, the roster fan-out hook (see the
// KNOWN COST note in lib/hos/compliance.ts), and the driver link.

export function SpecChip({ spec, label }: { spec: ChipSpec; label?: string }) {
  return <StatusChip kind={spec.kind} glyph={spec.glyph} label={label ?? spec.label} />;
}

export interface FanOutState<R> {
  /** null while loading; one entry per driver whose call succeeded, in roster order. */
  rows: { driver: DriverRecord; value: R }[] | null;
  /** Drivers whose call failed, with the server's message verbatim. */
  failures: { driver: DriverRecord; error: string }[];
}

/**
 * Calls `fetchOne` for every roster driver (capped concurrency) and re-runs when `version`
 * bumps. `fetchOne` must be stable (module-level or memoised) — it is an effect dependency.
 */
export function useRosterFanOut<R>(
  roster: DriverRecord[],
  fetchOne: (driver: DriverRecord) => Promise<R>,
  version: number,
): FanOutState<R> {
  const [state, setState] = useState<{ roster: DriverRecord[]; version: number; fetchOne: unknown; result: FanOutState<R> } | null>(null);

  useEffect(() => {
    let active = true;
    fanOut(roster, async (d) => {
      try {
        return await fetchOne(d);
      } catch (e) {
        // Keep the server's sentence; a non-API failure gets a plain one.
        throw new Error(e instanceof ApiError ? e.message : "Request failed.");
      }
    }).then((results) => {
      if (!active) return;
      const rows: { driver: DriverRecord; value: R }[] = [];
      const failures: { driver: DriverRecord; error: string }[] = [];
      for (const r of results) {
        if (r.ok) rows.push({ driver: r.item, value: r.value });
        else failures.push({ driver: r.item, error: r.error });
      }
      setState({ roster, version, fetchOne, result: { rows, failures } });
    });
    return () => {
      active = false;
    };
  }, [roster, fetchOne, version]);

  const current = state && state.roster === roster && state.version === version && state.fetchOne === fetchOne;
  return current ? state.result : { rows: null, failures: [] };
}

/** One line per driver whose records could not be loaded — partial data is labelled, never silent. */
export function FanOutFailures({ failures, what }: { failures: { driver: DriverRecord; error: string }[]; what: string }) {
  if (failures.length === 0) return null;
  return (
    <Panel borderColor="rgba(213,94,0,.4)" style={{ marginBottom: 12 }}>
      <StatusChip kind="over" label={`${what} unavailable for ${failures.length} driver${failures.length === 1 ? "" : "s"} — the list below is incomplete`} />
      <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, marginTop: 8, lineHeight: 1.6 }}>
        {failures.map((f) => (
          <div key={f.driver.id}>
            {f.driver.name}: {f.error}
          </div>
        ))}
      </div>
    </Panel>
  );
}

export function LoadingLine({ children }: { children: ReactNode }) {
  return (
    <div role="status" style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 0" }}>
      {children}
    </div>
  );
}

export function EmptyLine({ children }: { children: ReactNode }) {
  return <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 0" }}>{children}</div>;
}

const linkBase: CSSProperties = {
  appearance: "none",
  background: "none",
  border: "none",
  padding: 0,
  cursor: "pointer",
  fontFamily: fonts.body,
  fontWeight: 600,
  fontSize: 12.5,
  color: colors.blue,
  textAlign: "left",
};

/** The driver's name as a link to Drivers → that driver → Hours of Service. */
export function DriverLink({ driver, onOpen }: { driver: { id: string; name: string }; onOpen: (driverId: string) => void }) {
  return (
    <button type="button" onClick={() => onOpen(driver.id)} style={linkBase} title={`Open ${driver.name} — Hours of Service`}>
      {driver.name}
    </button>
  );
}

/** A small uppercase text action (RESOLVE, LIFT, REJECT…) as a real button. */
export function RowAction({ children, onClick, label }: { children: ReactNode; onClick: () => void; label: string }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={label}
      style={{
        ...linkBase,
        fontFamily: fonts.semiCondensed,
        fontSize: 10.5,
        letterSpacing: ".08em",
        textTransform: "uppercase",
      }}
    >
      {children}
    </button>
  );
}

/** The column-head row style the registers share. */
export const headRow: CSSProperties = {
  display: "grid",
  gap: 10,
  padding: "0 12px 8px",
  fontFamily: fonts.semiCondensed,
  fontSize: 9.5,
  letterSpacing: ".12em",
  textTransform: "uppercase",
  color: colors.textFaint,
};

export const bodyRow: CSSProperties = {
  display: "grid",
  gap: 10,
  alignItems: "center",
  padding: "10px 12px",
  borderTop: `1px solid ${colors.borderSubtle}`,
  fontFamily: fonts.body,
  fontSize: 12.5,
  color: colors.textPrimary,
};

export const mono: CSSProperties = { fontFamily: fonts.mono, fontSize: 11.5, color: colors.textDim };

/** Filter pill (the Trips/PeriodNav treatment) as a toggle button. */
export function Pill({ active, onClick, children }: { active: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      aria-pressed={active}
      onClick={onClick}
      style={{
        appearance: "none",
        fontFamily: fonts.body,
        fontWeight: active ? 600 : 500,
        fontSize: 12,
        padding: "5px 12px",
        borderRadius: 7,
        background: active ? colors.cardBgActive : colors.cardBg,
        border: `1px solid ${active ? colors.borderActive : colors.border}`,
        color: active ? colors.headingBright : colors.textMuted,
        cursor: "pointer",
      }}
    >
      {children}
    </button>
  );
}
