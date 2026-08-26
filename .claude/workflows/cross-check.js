export const meta = {
  name: 'cross-check',
  description: 'Audit a multi-agent session: territory boundaries, design-system drift, API seam, status colors, plus per-territory build/test gates',
  whenToUse: 'Run after any parallel multi-agent batch, or before pushing changes that span more than one app folder',
  phases: [
    { title: 'Scope', detail: 'bucket the combined diff by territory' },
    { title: 'Check', detail: 'seam and boundary audits for touched territories only' },
    { title: 'Verify', detail: 'adversarial refutation of each finding' },
    { title: 'Gate', detail: 'build/test per touched territory' },
  ],
}

// Territory matrix — must match the routing table in CLAUDE.md.
const TERRITORIES = {
  backend: 'Backend/ (excluding Backend/Dockerfile)',
  dispatcher: 'Dispatcher/ (excluding Dispatcher/Dockerfile)',
  budgeting: 'Budgeting/ (excluding Budgeting/Dockerfile)',
  website: 'Website/ (excluding Website/Dockerfile)',
  mobile: 'CommunityMobile/',
  platform:
    'AppHost/, any Dockerfile, .do/, .github/, Directory.Build.props, Directory.Packages.props, aspire.config.json',
  meta: '.claude/, CLAUDE.md files, *.md at the repo root',
}

const base = (args && args.base) || 'origin/main'

const SCOPE_SCHEMA = {
  type: 'object',
  required: ['territories', 'files', 'orphanFiles'],
  properties: {
    territories: { type: 'array', items: { type: 'string' } },
    files: {
      type: 'array',
      items: {
        type: 'object',
        required: ['path', 'territory'],
        properties: {
          path: { type: 'string' },
          territory: { type: 'string' },
        },
      },
    },
    orphanFiles: { type: 'array', items: { type: 'string' } },
  },
}

const FINDINGS_SCHEMA = {
  type: 'object',
  required: ['findings'],
  properties: {
    findings: {
      type: 'array',
      items: {
        type: 'object',
        required: ['title', 'detail', 'severity'],
        properties: {
          title: { type: 'string' },
          file: { type: 'string' },
          detail: { type: 'string' },
          severity: { enum: ['blocker', 'warning'] },
        },
      },
    },
  },
}

const VERDICT_SCHEMA = {
  type: 'object',
  required: ['refuted', 'reason'],
  properties: {
    refuted: { type: 'boolean' },
    reason: { type: 'string' },
  },
}

const GATE_SCHEMA = {
  type: 'object',
  required: ['territory', 'passed', 'summary'],
  properties: {
    territory: { type: 'string' },
    passed: { type: 'boolean' },
    summary: { type: 'string' },
  },
}

phase('Scope')
const scope = await agent(
  `You are auditing the working tree of the Northern Link workspace (the current directory is the repo root).

1. Run: git status --porcelain
2. Run: git diff --name-only ${base}...HEAD  (if the ref ${base} does not exist, fall back to: git diff --name-only HEAD~1)
3. Union the two file lists (from --porcelain take the path column; for renames take the new path). Ignore deletions of files you cannot classify.

Classify every changed file into exactly one territory using this matrix (first match wins, top to bottom):
- platform: ${TERRITORIES.platform}
- backend: ${TERRITORIES.backend}
- dispatcher: ${TERRITORIES.dispatcher}
- budgeting: ${TERRITORIES.budgeting}
- website: ${TERRITORIES.website}
- mobile: ${TERRITORIES.mobile}
- meta: ${TERRITORIES.meta}

Anything that matches nothing goes in orphanFiles. Return the deduplicated list of touched territories, the per-file classification, and orphanFiles. If there are no changes at all, return empty arrays.`,
  { label: 'scope:diff', phase: 'Scope', schema: SCOPE_SCHEMA }
)

if (!scope || scope.files.length === 0) {
  log('No changes vs ' + base + ' — nothing to cross-check.')
  return { confirmedFindings: [], gateResults: [], scope }
}

const touched = new Set(scope.territories)
const frontendTouched = ['dispatcher', 'budgeting', 'website'].some((t) => touched.has(t))
const uiTouched = frontendTouched || touched.has('mobile')
log(`Territories touched: ${[...touched].join(', ')} (${scope.files.length} files)`)

