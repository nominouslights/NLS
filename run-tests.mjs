#!/usr/bin/env node
// One command to run every test suite in the workspace. Zero dependencies.
//
//   node run-tests.mjs             — run everything, quiet; failing suites print their tail
//   node run-tests.mjs --verbose   — stream each suite's output live
//   node run-tests.mjs --list      — print the suites that would run, then exit
//
// Suites are DISCOVERED, not hardcoded: the Backend solution plus every frontend folder whose
// package.json declares a `test` script. A frontend with no `test` script (Website today) is
// skipped, not failed — but a folder that declares one and cannot run it (no node_modules) is a
// failure, because a suite that never ran must never be reported as green.
//
// Exit code is 0 only when every discovered suite passed.

import { spawn, spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import fs from 'node:fs';
import path from 'node:path';

const repoRoot = path.dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const verbose = args.includes('--verbose') || args.includes('-v');
const listOnly = args.includes('--list');

// npm is npm.cmd on Windows, and Node refuses to spawn a .cmd without a shell; dotnet is a real
// executable everywhere. Nothing here shells out with `cd` — every suite gets an explicit cwd.
const isWindows = process.platform === 'win32';
const FRONTENDS = ['Dispatcher', 'Website', 'Budgeting', 'DriverField'];

const c = process.stdout.isTTY && !process.env.NO_COLOR
  ? { dim: s => `\x1b[2m${s}\x1b[0m`, red: s => `\x1b[31m${s}\x1b[0m`,
      green: s => `\x1b[32m${s}\x1b[0m`, yellow: s => `\x1b[33m${s}\x1b[0m`,
      bold: s => `\x1b[1m${s}\x1b[0m` }
  : { dim: s => s, red: s => s, green: s => s, yellow: s => s, bold: s => s };

const readJson = file => {
  try { return JSON.parse(fs.readFileSync(file, 'utf8')); } catch { return null; }
};

/** Is this command on PATH at all? Probed without a shell so ENOENT stays visible. */
function toolExists(command, probeArgs) {
  const probe = spawnSync(command, probeArgs, { shell: isWindows, stdio: 'ignore' });
  return !probe.error && probe.status === 0;
}

function discoverSuites() {
  const suites = [];

  const solution = path.join(repoRoot, 'Backend', 'NorthernLink.slnx');
  if (fs.existsSync(solution)) {
    suites.push({
      name: 'Backend',
      label: 'dotnet test (NorthernLink.slnx)',
      cwd: path.join(repoRoot, 'Backend'),
      command: 'dotnet',
      args: ['test'],
      shell: false,
      tool: { command: 'dotnet', probe: ['--version'],
              missing: 'the .NET SDK is not on PATH — install .NET 10 or open a shell that has it' },
      parse: parseDotnet,
    });
  }

  for (const dir of FRONTENDS) {
    const appDir = path.join(repoRoot, dir);
    const pkg = readJson(path.join(appDir, 'package.json'));
    if (!pkg) continue;
    if (!pkg.scripts?.test) continue; // no suite declared — nothing to run, nothing to report
    suites.push({
      name: dir,
      label: `npm test  ${c.dim(`(${pkg.scripts.test})`)}`,
      cwd: appDir,
      command: 'npm',
      args: ['test', '--silent'],
      shell: isWindows,
      tool: { command: 'npm', probe: ['--version'],
              missing: 'npm is not on PATH — install Node.js or open a shell that has it' },
      requiresDir: { path: path.join(appDir, 'node_modules'),
                     missing: `dependencies are not installed — run \`npm ci\` in ${dir}/` },
      parse: parseVitest,
    });
  }

  return suites;
}

/** `dotnet test` prints a per-project VSTest summary, or one MTP summary for the whole run. */
function parseDotnet(output) {
  let passed = 0, failed = 0, skipped = 0, matched = false;

  const vstest = /Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+)/g;
  for (const m of output.matchAll(vstest)) {
    matched = true;
    failed += Number(m[1]); passed += Number(m[2]); skipped += Number(m[3]);
  }
  if (matched) return { passed, failed, skipped };

  const mtp = /failed:\s*(\d+).*?succeeded:\s*(\d+).*?skipped:\s*(\d+)/is.exec(output);
  if (mtp) return { failed: Number(mtp[1]), passed: Number(mtp[2]), skipped: Number(mtp[3]) };

  return null;
}

/** Vitest's summary line: "Tests  3 failed | 41 passed | 1 skipped (45)". */
function parseVitest(output) {
  const line = /^\s*Tests\s+(.+)$/m.exec(output);
  if (!line) return null;
  const pick = word => {
    const m = new RegExp(`(\\d+)\\s+${word}`).exec(line[1]);
    return m ? Number(m[1]) : 0;
  };
  return { passed: pick('passed'), failed: pick('failed'), skipped: pick('skipped') };
}

