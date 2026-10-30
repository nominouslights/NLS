"use client";

import { useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import type { RouteRecord, TripDirection } from "@/lib/api/trips";
import type { Vehicle } from "@/lib/api/fleet";
import type {
  BookeoImportSummary,
  BookeoProductMappingInput,
  BookeoUnitMappingInput,
  BookeoUnmappedProduct,
  BookeoUnmatchedUnit,
  ResidentStopRole,
} from "@/lib/api/bookeoImport";
import { productKey, suggestResidentRole } from "@/lib/bookeoImport";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import {
  CompactSelect,
  DIRECTION_OPTIONS,
  RESIDENT_ROLE_HELP,
  ROLE_OPTIONS,
  mono,
  muted,
  routeOptions,
  vehicleOptions,
} from "./shared";

// The two "fix it here" panels between the preview summary and the groups
// table. Each saves its mappings (PUT) and the parent re-runs the preview with
// the same File, so the dispatcher never re-picks the spreadsheet.

// ---------------------------------------------------------------------------
// Summary bar
// ---------------------------------------------------------------------------

function Stat({ label, value, tone }: { label: string; value: number; tone?: string }) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 2, minWidth: 64 }}>
      <span
        style={{
          fontFamily: fonts.semiCondensed,
          fontSize: 9.5,
          letterSpacing: ".12em",
          textTransform: "uppercase",
          color: colors.textLabel,
        }}
      >
        {label}
      </span>
      <span style={{ fontFamily: fonts.mono, fontSize: 15, fontWeight: 600, color: tone ?? colors.textPrimary }}>
        {value}
      </span>
    </div>
  );
}

export function SummaryBar({ summary }: { summary: BookeoImportSummary }) {
  const divider = <span style={{ width: 1, alignSelf: "stretch", background: colors.borderSubtle }} />;
  return (
    <Panel style={{ display: "flex", flexWrap: "wrap", gap: 16, alignItems: "flex-start", padding: "12px 16px" }}>
      <Stat label="Rows" value={summary.rows} />
      <Stat label="New" value={summary.new} />
      <Stat label="Changed" value={summary.changed} />
      <Stat label="Cancelled" value={summary.cancelled} />
      <Stat label="Unchanged" value={summary.unchanged} />
      <Stat label="Skipped" value={summary.skipped} />
      {divider}
      <Stat label="Trips to create" value={summary.tripsToCreate} />
      <Stat label="Trips to update" value={summary.tripsToUpdate} />
      <Stat label="Trips to cancel" value={summary.tripsToCancel} />
      {divider}
      <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
        <StatusChip
          kind={summary.blockedGroups > 0 ? "over" : "ontime"}
          label={`${summary.blockedGroups} blocked group${summary.blockedGroups === 1 ? "" : "s"}`}
        />
        <StatusChip
          kind={summary.warnings > 0 ? "soon" : "ontime"}
          label={`${summary.warnings} warning${summary.warnings === 1 ? "" : "s"}`}
        />
      </div>
    </Panel>
  );
}

// ---------------------------------------------------------------------------
// Unmapped products
// ---------------------------------------------------------------------------

interface ProductDraft {
  routeId: string;
  direction: "" | TripDirection;
  role: "" | ResidentStopRole;
}

function initialDraft(p: BookeoUnmappedProduct): ProductDraft {
  return { routeId: "", direction: "", role: suggestResidentRole(p.productName) ?? "" };
}

