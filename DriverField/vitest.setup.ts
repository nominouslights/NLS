// Runs before every test file (vitest.config.mts `setupFiles`).
//
// jsdom has no IndexedDB, and the command queue (lib/sync/queue.ts) and the ledger cache
// (lib/hos/ledger.ts) refuse to pretend without one — enqueue() rejects rather than hold a
// command in memory only. `fake-indexeddb/auto` installs an in-memory implementation on the
// global, so the real queue runs in every test exactly as it does on the tablet, durability
// included (a test can forget the in-memory mirror and reload from the fake database, which
// is what "survives a reload" means in lib/sync/queue.test.ts).
import "fake-indexeddb/auto";
