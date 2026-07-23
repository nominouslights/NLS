import { chipStyle, statusMeta, type StatusKind } from "@/lib/theme";
import type { ClientRecord } from "@/lib/api/clients";
import type { InteractionType } from "@/lib/types";

// Shared bits for the Client detail sub-tree (tabs, client-type gating, small
// presentational helpers) so Clients.tsx, ClientDetail, and the tab components
// agree.

export const CLIENT_TABS = ["Overview & POs", "Contacts", "Interactions"];
export const VENDOR_TABS = ["Overview & POs"];

/** The CRM roster + interaction log apply to Client-type organizations only.
 *  Vendor/Partner records don't get the CRM UI. */
export function isClientType(client: { type: string }): boolean {
  return client.type === "Client";
}

// ---------------------------------------------------------------------------
// Prototype-CRM shim — clients now come from the real Clients API (Guid ids),
// but the CRM store (lib/clientStore.ts: contacts, interactions, follow-ups)
// is still a frontend mock keyed by the old numeric prototype ids (1..5).
// Bridge them by name-match against the seed roster, falling back to roster
// position + 1 — mirroring how the Drivers cutover joined the mock HOS rows
// (components/screens/Drivers.tsx). Remove with the CRM backend phase.
// ---------------------------------------------------------------------------

const CRM_SEED_NAME_IDS: [RegExp, number][] = [
  [/alamos/i, 1],
  [/nihb/i, 2],
  [/keewatin/i, 3],
  [/community/i, 4],
  [/miller/i, 5],
];

/** Numeric prototype-CRM id for an API client (name-match, then position). */
export function mockCrmIdFor(client: ClientRecord, rosterIndex: number): number {
  for (const [re, id] of CRM_SEED_NAME_IDS) {
    if (re.test(client.name)) return id;
  }
  return rosterIndex + 1;
}

/** mock CRM id → API client, for widgets keyed by clientStore rows (e.g. the
 *  overview follow-ups list). */
export function crmClientMap(roster: ClientRecord[]): Map<number, ClientRecord> {
  const map = new Map<number, ClientRecord>();
  roster.forEach((c, i) => {
    const mockId = mockCrmIdFor(c, i);
    if (!map.has(mockId)) map.set(mockId, c);
  });
  return map;
}

export interface TypeMeta {
  glyph: string;
  bg: string;
  bd: string;
  tx: string;
}

const TYPE_META: Record<InteractionType, TypeMeta> = {
  Call: { glyph: "☎", bg: "rgba(31,111,178,.08)", bd: "rgba(31,111,178,.32)", tx: "#1F6FB2" },
  Meeting: { glyph: "◆", bg: "rgba(23,119,158,.09)", bd: "rgba(23,119,158,.35)", tx: "#17779E" },
  Email: { glyph: "✉", bg: "rgba(72,101,129,.08)", bd: "rgba(72,101,129,.3)", tx: "#40566C" },
  "Site Visit": { glyph: "⚑", bg: "rgba(0,158,115,.10)", bd: "rgba(0,158,115,.38)", tx: "#007A59" },
  Other: { glyph: "●", bg: "rgba(86,113,140,.08)", bd: "rgba(86,113,140,.3)", tx: "#4C6378" },
};

export function InteractionTypeChip({ type }: { type: InteractionType }) {
  const m = TYPE_META[type] ?? TYPE_META.Other;
  return (
    <span style={chipStyle(m.bg, m.bd, m.tx)}>
      <span style={{ fontSize: 10, lineHeight: 1 }}>{m.glyph}</span>
      {type}
    </span>
  );
}

/** Follow-up chip label for a status kind derived from its due date. */
export function followUpLabel(kind: StatusKind): string {
  if (kind === "over") return "Overdue";
  if (kind === "soon") return "Due soon";
  return "Scheduled";
}

/** PO expiry chip label for a status kind derived from its expiry date. */
export function poExpiryLabel(kind: StatusKind): string {
  if (kind === "over") return "Expired";
  if (kind === "soon") return "Expiring soon";
  return "Valid";
}

export { statusMeta };
