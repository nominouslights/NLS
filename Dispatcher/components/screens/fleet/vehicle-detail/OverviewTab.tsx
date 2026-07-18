"use client";

import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import { pmReminders } from "@/lib/data";
import { formatCad, formatKm, formatUtcDate, isDisposed, lifeKindFor, type Vehicle } from "@/lib/api";
import { DetailRow, Panel, SectionLabel } from "@/components/ui/Panel";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import OverviewPrompts from "./OverviewPrompts";
import OverviewActions from "./OverviewActions";
import { EmptyTabNote, errBadge, warnBanner, type ModalKind, type PromptKind } from "./shared";

export interface OverviewTabProps {
  f: Vehicle;
  prompt: PromptKind;
  setPrompt: (p: PromptKind) => void;
  reasonInput: string;
  setReasonInput: (v: string) => void;
  odoInput: string;
  setOdoInput: (v: string) => void;
  priceInput: string;
  setPriceInput: (v: string) => void;
  busy: boolean;
  actionError: string | null;
  autoRetired: boolean;
  runAction: (fn: () => Promise<void>, opts?: { detectAutoRetire?: boolean }) => void;
  setModal: (m: ModalKind) => void;
  setCertOpen: (b: boolean) => void;
}

export default function OverviewTab({
  f,
  prompt,
  setPrompt,
  reasonInput,
  setReasonInput,
  odoInput,
  setOdoInput,
  priceInput,
  setPriceInput,
  busy,
  actionError,
  autoRetired,
  runAction,
  setModal,
  setCertOpen,
}: OverviewTabProps) {
  const oos = f.status === "OutOfService";
  const disposed = isDisposed(f.status);
  const retired = f.status === "Retired";
  const readOnly = disposed;
  const lifePct = Math.min(100, Math.max(0, f.lifeUsedPct));
  const lifeKind = lifeKindFor(f.lifeUsedPct);
  const lifeMeta = statusMeta(lifeKind);
  const unitPm = pmReminders.filter((r) => r.unit === f.unitNumber);

  return (
    <div>
      {autoRetired && (
        <div style={warnBanner}>
          <span style={errBadge}>▲</span>
          <div style={{ fontFamily: fonts.body, fontSize: 13, color: statusMeta("over").t, fontWeight: 600 }}>
            End of service life reached — vehicle retired. A retirement certificate has been issued.
          </div>
        </div>
      )}

      {oos && (
        <div style={warnBanner}>
          <span style={errBadge}>▲</span>
          <div style={{ fontFamily: fonts.body, fontSize: 13, color: statusMeta("over").t, fontWeight: 600 }}>
            Out of service — removed from dispatch eligibility until cleared.
            {f.statusReason ? ` Reason: ${f.statusReason}.` : ""}
          </div>
        </div>
      )}

      {actionError && (
        <div
          style={{
            padding: "11px 14px",
            background: "rgba(213,94,0,.1)",
            border: "1px solid rgba(213,94,0,.4)",
            borderRadius: 10,
            marginBottom: 14,
            fontFamily: fonts.body,
            fontSize: 12.5,
            color: statusMeta("over").t,
            fontWeight: 600,
          }}
        >
          ▲ {actionError}
        </div>
      )}

      {/* End of Service Life panel — REAL data */}
      <Panel style={{ marginBottom: 12 }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 11 }}>
          <SectionLabel>End of service life · km depreciation</SectionLabel>
          <StatusChip
            kind={lifeKind}
            label={f.lifeUsedPct >= 100 ? "Service life exhausted" : `${Math.round(f.lifeUsedPct)}% life used`}
          />
        </div>
        <div style={{ display: "flex", alignItems: "baseline", gap: 10, marginBottom: 10 }}>
          <span
            style={{
              fontFamily: fonts.condensed,
              fontWeight: 700,
              fontSize: 28,
              color: colors.headingBright,
              fontVariantNumeric: "tabular-nums",
            }}
          >
            {formatCad(f.currentValueCad)}
          </span>
          <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
            current value · acquired {formatCad(f.acquisitionCostCad)}
          </span>
        </div>
        <div style={{ height: 10, borderRadius: 6, background: colors.inputBg, overflow: "hidden", border: `1px solid ${colors.borderSubtle}` }}>
          <div style={{ height: "100%", width: `${lifePct}%`, background: lifeMeta.c, borderRadius: 6 }} />
        </div>
        <div style={{ display: "flex", justifyContent: "space-between", fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginTop: 7 }}>
          <span>{formatKm(f.odometerKm)} of {formatKm(f.endOfLifeKm)}</span>
          <span>{formatKm(f.remainingKm)} remaining · auto-retires at end-of-life km</span>
        </div>

        {(retired || disposed) && (
          <div style={{ marginTop: 13, paddingTop: 12, borderTop: `1px solid ${colors.borderSubtle}`, display: "flex", flexDirection: "column", gap: 7 }}>
            <DetailRow label="Lifecycle" value={disposed ? `Retired → ${f.status}` : "Retired — awaiting disposal (sell or recycle)"} />
            {f.statusReason && <DetailRow label="Retirement reason" value={f.statusReason} />}
            {f.status === "Sold" && (
              <DetailRow
                label="Sale price"
                value={f.salePriceCad != null ? formatCad(f.salePriceCad) : "—"}
                valueStyle={{ fontFamily: fonts.mono, fontSize: 12 }}
              />
            )}
            {disposed && <DetailRow label="Disposed" value={formatUtcDate(f.disposedAtUtc)} />}
            <DetailRow label="Retirement certificate" value="On file — view via the actions below" />
          </div>
        )}
      </Panel>

      {/* registry details — REAL data */}
      <Panel style={{ marginBottom: 12 }}>
        <SectionLabel>Registry record</SectionLabel>
        <div style={{ display: "flex", flexDirection: "column", gap: 7 }}>
          <DetailRow label="VIN" value={f.vin} valueStyle={{ fontFamily: fonts.mono, fontSize: 12 }} />
          <DetailRow label="Licence plate" value={f.licencePlate} valueStyle={{ fontFamily: fonts.mono, fontSize: 12 }} />
          <DetailRow label="Required licence" value={f.requiredLicenceClass} />
          <DetailRow label="Odometer" value={formatKm(f.odometerKm)} valueStyle={{ fontFamily: fonts.mono, fontSize: 12 }} />
          <DetailRow
            label="Periodic inspection"
            value={
              f.requiresPeriodicInspection
                ? "NSC-11 six-month periodic (11+ seats)"
                : "Trip-inspection / DVIR only (under 11 seats)"
            }
          />
          <DetailRow label="Registered" value={formatUtcDate(f.registeredAtUtc)} />
          <DetailRow label="Last updated" value={formatUtcDate(f.updatedAtUtc)} />
        </div>
      </Panel>

      {/* Preventive maintenance — MOCK (folded from the old Maintenance tab) */}
      <Panel style={{ marginBottom: 14 }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 4 }}>
          <SectionLabel>Preventive maintenance reminders</SectionLabel>
          <MonoTag color={statusMeta("soon").t}>MOCK</MonoTag>
        </div>
        {unitPm.length === 0 ? (
          <EmptyTabNote>{`No preventive-maintenance reminders on file for ${f.unitNumber}.`}</EmptyTabNote>
        ) : (
          unitPm.map((r) => (
            <div
              key={`${r.unit}-${r.task}`}
              style={{
                display: "grid",
                gridTemplateColumns: "1fr 90px 150px",
                gap: 11,
                alignItems: "center",
                padding: "10px 12px",
                marginBottom: 5,
                ...rowSurface(false),
                cursor: "default",
              }}
            >
              <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
                {r.task}
              </div>
              <MonoTag>{r.basis.toUpperCase()}</MonoTag>
              <StatusChip kind={r.k} label={r.due} />
            </div>
          ))
        )}
      </Panel>

      {/* inline confirm-prompts (out-of-service, odometer, retire, sell, recycle) */}
      <OverviewPrompts
        f={f}
        prompt={prompt}
        setPrompt={setPrompt}
        reasonInput={reasonInput}
        setReasonInput={setReasonInput}
        odoInput={odoInput}
        setOdoInput={setOdoInput}
        priceInput={priceInput}
        setPriceInput={setPriceInput}
        busy={busy}
        runAction={runAction}
      />

      {/* lifecycle action buttons — driven by the legal transition matrix */}
      <OverviewActions
        f={f}
        prompt={prompt}
        readOnly={readOnly}
        retired={retired}
        runAction={runAction}
        setPrompt={setPrompt}
        setReasonInput={setReasonInput}
        setOdoInput={setOdoInput}
        setPriceInput={setPriceInput}
        setModal={setModal}
        setCertOpen={setCertOpen}
      />
    </div>
  );
}
