"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import { listContacts, type ClientContactRecord } from "@/lib/api/clients";
import { SectionLabel } from "@/components/ui/Panel";
import { ActionButton } from "@/components/ui/Button";
import ClientContactFormModal from "@/components/ClientContactFormModal";

// Contact roster for a client — read-only display with add-contact flow.
// Fetches real contacts from the API (real backend CRM data, not the mock shim).

export default function ContactRoster({ clientId, clientName }: { clientId: string; clientName: string }) {
  const [contacts, setContacts] = useState<ClientContactRecord[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [showAddForm, setShowAddForm] = useState(false);

  useEffect(() => {
    let active = true;
    listContacts(clientId).then(
      (fresh) => {
        if (active) {
          setContacts(fresh);
          setLoadError(null);
        }
      },
      (e) => {
        if (active) {
          setLoadError(e instanceof ApiError ? e.message : "Failed to load contacts.");
          setContacts([]);
        }
      },
    );
    return () => {
      active = false;
    };
  }, [clientId]);

  async function onContactSaved() {
    setShowAddForm(false);
    // Refetch the roster after adding a contact.
    try {
      const fresh = await listContacts(clientId);
      setContacts(fresh);
    } catch (e) {
      setLoadError(e instanceof ApiError ? e.message : "Failed to reload contacts.");
    }
  }

  const roster = contacts ?? [];

  return (
    <div>
      <div style={{ display: "flex", alignItems: "center", marginBottom: 14 }}>
        <SectionLabel>
          Contact roster · {roster.length} contact{roster.length === 1 ? "" : "s"}
        </SectionLabel>
        <ActionButton variant="primary" style={{ marginLeft: "auto" }} onClick={() => setShowAddForm(true)}>
          + ADD CONTACT
        </ActionButton>
      </div>

      {loadError && (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: "#D55E00", padding: "6px 2px", marginBottom: 12 }}>
          {loadError}
        </div>
      )}

      {roster.length === 0 && !loadError ? (
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
                border: `1px solid ${c.isPrimary ? colors.borderActive : colors.borderSubtle}`,
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
                  {c.isPrimary && (
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
                  {c.receivesEmailReports && (
                    <span
                      style={{
                        fontFamily: fonts.semiCondensed,
                        fontSize: 9,
                        letterSpacing: ".1em",
                        textTransform: "uppercase",
                        color: "#FFFFFF",
                        background: colors.skyBlue,
                        padding: "2px 6px",
                        borderRadius: 5,
                      }}
                    >
                      Reports
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
            </div>
          ))}
        </div>
      )}

      {showAddForm && (
        <ClientContactFormModal
          clientId={clientId}
          clientName={clientName}
          onClose={() => setShowAddForm(false)}
          onSaved={onContactSaved}
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
