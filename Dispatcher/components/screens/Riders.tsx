"use client";

import { useEffect, useState } from "react";
import { colors, fonts, rowSurface, statusMeta, svcMeta } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import { SERVICE_TYPE_LABELS, svcForServiceType } from "@/lib/api/clients";
import { shortDateLabel, todayIso } from "@/lib/api/trips";
import { formatUtcDate } from "@/lib/api/format";
import {
  ROTATION_DUE_LABELS,
  ROTATION_OPTIONS,
  groupRiders,
  listRiders,
  nextExpectedFrom,
  rotationDueKind,
  setRiderRotation,
  type RiderRecord,
} from "@/lib/api/riders";
import { DetailRow, PageHeader, Panel, SectionLabel } from "@/components/ui/Panel";
import { ServiceChip, StatusChip } from "@/components/ui/Chip";

// Rider directory — populated automatically from saved trip manifests (Trips
// module Rider aggregate; eventually consistent, so new names land a few
// moments after a manifest is saved). Grouped by trip type with separator
// headers; ContractCrew riders carry a settable 20/10/5-day crew rotation and
// a "next expected travel" due state. Due state renders as StatusChip only —
// colour + icon + label, never colour alone.

/** "⟳ 20d" — the rotation length badge on ContractCrew rows. */
function RotationBadge({ days }: { days: number }) {
  return (
    <span
      style={{
        fontFamily: fonts.mono,
        fontSize: 10.5,
        fontWeight: 700,
        padding: "2px 7px",
        borderRadius: 5,
        border: `1px solid ${colors.borderStrong}`,
        color: colors.textSecondary,
        whiteSpace: "nowrap",
      }}
    >
      ⟳ {days}d
    </span>
  );
}

function lastTripLine(r: RiderRecord): string {
  if (!r.lastTripDate) return "No trips recorded yet";
  return `Last ${shortDateLabel(r.lastTripDate)}${r.lastTripNumber ? ` · ${r.lastTripNumber}` : ""}`;
}

