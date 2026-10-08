"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import {
  createVendor,
  listVendors,
  refetchUntil,
  updateVendor,
  type VendorRecord,
} from "@/lib/api/budgeting";
import {
  BLANK_VENDOR_DRAFT,
  VENDOR_LIMITS,
  draftToVendorInput,
  duplicateVendorMessage,
  findDuplicateVendor,
  vendorError,
  vendorReflects,
  vendorToDraft,
  type VendorDraft,
} from "@/lib/vendors";
import { ModalShell } from "@/components/ui/ModalShell";
import { TextAreaField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";

// Create-and-edit modal for one vendor in the tenant's register, following BudgetCodeFormModal:
// the server's validation is authoritative, and every 400/409 shows as the backend's own message
// in the modal's banner.
//
// Three things this form has to get right, because the server enforces them and none is
// guessable from the UI:
//
//   1. **PUT is a full replace.** An omitted optional field is cleared server-side, so the body is
//      always every key (draftToVendorInput), null when blank — an edit can never silently clear
//      a field it did not render.
//   2. **Names are unique per tenant, ignoring case — retired vendors included.** The modal runs
//      the server's rule (findDuplicateVendor ↔ VendorNameRule) live against the register it was
//      given, says which vendor holds the name in the server's own words, and offers to open it —
//      or, when that vendor is retired, to restore it, which is the server's own advice. The
//      server's 409 is still shown verbatim if the register was stale.
//   3. **No tax.** The GST registration number is reference data; its hint says so.
//
// Vendors are tenant-wide, so unlike the code and item modals this one names no period and takes
// no period hold.

/** The three requests the modal makes. A prop so the component test can inject vi.fn()s. */
export interface VendorFormApi {
  create: typeof createVendor;
  update: typeof updateVendor;
  /** Re-reads the whole register (retired included) after a save. */
  list: () => Promise<VendorRecord[]>;
}

export const DEFAULT_VENDOR_FORM_API: VendorFormApi = {
  create: createVendor,
  update: updateVendor,
  list: () => listVendors(true),
};

export default function VendorFormModal({
  vendor,
  vendors,
  onClose,
  onSaved,
  onOpenExisting,
  api = DEFAULT_VENDOR_FORM_API,
}: {
  /** null → create; a vendor → edit it. */
  vendor: VendorRecord | null;
  /** The whole register, retired included, for the duplicate-name check. */
  vendors: readonly VendorRecord[];
  onClose: () => void;
  /** Fresh register (already reflecting the change) plus the saved vendor's id. */
  onSaved: (records: VendorRecord[], id: string) => void;
  /** The duplicate note's link: open that vendor instead (the screen arms a restore if retired). */
  onOpenExisting: (existing: VendorRecord) => void;
  api?: VendorFormApi;
}) {
  const editing = vendor !== null;
  const [draft, setDraft] = useState<VendorDraft>(() =>
    vendor ? vendorToDraft(vendor) : BLANK_VENDOR_DRAFT,
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const set = (key: keyof VendorDraft) => (value: string) =>
    setDraft((d) => ({ ...d, [key]: value }));

  /** Inert while saving, so a result can never land on a modal that is gone. */
  const close = () => {
    if (!busy) onClose();
  };

  const duplicate = findDuplicateVendor(vendors, draft.name, vendor?.id ?? null);

  async function submit() {
    if (busy) return;
    const input = draftToVendorInput(draft);

    // The server's field rules first, in its order — then its name rule, as the handlers do.
    const invalid = vendorError(input);
    if (invalid) return setError(invalid);
    if (duplicate) return setError(duplicateVendorMessage(duplicate));

    setBusy(true);
    setError(null);
    try {
      let id: string;
      if (editing) {
        id = vendor.id;
        await api.update(id, input);
      } else {
        id = await api.create(input);
      }

      // Reads trail writes by one projection poll. Wait for the row to carry every value just
      // written — on edit the row was always there, so the id alone proves nothing.
      const records = await refetchUntil(api.list, (rows) =>
        rows.some((r) => r.id === id && vendorReflects(r, input)),
      );
      onSaved(records, id);
      onClose();
    } catch (e) {
      setError(
        e instanceof ApiError
          ? e.message
          : `Failed to ${editing ? "save the" : "create the"} vendor — please try again.`,
      );
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow="Vendors · Shared by every period"
      title={editing ? `Edit ${vendor.name}` : "New Vendor"}
      onClose={close}
      error={error}
      maxWidth={680}
      footer={
        <>
          <ActionButton onClick={close} disabled={busy}>
            CANCEL
          </ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "SAVING…" : editing ? "SAVE CHANGES" : "CREATE VENDOR"}
          </ActionButton>
        </>
      }
    >
      <Row>
        <TextField
          label="Name"
          value={draft.name}
          onChange={set("name")}
          maxLength={VENDOR_LIMITS.name}
          placeholder="Kal Tire Thompson"
          hint="Required — unique, ignoring case"
        />
        <TextField
          label="QuickBooks display name"
          value={draft.qboDisplayName}
          onChange={set("qboDisplayName")}
          maxLength={VENDOR_LIMITS.qboDisplayName}
          placeholder="Kal Tire #412"
          hint="Used later to match imported QuickBooks expenses"
        />
      </Row>

      {duplicate && (
        <div
          role="status"
          style={{
            marginTop: 10,
            display: "flex",
            alignItems: "center",
            gap: 10,
            flexWrap: "wrap",
            fontFamily: fonts.body,
            fontSize: 11.5,
            color: colors.textSecondary,
            lineHeight: 1.5,
          }}
        >
          <StatusChip kind="soon" label="Name taken" />
          <span style={{ flex: "1 1 260px" }}>{duplicateVendorMessage(duplicate)}</span>
          <ActionButton
            onClick={() => {
              if (!busy) onOpenExisting(duplicate);
            }}
            disabled={busy}
            style={{ padding: "5px 11px", fontSize: 12 }}
          >
            {duplicate.isActive ? `OPEN ${duplicate.name.toUpperCase()}` : `RESTORE ${duplicate.name.toUpperCase()}`}
          </ActionButton>
        </div>
      )}

      <Row top>
        <TextField
          label="Contact name"
          value={draft.contactName}
          onChange={set("contactName")}
          maxLength={VENDOR_LIMITS.contactName}
          placeholder="Dana Okemow"
          hint="Optional"
        />
        <TextField
          label="Phone"
          value={draft.phone}
          onChange={set("phone")}
          maxLength={VENDOR_LIMITS.phone}
          placeholder="204-555-0142"
          hint="Optional"
        />
      </Row>

      <Row top>
        <TextField
          label="Email"
          type="email"
          value={draft.email}
          onChange={set("email")}
          maxLength={VENDOR_LIMITS.email}
          placeholder="orders@example.com"
          hint="Optional"
        />
        <TextField
          label="Default budget code"
          value={draft.defaultBudgetCode}
          onChange={set("defaultBudgetCode")}
          mono
          maxLength={VENDOR_LIMITS.defaultBudgetCode}
          placeholder="FLEET-MAINT"
          hint="Pre-fills new items — need not exist in every period"
        />
      </Row>

      <Row top>
        <TextField
          label="GST registration number"
          value={draft.gstRegistrationNumber}
          onChange={set("gstRegistrationNumber")}
          mono
          maxLength={VENDOR_LIMITS.gstRegistrationNumber}
          placeholder="123456789 RT0001"
          hint="Reference only — the platform never calculates tax"
        />
        <div />
      </Row>

      <div style={{ marginTop: 14 }}>
        <TextAreaField
          label="Address"
          value={draft.address}
          onChange={set("address")}
          rows={2}
          placeholder="Street, town, province, postal code"
          hint={`Optional · up to ${VENDOR_LIMITS.address} characters`}
        />
      </div>

      <div style={{ marginTop: 14 }}>
        <TextAreaField
          label="Notes"
          value={draft.notes}
          onChange={set("notes")}
          rows={3}
          placeholder="Account numbers, ordering habits, who to call after hours."
          hint={`Optional · up to ${VENDOR_LIMITS.notes} characters`}
        />
      </div>
    </ModalShell>
  );
}

/** The modal's two-column row, matching BudgetCodeFormModal's grid. */
function Row({ children, top = false }: { children: React.ReactNode; top?: boolean }) {
  return (
    <div
      style={{
        display: "grid",
        gridTemplateColumns: "1fr 1fr",
        gap: 14,
        marginTop: top ? 14 : 0,
      }}
    >
      {children}
    </div>
  );
}
