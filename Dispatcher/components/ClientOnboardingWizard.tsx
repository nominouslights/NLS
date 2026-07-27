"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  createClient,
  createContact,
  refetchUntil,
  listClients,
  CLIENT_TYPE_LABELS,
  SERVICE_TYPE_LABELS,
  type ClientType,
  type ClientServiceType,
  type ClientInput,
  type ClientContactInput,
} from "@/lib/api/clients";
import { ModalShell } from "@/components/ui/ModalShell";
import { TextField, SelectField, TextAreaField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";
import ContractFormModal from "@/components/ContractFormModal";

// Multi-step client onboarding: Step 1 (org) → Step 2 (contact) → Step 3 (contract, Client-type only).
// Known limitation: primary-contact-required is UI flow control only, not a backend transaction
// guarantee. If the browser closes between steps, a contact-less Client row persists.

export default function ClientOnboardingWizard({
  onClose,
  onSaved,
}: {
  onClose: () => void;
  onSaved: (newClientId: string) => void;
}) {
  const [step, setStep] = useState<1 | 2 | 3>(1);

  // Step 1: Organization
  const [type, setType] = useState<ClientType>("Client");
  const [name, setName] = useState("");
  const [serviceType, setServiceType] = useState<ClientServiceType>("Community");
  const [tag, setTag] = useState("");
  const [agreementReference, setAgreementReference] = useState("");
  const [notes, setNotes] = useState("");

  // Step 2: Contact
  const [contactName, setContactName] = useState("");
  const [contactTitle, setContactTitle] = useState("");
  const [contactEmail, setContactEmail] = useState("");
  const [contactPhone, setContactPhone] = useState("");
  const [contactNotes, setContactNotes] = useState("");

  // Step 3: Contract (Client-type only)
  const [showContractForm, setShowContractForm] = useState(false);

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [newClientId, setNewClientId] = useState<string | null>(null);

  async function submitStep1() {
    if (busy) return;
    if (!name.trim()) return setError("Enter the client name.");
    if (!tag.trim()) return setError("Enter a tag (e.g., Corporate, Program).");
    if (type === "Client" && !serviceType) return setError("Select a service type.");

    setBusy(true);
    setError(null);

    try {
      const input: ClientInput = {
        name: name.trim(),
        type,
        serviceType: type === "Client" ? serviceType : null,
        tag: tag.trim(),
        agreementReference: agreementReference.trim() || undefined,
        notes: notes.trim() || undefined,
      };

      const clientId = await createClient(input);
      setNewClientId(clientId);
      setStep(2);
      setBusy(false);
    } catch (e) {
      setError(
        e instanceof ApiError ? e.message : "Failed to create the client — please try again."
      );
      setBusy(false);
    }
  }

  async function submitStep2() {
    if (busy) return;
    if (!contactName.trim()) return setError("Enter the contact's name.");
    if (!contactTitle.trim()) return setError("Enter a title/role.");

    setBusy(true);
    setError(null);

    try {
      const input: ClientContactInput = {
        name: contactName.trim(),
        title: contactTitle.trim(),
        email: contactEmail.trim() || undefined,
        phone: contactPhone.trim() || undefined,
        notes: contactNotes.trim() || undefined,
        isPrimary: true,
      };

      await createContact(newClientId!, input);

      if (type === "VendorPartner") {
        // Vendor path ends here — trigger the saved callback and close.
        await refetchUntil(listClients, (r) => r.some((c) => c.id === newClientId));
        onSaved(newClientId!);
        onClose();
      } else {
        // Client path: offer optional contract setup.
        setStep(3);
        setBusy(false);
      }
    } catch (e) {
      setError(
        e instanceof ApiError ? e.message : "Failed to create the contact — please try again."
      );
      setBusy(false);
    }
  }

  function handleContractSkip() {
    // Close contract form and finish.
    refetchUntil(listClients, (r) => r.some((c) => c.id === newClientId)).then(() => {
      onSaved(newClientId!);
      onClose();
    });
  }

  function handleContractSaved() {
    // Contract created; refetch and finish.
    refetchUntil(listClients, (r) => r.some((c) => c.id === newClientId)).then(() => {
      onSaved(newClientId!);
      onClose();
    });
  }

  const typeOptions = Object.entries(CLIENT_TYPE_LABELS).map(([k, label]) => ({
    value: k as ClientType,
    label,
  }));

  const serviceTypeOptions = Object.entries(SERVICE_TYPE_LABELS).map(([k, label]) => ({
    value: k as ClientServiceType,
    label,
  }));

  // Step 1: Organization
  if (step === 1) {
    return (
      <ModalShell
        eyebrow="Clients & Contracts · Onboarding"
        title="New Organization"
        onClose={onClose}
        error={error}
        footer={
          <>
            <ActionButton onClick={onClose}>CANCEL</ActionButton>
            <ActionButton variant="primary" onClick={submitStep1} disabled={busy}>
              {busy ? "CREATING…" : "NEXT"}
            </ActionButton>
          </>
        }
      >
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
          <SelectField
            label="Organization type"
            value={type}
            onChange={(v) => setType(v as ClientType)}
            options={typeOptions}
          />
          <TextField label="Organization name" value={name} onChange={setName} placeholder="Vale Manitoba Operations" />
        </div>

        {type === "Client" && (
          <div style={{ marginTop: 14, display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
            <SelectField
              label="Service type"
              value={serviceType}
              onChange={(v) => setServiceType(v as ClientServiceType)}
              options={serviceTypeOptions}
            />
          </div>
        )}

        {type === "VendorPartner" && (
          <div style={{ marginTop: 14 }}>
            <TextField
              label="Agreement reference (optional)"
              value={agreementReference}
              onChange={setAgreementReference}
              placeholder="e.g., MSA-2026-001"
            />
          </div>
        )}

        <div style={{ marginTop: 14, display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
          <TextField label="Tag" value={tag} onChange={setTag} placeholder="Corporate, Program, etc." />
        </div>

        <div style={{ marginTop: 14 }}>
          <TextAreaField label="Notes (optional)" value={notes} onChange={setNotes} rows={2} />
        </div>
      </ModalShell>
    );
  }

  // Step 2: Primary Contact
  if (step === 2) {
    return (
      <ModalShell
        eyebrow="Clients & Contracts · Onboarding"
        title="Add Primary Contact"
        onClose={onClose}
        error={error}
        footer={
          <>
            <ActionButton onClick={onClose} disabled={busy}>
              CANCEL
            </ActionButton>
            <ActionButton variant="primary" onClick={submitStep2} disabled={busy}>
              {busy ? "SAVING…" : type === "VendorPartner" ? "FINISH" : "NEXT"}
            </ActionButton>
          </>
        }
      >
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
          <TextField
            label="Name"
            value={contactName}
            onChange={setContactName}
            placeholder="Quinton Falk"
          />
          <TextField
            label="Role / Title"
            value={contactTitle}
            onChange={setContactTitle}
            placeholder="Operations, AP, Compliance"
          />
          <TextField
            label="Email (optional)"
            value={contactEmail}
            onChange={setContactEmail}
            placeholder="contact@client.com"
          />
          <TextField
            label="Phone (optional)"
            value={contactPhone}
            onChange={setContactPhone}
            placeholder="(204) 555-0100"
          />
        </div>
        <div style={{ marginTop: 14 }}>
          <TextAreaField label="Notes (optional)" value={contactNotes} onChange={setContactNotes} rows={2} />
        </div>
        <div style={{ marginTop: 14, display: "flex", alignItems: "center", gap: 11 }}>
          <span
            style={{
              width: 40,
              height: 22,
              flex: "none",
              borderRadius: 999,
              background: colors.blue,
              position: "relative",
            }}
          >
            <span
              style={{
                position: "absolute",
                top: 2,
                left: 20,
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
              First contact is automatically marked as primary.
            </div>
          </div>
        </div>
      </ModalShell>
    );
  }

  // Step 3: Optional Contract (Client-type only)
  if (step === 3) {
    if (showContractForm) {
      return (
        <ContractFormModal
          clientId={newClientId!}
          clientName={name}
          onClose={() => setShowContractForm(false)}
          onSaved={handleContractSaved}
        />
      );
    }

    return (
      <ModalShell
        eyebrow="Clients & Contracts · Onboarding"
        title="Add Initial Contract?"
        onClose={onClose}
        footer={
          <>
            <ActionButton onClick={handleContractSkip}>SKIP & FINISH</ActionButton>
            <ActionButton variant="primary" onClick={() => setShowContractForm(true)}>
              ADD CONTRACT
            </ActionButton>
          </>
        }
      >
        <div style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textPrimary, lineHeight: 1.6 }}>
          <p>You can add a contract now or create one later from the client detail screen.</p>
        </div>
      </ModalShell>
    );
  }

  return null;
}
