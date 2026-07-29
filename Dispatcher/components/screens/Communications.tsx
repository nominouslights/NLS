"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { PageHeader } from "@/components/ui/Panel";
import { ServiceChip, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { ApiError } from "@/lib/api/transport";
import { svcForServiceType } from "@/lib/api/clients";
import { refetchUntil } from "@/lib/api/trips";
import { listEmailTemplates, type EmailTemplateRecord } from "@/lib/api/notifications";
import EmailTemplateModal from "@/components/EmailTemplateModal";

// Communications — the TEMPLATE LIBRARY panel is real (Notifications module,
// pickup-email templates); the message log and compose panels remain mock
// until outbound messaging gets a backend.

const messages = [
  {
    title: "Driver dispatch · D. Chartrand",
    status: "Sent",
    ok: true,
    body: "TR-4821 assigned. Depart Thompson 06:30, Alamos crew shuttle to Lynn Lake.",
    time: "Jul 6 · 14:40",
  },
  {
    title: "Passenger · Eleanor Bighetty",
    status: "Sent",
    ok: true,
    body: "NIHB voucher confirmed. Your medical trip Jul 7 is booked at no cost. Escort included.",
    time: "Jul 6 · 09:12",
  },
  {
    title: "Passenger · Priya Sandhu",
    status: "Delivery failed · retry",
    ok: false,
    body: "Community run not yet viable — 2 more seats needed for Fri departure.",
    time: "Jul 7 · 07:50",
  },
];

export default function Communications() {
  const [templates, setTemplates] = useState<EmailTemplateRecord[] | null>(null);
  const [templatesError, setTemplatesError] = useState<string | null>(null);
  const [modal, setModal] = useState<null | { mode: "create" } | { mode: "edit"; template: EmailTemplateRecord }>(null);

  const fetchTemplates = useCallback(() => listEmailTemplates({ includeInactive: true }), []);

  const load = useCallback(async () => {
    try {
      const fresh = await fetchTemplates();
      setTemplates(fresh);
      setTemplatesError(null);
    } catch (e) {
      setTemplates(null);
      setTemplatesError(e instanceof ApiError ? e.message : "Failed to load templates.");
    }
  }, [fetchTemplates]);

  useEffect(() => {
    let active = true;
    fetchTemplates().then(
      (fresh) => {
        if (active) {
          setTemplates(fresh);
          setTemplatesError(null);
        }
      },
      (e) => {
        if (active) {
          setTemplates(null);
          setTemplatesError(e instanceof ApiError ? e.message : "Failed to load templates.");
        }
      },
    );
    return () => {
      active = false;
    };
  }, [fetchTemplates]);

  /** After a create/update/activate/deactivate: refetch until the projection
   *  reflects the change (rm_ reads are eventually consistent). */
  async function onTemplateSaved(id: string) {
    const before = templates?.find((r) => r.id === id) ?? null;
    const fresh = await refetchUntil(fetchTemplates, (rows) => {
      const row = rows.find((r) => r.id === id);
      if (!row) return false;
      // Existing row: wait for the updated snapshot; new row: presence is enough.
      return before ? row.updatedAtUtc !== before.updatedAtUtc || row.isActive !== before.isActive : true;
    });
    setTemplates(fresh);
    setTemplatesError(null);
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow="Business · Outbound messaging & templates" title="Communications" />
      </div>
      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "1fr 380px", borderTop: `1px solid ${colors.border}` }}>
        <div style={{ minHeight: 0, overflowY: "auto", padding: "18px 22px", borderRight: `1px solid ${colors.border}` }}>
          <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 16, letterSpacing: ".06em", color: colors.textSecondary, marginBottom: 12 }}>
            MESSAGE LOG
          </div>
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {messages.map((m) => (
              <div
                key={m.title}
                style={{
                  padding: "12px 15px",
                  borderRadius: 10,
                  background: colors.cardBg,
                  border: `1px solid ${m.ok ? colors.borderSubtle : "rgba(213,94,0,.28)"}`,
                  boxShadow: colors.shadowCard,
                }}
              >
                <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 4 }}>
                  <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>{m.title}</span>
                  <span
                    style={{
                      display: "inline-flex",
                      alignItems: "center",
                      gap: 5,
                      fontFamily: fonts.body,
                      fontWeight: 600,
                      fontSize: 10.5,
                      color: m.ok ? statusMeta("ontime").t : statusMeta("over").t,
                    }}
                  >
                    <span
                      style={{
                        width: 13,
                        height: 13,
                        borderRadius: 4,
                        background: m.ok ? "#009E73" : "#D55E00",
                        color: "#FFFFFF",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        fontSize: 8,
                        fontWeight: 800,
                      }}
                    >
                      {m.ok ? "✓" : "▲"}
                    </span>
                    {m.status}
                  </span>
                </div>
                <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, lineHeight: 1.5 }}>{m.body}</div>
                <div style={{ fontFamily: fonts.mono, fontSize: 10, color: colors.textDim, marginTop: 5 }}>{m.time}</div>
              </div>
            ))}
          </div>
        </div>
        <div style={{ minHeight: 0, overflowY: "auto", padding: "18px 22px" }}>
          <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12 }}>
            <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 16, letterSpacing: ".06em", color: colors.textSecondary, flex: 1 }}>
              TEMPLATE LIBRARY
            </div>
            <ActionButton variant="primary" onClick={() => setModal({ mode: "create" })}>
              + NEW TEMPLATE
            </ActionButton>
          </div>
          <div style={{ display: "flex", flexDirection: "column", gap: 7, marginBottom: 18 }}>
            {templatesError ? (
              <div
                style={{
                  padding: "12px 13px",
                  borderRadius: 9,
                  background: colors.cardBg,
                  border: "1px solid rgba(213,94,0,.4)",
                  display: "flex",
                  flexDirection: "column",
                  gap: 9,
                  alignItems: "flex-start",
                }}
              >
                <StatusChip kind="over" label="Templates unavailable" />
                <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, lineHeight: 1.5 }}>
                  {templatesError}
                </span>
                <ActionButton onClick={load}>RETRY</ActionButton>
              </div>
            ) : templates === null ? (
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 2px" }}>
                Loading templates…
              </div>
            ) : templates.length === 0 ? (
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 2px", lineHeight: 1.5 }}>
                No email templates yet. Create one to send pickup emails from a trip&rsquo;s detail panel.
              </div>
            ) : (
              templates.map((t) => (
                <div
                  key={t.id}
                  onClick={() => setModal({ mode: "edit", template: t })}
                  style={{
                    padding: "10px 13px",
                    borderRadius: 9,
                    background: colors.cardBg,
                    border: `1px solid ${colors.borderSubtle}`,
                    boxShadow: colors.shadowCard,
                    cursor: "pointer",
                    display: "flex",
                    flexDirection: "column",
                    gap: 7,
                  }}
                >
                  <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                    <span
                      style={{
                        fontFamily: fonts.body,
                        fontSize: 12.5,
                        fontWeight: 600,
                        color: colors.textPrimary,
                        flex: 1,
                        minWidth: 0,
                        whiteSpace: "nowrap",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                      }}
                    >
                      {t.name}
                    </span>
                    <StatusChip kind={t.isActive ? "ontime" : "off"} label={t.isActive ? "Active" : "Inactive"} />
                  </div>
                  <div style={{ display: "flex", alignItems: "center", gap: 8, minWidth: 0 }}>
                    <ServiceChip svc={svcForServiceType(t.serviceType)} />
                    {t.clientName && (
                      <span
                        style={{
                          fontFamily: fonts.body,
                          fontSize: 11.5,
                          color: colors.textMuted,
                          minWidth: 0,
                          whiteSpace: "nowrap",
                          overflow: "hidden",
                          textOverflow: "ellipsis",
                        }}
                      >
                        {t.clientName}
                      </span>
                    )}
                  </div>
                </div>
              ))
            )}
          </div>
          <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 16, letterSpacing: ".06em", color: colors.textSecondary, marginBottom: 11 }}>
            COMPOSE
          </div>
          <div style={{ padding: "14px 15px", borderRadius: 11, background: colors.cardBg, border: `1px solid ${colors.border}`, boxShadow: colors.shadowCard }}>
            <div
              style={{
                height: 120,
                borderRadius: 8,
                background: colors.inputBg,
                border: `1px solid ${colors.borderSubtle}`,
                padding: "11px 13px",
                fontFamily: fonts.body,
                fontSize: 12.5,
                color: colors.textDim,
                lineHeight: 1.5,
                marginBottom: 11,
              }}
            >
              Professional, safety-first, transparent tone. Canadian placeholders throughout…
            </div>
            <span
              style={{
                display: "inline-flex",
                fontFamily: fonts.condensed,
                fontWeight: 700,
                fontSize: 13,
                letterSpacing: ".03em",
                padding: "8px 16px",
                borderRadius: 8,
                background: colors.blue,
                color: "#FFFFFF",
                cursor: "pointer",
              }}
            >
              QUEUE MESSAGE
            </span>
          </div>
        </div>
      </div>

      {modal && (
        <EmailTemplateModal
          existing={modal.mode === "edit" ? modal.template : null}
          onClose={() => setModal(null)}
          onSaved={onTemplateSaved}
        />
      )}
    </div>
  );
}
