"use client";

import { useSyncExternalStore } from "react";
import type { ClientContact, ClientInteraction } from "./types";

// -----------------------------------------------------------------------------
// Client CRM prototype store — MOCK ONLY (no backend CRM domain yet).
//
// Contact roster and interaction (touchpoint) log for the Clients & Contracts
// screen. A module-level store (not per-component state) so edits survive
// navigating away from and back to the screen, which Console.tsx unmounts on
// nav — the same pattern as lib/maintenanceStore.ts.
//
// The client roster, contracts, and purchase orders now come from the real
// Clients API (lib/api/clients.ts). The CRM rows here are still keyed by the
// old numeric prototype client ids (1..5) — joined to API clients by the
// prototype-CRM shim in components/screens/clients/shared.tsx.
//
// NOTE: intentionally NO relationship health / happiness / satisfaction data —
// see the note in lib/types.ts.
// -----------------------------------------------------------------------------

// ---- id counters ------------------------------------------------------------
// Declared before the seed arrays so the mutation helpers and any init-time id
// use share one monotonic source (seed ids below are hard-coded so interactions
// can reference specific contacts).

const counters = { ct: 110, ix: 500 };
function nextId(kind: keyof typeof counters, prefix: string): string {
  counters[kind] += 1;
  return `${prefix}-${counters[kind]}`;
}

// ---- seed: contact roster (migrated from data.ts tuples) --------------------

export const contacts: ClientContact[] = [
  // Alamos Gold (client 1)
  { id: "CT-101", clientId: 1, name: "Quinton Falk", title: "Site operations", email: "qfalk@alamosgold.com", phone: "(204) 555-0142", primary: true },
  { id: "CT-102", clientId: 1, name: "Brendan D'Allaire", title: "Contract decision-maker", email: "bdallaire@alamosgold.com", phone: "(204) 555-0187", notes: "Owns the shuttle contract renewal — separate conversation from operations.", primary: false },
  { id: "CT-103", clientId: 1, name: "Daniel Lavallée", title: "Compliance", email: "dlavallee@alamosgold.com", primary: false },
  { id: "CT-104", clientId: 1, name: "LL Contractor Clearances", title: "Clearance desk", email: "clearances@alamosgold.com", notes: "Per-driver contractor clearance workflow (June 2026 incident).", primary: false },
  { id: "CT-105", clientId: 1, name: "Accounts Payable", title: "AP / invoice routing", email: "ap@alamosgold.com", primary: false },
  // NIHB Medical Transport (client 2)
  { id: "CT-106", clientId: 2, name: "NIHB Regional Office", title: "Voucher authorization", phone: "(204) 555-0210", primary: true },
  { id: "CT-107", clientId: 2, name: "Patient Travel Desk", title: "Booking coordination", phone: "(204) 555-0215", primary: false },
  // Keewatin Tribal Council (client 3)
  { id: "CT-108", clientId: 3, name: "Council Transport Lead", title: "Charter requests", email: "transport@ktc.ca", primary: true },
  // Community Riders (client 4)
  { id: "CT-109", clientId: 4, name: "Booking line", title: "Individual bookings", primary: true },
  // Miller the Mover (client 5 — Vendor/Partner: migrated, but no CRM UI yet)
  { id: "CT-110", clientId: 5, name: "Dispatch Liaison", title: "Driver supply", primary: true },
];

// ---- seed: interaction log --------------------------------------------------
// Dates are relative to the current prototype date (2026-07-15) so the overview
// follow-ups widget shows a realistic mix of overdue / due-soon / upcoming.

