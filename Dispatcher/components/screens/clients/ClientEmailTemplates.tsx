"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import { svcForServiceType } from "@/lib/api/clients";
import { refetchUntil } from "@/lib/api/trips";
import { listEmailTemplates, type EmailTemplateRecord } from "@/lib/api/notifications";
import { SectionLabel } from "@/components/ui/Panel";
import { ActionButton } from "@/components/ui/Button";
import { ServiceChip, StatusChip } from "@/components/ui/Chip";
import EmailTemplateModal from "@/components/EmailTemplateModal";

// Per-client email template management — the client-scoped counterpart to the
// global Template Library on Communications.tsx. Lists templates pinned to this
// client (Notifications module, real backend) and creates new ones locked to
// this client via EmailTemplateModal's `lockedClient`. Mirrors ContactRoster.

export default function ClientEmailTemplates({ clientId, clientName }: { clientId: string; clientName: string }) {
  const [templates, setTemplates] = useState<EmailTemplateRecord[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [modal, setModal] = useState<null | { mode: "create" } | { mode: "edit"; template: EmailTemplateRecord }>(null);

  useEffect(() => {
    let active = true;
    listEmailTemplates({ clientId, includeInactive: true }).then(
      (fresh) => {
        if (active) {
          setTemplates(fresh);
          setLoadError(null);
        }
      },
      (e) => {
        if (active) {
          setLoadError(e instanceof ApiError ? e.message : "Failed to load templates.");
          setTemplates([]);
        }
      },
    );
    return () => {
      active = false;
    };
  }, [clientId]);

  /** After a create/update/activate/deactivate: refetch until the projection
   *  reflects the change (rm_ reads are eventually consistent), then close. */
  async function onTemplateSaved(id: string) {
    const before = templates?.find((r) => r.id === id) ?? null;
    try {
      const fresh = await refetchUntil(
        () => listEmailTemplates({ clientId, includeInactive: true }),
        (rows) => {
          const row = rows.find((r) => r.id === id);
          if (!row) return false;
          // Existing row: wait for the updated snapshot; new row: presence is enough.
          return before ? row.updatedAtUtc !== before.updatedAtUtc || row.isActive !== before.isActive : true;
        },
      );
      setTemplates(fresh);
      setLoadError(null);
    } catch (e) {
      setLoadError(e instanceof ApiError ? e.message : "Failed to reload templates.");
    } finally {
      setModal(null);
    }
  }

  const roster = templates ?? [];

  return (
    <div>
      <div style={{ display: "flex", alignItems: "center", marginBottom: 14 }}>
        <SectionLabel>
          Email templates · {roster.length} template{roster.length === 1 ? "" : "s"}
        </SectionLabel>
        <ActionButton variant="primary" style={{ marginLeft: "auto" }} onClick={() => setModal({ mode: "create" })}>
          + ADD TEMPLATE
        </ActionButton>
      </div>

      {loadError && (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: "#D55E00", padding: "6px 2px", marginBottom: 12 }}>
          {loadError}
        </div>
      )}

      {templates === null && !loadError ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 2px" }}>
          Loading templates…
        </div>
      ) : roster.length === 0 && !loadError ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 2px", lineHeight: 1.5 }}>
          No email templates pinned to this client yet.
        </div>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          {roster.map((t) => (
            <div
              key={t.id}
              onClick={() => setModal({ mode: "edit", template: t })}
              style={{
                display: "flex",
                flexDirection: "column",
                gap: 8,
                padding: "12px 14px",
                background: colors.cardBg,
                border: `1px solid ${colors.borderSubtle}`,
                borderRadius: 10,
                boxShadow: colors.shadowCard,
                cursor: "pointer",
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: 9 }}>
                <span
                  style={{
                    fontFamily: fonts.body,
                    fontSize: 13.5,
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
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <ServiceChip svc={svcForServiceType(t.serviceType)} />
              </div>
              <div
                style={{
                  fontFamily: fonts.body,
                  fontSize: 12,
                  color: colors.textDim,
                  minWidth: 0,
                  whiteSpace: "nowrap",
                  overflow: "hidden",
                  textOverflow: "ellipsis",
                }}
              >
                {t.subject}
              </div>
            </div>
          ))}
        </div>
      )}

      {modal && (
        <EmailTemplateModal
          existing={modal.mode === "edit" ? modal.template : null}
          lockedClient={modal.mode === "create" ? { id: clientId, name: clientName } : undefined}
          onClose={() => setModal(null)}
          onSaved={onTemplateSaved}
        />
      )}
    </div>
  );
}
