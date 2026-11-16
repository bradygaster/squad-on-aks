const $ = selector => document.querySelector(selector);
function announce(selector, text) {
    const region = $(selector);
    if (region.textContent !== text) region.textContent = text;
}
const appId = new URLSearchParams(location.search).get('app');
const appPrefix = appId ? `/apps/${encodeURIComponent(appId)}` : '';
const appUrl = path => path === '/apps' ? path : appPrefix + path;
const storageKey = appId === 'first' ? 'soaks:conversation' : `soaks:conversation:${appId}`;
let libraryApps = [];
let lastLibrary = '';
let creatingApp = false;
// Where an imported app came from (from /workspace/source); { imported: false } for apps started from a description.
let source = { imported: false };
let importRepos = [];
let importChoice = null;
let importing = false;
let online = { available: false, online: false, url: null, message: '' };
let onlineBusy = false;
let fixRequest = '';
let localPreviewAvailable = false;
let threadId = crypto.randomUUID();
let previousRunId = null;
let messages = [];
let proposalText = '';
let appTitle = '';
let idea = '';
let refinements = [];
let startupQuestion = '';
let startingApp = false;
let generation = null;
let ready = false;
let busy = false;
let castingBusy = false;
// Measured waiting times only: when this turn started and when the team last reported anything.
let turnStartedAt = 0;
let lastSignalAt = 0;
let hasCast = false;
let teamMembers = [];
let teamLoaded = false;
let waves = [];
let selectedWave = null;
let selectionVersion = 0;
let previewUrl = null;
let serverBacked = false;
let runtimeStatus = { state: 'NotChecked', message: '' };
let reviewedPublication = null;
let publishing = false;
let polling = 0;
let refreshVersion = 0;
let lastTimeline = '';
let activityScope = null;
let activityRunning = false;
let lastActivity = '';

