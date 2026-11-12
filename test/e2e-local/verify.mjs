// Opt-in only: node test/e2e-local/verify.mjs. Requires the real forwarded host.
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
// E2E_OUTPUT lets cases run in parallel without overwriting each other's results.
const output = path.join(root, 'artifacts', process.env.E2E_OUTPUT || 'e2e-local');
const startedAt = new Date().toISOString();
const origin = process.env.SQUAD_APP_URL || 'http://127.0.0.1:58080';
assert(['127.0.0.1', 'localhost'].includes(new URL(origin).hostname), 'Use a loopback port-forward.');
const waveTimeout = Number(process.env.E2E_WAVE_TIMEOUT_MS || 25 * 60_000);
// E2E_SHAPE=1 runs apps with other architectures (more APIs, front ends, databases) and checks that the
// Aspire and cluster maps match the contract, the live namespace and the running configuration.
const shapeMode = Boolean(process.env.E2E_SHAPE);
const shapeCases = [
  { key: 'S1-two-apis', title: '1 front end + 2 APIs', expect: { projects: 2, frontends: 1, servers: 1 },
    prompt: 'A recipe catalog: people add recipes with a title, ingredients and steps, and browse and open them. Architecture (follow it exactly): one React front end; the gateway API named api; and a separate API service named catalog that stores the recipes in the PostgreSQL database. The gateway api forwards recipe requests to catalog.' },
  { key: 'S2-two-frontends', title: '2 front ends + 1 API', expect: { projects: 1, frontends: 2, servers: 1 },
    prompt: 'A volunteer shift signup. Architecture (follow it exactly): two React front ends served by one API named api: the public signup site at / where volunteers sign up for a shift with their name, and an admin front end at /admin/ where organizers create shifts and see who signed up. One PostgreSQL database.' },
  { key: 'S3-full', title: '2 front ends + 2 APIs + 2 databases', expect: { projects: 2, frontends: 2, servers: 2 },
    prompt: 'The Contoso Crafts store. Architecture (follow it exactly): two React front ends (the store at / and an admin panel at /admin/); two APIs: the gateway api, which owns products in the main PostgreSQL database, and an orders API named orders with its own PostgreSQL server named ordersserver; so two database servers. The gateway api forwards order requests to orders. Seed a few craft products. Shoppers place an order with their name; the admin panel adds products and lists orders.' },
  { key: 'S4-two-databases', title: '1 front end + 1 API + 2 databases on one server', expect: { projects: 1, frontends: 1, servers: 1, logical: { db: 2 } },
    prompt: 'A team expense tracker. Architecture (follow it exactly): one React front end, one API named api, and one PostgreSQL server named db with two databases: appdb for the expenses and auditdb for an audit log of every expense added, shown on an Audit section of the page.' },
  { key: 'S5-grow', title: 'Topology change in place', expect: { projects: 1, frontends: 1, servers: 1 },
    grow: { expect: { projects: 2, frontends: 2, servers: 2 },
      request: 'Change the architecture: add a new API service named reviews with its own PostgreSQL server named reviewsserver, so people can leave a short review on each book (the gateway api forwards review requests to reviews), and add an admin front end at /admin/ that lists all books and their reviews. Keep the books where they are now.' },
    prompt: 'A simple reading list: people add books with a title and author, see the list, and mark a book as read.' },
  { key: 'S6-three-apis', title: '1 front end + 3 APIs', expect: { projects: 3, frontends: 1, servers: 2 },
    prompt: 'A neighborhood events board. Architecture (follow it exactly): one React front end; the gateway API named api; and two internal API services behind it: events, which stores events in the main PostgreSQL database, and rsvps, which stores RSVPs in its own PostgreSQL server named rsvpserver. The browser only talks to api, which forwards to events and rsvps. People post an event and RSVP with their name.' }
];
const cases = (shapeMode ? shapeCases : [
  { key: 'A-tools', idea: 'tools', title: 'Tool-lending library' },
  { key: 'B-surprise', idea: 'surprise', title: 'Surprise me' },
  { key: 'C-books', title: 'Book club tracker', prompt: 'A small book club tracker: members add books, vote on the next read, and log meeting notes.' }
]).filter(item => !process.env.E2E_APP || process.env.E2E_APP.split(',').some(prefix => item.key.startsWith(prefix)));
const shapeChecks = [
  'Topology matches contract and cluster', 'Every front end answers',
  'Create record and reload', 'Architecture-changing wave', 'Redeployed into same namespace',
  'Topology matches after the change', 'Earlier record survives update', 'Every front end answers after the change'
];
const baseChecks = [
  'Plan and start build', 'Build wave completed', 'Recorded validation passed',
  'Put it online', 'Deployed topology and Ready api/db',
  'Dedicated namespace, Deployment, StatefulSet, Bound PVC',
  'Create record and reload', 'Database pod restart preserves record',
  'Second wave and recorded validation', 'Redeployed into same namespace',
  'Earlier record survives update', 'Search filters the list',
  'Build, check, deploy and health check Done'
];
const checks = shapeMode ? [...baseChecks.slice(0, 4), ...shapeChecks] : baseChecks;
const defaultComponents = {
  projects: [{ name: 'api', path: 'Api' }],
  frontends: [{ name: 'frontend', path: 'frontend', mount: '/' }],
  databases: [{ server: 'db', name: 'appdb' }]
};
const sameSet = (actual, expected, what) =>
  assert.deepEqual([...new Set(actual)].sort(), [...new Set(expected)].sort(), what);
