"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, svcMeta } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  hhmm,
  isCargoService,
  listScheduleTemplates,
  recurrenceSummary,
  svcForTrip,
  type ScheduleTemplateRecord,
} from "@/lib/api/trips";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { ServiceChip, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import SpecialDatesModal from "@/components/SpecialDatesModal";

// Cargo schedules — the Cargo/Grocery schedule templates, READ-ONLY here by
// design: template CRUD stays single-sourced in Routes & Schedules. What this
// pane adds is the cargo-first surface for SPECIAL DATES (Skip / ExtraRun /
// TimeOverride per date), the feature the cargo business runs on.

export default function CargoSchedulesPane({
  onOpenTrip,
}: {
  onOpenTrip: (tripId: string) => void;
}) {
  const [templates, setTemplates] = useState<ScheduleTemplateRecord[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [specialDatesFor, setSpecialDatesFor] = useState<ScheduleTemplateRecord | null>(null);

  const load = useCallback(async () => {
    try {
      const rows = await listScheduleTemplates();
      setTemplates(rows.filter((t) => isCargoService(t.serviceType)));
      setLoadError(null);
    } catch (e) {
      setTemplates(null);
      setLoadError(e instanceof ApiError ? e.message : "Failed to load cargo schedules.");
    }
  }, []);

  useEffect(() => {
    let active = true;
    listScheduleTemplates().then(
      (rows) => {
        if (active) {
          setTemplates(rows.filter((t) => isCargoService(t.serviceType)));
          setLoadError(null);
        }
      },
      (e) => {
        if (active) {
          setTemplates(null);
          setLoadError(e instanceof ApiError ? e.message : "Failed to load cargo schedules.");
        }
      },
    );
    return () => {
      active = false;
    };
  }, []);

  if (loadError) {
    return (
      <div style={{ padding: "14px 26px", maxWidth: 560 }}>
        <Panel borderColor="rgba(213,94,0,.4)">
          <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
            <StatusChip kind="over" label={`Cargo schedules unavailable — ${loadError}`} />
            <ActionButton variant="primary" onClick={load}>
              RETRY
            </ActionButton>
          </div>
        </Panel>
      </div>
    );
  }

  if (templates === null) {
    return (
      <div style={{ padding: "14px 26px" }}>
        {[0, 1, 2].map((i) => (
          <div
            key={i}
            style={{
              height: 58,
              borderRadius: 9,
              border: `1px solid ${colors.borderSubtle}`,
              background: colors.cardBg,
              marginBottom: 6,
              opacity: 0.55 - i * 0.12,
            }}
          />
        ))}
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, marginTop: 10 }}>
          Loading cargo schedule templates from API…
        </div>
      </div>
    );
  }

  return (
    <div style={{ flex: 1, minHeight: 0, overflowY: "auto", padding: "14px 26px 22px", borderTop: `1px solid ${colors.border}` }}>
      <div
        style={{
          fontFamily: fonts.semiCondensed,
          fontSize: 9.5,
          letterSpacing: ".14em",
          textTransform: "uppercase",
          color: colors.textFaint,
          margin: "6px 0 10px",
        }}
      >
        Cargo &amp; grocery schedule templates · {templates.length}
      </div>

      {templates.length === 0 && (
        <Panel style={{ maxWidth: 620 }}>
          <SectionLabel>No cargo schedules yet</SectionLabel>
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6 }}>
            Create a Cargo or Grocery schedule template in Routes &amp; Schedules — it needs no passenger seats — and
            it will appear here with its special-dates calendar.
          </div>
        </Panel>
      )}

      <div style={{ display: "flex", flexDirection: "column", gap: 8, maxWidth: 860 }}>
        {templates.map((t) => {
          const meta = svcMeta(svcForTrip(t.serviceType));
          return (
            <div
              key={t.id}
              style={{
                display: "flex",
                alignItems: "center",
                gap: 11,
                flexWrap: "wrap",
                padding: "13px 15px",
                borderRadius: 11,
                border: `1px solid ${colors.borderSubtle}`,
                background: colors.cardBg,
                boxShadow: `inset 3px 0 0 ${meta.accent}, ${colors.shadowCard}`,
              }}
            >
              <div style={{ minWidth: 0, flex: 1 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 9, flexWrap: "wrap" }}>
                  <span style={{ fontFamily: fonts.body, fontSize: 13.5, fontWeight: 700, color: colors.textPrimary }}>
                    {t.name}
                  </span>
                  <ServiceChip svc={svcForTrip(t.serviceType)} />
                  {t.active ? (
                    <StatusChip kind="ontime" label="Active" />
                  ) : (
                    <StatusChip kind="off" label="Inactive — not generating" />
                  )}
                </div>
                <div style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textDim, marginTop: 4 }}>
                  {t.routeName ?? "route"} · {recurrenceSummary(t)} · departs {hhmm(t.departureTime)}
                  {t.returnDepartureTime
                    ? ` · return ${hhmm(t.returnDepartureTime)}${t.returnNextDay ? " (next day)" : ""}`
                    : " · one-way"}
                </div>
                {t.cutoffNote && (
                  <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textMuted, marginTop: 3 }}>
                    Cutoff: {t.cutoffNote}
                  </div>
                )}
              </div>
              <ActionButton variant="primary" onClick={() => setSpecialDatesFor(t)}>
                SPECIAL DATES
              </ActionButton>
            </div>
          );
        })}
      </div>

      <div
        style={{
          marginTop: 16,
          maxWidth: 860,
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
        Templates are read-only here — create and edit them in{" "}
        <strong style={{ color: colors.textPrimary }}>Routes &amp; Schedules</strong>. Special dates apply at
        generation time only: a Skip stops a future trip from generating but never cancels one that already exists.
      </div>

      {specialDatesFor && (
        <SpecialDatesModal
          template={specialDatesFor}
          onClose={() => setSpecialDatesFor(null)}
          onOpenTrip={onOpenTrip}
        />
      )}
    </div>
  );
}
