"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import {
  getBookingSettings,
  updateBookingPolicy,
  upsertCorridorSettings,
  type BookingSettings,
  type CorridorSettingsRecord,
} from "@/lib/api/booking";
import { getRole } from "@/lib/claims";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { NumberField } from "@/components/ui/Field";

// Booking Policy (US-B.22/23) — the six tenant-wide policy numbers plus the
// per-corridor minimum/capacity table. Per-date overrides are edited from the
// Booking Calendar's day panel, not here.
//
// Edit controls are client-gated to the Owner role. That is a UX gate only —
// the real boundary is the AdminOnly policy on PUT /api/booking/settings/*
// (a non-Owner gets a 403; its message is surfaced below).

const POLICY_FIELDS = [
  { key: "cancellationWindowHours", label: "Cancellation window (hours)" },
  { key: "earlyCancellationPenaltyCad", label: "Early cancellation penalty (CAD)" },
  { key: "bookingCutoffHours", label: "Booking cutoff (hours before departure)" },
  { key: "seatHoldMinutes", label: "Seat hold (minutes)" },
  { key: "defaultPassengerMinimum", label: "Default passenger minimum" },
  { key: "defaultSeatCapacity", label: "Default seat capacity" },
] as const;

type PolicyKey = (typeof POLICY_FIELDS)[number]["key"];

export default function BookingPolicyPanel() {
  const isOwner = getRole() === "Owner";

  const [settings, setSettings] = useState<BookingSettings | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [draft, setDraft] = useState<Record<PolicyKey, string> | null>(null);
  const [policyBusy, setPolicyBusy] = useState(false);
  const [policyError, setPolicyError] = useState<string | null>(null);
  const [policySaved, setPolicySaved] = useState(false);

  const load = useCallback(async () => {
    try {
      const s = await getBookingSettings();
      setSettings(s);
      setLoadError(null);
      setDraft({
        cancellationWindowHours: String(s.policy.cancellationWindowHours),
        earlyCancellationPenaltyCad: String(s.policy.earlyCancellationPenaltyCad),
        bookingCutoffHours: String(s.policy.bookingCutoffHours),
        seatHoldMinutes: String(s.policy.seatHoldMinutes),
        defaultPassengerMinimum: String(s.policy.defaultPassengerMinimum),
        defaultSeatCapacity: String(s.policy.defaultSeatCapacity),
      });
    } catch (e) {
      setSettings(null);
      setLoadError(e instanceof ApiError ? e.message : "Failed to load the booking settings.");
    }
  }, []);

  // Mount fetch inlined with an `active` guard (house pattern — the
  // useCallback above stays for RETRY and post-save refreshes).
  useEffect(() => {
    let active = true;
    getBookingSettings().then(
      (s) => {
        if (!active) return;
        setSettings(s);
        setLoadError(null);
        setDraft({
          cancellationWindowHours: String(s.policy.cancellationWindowHours),
          earlyCancellationPenaltyCad: String(s.policy.earlyCancellationPenaltyCad),
          bookingCutoffHours: String(s.policy.bookingCutoffHours),
          seatHoldMinutes: String(s.policy.seatHoldMinutes),
          defaultPassengerMinimum: String(s.policy.defaultPassengerMinimum),
          defaultSeatCapacity: String(s.policy.defaultSeatCapacity),
        });
      },
      (e) => {
        if (!active) return;
        setSettings(null);
        setLoadError(e instanceof ApiError ? e.message : "Failed to load the booking settings.");
      },
    );
    return () => {
      active = false;
    };
  }, []);

  async function savePolicy() {
    if (policyBusy || !draft) return;
    const numbers = {} as Record<PolicyKey, number>;
    for (const f of POLICY_FIELDS) {
      const n = Number(draft[f.key]);
      if (draft[f.key].trim() === "" || Number.isNaN(n) || n < 0) {
        setPolicyError(`"${f.label}" needs a non-negative number.`);
        return;
      }
      numbers[f.key] = n;
    }
    setPolicyBusy(true);
    setPolicyError(null);
    setPolicySaved(false);
    try {
      await updateBookingPolicy(numbers);
      await load();
      setPolicySaved(true);
    } catch (e) {
      setPolicyError(e instanceof ApiError ? e.message : "Failed to save the booking policy.");
    } finally {
      setPolicyBusy(false);
    }
  }

  if (loadError) {
    return (
      <Panel borderColor="rgba(213,94,0,.4)">
        <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
          <StatusChip kind="over" label={`Booking settings unavailable — ${loadError}`} />
          <ActionButton variant="primary" onClick={load}>
            RETRY
          </ActionButton>
        </div>
      </Panel>
    );
  }

  if (!settings || !draft) {
    return (
      <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading booking settings…</div>
    );
  }

  return (
    <div>
      {!isOwner && (
        <div style={{ marginBottom: 12 }}>
          <StatusChip kind="off" label="Owner role required to edit — values shown read-only" />
        </div>
      )}

      <Panel style={{ marginBottom: 12 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
          <SectionLabel>Booking policy</SectionLabel>
          {!settings.policy.isPersisted && (
            <span style={{ marginTop: -11 }}>
              <StatusChip kind="soon" label="Defaults — never saved yet" />
            </span>
          )}
        </div>
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12 }}>
          {POLICY_FIELDS.map((f) => (
            <NumberField
              key={f.key}
              label={f.label}
              value={draft[f.key]}
              min={0}
              disabled={!isOwner}
              onChange={(v) => {
                setPolicySaved(false);
                setDraft((d) => (d ? { ...d, [f.key]: v } : d));
              }}
            />
          ))}
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 10, marginTop: 14, flexWrap: "wrap" }}>
          {isOwner && (
            <ActionButton variant="primary" onClick={() => void savePolicy()} disabled={policyBusy}>
              {policyBusy ? "WORKING…" : "SAVE POLICY"}
            </ActionButton>
          )}
          {policySaved && <StatusChip kind="ontime" label="Saved" />}
          {policyError && <StatusChip kind="over" label={policyError} />}
        </div>
        <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textMuted, marginTop: 12, lineHeight: 1.55 }}>
          Cutoff, window and penalty are stored and displayed this batch; hard enforcement lands with the public
          booking flow. Minimum and capacity drive the Booking Calendar today.
        </div>
      </Panel>

      <Panel>
        <SectionLabel>Per-corridor minimum &amp; capacity</SectionLabel>
        {settings.corridors.length === 0 ? (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6 }}>
            No corridors synced yet — corridors mirror the community routes in Routes &amp; Schedules and appear
            here after each route is re-saved once.
          </div>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {settings.corridors.map((c) => (
              <CorridorRow key={c.corridorId} row={c} canEdit={isOwner} onSaved={load} />
            ))}
          </div>
        )}
        <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textMuted, marginTop: 12, lineHeight: 1.55 }}>
          Blank = the policy default applies. Per-date overrides are set from the Booking Calendar&apos;s day panel.
        </div>
      </Panel>
    </div>
  );
}

