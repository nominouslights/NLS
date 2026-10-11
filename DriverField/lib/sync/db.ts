// ---------------------------------------------------------------------------
// The offline seam — the IndexedDB behind the queue and the ledger cache.
//
// One database, `nl-driverfield`, two object stores:
//   commands  QueuedCommand rows keyed by their client-generated id (lib/sync/queue.ts)
//   ledger    one row, the last server copy of the driver's hours (lib/hos/ledger.ts), so a
//             reload in a dead zone still shows figures rather than "No ledger"
//
// IndexedDB and not localStorage, by design (DriverField/CLAUDE.md): a queued DVIR or duty
// event is a compliance record, and localStorage is synchronous, small and string-only.
// IndexedDB is also EVICTABLE unless persistence is granted, so the first write asks for
// navigator.storage.persist() — a queued record vanishing because the browser reclaimed space
// is a compliance failure, not a lost draft. Refusal is recorded, never swallowed silently.
//
// Thin promise wrappers over the raw API, nothing more. No library: a fourth runtime
// dependency is not worth two object stores. Every function tolerates an environment with no
// `indexedDB` (the server render, a locked-down WebView) by rejecting with a plain Error, so
// callers decide — the queue refuses to pretend a command is held when it is not.
// ---------------------------------------------------------------------------

export const DB_NAME = "nl-driverfield";
export const DB_VERSION = 1;
export const COMMANDS_STORE = "commands";
export const LEDGER_STORE = "ledger";

export type StoreName = typeof COMMANDS_STORE | typeof LEDGER_STORE;

/** Whether this environment has IndexedDB at all. False during SSR. */
export function available(): boolean {
  return typeof indexedDB !== "undefined" && indexedDB !== null;
}

let opening: Promise<IDBDatabase> | null = null;

function open(): Promise<IDBDatabase> {
  if (!available()) {
    return Promise.reject(new Error("IndexedDB is not available in this environment."));
  }
  if (opening) return opening;

  opening = new Promise<IDBDatabase>((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, DB_VERSION);
    req.onupgradeneeded = () => {
      const db = req.result;
      if (!db.objectStoreNames.contains(COMMANDS_STORE)) db.createObjectStore(COMMANDS_STORE, { keyPath: "id" });
      if (!db.objectStoreNames.contains(LEDGER_STORE)) db.createObjectStore(LEDGER_STORE, { keyPath: "key" });
    };
    req.onsuccess = () => {
      const db = req.result;
      // If another tab upgrades or the database is deleted, drop our handle so the next call
      // reopens rather than failing forever on a closed connection.
      db.onversionchange = () => {
        db.close();
        opening = null;
      };
      db.onclose = () => {
        opening = null;
      };
      resolve(db);
    };
    req.onerror = () => {
      opening = null;
      reject(req.error ?? new Error("Could not open IndexedDB."));
    };
    req.onblocked = () => {
      opening = null;
      reject(new Error("IndexedDB open was blocked by another connection."));
    };
  });
  return opening;
}

function awaitRequest<T>(req: IDBRequest<T>): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error ?? new Error("IndexedDB request failed."));
  });
}

function awaitTransaction(tx: IDBTransaction): Promise<void> {
  return new Promise<void>((resolve, reject) => {
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error ?? new Error("IndexedDB transaction failed."));
    tx.onabort = () => reject(tx.error ?? new Error("IndexedDB transaction aborted."));
  });
}

export async function getAll<T>(store: StoreName): Promise<T[]> {
  const db = await open();
  const tx = db.transaction(store, "readonly");
  return awaitRequest(tx.objectStore(store).getAll() as IDBRequest<T[]>);
}

export async function get<T>(store: StoreName, key: string): Promise<T | undefined> {
  const db = await open();
  const tx = db.transaction(store, "readonly");
  return awaitRequest(tx.objectStore(store).get(key) as IDBRequest<T | undefined>);
}

/** Resolves only once the write is COMMITTED — "held on this device" must mean durable. */
export async function put<T>(store: StoreName, value: T): Promise<void> {
  const db = await open();
  const tx = db.transaction(store, "readwrite");
  tx.objectStore(store).put(value);
  await awaitTransaction(tx);
}

export async function remove(store: StoreName, key: string): Promise<void> {
  const db = await open();
  const tx = db.transaction(store, "readwrite");
  tx.objectStore(store).delete(key);
  await awaitTransaction(tx);
}

export async function clear(store: StoreName): Promise<void> {
  const db = await open();
  const tx = db.transaction(store, "readwrite");
  tx.objectStore(store).clear();
  await awaitTransaction(tx);
}

let persistenceRequested: Promise<boolean | null> | null = null;

/**
 * Asks the browser to exempt this origin's storage from eviction. Asked once per page load,
 * on the first durable write. Resolves true when granted, false when refused, null when the
 * API does not exist (an old WebView). Never throws.
 */
export function requestPersistence(): Promise<boolean | null> {
  if (persistenceRequested) return persistenceRequested;
  persistenceRequested = (async () => {
    try {
      if (typeof navigator === "undefined" || !navigator.storage?.persist) return null;
      return await navigator.storage.persist();
    } catch {
      return null;
    }
  })();
  return persistenceRequested;
}

/** Test seam: closes the handle and deletes the whole database, so a suite starts clean. */
export async function deleteDatabaseForTests(): Promise<void> {
  if (!available()) return;
  if (opening) {
    try {
      (await opening).close();
    } catch {
      // already closed
    }
    opening = null;
  }
  persistenceRequested = null;
  await new Promise<void>((resolve, reject) => {
    const req = indexedDB.deleteDatabase(DB_NAME);
    req.onsuccess = () => resolve();
    req.onerror = () => reject(req.error ?? new Error("Could not delete IndexedDB."));
    req.onblocked = () => resolve();
  });
}
