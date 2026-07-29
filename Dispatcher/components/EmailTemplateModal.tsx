"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import {
  listClients,
  SERVICE_TYPE_LABELS,
  type ClientRecord,
  type ClientServiceType,
} from "@/lib/api/clients";
import {
  createEmailTemplate,
  previewEmailTemplate,
  setEmailTemplateActive,
  updateEmailTemplate,
  MERGE_FIELDS,
  type EmailPreviewResult,
  type EmailTemplateInput,
  type EmailTemplateRecord,
} from "@/lib/api/notifications";
import { ModalShell } from "@/components/ui/ModalShell";
import { SelectField, TextAreaField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";
import { MonoTag } from "@/components/ui/Chip";
import { SectionLabel } from "@/components/ui/Panel";

// Create/edit modal for pickup-email templates (Notifications module). The
// template body is simple HTML + canonical merge fields; a typo'd token is
// rejected by the backend at save time (400 UnknownMergeField) and surfaced
// via the ModalShell error banner. PREVIEW runs the exact server renderer
// (null values → sample data) and renders the result in a FULLY sandboxed
// iframe — template HTML is untrusted and must never touch the console.

const SERVICE_TYPE_OPTIONS = (Object.keys(SERVICE_TYPE_LABELS) as ClientServiceType[]).map((v) => ({
  value: v,
  label: SERVICE_TYPE_LABELS[v],
}));

export default function EmailTemplateModal({
  existing,
  onClose,
  onSaved,
}: {
  /** null = create a new template. */
  existing: EmailTemplateRecord | null;
  onClose: () => void;
  /** Called after any mutation (create / update / activate / deactivate) —
   *  the parent refetches until the projection reflects the change. */
  onSaved: (id: string) => Promise<void>;
}) {
  const [name, setName] = useState(existing?.name ?? "");
  const [serviceType, setServiceType] = useState<ClientServiceType>(existing?.serviceType ?? "Community");
  const [clientId, setClientId] = useState(existing?.clientId ?? "");
  const [subject, setSubject] = useState(existing?.subject ?? "");
  const [htmlBody, setHtmlBody] = useState(existing?.htmlBody ?? "");
  // Client-type orgs from the Clients API — null while loading, [] on failure.
  const [clients, setClients] = useState<ClientRecord[] | null>(null);
  const [preview, setPreview] = useState<EmailPreviewResult | null>(null);
  const [previewBusy, setPreviewBusy] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    listClients().then(
      (rows) => {
        if (active) setClients(rows.filter((c) => c.type === "Client"));
      },
      () => {
        if (active) setClients([]); // best-effort — the "None" option still works
      },
    );
    return () => {
      active = false;
    };
  }, []);

  /** Resolve the clientName snapshot for the selected client id. */
  function resolveClientName(id: string): string | null {
    if (!id) return null;
    const fromList = clients?.find((c) => c.id === id)?.name ?? null;
    if (fromList) return fromList;
    // Editing a client-specific template while the client list is unavailable —
    // keep the stored snapshot.
    if (existing && existing.clientId === id) return existing.clientName;
    return null;
  }

  function onClientChange(v: string) {
    setClientId(v);
    if (!v) return;
    // Picking a client prefills the service type from the client's own, when set.
    const client = clients?.find((c) => c.id === v);
    if (client?.serviceType) setServiceType(client.serviceType);
  }

  function buildInput(): EmailTemplateInput | null {
    if (!name.trim()) {
      setError("Enter a template name.");
      return null;
    }
    if (!subject.trim()) {
      setError("Enter a subject line.");
      return null;
    }
    if (!htmlBody.trim()) {
      setError("Enter the HTML body.");
      return null;
    }
    const clientName = resolveClientName(clientId);
    if (clientId && !clientName) {
      setError("The selected client could not be resolved — pick it again.");
      return null;
    }
    return {
      name: name.trim(),
      serviceType,
      clientId: clientId || null,
      clientName,
      subject: subject.trim(),
      htmlBody,
    };
  }

  async function submit() {
    if (busy) return;
    const input = buildInput();
    if (!input) return;
    setBusy(true);
    setError(null);
    try {
      let id: string;
      if (existing) {
        await updateEmailTemplate(existing.id, input);
        id = existing.id;
      } else {
        id = await createEmailTemplate(input);
      }
      await onSaved(id);
      onClose();
    } catch (e) {
      // Backend 400 codes (e.g. Notifications.Template.UnknownMergeField)
      // arrive as ApiError with a human-readable message.
      setError(e instanceof ApiError ? e.message : "Failed to save the template — please try again.");
      setBusy(false);
    }
  }

  async function toggleActive() {
    if (!existing || busy) return;
    setBusy(true);
    setError(null);
    try {
      await setEmailTemplateActive(existing.id, !existing.isActive);
      await onSaved(existing.id);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to change the template's status — please try again.");
      setBusy(false);
    }
  }

  async function runPreview() {
    if (previewBusy) return;
    if (!subject.trim() || !htmlBody.trim()) {
      setError("Enter a subject and HTML body before previewing.");
      return;
    }
    setPreviewBusy(true);
    setError(null);
    try {
      // No values → the server substitutes its sample data.
      setPreview(await previewEmailTemplate({ subject, htmlBody, values: null }));
    } catch (e) {
      setPreview(null);
      setError(e instanceof ApiError ? e.message : "Preview failed — please try again.");
    } finally {
      setPreviewBusy(false);
    }
  }

  const clientOptions =
    clients === null
      ? [
          {
            value: existing?.clientId ?? "",
            label: existing?.clientName ?? "Loading clients…",
          },
        ]
      : [
          { value: "", label: "None (service-type template)" },
          ...clients.map((c) => ({ value: c.id, label: c.name })),
          // Keep an unlisted stored client selectable while editing.
          ...(existing?.clientId && !clients.some((c) => c.id === existing.clientId)
            ? [{ value: existing.clientId, label: existing.clientName ?? existing.clientId }]
            : []),
        ];

  return (
    <ModalShell
      eyebrow={existing ? `Communications · ${existing.name}` : "Communications · Template library"}
      title={existing ? "Edit Email Template" : "New Email Template"}
      onClose={onClose}
      error={error}
      maxWidth={760}
      footer={
        <>
          {existing && (
            <ActionButton
              variant={existing.isActive ? "secondary" : "success"}
              onClick={toggleActive}
              disabled={busy}
              style={{ marginRight: "auto" }}
            >
              {busy ? "WORKING…" : existing.isActive ? "DEACTIVATE" : "ACTIVATE"}
            </ActionButton>
          )}
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "SAVING…" : existing ? "SAVE TEMPLATE" : "CREATE TEMPLATE"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14, marginBottom: 14 }}>
        <TextField label="Template name" value={name} onChange={setName} placeholder="Alamos crew pickup reminder" maxLength={200} />
        <SelectField
          label="Service type"
          value={serviceType}
          onChange={(v) => setServiceType(v as ClientServiceType)}
          options={SERVICE_TYPE_OPTIONS}
        />
        <SelectField
          label="Client (optional)"
          hint={
            <span style={{ color: colors.textDim }}>
              — client-specific templates win over service-type ones
            </span>
          }
          value={clientId}
          onChange={onClientChange}
          options={clientOptions}
          disabled={clients === null}
        />
        <TextField label="Subject" value={subject} onChange={setSubject} placeholder="Your Northern Link pickup — {{TripDate}}" maxLength={300} />
      </div>

      <TextAreaField
        label="HTML body"
        value={htmlBody}
        onChange={setHtmlBody}
        rows={12}
        mono
        placeholder={"<p>Hi {{PassengerName}},</p>\n<p>Your shuttle departs {{PickupStop}} at {{PickupTime}} on {{TripDate}}.</p>"}
      />

      {/* merge-field cheat sheet — the canonical token set, validated at save */}
      <div style={{ marginTop: 12 }}>
        <SectionLabel>Merge fields</SectionLabel>
        <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          {MERGE_FIELDS.map((f) => (
            <div key={f.token} style={{ display: "flex", alignItems: "center", gap: 9 }}>
              <MonoTag color={colors.skyBlue}>{f.token}</MonoTag>
              <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>{f.description}</span>
            </div>
          ))}
        </div>
      </div>

      <div style={{ marginTop: 14, display: "flex", alignItems: "center", gap: 10 }}>
        <ActionButton onClick={runPreview} disabled={previewBusy}>
          {previewBusy ? "RENDERING…" : "PREVIEW"}
        </ActionButton>
        <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
          Rendered by the server with sample data.
        </span>
      </div>

      {preview && (
        <div style={{ marginTop: 14 }}>
          <SectionLabel>Preview</SectionLabel>
          <div
            style={{
              fontFamily: fonts.body,
              fontSize: 13,
              fontWeight: 600,
              color: colors.textPrimary,
              marginBottom: 8,
            }}
          >
            {preview.subject}
          </div>
          {/* sandbox="" = full sandbox: no scripts, no navigation, no same-origin */}
          <iframe
            sandbox=""
            srcDoc={preview.htmlBody}
            title="Template preview"
            style={{
              width: "100%",
              height: 280,
              border: `1px solid ${colors.borderStrong}`,
              borderRadius: 9,
              background: "#FFFFFF",
            }}
          />
        </div>
      )}
    </ModalShell>
  );
}
