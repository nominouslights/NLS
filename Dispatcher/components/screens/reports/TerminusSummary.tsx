"use client";

import { useEffect, useMemo, useState, type ReactNode } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import { listStops, stopTypeLabel, type StopRecord } from "@/lib/api/stops";
import { corridorLabel, listTrips, shortDateLabel, todayIso, type TripRecord } from "@/lib/api/trips";
import {
  averageKmLabel,
  buildTerminusReport,
  distanceLabel,
  flowLabel,
  terminusClipboardText,
  terminusRefFor,
  terminusStops,
  utilizationChip,
  utilizationLabel,
  utilizationRateLabel,
  type TerminusCorridor,
  type TerminusReport,
} from "@/lib/reports/terminus";
import { copyToClipboard } from "@/lib/clipboard";
import { printTerminusSummary } from "@/lib/documents/terminusPdf";
import { periodLabel, type Period } from "@/lib/period";
import { cellStyle, headerRowStyle } from "@/components/screens/reports/shared";
import { PageHeader, Panel, SectionLabel } from "@/components/ui/Panel";
import { MonoTag, StatusBadge, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { SelectField } from "@/components/ui/Field";
import { PeriodNav } from "@/components/ui/PeriodNav";

// Terminus Summary — service operated to and from one terminus venue over a
// period: legs, flow, distance and seat utilization, broken down by the far end
// of each corridor. Built to be handed to the venue while negotiating billing,
// which is why every excluded leg is listed rather than dropped. The derivation
// lives in lib/reports/terminus.ts, shared with the printed NL-TRM-01 sheet and
// the clipboard export so all three agree.

/** Pseudo-table column template shared by the header row and every corridor row. */
const GRID_COLS = "1fr 70px 110px 120px 120px 110px";

// ---------------------------------------------------------------------------
// Row pieces
// ---------------------------------------------------------------------------

/** One corridor: the far end named venue-first, with its community and route
 *  names beneath, and a call-out when that far end is NOT itself a terminus
 *  venue — a corridor to an airport is a different fact from one to a partner. */
function CorridorRow({ corridor, last }: { corridor: TerminusCorridor; last: boolean }) {
  const u = corridor.utilization;
  return (
    <div
      style={{
        display: "grid",
        gridTemplateColumns: GRID_COLS,
        gap: 11,
        padding: "9px 13px",
        alignItems: "center",
        borderBottom: last ? undefined : `1px solid ${colors.borderSubtle}`,
      }}
    >
      <div style={{ minWidth: 0 }}>
        <div
          style={{
            fontFamily: fonts.body,
            fontSize: 12.5,
            fontWeight: 600,
            color: colors.headingBright,
            overflow: "hidden",
            textOverflow: "ellipsis",
            whiteSpace: "nowrap",
          }}
        >
          {corridor.label}
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 7, marginTop: 3, flexWrap: "wrap" }}>
          {!corridor.farEndIsTerminus && <MonoTag>NOT A TERMINUS</MonoTag>}
          {corridor.routeNames.length > 0 && (
            <span style={{ fontFamily: fonts.mono, fontSize: 10, color: colors.textDim }}>
              {corridor.routeNames.join(" · ")}
            </span>
          )}
        </div>
      </div>
      <div style={{ ...cellStyle, textAlign: "right" }}>{corridor.legs.length}</div>
      <div style={{ ...cellStyle, textAlign: "right" }}>{flowLabel(corridor)}</div>
      <div style={{ ...cellStyle, textAlign: "right" }}>{distanceLabel(corridor.distance)}</div>
      <div style={{ ...cellStyle, textAlign: "right" }}>{averageKmLabel(corridor.distance)}</div>
      <div
        style={{
          ...cellStyle,
          textAlign: "right",
          color: u.rate === null ? colors.textDim : colors.textSecondary,
          fontWeight: u.rate === null ? 400 : 600,
        }}
        title={utilizationLabel(u)}
      >
        {u.rate === null ? "n/a" : utilizationRateLabel(u)}
      </div>
    </div>
  );
}

/** Summary tile — sibling of ClientAccruals' AccrualTile, kept local on the
 *  same principle. */
