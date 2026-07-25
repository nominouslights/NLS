"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import type { ClientContact } from "@/lib/types";
import { addContact, updateContact } from "@/lib/clientStore";
import { ModalShell } from "@/components/ui/ModalShell";
import { TextAreaField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

// Add or edit a client contact. One component, two modes (VehicleFormModal
// pattern): contact === null → create, otherwise edit. Prototype/mock — writes
// to lib/clientStore. Exactly one primary contact per client is enforced by the
// store, so toggling this on will demote the current primary on save.

export default function ContactFormModal({
  clientId,
  clientName,
  contact,
  isOnlyContact,
  onClose,
  onSaved,
}: {
  clientId: number;
  clientName: string;
  contact: ClientContact | null;
  isOnlyContact: boolean; // creating the first contact → primary is forced on
  onClose: () => void;
  onSaved: () => void;
}) {
  const editing = contact !== null;
  const [name, setName] = useState(contact?.name ?? "");
  const [title, setTitle] = useState(contact?.title ?? "");
  const [email, setEmail] = useState(contact?.email ?? "");
  const [phone, setPhone] = useState(contact?.phone ?? "");
  const [notes, setNotes] = useState(contact?.notes ?? "");
  const [primary, setPrimary] = useState(contact?.primary ?? false);
  const [error, setError] = useState<string | null>(null);

  // First-ever contact for a client is always primary; editing the existing
  // primary can't unset it here (promote another contact instead).
  const primaryLocked = (!editing && isOnlyContact) || (editing && contact.primary);
  const primaryOn = primaryLocked ? true : primary;

  function submit() {
    if (!name.trim()) return setError("Enter the contact's name.");
    if (!title.trim()) return setError("Enter a role / title (e.g. Operations, AP, Compliance).");

    const fields = {
      name: name.trim(),
      title: title.trim(),
      email: email.trim() || undefined,
      phone: phone.trim() || undefined,
      notes: notes.trim() || undefined,
      primary: primaryOn,
    };

    if (editing) {
      updateContact(contact.id, fields);
    } else {
      addContact({ clientId, ...fields });
    }
    onSaved();
    onClose();
  }

  return (
    <ModalShell
      eyebrow={`Clients & Contracts · ${clientName} · Contact roster`}
      title={editing ? "Edit Contact" : "Add Contact"}
      onClose={onClose}
      error={error}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit}>
            {editing ? "SAVE CHANGES" : "ADD CONTACT"}
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

      {/* primary toggle */}
      <div style={{ marginTop: 16, display: "flex", alignItems: "center", gap: 11 }}>
        <span
          onClick={() => !primaryLocked && setPrimary((v) => !v)}
          style={{
            width: 40,
            height: 22,
            flex: "none",
            borderRadius: 999,
            background: primaryOn ? colors.blue : colors.borderStrong,
            position: "relative",
            cursor: primaryLocked ? "not-allowed" : "pointer",
            opacity: primaryLocked ? 0.7 : 1,
            transition: "background .15s",
          }}
        >
          <span
            style={{
              position: "absolute",
              top: 2,
              left: primaryOn ? 20 : 2,
              width: 18,
              height: 18,
              borderRadius: "50%",
              background: "#FFFFFF",
              boxShadow: colors.shadowCard,
              transition: "left .15s",
            }}
          />
        </span>
        <div>
          <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
            Primary contact
          </div>
          <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
            {primaryLocked
              ? editing
                ? "Promote another contact to move the primary flag."
                : "First contact is set as primary."
              : "Exactly one primary per client — this will replace the current primary."}
          </div>
        </div>
      </div>
    </ModalShell>
  );
}
