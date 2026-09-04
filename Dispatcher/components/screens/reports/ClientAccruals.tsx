"use client";

import { useEffect, useMemo, useState, type ReactNode } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  directionMeta,
  formatInvoiceCad,
  getInvoice,
  invoiceChip,
  periodLabel as invoicePeriodLabel,
  type InvoiceDetailRecord,
} from "@/lib/api/billing";
import {
  contractRateLabel,
  listClients,
  renewalChipFor,
  type ClientRecord,
} from "@/lib/api/clients";
import { corridorLabel, listTrips, shortDateLabel, todayIso, type TripRecord } from "@/lib/api/trips";
import {
  ACCRUAL_BUCKET_META,
  ACCRUALS_ESTIMATE_NOTE,
  ACCRUALS_GST_NOTE,
  AMOUNT_NOTE_META,
  accrualsClipboardText,
  buildAccrualsReport,
  groupAmountLabel,
  groupRefLabel,
  groupRouteLabel,
  type AccrualBucket,
  type AccrualGroup,
  type AccrualsReport,
} from "@/lib/billing/accruals";
import { copyToClipboard } from "@/lib/clipboard";
import { printAccrualsReport } from "@/lib/documents/accrualsPdf";
import { periodLabel, type Period } from "@/lib/period";
import SendAccrualsEmailModal from "@/components/SendAccrualsEmailModal";
import { PageHeader, Panel, SectionLabel } from "@/components/ui/Panel";
import { cellStyle, headerRowStyle } from "@/components/screens/reports/shared";
import { MonoTag, StatusBadge, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { SelectField } from "@/components/ui/Field";
import { PeriodNav } from "@/components/ui/PeriodNav";

// Client Accruals — the monthly per-client accruals report, one of the two
// reports the Reports screen hosts: every trip in the month
// bucketed by billing state (paid / invoiced / ready / scheduled / upcoming),
// real invoice amounts where invoiced or paid, clearly-marked contract-rate
// estimates elsewhere, and a reconciliation section for cancelled/written-off
// trips. The derivation lives in lib/billing/accruals.ts, shared with the
// printed NL-ACC-01 sheet and the clipboard export so all three agree.

/** Pseudo-table column template shared by the header row and every group row. */
const GRID_COLS = "88px 170px 1fr 90px 140px 150px";

// ---------------------------------------------------------------------------
// Row pieces
// ---------------------------------------------------------------------------

/** One leg in the Trips cell — number + direction glyph AND word (never a bare
 *  arrow), with a DEADHEAD tag when the leg ran empty by design. */
function LegLine({ leg }: { leg: TripRecord }) {
  const d = directionMeta(leg.direction);
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 6, minWidth: 0 }}>
      <span style={cellStyle}>{leg.tripNumber}</span>
      {d && (
        <span
          style={{ fontFamily: fonts.body, fontSize: 10, color: colors.textDim, flex: "none" }}
          title={`${d.label} leg`}
        >
          {d.glyph} {d.label}
        </span>
      )}
      {leg.isEmptyLeg && <MonoTag>DEADHEAD</MonoTag>}
    </div>
  );
}

/** Amount cell: real dollars, "$X est.", or the spelled-out unpriced reason
 *  as a chip (colour + glyph + text — never colour alone). A plain "—" means
 *  the banner notes explain it (manual billing / no contract / no rate). */
function AmountCell({ group }: { group: AccrualGroup }) {
  const label = groupAmountLabel(group);
  if (label !== null) {
    return (
      <div style={{ ...cellStyle, textAlign: "right", color: colors.textSecondary, fontWeight: 600 }}>
        {label}
      </div>
    );
  }
  if (group.amountNote) {
    const meta = AMOUNT_NOTE_META[group.amountNote];
    return (
      <div style={{ display: "flex", justifyContent: "flex-end" }}>
        <StatusChip kind={meta.kind} label={meta.label} />
      </div>
    );
  }
  return <div style={{ ...cellStyle, textAlign: "right", color: colors.textDim }}>—</div>;
}

