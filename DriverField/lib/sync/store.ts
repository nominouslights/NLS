// ---------------------------------------------------------------------------
// The offline seam — local-first read store. NO-OP IMPLEMENTATION.
//
// Today: proxies straight to lib/data.ts, so every screen already reads through the boundary it
// will keep.
//
// Later (the offline batch): IndexedDB object stores, hydrated from the API when connectivity
// allows. Reads always hit local; the network becomes a sync concern, never a read path — a
// driver must never wait on a dead zone to see their own manifest.
//
// Scope discipline (architecture §8): sync only the subset of data this driver's assignments
// need, never the whole database. A field tablet that has cached every trip in the fleet is a
// data-residency problem, not a performance win.
//
// The queue beside this file is already on IndexedDB (lib/sync/db.ts, which asks for
// navigator.storage.persist() on the first write, because an evicted queued DVIR is a
// COMPLIANCE failure, not a lost draft). The read store is the half still to do; the ledger
// (lib/hos/ledger.ts) keeps its own cached server copy in the same database meanwhile.
// ---------------------------------------------------------------------------

import * as data from "../data";

/** The collections this app keeps locally. Each maps to a lib/data.ts export today. */
export type Collection = keyof typeof data;

/**
 * Reads one collection. Synchronous-looking by design: the offline implementation resolves from
 * local storage, so callers never gain a loading state they would then have to unwind.
 */
export function read<T>(collection: Collection): T {
  return data[collection] as T;
}

/**
 * Persists a collection locally. A no-op in this scaffold — lib/data.ts is a module-level
 * constant and screens treat it as read-only.
 */
export async function write<T>(collection: Collection, rows: T): Promise<void> {
  // Offline batch: put rows into the IndexedDB object store for this collection.
  void collection;
  void rows;
}