// The maps must equal three independent sources: the contract, the live namespace and its running configuration.
async function topologyMatches(page, result, expect) {
  const contract = await page.request.get(`${origin}/apps/${result.id}/workspace/raw?path=app.build.json`);
  assert(contract.ok(), 'Generated contract unavailable');
  const declared = (await contract.json()).runtime?.components ?? defaultComponents;
  const servers = [...new Set(declared.databases.map(database => database.server))];
  result.components = declared;
  assert.equal(declared.projects.length, expect.projects, `Declared projects: ${declared.projects.map(item => item.name)}`);
  assert.equal(declared.frontends.length, expect.frontends, `Declared front ends: ${declared.frontends.map(item => item.name)}`);
  assert.equal(servers.length, expect.servers, `Declared database servers: ${servers}`);
  const topology = await poll('deployed topology', async () => {
    const value = await read(page, result.id, '/workspace/topology');
    return value.state === 'deployed' ? value : false;
  }, 120_000);
  await save(result.directory, `topology-${result.topologyChecks = (result.topologyChecks || 0) + 1}.json`, topology);
  const aspire = kind => topology.aspire.nodes.filter(node => node.kind === kind).map(node => node.id);
  sameSet(aspire('project'), declared.projects.map(item => item.name), 'Aspire projects = declared projects');
  sameSet(aspire('frontend'), declared.frontends.map(item => item.name), 'Aspire front ends = declared front ends');
  sameSet(aspire('database-server'), servers, 'Aspire database servers = declared servers');
  for (const frontend of declared.frontends)
    assert(topology.aspire.edges.some(edge => edge.from === frontend.name && edge.to === 'api' &&
      edge.label === (frontend.mount === '/' ? 'built into wwwroot' : `served at ${frontend.mount}`)), `front end ${frontend.name} served by api`);
  for (const [server, count] of Object.entries(expect.logical || {})) {
    const detail = topology.aspire.nodes.find(node => node.id === server)?.detail || '';
    const names = declared.databases.filter(database => database.server === server).map(database => database.name);
    assert.equal(names.length, count, `Declared databases on ${server}: ${names}`);
    assert(names.every(name => detail.includes(name)), `${server} shows its databases (${detail})`);
  }
  const live = JSON.parse(kubectl('get', 'deployment,statefulset,service,pvc,configmap', '-n', topology.namespace, '-o', 'json')).items;
  await save(result.directory, `kubectl-${result.topologyChecks}.txt`, kubectl('get', 'deployment,statefulset,service,pvc', '-n', topology.namespace));
  const named = kind => live.filter(item => item.kind === kind).map(item => item.metadata.name);
  sameSet(named('Deployment').map(name => name.replace(/-deployment$/, '')), aspire('project'), 'Aspire projects = Deployments running in the namespace');
  sameSet(named('StatefulSet').map(name => name.replace(/-statefulset$/, '')), aspire('database-server'), 'Aspire database servers = StatefulSets running in the namespace');
  const cluster = prefix => topology.cluster.nodes.filter(node => node.id.startsWith(prefix + ':')).map(node => node.label);
  sameSet(cluster('workload'), [...named('Deployment'), ...named('StatefulSet')], 'Cluster workloads = live Deployments and StatefulSets');
  sameSet(cluster('service'), named('Service'), 'Cluster Services = live Services');
  assert.equal(topology.cluster.nodes.filter(node => node.kind === 'volume').length, named('PersistentVolumeClaim').length, 'Cluster volumes = live PVCs');
  // Connections, read from the configuration the projects are actually running with.
  const references = [];
  for (const map of live.filter(item => item.kind === 'ConfigMap')) {
    const from = map.metadata.name.replace(/-config$/, '');
    if (!aspire('project').includes(from)) continue;
    for (const [key, value] of Object.entries(map.data || {})) {
      const service = /^services__([a-z0-9]+)__/.exec(key);
      if (service) references.push(`${from} -> ${service[1]}`);
      if (key.endsWith('_HOST') && value.endsWith('-service')) references.push(`${from} -> ${value.slice(0, -'-service'.length)}`);
    }
  }
  sameSet(topology.aspire.edges.filter(edge => aspire('project').includes(edge.from)).map(edge => `${edge.from} -> ${edge.to}`),
    references, 'Aspire connections = references in the running configuration');
  result.architecture = { projects: aspire('project'), frontends: aspire('frontend'), servers: aspire('database-server'), references: [...new Set(references)] };
  // The page draws the same thing, in both views.
  await page.reload();
  await page.locator('#topology-section').scrollIntoViewIfNeeded();
  for (const view of ['Aspire', 'Cluster']) {
    await page.getByRole('button', { name: view, exact: true }).click();
    const nodes = view === 'Aspire' ? topology.aspire.nodes : topology.cluster.nodes.filter(node => node.kind !== 'namespace');
    await poll(`${view} view draws every node`, async () => {
      const text = await page.locator('#topology-diagram').innerText();
      // Short labels (config under its workload, "db-data") keep the full name as their title.
      const titles = await page.locator('#topology-diagram [title]').evaluateAll(items => items.map(item => item.title));
      return nodes.every(node => text.includes(node.label) || titles.includes(node.label));
    }, 30_000);
    await page.locator('#topology-section').screenshot({ path: path.join(result.directory, `topology-${result.topologyChecks}-${view.toLowerCase()}.png`) });
  }
  return topology;
}
async function frontendsAnswer(page, result) {
  for (const frontend of result.components.frontends) {
    const response = await page.request.get(`${origin}/online/${result.id}${frontend.mount}`, { timeout: 30_000 });
    assert(response.ok(), `${frontend.name} at ${frontend.mount}: HTTP ${response.status()}`);
    assert(/<script/i.test(await response.text()), `${frontend.name} at ${frontend.mount} serves its built page`);
  }
  // A separate context: the run's own context blocks service workers, and Playwright's blocking shim itself throws
  // inside Squad's sandboxed preview, which would be reported as the app's page error.
  const plain = await page.context().browser().newContext({ viewport: { width: 1280, height: 900 } });
  await plain.route('**/*', route =>
    new URL(route.request().url()).origin === new URL(origin).origin ? route.continue() : route.abort());
  try {
  for (const frontend of result.components.frontends) {
    const view = await plain.newPage();
    const errors = [];
    view.on('pageerror', error => errors.push(String(error.message || error)));
    await view.goto(`${origin}/online/${result.id}${frontend.mount}`);
    await view.waitForLoadState('networkidle').catch(() => {});
    await view.screenshot({ path: path.join(result.directory, `frontend-${frontend.name}-${result.topologyChecks}.png`), fullPage: true });
    await view.close();
    assert.equal(errors.length, 0, `${frontend.name} page errors: ${errors.join('; ').slice(0, 300)}`);
  }
  } finally { await plain.close(); }
}
const results = [];
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
const kubectl = (...args) => execFileSync('kubectl', ['--context', 'docker-desktop', ...args], {
  encoding: 'utf8', timeout: 60_000, stdio: ['ignore', 'pipe', 'pipe']
});
const safeError = error => String(error.message || error).split('\nCall log:')[0]
  .replace(/\b(?:gh[pousr]_[A-Za-z0-9_]+|github_pat_[A-Za-z0-9_]+)\b/g, '[REDACTED]')
  .slice(0, 2500);