function GroupRow({ group, last }: { group: AccrualGroup; last: boolean }) {
  return (
    <div
      style={{
        display: "grid",
        gridTemplateColumns: GRID_COLS,
        gap: 11,
        alignItems: "center",
        padding: "8px 13px",
        borderBottom: last ? undefined : `1px solid ${colors.borderSubtle}`,
      }}
    >
      <div style={cellStyle}>{shortDateLabel(group.legs[0].serviceDate)}</div>
      <div style={{ display: "flex", flexDirection: "column", gap: 3, minWidth: 0 }}>
        {group.legs.map((leg) => (
          <LegLine key={leg.id} leg={leg} />
        ))}
      </div>
      <div style={{ ...cellStyle, fontFamily: fonts.body, fontSize: 12, color: colors.textMuted }}>
        {groupRouteLabel(group)}
      </div>
      <div style={cellStyle}>{group.legs[0].poNumber ?? "—"}</div>
      <div style={cellStyle}>{groupRefLabel(group)}</div>
      <AmountCell group={group} />
    </div>
  );
}

// ---------------------------------------------------------------------------
// Summary tile — sibling of Billing's ReceivableTile (kept local on purpose).
// The status kind pairs a colour with the StatusChip's glyph and text label,
// so the bucket distinction never rests on colour alone.
// ---------------------------------------------------------------------------

function AccrualTile({ bucket }: { bucket: AccrualBucket }) {
  const n = bucket.groups.length;
  const sublines: string[] = [`${n} round trip${n === 1 ? "" : "s"}`];
  if (bucket.estimatedCad > 0) sublines.push(`incl. ${formatInvoiceCad(bucket.estimatedCad)} est.`);
  if (bucket.unpricedCount > 0) sublines.push(`${bucket.unpricedCount} unpriced`);
  return (
    <div
      style={{
        flex: "1 1 160px",
        padding: "11px 14px",
        background: colors.cardBg,
        border: `1px solid ${colors.border}`,
        borderRadius: 11,
        boxShadow: colors.shadowCard,
      }}
    >
      <StatusChip kind={bucket.kind} label={bucket.label} />
      <div
        style={{
          fontFamily: fonts.condensed,
          fontWeight: 700,
          fontSize: 21,
          color: colors.headingBright,
          fontVariantNumeric: "tabular-nums",
          marginTop: 7,
        }}
      >
        {formatInvoiceCad(bucket.actualCad + bucket.estimatedCad)}
      </div>
      <div style={{ fontFamily: fonts.mono, fontSize: 10, color: colors.textDim, marginTop: 3, minHeight: 13 }}>
        {sublines.join(" · ") || " "}
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Bucket section — chip + hint, pseudo-table, totals row.
// ---------------------------------------------------------------------------

function BucketSection({ bucket }: { bucket: AccrualBucket }) {
  const tallies = [`Actual ${formatInvoiceCad(bucket.actualCad)}`];
  if (bucket.estimatedCad > 0) tallies.push(`Estimated ${formatInvoiceCad(bucket.estimatedCad)} est.`);
  if (bucket.unpricedCount > 0) tallies.push(`${bucket.unpricedCount} unpriced`);
  return (
    <div style={{ marginTop: 16 }}>
      <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 7, flexWrap: "wrap" }}>
        <StatusChip kind={bucket.kind} label={bucket.label} />
        <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
          {ACCRUAL_BUCKET_META[bucket.id].hint}
        </span>
      </div>
      <div style={{ border: `1px solid ${colors.borderSubtle}`, borderRadius: 9, overflow: "hidden", background: colors.cardBg }}>
        <div style={headerRowStyle(GRID_COLS)}>
          <div>Date</div>
          <div>Trips</div>
          <div>Route</div>
          <div>PO</div>
          <div>Ref</div>
          <div style={{ textAlign: "right" }}>Amount</div>
        </div>
        {bucket.groups.map((g, i) => (
          <GroupRow key={g.key} group={g} last={i === bucket.groups.length - 1} />
        ))}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            gap: 12,
            padding: "7px 13px",
            borderTop: `1px solid ${colors.borderSubtle}`,
            background: colors.cardBgActive,
            fontFamily: fonts.mono,
            fontSize: 10.5,
            color: colors.textMuted,
          }}
        >
          <span>
            {bucket.groups.length} round trip{bucket.groups.length === 1 ? "" : "s"}
          </span>
          <span>{tallies.join(" · ")}</span>
        </div>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------
// The screen
// ---------------------------------------------------------------------------