// Render bounded summaries and team identities, never source or raw model output.
const technical = /```|`|<[^>]+>|https?:|(?:\b(?:AKS|Kubernetes|clusters?|pods?|containers?|instances?|git|commits?|branches|branch|diffs?|PR|pull request|workspaces?|terminal|stdout|stderr|bash|npm|dotnet|tests?\/|charter|routing|fixture|scripts?|traces?|logs?|code|coding|hire|HTML|CSS|JS|JavaScript|TypeScript|dependencies|databases?|network requests?|network tools?)\b)|\b[\w.-]+\.(?:html?|css|[cm]?jsx?|tsx?|json|cs|py|sh)\b|(?:[\w.-]+\/(?:[\w.-]+\/)*[\w.-]+)|\b[a-f0-9]{12,}\b|[{}]|^\s*[#|]/i;
function plain(text, fallback = '') {
    const value = String(text ?? '').trim();
    if (!value || technical.test(value)) return fallback;
    return value.replace(/\*\*/g, '').slice(0, 600);
}
// The person's own words: drop only real leaks (code, markup, links, paths, ids), not everyday words like "database".
const leak = /```|`|<[^>]+>|https?:|\b[\w.-]+\.(?:html?|css|[cm]?jsx?|tsx?|json|cs|py|sh)\b|(?:[\w.-]+\/(?:[\w.-]+\/)+[\w.-]+)|\b[a-f0-9]{12,}\b|[{}]|^\s*[#|(]/i;
// Squad's own instructions appended to what the person typed; never shown back to them.
const instructionMarkers = ['\n\n(Also give this app', '\n\n(When you finish, ', '\n\n(Squad on AKS'];
function withoutInstructions(text) {
    let value = String(text ?? '');
    for (const marker of instructionMarkers) {
        const cut = value.indexOf(marker);
        if (cut >= 0) value = value.slice(0, cut);
    }
    return value;
}
function humanIntent(text, fallback) {
    // Remove implementation clauses from the person's words, not just individual jargon tokens.
    const sentences = withoutInstructions(text).split(/(?<=[.!?])\s+|\n+/);
    const safe = sentences.map(sentence => sentence
        .replace(/\s+(?:in|using|with)\s+(?:(?:plain|vanilla|pure)\s+)?(?:HTML|CSS|JavaScript|TypeScript|React|Python|C#|\.NET)\b.*$/i, '')
        .replace(/\s+(?:served|hosted)\s+(?:from|in|on)\b.*$/i, '')
        .replace(/^\s*wave\s*\d+\s*[:.—–-]\s*/i, '')
        .trim()).filter(sentence => sentence && !leak.test(sentence));
    const joined = safe.join(' ');
    return (joined.length > 300 ? joined.slice(0, 300).replace(/[\s,:;—–-]+\S*$/, '') + '…' : joined) || fallback;
}
const titleRequest = '\n\n(Also give this app a short, descriptive name of two to four words. Put it on the very first line of your reply as "App name: <name>". If an essential app requirement is unclear, put one short question on a line starting "App question:". Ask only about the app, never team setup. Otherwise omit that line. Internally organize a focused team with clear task ownership, bounded handoffs, and a distinct validation responsibility. Avoid redundant specialists and duplicate work.)';
function proposedTitle(text) {
    const match = /^\W*app name\W*[:—–-]\s*\W*(.+?)\W*$/im.exec(text ?? '');
    const name = match?.[1].replace(/[*_`"“”]/g, '').trim();
    return name && name.length <= 40 && plain(name) ? name : '';
}
function proposedQuestion(text) {
    const match = /^app question:\s*(.+)$/im.exec(text ?? '');
    const question = plain(match?.[1]);
    // Team confirmation belongs to Build my app, not app clarification.
    return /\b(?:confirm|approve|accept)\s+(?:this|the|my|your|our)\s+(?:cast|squad)\b|\bteam files\b/i.test(question)
        ? '' : question;
}
function appBrief() {
    if (idea.trimStart().startsWith('Surprise me:'))
        return [appTitle ? `${appTitle}: an app idea the team chose.` : 'The team is choosing an app idea.', ...refinements].join('\n\n');
    return [idea.split('\n\n(Also give this app')[0], ...refinements].filter(Boolean).join('\n\n');
}
// A short, human title for the idea: its first sentence, trimmed to a word boundary.
function ideaTitle(text, fallback) {
    const intent = humanIntent(text, '');
    if (!intent) return fallback;
    let title = intent.split(/(?<=[.!?])\s+/)[0].replace(/[.!?]+$/, '').replace(/[,:;—–-]\s*$/, '').replace(/^[\s(]+/, '');
    if (!title) return fallback;
    if (title.length > 60) title = title.slice(0, 60).replace(/[\s,:;—–-]+\S*$/, '') + '…';
    return title;
}
// Each wave asks for a short recap written for the person with the idea; it is stripped back out of what they asked.
const recapMarker = '\n\n(When you finish, ';
const recapRequest = recapMarker + 'end your reply with a section headed "Summary for you:" holding two to four short bullets about what someone using the app will notice now, then a section headed "Next for this app:" with up to three one-line ideas for the next change, each building on what you just made. Plain everyday words, no technical terms.)';
function asked(text) {
    const value = String(text ?? '');
    // Surprise me sends Squad's own instruction; show what it means, not the instruction text.
    if (value.trimStart().startsWith('Surprise me:')) return 'An app idea the team chose.';
    return withoutInstructions(value);
}
// The squad's own plain-language bullets from its final reply, never code or technical detail.
// The team's own ideas for the next wave, from the recap it was asked for; nothing is shown when it gave none.
function squadNextIdeas(response) {
    const text = String(response ?? '');
    const next = /next for this app\W*\n/i.exec(text);
    if (!next) return [];
    return [...new Set(text.slice(next.index + next[0].length).split('\n')
        .map(line => /^\s*(?:[-*•]|\d+[.)])\s+(.+)$/.exec(line)?.[1] ?? '')
        .map(line => line.replace(/\*\*|__/g, '').replace(/^[\p{Extended_Pictographic}\uFE0F\s]+/u, '').trim())
        .filter(line => line.length > 8 && line.length <= 120 && plain(line)))].slice(0, 3);
}
function squadHighlights(response) {
    const text = String(response ?? '');
    const recap = /summary for you\W*\n/i.exec(text);
    const next = /next for this app\W*\n/i.exec(text);
    const bullets = (recap ? text.slice(recap.index + recap[0].length, next && next.index > recap.index ? next.index : undefined)
        : next ? text.slice(0, next.index) : text).split('\n')
        .map(line => /^\s*(?:[-*•]|\d+[.)])\s+(.+)$/.exec(line)?.[1] ?? '')
        .map(line => line.replace(/\*\*|__/g, '').replace(/^[\p{Extended_Pictographic}\uFE0F\s]+/u, '').trim())
        .map(line => line.length > 150 ? line.slice(0, 150).replace(/[\s,:;—–-]+\S*$/, '') + '…' : line)
        .filter(line => line.length > 12 && plain(line));
    return recap ? [...new Set(bullets)].slice(0, 4) : [...new Set(bullets)].slice(-5);
}
function showError(message = 'Couldn’t connect. Refresh the page or try again in a moment.') {
    $('#notice').textContent = message;
    $('#notice').hidden = false;
}
function clearError() { $('#notice').hidden = true; }
function fieldError(input, output, message) {
    output.textContent = message;
    output.hidden = false;
    input.setAttribute('aria-invalid', 'true');
    input.focus();
}
function clearField(input, output) {
    input.removeAttribute('aria-invalid');
    output.textContent = '';
    output.hidden = true;
}
async function json(url, options) {
    const response = await fetch(appUrl(url), { cache: 'no-store', ...options });
    if (!response.ok) throw new Error('Request unavailable');
    return response.json();
}
function persist() {
    try { sessionStorage.setItem(storageKey, JSON.stringify({ generation, threadId, previousRunId, messages, proposalText, idea, appTitle, refinements, startupQuestion })); }
    catch { /* The squad can keep working when browser storage is unavailable. */ }
}
function restore(value) {
    generation = value;
    try {
        const saved = JSON.parse(sessionStorage.getItem(storageKey) ?? 'null');
        if (saved?.generation === generation) {
            threadId = saved.threadId || threadId;
            previousRunId = saved.previousRunId;
            messages = saved.messages ?? [];
            proposalText = saved.proposalText ?? '';
            idea = saved.idea ?? '';
            appTitle = saved.appTitle ?? '';
            refinements = saved.refinements ?? [];
            startupQuestion = saved.startupQuestion ?? '';
        } else sessionStorage.removeItem(storageKey);
    } catch { sessionStorage.removeItem(storageKey); }
}
function renderState() {
    $('#prompt-ideas').hidden = Boolean(idea) || castingBusy || ready || startingApp || source.imported;
    if (!appId) return renderLibrary();
    const started = Boolean(idea || hasCast || waves.length);
    document.body.classList.toggle('has-app', ready);
    const name = source.imported ? source.fullName : appTitle || ideaTitle(appBrief() || asked(waves[0]?.objective), 'Untitled app');
    const heading = started || source.imported ? name : 'New app';
    document.title = `${heading} · Squad`;
    $('#idea-heading').textContent = heading;
    $('#idea-heading').classList.toggle('is-long', heading.length > 28);
    const imported = source.imported;
    // An imported repository already has content: once its team is ready, work starts with a wave, not a first build.
    const firstBuild = ready && !waves.length && !imported;
    $('#idea-description').textContent = imported
        ? ready ? `From ${source.fullName}. Send waves of work; each one is saved, and you can send them back to GitHub.`
            : `From ${source.fullName}. This repository has no Squad yet: describe the work and a team will be set up for it.`
        : ready ? 'Review the result, follow your team’s work and start the next wave.' : 'Describe what you want to build. Your team will plan the work and build from your brief.';
    $('label[for="casting-prompt"]').textContent = imported ? 'What should the team work on in this repository?' : 'Describe your app';
    $('#casting-prompt').placeholder = imported ? 'Add a level editor, and tests for the scoring rules…' : 'A book-swap app where neighbors can list books and request a trade…';
    $('#casting-controls').hidden = ready && !firstBuild;
    $('#startup-input').hidden = hasCast || ready;
    $('#casting-prompt').disabled = castingBusy || startingApp || busy || hasCast || ready;
    $('#send-casting-turn').hidden = hasCast || ready;
    $('#send-casting-turn').disabled = castingBusy || startingApp || busy;
    $('#send-casting-turn').textContent = castingBusy ? 'Planning…' : proposalText ? 'Update brief' : imported ? 'Plan the work' : 'Plan app';
    $('#startup-brief').hidden = !idea && !hasCast && !firstBuild;
    $('#startup-summary').textContent = humanIntent(appBrief(), 'Your app brief is ready for its first build.');
    $('#startup-question').textContent = startupQuestion;
    $('#startup-question').hidden = !startupQuestion;
    // The question is answered right where it is asked.
    $('#startup-answer').hidden = !startupQuestion || hasCast || ready;
    $('#startup-answer-text').disabled = castingBusy || startingApp || busy;
    $('#send-startup-answer').disabled = castingBusy || startingApp || busy;
    $('#send-startup-answer').textContent = castingBusy ? 'Updating brief…' : 'Send answer';
    $('#build-app').disabled = castingBusy || startingApp || busy || Boolean(startupQuestion);
    $('#build-app').textContent = startingApp ? 'Starting…' : imported ? 'Start the first wave' : 'Build my app';
    $('#casting-hint').textContent = startupQuestion ? 'Your team has a question about the brief. Answer it below.' : proposalText || hasCast || firstBuild
        ? imported ? 'Your brief is saved. The first wave will use it.' : 'Your brief is saved. The first build will use it.' : imported ? 'Say what you want done; a team is set up around this repository and your goal.' : 'Start with the goal and who will use it.';
    $('#startup-note').textContent = startupQuestion ? 'Answer the question to build.' : hasCast || ready
        ? 'Continue with your saved brief.' : 'Build from this brief, or add details above.';
    $('#jump-to-wave').hidden = !ready || firstBuild;
    $('#making').hidden = !waves.length && !(imported && ready);
    $('#keep-section').hidden = !waves.length;
    $('#send-branch').hidden = !imported;
    $('#toggle-publish').hidden = imported;
    // A repository that isn't set up for the cluster has no running app to preview or topology to draw yet.
    $('#viewing-window').hidden = imported && !source.deployable && !previewUrl;
    if (imported) $('#work-request').placeholder = 'Describe a focused change to this repository…';
    $('#topology-section').hidden = imported && !source.deployable;
    const topologyLink = $('.project-nav a[href$="#topology-section"]');
    if (topologyLink) topologyLink.hidden = imported && !source.deployable;
    $('#work-request').disabled = !ready || busy || startingApp;
    $('#send-work').disabled = !ready || busy || startingApp;
    $('#send-work').textContent = busy ? 'Work in progress…' : 'Start a wave';
    $('#cancel-work').hidden = !busy;
    $('#ask-before-fix-field').hidden = !online.available || (source.imported && !source.deployable);
    $('#ask-before-fix').checked = askFirst();
    $('#work-heading').textContent = busy ? 'Wave in progress' : 'Start the next wave';
    announce('#team-status', castingBusy ? 'Planning app' : busy ? 'Work in progress' : startingApp ? 'Starting build' : ready ? 'Ready' : startupQuestion ? 'Question for you' : proposalText || hasCast ? 'Ready to build' : 'New app');
    renderTeam();
    renderOnline();
    renderBuilding();
    updateActivityDisclosure();
}
function updateActivityDisclosure() {
    const running = selectedWave ? waves.find(wave => wave.number === selectedWave)?.state === 'Active'
        : busy || castingBusy || startingApp;
    if (running && !activityRunning) $('#activity-section').open = true;
    activityRunning = Boolean(running);
}
function activityView(scope) {
    if (scope === activityScope) return;
    activityScope = scope;
    lastActivity = '';
    $('#activity-list').replaceChildren();
    $('#activity-error').hidden = true;
    $('#activity-empty').hidden = false;
    $('#activity-empty').textContent = 'Loading recorded activity…';
    $('#activity-limit').hidden = true;
    $('#activity-status').textContent = 'Loading…';
    $('#activity-status').dataset.failed = 'false';
    $('#activity-context').textContent = scope === 'setup' ? 'Planning and team setup' : scope === 'pending' ? 'Starting wave' : `Wave ${scope}`;
    $('#activity-section').open = scope === 'setup' || scope === 'pending' ? busy || castingBusy || startingApp
        : waves.find(wave => wave.number === scope)?.state === 'Active';
    activityRunning = false;
    updateActivityDisclosure();
}
function renderActivity(items, wave = null, failed = false) {
    const newest = Math.max(0, ...(items ?? []).map(item => Date.parse(item.timestamp) || 0));
    if (newest > lastSignalAt) lastSignalAt = newest;
    const labels = {
        Delegated: 'Assigned', Selected: 'Selected', Active: 'In progress', Progress: 'Update',
        Succeeded: 'Completed', Passed: 'Passed', Failed: 'Failed', Completed: 'Completed',
        Cancelled: 'Stopped', Cancelling: 'Stopping', Building: 'Building', Starting: 'Starting',
        Readiness: 'Health checks', Browser: 'Playwright', Persistence: 'Restart checks',
        Blocked: 'Blocked', NotChecked: 'Not checked', Unavailable: 'Unavailable'
    };
    const nativeMessages = {
        Delegated: 'Task assigned to a team member.', Selected: 'Team member selected for this task.',
        Active: 'Team member started work.', Progress: 'Working; no further detail was reported.',
        Succeeded: 'Team member finished this task.', Failed: 'Team member reported a task failure.'
    };
    const safeTask = value => typeof value === 'string' && value.length <= 200 &&
        !/[\r\n`<>{}]|https?:\/\/|\[REDACTED\]|(?:password|token|secret|api[_ -]?key)\s*[:=]|github_pat_|gh[pousr]_/i.test(value)
        ? value.trim() : '';
    const safeTools = values => Array.isArray(values) ? [...new Set(values.filter(value =>
        typeof value === 'string' && /^[a-zA-Z][a-zA-Z0-9_.:-]{0,79}$/.test(value) &&
        !/github_pat_|gh[pousr]_/i.test(value)))].slice(0, 8) : [];
    const toolLabels = {
        bash: 'command execution', shell: 'command execution', view: 'file reading', read_file: 'file reading',
        rg: 'code search', grep: 'code search', glob: 'file discovery', apply_patch: 'file editing',
        edit_file: 'file editing', create_file: 'file creation', task: 'delegation', report_intent: 'progress reporting'
    };
    const toolLabel = name => {
        const short = name.split(/[.:]/).at(-1);
        return toolLabels[short] ? `${toolLabels[short]} (${short})` : name;
    };
    // Only explicitly public metadata is shown. Legacy summaries are read solely for exact tool-name lists.
    const entries = (items ?? []).flatMap(item => {
        let actor = 'Team';
        let message;
        let task = '';
        if (item.kind === 'specialist') {
            actor = plain(item.actor, 'Team').slice(0, 80);
            task = safeTask(item.activity?.task);
            const legacyTools = /^Requested tools: ([a-zA-Z0-9_.:, -]+)$/.exec(item.summary ?? '');
            const tools = safeTools(item.activity?.tools ?? legacyTools?.[1].split(',').map(value => value.trim()));
            message = nativeMessages[item.state] ?? 'Team activity recorded.';
            if (item.state === 'Progress' && tools.length)
                message = `Requested ${tools.map(toolLabel).join(', ')}.`;
            if (task && ['Delegated', 'Selected', 'Active', 'Succeeded', 'Failed'].includes(item.state))
                message = `${message} ${task}`;
            if (actor === 'Team') message = message.replace('Team member', 'Team').replace('a team member', 'the team');
        } else if (item.kind === 'validation') {
            actor = 'Validation';
            message = item.state === 'Passed' ? 'Validation passed.'
                : item.state === 'Failed' ? 'Validation failed. Review the result before continuing.' : 'Validation result recorded.';
        } else if ((item.kind === 'preflight' || item.kind === 'runtime') && item.source === 'App acceptance') {
            actor = item.kind === 'preflight' ? 'Predictive checks' : 'Running app';
            message = safeTask(item.summary) || 'App checks updated.';
            if (item.kind === 'preflight' && typeof item.path === 'string' &&
                /^(?:[a-zA-Z0-9_-][a-zA-Z0-9_. -]*\/)*[a-zA-Z0-9_-][a-zA-Z0-9_. -]*$/.test(item.path))
                message += ` ${item.path}`;
        } else if (item.kind === 'state') {
            actor = 'Lifecycle';
            message = { Active: 'A team turn started.', Completed: 'This turn completed.',
                Failed: 'This turn didn’t finish. Earlier saved work is retained.',
                Cancelled: 'This turn was stopped. Earlier saved work is retained.',
                Cancelling: 'Stopping this turn.' }[item.state] ?? 'Work status updated.';
        } else if (item.kind === 'application' && typeof item.path === 'string' &&
            /^(?:[a-zA-Z0-9_-][a-zA-Z0-9_. -]*\/)*[a-zA-Z0-9_-][a-zA-Z0-9_. -]*$/.test(item.path) &&
            !item.path.split('/').some(segment => /^(?:node_modules|bin|obj)$/i.test(segment))) {
            actor = 'Files';
            message = `${({ Added: 'Added', Deleted: 'Deleted', Modified: 'Updated' })[item.state] ?? 'Changed'} ${item.path}`;
        } else return [];
        return [{ actor, message, task, state: item.state, timestamp: item.timestamp, count: 1 }];
    });
    if (wave?.commitSha) entries.push({
        actor: 'Checkpoint', message: 'Changes saved for this wave.', state: 'Succeeded',
        timestamp: wave.completedAt, count: 1
    });
    const grouped = [];
    for (const entry of entries) {
        const prior = grouped.at(-1);
        if (prior && entry.state === 'Progress' && prior.state === entry.state &&
            prior.actor === entry.actor && prior.message === entry.message && prior.task === entry.task) {
            prior.count++;
            prior.timestamp = entry.timestamp;
        } else grouped.push({ ...entry });
    }
    const visible = grouped.slice(-30);
    const key = JSON.stringify([visible, grouped.length > 30, failed, Boolean(wave)]);
    if (key === lastActivity) return;
    lastActivity = key;
    $('#activity-status').textContent = failed ? 'Unavailable' : entries.length
        ? `${entries.length} events · ${grouped.length} entries` : 'Waiting';
    $('#activity-status').dataset.failed = String(failed);
    $('#activity-error').hidden = !failed;
    $('#activity-empty').hidden = failed || visible.length > 0;
    $('#activity-empty').textContent = wave ? 'No public activity was recorded for this wave.'
        : 'Waiting for recorded activity. Events appear as your team reports them.';
    $('#activity-limit').hidden = grouped.length <= 30;
    $('#activity-list').replaceChildren(...visible.map(entry => {
        const item = document.createElement('li');
        const meta = document.createElement('div');
        meta.className = 'activity-meta';
        const actor = document.createElement('strong');
        actor.textContent = entry.actor;
        const state = document.createElement('span');
        state.className = 'activity-state';
        state.dataset.state = labels[entry.state] ? entry.state : '';
        state.textContent = labels[entry.state] ?? 'Recorded';
        meta.append(actor, state);
        if (entry.count > 1) {
            const repeats = document.createElement('span');
            repeats.className = 'activity-state';
            repeats.textContent = `${entry.count} updates`;
            meta.append(repeats);
        }
        const date = new Date(entry.timestamp);
        if (entry.timestamp && !Number.isNaN(date.getTime())) {
            const time = document.createElement('time');
            time.dateTime = date.toISOString();
            time.textContent = date.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit' });
            meta.append(time);
        }
        const message = document.createElement('p');
        message.textContent = entry.message;
        item.append(meta, message);
        return item;
    }));
}
function renderOnline() {
    $('#put-online').disabled = !online.available || !localPreviewAvailable || busy || castingBusy || onlineBusy ||
        (source.imported && !source.deployable);
    $('#put-online').textContent = onlineBusy ? (online.online ? 'Updating…' : 'Deploying…') : online.online ? 'Update online app' : 'Put it online';
    announce('#online-note', onlineBusy ? 'Building this revision, testing it in a throwaway copy, then deploying it to the app’s namespace…'
        : source.imported && !source.deployable ? 'This repository isn’t set up to run on your cluster. To put it online, ask your team in a wave to make it deployable with Squad.'
        : plain(online.message));
}
// Squad keeps going until the app is online: a completed wave is put online, and a version its checks hold back gets
// up to two automatic fix waves per request the person typed. Progress is kept per tab, so a reload resumes it
// instead of starting anything twice.
const autoKey = `soaks:auto:${appId}`;
const askFirstKey = `soaks:ask-before-fix:${appId}`;
const autoFixLimit = 2;
const autoFixText = {
    'journeys-failed': 'Fix: The last version didn’t pass its checks before going online. Fix what the checks found so it can go online.',
    'not-ready': 'Fix: The last version was deployed but never became ready, so it was held back. Fix what kept it from becoming ready so it can go online.'
};
const isAutoFix = objective => Object.values(autoFixText).some(text => String(objective ?? '').startsWith(text));
// From the recorded waves: how many automatic fixes in a row end with this wave.
function autoFixAttempt(number) {
    let attempt = 0;
    for (const wave of waves.filter(item => item.number <= number).reverse()) {
        if (!isAutoFix(wave.objective)) break;
        attempt++;
    }
    return attempt;
}
const autoFixLabel = wave => {
    const attempt = autoFixAttempt(wave.number);
    return attempt <= autoFixLimit ? `Automatic fix (attempt ${attempt} of ${autoFixLimit})` : 'Automatic fix';
};
function autoState() {
    try { return JSON.parse(sessionStorage.getItem(autoKey) ?? 'null') ?? { fixes: 0 }; } catch { return { fixes: 0 }; }
}
function saveAuto(value) { try { sessionStorage.setItem(autoKey, JSON.stringify(value)); } catch { } }
function askFirst() { try { return localStorage.getItem(askFirstKey) === '1'; } catch { return false; } }
let deploymentState = null;
let mending = false;
function latestRevision() {
    const revision = deploymentState?.revisions?.at(-1);
    return revision ? { ...revision, phase: phaseName(revision.phase) } : null;
}
const revisionMark = revision => revision ? `${revision.runId}:${revision.updatedAt}` : '';
async function keepGoing() {
    if (mending || !appId || busy || onlineBusy || castingBusy) return;
    const auto = autoState();
    if (auto.follow == null && !auto.online && !auto.fix) return;
    mending = true;
    try {
        if (!auto.fix && auto.follow != null) {
            const latest = waves.at(-1);
            if (!latest || latest.number <= auto.follow || ['Active', 'Cancelling'].includes(latest.state)) return;
            delete auto.follow;
            const deployable = online.available && localPreviewAvailable && (!source.imported || source.deployable);
            if (latest.state === 'Completed' && latest.validationState !== 'Failed' && deployable)
                auto.online = { since: revisionMark(latestRevision()), started: false };
            saveAuto(auto);
        }
        if (!auto.fix && auto.online) await followOnline(auto);
    } finally {
        mending = false;
        renderFix();
    }
    if (!auto.fix) return;
    // The page's own busy flag can lag behind the deployment that just finished; ask before starting the fix.
    const status = await json('/work/status').catch(() => null);
    if (status) busy = status.isBusy;
    // If the app is still busy, runWork leaves the fix pending and a later update starts it.
    await runWork(auto.fix, true);
}
// Puts the app online (or, after a reload, finds out how that went) and decides whether to fix it automatically.
async function followOnline(auto) {
    let attempted = false;
    const attempt = async () => { attempted = true; saveAuto(auto); await putOnline(); };
    if (!auto.online.started) { auto.online.started = true; await attempt(); }
    const read = async () => {
        deploymentState = await json('/workspace/deployments').catch(() => deploymentState);
        const revision = latestRevision();
        return { revision, changed: Boolean(revision) && revisionMark(revision) !== auto.online.since };
    };
    let { revision, changed } = await read();
    // After a reload the request may never have reached Squad: try it once more.
    if (!changed && !attempted && !auto.online.retried) {
        auto.online.retried = true;
        await attempt();
        ({ revision, changed } = await read());
    }
    // Still building, checking or deploying after a reload: look again on the next update.
    if (changed && !['Deployed', 'Failed'].includes(revision.phase)) return;
    delete auto.online;
    const reason = changed && revision.phase === 'Failed' ? revision.reason : null;
    if (autoFixText[reason] && !askFirst() && auto.fixes < autoFixLimit) {
        auto.fixes++;
        auto.fix = autoFixText[reason];
    }
    saveAuto(auto);
}
async function putOnline() {
    if (!online.available || !localPreviewAvailable || busy || onlineBusy) return;
    onlineBusy = true;
    clearError();
    renderOnline();
    try {
        const response = await fetch(appUrl('/workspace/online'), { method: 'POST', cache: 'no-store' });
        if (!response.ok) {
            showError(plain(await response.text(), 'We couldn’t put your app online. Your idea is safe here. Please try again.'));
            return;
        }
        online = await response.json();
        await refreshPreview();
        if (previewUrl) $('#app-preview').src = previewUrl;
    } catch { showError('We couldn’t put your app online. Your idea is safe here. Please try again.'); }
    finally { onlineBusy = false; renderOnline(); }
}
function renderLibrary() {
    const key = JSON.stringify([libraryApps, creatingApp]);
    if (key === lastLibrary) return;
    lastLibrary = key;
    $('#create-app').disabled = creatingApp;
    $('#create-app').textContent = creatingApp ? 'Creating…' : 'New app';
    const focused = document.activeElement?.dataset.app;
    const entries = libraryApps.map(app => {
        const item = document.createElement('li');
        item.className = 'app-plot';
        const link = document.createElement('a');
        link.href = `?app=${encodeURIComponent(app.id)}`;
        link.setAttribute('data-enhance-nav', 'false');
        link.dataset.app = app.id;
        const art = document.createElement('span');
        art.className = 'plot-art';
        art.setAttribute('aria-hidden', 'true');
        art.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true"><rect x="4" y="3" width="16" height="18" rx="2" /><path d="M8 3v18M11 8h5M11 12h5" /></svg>';
        const name = document.createElement('h3');
        // An imported app is titled owner/repo, which the strict filter would treat as a path.
        name.textContent = /^[A-Za-z0-9-]{1,39}\/[A-Za-z0-9._-]{1,100}$/.test(app.title ?? '') ? app.title : plain(app.title, 'Untitled app');
        const state = document.createElement('span');
        state.className = 'plot-state';
        const stateLabels = { Active: 'In progress', Casting: 'Planning', Cancelling: 'Stopping', Publishing: 'Publishing', Failed: 'Failed' };
        state.textContent = stateLabels[app.state] || (app.topology?.state === 'deployed' ? 'Online'
            : app.runtimeState === 'Ready' ? 'Verified' : app.previewAvailable ? 'Preview available'
            : app.latest ? waveLabel(app.latest)
            : app.hasCast ? 'Ready to build' : 'Not started');
        state.dataset.state = state.textContent === 'Online' ? 'online' : state.textContent === 'Needs attention' ? 'attention' : '';
        link.append(art, name, state);
        if (app.topology) {
            const mini = document.createElement('span');
            mini.className = 'plot-topology';
            mini.dataset.topologyApp = app.id;
            mini.setAttribute('aria-hidden', 'true');
            link.append(mini);
        }
        const update = document.createElement('p');
        update.className = 'plot-update';
        update.textContent = app.updateUnavailable ? 'Latest update unavailable. Open the app to reconnect.'
            : app.first ? humanIntent(asked(app.first.objective), 'App work') : 'No waves yet.';
        link.append(update);
        const updatedAt = app.latest?.completedAt || app.latest?.startedAt;
        if (updatedAt) {
            const date = document.createElement('time');
            date.dateTime = updatedAt;
            date.textContent = new Date(updatedAt).toLocaleString(undefined, { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
            link.append(date);
        }
        item.append(link);
        return item;
    });
    const empty = document.createElement('li');
    empty.className = 'app-plot empty-plot';
    const create = document.createElement('button');
    create.type = 'button';
    create.id = 'new-app';
    create.disabled = creatingApp;
    create.dataset.app = 'new';
    const plus = document.createElement('span');
    plus.className = 'plot-plus';
    plus.setAttribute('aria-hidden', 'true');
    plus.textContent = '+';
    const heading = document.createElement('h3');
    heading.textContent = creatingApp ? 'Creating app…' : 'Create a new app';
    const note = document.createElement('span');
    note.textContent = 'Separate team and work history';
    create.append(plus, heading, note);
    create.addEventListener('click', createApp);
    empty.append(create);
    $('#app-library').replaceChildren(...entries, empty);
    mountLibraryTopologies();
    if (focused) [...$('#app-library').querySelectorAll('[data-app]')].find(element => element.dataset.app === focused)?.focus({ preventScroll: true });
}
async function createApp() {
    if (creatingApp) return;
    creatingApp = true;
    clearError();
    renderLibrary();
    try {
        const app = await json('/apps', { method: 'POST' });
        location.assign(`?app=${encodeURIComponent(app.id)}`);
    } catch {
        creatingApp = false;
        renderLibrary();
        showError('Couldn’t create a new app. Please try again.');
    }
}
function openImport() {
    clearError();
    importChoice = null;
    $('#import-result').hidden = true;
    $('#import-filter').value = '';
    $('#import-detail').textContent = '';
    $('#import-dialog').showModal();
    loadImportOwners();
}
async function loadImportOwners() {
    const select = $('#import-owner');
    select.disabled = true;
    try {
        const owners = await json('/github/owners');
        select.replaceChildren();
        if (!owners.available) {
            const option = document.createElement('option'); option.textContent = 'GitHub isn’t connected'; select.append(option);
            importFailed(plain(owners.reason, 'No GitHub token is configured for this Squad.'));
            return;
        }
        for (const owner of [owners.login, ...owners.organizations]) {
            const option = document.createElement('option'); option.value = owner; option.textContent = owner; select.append(option);
        }
        select.disabled = false;
        await loadImportRepos();
    } catch { importFailed('Your GitHub accounts couldn’t be loaded. Try again in a moment.'); }
}
async function loadImportRepos() {
    importRepos = [];
    importChoice = null;
    renderImportRepos('Loading repositories…');
    try {
        const response = await fetch(`/github/repositories?owner=${encodeURIComponent($('#import-owner').value)}`, { cache: 'no-store' });
        const body = await response.json();
        if (!response.ok) return importFailed(plain(body.message, 'Repositories couldn’t be loaded.'));
        importRepos = body;
        renderImportRepos();
    } catch { importFailed('Repositories couldn’t be loaded. Try again in a moment.'); }
}
function renderImportRepos(message) {
    const list = $('#import-repos');
    const filter = $('#import-filter').value.trim().toLowerCase();
    const shown = importRepos.filter(repo => !filter || repo.name.toLowerCase().includes(filter)).slice(0, 50);
    const items = shown.map(repo => {
        const item = document.createElement('li');
        const button = document.createElement('button');
        button.type = 'button';
        button.setAttribute('aria-pressed', String(importChoice?.fullName === repo.fullName));
        const name = document.createElement('strong'); name.textContent = repo.name;
        const note = document.createElement('span');
        note.textContent = [repo.private ? 'Private' : 'Public', repo.description].filter(Boolean).join(' · ');
        button.append(name, note);
        button.addEventListener('click', () => chooseImport(repo));
        item.append(button);
        return item;
    });
    if (!items.length) {
        const empty = document.createElement('li'); empty.className = 'empty';
        empty.textContent = message || (importRepos.length ? 'No repositories match.' : 'No repositories here.');
        items.push(empty);
    }
    list.replaceChildren(...items);
    $('#import-submit').disabled = importing || !importChoice?.detail;
    $('#import-submit').textContent = importing ? 'Copying repository…' : 'Start app';
}
async function chooseImport(repo) {
    importChoice = { ...repo, detail: null };
    $('#import-result').hidden = true;
    announce('#import-detail', `Checking ${repo.fullName}…`);
    renderImportRepos();
    try {
        const [owner, name] = repo.fullName.split('/');
        const response = await fetch(`/github/repository?owner=${encodeURIComponent(owner)}&name=${encodeURIComponent(name)}`, { cache: 'no-store' });
        const detail = await response.json();
        if (importChoice?.fullName !== repo.fullName) return;
        if (!response.ok) return importFailed(plain(detail.message, 'That repository couldn’t be read.'));
        importChoice.detail = detail;
        announce('#import-detail', (detail.hasSquad ? 'Has a Squad. Its team will be used, so you can send waves right away.'
            : 'No Squad yet. You’ll describe the work and a team will be set up for this repository.') +
            (detail.hasContract ? ' It can be put online on your cluster.' : ' It isn’t set up to run on your cluster; the team can make it deployable if you ask.'));
        renderImportRepos();
    } catch { importFailed('That repository couldn’t be read. Try again in a moment.'); }
}
function importFailed(message) {
    $('#import-result').textContent = message;
    $('#import-result').hidden = false;
    renderImportRepos(' ');
}
async function submitImport(event) {
    event.preventDefault();
    if (importing || !importChoice?.detail) return;
    importing = true;
    renderImportRepos();
    try {
        const [owner, name] = importChoice.fullName.split('/');
        const response = await fetch('/library/import', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ owner, name }) });
        const body = await response.json();
        if (!response.ok) throw new Error(plain(body.message, 'The repository couldn’t be copied.'));
        location.assign(`?app=${encodeURIComponent(body.id)}`);
    } catch (error) {
        importing = false;
        importFailed(error.message || 'The repository couldn’t be copied. Try again in a moment.');
    }
}
let sending = false;
async function sendBranch() {
    if (sending) return;
    sending = true;
    $('#send-branch').disabled = true;
    $('#send-branch').textContent = 'Sending…';
    const result = $('#send-result');
    try {
        const response = await fetch(appUrl('/workspace/send'), { method: 'POST' });
        const body = await response.json();
        result.replaceChildren(plain(body.message, response.ok ? 'Sent.' : 'Sending failed.'));
        if (response.ok && body.url) {
            const link = document.createElement('a');
            link.href = body.url; link.target = '_blank'; link.rel = 'noopener';
            link.textContent = ' Compare on GitHub ↗';
            result.append(link);
        }
    } catch { result.textContent = 'Sending failed. Try again in a moment.'; }
    result.hidden = false;
    sending = false;
    $('#send-branch').disabled = false;
    $('#send-branch').textContent = 'Send to GitHub';
}
function ago(ms) {
    const seconds = Math.max(0, Math.round(ms / 1000));
    return seconds < 60 ? `${seconds} s` : `${Math.round(seconds / 60)} min`;
}
// While the team works, say how long it has been and when it last reported, so silence is visible, not hidden.
function renderWaitClock() {
    const clock = $('#wait-clock');
    const working = busy || castingBusy;
    if (!working) { turnStartedAt = 0; clock.hidden = true; return; }
    const started = Date.parse(waves.at(-1)?.state === 'Active' && busy ? waves.at(-1).startedAt : '') || turnStartedAt || Date.now();
    turnStartedAt = started;
    const now = Date.now();
    const heard = Math.max(lastSignalAt, started);
    const quiet = now - heard;
    clock.hidden = false;
    clock.dataset.quiet = String(quiet > 3 * 60_000);
    clock.textContent = quiet > 3 * 60_000
        ? `Started ${ago(now - started)} ago · no update from your team for ${ago(quiet)}. A turn is stopped after 15 min without one.`
        : `Started ${ago(now - started)} ago · last update ${ago(quiet)} ago`;
}
setInterval(() => { if (appId) renderWaitClock(); }, 1000);
// When Squad is opened on this machine, each online app gets its own origin ({id}.localhost on the same port), so
// browsers treat it as its own site: its scripts load normally and it never shares Squad's origin.
function ownOrigin(url) {
    const match = /^\/online\/([0-9a-f]{32})\/(.*)$/.exec(new URL(url, location.href).pathname);
    if (!match || !['127.0.0.1', 'localhost', '[::1]'].includes(location.hostname)) return null;
    return `${location.protocol}//${match[1]}.localhost${location.port ? `:${location.port}` : ''}/${match[2]}`;
}
function isOwnOrigin(url) {
    try { return /^[0-9a-f]{32}\.localhost$/.test(new URL(url, location.href).hostname); } catch { return false; }
}
function renderBuilding() {
    const building = busy && ready;
    $('#viewing-window').classList.toggle('is-building', building);
    $('#viewing-window').setAttribute('aria-busy', String(building));
    const checking = ['Starting', 'Readiness', 'Browser', 'Persistence'].includes(runtimeStatus.state);
    const failed = ['Failed', 'Blocked', 'Unavailable', 'Cancelled'].includes(runtimeStatus.state);
    announce('#preview-status', checking ? runtimeStatus.state === 'Browser' ? 'Playwright testing…' : 'Checking app…'
        : building ? 'Build in progress…' : failed ? 'Not verified' : runtimeStatus.state === 'Ready' ? 'Verified'
        : previewUrl ? 'Preview available' : 'No preview yet');
    $('#preview-empty-title').textContent = serverBacked ? previewUrl ? 'Your app is running' : 'Running app not verified'
        : building ? 'Build in progress' : 'Your app preview will appear here';
    $('#preview-empty-note').textContent = serverBacked
        ? previewUrl ? 'Use Open app to use the real application in its own tab.'
            : plain(runtimeStatus.message, 'This app needs its services running and browser checks completed. A static preview is not the application.')
        : building ? 'Your team is working on this app. Results and contributors appear in the wave below.'
        : 'Plan your app, then start a build. The preview becomes available when there’s a result to open.';
}

// Keep the existing AG-UI conversation protocol, but never render its raw transcript.
async function streamAgentTurn(text, phase) {
    const message = { id: crypto.randomUUID(), role: 'user', content: text };
    messages.push(message);
    let answer = '';
    try {
        const response = await fetch(appUrl('/ag-ui'), {
            method: 'POST', headers: { 'content-type': 'application/json', 'X-Squad-Phase': phase },
            body: JSON.stringify({ threadId, runId: crypto.randomUUID(), parentRunId: previousRunId, messages, state: {}, tools: [], context: [], forwardedProps: {} })
        });
        if (!response.ok || !response.body) { messages.pop(); throw new Error('Turn unavailable'); }
        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let buffer = '';
        let runError = false;
        function consume(frame) {
            const data = frame.split('\n').filter(line => line.startsWith('data:')).map(line => line.slice(5).trimStart()).join('\n');
            if (!data) return;
            const event = JSON.parse(data);
            if (event.type === 'RUN_STARTED') previousRunId = event.runId;
            if (event.type === 'TEXT_MESSAGE_CONTENT') answer += event.delta ?? '';
            if (event.type === 'RUN_ERROR') runError = true;
        }
        try {
            while (true) {
                const { value, done } = await reader.read();
                if (value?.length) lastSignalAt = Date.now();
                buffer += decoder.decode(value, { stream: !done }).replace(/\r\n/g, '\n');
                const frames = buffer.split('\n\n');
                buffer = frames.pop() ?? '';
                frames.forEach(consume);
                if (done) { if (buffer.trim()) consume(buffer); break; }
            }
        } finally { reader.releaseLock(); }
        if (answer.trim()) messages.push({ id: crypto.randomUUID(), role: 'assistant', content: answer });
        if (runError) throw new Error('Turn did not finish');
        return answer;
    } finally { persist(); }
}
async function propose() {
    if (castingBusy || startingApp || busy || ready) return;
    const input = $('#casting-prompt');
    const text = input.value.trim();
    if (!text) return fieldError(input, $('#casting-prompt-error'), 'Describe the app you want to build.');
    clearField(input, $('#casting-prompt-error'));
    clearError();
    castingBusy = true;
    if (!idea) idea = text;
    renderState();
    try {
        proposalText = await streamAgentTurn(text + titleRequest, 'casting');
        if (!proposalText.trim()) throw new Error('No proposal');
        appTitle = proposedTitle(proposalText) || appTitle;
        startupQuestion = proposedQuestion(proposalText);
        if (text !== idea) refinements.push(text);
        persist();
        if (appTitle) await json('/casting/title', { method: 'PUT', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ title: appTitle }) });
        input.value = '';
        persist();
    } catch { showError('Planning didn’t finish. Your saved brief is still here; try again in a moment.'); }
    finally {
        castingBusy = false;
        renderState();
        // The team was busy while planning; read its state now rather than waiting for the next poll.
        refresh().catch(() => { });
    }
}
// Sends the answer through the same planning turn as the brief box, so the brief is updated with it.
async function answerQuestion() {
    if (castingBusy || startingApp || busy || ready) return;
    const answer = $('#startup-answer-text');
    const text = answer.value.trim();
    if (!text) return fieldError(answer, $('#startup-answer-error'), 'Type an answer, or say “no preference”.');
    clearField(answer, $('#startup-answer-error'));
    $('#casting-prompt').value = text;
    await propose();
    if (!$('#casting-prompt').value) answer.value = '';
    if (startupQuestion) answer.focus();
}
async function loadReview() {
    const status = await json('/casting/status');
    hasCast = status.hasCast;
}
function reviewMembers(files) {
    const team = files.find(file => file.path === '.squad/team.md')?.content;
    if (!team) throw new Error('Team roster unavailable');
    const clean = text => text.replace(/\[([^\]]+)\]\([^)]*\)/g, '$1').replace(/[*_`]/g, '').trim();
    const members = [];
    const seen = new Set();
    const roster = /(?:^|\n)## Members[^\n]*\n([\s\S]*?)(?=\n## |$)/.exec(team)?.[1];
    if (!roster) throw new Error('Team roster unavailable');
    for (const line of roster.split('\n')) {
        const path = /\.squad\/agents\/[a-zA-Z0-9][a-zA-Z0-9-]*\/charter\.md/.exec(line)?.[0];
        if (!path || seen.has(path)) continue;
        const charter = files.find(file => file.path === path)?.content;
        if (!charter) throw new Error('Team charter unavailable');
        const cells = line.trim().startsWith('|') ? line.split('|').map(clean).filter(Boolean) : [];
        const beforePath = clean(line.split(path)[0]).replace(/\[$/, '').replace(/^[-+]\s*/, '').trim();
        const name = cells.length ? cells[0] : clean(beforePath.split(':')[0]).replace(/\s*[—–-]\s*$/, '').trim();
        const paragraph = charter.split('\n').map(clean).find(text => text && !/^[#>|-]/.test(text));
        const role = plain(cells.length >= 3 ? cells[1] : paragraph, 'Team member');
        if (!name || name.length > 80 || /[<>{}\u0000-\u001f]/.test(name)) throw new Error('Invalid team name');
        seen.add(path);
        members.push({ name, role: role.slice(0, 100) });
    }
    if (!members.length) throw new Error('Team members unavailable');
    return members;
}
async function loadTeam() {
    if (!hasCast || teamLoaded) return;
    try {
        teamMembers = reviewMembers(await json('/casting/review'));
        teamLoaded = true;
        $('#team-error').hidden = true;
    } catch {
        announce('#team-error', 'Team details couldn’t be loaded. Refresh the page to try again. Your app and waves are still available.');
        $('#team-error').hidden = false;
    }
    renderTeam();
}
function renderTeam() {
    $('#team-description').textContent = teamLoaded ? 'Assigned to this app. Each wave records who contributed.'
        : startingApp || hasCast ? 'Setting up the team for this app…' : 'A team is created for this app when you start its first build.';
    $('#member-count').hidden = !teamMembers.length;
    $('#member-count').textContent = teamMembers.length;
    if ($('#team-members').dataset.roster === JSON.stringify(teamMembers)) return;
    $('#team-members').dataset.roster = JSON.stringify(teamMembers);
    $('#team-members').replaceChildren(...teamMembers.map(member => {
        const item = document.createElement('li');
        const avatar = document.createElement('span');
        avatar.className = 'member-avatar';
        avatar.setAttribute('aria-hidden', 'true');
        avatar.textContent = member.name.split(/\s+/).map(word => word[0]).join('').slice(0, 2).toUpperCase();
        const details = document.createElement('span');
        details.className = 'member-details';
        const name = document.createElement('strong');
        name.textContent = member.name;
        const role = document.createElement('span');
        role.className = 'member-role';
        role.textContent = member.role;
        details.append(name, role);
        item.append(avatar, details);
        return item;
    }));
}
async function castSquad() {
    castingBusy = true;
    renderState();
    try {
        const confirm = `The user chose Build my app for this app brief:\n\n${appBrief()}\n\nI explicitly confirm this proposal. Create the Squad now using the native Init Mode confirmation path. Organize a focused team with clear ownership, non-overlapping tasks, bounded handoffs, and independent validation. Prefer the smallest effective team and avoid duplicated work. Only create the team files in this turn: do not start, plan in code, or write any project work yet. The initial app brief will arrive immediately afterward as a separate work request.`;
        if (!hasCast) {
            await streamAgentTurn(confirm, 'cast-review');
            await loadReview();
        }
        const activation = await fetch(appUrl('/casting/activate'), { method: 'POST', cache: 'no-store' });
        if (!activation.ok) {
            // The squad sometimes writes part of its team early; ask it to finish the setup, then try once more.
            await streamAgentTurn(confirm + ' Some team files already exist: complete every missing piece, including each member’s history file.', 'cast-review');
            await loadReview();
            await json('/casting/activate', { method: 'POST' });
        }
        ready = true;
        await loadIntent();
        await loadTeam();
    } finally { castingBusy = false; renderState(); }
}
async function buildApp() {
    if (startingApp || castingBusy || busy || startupQuestion) return;
    // Unsent details must not be silently left out of the first build.
    if ($('#casting-prompt').value.trim()) {
        return fieldError($('#casting-prompt'), $('#casting-prompt-error'), 'Update your idea to include these details before building.');
    }
    startingApp = true;
    clearError();
    renderState();
    try {
        await refresh();
        if (waves.length || busy) return;
        if (!appBrief().trim()) throw new Error('Saved idea unavailable');
        if (!ready) await castSquad();
        await refresh();
        await runWork(appBrief());
    } catch { showError('We couldn’t start your app. Your idea is saved. Choose Build my app to try again.'); }
    finally { startingApp = false; renderState(); }
}
async function loadIntent() {
    const response = await fetch(appUrl('/casting/intent'), { cache: 'no-store' });
    if (response.ok) { const result = await response.json(); idea = result.text || idea; persist(); }
    else if (response.status !== 404) throw new Error('Saved idea unavailable');
}
const stateNames = { Active: 'In progress', Completed: 'Completed', Failed: 'Failed', Cancelled: 'Stopped', Cancelling: 'Stopping' };
// A finished wave whose checks failed needs attention; "Completed" alone reads as success.
const waveLabel = wave => wave?.state === 'Completed' && wave.validationState === 'Failed'
    ? 'Needs attention' : stateNames[wave?.state] ?? 'Pending';
function renderTimeline() {
    const key = JSON.stringify([waves, selectedWave]);
    if (key === lastTimeline) return;
    lastTimeline = key;
    const focused = document.activeElement?.dataset.wave;
    $('#waves-empty').hidden = waves.length > 0;
    $('#waves-empty').textContent = source.imported ? 'No waves yet. Describe a change below to send the first one.' : 'No waves yet. Start a build to create the first one.';
    $('#wave-list').replaceChildren();
    for (const wave of [...waves].reverse()) {
        const li = document.createElement('li');
        const button = document.createElement('button');
        button.type = 'button';
        button.dataset.wave = wave.number;
        button.setAttribute('aria-pressed', String(wave.number === selectedWave));
        const heading = document.createElement('strong');
        heading.textContent = `Wave ${wave.number}`;
        const summary = document.createElement('span');
        summary.textContent = isAutoFix(wave.objective) ? autoFixLabel(wave) : ideaTitle(asked(wave.objective), 'A round of work on your idea');
        const state = document.createElement('span');
        state.className = 'timeline-state';
        state.textContent = waveLabel(wave);
        button.append(heading, summary, state);
        button.addEventListener('click', () => selectWave(wave.number).catch(() => showError('This wave couldn’t be opened. Please try again.')));
        li.append(button);
        $('#wave-list').append(li);
    }
    if (focused) $(`#wave-list [data-wave="${focused}"]`)?.focus({ preventScroll: true });
}
function renderNotice(detail) {
    const wave = detail.summary;
    const evidence = detail.evidence ?? [];
    const checked = wave.validationState ?? evidence.filter(item => item.kind === 'validation').at(-1)?.state;
    $('#wave-notice').hidden = false;
    $('#evidence-heading').textContent = `Wave ${wave.number}`;
    $('#wave-state').textContent = waveLabel(wave);
    $('#wave-state').dataset.state = waveLabel(wave) === 'Needs attention' ? 'Attention' : wave.state;
    const date = wave.startedAt || wave.createdAt;
    $('#wave-date').textContent = date ? new Date(date).toLocaleDateString(undefined, { month: 'long', day: 'numeric', year: 'numeric' }) : '';
    if (date) $('#wave-date').dateTime = date;
    const automatic = isAutoFix(wave.objective);
    $('#wave-objective').textContent = automatic ? autoFixLabel(wave) : humanIntent(asked(wave.objective), 'Bring your idea a little further.');
    const defaults = {
        Active: automatic ? 'The last version was held back before going online, so your team is fixing what was found. When this wave finishes, Squad tries putting the app online again.'
            : 'Your team is working on this request. You can return to this wave later.',
        Completed: checked === 'Failed' ? 'This wave finished, but validation failed. The result has not been verified. Review it before continuing.'
            : source.imported ? 'This wave is complete. Send the next wave, or send this work to GitHub to review it there.'
            : 'This wave is complete. Review the result in the app before starting the next change.',
        Failed: 'This wave didn’t finish. Your earlier work is retained. Try a smaller request, or retry in a new wave.',
        Cancelled: 'This wave was stopped. Work already saved is retained.'
    };
    announce('#wave-result', defaults[wave.state] ?? 'Wrapping up this round.');
    const highlights = squadHighlights(detail.response);
    $('#wave-story').hidden = !highlights.length || wave.state === 'Active' || checked === 'Failed' || wave.runtimeState === 'Failed';
    $('#wave-highlights').replaceChildren(...highlights.map(text => { const li = document.createElement('li'); li.textContent = text; return li; }));
    const ideas = squadNextIdeas(detail.response);
    const suggest = ideas.length && wave.state === 'Completed' && checked !== 'Failed' && wave.number === waves.at(-1)?.number && !busy;
    $('#wave-next').hidden = !suggest;
    $('#wave-next-ideas').replaceChildren(...(suggest ? ideas : []).map(text => {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'secondary';
        button.textContent = text;
        button.addEventListener('click', () => {
            const request = $('#work-request');
            request.value = text;
            request.focus();
            request.scrollIntoView({ block: 'center', behavior: 'smooth' });
        });
        return button;
    }));
    const proof = [];
    const made = (wave.changedFiles ?? []).filter(file => file.category === 'application');
    if (made.length) proof.push(`${made.length} application ${made.length === 1 ? 'file' : 'files'} changed.`);
    else if (wave.state === 'Completed') proof.push('No application files changed in this wave.');
    if (checked === 'Passed') proof.push('Validation passed.');
    if (checked === 'Failed') proof.push('Validation failed. Review the result before continuing.');
    // Report running-app checks only once the team's turn is over; nothing runs while it works.
    if (wave.state === 'Active') { /* the wave result already says the team is working */ }
    else if (wave.runtimeState === 'Passed') proof.push('Running-app browser and readiness checks passed.');
    else if (wave.runtimeState === 'NotChecked' && source.imported && !source.deployable) proof.push('This repository has no Squad checks, so nothing was run.');
    else if (wave.runtimeState === 'NotChecked') proof.push(online.available
        ? 'The app’s user journeys and health checks run when it goes online.' : 'No running-app browser check was recorded for this wave.');
    else if (wave.runtimeState === 'Blocked') proof.push('Running-app checks did not run because an earlier check failed.');
    else if (wave.runtimeState) proof.push(['Failed', 'Cancelled'].includes(wave.runtimeState)
        ? 'Running-app checks did not pass.' : 'Running-app checks are in progress.');
    else if (serverBacked) proof.push('No running-app browser check was recorded for this wave.');
    if (!proof.length) proof.push(wave.state === 'Active' ? 'Results will appear when this wave finishes.' : 'No validation result was recorded.');
    $('#wave-proof').replaceChildren(...proof.map(text => { const li = document.createElement('li'); li.textContent = text; return li; }));
    // Platform check findings are fixed sentences (never command output or source), so they can be shown.
    // They are written for the team, so the person sees a plain summary and the request carries the details.
    const findings = [...new Set(evidence.filter(item => item.kind === 'preflight' && item.state === 'Failed')
        .map(item => String(item.summary ?? '').trim().slice(0, 300)).filter(Boolean))].slice(0, 6);
    lastNotice = { wave, checked, findings };
    renderFix();
    const contributors = [...new Set(wave.specialists ?? [])].map(name => plain(name)).filter(Boolean);
    $('#wave-team').hidden = !contributors.length;
    $('#wave-contributors').replaceChildren(...contributors.map(name => {
        const item = document.createElement('li');
        item.textContent = name;
        return item;
    }));
    $('#wave-try').hidden = !previewUrl;
    if (previewUrl) $('#wave-try').href = previewUrl;
}
let lastNotice = null;
// Shown when the latest wave failed its own checks, or when the version it made was held back before going online.
function renderFix() {
    if (!lastNotice) return;
    const { wave, checked, findings } = lastNotice;
    const latestWave = wave.number === waves.at(-1)?.number;
    const failedChecks = checked === 'Failed' && wave.state === 'Completed' && latestWave;
    const revision = latestRevision();
    const heldBack = !failedChecks && latestWave && wave.state === 'Completed' && revision?.phase === 'Failed' &&
        Boolean(autoFixText[revision.reason]) && Date.parse(revision.startedAt) >= (Date.parse(wave.completedAt ?? '') || 0);
    // Hidden while Squad is putting the app online or is about to start its own fix.
    $('#wave-fix').hidden = mending || Boolean(autoState().fix) || !(failedChecks || heldBack);
    let found;
    if (heldBack) {
        const previous = online.online ? 'The previous version is still online.' : 'It isn’t online yet.';
        const notReady = revision.reason === 'not-ready';
        found = autoFixAttempt(wave.number) >= autoFixLimit
            ? [`Two automatic fixes didn’t get it through. ${previous}`]
            : [`${notReady ? 'This version was deployed but never became ready' : 'This version didn’t pass its checks before going online'}, so it was held back. ${previous}`];
        found.push('Asking the team to fix this passes along what was found.');
        fixRequest = notReady
            ? 'The last version was deployed but never became ready, so it was held back. Fix what kept it from becoming ready so it can go online.'
            : 'The last version didn’t pass its checks before going online. Fix what the checks found so it can go online.';
    } else {
        found = findings.length
            ? [`Squad’s checks found ${findings.length === 1 ? 'a problem' : `${findings.length} problems`} in what the team built, so the app can’t go online yet.`,
                'Asking the team to fix this passes along exactly what was found.']
            : ['The team’s own checks for the app did not pass, so it can’t go online yet.'];
        fixRequest = findings.length
            ? `Fix what the last checks found, so the app can go online:\n${findings.map(text => `- ${text}`).join('\n')}`
            : 'The app’s checks did not pass in the last wave. Find and fix the cause so the checks pass and the app can go online.';
    }
    const key = JSON.stringify(found);
    if ($('#wave-findings').dataset.key !== key) {
        $('#wave-findings').dataset.key = key;
        $('#wave-findings').replaceChildren(...found.map(text => { const li = document.createElement('li'); li.textContent = text; return li; }));
    }
}
async function selectWave(number) {
    selectedWave = number;
    const version = ++selectionVersion;
    renderTimeline();
    activityView(number);
    try {
        // Wave details already include live evidence while active and retained evidence after completion.
        // Do not replace them with the current turn's evidence when revisiting an earlier wave.
        const detail = await json(`/waves/${number}`);
        if (version === selectionVersion) {
            renderNotice(detail);
            renderActivity(detail.evidence, detail.summary);
        }
    } catch (error) {
        if (version === selectionVersion) renderActivity([], null, true);
        throw error;
    }
}
async function refreshPreview() {
    const [preview, status] = await Promise.all([json('/workspace/preview'), json('/workspace/online')]);
    online = status;
    serverBacked = Boolean(preview.serverBacked);
    runtimeStatus = preview.runtime ?? { state: 'NotChecked', message: '' };
    localPreviewAvailable = preview.deployable ?? preview.available;
    const base = preview.url || (preview.port ? `${location.protocol}//${location.hostname}:${preview.port}${appPrefix}` : null);
    const runtimeUrl = runtimeStatus.state === 'Ready' && typeof runtimeStatus.url === 'string' &&
        /^http:\/\/127\.0\.0\.1:\d+\/$/.test(runtimeStatus.url) ? runtimeStatus.url : null;
    // A deployed app passed its namespace health checks, so its online URL is shown for every app shape.
    const onlineUrl = online.online && online.url ? ownOrigin(online.url) || new URL(online.url, location.href).href : null;
    const frameTarget = onlineUrl || (!serverBacked && preview.available ? base : null);
    const target = runtimeUrl || frameTarget;
    previewUrl = target && /^https?:\/\//i.test(new URL(target, location.href).href) ? `${target.replace(/\/$/, '')}/` : null;
    $('#preview-empty').hidden = Boolean(frameTarget);
    $('#app-preview').hidden = !frameTarget;
    // An app on its own origin can keep that origin inside the frame; anything else stays in an opaque sandbox.
    $('#app-preview').setAttribute('sandbox', `allow-scripts allow-forms allow-popups allow-modals${frameTarget && isOwnOrigin(frameTarget) ? ' allow-same-origin' : ''}`);
    $('#open-app').setAttribute('aria-disabled', String(!previewUrl));
    $('#open-app').hidden = !previewUrl;
    $('#open-app').href = previewUrl || '#';
    renderBuilding();
    $('#preview-caption').textContent = runtimeStatus.state === 'Ready' ? 'Running app verified; Open app uses its real runtime.'
        : onlineUrl ? 'Running in its own namespace; it passed its deployment health checks.'
        : serverBacked ? plain(runtimeStatus.message, 'Running app not verified.')
        : previewUrl ? 'Static preview only; running-app checks have not passed.' : 'Waiting for the first build.';
    if (frameTarget && $('#app-preview').getAttribute('src') !== frameTarget) {
        $('#app-preview').setAttribute('sandbox', 'allow-scripts allow-forms allow-popups allow-modals');
        $('#app-preview').src = frameTarget;
    } else if (!frameTarget) {
        $('#app-preview').removeAttribute('src');
    }
    $('#wave-try').hidden = !previewUrl || !selectedWave;
    if (previewUrl) $('#wave-try').href = previewUrl;
    renderOnline();
}
// Topology islands (React Flow bundle in /topology, built from ClientApp/). Loaded on demand.
let topologyModule = null;
let topologyIsland = null;
let shareIsland = null;
let topologyKey = '';
let topologyView = (() => { try { return localStorage.getItem('squad:topology-view') === 'cluster' ? 'cluster' : 'aspire'; } catch { return 'aspire'; } })();
function loadTopologyModule() {
    // The import map resolves this to the fingerprinted bundle; its stylesheet is linked in App.razor.
    if (!topologyModule) {
        topologyModule = import('/topology/topology.js').catch(error => { topologyModule = null; throw error; });
    }
    return topologyModule;
}
const shortRevision = id => typeof id === 'string' ? id.slice(0, 8) : '';
function formatTime(value) {
    return value ? new Date(value).toLocaleString(undefined, { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit', second: '2-digit' }) : '';
}
function topologyStateLabel(topology) {
    if (topology.state === 'deployed') return online.online ? `Running · ${shortRevision(topology.revision)}` : `Deployed · ${shortRevision(topology.revision)}`;
    return topology.state === 'built' ? 'Built' : 'Planned';
}
function renderTopologyToolbar(topology) {
    for (const button of document.querySelectorAll('[data-topology-view]'))
        button.setAttribute('aria-pressed', String(button.dataset.topologyView === topologyView));
    const planned = topology?.state === 'planned';
    $('#topology-note').textContent = topologyView === 'aspire'
        ? (planned ? 'How your team will build this app in Aspire. It updates after the first build.' : 'How this app is built in Aspire, from its published resources.')
        : (planned ? `How it will run in its own namespace, ${topology?.namespace ?? ''}.` : `How it runs in its own namespace, ${topology.namespace}.`);
}
const pipelineLabels = { done: 'Done', active: 'In progress', waiting: 'Waiting', failed: 'Failed', pending: 'Not started' };
function pipelineSteps(revision, topology, live) {
    const phase = revision?.phase;
    const built = Boolean(revision?.artifactDigest);
    const failedAt = phase === 'Failed' ? (!built ? 'build' : revision.reason === 'journeys-failed' ? 'check' : 'deploy') : null;
    const step = (id, label, detail, state) => ({ id, label, detail, state });
    const buildState = !revision ? 'pending' : failedAt === 'build' ? 'failed' : phase === 'Building' ? 'active' : 'done';
    const checkState = !revision || buildState !== 'done' ? 'pending' : failedAt === 'check' ? 'failed'
        : phase === 'Built' ? 'waiting' : phase === 'Checking' ? 'active' : 'done';
    const deployState = checkState !== 'done' ? 'pending' : failedAt === 'deploy' ? 'failed'
        : phase === 'Deploying' ? 'active' : 'done';
    const failing = Object.entries(live ?? {}).filter(([id, status]) => id.startsWith('workload:') && status.state === 'failing');
    const healthState = deployState !== 'done' ? 'pending' : failing.length ? 'failed' : online.online ? 'done' : 'active';
    const healthDetail = failing.length
        ? `Not working yet: ${failing.map(([id, status]) => `${id.slice('workload:'.length)} (${status.detail})`).join('; ')}.`
        : 'The front end must load and every declared health check (including the database) must pass.';
    return [
        step('build', 'Build the app', 'Front end, API image and Aspire chart, built in an isolated worker without network access.', buildState),
        step('check', 'Test it in a throwaway copy', 'The app’s own user journeys run in a real browser against a fresh copy with an empty database, in a temporary namespace that is removed afterwards.', checkState),
        step('deploy', 'Deploy to its namespace', `Helm release into ${topology?.namespace ?? 'the app’s namespace'}, with its PostgreSQL data volume.`, deployState),
        step('health', 'Check that it works', healthDetail, healthState)
    ];
}
// AppDeploymentPhase is serialized by value; keep this list in the enum's order.
const deploymentPhases = ['Building', 'Built', 'Deploying', 'Deployed', 'Failed', 'Checking'];
// Fixed reason codes recorded by the platform for a failed build or deploy (never log text).
function deploymentReasons(reason) {
    const fixed = {
        'packages-unavailable': 'The app asks for packages the offline build doesn’t have. Ask your team in a wave to keep the provided package versions, then put it online again.',
        'build-failed': 'The app’s code didn’t build. Ask your team in a wave to fix the build, then put it online again.',
        'not-ready': 'The new revision didn’t become ready, so the previous one keeps running. Ask your team in a wave to check the app’s health checks.',
        'cluster-full': 'Your cluster doesn’t have room to run this right now, so nothing was changed. Take apps you aren’t using offline or delete them, then put it online again.',
        'journeys-failed': 'The new revision failed its own user journeys in a throwaway test copy, so it was not put online. Ask your team in a wave to fix what the test found, then put it online again.'
    };
    if (fixed[reason]) return fixed[reason];
    const missing = /^missing:((?:AppHost|Api|frontend)\/[\w.-]+)$/.exec(reason ?? '');
    return missing ? `The app is missing ${missing[1]}. Ask your team in a wave to add it, then put it online again.` : null;
}
const phaseName = phase => typeof phase === 'number' ? deploymentPhases[phase] : phase;
function renderPipeline(deployments, topology, live) {
    const revisions = (deployments?.revisions ?? []).map(revision => ({ ...revision, phase: phaseName(revision.phase) }));
    const latest = revisions.at(-1);
    const steps = pipelineSteps(latest, topology, live);
    const active = steps.find(item => item.state === 'active' || item.state === 'failed' || item.state === 'waiting');
    $('#pipeline-summary').textContent = !latest ? 'No deployments yet'
        : active ? `${active.label}: ${pipelineLabels[active.state].toLowerCase()}`
        : `Revision ${shortRevision(latest.runId)} is running`;
    $('#pipeline-steps').replaceChildren(...steps.map(item => {
        const entry = document.createElement('li');
        entry.className = 'pipeline-step';
        entry.dataset.state = item.state;
        const marker = document.createElement('span');
        marker.className = 'pipeline-marker';
        marker.setAttribute('aria-hidden', 'true');
        const body = document.createElement('div');
        const heading = document.createElement('strong');
        heading.textContent = item.label;
        const state = document.createElement('span');
        state.className = 'pipeline-state';
        state.textContent = pipelineLabels[item.state];
        const detail = document.createElement('p');
        detail.textContent = item.detail;
        body.append(heading, state, detail);
        if (latest && item.id === 'build' && item.state !== 'pending') {
            const time = document.createElement('time');
            time.dateTime = latest.startedAt;
            time.textContent = `Started ${formatTime(latest.startedAt)}`;
            body.append(time);
        }
        if (latest && item.state === 'failed') {
            const help = document.createElement('p');
            help.className = 'pipeline-help';
            help.textContent = deploymentReasons(latest.reason) ?? (item.id === 'build'
                ? 'The build did not produce a deployable package. Ask your team in a wave to check that the app builds, then put it online again.'
                : item.id === 'health'
                    ? 'Ask your team to fix it in a new wave; the fix is deployed into the same namespace when the wave completes.'
                    : 'The release did not become ready, so the previous revision keeps running. Try putting it online again.');
            body.append(help);
        }
        entry.append(marker, body);
        return entry;
    }));
    const earlier = revisions.slice(0, -1).reverse().slice(0, 5);
    $('#pipeline-history-heading').hidden = !earlier.length;
    $('#pipeline-history').hidden = !earlier.length;
    $('#pipeline-history').replaceChildren(...earlier.map(revision => {
        const entry = document.createElement('li');
        const phase = { Deployed: 'Deployed', Failed: 'Failed', Building: 'Interrupted', Built: 'Interrupted', Checking: 'Interrupted', Deploying: 'Interrupted' }[revision.phase] ?? revision.phase;
        entry.textContent = `Revision ${shortRevision(revision.runId)} · ${phase} · ${formatTime(revision.updatedAt)}`;
        return entry;
    }));
}
async function refreshTopology() {
    if (!appId) return;
    let topology, deployments;
    try {
        [topology, deployments] = await Promise.all([json('/workspace/topology'), json('/workspace/deployments').catch(() => null)]);
    } catch {
        $('#topology-state').textContent = 'Unavailable';
        return;
    }
    let live = null;
    if (topology.state === 'deployed') {
        try { live = await json('/workspace/topology/live'); } catch { live = { available: false }; }
    }
    $('#topology-state').textContent = topologyStateLabel(topology);
    $('#topology-state').dataset.state = topology.state;
    renderTopologyToolbar(topology);
    $('#topology-live').hidden = !live;
    if (live) $('#topology-live').textContent = live.available
        ? `Live · updated ${new Date(live.observedAt).toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit', second: '2-digit' })}`
        : 'Live status unavailable';
    const liveNodes = live?.available ? live.nodes : null;
    if (deployments) deploymentState = deployments;
    renderFix();
    renderPipeline(deployments, topology, liveNodes);
    topology = { ...topology, live: liveNodes };
    const key = JSON.stringify([topology, topologyView]);
    if (key === topologyKey) return;
    topologyKey = key;
    try {
        const { mountTopology } = await loadTopologyModule();
        const target = $('#topology-diagram');
        if (!topologyIsland) { target.replaceChildren(); topologyIsland = mountTopology(target, topology, { view: topologyView }); }
        else topologyIsland.update(topology, { view: topologyView });
        const share = $('#share-topology');
        share.hidden = topology.state !== 'deployed';
        share.setAttribute('aria-label', `Deployed topology: running in ${topology.namespace}, revision ${shortRevision(topology.revision)}`);
        if (topology.state === 'deployed') {
            if (!shareIsland) shareIsland = mountTopology(share, topology, { view: 'cluster', compact: true });
            else shareIsland.update(topology);
        }
    } catch {
        topologyKey = '';
        $('#topology-diagram').replaceChildren(Object.assign(document.createElement('p'), {
            className: 'topology-loading', textContent: 'The topology diagram couldn’t be loaded. Refresh to try again.'
        }));
    }
}
for (const button of document.querySelectorAll('[data-topology-view]')) {
    button.addEventListener('click', () => {
        topologyView = button.dataset.topologyView;
        try { localStorage.setItem('squad:topology-view', topologyView); } catch { }
        refreshTopology();
    });
}
async function mountLibraryTopologies() {
    const targets = [...document.querySelectorAll('#app-library .plot-topology[data-topology-app]')];
    if (!targets.length) return;
    try {
        const { mountTopology } = await loadTopologyModule();
        for (const target of targets) {
            const app = libraryApps.find(item => item.id === target.dataset.topologyApp);
            if (app?.topology && !target.dataset.mounted) {
                target.dataset.mounted = 'true';
                mountTopology(target, app.topology, { view: 'aspire', compact: true });
            }
        }
    } catch { }
}
async function refresh() {
    if (!appId) return refreshLibrary();
    const version = ++refreshVersion;
    const [status, history] = await Promise.all([json('/work/status'), json('/waves')]);
    if (version !== refreshVersion) return;
    busy = status.isBusy;
    if (status.state === 'Ready') ready = true;
    const prior = waves.at(-1);
    waves = history;
    await loadTeam();
    if (!selectedWave || (!prior || waves.at(-1)?.number !== prior.number)) selectedWave = waves.at(-1)?.number ?? null;
    renderTimeline();
    if (selectedWave) await selectWave(selectedWave);
    else {
        activityView('setup');
        const selection = selectionVersion;
        try {
            const evidence = await json('/work/evidence?after=0');
            if (version === refreshVersion && selection === selectionVersion && !selectedWave)
                renderActivity(evidence.items);
        } catch {
            if (version === refreshVersion && selection === selectionVersion && !selectedWave)
                renderActivity([], null, true);
        }
    }
    if (version !== refreshVersion) return;
    await refreshPreview();
    await refreshTopology();
    renderState();
    keepGoing().catch(() => showError());
}
async function sendWork() {
    if (!ready || busy || startingApp) return;
    const input = $('#work-request');
    const text = input.value.trim();
    if (!text) return fieldError(input, $('#work-request-error'), 'Describe one thing to make or improve.');
    clearField(input, $('#work-request-error'));
    await runWork(text);
}
async function runWork(text, automatic = false) {
    if (!ready || busy) {
        if (startingApp) throw new Error('The first build is not ready to start.');
        return;
    }
    clearError();
    // A request the person typed starts a fresh count of automatic fixes.
    const auto = automatic ? autoState() : { fixes: 0 };
    auto.follow = waves.at(-1)?.number ?? 0;
    delete auto.online;
    delete auto.fix;
    saveAuto(auto);
    busy = true;
    selectedWave = null;
    activityView('pending');
    renderState();
    try { await streamAgentTurn(text + recapRequest, 'work'); if (!automatic) $('#work-request').value = ''; }
    catch { showError('This wave didn’t finish as expected. Check its notice below, then try another wave.'); }
    finally {
        try {
            // refresh() then puts a completed wave online (updating a deployed app in place, in the same namespace).
            await refresh();
            if (previewUrl) $('#app-preview').src = previewUrl;
            // No wave was recorded for this request: there is nothing to follow.
            const current = autoState();
            if (current.follow != null && !(waves.at(-1)?.number > current.follow) && !busy) { delete current.follow; saveAuto(current); }
            if (startingApp && !waves.length) showError('We couldn’t start your app. Your idea is saved. Choose Build my app to try again.');
        }
        catch { busy = false; renderState(); showError(); }
    }
}
async function cancelWork() {
    $('#cancel-work').disabled = true;
    try {
        const result = await json('/work/cancel', { method: 'POST' });
        $('#team-status').textContent = result.requested ? 'Stopping this wave' : 'This wave has already finished';
        await refresh();
    } catch { showError('We couldn’t stop this wave. Please try again.'); }
    finally { $('#cancel-work').disabled = false; }
}
async function openPublish() {
    $('#publish-owner').disabled = true;
    $('#publish-submit').disabled = true;
    $('#publish-dialog').classList.remove('published');
    $('#publish-dialog').showModal();
    $('#publish-name').value ||= (idea.toLowerCase().match(/[a-z0-9]+/g) ?? ['my', 'app']).slice(0, 4).join('-');
    $('#publish-result').textContent = '';
    $('#publish-result').hidden = false;
    $('#publish-name').removeAttribute('aria-invalid');
    resetPublication();
    $('#publish-name').focus();
    try {
        const owners = await json('/publish/owners');
        $('#publish-owner').replaceChildren();
        $('#publish-owner').disabled = !owners.available;
        $('#publish-submit').disabled = !owners.available;
        if (!owners.available) { $('#publish-result').textContent = 'Publishing isn’t connected yet. Ask the administrator to connect a GitHub account, then try again.'; return; }
        for (const owner of [owners.login, ...(owners.organizations ?? [])]) {
            const option = document.createElement('option'); option.value = owner; option.textContent = owner; $('#publish-owner').append(option);
        }
        $('#publish-auth').textContent = `Connected as ${owners.login}. This installation uses a shared account, not your browser’s GitHub sign-in.`;
    } catch { $('#publish-submit').disabled = true; $('#publish-result').textContent = 'We couldn’t load your accounts. Close this window and try again.'; }
}
function resetPublication() { reviewedPublication = null; $('#publish-review').hidden = true; }
function closePublish() { if (!publishing) { $('#publish-dialog').close(); $('#toggle-publish').focus(); } }
function reviewPublication(event) {
    event.preventDefault();
    if (publishing) return;
    const name = $('#publish-name').value.trim();
    if (!/^[A-Za-z0-9._-]{1,100}$/.test(name) || name === '.' || name === '..') return fieldError($('#publish-name'), $('#publish-result'), 'Use 1–100 letters, numbers, dots, dashes, or underscores for your app’s name.');
    if ($('#publish-owner').disabled || !$('#publish-owner').value) return;
    clearField($('#publish-name'), $('#publish-result'));
    reviewedPublication = { owner: $('#publish-owner').value, name, private: true, description: plain(idea, 'Built with Squad').slice(0, 300) };
    $('#publish-destination').textContent = `Private repository: ${reviewedPublication.owner} / ${name}. Only invited people can see it.`;
    $('#publish-review').hidden = false;
    $('#publish-confirm').focus();
}
async function publish() {
    if (publishing || !reviewedPublication) return;
    const request = reviewedPublication;
    publishing = true;
    for (const control of $('#publish-form').elements) control.disabled = true;
    $('#publish-result').hidden = false;
    $('#publish-result').textContent = 'Publishing repository…';
    try {
        const response = await fetch(appUrl('/publish'), { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(request) });
        const result = await response.json();
        $('#publish-result').replaceChildren();
        if (response.ok && result.success && result.private === true) {
            $('#publish-result').append('Published to a private repository. ');
            if (result.url && /^https:\/\/github\.com\//i.test(result.url)) {
                const link = document.createElement('a'); link.href = result.url; link.target = '_blank'; link.rel = 'noopener'; link.textContent = 'View repository ↗'; $('#publish-result').append(link);
            }
            $('#publish-dialog').classList.add('published');
        } else $('#publish-result').textContent = 'Publication isn’t complete. Check the connected account before retrying with the same name; the repository may already exist.';
    } catch { $('#publish-result').textContent = 'We couldn’t confirm publication. Check the connected account before trying again with the same name.'; }
    finally {
        publishing = false;
        for (const control of $('#publish-form').elements) control.disabled = false;
        resetPublication();
        $('#publish-result').focus();
    }
}
// Starting points for a new app. Every app is a front end, an API and a PostgreSQL database
// (see AppBuildPolicy); "Surprise me" lets the team invent one within that shape.
const promptIdeas = {
    tools: 'A neighborhood tool-lending library. Neighbors list tools they can lend, browse what is available, request to borrow something with pickup and return dates, and the owner approves or declines. Show who has what right now.',
    recipes: 'A shared family recipe box. People add recipes with ingredients and steps, tag them, search by ingredient, and build a weekly shopping list from the recipes they pick, with duplicate ingredients combined.',
    standup: 'A small-team async standup board. Each person posts yesterday, today and blockers once a day, sees the team’s posts grouped by day, and can mark a blocker resolved. Show who has not posted yet today.',
    surprise: 'Surprise me: invent a small, genuinely useful app that a few people could use together. It needs a real front end, an API and a database that stores what people create, so the data is still there after a restart. Choose something with a clear purpose and two or three core workflows, give it a short name, and explain in one sentence who it is for.'
};
for (const button of document.querySelectorAll('[data-idea]')) {
    button.addEventListener('click', () => {
        const input = $('#casting-prompt');
        input.value = promptIdeas[button.dataset.idea];
        clearField(input, $('#casting-prompt-error'));
        if (button.dataset.idea === 'surprise') propose();
        else input.focus();
    });
}
$('#send-casting-turn').addEventListener('click', propose);
$('#create-app').addEventListener('click', createApp);
$('#import-app').addEventListener('click', openImport);
$('#import-owner').addEventListener('change', loadImportRepos);
$('#import-filter').addEventListener('input', () => renderImportRepos());
$('#import-form').addEventListener('submit', submitImport);
$('#import-cancel').addEventListener('click', () => { if (!importing) $('#import-dialog').close(); });
$('#send-branch').addEventListener('click', sendBranch);
function renderNavigation() {
    const target = ['#team-section', '#next-wave'].includes(location.hash) ? location.hash : '#app-overview';
    for (const link of document.querySelectorAll('.project-nav a')) {
        if (new URL(link.href).hash === target) link.setAttribute('aria-current', 'location');
        else link.removeAttribute('aria-current');
    }
}
// The document's root <base> would otherwise discard the selected app's query.
for (const link of document.querySelectorAll('.project-nav a, .skip-link')) {
    link.href = `${location.pathname}${location.search}${link.getAttribute('href')}`;
    link.setAttribute('data-enhance-nav', 'false');
}
window.addEventListener('hashchange', renderNavigation);
renderNavigation();
$('#build-app').addEventListener('click', buildApp);
$('#send-work').addEventListener('click', sendWork);
$('#cancel-work').addEventListener('click', cancelWork);
$('#open-app').addEventListener('click', event => { if (!previewUrl) event.preventDefault(); });
$('#toggle-publish').addEventListener('click', openPublish);
// Putting it online by hand gets the same follow-through as after a wave.
$('#put-online').addEventListener('click', () => {
    if ($('#put-online').disabled || mending) return;
    const auto = autoState();
    delete auto.follow;
    auto.online = { since: revisionMark(latestRevision()), started: false };
    saveAuto(auto);
    keepGoing().catch(() => showError());
});
$('#ask-before-fix').addEventListener('change', event => {
    try { localStorage.setItem(askFirstKey, event.target.checked ? '1' : '0'); } catch { }
    renderFix();
});
$('#ask-fix').addEventListener('click', () => {
    const input = $('#work-request');
    if (input.disabled || !fixRequest) return;
    input.value = fixRequest;
    input.scrollIntoView({ block: 'center', behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
    input.focus();
});
$('#publish-form').addEventListener('submit', reviewPublication);
$('#publish-confirm').addEventListener('click', publish);
$('#publish-back').addEventListener('click', () => { resetPublication(); $('#publish-name').focus(); });
$('#publish-cancel').addEventListener('click', closePublish);
$('#publish-dialog').addEventListener('cancel', event => { event.preventDefault(); closePublish(); });
for (const id of ['publish-name', 'publish-owner']) $(`#${id}`).addEventListener('input', resetPublication);
let deleting = false;
function listRepositories(list, repositories) {
    list.replaceChildren(...repositories.map(repository => {
        const item = document.createElement('li');
        const link = document.createElement('a');
        link.href = repository.url;
        link.target = '_blank';
        link.rel = 'noopener';
        link.textContent = repository.fullName;
        item.append(link);
        return item;
    }));
}
async function openDelete() {
    $('#delete-dialog-heading').textContent = appTitle ? `Delete ${appTitle}?` : 'Delete this app?';
    $('#delete-result').hidden = true;
    $('#delete-repositories').hidden = true;
    $('#delete-confirm').disabled = false;
    $('#delete-dialog').showModal();
    try {
        const repositories = await json('/publish/repositories');
        listRepositories($('#delete-repository-list'), repositories);
        $('#delete-repositories').hidden = !repositories.length;
    } catch { /* The warning is shown again after deletion if any repositories remain. */ }
}
function closeDelete() { if (!deleting) { $('#delete-dialog').close(); $('#delete-app').focus(); } }
async function deleteApp(event) {
    event.preventDefault();
    if (deleting) return;
    deleting = true;
    $('#delete-confirm').disabled = true;
    $('#delete-confirm').textContent = 'Deleting…';
    $('#delete-result').hidden = true;
    try {
        const response = await fetch(appUrl('/app'), { method: 'DELETE', cache: 'no-store' });
        const result = await response.json().catch(() => null);
        if (!response.ok || !result?.deleted) {
            $('#delete-result').textContent = plain(result?.message, 'The app couldn’t be deleted. Please try again.');
            $('#delete-result').hidden = false;
            return;
        }
        try {
            sessionStorage.removeItem(storageKey);
            sessionStorage.setItem('soaks:deleted', JSON.stringify({ title: appTitle, repositories: result.repositories || [] }));
        } catch { /* The library still loads without the notice. */ }
        location.assign('/');
    } catch {
        $('#delete-result').textContent = 'The app couldn’t be deleted. Please try again.';
        $('#delete-result').hidden = false;
    } finally {
        deleting = false;
        $('#delete-confirm').disabled = false;
        $('#delete-confirm').textContent = 'Delete app';
    }
}
function showDeletedNotice() {
    let deleted = null;
    try {
        deleted = JSON.parse(sessionStorage.getItem('soaks:deleted') || 'null');
        sessionStorage.removeItem('soaks:deleted');
    } catch { return; }
    if (!deleted) return;
    const notice = $('#library-notice');
    const summary = document.createElement('p');
    summary.textContent = `${plain(deleted.title, 'The app')} was deleted.`;
    notice.replaceChildren(summary);
    if (deleted.repositories?.length) {
        const kept = document.createElement('p');
        kept.textContent = 'Its GitHub repository wasn’t deleted. Delete it on GitHub if you no longer need it:';
        const list = document.createElement('ul');
        listRepositories(list, deleted.repositories);
        notice.append(kept, list);
    }
    notice.hidden = false;
}
$('#delete-app').addEventListener('click', openDelete);
$('#delete-form').addEventListener('submit', deleteApp);
$('#delete-cancel').addEventListener('click', closeDelete);
$('#delete-dialog').addEventListener('cancel', event => { event.preventDefault(); closeDelete(); });
$('#send-startup-answer').addEventListener('click', answerQuestion);
for (const [id, action] of [['casting-prompt', propose], ['startup-answer-text', answerQuestion], ['work-request', sendWork]]) {
    $(`#${id}`).addEventListener('keydown', event => { if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) { event.preventDefault(); action(); } });
}
async function initialize() {
    $('#library').hidden = Boolean(appId);
    $('#project-view').hidden = !appId;
    $('#delete-section').hidden = !appId || appId === 'first';
    if (!appId) {
        announce('#team-status', 'All apps');
        showDeletedNotice();
        await refreshLibrary();
        startPolling();
        return;
    }
    await json('/select', { method: 'POST' });
    $('#download-zip').href = appUrl('/workspace/export.zip');
    try { source = await json('/workspace/source'); } catch { source = { imported: false }; }
    const status = await json('/casting/status');
    restore(status.generation);
    appTitle = status.appTitle || appTitle;
    ready = status.canTakeRequests;
    hasCast = status.hasCast;
    await loadIntent();
    await refresh();
    renderState();
    startPolling();
}
async function refreshLibrary() {
    const apps = await json('/apps');
    libraryApps = await Promise.all(apps.map(async app => {
        try {
            const response = await fetch(`/apps/${encodeURIComponent(app.id)}/waves`, { cache: 'no-store' });
            if (!response.ok) throw new Error('Latest update unavailable');
            const history = await response.json();
            // A repository that can't be deployed yet has no Aspire map to draw.
            const topology = app.imported && !app.deployable ? null : await fetch(`/apps/${encodeURIComponent(app.id)}/workspace/topology`, { cache: 'no-store' })
                .then(result => result.ok ? result.json() : null).catch(() => null);
            return { ...app, first: history[0], latest: history.at(-1), topology };
        } catch { return { ...app, updateUnavailable: true }; }
    }));
    // Hide drafts nobody described (New app creates one per click), then list recent work first.
    const activity = app => Date.parse(app.latest?.completedAt || app.latest?.startedAt || '') || 0;
    libraryApps = libraryApps
        .map((app, index) => ({ app, index }))
        .filter(({ app }) => app.updateUnavailable || app.title || app.hasCast || app.latest || app.state !== 'Needs cast')
        .sort((a, b) => activity(b.app) - activity(a.app) || b.index - a.index)
        .map(({ app }) => app);
    $('#library-loading').hidden = true;
    renderLibrary();
}
function startPolling() {
    setInterval(async () => {
        // A request lost on a dropped connection must not freeze the page (for example a stale "busy" that keeps
        // Build my app disabled): after 20 s start a fresh refresh; refresh() ignores the abandoned one's answer.
        if (document.hidden || (polling && Date.now() - polling < 20_000)) return;
        const started = polling = Date.now();
        try { await refresh(); } catch { showError(); } finally { if (polling === started) polling = 0; }
    }, appId ? 2000 : 5000);
}
const systemNight = matchMedia('(prefers-color-scheme: dark)');
function isNight() { const chosen = document.documentElement.dataset.theme; return chosen ? chosen === 'dark' : systemNight.matches; }
function renderTheme() {
    const night = isNight();
    $('#theme-toggle').setAttribute('aria-label', night ? 'Switch to light theme' : 'Switch to dark theme');
    $('#theme-toggle .theme-glyph').textContent = night ? '☀' : '☾';
    $('#theme-toggle .theme-label').textContent = night ? 'Light' : 'Dark';
}
$('#theme-toggle').addEventListener('click', () => {
    const next = isNight() ? 'light' : 'dark';
    document.documentElement.dataset.theme = next;
    try { localStorage.setItem('soaks:theme', next); } catch { }
    renderTheme();
});
systemNight.addEventListener('change', renderTheme);
renderTheme();

initialize().catch(() => { renderState(); showError('Couldn’t load this app. Refresh the page to reconnect.'); });