const save = (directory, name, value) => writeFile(path.join(directory, name),
  typeof value === 'string' ? value : JSON.stringify(value, null, 2));
async function poll(description, action, timeout = waveTimeout) {
  const end = Date.now() + timeout;
  while (Date.now() < end) {
    const result = await action();
    if (result) return result;
    await sleep(2500);
  }
  throw new Error(`Timed out: ${description} (${timeout} ms)`);
}
async function read(page, id, suffix) {
  const response = await page.request.get(`${origin}/apps/${encodeURIComponent(id)}${suffix}`, { timeout: 30_000 });
  assert(response.ok(), `${suffix}: HTTP ${response.status()}`);
  return response.json();
}
async function screenshot(page, directory, name) {
  await page.screenshot({ path: path.join(directory, `${name}.png`), fullPage: true, timeout: 30_000 });
}
async function pipeline(page) {
  await page.locator('#pipeline-section').evaluate(element => { element.open = true; });
  return page.locator('#pipeline-section').innerText();
}
async function diagnostics(page, result) {
  const errors = [];
  try { await save(result.directory, 'whats-happening.txt', await pipeline(page)); }
  catch (error) { errors.push(`Pipeline: ${safeError(error)}`); }
  try { await screenshot(page, result.directory, 'failure'); }
  catch (error) { errors.push(`Screenshot: ${safeError(error)}`); }
  if (result.id) {
    try {
      await save(result.directory, 'lifecycle.json', {
        casting: await read(page, result.id, '/casting/status'),
        work: await read(page, result.id, '/work/status'),
        waves: await read(page, result.id, '/waves')
      });
    } catch (error) { errors.push(`Lifecycle: ${safeError(error)}`); }
    try {
      const topology = await read(page, result.id, '/workspace/topology');
      result.namespace = topology.namespace;
      await save(result.directory, 'topology.json', topology);
      await save(result.directory, 'deployments.json', await read(page, result.id, '/workspace/deployments'));
    } catch (error) { errors.push(`Read-only deployment state: ${safeError(error)}`); }
    try {
      const response = await page.request.get(`${origin}/apps/${result.id}/work/preflight`);
      if (response.ok()) await save(result.directory, 'preflight.json', await response.json());
      else errors.push(`Preflight: HTTP ${response.status()}`);
    } catch (error) { errors.push(`Preflight: ${safeError(error)}`); }
    const namespace = result.namespace || `soaks-app-${result.id}`;
    assert(/^soaks-app-[a-z0-9-]+$/.test(namespace), 'Unexpected app namespace');
    for (const [file, args] of [
      ['kubectl-get-all-pvc.txt', ['get', 'all,pvc', '-n', namespace]],
      ['namespace.txt', ['get', 'ns', namespace]]
    ]) {
      const command = spawnSync('kubectl', ['--context', 'docker-desktop', ...args], {
        encoding: 'utf8', timeout: 60_000
      });
      await save(result.directory, file,
        `$ kubectl --context docker-desktop ${args.join(' ')}\nExit code: ${command.status}\n${command.stdout || ''}${command.stderr || ''}`);
      if (command.error) errors.push(`kubectl: ${safeError(command.error)}`);
    }
    try {
      const pods = JSON.parse(kubectl('get', 'pods', '-n', namespace, '-o', 'json'));
      const reasons = [];
      for (const pod of pods.items) {
        // Never persist raw describe: it contains environment variables and event messages.
        const description = kubectl('describe', 'pod', pod.metadata.name, '-n', namespace);
        const codes = [...description.matchAll(/^\s*(?:Reason:\s*|(?:Normal|Warning)\s+)([A-Za-z][A-Za-z0-9]+)\b/gm)]
          .map(match => match[1]);
        reasons.push({
          pod: pod.metadata.name, phase: pod.status.phase, reasonCodes: [...new Set(codes)],
          containers: [...(pod.status.initContainerStatuses || []), ...(pod.status.containerStatuses || [])]
            .map(container => ({ name: container.name, ready: container.ready, restartCount: container.restartCount,
              reasons: Object.values(container.state || {}).map(state => state.reason).filter(Boolean) }))
        });
      }
      await save(result.directory, 'pod-describe-reasons.json', reasons);
    } catch (error) { errors.push(`Pod reason collection: ${safeError(error)}`); }
  }
  await save(result.directory, 'diagnostic-errors.json', errors);
}
async function waitForWave(page, result, number) {
  const summary = await poll(`wave ${number} terminal state`, async () => {
    const waves = await read(page, result.id, '/waves');
    const wave = waves.find(item => item.number === number);
    return wave && wave.state !== 'Active' ? wave : null;
  });
  const detail = await read(page, result.id, `/waves/${number}`);
  // Store measured evidence, not the model response, transcripts, or command output.
  await save(result.directory, `wave-${number}.json`, {
    summary,
    evidence: detail.evidence.map(({ kind, state, source, timestamp, exitCode, path: file }) =>
      ({ kind, state, source, timestamp, exitCode, path: file }))
  });
  await page.reload();
  await page.locator('#wave-notice').waitFor();
  return { summary, evidence: detail.evidence };
}
function validationPassed(wave) {
  const recorded = wave.evidence.filter(item => item.kind === 'validation').at(-1);
  assert.equal(wave.summary.validationState, 'Passed', 'Wave validationState is not Passed');
  assert(recorded, 'No recorded validator evidence');
  assert.equal(recorded.state, 'Passed');
  assert.equal(recorded.exitCode, 0, 'Validator did not record exit code 0');
}
// Playwright acts faster than a person: let the app's own requests finish after each action,
// so a generated app's slow-network race (not a platform fault) doesn't decide the run.
function trackRequests(page) {
  const pending = new Set();
  page.on('request', request => pending.add(request));
  page.on('requestfinished', request => pending.delete(request));
  page.on('requestfailed', request => pending.delete(request));
  page.settle = async () => {
    let quietSince = Date.now();
    const end = Date.now() + 15_000;
    while (Date.now() < end) {
      if (pending.size) quietSince = Date.now();
      else if (Date.now() - quietSince >= 500) return;
      await sleep(100);
    }
  };
}
async function performSteps(page, steps, previous, token) {
  for (const step of steps) {
    // Case-insensitive, so a journey that re-enters "dune" to prove "Dune" is a duplicate still does.
    const value = typeof step.value === 'string'
      ? step.value.replace(new RegExp(previous.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'gi'), token) : step.value;
    const locator = step.action === 'reload' ? null : page.locator(step.selector);
    switch (step.action) {
      case 'fill': await locator.fill(value); break;
      case 'click': await locator.click(); await page.settle?.(); break;
      case 'press': await locator.press(value); await page.settle?.(); break;
      case 'reload': await page.reload(); break;
      case 'text': await poll(`text assertion ${step.selector}`, async () =>
        (await locator.innerText()).includes(value), 30_000); break;
      case 'visible': await locator.waitFor({ state: 'visible' }); break;
      case 'count': assert.equal(await locator.count(), Number(value), step.selector); break;
      default: throw new Error(`Unsupported browser journey action: ${step.action}`);
    }
  }
}
async function recordAdapter(page, result) {
  // Generated selectors are read-only input for real browser actions, never edits or API writes.
  const response = await page.request.get(
    `${origin}/apps/${result.id}/workspace/raw?path=app.build.json`);
  assert(response.ok(), 'Generated browser contract unavailable');
  const manifest = await response.json();
  assert.equal(manifest.architecture, 'aspire');
  assert.equal(manifest.runtime?.database, 'postgres');
  for (const journey of manifest.runtime.journeys) {
    const fill = journey.steps.find(step => step.action === 'fill' && step.value &&
      journey.steps.some(other => other.action === 'text' && other.value?.includes(step.value)) &&
      manifest.runtime.restartJourneys?.some(restart =>
        restart.steps.some(other => other.action === 'text' && other.value?.includes(step.value))));
    if (fill) return { steps: journey.steps, previous: fill.value };
  }
  throw new Error('No safe text-record adapter in generated journeys; browser-specific selectors need inspection, not a platform fix.');
}
async function writeReport() {
  const lines = [
    '# Local Kubernetes real-model verification', '',
    `Started: ${startedAt}. Context: docker-desktop. URL: ${origin}.`,
    `Checkout HEAD: ${execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim()}.`,
    '', '## Environment', '',
    'The runner verified the docker-desktop context and Ready nodes before opening the browser.',
    'Registry/worker/deployment preparation is a separate prerequisite; see environment.txt when captured.',
    '', '| Check | ' + results.map(result => result.title).join(' | ') + ' |',
    '| --- | ' + results.map(() => '---').join(' | ') + ' |',
    ...checks.map(check => `| ${check} | ${results.map(result => result.checks[check]?.status || 'BLOCKED').join(' | ')} |`),
    '', 'BLOCKED means not reached; it is not a pass. FAIL means an attempted assertion failed.',
    '', '## Findings', ''
  ];
  for (const result of results) {
    lines.push(`### ${result.title}`, '', `App ID: ${result.id || 'not created'}. Namespace: ${result.namespace || 'not yet observed'}.`, '');
    for (const [check, detail] of Object.entries(result.checks)) {
      if (detail.status === 'FAIL') lines.push(`**Failed step: ${check}.**`, '', detail.error, '');
    }
    lines.push(`Evidence: \`${result.key}/\` (screenshots, recorded wave evidence, What's happening, cluster inventory and pod reason codes).`, '');
    if (result.rootCause) lines.push(`Root cause: ${result.rootCause}`, '', `Smallest proposed fix: ${result.fix}`, '');
  }
  lines.push('## Boundary', '',
    'No platform or generated application source was changed. No mocks, direct app API writes, remote pushes, or Phase 3 UI fixes.',
    'The standalone runner uses UI actions to plan/build/deploy and read-only APIs to corroborate recorded results.',
    'Database-pod deletion is restricted to the observed app StatefulSet in its dedicated namespace.',
    'This report records only checks actually reached; it does not claim end-to-end success from deterministic tests.', '');
  await writeFile(path.join(output, 'REPORT.md'), lines.join('\n'));
  await writeFile(path.join(output, 'results.json'), JSON.stringify(results, null, 2));
}
assert.equal(kubectl('config', 'current-context').trim(), 'docker-desktop');
const nodes = JSON.parse(kubectl('get', 'nodes', '-o', 'json')).items;
assert(nodes.length && nodes.every(node =>
  node.status.conditions.some(condition => condition.type === 'Ready' && condition.status === 'True')), 'Nodes must be Ready');
