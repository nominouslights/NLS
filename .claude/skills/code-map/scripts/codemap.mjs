#!/usr/bin/env node
// Code map for the Northern Link workspace. Zero dependencies.
//
//   generate — rewrite .claude/skills/code-map/references/*.md from the working tree
//   check    — silent + exit 0 when the map matches the tree; print stale sections, exit 1 otherwise
//   log      — append the edited file path from a PostToolUse hook payload (stdin JSON)
//              to .claude/state/codemap-pending.log
//
// Mechanical sections live inside <!-- gen:key:start/end --> markers and are owned by this
// script. Prose lives inside <!-- notes:key:start/end --> markers and survives regeneration —
// the code-indexer agent maintains those.

import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, '..', '..', '..', '..');
const refsDir = path.resolve(scriptDir, '..', 'references');
const stateDir = path.join(repoRoot, '.claude', 'state');
const pendingLog = path.join(stateDir, 'codemap-pending.log');

const TERRITORIES = ['Backend/', 'Dispatcher/', 'Budgeting/', 'Website/', 'AppHost/'];
const KNOWN_TOP = new Set(['Backend', 'Dispatcher', 'Budgeting', 'Website', 'AppHost', '.github']);
const REF_FILES = ['backend.md', 'dispatcher.md', 'budgeting.md', 'website-apphost.md'];
const MIGRATIONS_WARNING =
  'Migrations: DO NOT READ files under Infrastructure/Persistence/Migrations/ — generated 1,600–2,000-line Designer files. The map records count + latest name only.';