export function UnmappedProductsPanel({
  products,
  routes,
  busy,
  onSave,
}: {
  products: BookeoUnmappedProduct[];
  routes: RouteRecord[];
  busy: boolean;
  onSave: (mappings: BookeoProductMappingInput[]) => Promise<void>;
}) {
  const [drafts, setDrafts] = useState<Record<string, ProductDraft>>({});

  const draftOf = (p: BookeoUnmappedProduct) => drafts[productKey(p)] ?? initialDraft(p);
  const patch = (p: BookeoUnmappedProduct, next: Partial<ProductDraft>) =>
    setDrafts((d) => ({ ...d, [productKey(p)]: { ...draftOf(p), ...next } }));

  const complete = products.filter((p) => {
    const d = draftOf(p);
    return d.routeId !== "" && d.role !== "";
  });

  function save() {
    if (complete.length === 0 || busy) return;
    void onSave(
      complete.map((p) => {
        const d = draftOf(p);
        return {
          productCode: p.productCode,
          productName: p.productName,
          destination: p.destination,
          routeId: d.routeId,
          direction: d.direction === "" ? null : d.direction,
          residentStopRole: d.role as ResidentStopRole,
        };
      }),
    );
  }

  return (
    <Panel borderColor={statusMeta("over").bd}>
      <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 6 }}>
        <StatusChip
          kind="over"
          label={`${products.length} unmapped product${products.length === 1 ? "" : "s"} — blocks Confirm`}
        />
      </div>
      <div style={{ ...muted, marginBottom: 12 }}>
        Tell the import which route each Bookeo product runs on. {RESIDENT_ROLE_HELP}
      </div>
      <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
        {products.map((p) => {
          const d = draftOf(p);
          return (
            <div
              key={productKey(p)}
              data-testid="unmapped-product"
              style={{ borderTop: `1px solid ${colors.borderSubtle}`, paddingTop: 10 }}
            >
              <div style={{ display: "flex", flexWrap: "wrap", gap: 8, alignItems: "baseline", marginBottom: 8 }}>
                <span style={{ fontFamily: fonts.body, fontWeight: 600, fontSize: 13, color: colors.textPrimary }}>
                  {p.productName}
                </span>
                {p.destination && (
                  <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textSecondary }}>
                    · {p.destination}
                  </span>
                )}
                <span style={{ ...mono, color: colors.textDim }}>{p.productCode}</span>
                <span style={{ ...muted, marginLeft: "auto" }}>
                  {p.rowCount} booking{p.rowCount === 1 ? "" : "s"}
                </span>
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr 1fr", gap: 10 }}>
                <CompactSelect
                  label="Route"
                  value={d.routeId}
                  onChange={(v) => patch(p, { routeId: v })}
                  options={routeOptions(routes)}
                  disabled={busy}
                />
                <CompactSelect
                  label="Direction"
                  value={d.direction}
                  onChange={(v) => patch(p, { direction: v as ProductDraft["direction"] })}
                  options={DIRECTION_OPTIONS}
                  disabled={busy}
                />
                <CompactSelect
                  label="Resident stop is the"
                  value={d.role}
                  onChange={(v) => patch(p, { role: v as ProductDraft["role"] })}
                  options={ROLE_OPTIONS}
                  disabled={busy}
                />
              </div>
            </div>
          );
        })}
      </div>
      <div style={{ display: "flex", justifyContent: "flex-end", alignItems: "center", gap: 10, marginTop: 12 }}>
        {complete.length < products.length && (
          <span style={muted}>Pick a route and a resident-stop role for each product.</span>
        )}
        <ActionButton variant="primary" onClick={save} disabled={busy || complete.length === 0}>
          {busy
            ? "SAVING…"
            : complete.length === 0
              ? "SAVE MAPPINGS & RE-PREVIEW"
              : `SAVE ${complete.length} MAPPING${complete.length === 1 ? "" : "S"} & RE-PREVIEW`}
        </ActionButton>
      </div>
    </Panel>
  );
}

// ---------------------------------------------------------------------------
// Unmatched units
// ---------------------------------------------------------------------------

export function UnmatchedUnitsPanel({
  units,
  vehicles,
  busy,
  onSave,
  onSkip,
}: {
  units: BookeoUnmatchedUnit[];
  vehicles: Vehicle[];
  busy: boolean;
  onSave: (mappings: BookeoUnitMappingInput[]) => Promise<void>;
  onSkip: () => void;
}) {
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const complete = units.filter((u) => (drafts[u.unitText] ?? "") !== "");

  function save() {
    if (complete.length === 0 || busy) return;
    void onSave(complete.map((u) => ({ unitText: u.unitText, vehicleId: drafts[u.unitText] })));
  }

  return (
    <Panel borderColor={statusMeta("soon").bd}>
      <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 6 }}>
        <StatusChip
          kind="soon"
          label={`${units.length} Bookeo unit${units.length === 1 ? "" : "s"} not matched to a vehicle`}
        />
      </div>
      <div style={{ ...muted, marginBottom: 12 }}>
        Optional. Map a unit to a fleet vehicle and the import assigns it; skip, and those trips stay unassigned
        for you to assign later. The import matches on unit number only — a make/model such as “FORD TRANSIT
        150” is never matched automatically, so map it to the right vehicle here once and it is remembered.
      </div>
      <SectionLabel>Bookeo unit → vehicle</SectionLabel>
      <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
        {units.map((u) => (
          <div
            key={u.unitText}
            style={{ display: "grid", gridTemplateColumns: "1fr 2fr", gap: 10, alignItems: "end" }}
          >
            <div style={{ display: "flex", flexDirection: "column", gap: 2, paddingBottom: 8 }}>
              <span style={{ fontFamily: fonts.body, fontWeight: 600, fontSize: 13 }}>{u.unitText}</span>
              <span style={muted}>
                {u.rowCount} booking{u.rowCount === 1 ? "" : "s"}
              </span>
            </div>
            <CompactSelect
              label={`Vehicle for “${u.unitText}”`}
              value={drafts[u.unitText] ?? ""}
              onChange={(v) => setDrafts((d) => ({ ...d, [u.unitText]: v }))}
              options={vehicleOptions(vehicles)}
              disabled={busy}
            />
          </div>
        ))}
      </div>
      <div style={{ display: "flex", justifyContent: "flex-end", gap: 10, marginTop: 12 }}>
        <ActionButton onClick={onSkip} disabled={busy}>
          SKIP — LEAVE UNASSIGNED
        </ActionButton>
        <ActionButton variant="primary" onClick={save} disabled={busy || complete.length === 0}>
          {busy ? "SAVING…" : "SAVE & RE-PREVIEW"}
        </ActionButton>
      </div>
    </Panel>
  );
}