export default function ClientAccruals({
  clientId,
  setClientId,
  period,
  setPeriod,
  tabs,
}: {
  clientId: string | null;
  setClientId: (id: string | null) => void;
  period: Period;
  setPeriod: (p: Period) => void;
  /** The report switcher, built once by the Reports shell. It renders inside
   *  this header block rather than above it because the PageHeader's actions
   *  are report-local (copy state, the email modal). */
  tabs: ReactNode;
}) {
  // Client roster for the picker.
  const [clients, setClients] = useState<ClientRecord[] | null>(null);
  const [rosterError, setRosterError] = useState<string | null>(null);
  const [rosterAttempt, setRosterAttempt] = useState(0);

  useEffect(() => {
    let active = true;
    listClients().then(
      (rows) => {
        if (active) setClients([...rows].sort((a, b) => a.name.localeCompare(b.name)));
      },
      (e) => {
        if (active) setRosterError(e instanceof ApiError ? e.message : "Failed to load the client roster.");
      },
    );
    return () => {
      active = false;
    };
  }, [rosterAttempt]);

  const selected = clients?.find((c) => c.id === clientId) ?? null;

  // Month data, keyed by the client+period it was fetched for so a changed
  // selection simply stops matching (no synchronous state resets) — the same
  // stale-response guard as Billing's GenerateDraftModal preview.
  const dataKey = clientId ? `${clientId}|${period.start}|${period.end}` : null;
  const [data, setData] = useState<{
    key: string;
    today: string;
    trips: TripRecord[];
    invoices: InvoiceDetailRecord[];
  } | null>(null);
  const [dataError, setDataError] = useState<{ key: string; message: string } | null>(null);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    if (!dataKey) return;
    let active = true;
    const [cid, from, to] = dataKey.split("|");
    (async () => {
      // listTrips unpaged returns the complete match — one call per month.
      const page = await listTrips({ from, to, clientId: cid });
      // Then the distinct invoices the trips' billing states reference: the
      // real amounts live on their lines. One failed detail fetch degrades
      // that invoice's amounts to "unavailable", not the whole report.
      const ids = [
        ...new Set(
          page.items
            .map((t) => t.billing?.invoiceId)
            .filter((id): id is string => Boolean(id)),
        ),
      ];
      const fetched = await Promise.all(
        ids.map((id) => getInvoice(id).catch((): InvoiceDetailRecord | null => null)),
      );
      if (!active) return;
      setData({
        key: dataKey,
        // The day the scheduled/upcoming split was made against — captured at
        // fetch time so the memoised report stays stable across renders.
        today: todayIso(),
        trips: page.items,
        invoices: fetched.filter((inv): inv is InvoiceDetailRecord => inv !== null),
      });
    })().catch((e) => {
      if (active)
        setDataError({
          key: dataKey,
          message: e instanceof ApiError ? e.message : "Failed to load trips for the period.",
        });
    });
    return () => {
      active = false;
    };
  }, [dataKey, attempt]);

  const loaded = data !== null && data.key === dataKey ? data : null;
  const loadErr = dataError !== null && dataError.key === dataKey ? dataError.message : null;

  const report: AccrualsReport | null = useMemo(
    () =>
      loaded && selected
        ? buildAccrualsReport({
            client: selected,
            period,
            today: loaded.today,
            trips: loaded.trips,
            invoices: loaded.invoices,
          })
        : null,
    [loaded, selected, period],
  );

  function retry() {
    setDataError(null);
    setAttempt((a) => a + 1);
  }

  // Copy feedback, same shape as Billing's COPY FOR QUICKBOOKS.
  const [copyState, setCopyState] = useState<"idle" | "ok" | "fail">("idle");
  async function handleCopy() {
    if (!report) return;
    const ok = await copyToClipboard(accrualsClipboardText(report));
    setCopyState(ok ? "ok" : "fail");
    setTimeout(() => setCopyState("idle"), ok ? 2000 : 4500);
  }

  // The email modal gets a SNAPSHOT of the report taken when it opens, so a
  // month step or refetch under the open modal can't change what gets sent.
  const [emailReport, setEmailReport] = useState<AccrualsReport | null>(null);

  const contract = selected?.activeContract ?? null;
  const renewalChip = selected ? renewalChipFor(contract) : null;
  const emptyMonth = report !== null && report.buckets.every((b) => b.groups.length === 0);

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 6px" }}>
        <PageHeader
          eyebrow="Business · Monthly client accruals"
          title="Reports"
          right={
            <div style={{ display: "flex", gap: 10 }}>
              <ActionButton onClick={handleCopy} disabled={!report}>
                {copyState === "ok" ? "COPIED ✓" : copyState === "fail" ? "COPY FAILED" : "COPY REPORT"}
              </ActionButton>
              <ActionButton onClick={() => report && setEmailReport(report)} disabled={!report}>
                EMAIL TO CLIENT
              </ActionButton>
              <ActionButton
                variant="primary"
                onClick={() => report && printAccrualsReport(report)}
                disabled={!report}
              >
                PRINT ACCRUALS REPORT
              </ActionButton>
            </div>
          }
        />
        {tabs}
      </div>

      <div style={{ flex: 1, minHeight: 0, overflowY: "auto", padding: "4px 26px 26px" }}>
        {/* controls — client picker + month stepper */}
        <Panel style={{ marginTop: 14 }}>
          <div style={{ display: "flex", gap: 20, flexWrap: "wrap", alignItems: "flex-end" }}>
            <div style={{ flex: "1 1 260px", maxWidth: 400 }}>
              <SelectField
                label={clients === null && !rosterError ? "Client (loading…)" : "Client"}
                value={clientId ?? ""}
                onChange={(v) => setClientId(v || null)}
                disabled={clients === null}
                options={[
                  { value: "", label: clients === null ? "Loading clients…" : "Select a client…" },
                  ...(clients ?? []).map((c) => ({ value: c.id, label: c.name })),
                ]}
              />
            </div>
            <div style={{ paddingBottom: 2 }}>
              <PeriodNav period={period} onChange={setPeriod} granularities={["month"]} />
            </div>
          </div>

          {rosterError && (
            <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap", marginTop: 12 }}>
              <StatusChip kind="over" label={`Client roster unavailable — ${rosterError}`} />
              <ActionButton
                variant="primary"
                onClick={() => {
                  setRosterError(null);
                  setRosterAttempt((a) => a + 1);
                }}
              >
                RETRY
              </ActionButton>
            </div>
          )}

          {/* contract hint — what any estimates below are priced from */}
          {selected && renewalChip && (
            <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap", marginTop: 12 }}>
              <StatusChip kind={renewalChip.kind} label={renewalChip.label} />
              <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textMuted }}>
                {contract ? contractRateLabel(contract) : "No contract terms on file"}
              </span>
              {contract?.defaultPoNumber && (
                <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textDim }}>
                  Default PO {contract.defaultPoNumber}
                </span>
              )}
              {contract?.budgetCode && (
                <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textDim }}>
                  Budget code {contract.budgetCode}
                </span>
              )}
            </div>
          )}
        </Panel>

        {/* no selection yet */}
        {!clientId && (
          <Panel style={{ marginTop: 14 }}>
            <SectionLabel>Monthly accruals by client</SectionLabel>
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6 }}>
              Pick a client to build their accruals report for the month shown: every trip bucketed by
              billing state, real invoice amounts where invoiced or paid, contract-rate estimates
              (clearly marked) elsewhere, and a reconciliation of cancelled and written-off trips.
            </div>
          </Panel>
        )}

        {/* fetch failed */}
        {clientId && loadErr && (
          <Panel borderColor="rgba(213,94,0,.4)" style={{ marginTop: 14 }}>
            <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
              <StatusChip kind="over" label={`Report unavailable — ${loadErr}`} />
              <ActionButton variant="primary" onClick={retry}>
                RETRY
              </ActionButton>
            </div>
          </Panel>
        )}

        {/* loading */}
        {clientId && !loadErr && !report && (
          <div style={{ marginTop: 14 }}>
            {[0, 1, 2, 3].map((i) => (
              <div
                key={i}
                style={{
                  height: 56,
                  borderRadius: 9,
                  border: `1px solid ${colors.borderSubtle}`,
                  background: colors.cardBg,
                  marginBottom: 5,
                  opacity: 0.55 - i * 0.11,
                }}
              />
            ))}
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, marginTop: 10 }}>
              Loading trips and invoices from API…
            </div>
          </div>
        )}

        {report && (
          <>
            {/* degradation banners — each explains a class of "—" rows below */}
            {report.notes.length > 0 && (
              <Panel borderColor="rgba(225,176,0,.45)" style={{ marginTop: 14 }}>
                {report.notes.map((note, i) => (
                  <div
                    key={i}
                    style={{
                      display: "flex",
                      alignItems: "flex-start",
                      gap: 9,
                      marginTop: i === 0 ? 0 : 8,
                      fontFamily: fonts.body,
                      fontSize: 12.5,
                      color: colors.textMuted,
                      lineHeight: 1.5,
                    }}
                  >
                    <StatusBadge kind="soon" />
                    <span>{note}</span>
                  </div>
                ))}
              </Panel>
            )}

            {/* summary tiles — one per bucket, always all five */}
            <div style={{ display: "flex", gap: 10, flexWrap: "wrap", marginTop: 14 }}>
              {report.buckets.map((b) => (
                <AccrualTile key={b.id} bucket={b} />
              ))}
            </div>

            {/* empty month still renders — and still prints */}
            {emptyMonth && (
              <Panel style={{ marginTop: 14 }}>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted }}>
                  No trips for {report.client.name} in {periodLabel(report.period)}. The report can
                  still be printed as an explicit nil statement.
                </div>
              </Panel>
            )}

            {/* per-bucket detail tables (non-empty buckets only) */}
            {report.buckets
              .filter((b) => b.groups.length > 0)
              .map((b) => (
                <BucketSection key={b.id} bucket={b} />
              ))}

            {/* reconciliation — listed so the month adds up, never counted */}
            {(report.cancelled.length > 0 || report.writtenOff.length > 0) && (
              <Panel style={{ marginTop: 18 }}>
                <SectionLabel>Reconciliation — not counted in accruals</SectionLabel>
                {report.cancelled.map((t) => (
                  <div
                    key={t.id}
                    style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap", marginBottom: 7 }}
                  >
                    <StatusChip kind="off" label="Cancelled" />
                    <span style={cellStyle}>{shortDateLabel(t.serviceDate)}</span>
                    <span style={cellStyle}>{t.tripNumber}</span>
                    <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted }}>
                      {corridorLabel(t)}
                    </span>
                    <span style={{ fontFamily: fonts.body, fontSize: 11.5, fontStyle: "italic", color: colors.textDim }}>
                      {t.cancelledReason ?? "no reason recorded"}
                    </span>
                  </div>
                ))}
                {report.writtenOff.map((g) => (
                  <div
                    key={g.key}
                    style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap", marginBottom: 7 }}
                  >
                    <StatusChip
                      kind="over"
                      label={`Written off${groupAmountLabel(g) ? ` · ${groupAmountLabel(g)}` : " · amount unavailable"}`}
                    />
                    <span style={cellStyle}>{shortDateLabel(g.legs[0].serviceDate)}</span>
                    <span style={cellStyle}>{g.legs.map((l) => l.tripNumber).join(" + ")}</span>
                    <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted }}>
                      {groupRouteLabel(g)}
                    </span>
                    <span style={{ fontFamily: fonts.body, fontSize: 11.5, fontStyle: "italic", color: colors.textDim }}>
                      {g.legs.map((l) => l.writtenOffReason).find(Boolean) ?? "no reason recorded"}
                    </span>
                  </div>
                ))}
              </Panel>
            )}

            {/* invoices referenced — the one place GST shows */}
            <Panel style={{ marginTop: 18 }}>
              <SectionLabel>Invoices referenced — GST shown here</SectionLabel>
              {report.invoices.length === 0 ? (
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
                  No issued invoices are referenced by this month&rsquo;s trips.
                </div>
              ) : (
                report.invoices.map((inv) => {
                  const chip = invoiceChip(inv);
                  return (
                    <div
                      key={inv.id}
                      style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap", marginBottom: 7 }}
                    >
                      <span style={{ ...cellStyle, fontWeight: 600, color: colors.headingBright }}>
                        {inv.invoiceNumber}
                        {inv.qboInvoiceId ? ` · QBO ${inv.qboInvoiceId}` : ""}
                      </span>
                      <StatusChip kind={chip.kind} label={chip.label} />
                      <span style={cellStyle}>{invoicePeriodLabel(inv)}</span>
                      <span style={{ ...cellStyle, marginLeft: "auto" }}>
                        {formatInvoiceCad(inv.subtotalCad)} + GST {formatInvoiceCad(inv.gstCad)} ={" "}
                        {formatInvoiceCad(inv.totalCad)}
                      </span>
                    </div>
                  );
                })
              )}
            </Panel>

            {/* footer wording shared with the printed sheet + clipboard */}
            <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, lineHeight: 1.6, marginTop: 14 }}>
              {ACCRUALS_GST_NOTE} {ACCRUALS_ESTIMATE_NOTE}
            </div>
          </>
        )}
      </div>

      {emailReport && (
        <SendAccrualsEmailModal report={emailReport} onClose={() => setEmailReport(null)} />
      )}
    </div>
  );
}
