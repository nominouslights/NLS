"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import type { ClientContact } from "@/lib/types";
import { contactsFor, setPrimaryContact, useClientStore } from "@/lib/clientStore";
import { SectionLabel } from "@/components/ui/Panel";
import { ActionButton } from "@/components/ui/Button";
import ContactFormModal from "@/components/ContactFormModal";

// Contact roster for a client — multiple contacts, one primary, each with a
// role/title, email, phone, and notes. Read/write; writes go to lib/clientStore.

type Editor = { mode: "new" } | { mode: "edit"; contact: ClientContact } | null;

export default function ContactRoster({ clientId, clientName }: { clientId: number; clientName: string }) {
  useClientStore();
  const [editor, setEditor] = useState<Editor>(null);
  const roster = contactsFor(clientId);

  return (
    <div>
      <div style={{ display: "flex", alignItems: "center", marginBottom: 14 }}>
        <SectionLabel>
          Contact roster · {roster.length} contact{roster.length === 1 ? "" : "s"}
        </SectionLabel>
        <ActionButton variant="primary" style={{ marginLeft: "auto" }} onClick={() => setEditor({ mode: "new" })}>
          + ADD CONTACT
        </ActionButton>
      </div>

      {roster.length === 0 ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 2px" }}>
          No contacts on the roster yet.
        </div>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          {roster.map((c) => (
            <div
              key={c.id}
              style={{
                display: "flex",
                alignItems: "flex-start",
                gap: 12,
                padding: "12px 14px",
                background: colors.cardBg,
                border: `1px solid ${c.primary ? colors.borderActive : colors.borderSubtle}`,
                borderRadius: 10,
                boxShadow: colors.shadowCard,
              }}
            >
              <div
                style={{
                  width: 34,
                  height: 34,
                  flex: "none",
                  borderRadius: 9,
                  background: colors.cardBgActive,
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontFamily: fonts.condensed,
                  fontWeight: 700,
                  fontSize: 13,
                  color: colors.skyBlue,
                }}
              >
                {initials(c.name)}
              </div>

              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 9, marginBottom: 2 }}>
                  <span style={{ fontFamily: fonts.body, fontSize: 13.5, fontWeight: 600, color: colors.textPrimary }}>
                    {c.name}
                  </span>
                  {c.primary && (
                    <span
                      style={{
                        fontFamily: fonts.semiCondensed,
                        fontSize: 9,
                        letterSpacing: ".1em",
                        textTransform: "uppercase",
                        color: "#FFFFFF",
                        background: colors.blue,
                        padding: "2px 6px",
                        borderRadius: 5,
                      }}
                    >
                      Primary
                    </span>
                  )}
                </div>
                <div
                  style={{
                    fontFamily: fonts.semiCondensed,
                    fontSize: 10.5,
                    letterSpacing: ".06em",
                    textTransform: "uppercase",
                    color: colors.textLabel,
                    marginBottom: c.email || c.phone ? 6 : 0,
                  }}
                >
                  {c.title}
                </div>
                {(c.email || c.phone) && (
                  <div style={{ display: "flex", flexWrap: "wrap", gap: "2px 16px", fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
                    {c.email && <span>{c.email}</span>}
                    {c.phone && <span style={{ fontFamily: fonts.mono, fontSize: 11.5 }}>{c.phone}</span>}
                  </div>
                )}
                {c.notes && (
                  <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textSecondary, marginTop: 6, lineHeight: 1.5 }}>
                    {c.notes}
                  </div>
                )}
              </div>

              <div style={{ display: "flex", flexDirection: "column", gap: 6, flex: "none" }}>
                <ActionButton onClick={() => setEditor({ mode: "edit", contact: c })}>EDIT</ActionButton>
                {!c.primary && (
                  <ActionButton onClick={() => setPrimaryContact(clientId, c.id)}>MAKE PRIMARY</ActionButton>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {editor && (
        <ContactFormModal
          clientId={clientId}
          clientName={clientName}
          contact={editor.mode === "edit" ? editor.contact : null}
          isOnlyContact={roster.length === 0}
          onClose={() => setEditor(null)}
          onSaved={() => undefined}
        />
      )}
    </div>
  );
}

function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "—";
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}
