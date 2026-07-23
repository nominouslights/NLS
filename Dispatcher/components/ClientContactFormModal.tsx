"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import { createContact, refetchUntil, listContacts, type ClientContactInput } from "@/lib/api/clients";
import { ModalShell } from "@/components/ui/ModalShell";
import { TextField, TextAreaField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

// Create-only modal for adding a new contact to an existing client.
// Wrapper around createContact; the caller's refetch logic is their responsibility.

export default function ClientContactFormModal({
  clientId,
  clientName,
  onClose,
  onSaved,
}: {
  clientId: string;
  clientName: string;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [name, setName] = useState("");
  const [title, setTitle] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [notes, setNotes] = useState("");
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
        isPrimary: false, // New contacts from this form are never primary (first contact was during onboarding).
      };

      await createContact(clientId, input);
      await refetchUntil(listContacts.bind(null, clientId), (r) => r.length > 0);
      onSaved();
      onClose();
    } catch (e) {
      setError(
        e instanceof ApiError ? e.message : "Failed to add the contact — please try again."
      );
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Clients & Contracts · ${clientName} · Contacts`}
      title="Add Contact"
      onClose={onClose}
      error={error}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "SAVING…" : "ADD CONTACT"}
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
    </ModalShell>
  );
}
