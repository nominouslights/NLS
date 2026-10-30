"use client";

import { colors, fonts } from "@/lib/theme";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { DetailRow, Panel, SectionLabel } from "@/components/ui/Panel";
import { formatUtcDate } from "@/lib/api/format";
import { userDisplay, type BudgetOwnerOption, type VendorRecord } from "@/lib/api/budgeting";
import { vendorStatus } from "@/lib/vendors";

// The right-hand pane of the Vendors screen: one vendor's record, and its four actions. The
// actions' two-click state lives in the screen (one pending confirm at a time, cancelled by any
// selection change, as on Budget Codes); this component only renders it.

export type VendorConfirmKind = "retire" | "restore" | "delete";

export default function VendorDetail({
  vendor,
  users,
  busy,
  confirming,
  onEdit,
  onToggleActive,
  onDelete,
}: {
  vendor: VendorRecord;
  /** The tenant's users (codes/owners), to name createdBy / modifiedBy — the response has ids only. */
  users: readonly BudgetOwnerOption[];
  busy: boolean;
  /** The pending confirm on THIS vendor, if any. */
  confirming: VendorConfirmKind | null;
  onEdit: () => void;
  onToggleActive: () => void;
  onDelete: () => void;
}) {
  const status = vendorStatus(vendor.isActive);

  return (
    <>
      <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 14, flexWrap: "wrap" }}>
        <div
          style={{
            fontFamily: fonts.condensed,
            fontWeight: 700,
            fontSize: 22,
            color: colors.headingBright,
            flex: "1 1 auto",
            minWidth: 0,
          }}
        >
          {vendor.name}
        </div>
        <ActionButton onClick={onEdit} disabled={busy}>
          EDIT
        </ActionButton>
        <ActionButton variant={vendor.isActive ? "amber" : "success"} onClick={onToggleActive} disabled={busy}>
          {busy
            ? "WORKING…"
            : vendor.isActive
              ? confirming === "retire"
                ? "CONFIRM RETIRE"
                : "RETIRE"
              : confirming === "restore"
                ? "CONFIRM RESTORE"
                : "RESTORE"}
        </ActionButton>
        <ActionButton variant="destructive" onClick={onDelete} disabled={busy}>
          {confirming === "delete" ? "CONFIRM DELETE" : "DELETE"}
        </ActionButton>
      </div>

      {confirming === "retire" && (
        <ConfirmNote>
          Retiring {vendor.name} keeps it listed — anything that already names it still resolves — but
          it stops being offered for new work, in every period. Click CONFIRM RETIRE to proceed;
          selecting another vendor cancels.
        </ConfirmNote>
      )}
      {confirming === "restore" && (
        <ConfirmNote>
          Restoring {vendor.name} makes it available for new work again, in every period. Click
          CONFIRM RESTORE to proceed; selecting another vendor cancels.
        </ConfirmNote>
      )}
      {confirming === "delete" && (
        <ConfirmNote>
          Deleting {vendor.name} is permanent and is only for a vendor added in error. If anything
          already refers to it the server refuses, and retiring is the way to go. Click CONFIRM
          DELETE to proceed; anything else cancels.
        </ConfirmNote>
      )}

      <Panel style={{ marginBottom: 12 }}>
        <SectionLabel>Contact</SectionLabel>
        <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
          <DetailRow label="Contact name" value={vendor.contactName ?? "—"} />
          <DetailRow label="Email" value={vendor.email ?? "—"} />
          <DetailRow label="Phone" value={vendor.phone ?? "—"} />
          <DetailRow label="Address" value={vendor.address ?? "—"} />
        </div>
      </Panel>

      <Panel style={{ marginBottom: 12 }}>
        <SectionLabel>Accounting</SectionLabel>
        <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
          <DetailRow label="QuickBooks display name" value={vendor.qboDisplayName ?? "—"} />
          <DetailRow label="Default budget code" value={vendor.defaultBudgetCode ?? "—"} />
          <DetailRow label="GST registration number" value={vendor.gstRegistrationNumber ?? "—"} />
        </div>
        <Note>
          The GST registration number is reference only — the platform never calculates tax;
          QuickBooks does.
        </Note>
      </Panel>

      {vendor.notes && (
        <Panel style={{ marginBottom: 12 }}>
          <SectionLabel>Notes</SectionLabel>
          <div
            style={{
              fontFamily: fonts.body,
              fontSize: 12.5,
              color: colors.textSecondary,
              lineHeight: 1.65,
              whiteSpace: "pre-wrap",
            }}
          >
            {vendor.notes}
          </div>
        </Panel>
      )}

      <Panel>
        <SectionLabel>Record</SectionLabel>
        <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
          <DetailRow label="Status" value={<StatusChip kind={status.kind} label={status.label} />} />
          <DetailRow label="Created by" value={userName(users, vendor.createdBy)} />
          <DetailRow label="Created" value={formatUtcDate(vendor.createdAtUtc)} />
          <DetailRow label="Last modified by" value={userName(users, vendor.modifiedBy)} />
          <DetailRow label="Last updated" value={formatUtcDate(vendor.updatedAtUtc)} />
        </div>
      </Panel>

      {!vendor.isActive && (
        <Note>
          Retired vendors stay listed on purpose, and their name stays taken — restore this one
          rather than adding it again.
        </Note>
      )}
    </>
  );
}

function userName(users: readonly BudgetOwnerOption[], id: string | null): string {
  if (!id) return "—";
  const user = users.find((u) => u.userId === id);
  return user ? userDisplay(user.fullName, user.email, "—") : "Unknown user";
}

/** The text under a pending two-click confirm, naming what the second click will do. */
function ConfirmNote({ children }: { children: React.ReactNode }) {
  return (
    <div
      style={{
        marginBottom: 12,
        fontFamily: fonts.body,
        fontSize: 11.5,
        color: colors.textSecondary,
        lineHeight: 1.6,
      }}
    >
      {children}
    </div>
  );
}

/** A quiet explanatory line. */
function Note({ children }: { children: React.ReactNode }) {
  return (
    <div
      style={{
        marginTop: 10,
        fontFamily: fonts.body,
        fontSize: 11.5,
        color: colors.textDim,
        lineHeight: 1.6,
      }}
    >
      {children}
    </div>
  );
}