function describeCounts(counts) {
  if (!counts) return 'counts unavailable';
  const parts = [`${counts.passed} passed`];
  if (counts.failed) parts.push(`${counts.failed} failed`);
  if (counts.skipped) parts.push(`${counts.skipped} skipped`);
  return parts.join(', ');
}

const seconds = ms => `${(ms / 1000).toFixed(1)}s`;

function tail(output, lines = 40) {
  const rows = output.split(/\r?\n/).filter(row => row.trim().length > 0);
  return rows.slice(-lines).map(row => `    ${row}`).join('\n');
}

function runSuite(suite) {
  return new Promise(resolve => {
    const started = Date.now();
    let output = '';
    const child = spawn(suite.command, suite.args, {
      cwd: suite.cwd,
      shell: suite.shell,
      stdio: ['ignore', 'pipe', 'pipe'],
      env: process.env,
    });

    const collect = stream => stream.on('data', chunk => {
      const text = chunk.toString();
      output += text;
      if (verbose) process.stdout.write(text);
    });
    collect(child.stdout);
    collect(child.stderr);

    child.on('error', error => resolve({
      status: 'blocked', elapsed: Date.now() - started, output,
      message: `could not start \`${suite.command}\` — ${error.message}`,
    }));

    child.on('close', code => resolve({
      status: code === 0 ? 'passed' : 'failed',
      elapsed: Date.now() - started,
      output,
      counts: suite.parse(output),
      code,
    }));
  });
}

const suites = discoverSuites();
const skipped = FRONTENDS.filter(dir => {
  const pkg = readJson(path.join(repoRoot, dir, 'package.json'));
  return pkg && !pkg.scripts?.test;
});

if (listOnly) {
  for (const suite of suites) console.log(`${suite.name.padEnd(11)} ${suite.label}`);
  for (const dir of skipped) console.log(`${dir.padEnd(11)} ${c.dim('no test script — skipped')}`);
  process.exit(0);
}

console.log(c.bold(`Northern Link — running ${suites.length} test suite(s)`));
for (const dir of skipped) console.log(c.dim(`  - ${dir}: no test script, skipped`));
console.log('');

const results = [];
const runStarted = Date.now();

for (const suite of suites) {
  process.stdout.write(`${c.bold(suite.name)}  ${suite.label}\n`);

  if (suite.requiresDir && !fs.existsSync(suite.requiresDir.path)) {
    results.push({ suite, status: 'blocked', elapsed: 0, message: suite.requiresDir.missing });
    console.log(`  ${c.yellow('BLOCKED')} ${suite.requiresDir.missing}\n`);
    continue;
  }
  if (suite.tool && !toolExists(suite.tool.command, suite.tool.probe)) {
    results.push({ suite, status: 'blocked', elapsed: 0, message: suite.tool.missing });
    console.log(`  ${c.yellow('BLOCKED')} ${suite.tool.missing}\n`);
    continue;
  }

  const result = await runSuite(suite);
  results.push({ suite, ...result });

  if (result.status === 'passed') {
    console.log(`  ${c.green('PASS')} ${describeCounts(result.counts)} ${c.dim(`in ${seconds(result.elapsed)}`)}\n`);
  } else if (result.status === 'blocked') {
    console.log(`  ${c.yellow('BLOCKED')} ${result.message}\n`);
  } else {
    console.log(`  ${c.red('FAIL')} ${describeCounts(result.counts)} ${c.dim(`in ${seconds(result.elapsed)}, exit ${result.code}`)}`);
    if (!verbose && result.output.trim()) {
      console.log(c.dim('  last lines of output:'));
      console.log(tail(result.output));
    }
    console.log('');
  }
}

const totalElapsed = Date.now() - runStarted;
console.log(c.bold('Summary'));
for (const result of results) {
  const detail = result.status === 'blocked'
    ? result.message
    : `${describeCounts(result.counts)} — ${seconds(result.elapsed)}`;
  const tag = result.status === 'passed' ? c.green('PASS   ')
    : result.status === 'blocked' ? c.yellow('BLOCKED') : c.red('FAIL   ');
  console.log(`  ${tag}  ${result.suite.name.padEnd(11)} ${detail}`);
}
for (const dir of skipped) console.log(`  ${c.dim('SKIP   ')}  ${dir.padEnd(11)} ${c.dim('no test script')}`);
console.log(c.dim(`  total ${seconds(totalElapsed)}`));

const failures = results.filter(result => result.status !== 'passed');
if (failures.length > 0) {
  console.log(c.red(`\n${failures.length} suite(s) did not pass.`));
  process.exit(1);
}
console.log(c.green('\nAll suites passed.'));
