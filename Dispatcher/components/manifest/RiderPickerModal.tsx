"use client";

import { useEffect, useMemo, useState } from "react";
import { colors, fonts, statusMeta, svcMeta } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import { SERVICE_TYPE_LABELS, svcForServiceType } from "@/lib/api/clients";
import { shortDateLabel, type TripServiceType } from "@/lib/api/trips";
import {
  RIDER_GROUP_ORDER,
  groupRiders,
  listRiders,
  type RiderRecord,
  type RiderServiceType,
} from "@/lib/api/riders";
import { emptyPax, passengerCapFor, type PaxRow, type StopOption } from "@/components/manifest/manifestRows";
import { ModalShell } from "@/components/ui/ModalShell";
import { ActionButton } from "@/components/ui/Button";

// "Add from riders" — auto-populate passenger rows from the rider directory
// (which builds itself from previously saved manifests). Follows the CSV
// import's preview → apply shape: pick names in a modal, then the rows are
// merged onto the manifest by the caller's applyImport. Riders are grouped
// under the same trip-type separator headers as the Riders screen, with the
// trip's own service type first and expanded. The row cap tracks the assigned
// unit's seat capacity; over-selection is blocked with an icon + text notice,
// never colour alone.

/** The rider directory stores one contact string; manifest rows split
 *  email/phone — route it by shape. */
function contactFields(contact: string | null): { email: string; phone: string } {
  const c = (contact ?? "").trim();
  if (!c) return { email: "", phone: "" };
  return c.includes("@") ? { email: c, phone: "" } : { email: "", phone: c };
}

/** Trip service type → the picker's default rider group (Cargo/Grocery trips
 *  have no rider group of their own — fall back to the first group in order). */
function defaultGroupFor(serviceType: TripServiceType | null): RiderServiceType | null {
  return serviceType && (RIDER_GROUP_ORDER as string[]).includes(serviceType)
    ? (serviceType as RiderServiceType)
    : null;
}

