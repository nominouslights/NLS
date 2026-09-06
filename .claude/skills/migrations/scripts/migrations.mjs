#!/usr/bin/env node
// Pending-migration check, explanation, and apply for the Northern Link backend. Zero dependencies.
//
//   check [--force]   — silent + exit 0 when every module schema is current;
//                       one line per pending migration + exit 1 otherwise
//   explain [--json]  — the full request: real DDL, additive/breaking verdict, and the commit
//                       that introduced each pending migration (implies check, ignores cache)
//   apply <Module>    — dotnet ef database update for one module, then re-verify
//   classify <file>   — parse one migration .cs and print its operations + verdict; no database,
//                       no dotnet. Exists so the severity rules stay checkable against real files.
//   pr-hook           — PostToolUse payload on stdin; nudges Claude after `gh pr create`,
//                       `gh pr merge`, `git merge` or `git pull` when migrations are pending
//
// Nothing here applies anything on its own. `apply` exists so that the human's "yes" maps to one
// explicit command per module, never a blanket "migrate everything".
//
// Why this script exists: Migrations__RunOnStartup is false in every local mode, so no run of the
// API ever applies a migration. The DB silently lags the model until a query throws 42703. See
// .claude/skills/migrations/SKILL.md.

import { spawn, execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = process.env.CLAUDE_PROJECT_DIR
  ? path.resolve(process.env.CLAUDE_PROJECT_DIR)
  : path.resolve(scriptDir, '..', '..', '..', '..');
const stateDir = path.join(repoRoot, '.claude', 'state');
const cacheFile = path.join(stateDir, 'migrations-check.json');
const launchSettings = path.join(
  repoRoot, 'Backend', 'src', 'Api', 'NorthernLink.Api', 'Properties', 'launchSettings.json');
const apiProject = 'Backend/src/Api/NorthernLink.Api/NorthernLink.Api.csproj';

// EF operation → blast radius. The split drives the confirmation the skill asks for: `additive`
// is a plain yes/no, everything else has to be spelled out and explicitly overridden.
const BREAKING_OPS = new Set([
  'DropColumn', 'DropTable', 'DropSchema', 'DropPrimaryKey', 'DropUniqueConstraint',
  'DropForeignKey', 'DropCheckConstraint', 'DropSequence',
  'RenameColumn', 'RenameTable', 'RenameIndex', 'RenameSequence',
]);
const REVIEW_OPS = new Set([
  'AlterColumn', 'AlterTable', 'AlterDatabase', 'Sql',
  'InsertData', 'UpdateData', 'DeleteData', 'DropIndex',
]);
const ADDITIVE_OPS = new Set([
  'AddColumn', 'CreateTable', 'CreateIndex', 'AddForeignKey', 'AddPrimaryKey',
  'AddUniqueConstraint', 'AddCheckConstraint', 'EnsureSchema', 'CreateSequence', 'RestartSequence',
]);

const git = (...args) =>
  execFileSync('git', ['-C', repoRoot, ...args], { encoding: 'utf8', maxBuffer: 32 * 1024 * 1024 });

/** Module libraries own a schema each; Shared's ModuleDbContext sits elsewhere and is skipped. */
function discoverModules() {
  const srcDir = path.join(repoRoot, 'Backend', 'src');
  const modules = [];
  const entries = fs.readdirSync(srcDir, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name));
  for (const entry of entries) {
    if (!entry.isDirectory()) continue;
    const persistence = path.join(srcDir, entry.name, 'Infrastructure', 'Persistence');
    if (!fs.existsSync(persistence)) continue;
    const context = fs.readdirSync(persistence).find((f) => f.endsWith('DbContext.cs'));
    const csproj = path.join(srcDir, entry.name, `NorthernLink.${entry.name}.csproj`);
    if (!context || !fs.existsSync(csproj)) continue;
    modules.push({
      name: entry.name,
      context: context.replace(/\.cs$/, ''),
      project: `Backend/src/${entry.name}/NorthernLink.${entry.name}.csproj`,
      migrationsDir: path.join(persistence, 'Migrations'),
    });
  }
  return modules;
}

/**
 * The connection string exists in exactly one place on a dev machine: the gitignored
 * launchSettings.json. Nothing is read from appsettings.json — that is the whole point of the
 * env-var-only secrets convention.
 */
function loadEnv() {
  if (!fs.existsSync(launchSettings)) return null;
  const profiles = JSON.parse(fs.readFileSync(launchSettings, 'utf8').replace(/^﻿/, '')).profiles ?? {};
  const vars = (profiles.http ?? Object.values(profiles)[0])?.environmentVariables;
  return vars && vars['ConnectionStrings__Postgres'] ? vars : null;
}