await mkdir(output, { recursive: true });
const browser = await chromium.launch({
  headless: true,
  ...(process.env.E2E_BROWSER_CHANNEL ? { channel: process.env.E2E_BROWSER_CHANNEL } : {})
});
try {
  for (const item of cases) {
    const result = { ...item, directory: path.join(output, item.key), checks: {} };
    results.push(result);
    await mkdir(result.directory, { recursive: true });
    const context = await browser.newContext({ viewport: { width: 1440, height: 1100 }, serviceWorkers: 'block' });
    await context.route('**/*', route =>
      new URL(route.request().url()).origin === new URL(origin).origin ? route.continue() : route.abort());
    const page = await context.newPage();
    page.setDefaultTimeout(30_000);
    let active, app;
    const check = async (name, action, target = page) => {
      active = name;
      console.log(`[${item.key}] ${name}`);
      await action();
      await screenshot(target, result.directory, `${String(checks.indexOf(name) + 1).padStart(2, '0')}-pass`);
      result.checks[name] = { status: 'PASS' };
      await writeReport();
    };
    try {
      await check(checks[0], async () => {
        await page.goto(origin);
        await page.getByRole('button', { name: 'New app', exact: true }).click();
        await page.waitForURL(url => url.searchParams.has('app'));
        result.id = new URL(page.url()).searchParams.get('app');
        if (item.idea) await page.locator(`[data-idea="${item.idea}"]`).click();
        else await page.locator('#casting-prompt').fill(item.prompt);
        if (item.idea !== 'surprise') await page.getByRole('button', { name: 'Plan app', exact: true }).click();
        const clarifications = [];
        await poll('planning completes', async () => {
          if (await page.locator('#notice').isVisible()) throw new Error(await page.locator('#notice').innerText());
          if (await page.locator('#startup-question').isVisible() &&
              await page.locator('#send-casting-turn').isEnabled() &&
              !(await page.locator('#build-app').isEnabled())) {
            assert(clarifications.length < 3, 'Planning still requires clarification after three answers');
            const question = await page.locator('#startup-question').innerText();
            const answer = 'Use the simplest useful defaults for this small local app. People can enter their names; no accounts or sign-in are needed. Keep the requested core workflows, with no external services.' +
              (shapeMode ? ' Follow the architecture in the brief exactly.' : '');
            clarifications.push({ question, answer });
            await save(result.directory, 'planning-clarifications.json', clarifications);
            await screenshot(page, result.directory, `clarification-${clarifications.length}`);
            await page.locator('#casting-prompt').fill(answer);
            await page.locator('#send-casting-turn').click();
            return false;
          }
          return await page.locator('#build-app').isVisible() && await page.locator('#build-app').isEnabled();
        }, 5 * 60_000);
        await screenshot(page, result.directory, 'planned');
        await page.getByRole('button', { name: 'Build my app', exact: true }).click();
        await poll('first wave starts', async () => {
          const waves = await read(page, result.id, '/waves');
          if (waves.length) return true;
          if (await page.locator('#notice').isVisible()) throw new Error(await page.locator('#notice').innerText());
          return false;
        }, 8 * 60_000);
      });
      let first;
      await check(checks[1], async () => {
        first = await waitForWave(page, result, 1);
        assert.equal(first.summary.state, 'Completed', `Build wave state: ${first.summary.state}; ${first.summary.message || ''}`);
      });
      await check(checks[2], async () => {
        // A first wave that fails its checks is answered the way a person would: "Ask the team to fix this".
        // A second press is allowed: fixing one problem (e.g. a check that blocked compiling) can reveal the next.
        for (let attempt = 0; attempt < 2 && first.summary.validationState !== 'Passed' && await page.locator('#ask-fix').isVisible(); attempt++) {
          await page.locator('#ask-fix').click();
          await page.locator('#send-work').click();
          result.fixWaves = (result.fixWaves || 0) + 1;
          first = await waitForWave(page, result, 1 + result.fixWaves);
        }
        validationPassed(first);
      });
      await check(checks[3], async () => {
        const goOnline = async () => {
          await page.locator('#put-online').click();
          return poll('online healthy', async () => {
            const online = await read(page, result.id, '/workspace/online');
            if (online.online) { result.onlineUrl = online.url; return 'online'; }
            const deployments = await read(page, result.id, '/workspace/deployments');
            const last = deployments.revisions.at(-1);
            if (last && (last.phase === 'Failed' || last.phase === 4))
              return last.reason === 'journeys-failed' ? 'journeys-failed'
                : (() => { throw new Error(`Deployment failed: ${last.reason || 'inspect deployment state'}`); })();
            return false;
          });
        };
        // A revision whose own journeys fail in the throwaway copy is held back; answer it the way a person
        // would, by asking the team to fix it (the team is given what the browser saw), then try again once.
        if (await goOnline() === 'journeys-failed') {
          result.checkFixWaves = (result.checkFixWaves || 0) + 1;
          await page.locator('#work-request').fill('The app failed its own user journeys in the test copy, so it could not go online. Fix what the test found.');
          await page.getByRole('button', { name: 'Start a wave', exact: true }).click();
          result.fixWaves = (result.fixWaves || 0) + 1;
          let fixed = await waitForWave(page, result, 1 + result.fixWaves);
          // If that fix breaks the app's own checks, ask again, as after the first build.
          for (let attempt = 0; attempt < 2 && fixed.summary.validationState !== 'Passed' && await page.locator('#ask-fix').isVisible(); attempt++) {
            await page.locator('#ask-fix').click();
            await page.locator('#send-work').click();
            result.fixWaves = (result.fixWaves || 0) + 1;
            fixed = await waitForWave(page, result, 1 + result.fixWaves);
          }
          assert.equal(fixed.summary.state, 'Completed', 'Fix wave did not complete');
          validationPassed(fixed);
          assert.equal(await goOnline(), 'online', 'The fixed revision still failed its journeys in the test copy');
        }
      });
      if (shapeMode) {
        await shapeFlow();
        continue;
      }
      await check(checks[4], async () => {
        await page.getByRole('button', { name: 'Cluster', exact: true }).click();
        await poll('deployed topology and live readiness', async () => {
          const topology = await read(page, result.id, '/workspace/topology');
          result.namespace = topology.namespace;
          if (topology.state !== 'deployed') return false;
          const live = await read(page, result.id, '/workspace/topology/live');
          await save(result.directory, 'live-ready.json', live);
          const workloads = topology.cluster.nodes.filter(node => ['deployment', 'statefulset'].includes(node.kind));
          assert(workloads.some(node => /^api(?:-|$)/.test(node.label)) &&
            workloads.some(node => /^db(?:-|$)/.test(node.label)));
          return live.available && workloads.every(node => live.nodes[node.id]?.state === 'ready') &&
            await page.locator('#topology-state').getAttribute('data-state') === 'deployed' &&
            /^(Deployed|Running) · /.test(await page.locator('#topology-state').innerText()) &&
            (await page.locator('#topology-diagram').innerText()).includes('Ready');
        }, 120_000);
      });
      let databasePod, namespaceUid, firstRevision;
      await check(checks[5], async () => {
        assert.equal(result.namespace, `soaks-app-${result.id}`);
        const namespace = JSON.parse(kubectl('get', 'ns', result.namespace, '-o', 'json'));
        namespaceUid = namespace.metadata.uid;
        await save(result.directory, 'namespace.txt', kubectl('get', 'ns', result.namespace));
        await save(result.directory, 'kubectl-get-all-pvc.txt', kubectl('get', 'all,pvc', '-n', result.namespace));
        const resources = JSON.parse(kubectl('get', 'deployment,statefulset,pvc,pod', '-n', result.namespace, '-o', 'json')).items;
        assert(resources.some(resource => resource.kind === 'Deployment' && resource.metadata.name.startsWith('api')));
        const database = resources.find(resource => resource.kind === 'StatefulSet' && resource.metadata.name.startsWith('db'));
        assert(database, 'db StatefulSet missing');
        assert(resources.some(resource => resource.kind === 'PersistentVolumeClaim' && resource.status.phase === 'Bound'));
        databasePod = resources.find(resource => resource.kind === 'Pod' &&
          resource.metadata.ownerReferences?.some(owner => owner.uid === database.metadata.uid));
        assert(databasePod, 'Database pod missing');
        firstRevision = (await read(page, result.id, '/workspace/topology')).revision;
      });
      app = await context.newPage();
      app.setDefaultTimeout(30_000);
      trackRequests(app);
      const token = `E2E-${item.key}-${Date.now()}`;
      let adapter;
      await check(checks[6], async () => {
        adapter = await recordAdapter(page, result);
        const response = await app.goto(`${origin}/online/${result.id}/`);
        assert(response.ok(), `Online browser HTTP ${response.status()}`);
        await performSteps(app, adapter.steps, adapter.previous, token);
        await app.reload();
        await app.getByText(token, { exact: false }).first().waitFor();
      }, app);
      await check(checks[7], async () => {
        assert.equal(kubectl('config', 'current-context').trim(), 'docker-desktop');
        kubectl('delete', 'pod', databasePod.metadata.name, '-n', result.namespace, '--wait=true', '--timeout=60s');
        await poll('replacement database pod Ready', async () => {
          const pods = JSON.parse(kubectl('get', 'pods', '-n', result.namespace, '-o', 'json')).items;
          return pods.some(pod => pod.metadata.name === databasePod.metadata.name &&
            pod.metadata.uid !== databasePod.metadata.uid &&
            pod.status.conditions?.some(condition => condition.type === 'Ready' && condition.status === 'True'));
        }, 180_000);
        // The API reconnects to the restarted database on its own (about 45 s here); persistence is what is checked.
        await poll('record readable after database restart', async () => {
          await app.reload();
          return await app.getByText(token, { exact: false }).first().isVisible({ timeout: 5_000 }).catch(() => false) ||
            await app.getByText(token, { exact: false }).first().waitFor({ timeout: 5_000 }).then(() => true, () => false);
        }, 180_000);
      }, app);
      await check(checks[8], async () => {
        // Say which list, as a person would: an app can have several (events and their dishes, say).
        await page.locator('#work-request').fill(
          `Add a search box that filters the list where "${adapter.previous}" appears, so typing part of a name shows only the matching entries.`);
        await page.getByRole('button', { name: 'Start a wave', exact: true }).click();
        const second = await waitForWave(page, result, 2 + (result.fixWaves || 0));
        assert.equal(second.summary.state, 'Completed');
        validationPassed(second);
      });
      await check(checks[9], async () => {
        await poll('new deployed revision in original namespace', async () => {
          const topology = await read(page, result.id, '/workspace/topology');
          assert.equal(topology.namespace, result.namespace);
          const namespace = JSON.parse(kubectl('get', 'ns', result.namespace, '-o', 'json'));
          assert.equal(namespace.metadata.uid, namespaceUid, 'Namespace was recreated');
          const deployments = await read(page, result.id, '/workspace/deployments');
          const latest = deployments.revisions.at(-1);
          if (latest && (latest.phase === 4 || latest.phase === 'Failed')) throw new Error('Update deployment failed');
          return topology.state === 'deployed' && topology.revision !== firstRevision &&
            (await read(page, result.id, '/workspace/online')).online;
        });
      });
      await check(checks[10], async () => {
        await app.reload();
        await app.getByText(token, { exact: false }).first().waitFor();
      }, app);
      await check(checks[11], async () => {
        const secondToken = `Other-${Date.now()}`;
        // Only create the second record: later journey steps (vote, borrow…) assume a single record
        // and would hit two matching buttons now.
        const created = adapter.steps.findIndex(step => step.action === 'text' && step.value?.includes(adapter.previous));
        await performSteps(app, adapter.steps.slice(0, created + 1), adapter.previous, secondToken);
        // Creating a record may open its own page; search lives on the app's home page.
        await app.goto(`${origin}/online/${result.id}/`);
        // Find the search box the way a person would: by its role, its label or its hint text.
        const search = app.getByRole('searchbox')
          .or(app.getByLabel(/search|filter/i))
          .or(app.locator('input[placeholder*="search" i], input[placeholder*="filter" i]'))
          .first();
        // Search filters its list: the other record disappears from at least one place (other sections,
        // like a vote list, may rightly still show every record) and comes back when the search is cleared.
        const visible = async text => {
          const matches = app.getByText(text, { exact: false });
          let count = 0;
          for (let index = 0; index < await matches.count(); index++)
            if (await matches.nth(index).isVisible()) count++;
          return count;
        };
        const unfiltered = await poll('second record visible', async () => (await visible(secondToken)) || false, 30_000);
        await search.fill(token);
        await poll('search hides the other record', async () =>
          await visible(token) > 0 && await visible(secondToken) < unfiltered, 30_000);
        await search.fill('');
        await poll('cleared search shows the other record again', async () =>
          await visible(secondToken) === unfiltered, 30_000);
      }, app);
      await check(checks[12], async () => {
        await poll('all pipeline steps Done', async () => {
          await pipeline(page);
          return await page.locator('#pipeline-steps .pipeline-step[data-state="done"]').count() === 4;
        }, 120_000);
        await save(result.directory, 'whats-happening.txt', await pipeline(page));
      });
      async function shapeFlow() {
        let topology;
        await check('Topology matches contract and cluster', async () => { topology = await topologyMatches(page, result, item.expect); });
        result.namespace = topology.namespace;
        await check('Every front end answers', () => frontendsAnswer(page, result));
        if (!item.grow) {
          for (const name of shapeChecks.slice(2)) result.checks[name] = { status: 'SKIP' };
          return;
        }
        const namespaceUid = JSON.parse(kubectl('get', 'ns', result.namespace, '-o', 'json')).metadata.uid;
        app = await context.newPage();
        app.setDefaultTimeout(30_000);
        trackRequests(app);
        const token = `E2E-${item.key}-${Date.now()}`;
        await check('Create record and reload', async () => {
          const adapter = await recordAdapter(page, result);
          const response = await app.goto(`${origin}/online/${result.id}/`);
          assert(response.ok(), `Online browser HTTP ${response.status()}`);
          await performSteps(app, adapter.steps, adapter.previous, token);
          await app.reload();
          await app.getByText(token, { exact: false }).first().waitFor();
        }, app);
        const before = (await read(page, result.id, '/workspace/deployments')).revisions.length;
        await check('Architecture-changing wave', async () => {
          await page.locator('#work-request').fill(item.grow.request);
          await page.getByRole('button', { name: 'Start a wave', exact: true }).click();
          let wave = await waitForWave(page, result, 2 + (result.fixWaves || 0));
          for (let attempt = 0; attempt < 2 && wave.summary.validationState !== 'Passed' && await page.locator('#ask-fix').isVisible(); attempt++) {
            await page.locator('#ask-fix').click();
            await page.locator('#send-work').click();
            result.fixWaves = (result.fixWaves || 0) + 1;
            wave = await waitForWave(page, result, 2 + result.fixWaves);
          }
          assert.equal(wave.summary.state, 'Completed');
          validationPassed(wave);
        });
        await check('Redeployed into same namespace', async () => {
          let nudged = false;
          const started = Date.now();
          await poll('new deployed revision in original namespace', async () => {
            const deployments = await read(page, result.id, '/workspace/deployments');
            const latest = deployments.revisions.at(-1);
            // The page puts an updated app back online after its wave; if the reload above interrupted that, do it as a person would.
            if (deployments.revisions.length === before && !nudged && Date.now() - started > 90_000) {
              nudged = true;
              await page.locator('#put-online').click();
            }
            if (deployments.revisions.length > before && latest && (latest.phase === 4 || latest.phase === 'Failed'))
              throw new Error(`Update deployment failed: ${latest.reason || 'inspect deployment state'}`);
            assert.equal(JSON.parse(kubectl('get', 'ns', result.namespace, '-o', 'json')).metadata.uid, namespaceUid, 'Namespace was recreated');
            return deployments.revisions.length > before && (latest.phase === 3 || latest.phase === 'Deployed') &&
              (await read(page, result.id, '/workspace/online')).online;
          });
        });
        await check('Topology matches after the change', async () => {
          const changed = await topologyMatches(page, result, item.grow.expect);
          assert.equal(changed.namespace, result.namespace);
          assert.notEqual(changed.revision, topology.revision, 'The topology is drawn from the new revision');
        });
        await check('Earlier record survives update', async () => {
          await poll('record still listed', async () => {
            await app.goto(`${origin}/online/${result.id}/`);
            return app.getByText(token, { exact: false }).first().waitFor({ timeout: 10_000 }).then(() => true, () => false);
          }, 180_000);
        }, app);
        await check('Every front end answers after the change', () => frontendsAnswer(page, result));
      }
    } catch (error) {
      result.checks[active] = { status: 'FAIL', error: safeError(error) };
      console.log(`[${item.key}] FAIL ${active}: ${safeError(error)}`);
      if (app) await screenshot(app, result.directory, 'failure-app').catch(() => {});
      await diagnostics(page, result);
    } finally {
      await context.close();
      await writeReport();
    }
  }
} finally {
  await browser.close();
}
console.log(`Report: ${path.join(output, 'REPORT.md')}`);
process.exitCode = results.every(result => checks.every(check => ['PASS', 'SKIP'].includes(result.checks[check]?.status))) ? 0 : 1;
