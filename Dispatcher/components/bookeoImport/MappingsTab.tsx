"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import type { RouteRecord, TripDirection } from "@/lib/api/trips";
import type { Vehicle } from "@/lib/api/fleet";
import {
  bookeoErrorMessage,
  deleteBookeoProductMapping,
  deleteBookeoUnitMapping,
  listBookeoProductMappings,
  listBookeoUnitMappings,
  saveBookeoProductMappings,
  saveBookeoUnitMappings,
  type BookeoProductMapping,
  type BookeoUnitMapping,
  type ResidentStopRole,
} from "@/lib/api/bookeoImport";
import { ActionButton } from "@/components/ui/Button";
import { ModalError } from "@/components/ui/ModalShell";
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

// Lists, edits and deletes the saved Bookeo → platform mappings. Each row
// saves on its own (PUT upserts by key, so sending one row edits just that one).

export function MappingsTab({ routes, vehicles }: { routes: RouteRecord[]; vehicles: Vehicle[] }) {
  const [products, setProducts] = useState<BookeoProductMapping[] | null>(null);
  const [units, setUnits] = useState<BookeoUnitMapping[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    Promise.all([listBookeoProductMappings(), listBookeoUnitMappings()]).then(
      ([p, u]) => {
        if (!active) return;
        setProducts(p);
        setUnits(u);
      },
      (e) => {
        if (active) setError(bookeoErrorMessage(e));
      },
    );
    return () => {
      active = false;
    };
  }, []);

  async function run<T>(op: () => Promise<T>): Promise<T | undefined> {
    setError(null);
    try {
      return await op();
    } catch (e) {
      setError(bookeoErrorMessage(e));
      return undefined;
    }
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 18 }}>
      {error && <ModalError message={error} />}

      <div>
        <SectionLabel>Product mappings</SectionLabel>
        <div style={{ ...muted, marginBottom: 10 }}>{RESIDENT_ROLE_HELP}</div>
        {products === null ? (
          <div style={muted}>{error ? "—" : "Loading…"}</div>
        ) : products.length === 0 ? (
          <div style={muted}>No product mappings yet — they are created from the Import tab.</div>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
            {products.map((m) => (
              <ProductMappingRow
                key={m.id}
                mapping={m}
                routes={routes}
                onSave={async (next) => {
                  const list = await run(() =>
                    saveBookeoProductMappings([
                      {
                        productCode: m.productCode,
                        productName: m.productName,
                        destination: m.destination,
                        routeId: next.routeId,
                        direction: next.direction,
                        residentStopRole: next.residentStopRole,
                      },
                    ]),
                  );
                  if (list) setProducts(list);
                }}
                onDelete={async () => {
                  const ok = await run(() => deleteBookeoProductMapping(m.id).then(() => true));
                  if (ok) setProducts((prev) => (prev ?? []).filter((x) => x.id !== m.id));
                }}
              />
            ))}
          </div>
        )}
      </div>

      <div>
        <SectionLabel>Unit mappings</SectionLabel>
        {units === null ? (
          <div style={muted}>{error ? "—" : "Loading…"}</div>
        ) : units.length === 0 ? (
          <div style={muted}>No unit mappings yet — they are created from the Import tab.</div>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
            {units.map((m) => (
              <UnitMappingRow
                key={m.id}
                mapping={m}
                vehicles={vehicles}
                onSave={async (vehicleId) => {
                  const list = await run(() => saveBookeoUnitMappings([{ unitText: m.unitText, vehicleId }]));
                  if (list) setUnits(list);
                }}
                onDelete={async () => {
                  const ok = await run(() => deleteBookeoUnitMapping(m.id).then(() => true));
                  if (ok) setUnits((prev) => (prev ?? []).filter((x) => x.id !== m.id));
                }}
              />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function RowActions({
  dirty,
  busy,
  onSave,
  onDelete,
}: {
  dirty: boolean;
  busy: boolean;
  onSave: () => void;
  onDelete: () => void;
}) {
  const [confirming, setConfirming] = useState(false);
  return (
    <div style={{ display: "flex", gap: 8, alignItems: "flex-end" }}>
      <ActionButton variant="primary" onClick={onSave} disabled={!dirty || busy}>
        SAVE
      </ActionButton>
      {confirming ? (
        <>
          <ActionButton variant="destructive" onClick={onDelete} disabled={busy}>
            CONFIRM DELETE
          </ActionButton>
          <ActionButton onClick={() => setConfirming(false)} disabled={busy}>
            KEEP
          </ActionButton>
        </>
      ) : (
        <ActionButton variant="destructive" onClick={() => setConfirming(true)} disabled={busy}>
          DELETE
        </ActionButton>
      )}
    </div>
  );
}

function ProductMappingRow({
  mapping: m,
  routes,
  onSave,
  onDelete,
}: {
  mapping: BookeoProductMapping;
  routes: RouteRecord[];
  onSave: (next: { routeId: string; direction: TripDirection | null; residentStopRole: ResidentStopRole }) => Promise<void>;
  onDelete: () => Promise<void>;
}) {
  const [routeId, setRouteId] = useState(m.routeId);
  const [direction, setDirection] = useState<"" | TripDirection>(m.direction ?? "");
  const [role, setRole] = useState<ResidentStopRole>(m.residentStopRole);
  const [busy, setBusy] = useState(false);
  const dirty = routeId !== m.routeId || (direction || null) !== m.direction || role !== m.residentStopRole;

  // A route that has since been deactivated stays selectable on its own row,
  // labelled by the name the mapping carries.
  const options = routeOptions(routes, m.routeId);
  if (!options.some((o) => o.value === m.routeId)) options.push({ value: m.routeId, label: m.routeName });

  async function wrap(op: () => Promise<void>) {
    setBusy(true);
    try {
      await op();
    } finally {
      setBusy(false);
    }
  }

  return (
    <Panel style={{ padding: "12px 14px" }}>
      <div style={{ display: "flex", flexWrap: "wrap", gap: 8, alignItems: "baseline", marginBottom: 8 }}>
        <span style={{ fontFamily: fonts.body, fontWeight: 600, fontSize: 13, color: colors.textPrimary }}>
          {m.productName}
        </span>
        <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textSecondary }}>
          {m.destination ? `· ${m.destination}` : "· any destination"}
        </span>
        <span style={{ ...mono, color: colors.textDim }}>{m.productCode}</span>
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr 1fr auto", gap: 10, alignItems: "end" }}>
        <CompactSelect label="Route" value={routeId} onChange={setRouteId} options={options} disabled={busy} />
        <CompactSelect
          label="Direction"
          value={direction}
          onChange={(v) => setDirection(v as "" | TripDirection)}
          options={DIRECTION_OPTIONS}
          disabled={busy}
        />
        <CompactSelect
          label="Resident stop is the"
          value={role}
          onChange={(v) => setRole(v as ResidentStopRole)}
          options={ROLE_OPTIONS.filter((o) => o.value !== "")}
          disabled={busy}
        />
        <RowActions
          dirty={dirty && routeId !== ""}
          busy={busy}
          onSave={() =>
            void wrap(() => onSave({ routeId, direction: direction === "" ? null : direction, residentStopRole: role }))
          }
          onDelete={() => void wrap(onDelete)}
        />
      </div>
    </Panel>
  );
}

function UnitMappingRow({
  mapping: m,
  vehicles,
  onSave,
  onDelete,
}: {
  mapping: BookeoUnitMapping;
  vehicles: Vehicle[];
  onSave: (vehicleId: string) => Promise<void>;
  onDelete: () => Promise<void>;
}) {
  const [vehicleId, setVehicleId] = useState(m.vehicleId);
  const [busy, setBusy] = useState(false);
  const options = vehicleOptions(vehicles, m.vehicleId);
  if (!options.some((o) => o.value === m.vehicleId)) options.push({ value: m.vehicleId, label: m.vehicleUnit });

  async function wrap(op: () => Promise<void>) {
    setBusy(true);
    try {
      await op();
    } finally {
      setBusy(false);
    }
  }

  return (
    <Panel style={{ padding: "12px 14px" }}>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 2fr auto", gap: 10, alignItems: "end" }}>
        <div style={{ display: "flex", flexDirection: "column", gap: 2, paddingBottom: 8 }}>
          <span style={{ fontFamily: fonts.body, fontWeight: 600, fontSize: 13 }}>{m.unitText}</span>
          <span style={muted}>Bookeo unit</span>
        </div>
        <CompactSelect label="Vehicle" value={vehicleId} onChange={setVehicleId} options={options} disabled={busy} />
        <RowActions
          dirty={vehicleId !== m.vehicleId && vehicleId !== ""}
          busy={busy}
          onSave={() => void wrap(() => onSave(vehicleId))}
          onDelete={() => void wrap(onDelete)}
        />
      </div>
    </Panel>
  );
}
