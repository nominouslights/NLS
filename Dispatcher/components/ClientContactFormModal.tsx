"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  createContact,
  updateContact,
  refetchUntil,
  listContacts,
  type ClientContactInput,
  type ClientContactRecord,
} from "@/lib/api/clients";
import { ModalShell } from "@/components/ui/ModalShell";
import { TextField, TextAreaField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

// Create/edit modal for a client contact. Pass `contact` to edit (prefilled,
// PUT full-document replace on save); omit it to create. The caller's refetch
// logic is their responsibility beyond the projection-visibility wait here.

export default function ClientContactFormModal({
  clientId,
  clientName,
  contact,
  onClose,
  onSaved,
}: {
  clientId: string;
  clientName: string;
  /** Existing contact to edit; omit for create mode. */
  contact?: ClientContactRecord;
  onClose: () => void;
  onSaved: () => void;
}) {
  const editing = contact !== undefined;
  const [name, setName] = useState(contact?.name ?? "");
  const [title, setTitle] = useState(contact?.title ?? "");
  const [email, setEmail] = useState(contact?.email ?? "");
  const [phone, setPhone] = useState(contact?.phone ?? "");
  const [notes, setNotes] = useState(contact?.notes ?? "");
  const [isPrimary, setIsPrimary] = useState(contact?.isPrimary ?? false);
  const [receivesEmailReports, setReceivesEmailReports] = useState(
    contact?.receivesEmailReports ?? false,
  );
  const [receivesAccrualsReports, setReceivesAccrualsReports] = useState(
    contact?.receivesAccrualsReports ?? false,
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (busy) return;
    if (!name.trim()) return setError("Enter the contact's name.");
    if (!title.trim()) return setError("Enter a role / title (e.g., Operations, AP, Compliance).");

    setBusy(true);
    setError(null);

    try {
      const input: ClientContactInput = {
        name: name.trim(),
        title: title.trim(),
        email: email.trim() || undefined,
        phone: phone.trim() || undefined,
        notes: notes.trim() || undefined,
        // New contacts from this form are never primary (the first contact was
        // marked primary during onboarding); editing keeps the toggle live.
        isPrimary: editing ? isPrimary : false,
        receivesEmailReports,
        receivesAccrualsReports,
      };

      if (editing) {
        await updateContact(clientId, contact.id, input);
        // Wait for the projection to reflect the update (updatedAtUtc moves).
        await refetchUntil(listContacts.bind(null, clientId), (r) =>
          r.some((c) => c.id === contact.id && c.updatedAtUtc !== contact.updatedAtUtc),
        );
      } else {
        await createContact(clientId, input);
        await refetchUntil(listContacts.bind(null, clientId), (r) => r.length > 0);
      }
      onSaved();
      onClose();
    } catch (e) {
      // The one conflict this form can cause gets spelled out — a generic 409
      // message would leave the dispatcher guessing which contact blocks it.
      if (e instanceof ApiError && e.code === "Clients.ClientContact.PrimaryAlreadyExists") {
        setError(
          "This client already has a primary contact — unmark the current primary first, then mark this one.",
        );
      } else {
        setError(
          e instanceof ApiError
            ? e.message
            : `Failed to ${editing ? "save" : "add"} the contact — please try again.`,
        );
      }
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Clients & Contracts · ${clientName} · Contacts`}
      title={editing ? "Edit Contact" : "Add Contact"}
      onClose={onClose}
      error={error}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "SAVING…" : editing ? "SAVE CONTACT" : "ADD CONTACT"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <TextField label="Name" value={name} onChange={setName} placeholder="Quinton Falk" />
        <TextField label="Role / title" value={title} onChange={setTitle} placeholder="Operations · AP · Compliance" />
        <TextField label="Email (optional)" value={email} onChange={setEmail} placeholder="name@client.com" />
        <TextField label="Phone (optional)" value={phone} onChange={setPhone} placeholder="(204) 555-0100" />
      </div>
      <div style={{ marginTop: 14 }}>
        <TextAreaField label="Notes (optional)" value={notes} onChange={setNotes} rows={2} />
      </div>

      {editing ? (
        <label
          style={{
            marginTop: 16,
            display: "flex",
            alignItems: "flex-start",
            gap: 11,
            cursor: "pointer",
          }}
        >
          <input
            type="checkbox"
            checked={isPrimary}
            onChange={(e) => setIsPrimary(e.target.checked)}
            style={{ accentColor: colors.blue, cursor: "pointer", marginTop: 2 }}
          />
          <div>
            <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
              Primary contact
            </div>
            <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
              One primary per client — unmark the current primary before promoting another.
            </div>
          </div>
        </label>
      ) : (
        <div style={{ marginTop: 16, display: "flex", alignItems: "center", gap: 11 }}>
          <span
            style={{
              width: 40,
              height: 22,
              flex: "none",
              borderRadius: 999,
              background: colors.borderStrong,
              position: "relative",
              opacity: 0.7,
            }}
          >
            <span
              style={{
                position: "absolute",
                top: 2,
                left: 2,
                width: 18,
                height: 18,
                borderRadius: "50%",
                background: "#FFFFFF",
                boxShadow: colors.shadowCard,
              }}
            />
          </span>
          <div>
            <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
              Primary contact
            </div>
            <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
              Only the first contact is marked as primary.
            </div>
          </div>
        </div>
      )}

      <label
        style={{
          marginTop: 14,
          display: "flex",
          alignItems: "flex-start",
          gap: 11,
          cursor: "pointer",
        }}
      >
        <input
          type="checkbox"
          checked={receivesEmailReports}
          onChange={(e) => setReceivesEmailReports(e.target.checked)}
          style={{ accentColor: colors.blue, cursor: "pointer", marginTop: 2 }}
        />
        <div>
          <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
            Receives email reports
          </div>
          <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
            Gets a report (with a PDF of each email) when pickup emails are sent for this client&rsquo;s
            crew trips.
          </div>
        </div>
      </label>

      <label
        style={{
          marginTop: 14,
          display: "flex",
          alignItems: "flex-start",
          gap: 11,
          cursor: "pointer",
        }}
      >
        <input
          type="checkbox"
          checked={receivesAccrualsReports}
          onChange={(e) => setReceivesAccrualsReports(e.target.checked)}
          style={{ accentColor: colors.blue, cursor: "pointer", marginTop: 2 }}
        />
        <div>
          <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
            Receives accruals reports
          </div>
          <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
            Listed as a recipient when a dispatcher emails this client&rsquo;s monthly accruals report
            (trips by billing state, with the PDF statement attached).
          </div>
        </div>
      </label>
    </ModalShell>
  );
}
