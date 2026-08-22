---
name: code-map
description: Machine-maintained index of every file in the Northern Link workspace — use it to locate code instead of exploring with find/grep/ls. Consult whenever you need to answer "where is X", "which file has Y", "find the code for Z", or before starting work in any territory (Backend, Dispatcher, Budgeting, Website, AppHost) — read that territory's reference file first and jump straight to the named paths. Also covers maintaining the map: regenerating it, checking staleness, and the code-indexer agent that refreshes prose notes.
---

# Code Map — locate code without exploring

The map lives in per-territory reference files under `references/`. **Read only the file for the
territory you're working in** — that's the point: one targeted read replaces a find/grep session,
and backend work never pays for the frontend map.

| Territory | Reference file | Owning agent |
|---|---|---|
| `Backend/` | `references/backend.md` | backend-dev |
| `Dispatcher/` | `references/dispatcher.md` | frontend-dev |
| `Budgeting/` | `references/budgeting.md` | — (copy-of-Dispatcher manifest lives in `Budgeting/CLAUDE.md`, not here) |
| `Website/`, `AppHost/`, workspace root | `references/website-apphost.md` | — |

Each section is a generated block (`<!-- gen:key -->` markers) plus a notes block
(`<!-- notes:key -->` markers) holding one-line human annotations for files whose names aren't
self-explanatory.

## Hard rule: Migrations

**Never read files under `Infrastructure/Persistence/Migrations/`.** They are generated
1,600–2,000-line EF Designer files that will flood your context. The map records the count and
latest migration name per domain — that is all you need; for schema questions read the
`*Configuration.cs` files in `Persistence/` instead.

## Maintenance

- Mechanical sections are generated — **never hand-edit outside `notes` blocks**; regeneration
  overwrites everything else.
- Regenerate (free, deterministic): `node .claude/skills/code-map/scripts/codemap.mjs generate`
- Verify freshness: `node .claude/skills/code-map/scripts/codemap.mjs check` — prints nothing and
  exits 0 when the map matches the tree; otherwise prints one `stale:` line per drifted section.
- A PostToolUse hook (`.claude/settings.json`) logs every file Claude edits to
  `.claude/state/codemap-pending.log`; a SessionStart hook runs `check` and warns when stale.
- Prose updates (the notes blocks): launch the **code-indexer** agent — it regenerates, reads the
  pending log plus git diff, touches only affected sections, and truncates the log. Don't update
  notes ad hoc mid-task; queue it for the agent.
