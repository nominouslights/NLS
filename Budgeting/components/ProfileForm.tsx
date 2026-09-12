"use client";

import { useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";
import { DetailRow, Panel, SectionLabel } from "@/components/ui/Panel";
import { TextField } from "@/components/ui/Field";
import { ErrorNotice } from "@/components/ErrorNotice";
import { ApiError } from "@/lib/api/transport";
import {
  PROFILE_FIELD_MAX_LENGTH,
  normalizeProfileField,
  type MyProfile,
  type MyProfileInput,
} from "@/lib/api/identity";
import { hasBudgetAccess } from "@/lib/roles";

// The self-service half of Settings: the two fields a user owns, beside the two they do not.
//
// onSave arrives as a prop rather than being imported, so a test can drive this component
// without mocking the transport — lib/api/budgeting.test.ts's rule is that client tests exercise
// pure functions and never stub request(), and this keeps that intact.
//
// Mount this keyed on the profile's userId. Then useState seeds from props correctly with no
// syncing effect, the same thing BudgetCodeFormModal gets for free by only existing while open.

export default function ProfileForm({
  profile,
  onSave,
  onSaved,
}: {
  profile: MyProfile;
  onSave: (input: MyProfileInput) => Promise<MyProfile>;
  onSaved: (profile: MyProfile) => void;
}) {
  const [fullName, setFullName] = useState(profile.fullName ?? "");
  const [jobTitle, setJobTitle] = useState(profile.jobTitle ?? "");

  // What is currently stored, as far as this form knows — seeded from the profile and advanced
  // by each successful save. Keeping it here rather than reading `profile` straight back means
  // the dirty check stays correct whether or not the parent re-renders us with the new values.
  const [saved, setSaved] = useState<MyProfileInput>({
    fullName: profile.fullName,
    jobTitle: profile.jobTitle,
  });
  const [justSaved, setJustSaved] = useState(false);

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<{ message: string; code: string } | null>(null);

  const input: MyProfileInput = {
    fullName: normalizeProfileField(fullName),
    jobTitle: normalizeProfileField(jobTitle),
  };

  // Compared on the normalized values, so a stray space is not a change — matching the server,
  // where an update that changes nothing writes nothing. Disabling SAVE when clean means the
  // button never promises a write that will not happen.
  const dirty = input.fullName !== saved.fullName || input.jobTitle !== saved.jobTitle;

  function edit(set: (v: string) => void) {
    return (v: string) => {
      set(v);
      setJustSaved(false);
    };
  }

  async function submit() {
    if (busy || !dirty) return;
    setBusy(true);
    setError(null);
    try {
      const updated = await onSave(input);
      onSaved(updated);
      // Re-seed from the response, not from what was typed: the server trims and blanks to null,
      // so this is what is actually stored.
      setFullName(updated.fullName ?? "");
      setJobTitle(updated.jobTitle ?? "");
      setSaved({ fullName: updated.fullName, jobTitle: updated.jobTitle });
      setJustSaved(true);
    } catch (e) {
      // Server messages are surfaced verbatim — they name the rule that was broken.
      setError(
        e instanceof ApiError
          ? { message: e.message, code: e.code }
          : { message: "Failed to save your profile — please try again.", code: "Unknown" },
      );
    } finally {
      // Unlike the modals, this component stays mounted after a successful save.
      setBusy(false);
    }
  }

  return (
    <>
      <Panel style={{ marginBottom: 12 }}>
        <SectionLabel>Your details</SectionLabel>

        {error && (
          <div style={{ marginBottom: 11 }}>
            <ErrorNotice
              title="Could not save your profile"
              message={error.message}
              code={error.code}
            />
          </div>
        )}

        <div style={{ display: "flex", flexDirection: "column", gap: 11 }}>
          <TextField
            label="Full name"
            value={fullName}
            onChange={edit(setFullName)}
            maxLength={PROFILE_FIELD_MAX_LENGTH}
            placeholder="e.g. Léa Fontaine"
            hint="Shown wherever you are named — budget codes you own, and what you create or change."
          />
          <TextField
            label="Job title"
            value={jobTitle}
            onChange={edit(setJobTitle)}
            maxLength={PROFILE_FIELD_MAX_LENGTH}
            placeholder="e.g. Financial Planner"
            hint="Yours to describe. It grants nothing — your role does that."
          />
        </div>

        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: 10,
            marginTop: 14,
          }}
        >
          <ActionButton variant="primary" onClick={() => void submit()} disabled={busy || !dirty}>
            {busy ? "SAVING…" : "SAVE PROFILE"}
          </ActionButton>
          {justSaved && !dirty && (
            // Glyph plus words, never the colour alone — the platform rule, and what keeps this
            // readable in grayscale.
            <span
              style={{
                fontFamily: fonts.body,
                fontSize: 12,
                color: statusMeta("ontime").t,
              }}
            >
              {statusMeta("ontime").g} Saved.
            </span>
          )}
        </div>
      </Panel>

      <Panel>
        <SectionLabel>Set by an owner</SectionLabel>
        <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
          <DetailRow label="Email" value={profile.email || "—"} />
          <DetailRow
            label="Role"
            value={
              <StatusChip
                kind={hasBudgetAccess(profile.role) ? "ontime" : "over"}
                label={profile.role || "—"}
              />
            }
          />
        </div>
        <div
          style={{
            fontFamily: fonts.body,
            fontSize: 11.5,
            color: colors.textDim,
            lineHeight: 1.6,
            marginTop: 12,
          }}
        >
          Your email is how you sign in, so it is not editable here — an owner changes it from the
          Dispatch Console. Your role decides what you can reach, including this console.
        </div>
      </Panel>
    </>
  );
}