function CorridorRow({
  row,
  canEdit,
  onSaved,
}: {
  row: CorridorSettingsRecord;
  canEdit: boolean;
  onSaved: () => Promise<void>;
}) {
  const [min, setMin] = useState(row.passengerMinimum === null ? "" : String(row.passengerMinimum));
  const [cap, setCap] = useState(row.seatCapacity === null ? "" : String(row.seatCapacity));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  async function save() {
    if (busy) return;
    setBusy(true);
    setError(null);
    setSaved(false);
    try {
      await upsertCorridorSettings(row.corridorId, {
        passengerMinimum: min.trim() === "" ? null : Number(min),
        seatCapacity: cap.trim() === "" ? null : Number(cap),
      });
      await onSaved();
      setSaved(true);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to save the corridor settings.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div
      style={{
        display: "grid",
        gridTemplateColumns: "minmax(140px, 1fr) 130px 130px auto",
        gap: 10,
        alignItems: "end",
        padding: "10px 13px",
        borderRadius: 9,
        border: `1px solid ${colors.borderSubtle}`,
        background: colors.cardBg,
        boxShadow: colors.shadowCard,
      }}
    >
      <div style={{ alignSelf: "center" }}>
        <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
          {row.corridorName}
        </div>
        {error && (
          <div style={{ marginTop: 4, display: "flex", alignItems: "center", gap: 5 }}>
            <span style={{ color: statusMeta("over").t, fontSize: 10, fontWeight: 800 }} aria-hidden>
              {statusMeta("over").g}
            </span>
            <span style={{ fontFamily: fonts.body, fontSize: 11, color: statusMeta("over").t }}>{error}</span>
          </div>
        )}
      </div>
      <NumberField label="Minimum" value={min} min={0} disabled={!canEdit} placeholder="default" onChange={(v) => { setSaved(false); setMin(v); }} />
      <NumberField label="Capacity" value={cap} min={0} disabled={!canEdit} placeholder="default" onChange={(v) => { setSaved(false); setCap(v); }} />
      <div style={{ display: "flex", alignItems: "center", gap: 8, height: 40 }}>
        {canEdit && (
          <ActionButton onClick={() => void save()} disabled={busy}>
            {busy ? "WORKING…" : "SAVE"}
          </ActionButton>
        )}
        {saved && <StatusChip kind="ontime" label="Saved" />}
      </div>
    </div>
  );
}
