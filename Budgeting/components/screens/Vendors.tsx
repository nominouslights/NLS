"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, rowSurface } from "@/lib/theme";
import { StatusChip, MonoTag } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { TextField } from "@/components/ui/Field";
import { ApiError } from "@/lib/api/transport";
import {
  deleteVendor,
  listBudgetOwnerCandidates,
  refetchUntil,
  setVendorActive,
  type BudgetOwnerOption,
  type VendorRecord,
} from "@/lib/api/budgeting";
import { vendorMatchesSearch, vendorStatus } from "@/lib/vendors";
import { ErrorNotice } from "@/components/ErrorNotice";
import VendorFormModal, {
  DEFAULT_VENDOR_FORM_API,
  type VendorFormApi,
} from "@/components/VendorFormModal";
import VendorDetail, { type VendorConfirmKind } from "@/components/screens/vendors/VendorDetail";
import { EmptyNote, Screen } from "@/components/screens/shared";

// The tenant's vendor register, live against /api/budgeting/vendors. Master/detail like Budget
// Codes — a list on the left, the selected vendor's record on the tinted detail pane — but NOT
// period-scoped: a vendor is the same counterparty in every period, so this screen renders with
// or without an entered period (lib/nav.ts PERIOD_SCOPED leaves it out), names no period, and
// takes no period hold.
//
// The whole register is fetched once, retired vendors included (includeInactive=true), and the
// "Show retired" toggle filters client-side. Fetching everything is what lets the create/edit
// modal's duplicate-name check see a retired vendor whose name is still taken.
//
// Retiring is the normal end of a vendor's life (it stays listed; its name stays taken). DELETE
// is for a vendor added in error — the console cannot know whether anything references it, so it
// offers Delete and shows the server's 409 Budgeting.Vendor.InUse verbatim, with a way to retire
// instead. Retire, restore and delete are each two-click; one pending confirm at a time, and any
// selection change cancels it.
//
// Reads trail writes by one projection poll, so every write refetches with refetchUntil and a
// predicate for the change just made.

/** Every request this screen and its modal make. A prop so the component test can inject vi.fn()s. */
export interface VendorsApi extends VendorFormApi {
  setActive: typeof setVendorActive;
  remove: typeof deleteVendor;
  users: typeof listBudgetOwnerCandidates;
}

const DEFAULT_API: VendorsApi = {
  ...DEFAULT_VENDOR_FORM_API,
  setActive: setVendorActive,
  remove: deleteVendor,
  users: listBudgetOwnerCandidates,
};

type PendingConfirm = { kind: VendorConfirmKind; id: string } | null;

type ScreenError = {
  message: string;
  code: string;
  /** "load" offers RETRY; an action's refusal does not. */
  source: "load" | "action";
  /** For an action error, the vendor it was about — an InUse 409 offers to retire that one. */
  vendorId?: string;
};

