const $ = selector => document.querySelector(selector);
function announce(selector, text) {
    const region = $(selector);
    if (region.textContent !== text) region.textContent = text;
}
const appId = new URLSearchParams(location.search).get('app');
const appPrefix = appId ? `/apps/${encodeURIComponent(appId)}` : '';
const appUrl = path => path === '/apps' ? path : appPrefix + path;
const storageKey = appId === 'first' ? 'daydream:conversation' : `daydream:conversation:${appId}`;
let libraryApps = [];
let lastLibrary = '';
let creatingApp = false;
let online = { available: false, online: false, url: null, message: '' };
let onlineBusy = false;
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
let hasCast = false;
let teamMembers = [];
let teamLoaded = false;
let waves = [];
let selectedWave = null;
let selectionVersion = 0;
let previewUrl = null;
let reviewedPublication = null;
let publishing = false;
let polling = false;
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
function humanIntent(text, fallback) {
    // Remove implementation clauses from the person's words, not just individual jargon tokens.
    const sentences = String(text ?? '').split(/(?<=[.!?])\s+|\n+/);
    const safe = sentences.map(sentence => sentence
        .replace(/\s+(?:in|using|with)\s+(?:(?:plain|vanilla|pure)\s+)?(?:HTML|CSS|JavaScript|TypeScript|React|Python|C#|\.NET)\b.*$/i, '')
        .replace(/\s+(?:served|hosted)\s+(?:from|in|on)\b.*$/i, '')
        .replace(/^\s*wave\s*\d+\s*[:.—–-]\s*/i, '')
        .trim()).filter(sentence => plain(sentence));
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
    return [idea.split('\n\n(Also give this app')[0], ...refinements].filter(Boolean).join('\n\n');
}
// A short, human title for the idea: its first sentence, trimmed to a word boundary.
function ideaTitle(text, fallback) {
    const intent = humanIntent(text, '');
    if (!intent) return fallback;
    let title = intent.split(/(?<=[.!?])\s+/)[0].replace(/[.!?]+$/, '').replace(/[,:;—–-]\s*$/, '');
    if (title.length > 60) title = title.slice(0, 60).replace(/[\s,:;—–-]+\S*$/, '') + '…';
    return title;
}
// Each wave asks for a short recap written for the person with the idea; it is stripped back out of what they asked.
const recapMarker = '\n\n(When you finish, ';
const recapRequest = recapMarker + 'end your reply with a section headed "For the dreamer:" holding two to four short bullets about what someone using the app will notice now. Plain everyday words, no technical terms.)';
function asked(text) {
    const value = String(text ?? '');
    const cut = value.indexOf(recapMarker);
    return cut < 0 ? value : value.slice(0, cut);
}
// The squad's own plain-language bullets from its final reply, never code or technical detail.
function squadHighlights(response) {
    const text = String(response ?? '');
    const recap = /for the dreamer\W*\n/i.exec(text);
    const bullets = (recap ? text.slice(recap.index + recap[0].length) : text).split('\n')
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
    if (!appId) return renderLibrary();
    const started = Boolean(idea || hasCast || waves.length);
    document.body.classList.toggle('has-app', ready);
    const name = appTitle || ideaTitle(idea || asked(waves[0]?.objective), 'Untitled app');
    const heading = started ? name : 'New app';
    document.title = `${heading} · Squad`;
    $('#idea-heading').textContent = heading;
    $('#idea-heading').classList.toggle('is-long', heading.length > 28);
    const firstBuild = ready && !waves.length;
    $('#idea-description').textContent = ready ? 'Review the result, follow your team’s work and start the next wave.' : 'Describe what you want to build. Your team will plan the work and build from your brief.';
    $('#casting-controls').hidden = ready && !firstBuild;
    $('#startup-input').hidden = hasCast || ready;
    $('#casting-prompt').disabled = castingBusy || startingApp || busy || hasCast || ready;
    $('#send-casting-turn').hidden = hasCast || ready;
    $('#send-casting-turn').disabled = castingBusy || startingApp || busy;
    $('#send-casting-turn').textContent = castingBusy ? 'Planning…' : proposalText ? 'Update brief' : 'Plan app';
    $('#startup-brief').hidden = !idea && !hasCast && !firstBuild;
    $('#startup-summary').textContent = humanIntent(appBrief(), 'Your app brief is ready for its first build.');
    $('#startup-question').textContent = startupQuestion;
    $('#startup-question').hidden = !startupQuestion;
    $('#build-app').disabled = castingBusy || startingApp || busy || Boolean(startupQuestion);
    $('#build-app').textContent = startingApp ? 'Starting build…' : 'Build my app';
    $('#casting-hint').textContent = startupQuestion ? 'Answer above, then update the brief.' : proposalText || hasCast || firstBuild
        ? 'Your brief is saved. The first build will use it.' : 'Start with the goal and who will use it.';
    $('#startup-note').textContent = startupQuestion ? 'Resolve this question before building.' : hasCast || ready
        ? 'Continue with your saved brief.' : 'Build from this brief, or add details above.';
    $('#jump-to-wave').hidden = !ready || firstBuild;
    $('#making').hidden = !waves.length;
    $('#keep-section').hidden = !waves.length;
    $('#work-request').disabled = !ready || busy || startingApp;
    $('#send-work').disabled = !ready || busy || startingApp;
    $('#send-work').textContent = busy ? 'Work in progress…' : 'Start a wave';
    $('#cancel-work').hidden = !busy;
    $('#work-heading').textContent = busy ? 'Wave in progress' : 'Start the next wave';
    announce('#team-status', startingApp ? 'Starting build' : castingBusy ? 'Planning app' : busy ? 'Work in progress' : ready ? 'Ready' : proposalText || hasCast ? 'Ready to build' : 'New app');
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
    const labels = {
        Delegated: 'Assigned', Selected: 'Selected', Active: 'In progress', Progress: 'Update',
        Succeeded: 'Completed', Passed: 'Passed', Failed: 'Failed', Completed: 'Completed',
        Cancelled: 'Stopped', Cancelling: 'Stopping'
    };
    const nativeMessages = {
        Delegated: 'Task assigned to a team member.', Selected: 'Team member selected for this task.',
        Active: 'Team member started work.', Progress: 'Team member reported an activity update.',
        Succeeded: 'Team member finished this task.', Failed: 'Team member reported a task failure.'
    };
    // Native summaries can contain prompts or reasoning. Describe only the recorded event, never its payload.
    const entries = (items ?? []).flatMap(item => {
        let actor = 'Team';
        let message;
        if (item.kind === 'specialist') {
            actor = plain(item.actor, 'Team').slice(0, 80);
            message = nativeMessages[item.state] ?? 'Team activity recorded.';
            if (actor === 'Team') message = message.replace('Team member', 'Team').replace('a team member', 'the team');
        } else if (item.kind === 'validation') {
            actor = 'Validation';
            message = item.state === 'Passed' ? 'Validation passed.'
                : item.state === 'Failed' ? 'Validation failed. Review the result before continuing.' : 'Validation result recorded.';
        } else if (item.kind === 'state') {
            actor = 'Lifecycle';
            message = { Active: 'A team turn started.', Completed: 'This turn completed.',
                Failed: 'This turn didn’t finish. Earlier saved work is retained.',
                Cancelled: 'This turn was stopped. Earlier saved work is retained.',
                Cancelling: 'Stopping this turn.' }[item.state] ?? 'Work status updated.';
        } else return [];
        return [{ actor, message, state: item.state, timestamp: item.timestamp }];
    });
    if (wave?.commitSha) entries.push({
        actor: 'Checkpoint', message: 'Changes saved for this wave.', state: 'Succeeded',
        timestamp: wave.completedAt
    });
    const visible = entries.slice(-30);
    const key = JSON.stringify([visible, entries.length > 30, failed, Boolean(wave)]);
    if (key === lastActivity) return;
    lastActivity = key;
    $('#activity-status').textContent = failed ? 'Unavailable' : entries.length ? `${entries.length} recorded` : 'Waiting';
    $('#activity-status').dataset.failed = String(failed);
    $('#activity-error').hidden = !failed;
    $('#activity-empty').hidden = failed || visible.length > 0;
    $('#activity-empty').textContent = wave ? 'No public activity was recorded for this wave.'
        : 'Waiting for recorded activity. Events appear as your team reports them.';
    $('#activity-limit').hidden = entries.length <= 30;
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
    $('#put-online').disabled = !online.available || !localPreviewAvailable || busy || castingBusy || onlineBusy;
    $('#put-online').textContent = onlineBusy ? 'Publishing app…' : online.online ? 'Update online app' : 'Put it online';
    announce('#online-note', onlineBusy ? 'Building and publishing this revision…' : plain(online.message));
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
        link.href = `?app=${encodeURIComponent(app.id)}#main`;
        link.setAttribute('data-enhance-nav', 'false');
        link.dataset.app = app.id;
        const art = document.createElement('span');
        art.className = 'plot-art';
        art.setAttribute('aria-hidden', 'true');
        art.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true"><rect x="4" y="3" width="16" height="18" rx="2" /><path d="M8 3v18M11 8h5M11 12h5" /></svg>';
        const name = document.createElement('h3');
        name.textContent = plain(app.title, 'Untitled app');
        const state = document.createElement('span');
        state.className = 'plot-state';
        const stateLabels = { Active: 'In progress', Casting: 'Planning', Cancelling: 'Stopping', Publishing: 'Publishing', Failed: 'Failed' };
        state.textContent = stateLabels[app.state] || (app.previewAvailable ? 'Ready to try'
            : app.latest ? stateNames[app.latest.state] || 'Pending'
            : app.hasCast ? 'Ready to build' : 'Not started');
        link.append(art, name, state);
        const update = document.createElement('p');
        update.className = 'plot-update';
        update.textContent = app.updateUnavailable ? 'Latest update unavailable. Open the app to reconnect.'
            : app.latest ? humanIntent(asked(app.latest.objective), 'App work') : 'No waves yet.';
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
    if (focused) [...$('#app-library').querySelectorAll('[data-app]')].find(element => element.dataset.app === focused)?.focus({ preventScroll: true });
}
async function createApp() {
    if (creatingApp) return;
    creatingApp = true;
    clearError();
    renderLibrary();
    try {
        const app = await json('/apps', { method: 'POST' });
        location.assign(`?app=${encodeURIComponent(app.id)}#main`);
    } catch {
        creatingApp = false;
        renderLibrary();
        showError('Couldn’t create a new app. Please try again.');
    }
}
function renderBuilding() {
    const building = busy && ready;
    $('#viewing-window').classList.toggle('is-building', building);
    $('#viewing-window').setAttribute('aria-busy', String(building));
    announce('#preview-status', building ? 'Build in progress…' : previewUrl ? 'Available' : 'No preview yet');
    $('#preview-empty-title').textContent = building ? 'Build in progress' : 'Your app preview will appear here';
    $('#preview-empty-note').textContent = building ? 'Your team is working on this app. Results and contributors appear in the wave below.' : 'Plan your app, then start a build. The preview becomes available when there’s a result to open.';
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
    finally { castingBusy = false; renderState(); }
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
function renderTimeline() {
    const key = JSON.stringify([waves, selectedWave]);
    if (key === lastTimeline) return;
    lastTimeline = key;
    const focused = document.activeElement?.dataset.wave;
    $('#waves-empty').hidden = waves.length > 0;
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
        summary.textContent = ideaTitle(asked(wave.objective), 'A round of work on your idea');
        const state = document.createElement('span');
        state.className = 'timeline-state';
        state.textContent = stateNames[wave.state] ?? 'Pending';
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
    $('#wave-state').textContent = stateNames[wave.state] ?? 'Pending';
    $('#wave-state').dataset.state = wave.state;
    const date = wave.startedAt || wave.createdAt;
    $('#wave-date').textContent = date ? new Date(date).toLocaleDateString(undefined, { month: 'long', day: 'numeric', year: 'numeric' }) : '';
    if (date) $('#wave-date').dateTime = date;
    $('#wave-objective').textContent = humanIntent(asked(wave.objective), 'Bring your idea a little further.');
    const defaults = {
        Active: 'Your team is working on this request. You can return to this wave later.',
        Completed: checked === 'Failed' ? 'This wave finished, but validation failed. The result has not been verified. Review it before continuing.'
            : 'This wave is complete. Review the result in the app before starting the next change.',
        Failed: 'This wave didn’t finish. Your earlier work is retained. Try a smaller request, or retry in a new wave.',
        Cancelled: 'This wave was stopped. Work already saved is retained.'
    };
    announce('#wave-result', defaults[wave.state] ?? 'Wrapping up this round.');
    const highlights = squadHighlights(detail.response);
    $('#wave-story').hidden = !highlights.length || wave.state === 'Active';
    $('#wave-highlights').replaceChildren(...highlights.map(text => { const li = document.createElement('li'); li.textContent = text; return li; }));
    const proof = [];
    const made = (wave.changedFiles ?? []).filter(file => file.category === 'application');
    if (made.length) proof.push(`${made.length} application ${made.length === 1 ? 'file' : 'files'} changed.`);
    else if (wave.state === 'Completed') proof.push('No application files changed in this wave.');
    if (checked === 'Passed') proof.push('Validation passed.');
    if (checked === 'Failed') proof.push('Validation failed. Review the result before continuing.');
    if (!proof.length) proof.push(wave.state === 'Active' ? 'Results will appear when this wave finishes.' : 'No validation result was recorded.');
    $('#wave-proof').replaceChildren(...proof.map(text => { const li = document.createElement('li'); li.textContent = text; return li; }));
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
    localPreviewAvailable = preview.deployable ?? preview.available;
    const base = preview.url || (preview.port ? `${location.protocol}//${location.hostname}:${preview.port}${appPrefix}` : null);
    const target = online.online && online.url ? new URL(online.url, location.href).href : preview.available ? base : null;
    previewUrl = target && /^https?:\/\//i.test(new URL(target, location.href).href) ? `${target.replace(/\/$/, '')}/` : null;
    $('#preview-empty').hidden = Boolean(previewUrl);
    $('#app-preview').hidden = !previewUrl;
    $('#open-app').setAttribute('aria-disabled', String(!previewUrl));
    $('#open-app').href = previewUrl || '#';
    renderBuilding();
    $('#preview-caption').textContent = previewUrl ? 'Latest available app preview.' : 'Waiting for the first build.';
    if (previewUrl && $('#app-preview').getAttribute('src') !== previewUrl) {
        const separateOrigin = new URL(previewUrl, location.href).origin !== location.origin;
        $('#app-preview').setAttribute('sandbox', `allow-scripts allow-forms allow-popups allow-modals${separateOrigin ? ' allow-same-origin' : ''}`);
        $('#app-preview').src = previewUrl;
    }
    $('#wave-try').hidden = !previewUrl || !selectedWave;
    if (previewUrl) $('#wave-try').href = previewUrl;
    renderOnline();
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
    renderState();
}
async function sendWork() {
    if (!ready || busy || startingApp) return;
    const input = $('#work-request');
    const text = input.value.trim();
    if (!text) return fieldError(input, $('#work-request-error'), 'Describe one thing to make or improve.');
    clearField(input, $('#work-request-error'));
    await runWork(text);
}
async function runWork(text) {
    if (!ready || busy) {
        if (startingApp) throw new Error('The first build is not ready to start.');
        return;
    }
    clearError();
    busy = true;
    selectedWave = null;
    activityView('pending');
    renderState();
    try { await streamAgentTurn(text + recapRequest, 'work'); $('#work-request').value = ''; }
    catch { showError('This wave didn’t finish as expected. Check its notice below, then try another wave.'); }
    finally {
        try {
            await refresh();
            if (previewUrl) $('#app-preview').src = previewUrl;
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
$('#send-casting-turn').addEventListener('click', propose);
$('#create-app').addEventListener('click', createApp);
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
$('#put-online').addEventListener('click', putOnline);
$('#publish-form').addEventListener('submit', reviewPublication);
$('#publish-confirm').addEventListener('click', publish);
$('#publish-back').addEventListener('click', () => { resetPublication(); $('#publish-name').focus(); });
$('#publish-cancel').addEventListener('click', closePublish);
$('#publish-dialog').addEventListener('cancel', event => { event.preventDefault(); closePublish(); });
for (const id of ['publish-name', 'publish-owner']) $(`#${id}`).addEventListener('input', resetPublication);
for (const [id, action] of [['casting-prompt', propose], ['work-request', sendWork]]) {
    $(`#${id}`).addEventListener('keydown', event => { if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) { event.preventDefault(); action(); } });
}
async function initialize() {
    $('#library').hidden = Boolean(appId);
    $('#project-view').hidden = !appId;
    if (!appId) {
        announce('#team-status', 'All apps');
        await refreshLibrary();
        startPolling();
        return;
    }
    await json('/select', { method: 'POST' });
    $('#download-zip').href = appUrl('/workspace/export.zip');
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
            return { ...app, latest: history.at(-1) };
        } catch { return { ...app, updateUnavailable: true }; }
    }));
    $('#library-loading').hidden = true;
    renderLibrary();
}
function startPolling() {
    setInterval(async () => {
        if (polling || document.hidden) return;
        polling = true;
        try { await refresh(); } catch { showError(); } finally { polling = false; }
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
    try { localStorage.setItem('daydream:theme', next); } catch { }
    renderTheme();
});
systemNight.addEventListener('change', renderTheme);
renderTheme();

initialize().catch(() => { renderState(); showError('Couldn’t load this app. Refresh the page to reconnect.'); });