export const interactions: ClientInteraction[] = [
  {
    id: "IX-503",
    clientId: 1,
    date: "2026-07-10",
    type: "Meeting",
    summary: "Q3 crew-shuttle scheduling review; walked through the contractor-clearance backlog and driver roster changes.",
    participantContactIds: ["CT-101", "CT-103"],
    followUpDate: "2026-07-20",
    followUpNote: "Confirm updated per-driver clearance list before the renewal meeting.",
  },
  {
    id: "IX-502",
    clientId: 1,
    date: "2026-06-28",
    type: "Call",
    summary: "Contract renewal terms — Brendan flagged the rate-escalation clause for review.",
    participantContactIds: ["CT-102"],
    followUpDate: "2026-07-18",
    followUpNote: "Send revised rate schedule for the 2026-08 renewal.",
  },
  {
    id: "IX-501",
    clientId: 1,
    date: "2026-05-15",
    type: "Email",
    summary: "Invoice routing switched to the Alamos AP portal.",
    participantContactIds: ["CT-105"],
  },
  {
    id: "IX-504",
    clientId: 2,
    date: "2026-07-02",
    type: "Site Visit",
    summary: "Patient Travel Desk coordination walk-through; reviewed escort travel policy.",
    participantContactIds: ["CT-107"],
    followUpDate: "2026-08-05",
    followUpNote: "Share updated escort policy one-pager.",
  },
  {
    id: "IX-505",
    clientId: 3,
    date: "2026-06-20",
    type: "Call",
    summary: "Charter request cadence for the fall season.",
    participantContactIds: ["CT-108"],
    followUpDate: "2026-07-05",
    followUpNote: "Confirm October multi-community charter dates.",
  },
];

// Purchase orders now come from the real Clients API (lib/api/clients.ts).

// ---- queries ----------------------------------------------------------------

/** Contacts for a client, primary first then alphabetical. */
export function contactsFor(clientId: number): ClientContact[] {
  return contacts
    .filter((c) => c.clientId === clientId)
    .sort((a, b) => (a.primary === b.primary ? a.name.localeCompare(b.name) : a.primary ? -1 : 1));
}

export function primaryContactFor(clientId: number): ClientContact | undefined {
  return contacts.find((c) => c.clientId === clientId && c.primary);
}

/** Interaction timeline for a client, newest first. */
export function timelineFor(clientId: number): ClientInteraction[] {
  return [...interactions.filter((i) => i.clientId === clientId)].sort((a, b) => (a.date < b.date ? 1 : -1));
}

/** Every open follow-up across all clients, soonest due first (overdue first).
 *  Drives the "upcoming follow-ups" widget on the clients overview. */
export function openFollowUps(): ClientInteraction[] {
  return [...interactions.filter((i) => i.followUpDate)].sort((a, b) =>
    (a.followUpDate as string) < (b.followUpDate as string) ? -1 : 1,
  );
}

// ---- mutations --------------------------------------------------------------

/** Add a contact. First contact for a client is forced primary; a new primary
 *  demotes the previous one so exactly one primary exists per client. */
export function addContact(input: Omit<ClientContact, "id">): ClientContact {
  const existing = contacts.filter((c) => c.clientId === input.clientId);
  const primary = existing.length === 0 ? true : input.primary;
  if (primary) demotePrimaries(input.clientId);
  const contact: ClientContact = { ...input, primary, id: nextId("ct", "CT") };
  contacts.push(contact);
  emit();
  return contact;
}

/** Update a contact. Setting primary true demotes the previous primary. */
export function updateContact(id: string, patch: Partial<Omit<ClientContact, "id" | "clientId">>): void {
  const contact = contacts.find((c) => c.id === id);
  if (!contact) return;
  if (patch.primary) demotePrimaries(contact.clientId, id);
  Object.assign(contact, patch);
  emit();
}

/** Promote one contact to primary, demoting the rest for that client. */
export function setPrimaryContact(clientId: number, contactId: string): void {
  let changed = false;
  for (const c of contacts) {
    if (c.clientId !== clientId) continue;
    const shouldBe = c.id === contactId;
    if (c.primary !== shouldBe) {
      c.primary = shouldBe;
      changed = true;
    }
  }
  if (changed) emit();
}

function demotePrimaries(clientId: number, exceptId?: string): void {
  for (const c of contacts) {
    if (c.clientId === clientId && c.id !== exceptId) c.primary = false;
  }
}

export function addInteraction(input: Omit<ClientInteraction, "id">): ClientInteraction {
  const interaction: ClientInteraction = { ...input, id: nextId("ix", "IX") };
  interactions.unshift(interaction);
  emit();
  return interaction;
}

// ---- subscription (useSyncExternalStore) ------------------------------------

let version = 0;
const listeners = new Set<() => void>();

function emit(): void {
  version += 1;
  listeners.forEach((l) => l());
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** Subscribe a component to store mutations. Returns a version number that
 *  changes on every mutation, forcing a re-read of the exported arrays. */
export function useClientStore(): number {
  return useSyncExternalStore(
    subscribe,
    () => version,
    () => version,
  );
}