export default function Vendors({ api = DEFAULT_API }: { api?: VendorsApi }) {
  // null = still loading.
  const [vendors, setVendors] = useState<VendorRecord[] | null>(null);
  const [users, setUsers] = useState<BudgetOwnerOption[]>([]);
  const [error, setError] = useState<ScreenError | null>(null);
  const [selId, setSelId] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [showRetired, setShowRetired] = useState(false);
  const [confirmAction, setConfirmAction] = useState<PendingConfirm>(null);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState<{ vendor: VendorRecord | null } | null>(null);

  const applyLoaded = useCallback((records: VendorRecord[]) => {
    setVendors(records);
    setError(null);
  }, []);

  const applyLoadError = useCallback((e: unknown) => {
    setVendors((prev) => prev ?? []);
    setError(
      e instanceof ApiError
        ? { message: e.message, code: e.code, source: "load" }
        : { message: "Failed to load vendors.", code: "Unknown", source: "load" },
    );
  }, []);

  /** Retry handler — the mount fetch below uses then-callbacks per the Stops.tsx lint idiom. */
  const load = useCallback(() => {
    api.list().then(applyLoaded, applyLoadError);
  }, [api, applyLoaded, applyLoadError]);

  useEffect(() => {
    let active = true;
    api.list().then(
      (records) => {
        if (active) applyLoaded(records);
      },
      (e) => {
        if (active) applyLoadError(e);
      },
    );
    // Names for "Created by" / "Last modified by" (the response carries user ids only). Not worth
    // blocking the screen for: on failure those rows read "Unknown user".
    api.users().then(
      (rows) => {
        if (active) setUsers(rows);
      },
      () => {},
    );
    return () => {
      active = false;
    };
  }, [api, applyLoaded, applyLoadError]);

  const all = vendors ?? [];
  const retiredCount = all.filter((v) => !v.isActive).length;
  const shown = all.filter((v) => (showRetired || v.isActive) && vendorMatchesSearch(v, search));
  // Falls back to the first visible row, so a filter never leaves the pane showing a vendor the
  // list no longer shows.
  const selected = shown.find((v) => v.id === selId) ?? shown[0] ?? null;
  const confirming =
    selected !== null && confirmAction?.id === selected.id ? confirmAction.kind : null;

  function select(id: string) {
    setSelId(id);
    setConfirmAction(null); // a pending confirmation never survives a selection change
  }

  function openForm(vendor: VendorRecord | null) {
    setConfirmAction(null);
    setForm({ vendor });
  }

  function handleSaved(records: VendorRecord[] | null, id: string) {
    setSelId(id);
    // The save landed but its refetch failed: reload rather than show a stale list.
    if (records === null) return load();
    applyLoaded(records);
    const saved = records.find((v) => v.id === id);
    // Keep the saved vendor visible: clear a search that would hide it.
    if (saved && !vendorMatchesSearch(saved, search)) setSearch("");
    setSelId(id);
  }

  /** From the modal's duplicate note: show the vendor that holds the name — and arm a restore when retired. */
  function openExisting(existing: VendorRecord) {
    setForm(null);
    setSearch("");
    if (!existing.isActive) setShowRetired(true);
    setSelId(existing.id);
    setConfirmAction(existing.isActive ? null : { kind: "restore", id: existing.id });
  }

  async function runAction(
    target: VendorRecord,
    action: () => Promise<unknown>,
    settled: (rows: VendorRecord[]) => boolean,
  ) {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      await action();
      applyLoaded(await refetchUntil(api.list, settled));
    } catch (e) {
      setError(
        e instanceof ApiError
          ? { message: e.message, code: e.code, source: "action", vendorId: target.id }
          : {
              message: `Failed to update ${target.name} — please try again.`,
              code: "Unknown",
              source: "action",
              vendorId: target.id,
            },
      );
    } finally {
      setBusy(false);
    }
  }

  function toggleActive(target: VendorRecord) {
    const kind: VendorConfirmKind = target.isActive ? "retire" : "restore";
    if (confirmAction?.kind !== kind || confirmAction.id !== target.id) {
      setConfirmAction({ kind, id: target.id });
      return;
    }
    setConfirmAction(null);
    const active = !target.isActive;
    // Restoring while "Show retired" is off would otherwise be invisible; retiring with it off
    // would make the row vanish. Show retired after a retire so the planner sees what happened.
    if (!active) setShowRetired(true);
    void runAction(
      target,
      () => api.setActive(target.id, active),
      (rows) => rows.some((r) => r.id === target.id && r.isActive === active),
    );
  }

  function remove(target: VendorRecord) {
    if (confirmAction?.kind !== "delete" || confirmAction.id !== target.id) {
      setConfirmAction({ kind: "delete", id: target.id });
      return;
    }
    setConfirmAction(null);
    // A 409 Budgeting.Vendor.InUse lands in the error banner with the server's own words, which
    // name retiring as the alternative — and the banner offers to do exactly that.
    void runAction(
      target,
      () => api.remove(target.id),
      (rows) => !rows.some((r) => r.id === target.id),
    );
  }

  const inUseTarget =
    error?.code === "Budgeting.Vendor.InUse"
      ? (all.find((v) => v.id === error.vendorId && v.isActive) ?? null)
      : null;

  return (
    <Screen
      eyebrow="Planning · Shared by every period"
      title="Vendors"
      right={
        <ActionButton variant="primary" onClick={() => openForm(null)} disabled={busy || vendors === null}>
          + NEW VENDOR
        </ActionButton>
      }
    >
      {error && (
        <div style={{ marginBottom: 12 }}>
          <ErrorNotice title="Vendors" message={error.message} code={error.code} />
          {error.source === "load" && (
            <div style={{ marginTop: 9 }}>
              <ActionButton onClick={load}>RETRY</ActionButton>
            </div>
          )}
          {inUseTarget && (
            <div style={{ marginTop: 9, display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
              <ActionButton
                variant="amber"
                onClick={() => {
                  setError(null);
                  select(inUseTarget.id);
                  setConfirmAction({ kind: "retire", id: inUseTarget.id });
                }}
              >
                RETIRE {inUseTarget.name.toUpperCase()} INSTEAD
              </ActionButton>
              <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
                Retiring keeps it listed and stops it being offered for new work.
              </span>
            </div>
          )}
        </div>
      )}

      {vendors === null && !error && <EmptyNote>Loading vendors…</EmptyNote>}

      {vendors !== null && all.length === 0 && !error && (
        <div style={{ marginBottom: 12 }}>
          <EmptyNote>
            No vendors yet. The register is shared by every period — add the suppliers you pay
            (and anyone who pays you) once, and every period can refer to them.
          </EmptyNote>
        </div>
      )}

      {all.length > 0 && (
        <>
          <div style={{ display: "flex", alignItems: "flex-end", gap: 16, flexWrap: "wrap", marginBottom: 12 }}>
            <div style={{ flex: "1 1 260px", maxWidth: 420 }}>
              <TextField
                label="Search"
                value={search}
                onChange={setSearch}
                placeholder="Name, contact, email, phone or code"
              />
            </div>
            <label
              style={{
                display: "flex",
                alignItems: "center",
                gap: 7,
                height: 40,
                fontFamily: fonts.body,
                fontSize: 12.5,
                color: colors.textSecondary,
                cursor: "pointer",
              }}
            >
              <input
                type="checkbox"
                checked={showRetired}
                onChange={(e) => {
                  setShowRetired(e.target.checked);
                  setConfirmAction(null);
                }}
              />
              Show retired ({retiredCount})
            </label>
          </div>

          {shown.length === 0 ? (
            <EmptyNote>
              {search.trim()
                ? `No ${showRetired ? "" : "active "}vendor matches “${search.trim()}”.`
                : "Every vendor is retired — tick Show retired to see them."}
            </EmptyNote>
          ) : (
            <div
              style={{
                display: "grid",
                gridTemplateColumns: "38% 1fr",
                borderTop: `1px solid ${colors.border}`,
                minHeight: 0,
              }}
            >
              <div
                role="list"
                aria-label="Vendors"
                style={{
                  overflowY: "auto",
                  padding: "16px 18px 16px 0",
                  borderRight: `1px solid ${colors.border}`,
                  display: "flex",
                  flexDirection: "column",
                  gap: 8,
                }}
              >
                {shown.map((v) => {
                  const status = vendorStatus(v.isActive);
                  return (
                    <div
                      key={v.id}
                      role="listitem"
                      onClick={() => select(v.id)}
                      style={{ ...rowSurface(selected?.id === v.id), padding: "11px 13px" }}
                    >
                      <div style={{ display: "flex", alignItems: "center", gap: 9, marginBottom: 4 }}>
                        <div
                          style={{
                            fontFamily: fonts.body,
                            fontWeight: 600,
                            fontSize: 12.5,
                            color: colors.textPrimary,
                            flex: "1 1 auto",
                            minWidth: 0,
                          }}
                        >
                          {v.name}
                        </div>
                        <StatusChip kind={status.kind} label={status.label} />
                      </div>
                      <div
                        style={{
                          display: "flex",
                          alignItems: "center",
                          gap: 8,
                          fontFamily: fonts.body,
                          fontSize: 11,
                          color: colors.textDim,
                        }}
                      >
                        {v.defaultBudgetCode && <MonoTag>{v.defaultBudgetCode}</MonoTag>}
                        <span>{[v.contactName, v.phone ?? v.email].filter(Boolean).join(" · ") || "No contact on file"}</span>
                      </div>
                    </div>
                  );
                })}
              </div>

              <div style={{ overflowY: "auto", padding: "22px 0 22px 26px", background: colors.detailBg }}>
                {selected ? (
                  <VendorDetail
                    vendor={selected}
                    users={users}
                    busy={busy}
                    confirming={confirming}
                    onEdit={() => openForm(selected)}
                    onToggleActive={() => toggleActive(selected)}
                    onDelete={() => remove(selected)}
                  />
                ) : (
                  <EmptyNote>No vendor selected.</EmptyNote>
                )}
              </div>
            </div>
          )}
        </>
      )}

      {form && (
        <VendorFormModal
          vendor={form.vendor}
          vendors={all}
          onClose={() => setForm(null)}
          onSaved={handleSaved}
          onOpenExisting={openExisting}
          api={api}
        />
      )}
    </Screen>
  );
}