function FigureTile({ label, value, subline }: { label: string; value: string; subline?: string }) {
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
      <div
        style={{
          fontFamily: fonts.semiCondensed,
          fontSize: 9.5,
          letterSpacing: ".14em",
          textTransform: "uppercase",
          color: colors.textLabel,
        }}
      >
        {label}
      </div>
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
        {value}
      </div>
      <div style={{ fontFamily: fonts.mono, fontSize: 10, color: colors.textDim, marginTop: 3, minHeight: 13 }}>
        {subline ?? " "}
      </div>
    </div>
  );
}

/** One row of the "not counted" panel — a chip carrying the disposition in
 *  words, then the leg and why it was left out. */
function ExcludedRow({ kind, label, trip, why }: {
  kind: "off" | "soon" | "info";
  label: string;
  trip: TripRecord;
  why: string;
}) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap", marginBottom: 7 }}>
      <StatusChip kind={kind} label={label} />
      <span style={cellStyle}>{shortDateLabel(trip.serviceDate)}</span>
      <span style={cellStyle}>{trip.tripNumber}</span>
      <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted }}>{corridorLabel(trip)}</span>
      <span style={{ fontFamily: fonts.body, fontSize: 11.5, fontStyle: "italic", color: colors.textDim }}>
        {why}
      </span>
    </div>
  );
}

// ---------------------------------------------------------------------------

