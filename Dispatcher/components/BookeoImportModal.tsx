"use client";

import { useEffect, useRef, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { listRoutes, type RouteRecord } from "@/lib/api/trips";
import { listVehicles, type Vehicle } from "@/lib/api/fleet";
import {
  bookeoErrorKind,
  bookeoErrorMessage,
  commitBookeoImport,
  previewBookeoImport,
  saveBookeoProductMappings,
  saveBookeoUnitMappings,
  type BookeoImportCommitResult,
  type BookeoImportPreview,
  type BookeoProductMappingInput,
  type BookeoUnitMappingInput,
} from "@/lib/api/bookeoImport";
import { confirmState, summaryChanges } from "@/lib/bookeoImport";
import { ModalShell, ModalError } from "@/components/ui/ModalShell";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";
import { DetailRow, Panel, SectionLabel } from "@/components/ui/Panel";
import { SummaryBar, UnmappedProductsPanel, UnmatchedUnitsPanel } from "@/components/bookeoImport/MappingPanels";
import { GroupsTable } from "@/components/bookeoImport/GroupsTable";
import { MappingsTab } from "@/components/bookeoImport/MappingsTab";
import { HistoryTab } from "@/components/bookeoImport/HistoryTab";
import { Notice, muted } from "@/components/bookeoImport/shared";

// Import from Bookeo (Trips → IMPORT FROM BOOKEO). Bookeo is the system of
// record for community shuttle bookings; this turns its booking report into
// Community trips + passenger manifests.
//
// Preview → confirm, modelled on GenerateTripsModal: the server parses the
// spreadsheet and returns the full plan; nothing is written until Confirm,
// which sends back the plan's hash. If the database moved in between (409
// PreviewStale) the preview re-runs on its own and the dispatcher is told what
// changed. The picked File is kept in state so saving a mapping can re-run the
// preview without the dispatcher choosing the spreadsheet again.
//
// Money columns are Bookeo's gross (tax-inclusive), paid and due. The platform
// never computes or shows tax.

type Tab = "import" | "mappings" | "history";

const TABS: { id: Tab; label: string }[] = [
  { id: "import", label: "Import" },
  { id: "mappings", label: "Mappings" },
  { id: "history", label: "History" },
];

const ACCEPT =
  ".xls,.xlsx,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

type NoticeState = { kind: "ontime" | "soon" | "over" | "off"; text: string; details?: string[] };

export default function BookeoImportModal({ onClose }: { onClose: (committed: boolean) => void }) {
  const [tab, setTab] = useState<Tab>("import");
  const [routes, setRoutes] = useState<RouteRecord[]>([]);
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);

  const fileRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<BookeoImportPreview | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [committing, setCommitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<NoticeState | null>(null);
  const [result, setResult] = useState<BookeoImportCommitResult | null>(null);
  const [unitsSkipped, setUnitsSkipped] = useState(false);
  const [historyKey, setHistoryKey] = useState(0);
  // Only the latest preview request may land — a slow earlier one must not
  // overwrite the plan the dispatcher is now looking at.
  const seq = useRef(0);

  useEffect(() => {
    let active = true;
    // Pickers degrade to "No routes loaded" rather than failing the dialog.
    listRoutes().then(
      (r) => {
        if (active) setRoutes(r);
      },
      () => undefined,
    );
    listVehicles().then(
      (v) => {
        if (active) setVehicles(v);
      },
      () => undefined,
    );
    return () => {
      active = false;
    };
  }, []);

  async function runPreview(f: File): Promise<BookeoImportPreview | null> {
    const mine = ++seq.current;
    setPreviewing(true);
    setError(null);
    try {
      const p = await previewBookeoImport(f);
      if (mine !== seq.current) return null;
      setPreview(p);
      setHistoryKey((k) => k + 1); // every upload is a batch
      return p;
    } catch (e) {
      if (mine === seq.current) {
        setPreview(null);
        setError(bookeoErrorMessage(e));
      }
      return null;
    } finally {
      if (mine === seq.current) setPreviewing(false);
    }
  }

  function onPick(e: React.ChangeEvent<HTMLInputElement>) {
    const f = e.target.files?.[0];
    e.target.value = ""; // allow re-selecting the same file
    if (!f) return;
    setFile(f);
    setPreview(null);
    setResult(null);
    setNotice(null);
    setUnitsSkipped(false);
    void runPreview(f);
  }

  async function saveProducts(mappings: BookeoProductMappingInput[]) {
    if (!file) return;
    setSaving(true);
    setError(null);
    try {
      await saveBookeoProductMappings(mappings);
    } catch (e) {
      setError(bookeoErrorMessage(e));
      setSaving(false);
      return;
    }
    setSaving(false);
    if (await runPreview(file)) {
      setNotice({
        kind: "ontime",
        text: `Saved ${mappings.length} product mapping${mappings.length === 1 ? "" : "s"} — preview refreshed.`,
      });
    }
  }

  async function saveUnits(mappings: BookeoUnitMappingInput[]) {
    if (!file) return;
    setSaving(true);
    setError(null);
    try {
      await saveBookeoUnitMappings(mappings);
    } catch (e) {
      setError(bookeoErrorMessage(e));
      setSaving(false);
      return;
    }
    setSaving(false);
    if (await runPreview(file)) {
      setNotice({
        kind: "ontime",
        text: `Saved ${mappings.length} unit mapping${mappings.length === 1 ? "" : "s"} — preview refreshed.`,
      });
    }
  }

  const confirm = confirmState(preview, previewing || saving || committing);

  async function commit() {
    if (!preview || confirm.disabled) return;
    setCommitting(true);
    setError(null);
    setNotice(null);
    try {
      const r = await commitBookeoImport(preview.batchId, preview.planHash);
      setResult(r);
      setHistoryKey((k) => k + 1);
    } catch (e) {
      const kind = bookeoErrorKind(e);
      if (kind === "PreviewStale" && file) {
        const before = preview.summary;
        const fresh = await runPreview(file);
        if (fresh) {
          const changes = summaryChanges(before, fresh.summary);
          setNotice({
            kind: "soon",
            text: `${bookeoErrorMessage(e)} The preview has been refreshed — review it and confirm again.`,
            details: changes.length > 0 ? changes : ["The counts are the same, but individual bookings or trips changed."],
          });
        }
      } else if (kind === "AlreadyCommitted") {
        setNotice({ kind: "off", text: bookeoErrorMessage(e) });
        setHistoryKey((k) => k + 1);
        setTab("history");
      } else {
        setError(bookeoErrorMessage(e));
      }
    } finally {
      setCommitting(false);
    }
  }

  const close = () => onClose(result !== null);

  const footer =
    tab !== "import" || result !== null ? (
      <ActionButton variant="primary" onClick={close}>
        CLOSE
      </ActionButton>
    ) : (
      <>
        {preview && confirm.reason && (
          <span style={{ marginRight: "auto", fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
            {confirm.reason}
          </span>
        )}
        <ActionButton onClick={close}>CANCEL</ActionButton>
        <ActionButton variant="primary" onClick={commit} disabled={confirm.disabled || preview === null}>
          {committing ? "APPLYING…" : confirm.label.toUpperCase()}
        </ActionButton>
      </>
    );

  return (
    <ModalShell eyebrow="Operations · Trips" title="Import from Bookeo" onClose={close} maxWidth={1120} footer={footer}>
      <div style={{ display: "flex", gap: 2, borderBottom: `1px solid ${colors.border}`, marginBottom: 16 }}>
        {TABS.map((t) => (
          <span
            key={t.id}
            role="tab"
            aria-selected={tab === t.id}
            onClick={() => setTab(t.id)}
            style={{
              fontFamily: fonts.body,
              fontWeight: tab === t.id ? 600 : 500,
              fontSize: 13,
              padding: "9px 16px",
              color: tab === t.id ? colors.headingBright : colors.textDim,
              borderBottom: tab === t.id ? `2px solid ${colors.blue}` : undefined,
              marginBottom: -1,
              cursor: "pointer",
            }}
          >
            {t.label}
          </span>
        ))}
      </div>

      {notice && (tab === "import" || tab === "history") && (
        <div style={{ marginBottom: 14 }}>
          <Notice kind={notice.kind} details={notice.details}>
            {notice.text}
          </Notice>
        </div>
      )}

      {tab === "mappings" && <MappingsTab routes={routes} vehicles={vehicles} />}
      {tab === "history" && <HistoryTab refreshKey={historyKey} />}

      {tab === "import" && (
        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          {error && <ModalError message={error} />}

          {result !== null ? (
            <ResultPanel result={result} />
          ) : (
            <>
              <Panel style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
                <input
                  ref={fileRef}
                  type="file"
                  accept={ACCEPT}
                  data-testid="bookeo-file"
                  onChange={onPick}
                  style={{ display: "none" }}
                />
                <ActionButton
                  variant={file ? "secondary" : "primary"}
                  onClick={() => fileRef.current?.click()}
                  disabled={previewing || saving || committing}
                >
                  ⭱ {file ? "CHOOSE A DIFFERENT FILE" : "CHOOSE BOOKEO REPORT"}
                </ActionButton>
                {file ? (
                  <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary }}>
                    {file.name}
                    {previewing && <span style={{ color: colors.textDim }}> · building preview…</span>}
                  </span>
                ) : (
                  <span style={muted}>
                    Export the booking report from Bookeo (.xls or .xlsx, up to 5 MB). Nothing is saved until you
                    confirm.
                  </span>
                )}
              </Panel>

              {preview && (
                <>
                  <SummaryBar summary={preview.summary} />

                  {preview.unmappedProducts.length > 0 && (
                    <UnmappedProductsPanel
                      products={preview.unmappedProducts}
                      routes={routes}
                      busy={saving || previewing}
                      onSave={saveProducts}
                    />
                  )}

                  {preview.unmatchedUnits.length > 0 &&
                    (unitsSkipped ? (
                      <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                        <StatusChip
                          kind="soon"
                          label={`${preview.unmatchedUnits.length} unit${preview.unmatchedUnits.length === 1 ? "" : "s"} skipped — those trips stay unassigned`}
                        />
                        <ActionButton onClick={() => setUnitsSkipped(false)}>MAP UNITS</ActionButton>
                      </div>
                    ) : (
                      <UnmatchedUnitsPanel
                        units={preview.unmatchedUnits}
                        vehicles={vehicles}
                        busy={saving || previewing}
                        onSave={saveUnits}
                        onSkip={() => setUnitsSkipped(true)}
                      />
                    ))}

                  <div>
                    <SectionLabel>Trips in this file</SectionLabel>
                    <div style={{ ...muted, marginBottom: 8 }}>
                      Click a trip to see its bookings. Amounts are Bookeo&apos;s gross (tax-inclusive), paid and due.
                    </div>
                    <GroupsTable preview={preview} />
                  </div>
                </>
              )}
            </>
          )}
        </div>
      )}
    </ModalShell>
  );
}

function ResultPanel({ result }: { result: BookeoImportCommitResult }) {
  return (
    <div>
      <SectionLabel>Result</SectionLabel>
      <Panel>
        <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
          <div>
            <StatusChip kind="ontime" label="Import applied" />
          </div>
          <DetailRow label="Trips created" value={String(result.tripsCreated)} valueStyle={{ fontFamily: fonts.mono }} />
          <DetailRow label="Trips updated" value={String(result.tripsUpdated)} valueStyle={{ fontFamily: fonts.mono }} />
          <DetailRow
            label="Trips cancelled"
            value={String(result.tripsCancelled)}
            valueStyle={{ fontFamily: fonts.mono }}
          />
          <DetailRow
            label="Bookings imported"
            value={String(result.bookingsImported)}
            valueStyle={{ fontFamily: fonts.mono }}
          />
          <DetailRow
            label="Bookings cancelled"
            value={String(result.bookingsCancelled)}
            valueStyle={{ fontFamily: fonts.mono }}
          />
          {result.groupsSkippedBlocked > 0 && (
            <div>
              <StatusChip
                kind="over"
                label={`${result.groupsSkippedBlocked} blocked group${result.groupsSkippedBlocked === 1 ? "" : "s"} skipped — fix and re-upload`}
              />
            </div>
          )}
          {result.createdTripNumbers.length > 0 && (
            <div>
              <div style={{ ...muted, marginBottom: 6 }}>New trips</div>
              <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
                {result.createdTripNumbers.map((n) => (
                  <span
                    key={n}
                    style={{
                      fontFamily: fonts.mono,
                      fontSize: 12,
                      padding: "3px 8px",
                      borderRadius: 6,
                      border: `1px solid ${colors.borderStrong}`,
                      color: colors.textPrimary,
                    }}
                  >
                    {n}
                  </span>
                ))}
              </div>
            </div>
          )}
          <span style={muted}>The Trips list refreshes when you close this dialog.</span>
        </div>
      </Panel>
    </div>
  );
}