// ---- Build the check list — only checks whose territory was touched ----
const fileList = scope.files.map((f) => `${f.path} [${f.territory}]`).join('\n')
const checks = []

checks.push({
  key: 'territory-audit',
  prompt: `Territory audit for the Northern Link workspace (repo root is the current directory). The agent routing rule is one agent per territory; cross-territory changes must be deliberate seam work.

Changed files (with territory classification):
${fileList}

Orphan files (matched no territory): ${JSON.stringify(scope.orphanFiles)}

Report findings for:
1. Any orphan file — changes outside every known territory need an owner.
2. Files that are gitignored by convention but appear staged/tracked: run "git status --porcelain" and "git ls-files" to check whether AppHost/NorthernLink.AppHost/AppHost.cs or Backend/src/Api/NorthernLink.Api/Properties/launchSettings.json is tracked or staged — either being in git is a blocker (root CLAUDE.md forbids re-adding them).
3. A single logical change spanning two app territories where the seam rules don't explain it (e.g. Dispatcher and Budgeting both changed but the Budgeting changes are NOT re-copies of Dispatcher files; or a frontend and Backend changed but the frontend references endpoints the Backend diff didn't add). Use git diff to inspect when unsure.
Severity: blocker for tracked gitignored files and clear boundary violations; warning for suspicious-but-explainable spans. No findings if everything is clean.`,
})

if (touched.has('dispatcher') || touched.has('budgeting')) {
  checks.push({
    key: 'drift-check',
    prompt: `Design-system drift check between Dispatcher/ and Budgeting/ (repo root is the current directory). Budgeting holds verbatim copies of Dispatcher files; the manifest and the canonical check live in Budgeting/CLAUDE.md — read its section "The design system is a copy, not a shared package" and run the drift-check shell loop from it VERBATIM (bash). Every "DRIFT: <file>" line is a blocker finding.

Also verify:
1. The four Google Fonts <link> tags in Budgeting/app/layout.tsx are byte-identical to Dispatcher/app/layout.tsx's.
2. Budgeting/lib/roles.ts still mirrors Roles.BudgetAccess in Backend/src/Shared/Kernel/Roles.cs (same role strings, case-sensitive).
No findings if the loop prints nothing and both mirrors match.`,
  })
}

if (frontendTouched) {
  checks.push({
    key: 'seam-check',
    prompt: `API-seam check (repo root is the current directory): frontends never invent endpoint shapes — every API path a frontend calls must exist in the backend.

Changed files:
${fileList}

For each changed frontend file under Dispatcher/lib/api/, Budgeting/lib/api/, or any changed screen/component that fetches: extract the /api/... paths it requests (git diff ${base}...HEAD -- <file> to focus on NEW paths). Then confirm each path is served by an endpoint mapping under Backend/src/*/Infrastructure/Endpoints/ (grep for the route segment). Website/ must make NO API calls at all — any fetch to /api/* there is a blocker.
A new frontend path with no backend endpoint is a blocker. Pre-existing mock data (lib/data.ts, mock_data.dart) is fine and out of scope. No findings if every new path resolves.`,
  })
}

if (uiTouched) {
  checks.push({
    key: 'status-color-audit',
    prompt: `Status-color audit (repo root is the current directory). Platform rule: status is never conveyed by color alone — always color + icon + text label. Protected hexes: #009E73 (teal/good), #E1B000 (gold/caution), #D55E00 (vermillion/problem), plus #7A8899.

Only audit apps with changed files among: ${[...touched].join(', ')}.
For each touched frontend app (Dispatcher/, Budgeting/, Website/):
- grep -rn "#009E73\\|#E1B000\\|#D55E00\\|#7A8899" components lib app — protected hexes belong only in lib/theme.ts (and copied theme files); elsewhere is a finding unless the element visibly carries an icon/label beside it (read the surrounding code to decide).
- grep -rn "statusMeta(" components lib (excluding components/ui/) — every call site must feed a StatusChip/StatusBadge or sit beside text.
For CommunityMobile/ (if touched): status rendering must go through lib/widgets/status_chip.dart — grep lib/ for the four hexes outside lib/theme/nl_theme.dart and status_chip.dart.
Severity: blocker for a status conveyed by color alone; warning for a protected hex in a decorative element that does carry adjacent text. No findings if clean.`,
  })
}