export default function RiderPickerModal({
  stops,
  capacity,
  existingCount,
  defaultServiceType,
  onApply,
  onClose,
}: {
  /** The trip's route stops — only used to warn when there are none yet
   *  (pickup / drop-off are always left for the dispatcher to set). */
  stops: StopOption[];
  /** Assigned unit's seat capacity (null when unknown → falls back to 8). */
  capacity: number | null;
  /** Passenger rows already carrying content (name/email/phone). */
  existingCount: number;
  /** The trip's service type — its rider group lists first, expanded. */
  defaultServiceType: TripServiceType | null;
  onApply: (rows: PaxRow[]) => void;
  onClose: () => void;
}) {
  const [riders, setRiders] = useState<RiderRecord[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState<Set<string>>(new Set());

  const defaultGroup = defaultGroupFor(defaultServiceType);
  const [expanded, setExpanded] = useState<Set<RiderServiceType>>(
    () => new Set(defaultGroup ? [defaultGroup] : [RIDER_GROUP_ORDER[0]]),
  );

  useEffect(() => {
    let active = true;
    listRiders().then(
      (rows) => {
        if (active) setRiders(rows);
      },
      (e) => {
        if (active) setLoadError(e instanceof ApiError ? e.message : "Failed to load the rider directory.");
      },
    );
    return () => {
      active = false;
    };
  }, []);

  const cap = passengerCapFor(capacity);
  const remaining = Math.max(0, cap - existingCount);
  const atCap = selected.size >= remaining;

  const query = search.trim().toLowerCase();
  const filtered = useMemo(() => {
    if (!riders) return [];
    if (!query) return riders;
    return riders.filter(
      (r) => r.name.toLowerCase().includes(query) || (r.contact ?? "").toLowerCase().includes(query),
    );
  }, [riders, query]);

  // Groups in separator order, the trip's own group first.
  const groups = useMemo(() => {
    const gs = groupRiders(filtered);
    if (!defaultGroup) return gs;
    return [...gs.filter((g) => g.serviceType === defaultGroup), ...gs.filter((g) => g.serviceType !== defaultGroup)];
  }, [filtered, defaultGroup]);

  function toggle(id: string) {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else if (next.size < remaining) next.add(id);
      return next;
    });
  }

  function toggleGroup(serviceType: RiderServiceType) {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(serviceType)) next.delete(serviceType);
      else next.add(serviceType);
      return next;
    });
  }

  function apply() {
    if (!riders || selected.size === 0) return;
    const rows: PaxRow[] = riders
      .filter((r) => selected.has(r.id))
      .map((r) => ({ ...emptyPax(), name: r.name, ...contactFields(r.contact) }));
    onApply(rows);
    onClose();
  }

  return (
    <ModalShell
      eyebrow="Manifest · Add passengers from the rider directory"
      title="Add from riders"
      onClose={onClose}
      error={loadError}
      maxWidth={760}
      footer={
        <>
          <span style={{ marginRight: "auto", fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
            {selected.size} selected · {Math.max(0, remaining - selected.size)} seat
            {remaining - selected.size === 1 ? "" : "s"} left
          </span>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={apply} disabled={selected.size === 0}>
            {selected.size > 0 ? `ADD ${selected.size} PASSENGER${selected.size === 1 ? "" : "S"}` : "ADD"}
          </ActionButton>
        </>
      }
    >
      {/* capacity / stops notices — icon + label + colour, never colour alone */}
      <div style={{ display: "flex", flexDirection: "column", gap: 7, marginBottom: 12 }}>
        {remaining === 0 && (
          <Notice
            kind="over"
            text={`No seats left — the unit seats ${cap}${
              existingCount > 0
                ? ` and ${existingCount} ${existingCount === 1 ? "is" : "are"} already on the manifest`
                : ""
            }. Put more passengers on a second trip.`}
          />
        )}
        {remaining > 0 && atCap && (
          <Notice
            kind="soon"
            text={`Seat cap reached — the unit seats ${cap}${
              existingCount > 0 ? ` and ${existingCount} ${existingCount === 1 ? "is" : "are"} already on the manifest` : ""
            }. Deselect someone to swap, or put the rest on a second trip.`}
          />
        )}
        {stops.length === 0 && (
          <Notice kind="info" text="No route stops yet — set each passenger's pickup / drop-off after adding." />
        )}
      </div>

      <input
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder="Search riders by name or contact…"
        style={{
          width: "100%",
          boxSizing: "border-box",
          fontFamily: fonts.body,
          fontSize: 13,
          padding: "9px 12px",
          borderRadius: 8,
          border: `1px solid ${colors.borderStrong}`,
          background: colors.inputBg,
          color: colors.textPrimary,
          outline: "none",
          marginBottom: 14,
        }}
      />

      {riders === null && !loadError && (
        <Notice kind="info" text="Loading the rider directory…" />
      )}
      {riders !== null && riders.length === 0 && (
        <Notice kind="info" text="Riders appear here automatically a few moments after a trip manifest is saved." />
      )}
      {riders !== null && riders.length > 0 && groups.length === 0 && (
        <Notice kind="info" text={`No riders match “${search.trim()}”.`} />
      )}

      {groups.map((g) => {
        const meta = svcMeta(svcForServiceType(g.serviceType));
        // Searching shows every match; otherwise groups collapse/expand.
        const open = query.length > 0 || expanded.has(g.serviceType);
        return (
          <div key={g.serviceType} style={{ marginBottom: 12 }}>
            {/* trip-type separator header */}
            <div
              onClick={() => toggleGroup(g.serviceType)}
              style={{
                display: "flex",
                alignItems: "center",
                gap: 8,
                padding: "5px 2px 7px",
                borderBottom: `2px solid ${meta.accent}`,
                marginBottom: 7,
                cursor: "pointer",
                userSelect: "none",
              }}
            >
              <span style={{ fontSize: 11, lineHeight: 1, color: meta.accent }}>{meta.glyph}</span>
              <span
                style={{
                  fontFamily: fonts.semiCondensed,
                  fontSize: 10.5,
                  letterSpacing: ".14em",
                  textTransform: "uppercase",
                  color: colors.textLabel,
                  fontWeight: 600,
                }}
              >
                {SERVICE_TYPE_LABELS[g.serviceType]}
              </span>
              {g.serviceType === defaultGroup && (
                <span style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim }}>· this trip</span>
              )}
              <span style={{ marginLeft: "auto", fontFamily: fonts.mono, fontSize: 10.5, color: colors.textDim }}>
                {g.riders.length}
              </span>
              <span style={{ fontSize: 10, color: colors.textDim }}>{open ? "▾" : "▸"}</span>
            </div>

            {open &&
              g.riders.map((r) => {
                const checked = selected.has(r.id);
                const blocked = !checked && atCap;
                return (
                  <label
                    key={r.id}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: 10,
                      padding: "8px 10px",
                      marginBottom: 4,
                      borderRadius: 8,
                      border: `1px solid ${checked ? colors.borderActive : colors.borderSubtle}`,
                      background: checked ? colors.cardBgActive : colors.cardBg,
                      cursor: blocked ? "not-allowed" : "pointer",
                      opacity: blocked ? 0.55 : 1,
                    }}
                  >
                    <input
                      type="checkbox"
                      checked={checked}
                      disabled={blocked}
                      onChange={() => toggle(r.id)}
                      style={{ flex: "none", width: 15, height: 15, accentColor: colors.blue }}
                    />
                    <span
                      style={{
                        fontFamily: fonts.body,
                        fontSize: 13,
                        fontWeight: 600,
                        color: colors.textPrimary,
                        whiteSpace: "nowrap",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                      }}
                    >
                      {r.name}
                    </span>
                    {r.contact && (
                      <span
                        style={{
                          fontFamily: fonts.mono,
                          fontSize: 11,
                          color: colors.textDim,
                          whiteSpace: "nowrap",
                          overflow: "hidden",
                          textOverflow: "ellipsis",
                        }}
                      >
                        {r.contact}
                      </span>
                    )}
                    <span
                      style={{
                        marginLeft: "auto",
                        fontFamily: fonts.body,
                        fontSize: 11,
                        color: colors.textDim,
                        whiteSpace: "nowrap",
                      }}
                    >
                      {r.lastTripDate
                        ? `last ${shortDateLabel(r.lastTripDate)}${r.lastTripNumber ? ` · ${r.lastTripNumber}` : ""}`
                        : "no trips yet"}
                    </span>
                    {r.serviceType === "ContractCrew" && r.rotationDays != null && (
                      <span
                        style={{
                          flex: "none",
                          fontFamily: fonts.mono,
                          fontSize: 10,
                          fontWeight: 700,
                          padding: "2px 6px",
                          borderRadius: 5,
                          border: `1px solid ${colors.borderStrong}`,
                          color: colors.textSecondary,
                          whiteSpace: "nowrap",
                        }}
                      >
                        ⟳ {r.rotationDays}d
                      </span>
                    )}
                  </label>
                );
              })}
          </div>
        );
      })}
    </ModalShell>
  );
}

function Notice({ kind, text }: { kind: "over" | "soon" | "info"; text: string }) {
  const meta = statusMeta(kind);
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 9,
        padding: "9px 12px",
        background: meta.bg,
        border: `1px solid ${meta.bd}`,
        borderRadius: 9,
      }}
    >
      <span style={{ color: meta.t, fontSize: 12, fontWeight: 800, flex: "none" }}>{meta.g}</span>
      <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: meta.t, fontWeight: 600 }}>{text}</span>
    </div>
  );
}