export default function Riders() {
  const [riders, setRiders] = useState<RiderRecord[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [selId, setSelId] = useState<string | null>(null);

  const [rotationBusy, setRotationBusy] = useState(false);
  const [rotationError, setRotationError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    listRiders().then(
      (rows) => {
        if (!active) return;
        setRiders(rows);
        setSelId((cur) => cur ?? rows[0]?.id ?? null);
      },
      (e) => {
        if (active) setLoadError(e instanceof ApiError ? e.message : "Failed to load the rider directory.");
      },
    );
    return () => {
      active = false;
    };
  }, []);

  const today = todayIso();
  const groups = riders ? groupRiders(riders) : [];
  const selected = riders?.find((r) => r.id === selId) ?? null;

  async function applyRotation(rider: RiderRecord, days: number | null) {
    if (rotationBusy || rider.rotationDays === days) return;
    setRotationBusy(true);
    setRotationError(null);
    try {
      await setRiderRotation(rider.id, days);
      setRiders(
        (prev) =>
          prev?.map((r) =>
            r.id === rider.id
              ? { ...r, rotationDays: days, nextExpectedTravelDate: nextExpectedFrom(r.lastTripDate, days) }
              : r,
          ) ?? prev,
      );
    } catch (e) {
      setRotationError(e instanceof ApiError ? e.message : "Failed to update the rotation — please try again.");
    } finally {
      setRotationBusy(false);
    }
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow="Business · Rider directory by trip type" title="Riders" />
      </div>
      <div
        style={{
          flex: 1,
          minHeight: 0,
          display: "grid",
          gridTemplateColumns: "38% 1fr",
          borderTop: `1px solid ${colors.border}`,
        }}
      >
        {/* left — grouped list with trip-type separators */}
        <div style={{ minHeight: 0, overflowY: "auto", padding: "16px 18px", borderRight: `1px solid ${colors.border}` }}>
          {loadError ? (
            <StateNote kind="over" text={loadError} />
          ) : riders === null ? (
            <StateNote kind="info" text="Loading the rider directory…" />
          ) : riders.length === 0 ? (
            <StateNote
              kind="info"
              text="Riders appear here automatically a few moments after a trip manifest is saved."
            />
          ) : (
            groups.map((g) => {
              const meta = svcMeta(svcForServiceType(g.serviceType));
              return (
                <div key={g.serviceType} style={{ marginBottom: 14 }}>
                  {/* trip-type separator header */}
                  <div
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: 8,
                      padding: "4px 2px 7px",
                      borderBottom: `2px solid ${meta.accent}`,
                      marginBottom: 7,
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
                    <span style={{ marginLeft: "auto", fontFamily: fonts.mono, fontSize: 10.5, color: colors.textDim }}>
                      {g.riders.length}
                    </span>
                  </div>

                  {g.riders.map((row) => {
                    const active = row.id === selId;
                    const due = rotationDueKind(row, today);
                    return (
                      <div
                        key={row.id}
                        onClick={() => {
                          setSelId(row.id);
                          setRotationError(null);
                        }}
                        style={{
                          display: "flex",
                          gap: 10,
                          alignItems: "center",
                          padding: "11px 13px",
                          marginBottom: 5,
                          ...rowSurface(active, meta.accent),
                        }}
                      >
                        <div style={{ minWidth: 0, flex: 1 }}>
                          <div
                            style={{
                              fontFamily: fonts.body,
                              fontSize: 13.5,
                              fontWeight: 600,
                              color: colors.textPrimary,
                              whiteSpace: "nowrap",
                              overflow: "hidden",
                              textOverflow: "ellipsis",
                            }}
                          >
                            {row.name}
                          </div>
                          <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
                            {lastTripLine(row)}
                          </div>
                        </div>
                        {row.serviceType === "ContractCrew" && row.rotationDays != null && (
                          <RotationBadge days={row.rotationDays} />
                        )}
                        {due && <StatusChip kind={due} label={ROTATION_DUE_LABELS[due]} />}
                      </div>
                    );
                  })}
                </div>
              );
            })
          )}
        </div>

        {/* right — rider detail */}
        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: colors.detailBg }}>
          {selected ? (
            <div className="detailfade" key={selected.id}>
              <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 4 }}>
                <h2
                  style={{
                    fontFamily: fonts.condensed,
                    fontWeight: 700,
                    fontSize: 28,
                    lineHeight: 1,
                    color: colors.headingBright,
                    margin: 0,
                  }}
                >
                  {selected.name}
                </h2>
                <ServiceChip
                  svc={svcForServiceType(selected.serviceType)}
                  label={SERVICE_TYPE_LABELS[selected.serviceType]}
                />
              </div>
              <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textMuted, marginBottom: 16 }}>
                {selected.contact || "No contact on file"} · {lastTripLine(selected)}
              </div>

              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 12 }}>
                <Panel>
                  <SectionLabel>Travel</SectionLabel>
                  <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                    <DetailRow
                      label="Last trip"
                      value={selected.lastTripDate ? shortDateLabel(selected.lastTripDate) : "—"}
                    />
                    <DetailRow label="Trip number" value={selected.lastTripNumber ?? "—"} />
                    <DetailRow label="Trips recorded" value={selected.tripCount} />
                    <DetailRow label="First seen" value={formatUtcDate(selected.createdAtUtc)} />
                  </div>
                </Panel>

                {selected.serviceType === "ContractCrew" ? (
                  <Panel>
                    <SectionLabel>Crew rotation</SectionLabel>
                    <div style={{ display: "flex", gap: 7, flexWrap: "wrap", marginBottom: 12 }}>
                      {ROTATION_OPTIONS.map((days) => {
                        const active = selected.rotationDays === days;
                        return (
                          <span
                            key={days}
                            onClick={() => void applyRotation(selected, days)}
                            style={{
                              fontFamily: fonts.body,
                              fontSize: 12,
                              fontWeight: 600,
                              padding: "5px 12px",
                              borderRadius: 8,
                              cursor: rotationBusy ? "wait" : "pointer",
                              userSelect: "none",
                              border: `1px solid ${active ? colors.blue : colors.borderStrong}`,
                              background: active ? colors.blue : "transparent",
                              color: active ? "#FFFFFF" : colors.textSecondary,
                              opacity: rotationBusy ? 0.6 : 1,
                            }}
                          >
                            ⟳ {days} days
                          </span>
                        );
                      })}
                      <span
                        onClick={() => void applyRotation(selected, null)}
                        style={{
                          fontFamily: fonts.body,
                          fontSize: 12,
                          fontWeight: 600,
                          padding: "5px 12px",
                          borderRadius: 8,
                          cursor: rotationBusy ? "wait" : "pointer",
                          userSelect: "none",
                          border: `1px dashed ${colors.borderStrong}`,
                          color: selected.rotationDays == null ? colors.textPrimary : colors.textDim,
                          background: selected.rotationDays == null ? colors.cardBgActive : "transparent",
                          opacity: rotationBusy ? 0.6 : 1,
                        }}
                      >
                        No rotation
                      </span>
                    </div>

                    {rotationError && <StateNote kind="over" text={rotationError} />}

                    <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                      <DetailRow
                        label="Next expected travel"
                        value={
                          selected.nextExpectedTravelDate ? (
                            shortDateLabel(selected.nextExpectedTravelDate)
                          ) : selected.rotationDays ? (
                            "Awaiting first trip"
                          ) : (
                            "Set a rotation to project it"
                          )
                        }
                      />
                      {(() => {
                        const due = rotationDueKind(selected, today);
                        return due ? (
                          <div style={{ display: "flex", justifyContent: "flex-end" }}>
                            <StatusChip kind={due} label={ROTATION_DUE_LABELS[due]} />
                          </div>
                        ) : null;
                      })()}
                    </div>
                  </Panel>
                ) : (
                  <Panel>
                    <SectionLabel>Rotation</SectionLabel>
                    <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, lineHeight: 1.55 }}>
                      Crew rotation applies to contract crew riders only — {SERVICE_TYPE_LABELS[selected.serviceType]}{" "}
                      riders travel on demand.
                    </div>
                  </Panel>
                )}
              </div>

              <div
                style={{
                  padding: "12px 15px",
                  background: "rgba(31,111,178,.07)",
                  border: "1px solid rgba(31,111,178,.25)",
                  borderRadius: 10,
                  fontFamily: fonts.body,
                  fontSize: 12,
                  lineHeight: 1.55,
                  color: colors.textMuted,
                }}
              >
                This directory builds itself from trip manifests — passengers are added or updated a few moments after
                a manifest is saved. Use &ldquo;Add from riders&rdquo; on a new manifest to auto-fill passenger rows.
              </div>
            </div>
          ) : (
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
              {riders !== null && riders.length === 0
                ? "Riders appear here automatically a few moments after a trip manifest is saved."
                : "Select a rider to see their details."}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

/** Loading / error / empty note — always icon + text + colour, never colour alone. */
function StateNote({ kind, text }: { kind: "over" | "soon" | "info"; text: string }) {
  const meta = statusMeta(kind);
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 9,
        padding: "10px 13px",
        marginBottom: 10,
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