/**
 * dotnet ef echoes the connection string into its own error messages. Every captured byte goes
 * through here before it can reach stdout, a log, or Claude's context.
 */
function makeRedactor(vars) {
  const secrets = Object.entries(vars ?? {})
    .filter(([k]) => /password|key|secret|token|connectionstrings/i.test(k))
    .map(([, v]) => v)
    .filter((v) => typeof v === 'string' && v.length > 7)
    .sort((a, b) => b.length - a.length);
  return (text) => {
    let out = String(text ?? '');
    for (const secret of secrets) out = out.split(secret).join('«redacted»');
    return out.replace(/(Password|Username|User ID)=[^;\s"']+/gi, '$1=«redacted»');
  };
}

function run(args, vars, redact) {
  return new Promise((resolve) => {
    const child = spawn('dotnet', args, {
      cwd: repoRoot,
      env: { ...process.env, ...vars },
      shell: process.platform === 'win32',
      windowsHide: true,
    });
    let out = '', err = '';
    child.stdout.on('data', (d) => { out += d; });
    child.stderr.on('data', (d) => { err += d; });
    child.on('close', (code) => resolve({ code, out: redact(out), err: redact(err) }));
    child.on('error', (e) => resolve({ code: -1, out: redact(out), err: redact(String(e)) }));
  });
}

const efArgs = (module, ...rest) => [
  'ef', ...rest,
  '--context', module.context,
  '--project', module.project,
  '--startup-project', apiProject,
  '--no-build',
];

async function listMigrations(module, vars, redact) {
  const result = await run(efArgs(module, 'migrations', 'list'), vars, redact);
  if (result.code !== 0) {
    const combined = result.err + result.out;
    const hint = /(--no-build|assets|not find|Could not|MSB)/i.test(combined)
      ? 'run `dotnet build` in Backend/ first'
      : 'see error';
    const tail = (result.err || result.out).trim().split('\n').slice(-3).join(' ');
    return { module: module.name, error: `${hint}: ${tail}` };
  }
  // `dotnet ef migrations list` exits 0 even when it cannot reach the database — it then
  // prints every migration WITHOUT the "(Pending)" marker plus a warning. Treating that as
  // "all applied" is exactly the wrong answer, so it must surface as an error, not silence.
  if (/Unable to determine which migrations have been applied/i.test(result.out + result.err)) {
    const conn = /Failed to connect to ([\d.:]+)/.exec(result.out + result.err)?.[1];
    return {
      module: module.name,
      error: `database unreachable${conn ? ` (${conn})` : ''} — pending status unknown; `
        + 'check the DigitalOcean Trusted Sources firewall before assuming a code problem',
    };
  }
  const pending = [], applied = [];
  for (const line of result.out.split('\n')) {
    const match = line.trim().match(/^(\d{14}_\S+?)(\s+\(Pending\))?$/);
    if (!match) continue;
    (match[2] ? pending : applied).push(match[1]);
  }
  return { module: module.name, pending, applied };
}

// ── Migration source parsing ────────────────────────────────────────────────
// Only the hand-sized migration .cs is read — never the 1,600–2,000-line *.Designer.cs sibling
// (code-map's hard rule). Parsing happens here, mechanically, so those files never enter context.

function balancedArgs(source, openIndex) {
  let depth = 0, inString = false;
  for (let i = openIndex; i < source.length; i++) {
    const ch = source[i];
    if (inString) {
      if (ch === '\\') i++;
      else if (ch === '"') inString = false;
      continue;
    }
    if (ch === '"') inString = true;
    else if (ch === '(') depth++;
    else if (ch === ')' && --depth === 0) return source.slice(openIndex + 1, i);
  }
  return source.slice(openIndex + 1);
}

function methodBody(source, name) {
  const signature = new RegExp(`void\\s+${name}\\s*\\(MigrationBuilder`).exec(source);
  if (!signature) return '';
  const open = source.indexOf('{', signature.index);
  if (open < 0) return '';
  let depth = 0;
  for (let i = open; i < source.length; i++) {
    if (source[i] === '{') depth++;
    else if (source[i] === '}' && --depth === 0) return source.slice(open + 1, i);
  }
  return '';
}

/**
 * A named argument's value. Migrations legitimately pass a C# variable rather than a literal
 * (`foreach (var table in ...) AddColumn(table: table)`), so a non-literal is returned marked
 * with `$` instead of being reported as if it were the real identifier — the generated SQL in
 * `explain` is the authority on those, not this parse.
 */
const namedArg = (args, key) => {
  const match = new RegExp(`\\b${key}\\s*:\\s*("([^"]*)"|[A-Za-z0-9_.]+)`).exec(args);
  if (!match) return null;
  return match[2] !== undefined ? match[2] : `$${match[1]}`;
};

function parseMigration(file) {
  const source = fs.readFileSync(file, 'utf8').replace(/^﻿/, '');
  const body = methodBody(source, 'Up');
  const ops = [];
  const opRe = /migrationBuilder\.([A-Za-z]+)\s*(?:<[^>()]*>)?\s*\(/g;
  let match;
  while ((match = opRe.exec(body)) !== null) {
    const args = balancedArgs(body, opRe.lastIndex - 1);
    const op = match[1];
    const target = [namedArg(args, 'schema'), namedArg(args, 'table')].filter(Boolean).join('.');
    const column = namedArg(args, 'name');
    let severity = BREAKING_OPS.has(op) ? 'breaking'
      : REVIEW_OPS.has(op) ? 'review'
        : ADDITIVE_OPS.has(op) ? 'additive' : 'review';
    let note = null;
    // An added NOT NULL column with no default is rejected outright by Postgres on a non-empty
    // table — additive in name only.
    if (op === 'AddColumn' && /\bnullable\s*:\s*false/.test(args)
        && !/\bdefaultValue(Sql)?\s*:/.test(args)) {
      severity = 'breaking';
      note = 'NOT NULL with no default — fails on any non-empty table';
    }
    ops.push({ op, target: target || null, column, severity, note });
  }
  return ops;
}

/** One migration is only as safe as its worst operation. */
const verdictOf = (ops) => !ops.length ? 'no-op'
  : ops.some((o) => o.severity === 'breaking') ? 'breaking'
    : ops.some((o) => o.severity === 'review') ? 'review' : 'additive';

function migrationFile(module, id) {
  const file = path.join(module.migrationsDir, `${id}.cs`);
  return fs.existsSync(file) ? file : null;
}

/** The "why": the commit that first added this migration file, and whether it is on main yet. */
function provenance(file) {
  const relative = path.relative(repoRoot, file).split(path.sep).join('/');
  try {
    const log = git('log', '--diff-filter=A', '-1', '--format=%H%x1f%s%x1f%b', '--', relative).trim();
    if (!log) return null;
    const [sha, subject, bodyText] = log.split('\x1f');
    let onMain = null;
    for (const ref of ['main', 'origin/main']) {
      try {
        git('merge-base', '--is-ancestor', sha, ref);
        onMain = true;
        break;
      } catch {
        onMain = false;
      }
    }
    return {
      sha: sha.slice(0, 7),
      subject,
      rationale: (bodyText ?? '').split('\n\n')[0].replace(/\s+/g, ' ').trim(),
      onMain,
    };
  } catch {
    return null;
  }
}

/** The real DDL, straight from EF — not our reading of it. */
async function pendingSql(module, report, vars, redact) {
  if (!report.pending?.length) return null;
  const from = report.applied.length ? report.applied[report.applied.length - 1] : '0';
  const to = report.pending[report.pending.length - 1];
  const result = await run(efArgs(module, 'migrations', 'script', from, to), vars, redact);
  if (result.code !== 0) return null;
  return result.out
    .split('\n')
    .filter((l) => !/^\s*(START TRANSACTION|COMMIT|SELECT|BEGIN)/i.test(l))
    .filter((l) => !/__EFMigrationsHistory/.test(l) && !/^VALUES \('\d{14}_/.test(l))
    .join('\n')
    .replace(/\n{3,}/g, '\n\n')
    .trim();
}

// ── Cache ──────────────────────────────────────────────────────────────────
// A new migration means a new filename, so the set of migration filenames is a sound cache key
// for "nothing new to apply". `apply` clears it; --force bypasses it.

function fingerprint(modules) {
  const names = modules
    .flatMap((m) => (fs.existsSync(m.migrationsDir) ? fs.readdirSync(m.migrationsDir) : [])
      .filter((f) => f.endsWith('.cs') && !f.endsWith('.Designer.cs'))
      .map((f) => `${m.name}/${f}`))
    .sort()
    .join('\n');
  return crypto.createHash('sha1').update(names).digest('hex');
}

const readCache = () => {
  try { return JSON.parse(fs.readFileSync(cacheFile, 'utf8')); } catch { return null; }
};
const writeCache = (data) => {
  try {
    fs.mkdirSync(stateDir, { recursive: true });
    fs.writeFileSync(cacheFile, JSON.stringify(data, null, 2) + '\n');
  } catch { /* cache is an optimisation, never a hard dependency */ }
};
const clearCache = () => { try { fs.unlinkSync(cacheFile); } catch { /* already gone */ } };

// ── Commands ───────────────────────────────────────────────────────────────

async function gather() {
  const modules = discoverModules();
  const vars = loadEnv();
  if (!vars) {
    const rel = path.relative(repoRoot, launchSettings);
    return { fatal: `No connection string found. ${rel} is gitignored — recreate it locally before this check can run.` };
  }
  const redact = makeRedactor(vars);
  const reports = [];
  // Four at a time: each `dotnet ef` loads the whole API startup project.
  for (let i = 0; i < modules.length; i += 4) {
    const batch = modules.slice(i, i + 4).map((m) => listMigrations(m, vars, redact));
    reports.push(...await Promise.all(batch));
  }
  return { modules, vars, redact, reports };
}

async function check({ force = false, quiet = false } = {}) {
  const modules = discoverModules();
  const print = fingerprint(modules);
  if (!force) {
    const cached = readCache();
    if (cached?.fingerprint === print && cached.pending?.length === 0) return 0;
  }
  const state = await gather();
  if (state.fatal) {
    if (!quiet) console.error(state.fatal);
    return 2;
  }
  const pending = state.reports.flatMap((r) => (r.pending ?? []).map((id) => ({ module: r.module, id })));
  const errors = state.reports.filter((r) => r.error);
  // Never cache a run with per-module errors: an unreachable database would otherwise be
  // remembered as "pending: []" and every later check would silently report current.
  if (errors.length === 0) writeCache({ fingerprint: print, pending, checkedAt: new Date().toISOString() });
  else clearCache();
  if (!quiet) {
    for (const { module, id } of pending) console.log(`pending: ${module} — ${id}`);
    for (const r of errors) console.error(`error:   ${r.module} — ${r.error}`);
  }
  return pending.length || errors.length ? 1 : 0;
}

async function explain({ json = false } = {}) {
  const state = await gather();
  if (state.fatal) {
    console.error(state.fatal);
    return 2;
  }
  const { modules, vars, redact, reports } = state;
  const out = [];
  for (const report of reports) {
    if (report.error) { out.push({ module: report.module, error: report.error }); continue; }
    if (!report.pending.length) continue;
    const module = modules.find((m) => m.name === report.module);
    const migrations = report.pending.map((id) => {
      const file = migrationFile(module, id);
      const ops = file ? parseMigration(file) : [];
      return { id, verdict: verdictOf(ops), ops, provenance: file ? provenance(file) : null };
    });
    out.push({ module: report.module, migrations, sql: await pendingSql(module, report, vars, redact) });
  }
  writeCache({
    fingerprint: fingerprint(modules),
    pending: out.flatMap((m) => (m.migrations ?? []).map((x) => ({ module: m.module, id: x.id }))),
    checkedAt: new Date().toISOString(),
  });

  if (json) { console.log(JSON.stringify(out, null, 2)); return out.length ? 1 : 0; }
  if (!out.length) { console.log('Every module schema is current — nothing to apply.'); return 0; }

  const rank = { breaking: 0, review: 1, 'no-op': 2, additive: 3 };
  for (const entry of out) {
    if (entry.error) { console.log(`\n## ${entry.module}\n  error: ${entry.error}`); continue; }
    console.log(`\n## ${entry.module} — ${entry.migrations.length} pending`);
    for (const m of [...entry.migrations].sort((a, b) => rank[a.verdict] - rank[b.verdict])) {
      console.log(`\n  ${m.id}   [${m.verdict.toUpperCase()}]`);
      if (m.provenance) {
        const where = m.provenance.onMain ? 'on main' : 'NOT on main — feature branch only';
        console.log(`    why:    ${m.provenance.subject}  (${m.provenance.sha}, ${where})`);
        if (m.provenance.rationale) console.log(`            ${m.provenance.rationale}`);
      }
      if (!m.ops.length) console.log('    ops:    none — empty Up(), history row only');
      for (const op of m.ops) {
        const target = op.target ? ` on ${op.target}` : '';
        const column = op.column ? ` (${op.column})` : '';
        const note = op.note ? ` — ${op.note}` : '';
        console.log(`    op:     ${op.op}${target}${column} [${op.severity}]${note}`);
      }
    }
    if (entry.sql) {
      console.log(`\n  DDL that will run:\n${entry.sql.split('\n').map((l) => '    ' + l).join('\n')}`);
    }
    console.log(`\n  apply with: node .claude/skills/migrations/scripts/migrations.mjs apply ${entry.module}`);
  }
  return 1;
}

async function apply(moduleName) {
  const modules = discoverModules();
  const module = modules.find((m) => m.name.toLowerCase() === String(moduleName).toLowerCase());
  if (!module) {
    console.error(`Unknown module "${moduleName}". Known: ${modules.map((m) => m.name).join(', ')}`);
    return 2;
  }
  const vars = loadEnv();
  if (!vars) { console.error('No connection string — recreate launchSettings.json locally.'); return 2; }
  const redact = makeRedactor(vars);

  const before = await listMigrations(module, vars, redact);
  if (before.error) { console.error(`${module.name}: ${before.error}`); return 2; }
  if (!before.pending.length) { console.log(`${module.name}: already current — nothing applied.`); return 0; }

  console.log(`${module.name}: applying ${before.pending.length} migration(s) — ${before.pending.join(', ')}`);
  const result = await run(efArgs(module, 'database', 'update'), vars, redact);
  console.log(result.out.trim());
  clearCache();
  if (result.code !== 0) {
    console.error(result.err.trim());
    return 1;
  }
  const after = await listMigrations(module, vars, redact);
  if (after.pending?.length) {
    console.error(`${module.name}: STILL PENDING after update — ${after.pending.join(', ')}`);
    return 1;
  }
  console.log(`${module.name}: verified — no pending migrations.`);
  return 0;
}

function classify(file) {
  if (!file || !fs.existsSync(file)) {
    console.error(`usage: migrations.mjs classify <path to a migration .cs>`);
    return 2;
  }
  const ops = parseMigration(file);
  console.log(`${path.basename(file)}  [${verdictOf(ops).toUpperCase()}]`);
  if (!ops.length) console.log('  none — empty Up(), history row only');
  for (const op of ops) {
    const target = op.target ? ` on ${op.target}` : '';
    const column = op.column ? ` (${op.column})` : '';
    const note = op.note ? ` — ${op.note}` : '';
    console.log(`  ${op.op}${target}${column} [${op.severity}]${note}`);
  }
  return 0;
}

/**
 * Best-effort nudge. It cannot fire for a PR opened in the GitHub web UI, which is exactly why
 * the skill is invocable by hand too. Never fails the tool call it is attached to.
 */
function prHook() {
  let input = '';
  process.stdin.setEncoding('utf8');
  process.stdin.on('data', (d) => { input += d; });
  process.stdin.on('end', async () => {
    try {
      const command = JSON.parse(input)?.tool_input?.command ?? '';
      const trigger = /\bgh\s+pr\s+create\b/.test(command) ? 'a PR was created'
        : /\bgh\s+pr\s+merge\b/.test(command) ? 'a PR was merged'
          : /\bgit\s+(pull|merge)\b/.test(command) ? 'main moved' : null;
      if (!trigger) return;
      const code = await check({ quiet: true });
      if (code !== 1) return;
      const pending = readCache()?.pending ?? [];
      if (!pending.length) return;
      const summary = pending.map((p) => `${p.module}/${p.id}`).join(', ');
      process.stdout.write(JSON.stringify({
        hookSpecificOutput: {
          hookEventName: 'PostToolUse',
          additionalContext:
            `Migration check: ${trigger} and the shared DigitalOcean database is behind — `
            + `${pending.length} pending migration(s): ${summary}. `
            + 'Invoke the "migrations" skill now: run '
            + '`node .claude/skills/migrations/scripts/migrations.mjs explain`, present what each '
            + 'migration does and why, and ask the user before applying anything.',
        },
      }));
    } catch {
      // Never fail the hook.
    }
  });
}

const [cmd, arg] = process.argv.slice(2);
const flags = process.argv.slice(2);
if (cmd === 'check') check({ force: flags.includes('--force') }).then((c) => process.exit(c));
else if (cmd === 'explain') explain({ json: flags.includes('--json') }).then((c) => process.exit(c));
else if (cmd === 'apply') apply(arg).then((c) => process.exit(c));
else if (cmd === 'classify') process.exit(classify(arg));
else if (cmd === 'pr-hook') prHook();
else {
  console.error('usage: migrations.mjs <check [--force] | explain [--json] | apply <Module> | classify <file> | pr-hook>');
  process.exit(2);
}