function git(...args) {
  return execFileSync('git', ['-C', repoRoot, ...args], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
}

function listPaths() {
  return git('ls-files', '--cached', '--others', '--exclude-standard', '-z')
    .split('\0')
    .filter((p) => p && !p.startsWith('.claude/'))
    .sort();
}

const hashOf = (paths) => crypto.createHash('sha1').update(paths.join('\n')).digest('hex');
const under = (paths, prefix) => paths.filter((p) => p.startsWith(prefix)).map((p) => p.slice(prefix.length));
const topDirs = (paths) => [...new Set(paths.filter((p) => p.includes('/')).map((p) => p.slice(0, p.indexOf('/'))))].sort();
const directFiles = (paths) => paths.filter((p) => !p.includes('/')).sort();
const base = (p) => p.slice(p.lastIndexOf('/') + 1);
const stripExt = (f) => f.replace(/\.(cs|tsx|ts|mjs)$/, '');

function countLines(relPath) {
  try {
    return fs.readFileSync(path.join(repoRoot, relPath), 'utf8').split('\n').length;
  } catch {
    return 0;
  }
}

// ---------------------------------------------------------------------------
// Section builders. Each returns { key, heading, lines }.
// ---------------------------------------------------------------------------

function backendDomainSection(name, all) {
  const key = name.toLowerCase();
  const files = under(all, `Backend/src/${name}/`);
  const mig = files.filter((p) => p.includes('/Migrations/'));
  const nonMig = files.filter((p) => !p.includes('/Migrations/'));

  if (files.length <= 4) {
    return {
      key,
      heading: `${name} — Backend/src/${name} (stub, ${files.length} files)`,
      lines: [`Files: ${files.join(', ')}`, 'Stub module — DI wired, no domain code or endpoints yet.'],
    };
  }

  const lines = [];
  const dom = under(files, 'Domain/');
  const aggregates = topDirs(dom);
  if (aggregates.length) {
    lines.push(
      `Aggregates (Domain/): ${aggregates.join(' · ')}`,
      '  (each: <Aggregate>.cs, <Aggregate>Errors.cs, enums/value objects, Events/)'
    );
  }

  const app = under(files, 'Application/');
  const areas = topDirs(app).filter((a) => a !== 'Abstractions');
  if (areas.length) {
    lines.push('Use-case slices (Application/<Area>/<Slice>/ = Command|Query + Handler):', '| Area | Slices |', '|---|---|');
    const areaTypes = [];
    for (const area of areas) {
      const ap = under(app, `${area}/`);
      const slices = topDirs(ap);
      lines.push(`| ${area} | ${slices.join(', ') || '—'} |`);
      const direct = directFiles(ap).map(stripExt);
      if (direct.length) areaTypes.push(`${area}/: ${direct.join(', ')}`);
    }
    for (const t of areaTypes) lines.push(`Area-level types — ${t}`);
  }
  const abstractions = app.filter((p) => p.startsWith('Abstractions/'));
  if (abstractions.length) {
    lines.push(`Abstractions: Application/Abstractions/ — ${abstractions.length} repository/read-service interfaces`);
  }

  const infra = under(files, 'Infrastructure/');
  if (infra.length) {
    lines.push('Infrastructure:');
    const diRoot = infra.find((p) => p.endsWith('ServiceCollectionExtensions.cs'));
    if (diRoot) lines.push(`  DI root: Infrastructure/${diRoot}`);
    const endpoints = infra.filter((p) => p.startsWith('Endpoints/')).map((p) => base(p));
    if (endpoints.length) lines.push(`  Endpoints: ${endpoints.sort().join(', ')}`);
    const pers = under(infra, 'Persistence/').filter((p) => !p.startsWith('Migrations/'));
    if (pers.length) {
      const dbctx = pers.filter((p) => /DbContext(Factory)?\.cs$/.test(p)).map((p) => stripExt(base(p)));
      const kind = (suffix) => pers.filter((p) => !p.includes('/') && p.endsWith(suffix) && !/DbContext/.test(p)).length;
      const parts = [dbctx.join(', ')];
      const cfg = kind('Configuration.cs'); if (cfg) parts.push(`${cfg}×Configuration`);
      const repo = kind('Repository.cs'); if (repo) parts.push(`${repo}×Repository`);
      const read = kind('ReadService.cs'); if (read) parts.push(`${read}×ReadService`);
      const rm = pers.filter((p) => p.startsWith('ReadModels/')).length; if (rm) parts.push(`ReadModels (${rm})`);
      const proj = pers.filter((p) => p.startsWith('Projections/')).length; if (proj) parts.push(`Projections (${proj})`);
      const otherPers = topDirs(pers).filter((d) => !['ReadModels', 'Projections'].includes(d));
      for (const d of otherPers) parts.push(`${d}/ (${pers.filter((p) => p.startsWith(d + '/')).length})`);
      lines.push(`  Persistence: ${parts.filter(Boolean).join(', ')}`);
    }
    const otherInfra = topDirs(infra).filter((d) => !['Endpoints', 'Persistence'].includes(d));
    for (const d of otherInfra) {
      lines.push(`  ${d}/: ${infra.filter((p) => p.startsWith(d + '/')).map((p) => base(p)).join(', ')}`);
    }
    if (mig.length) {
      const latest = mig.map(base).filter((f) => /^\d+_/.test(f) && !f.includes('.Designer.')).sort().pop();
      lines.push(`  Migrations: ${mig.length} files, latest ${latest ? stripExt(latest) : '—'} — DO NOT READ (generated)`);
    }
  }

  return {
    key,
    heading: `${name} — Backend/src/${name} (${nonMig.length} files, +${mig.length} migration files)`,
    lines,
  };
}

function backendSharedSection(all) {
  const files = under(all, 'Backend/src/Shared/');
  const lines = [];
  const listDirs = ['Kernel', 'Messaging', 'EventBus'];
  for (const d of listDirs) {
    const inDir = under(files, `${d}/`);
    if (inDir.length) lines.push(`${d}/: ${inDir.map((p) => stripExt(base(p))).sort().join(', ')}`);
  }
  const ie = under(files, 'IntegrationEvents/');
  if (ie.length) {
    const perDomain = topDirs(ie).map((d) => `${d} (${ie.filter((p) => p.startsWith(d + '/')).length})`);
    lines.push(`IntegrationEvents/ (the only cross-domain contract surface): ${perDomain.join(', ')}`);
  }
  for (const d of topDirs(files).filter((d) => ![...listDirs, 'IntegrationEvents'].includes(d))) {
    lines.push(`${d}/ (${files.filter((p) => p.startsWith(d + '/')).length}): ${topDirs(under(files, d + '/')).map((s) => s + '/').join(', ') || 'flat'}`);
  }
  return { key: 'shared', heading: `Shared kernel — Backend/src/Shared (${files.length} files)`, lines };
}

function backendApiSection(all) {
  const files = under(all, 'Backend/src/Api/NorthernLink.Api/');
  return {
    key: 'api',
    heading: `Api gateway — Backend/src/Api/NorthernLink.Api (${files.length} files)`,
    lines: [
      'Composition root: Program.cs — DI + endpoint registration for every domain library.',
      `Files: ${files.sort().join(', ')}`,
    ],
  };
}

function backendTestsSection(all) {
  const files = under(all, 'Backend/tests/');
  const projects = topDirs(files);
  const lines = ['| Project | Files |', '|---|---|'];
  for (const p of projects) {
    const n = files.filter((f) => f.startsWith(p + '/')).length;
    const flag = p.includes('Architecture') ? ' (enforces domain-library boundaries)' : '';
    lines.push(`| ${p}${flag} | ${n} |`);
  }
  return { key: 'tests', heading: `Tests — Backend/tests (${projects.length} projects, ${files.length} files)`, lines };
}

function screensSection(appName, all) {
  const prefix = `${appName}/components/screens/`;
  const files = under(all, prefix);
  const top = directFiles(files);
  const lines = ['| Screen | ~Lines |', '|---|---|'];
  for (const f of top) {
    const n = countLines(prefix + f);
    const bucket = Math.round(n / 100) * 100;
    const flag = n > 800 ? ' — LARGE, read in targeted slices' : '';
    lines.push(`| ${f} | ~${bucket}${flag} |`);
  }
  const subs = topDirs(files);
  if (subs.length) {
    lines.push(`Sub-screen folders: ${subs.map((d) => `screens/${d}/ (${files.filter((p) => p.startsWith(d + '/')).length})`).join(', ')}`);
  }
  return {
    key: `${appName.toLowerCase()}-screens`,
    heading: `Screens — ${appName}/components/screens (${top.length} top-level)`,
    lines,
  };
}

function componentsSection(appName, all) {
  const prefix = `${appName}/components/`;
  const files = under(all, prefix);
  const lines = [];
  const appFiles = under(all, `${appName}/app/`);
  if (appFiles.length) lines.push(`app/: ${appFiles.sort().join(', ')}`);
  const top = directFiles(files).map(stripExt);
  if (top.length) lines.push(`Top-level: ${top.join(', ')}`);
  const ui = under(files, 'ui/').map(stripExt).sort();
  if (ui.length) lines.push(`ui/ (${ui.length}): ${ui.join(', ')}`);
  const feature = topDirs(files).filter((d) => !['screens', 'ui'].includes(d));
  for (const d of feature) {
    lines.push(`${d}/ (${files.filter((p) => p.startsWith(d + '/')).length}): ${under(files, d + '/').filter((p) => !p.includes('/')).map(stripExt).join(', ')}`);
  }
  return {
    key: `${appName.toLowerCase()}-components`,
    heading: `Components — ${appName}/components`,
    lines,
  };
}

function libSection(appName, all) {
  const prefix = `${appName}/lib/`;
  const files = under(all, prefix);
  const lines = [];
  const api = under(files, 'api/').map(stripExt).sort();
  if (api.length) lines.push(`api/ (${api.length}): ${api.join(', ')}`);
  const docs = under(files, 'documents/');
  if (docs.length) lines.push(`documents/ (PDF generators): ${topDirs(docs).join(', ')} (each index/sections/styles)`);
  const top = directFiles(files).map(stripExt);
  if (top.length) lines.push(`Top-level: ${top.join(', ')}`);
  for (const d of topDirs(files).filter((d) => !['api', 'documents'].includes(d))) {
    lines.push(`${d}/ (${files.filter((p) => p.startsWith(d + '/')).length})`);
  }
  return { key: `${appName.toLowerCase()}-lib`, heading: `Lib — ${appName}/lib (${files.length} files)`, lines };
}

function budgetingTestsSection(all) {
  const files = under(all, 'Budgeting/').filter((p) => /\.test\.(ts|tsx)$/.test(p) || p === 'vitest.config.ts');
  return {
    key: 'budgeting-tests',
    heading: `Tests — Budgeting (Vitest, the only frontend with tests)`,
    lines: [`Files: ${files.sort().join(', ')}`],
  };
}

function websiteSections(all) {
  const files = under(all, 'Website/');
  const routes = files
    .filter((p) => /^app\/.*page\.tsx$/.test(p))
    .map((p) => '/' + p.replace(/^app\//, '').replace(/\/?page\.tsx$/, ''))
    .sort();
  const routesSec = {
    key: 'website-routes',
    heading: `Website routes — Website/app (${routes.length} pages)`,
    lines: [
      `Routes: ${routes.map((r) => r || '/').join(', ')}`,
      `Other app files: ${directFiles(under(files, 'app/')).join(', ')}`,
    ],
  };
  const comp = under(files, 'components/');
  const compSec = {
    key: 'website-components',
    heading: 'Website components — Website/components',
    lines: [
      `Top-level: ${directFiles(comp).map(stripExt).join(', ')}`,
      ...topDirs(comp).map((d) => `${d}/ (${comp.filter((p) => p.startsWith(d + '/')).length}): ${under(comp, d + '/').filter((p) => !p.includes('/')).map(stripExt).join(', ')}`),
    ],
  };
  const lib = under(files, 'lib/');
  const libSec = {
    key: 'website-lib',
    heading: `Website lib — Website/lib (${lib.length} files)`,
    lines: [`Files: ${lib.map(stripExt).sort().join(', ')}`],
  };
  return [routesSec, compSec, libSec];
}

function appHostSection(all) {
  const files = under(all, 'AppHost/');
  return {
    key: 'apphost',
    heading: `AppHost — Aspire orchestrator (${files.length} files)`,
    lines: [
      `Files: ${files.sort().join(', ')}`,
      'AppHost.cs and Backend/src/Api/.../launchSettings.json are gitignored — a fresh clone recreates them by hand (see root CLAUDE.md).',
    ],
  };
}

function rootSection(all) {
  const rootF = directFiles(all);
  const unmapped = topDirs(all).filter((d) => !KNOWN_TOP.has(d));
  const lines = [`Root files: ${rootF.join(', ')}`];
  if (unmapped.length) {
    lines.push(`Unmapped top-level folders (not yet in the code map — flag for review): ${unmapped.map((d) => `${d}/ (${all.filter((p) => p.startsWith(d + '/')).length})`).join(', ')}`);
  }
  return { key: 'root', heading: 'Workspace root & unmapped folders', lines };
}

// ---------------------------------------------------------------------------
// Assembly
// ---------------------------------------------------------------------------

function buildAll(all) {
  const srcDirs = topDirs(under(all, 'Backend/src/'));
  const domains = srcDirs.filter((d) => !['Api', 'Shared'].includes(d));
  // Order domains by size (largest first) so the busiest maps are at the top.
  domains.sort((a, b) => under(all, `Backend/src/${b}/`).length - under(all, `Backend/src/${a}/`).length);

  return {
    'backend.md': {
      title: 'Code Map — Backend',
      intro: [
        'One shared .NET API: one class library per domain, composed in the Api gateway. Every',
        'domain library has the same shape — Domain/ (aggregates), Application/ (vertical use-case',
        'slices), Infrastructure/ (DI root, Endpoints/, Persistence/).',
        '',
        `**${MIGRATIONS_WARNING}**`,
      ],
      sections: [
        backendApiSection(all),
        backendSharedSection(all),
        ...domains.map((d) => backendDomainSection(d, all)),
        backendTestsSection(all),
      ],
    },
    'dispatcher.md': {
      title: 'Code Map — Dispatcher (Admin Web App / Dispatch Console)',
      intro: [
        'Next.js 16 / React 19, frontend-only prototype on mock data. components/Console.tsx is the',
        'shell (TopBar + NavRail + screen switch, state in lib/nav.ts). Styling is inline style',
        'objects driven by lib/theme.ts tokens — no Tailwind/CSS modules.',
      ],
      sections: [screensSection('Dispatcher', all), componentsSection('Dispatcher', all), libSection('Dispatcher', all)],
    },
    'budgeting.md': {
      title: 'Code Map — Budgeting (Zero-Based Budgeting Console)',
      intro: [
        'Next.js 16 / React 19, Owner/Accountant only. The design system (ui/, theme.ts,',
        'globals.css, NavRail, HeaderClock) is a **verbatim copy of Dispatcher** — change Dispatcher',
        'first, then re-copy; never edit the copy in place. Manifest + drift check: Budgeting/CLAUDE.md.',
      ],
      sections: [
        screensSection('Budgeting', all),
        componentsSection('Budgeting', all),
        libSection('Budgeting', all),
        budgetingTestsSection(all),
      ],
    },
    'website-apphost.md': {
      title: 'Code Map — Website, AppHost & workspace root',
      intro: [
        'Website: public marketing site (static/prototype, no API calls). AppHost: Aspire local-dev',
        'orchestrator (Postgres/RabbitMQ/API/frontends).',
      ],
      sections: [...websiteSections(all), appHostSection(all), rootSection(all)],
    },
  };
}

function parseNotes(content) {
  const notes = new Map();
  const re = /<!-- notes:([\w-]+):start -->\n([\s\S]*?)<!-- notes:\1:end -->/g;
  let m;
  while ((m = re.exec(content))) notes.set(m[1], m[2].replace(/\n$/, ''));
  return notes;
}

function parseGen(content) {
  const gen = new Map();
  const re = /## (.+)\n<!-- gen:([\w-]+):start -->\n([\s\S]*?)<!-- gen:\2:end -->/g;
  let m;
  while ((m = re.exec(content))) gen.set(m[2], m[1] + '\n' + m[3]);
  return gen;
}

function renderFile(spec, header, oldContent) {
  const oldNotes = parseNotes(oldContent || '');
  const used = new Set();
  const out = [header, '<!-- Generated by codemap.mjs — edit only inside notes blocks. Regenerate: node .claude/skills/code-map/scripts/codemap.mjs generate -->', '', `# ${spec.title}`, '', ...spec.intro, ''];
  for (const s of spec.sections) {
    used.add(s.key);
    const notes = oldNotes.get(s.key) ?? '';
    out.push(
      `## ${s.heading}`,
      `<!-- gen:${s.key}:start -->`,
      ...s.lines,
      `<!-- gen:${s.key}:end -->`,
      `<!-- notes:${s.key}:start -->`,
      ...(notes ? [notes] : []),
      `<!-- notes:${s.key}:end -->`,
      ''
    );
  }
  for (const [key, body] of oldNotes) {
    if (!used.has(key) && body.trim()) {
      console.error(`warning: orphaned notes for section "${key}" dropped — its source folder vanished. Review:\n${body}`);
    }
  }
  return out.join('\n');
}

function makeHeader(paths) {
  let head = 'no-git';
  try { head = git('rev-parse', '--short', 'HEAD').trim(); } catch {}
  const date = new Date().toISOString().slice(0, 10);
  return `<!-- codemap-hash: ${hashOf(paths)} @ ${head} ${date} -->`;
}

// ---------------------------------------------------------------------------
// Commands
// ---------------------------------------------------------------------------

function generate() {
  const paths = listPaths();
  const specs = buildAll(paths);
  const header = makeHeader(paths);
  fs.mkdirSync(refsDir, { recursive: true });
  for (const [name, spec] of Object.entries(specs)) {
    const file = path.join(refsDir, name);
    const old = fs.existsSync(file) ? fs.readFileSync(file, 'utf8') : '';
    fs.writeFileSync(file, renderFile(spec, header, old));
  }
  console.log(`code map regenerated (${paths.length} paths) → .claude/skills/code-map/references/`);
}

function check() {
  const paths = listPaths();
  const hash = hashOf(paths);
  const recorded = [];
  for (const name of REF_FILES) {
    const file = path.join(refsDir, name);
    if (!fs.existsSync(file)) { recorded.push(null); continue; }
    const m = fs.readFileSync(file, 'utf8').match(/codemap-hash: ([0-9a-f]{40})/);
    recorded.push(m ? m[1] : null);
  }
  if (recorded.every((h) => h === hash)) process.exit(0);

  // Deep compare: regenerate in memory, report which sections actually drifted.
  const specs = buildAll(paths);
  const stale = [];
  for (const [name, spec] of Object.entries(specs)) {
    const file = path.join(refsDir, name);
    if (!fs.existsSync(file)) { stale.push(`stale: references/${name} (missing — run generate)`); continue; }
    const oldGen = parseGen(fs.readFileSync(file, 'utf8'));
    for (const s of spec.sections) {
      if (oldGen.get(s.key) !== s.heading + '\n' + s.lines.join('\n') + '\n') {
        stale.push(`stale: references/${name} ## ${s.heading}`);
      }
      oldGen.delete(s.key);
    }
    for (const key of oldGen.keys()) stale.push(`stale: references/${name} — section "${key}" no longer exists`);
  }
  if (!stale.length) stale.push('stale: codemap hash drift (headers out of date) — run generate');
  for (const line of stale) console.log(line);
  process.exit(1);
}

function logChange() {
  let input = '';
  process.stdin.on('data', (d) => (input += d));
  process.stdin.on('end', () => {
    try {
      const payload = JSON.parse(input);
      const fp = payload?.tool_input?.file_path;
      if (!fp) return;
      const rel = path.relative(repoRoot, fp);
      if (rel.startsWith('..') || path.isAbsolute(rel)) return;
      if (!TERRITORIES.some((t) => rel.startsWith(t))) return;
      fs.mkdirSync(stateDir, { recursive: true });
      const existing = fs.existsSync(pendingLog) ? fs.readFileSync(pendingLog, 'utf8').split('\n') : [];
      if (!existing.includes(rel)) fs.appendFileSync(pendingLog, rel + '\n');
    } catch {
      // Never fail the hook.
    }
  });
}

const cmd = process.argv[2];
if (cmd === 'generate') generate();
else if (cmd === 'check') check();
else if (cmd === 'log') logChange();
else {
  console.error('usage: codemap.mjs <generate|check|log>');
  process.exit(2);
}