export default function TerminusSummary({
  stopId,
  setStopId,
  period,
  setPeriod,
  tabs,
}: {
  stopId: string | null;
  setStopId: (id: string | null) => void;
  period: Period;
  setPeriod: (p: Period) => void;
  tabs: ReactNode;
}) {
  // Stop catalog — the terminus picker AND the far-end lookup the derivation
  // needs, so it is fetched whether or not anything is selected yet.
  const [stops, setStops] = useState<StopRecord[] | null>(null);
  const [rosterError, setRosterError] = useState<string | null>(null);
  const [rosterAttempt, setRosterAttempt] = useState(0);

  useEffect(() => {
    let active = true;
    listStops().then(
      (rows) => {
        if (active) setStops(rows);
      },
      (e) => {
        if (active) setRosterError(e instanceof ApiError ? e.message : "Failed to load the stop catalog.");
      },
    );
    return () => {
      active = false;
    };
  }, [rosterAttempt]);

  const termini = useMemo(() => (stops ? terminusStops(stops) : []), [stops]);
  const selected = stops?.find((s) => s.id === stopId) ?? null;

  // A stop that has since been deactivated or reclassified stays in the list
  // while it is selected, so a selection never silently vanishes out from under
  // the report already on screen.
  const pickable = useMemo(
    () => (selected && !termini.some((t) => t.id === selected.id) ? [selected, ...termini] : termini),
    [selected, termini],
  );

  // Period data, keyed by the PERIOD ALONE — not the terminus. The terminus is
  // applied client-side by buildTerminusReport, and the actual workflow is
  // comparing venues against the same month, so keying on the stop would re-pull
  // a whole period on every flip. A selected stop still gates the fetch, so
  // landing here with nothing chosen issues no request.
  const dataKey = stopId ? `${period.start}|${period.end}` : null;
  const [data, setData] = useState<{ key: string; today: string; trips: TripRecord[] } | null>(null);
  const [dataError, setDataError] = useState<{ key: string; message: string } | null>(null);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    if (!dataKey) return;
    let active = true;
    const [from, to] = dataKey.split("|");
    // Unpaged: listTrips returns the complete match. Never paginate here — a
    // truncated set would silently shorten a report handed to a counterparty.
    // This is the console's first client-side scan of a whole period across all
    // clients; if trip volume grows it is the natural first candidate for a
    // backend aggregate endpoint. Not pre-solved, and not invented here.
    // Cancelled trips are deliberately NOT filtered out — they are listed under
    // "not counted" so the period reconciles.
    listTrips({ from, to }).then(
      (page) => {
        if (!active) return;
        setData({ key: dataKey, today: todayIso(), trips: page.items });
      },
      (e) => {
        if (active)
          setDataError({
            key: dataKey,
            message: e instanceof ApiError ? e.message : "Failed to load trips for the period.",
          });
      },
    );
    return () => {
      active = false;
    };
  }, [dataKey, attempt]);

  const loaded = data !== null && data.key === dataKey ? data : null;
  const loadErr = dataError !== null && dataError.key === dataKey ? dataError.message : null;

  // terminusRefFor is called INSIDE the memo: it returns a fresh object every
  // call, so depending on its result would rebuild the whole report each render.
  const report: TerminusReport | null = useMemo(
    () =>
      loaded && selected && stops
        ? buildTerminusReport({
            terminus: terminusRefFor(selected),
            period,
            today: loaded.today,
            trips: loaded.trips,
            stops,
          })
        : null,
    [loaded, selected, stops, period],
  );

  function retry() {
    setDataError(null);
    setAttempt((a) => a + 1);
  }

  const [copyState, setCopyState] = useState<"idle" | "ok" | "fail">("idle");
  async function handleCopy() {
    if (!report) return;
    const ok = await copyToClipboard(terminusClipboardText(report));
    setCopyState(ok ? "ok" : "fail");
    setTimeout(() => setCopyState("idle"), ok ? 2000 : 4500);
  }

  const noTerminiClassified = stops !== null && termini.length === 0;

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 6px" }}>
        <PageHeader
          eyebrow="Business · Terminus distance & seat utilization"
          title="Reports"
          right={
            <div style={{ display: "flex", gap: 10 }}>
              <ActionButton onClick={handleCopy} disabled={!report}>
                {copyState === "ok" ? "COPIED ✓" : copyState === "fail" ? "COPY FAILED" : "COPY REPORT"}
              </ActionButton>
              <ActionButton
                variant="primary"
                onClick={() => report && printTerminusSummary(report)}
                disabled={!report}
              >
                PRINT TERMINUS SUMMARY
              </ActionButton>
            </div>
          }
        />
        {tabs}
      </div>

      <div style={{ flex: 1, minHeight: 0, overflowY: "auto", padding: "4px 26px 26px" }}>
        {/* controls — terminus picker + period stepper (month or quarter) */}
        <Panel style={{ marginTop: 14 }}>
          <div style={{ display: "flex", gap: 20, flexWrap: "wrap", alignItems: "flex-end" }}>
            <div style={{ flex: "1 1 300px", maxWidth: 460 }}>
              <SelectField
                label={stops === null && !rosterError ? "Terminus (loading…)" : "Terminus"}
                value={stopId ?? ""}
                onChange={(v) => setStopId(v || null)}
                disabled={stops === null || pickable.length === 0}
                options={[
                  {
                    value: "",
                    label:
                      stops === null
                        ? "Loading stops…"
                        : termini.length === 0
                          ? "No terminus venues classified yet"
                          : "Select a terminus…",
                  },
                  ...pickable.map((s) => ({
                    value: s.id,
                    label: `${s.name} — ${s.city}, ${s.province}`,
                  })),
                ]}
              />
            </div>
            <div style={{ paddingBottom: 2 }}>
              <PeriodNav period={period} onChange={setPeriod} />
            </div>
          </div>

          {rosterError && (
            <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap", marginTop: 12 }}>
              <StatusChip kind="over" label={`Stop catalog unavailable — ${rosterError}`} />
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

          {selected && (
            <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap", marginTop: 12 }}>
              <StatusChip kind="info" label={stopTypeLabel(selected.stopType)} />
              <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textMuted }}>
                {selected.city}, {selected.province}
              </span>
              {!selected.active && <MonoTag>INACTIVE STOP</MonoTag>}
            </div>
          )}
        </Panel>

        {/* nothing classified as a terminus yet — the picker would otherwise
            just look broken */}
        {noTerminiClassified && (
          <Panel borderColor="rgba(225,176,0,.45)" style={{ marginTop: 14 }}>
            <div style={{ display: "flex", alignItems: "flex-start", gap: 9 }}>
              <StatusBadge kind="soon" />
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6 }}>
                No stop is classified as a <b>Terminus</b> yet, so there is nothing to report on. A
                terminus is a venue Northern Link has a standing business relationship with — the
                Best Western in Thompson, the Lynn Inn in Lynn Lake, Leaf Rapids Town Hall. Open the
                Stops screen and set the stop type to Terminus on each of them; a place you merely
                call at, like an airport used as a stand-in origin, should stay as it is.
              </div>
            </div>
          </Panel>
        )}

        {/* no selection yet */}
        {!stopId && !noTerminiClassified && (
          <Panel style={{ marginTop: 14 }}>
            <SectionLabel>Service by terminus venue</SectionLabel>
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6 }}>
              Pick a terminus to build its summary for the period shown: every leg that began or
              ended at that venue, broken down by the far end of each corridor, with distance and
              seat utilization. Legs that served the same corridor from a different endpoint are not
              represented; cancelled, not-yet-run and pass-through legs are listed but counted in
              nothing.
            </div>
          </Panel>
        )}

        {/* fetch failed */}
        {stopId && loadErr && (
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
        {stopId && !loadErr && !report && (
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
              Loading every trip in the period from API…
            </div>
          </div>
        )}

        {report && (
          <>
            {/* the report's own notes — each explains a class of leg left out */}
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

            {/* summary tiles */}
            <div style={{ display: "flex", gap: 10, flexWrap: "wrap", marginTop: 14 }}>
              <FigureTile
                label="Operated legs"
                value={String(report.operatedLegs)}
                subline={flowLabel(report)}
              />
              <FigureTile
                label="Distance"
                value={distanceLabel(report.distance)}
                subline={averageKmLabel(report.distance)}
              />
              <FigureTile
                label="Corridors"
                value={String(report.corridors.length)}
                subline={`${report.arrivals} arriving · ${report.departures} departing`}
              />
              <div
                style={{
                  flex: "1 1 220px",
                  padding: "11px 14px",
                  background: colors.cardBg,
                  border: `1px solid ${colors.border}`,
                  borderRadius: 11,
                  boxShadow: colors.shadowCard,
                }}
              >
                <StatusChip {...utilizationChip(report.utilization)} />
                <div
                  style={{
                    fontFamily: fonts.mono,
                    fontSize: 11,
                    color: colors.textDim,
                    marginTop: 9,
                    lineHeight: 1.5,
                  }}
                >
                  {utilizationLabel(report.utilization)}
                </div>
              </div>
            </div>

            {/* empty period still renders — and still prints */}
            {report.corridors.length === 0 && (
              <Panel style={{ marginTop: 14 }}>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted }}>
                  No leg began or ended at {report.terminus.name} in {periodLabel(report.period)}. The
                  report can still be printed as an explicit nil statement.
                </div>
              </Panel>
            )}

            {/* by corridor */}
            {report.corridors.length > 0 && (
              <div style={{ marginTop: 18 }}>
                <SectionLabel>By corridor — where the traffic went</SectionLabel>
                <div
                  style={{
                    border: `1px solid ${colors.borderSubtle}`,
                    borderRadius: 9,
                    overflow: "hidden",
                    background: colors.cardBg,
                  }}
                >
                  <div style={headerRowStyle(GRID_COLS)}>
                    <div>Corridor (far end)</div>
                    <div style={{ textAlign: "right" }}>Legs</div>
                    <div style={{ textAlign: "right" }}>Flow</div>
                    <div style={{ textAlign: "right" }}>Distance</div>
                    <div style={{ textAlign: "right" }}>Avg per leg</div>
                    <div style={{ textAlign: "right" }}>Seats</div>
                  </div>
                  {report.corridors.map((c, i) => (
                    <CorridorRow key={c.key} corridor={c} last={i === report.corridors.length - 1} />
                  ))}
                </div>
              </div>
            )}

            {/* not counted — listed so the period reconciles */}
            {(report.passedThrough.length > 0 ||
              report.upcoming.length > 0 ||
              report.cancelled.length > 0) && (
              <Panel style={{ marginTop: 18 }}>
                <SectionLabel>Not counted — listed so the period reconciles</SectionLabel>
                {report.passedThrough.map((t) => (
                  <ExcludedRow
                    key={t.id}
                    kind="info"
                    label="Passed through"
                    trip={t}
                    why="called here but neither began nor ended the leg"
                  />
                ))}
                {report.upcoming.map((t) => (
                  <ExcludedRow
                    key={t.id}
                    kind="soon"
                    label="Not yet run"
                    trip={t}
                    why={`scheduled after ${report.today}`}
                  />
                ))}
                {report.cancelled.map((t) => (
                  <ExcludedRow
                    key={t.id}
                    kind="off"
                    label="Cancelled"
                    trip={t}
                    why={t.cancelledReason ?? "no reason recorded"}
                  />
                ))}
              </Panel>
            )}
          </>
        )}
      </div>
    </div>
  );
}
