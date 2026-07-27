"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import type { InteractionType } from "@/lib/types";
import { ApiError } from "@/lib/api";
import { createInteraction, listContacts, type ClientContactRecord } from "@/lib/api/clients";
import { ModalShell } from "@/components/ui/ModalShell";
import { DateField, FieldLabel, SelectField, TextAreaField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

const TYPES: InteractionType[] = ["Call", "Meeting", "Email", "Site Visit", "Other"];

// Log a client interaction / touchpoint — WHEN, WHAT kind, a summary, WHO from
// the client took part (many-to-many with the contact roster), and an optional
// follow-up. Real Clients API — POSTs to /api/clients/{id}/interactions.

export default function InteractionModal({
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
  const [roster, setRoster] = useState<ClientContactRecord[]>([]);
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const [type, setType] = useState<InteractionType>("Call");
  const [summary, setSummary] = useState("");
  const [participants, setParticipants] = useState<Set<string>>(new Set());
  const [followUpDate, setFollowUpDate] = useState("");
  const [followUpNote, setFollowUpNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    listContacts(clientId).then(
      (fresh) => {
        if (active) setRoster(fresh);
      },
      () => {
        // Non-fatal — the interaction can still be logged without tagging participants.
        if (active) setRoster([]);
      },
    );
    return () => {
      active = false;
    };
  }, [clientId]);

  function toggleParticipant(id: string) {
    setParticipants((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  async function submit() {
    if (busy) return;
    if (!date) return setError("Choose the date of the interaction.");
    if (!summary.trim()) return setError("Enter a short summary of what happened.");
    if (followUpNote.trim() && !followUpDate) return setError("Set a follow-up date for the follow-up note.");

    setBusy(true);
    setError(null);

    try {
      await createInteraction(clientId, {
        type,
        occurredOn: date,
        summary: summary.trim(),
        participantContactIds: roster.filter((c) => participants.has(c.id)).map((c) => c.id),
        followUpDate: followUpDate || undefined,
        followUpNote: followUpNote.trim() || undefined,
      });
      setBusy(false);
      onSaved();
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to log the interaction — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Clients & Contracts · ${clientName} · Interaction log`}
      title="Log Interaction"
      onClose={onClose}
      error={error}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "SAVING…" : "LOG INTERACTION"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <DateField label="Date" value={date} onChange={setDate} />
        <SelectField
          label="Type"
          value={type}
          onChange={(v) => setType(v as InteractionType)}
          options={TYPES.map((t) => ({ value: t, label: t }))}
        />
      </div>

      <div style={{ marginTop: 14 }}>
        <TextAreaField
          label="Summary"
          value={summary}
          onChange={setSummary}
          rows={3}
          placeholder="What was discussed or decided…"
        />
      </div>

      {/* participant picker — many-to-many with the contact roster */}
      <div style={{ marginTop: 14 }}>
        <FieldLabel hint={<span style={{ color: colors.textFaint }}>· from the contact roster</span>}>
          Participants
        </FieldLabel>
        {roster.length === 0 ? (
          <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
            No contacts on the roster yet — add one first to tag participants.
          </div>
        ) : (
          <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
            {roster.map((c) => {
              const on = participants.has(c.id);
              return (
                <span
                  key={c.id}
                  onClick={() => toggleParticipant(c.id)}
                  style={{
                    display: "inline-flex",
                    alignItems: "center",
                    gap: 7,
                    padding: "6px 11px",
                    borderRadius: 8,
                    cursor: "pointer",
                    fontFamily: fonts.body,
                    fontSize: 12.5,
                    fontWeight: 600,
                    userSelect: "none",
                    color: on ? "#FFFFFF" : colors.textSecondary,
                    background: on ? colors.blue : colors.inputBg,
                    border: `1px solid ${on ? colors.blue : colors.borderStrong}`,
                  }}
                >
                  <span style={{ fontSize: 11 }}>{on ? "✓" : "＋"}</span>
                  {c.name}
                  <span style={{ fontSize: 10.5, opacity: 0.8, fontWeight: 500 }}>· {c.title}</span>
                </span>
              );
            })}
          </div>
        )}
      </div>

      {/* optional follow-up */}
      <div
        style={{
          marginTop: 18,
          paddingTop: 16,
          borderTop: `1px solid ${colors.border}`,
        }}
      >
        <div
          style={{
            fontFamily: fonts.semiCondensed,
            fontSize: 10,
            letterSpacing: ".12em",
            textTransform: "uppercase",
            color: colors.textLabel,
            marginBottom: 11,
          }}
        >
          Follow-up (optional)
        </div>
        <div style={{ display: "grid", gridTemplateColumns: "180px 1fr", gap: 14, alignItems: "start" }}>
          <DateField label="Follow-up date" value={followUpDate} onChange={setFollowUpDate} />
          <TextField
            label="Follow-up note"
            value={followUpNote}
            onChange={setFollowUpNote}
            placeholder="What needs to happen next"
          />
        </div>
      </div>
    </ModalShell>
  );
}