if (touched.has('backend')) {
  checks.push({
    key: 'boundary-check',
    prompt: `Backend module-boundary check (repo root is the current directory). Rules: one class library per domain; a domain library references NorthernLink.Shared and NOTHING else internal — never another domain library; cross-domain communication is integration-events-only.

1. grep -rn "ProjectReference" Backend/src/*/NorthernLink.*.csproj — any domain library referencing another domain library (anything other than NorthernLink.Shared) is a blocker.
2. If git diff ${base}...HEAD --name-only shows a NEW csproj under Backend/src/, confirm the domain is listed in ModuleGraph.DomainNames (grep Backend for "DomainNames") so the architecture tests cover it — missing is a blocker.
3. Changed files under Backend/src/*/Domain/ must not import EF Core, Npgsql, or RabbitMQ namespaces — spot-check the diffs.
Do NOT read files under Persistence/Migrations/. No findings if clean.`,
  })
}

// ---- Gates (depend only on scope, so they run concurrently with checks) ----
const GATES = {
  backend:
    'cd Backend && dotnet build (warnings are errors), then dotnet test. Report pass/fail with the failing project/test names if any.',
  dispatcher: 'cd Dispatcher && npm run build. Report pass/fail with the first errors if any.',
  budgeting:
    'cd Budgeting && npm run build, then npm test (Vitest). Report pass/fail with failing test names if any. Note: npm run lint has 3 known inherited errors in copied files — lint is NOT part of this gate.',
  website: 'cd Website && npm run build. Report pass/fail with the first errors if any.',
  mobile:
    'cd CommunityMobile && flutter analyze (warnings are the bar — any warning fails). Report pass/fail. If the flutter CLI is unavailable on this machine, report passed=false with summary "flutter unavailable — untested".',
  platform:
    'cd Backend && dotnet build (validates root Directory.*.props changes). If any Dockerfile changed and docker is available, also build the touched image with the correct context (API image context is the workspace root; each frontend builds from its own folder); if docker is unavailable, say so in the summary without failing the gate on that alone.',
}
const gateTerritories = [...touched].filter((t) => GATES[t])

const gatePromise = parallel(
  gateTerritories.map((t) => () =>
    agent(
      `Build/test gate for the "${t}" territory of the Northern Link workspace (repo root is the current directory). Run: ${GATES[t]} Return territory="${t}", passed, and a one-paragraph summary of the output.`,
      { label: `gate:${t}`, phase: 'Gate', schema: GATE_SCHEMA }
    )
  )
)

// ---- Checks → adversarial verify, pipelined so verification starts per-check ----
const checkResults = await pipeline(
  checks,
  (c) => agent(c.prompt, { label: `check:${c.key}`, phase: 'Check', schema: FINDINGS_SCHEMA }),
  (result, c) => {
    const findings = (result && result.findings) || []
    if (findings.length === 0) return []
    return parallel(
      findings.map((f) => () =>
        agent(
          `Adversarially verify a cross-check finding in the Northern Link workspace (repo root is the current directory). Try to REFUTE it — re-run the underlying commands/greps yourself and read the actual files. Default to refuted=true if you cannot reproduce it concretely.

Check: ${c.key}
Finding: ${f.title}
File: ${f.file || '(none given)'}
Detail: ${f.detail}`,
          { label: `verify:${c.key}`, phase: 'Verify', schema: VERDICT_SCHEMA }
        ).then((v) => ({ check: c.key, ...f, verdict: v }))
      )
    )
  }
)

const confirmedFindings = checkResults
  .filter(Boolean)
  .flat()
  .filter(Boolean)
  .filter((f) => f.verdict && f.verdict.refuted === false)

const gateResults = (await gatePromise).filter(Boolean)

const failedGates = gateResults.filter((g) => !g.passed).map((g) => g.territory)
log(
  `Cross-check done: ${confirmedFindings.length} confirmed finding(s), gates ${
    failedGates.length ? 'FAILED for ' + failedGates.join(', ') : 'all green'
  }.`
)

return { confirmedFindings, gateResults, scope }
