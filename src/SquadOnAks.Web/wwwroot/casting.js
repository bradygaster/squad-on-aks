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
let waves = [];
let selectedWave = null;
let selectionVersion = 0;
let previewUrl = null;
let reviewedPublication = null;
let publishing = false;
let polling = false;
let lastTimeline = '';

// Model text is untrusted: technical material is never displayed, even as progress.
const technical = /```|`|<[^>]+>|https?:|(?:\b(?:squad|cast|casting|agents?|specialists?|AKS|Kubernetes|clusters?|pods?|containers?|instances?|git|GitHub|commits?|branches|branch|repos?|repository|repositories|diffs?|PR|pull request|deploy\w*|workspaces?|terminal|stdout|stderr|bash|npm|dotnet|tests?\/|charter|routing|implementation|fixture|scripts?|traces?|logs?|code|coding|hire|HTML|CSS|JS|JavaScript|TypeScript|dependencies|databases?|network requests?|network tools?)\b)|\b[\w.-]+\.(?:html?|css|[cm]?jsx?|tsx?|json|cs|py|sh)\b|(?:[\w.-]+\/(?:[\w.-]+\/)*[\w.-]+)|\b[a-f0-9]{12,}\b|[{}]|^\s*[#|]/i;
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
const titleRequest = '\n\n(Also give this app a short, catchy name of two to four words, the kind you would paint on a shop sign. Put it on the very first line of your reply as "App name: <name>". If an essential app requirement is unclear, put one short everyday-language question on a line starting "App question:". Ask only about the app, never team setup or technical choices. Otherwise omit that line. Internally organize a focused team with clear task ownership, bounded handoffs, and a distinct validation responsibility. Avoid redundant specialists and duplicate work.)';
function proposedTitle(text) {
    const match = /^\W*app name\W*[:—–-]\s*\W*(.+?)\W*$/im.exec(text ?? '');
    const name = match?.[1].replace(/[*_`"“”]/g, '').trim();
    return name && name.length <= 40 && plain(name) ? name : '';
}
function proposedQuestion(text) {
    const match = /^app question:\s*(.+)$/im.exec(text ?? '');
    return plain(match?.[1]);
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
function showError(message = 'We couldn’t reach Daydream. Please try again in a moment.') {
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
    const name = appTitle || ideaTitle(idea || asked(waves[0]?.objective), 'Your next big idea.');
    const heading = started ? name : 'What do you want to make?';
    $('#idea-heading').textContent = heading;
    $('#idea-heading').classList.toggle('is-long', heading.length > 28);
    const firstBuild = ready && !waves.length;
    $('#idea-description').textContent = ready ? 'Your idea, a little more real with every wave.' : "That thing you wish existed? Let's make it real. Start with your idea.";
    $('#casting-controls').hidden = ready && !firstBuild;
    $('#startup-input').hidden = hasCast || ready;
    $('#casting-prompt').disabled = castingBusy || startingApp || busy || hasCast || ready;
    $('#send-casting-turn').hidden = hasCast || ready;
    $('#send-casting-turn').disabled = castingBusy || startingApp || busy;
    $('#send-casting-turn').textContent = castingBusy ? 'Shaping your idea…' : proposalText ? 'Update my idea ↗' : 'Shape my idea ↗';
    $('#startup-brief').hidden = !idea && !hasCast && !firstBuild;
    $('#startup-summary').textContent = humanIntent(appBrief(), 'Your idea is ready for its first build.');
    $('#startup-question').textContent = startupQuestion;
    $('#startup-question').hidden = !startupQuestion;
    $('#build-app').disabled = castingBusy || startingApp || busy || Boolean(startupQuestion);
    $('#build-app').textContent = startingApp ? 'Getting your app started…' : 'Build my app ↗';
    $('#casting-hint').textContent = startupQuestion ? 'Add your answer above, then update your idea.' : proposalText || hasCast || firstBuild
        ? 'Your idea is saved. No need to describe it again.' : 'Just a little imagination.';
    $('#startup-note').textContent = startupQuestion ? 'A little more detail will help us get this right.' : hasCast || ready
        ? 'Continue with your saved idea.' : 'Build from this idea, or add a little more detail above.';
    $('#jump-to-wave').hidden = !ready || firstBuild;
    $('#making').hidden = !waves.length;
    $('#keep-section').hidden = !waves.length;
    $('#work-request').disabled = !ready || busy || startingApp;
    $('#send-work').disabled = !ready || busy || startingApp;
    $('#send-work').textContent = busy ? 'Making your changes…' : 'Start a wave ↗';
    $('#cancel-work').hidden = !busy;
    $('#work-heading').textContent = busy ? 'Good things are taking shape.' : 'What shall we make happen?';
    announce('#team-status', startingApp ? 'Getting started' : castingBusy ? 'Shaping your idea' : busy ? 'Work in progress' : ready ? 'Ready' : proposalText || hasCast ? 'Ready to build' : 'A little possibility');
    renderOnline();
    renderBuilding();
}
function renderOnline() {
    $('#put-online').disabled = !online.available || !localPreviewAvailable || busy || castingBusy || onlineBusy;
    $('#put-online').textContent = onlineBusy ? 'Putting your app online…' : online.online ? 'Update your online app ↗' : 'Put it online ↗';
    announce('#online-note', onlineBusy ? 'Finding your app a place to stay…' : plain(online.message));
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
        art.textContent = '✳';
        const name = document.createElement('h3');
        name.textContent = plain(app.title, 'Your new idea');
        const state = document.createElement('span');
        const stateLabels = { Active: 'Work in progress', Casting: 'Shaping your idea', Cancelling: 'Stopping this wave', Publishing: 'Giving your app a home', Failed: 'Needs another try' };
        state.textContent = stateLabels[app.state] || (app.previewAvailable ? 'Ready to try'
            : app.latest ? stateNames[app.latest.state] || 'Taking shape'
            : app.hasCast ? 'Ready for its first build' : 'An empty plot, full of possibility');
        link.append(art, name, state);
        const update = document.createElement('p');
        update.className = 'plot-update';
        update.textContent = app.updateUnavailable ? 'Latest update unavailable. Open the app to reconnect.'
            : app.latest ? humanIntent(asked(app.latest.objective), 'A round of work on your app') : 'Your story starts here.';
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
    heading.textContent = creatingApp ? 'Making room for your idea…' : 'Dream up something new';
    const note = document.createElement('span');
    note.textContent = 'A fresh idea, a place of its own. Your other apps stay right here.';
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
        showError('We couldn’t make room for a new idea yet. Please try again.');
    }
}
function renderBuilding() {
    const building = busy && ready;
    $('#viewing-window').classList.toggle('is-building', building);
    $('#viewing-window').setAttribute('aria-busy', String(building));
    $('#preview-status').textContent = building ? 'Your app is taking shape…' : previewUrl ? 'Your idea, coming to life' : 'Room for something wonderful';
    $('#preview-empty-title').textContent = building ? 'Bringing your idea to life.' : 'Your idea goes here.';
    $('#preview-empty-note').textContent = building ? 'Hang tight. This window turns into your app the moment there’s something to try.' : 'As your idea takes shape, this window becomes your app.';
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
    if (!text) return fieldError(input, $('#casting-prompt-error'), 'Tell us a little about the app you’re imagining.');
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
    } catch { showError('We couldn’t finish shaping your idea. Your saved brief is still here; try again in a moment.'); }
    finally { castingBusy = false; renderState(); }
}
async function loadReview() {
    const status = await json('/casting/status');
    hasCast = status.hasCast;
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
        await runWork(appBrief());
    } catch { showError('We couldn’t start your app. Your idea is saved. Choose Build my app to try again.'); }
    finally { startingApp = false; renderState(); }
}
async function loadIntent() {
    const response = await fetch(appUrl('/casting/intent'), { cache: 'no-store' });
    if (response.ok) { const result = await response.json(); idea = result.text || idea; persist(); }
    else if (response.status !== 404) throw new Error('Saved idea unavailable');
}
const stateNames = { Active: 'In the making', Completed: 'Delivered', Failed: 'Needs another try', Cancelled: 'Stopped', Cancelling: 'Stopping' };
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
        state.textContent = stateNames[wave.state] ?? 'Taking shape';
        button.append(heading, summary, state);
        button.addEventListener('click', () => selectWave(wave.number).catch(() => showError('This wave couldn’t be opened. Please try again.')));
        li.append(button);
        $('#wave-list').append(li);
    }
    if (focused) $(`#wave-list [data-wave="${focused}"]`)?.focus({ preventScroll: true });
}
function renderNotice(detail) {
    const wave = detail.summary;
    $('#wave-notice').hidden = false;
    $('#evidence-heading').textContent = `Wave ${wave.number}`;
    $('#wave-state').textContent = stateNames[wave.state] ?? 'Taking shape';
    $('#wave-state').dataset.state = wave.state;
    const date = wave.startedAt || wave.createdAt;
    $('#wave-date').textContent = date ? new Date(date).toLocaleDateString(undefined, { month: 'long', day: 'numeric', year: 'numeric' }) : '';
    if (date) $('#wave-date').dateTime = date;
    $('#wave-objective').textContent = humanIntent(asked(wave.objective), 'Bring your idea a little further.');
    const defaults = {
        Active: 'Your app is taking shape. You can keep this page open or come back to this wave later.',
        Completed: 'This round of work is finished. Try the app to see what’s changed, then decide what you’d like next.',
        Failed: 'This round didn’t finish. Your earlier waves are still here. Try a smaller request, or give this another go.',
        Cancelled: 'You stopped this round. Anything already made is still yours. Start another wave whenever you’re ready.'
    };
    announce('#wave-result', defaults[wave.state] ?? 'Wrapping up this round.');
    const evidence = detail.evidence ?? [];
    const highlights = squadHighlights(detail.response);
    $('#wave-story').hidden = !highlights.length || wave.state === 'Active';
    $('#wave-highlights').replaceChildren(...highlights.map(text => { const li = document.createElement('li'); li.textContent = text; return li; }));
    const proof = [];
    const made = (wave.changedFiles ?? []).filter(file => file.category === 'application');
    const added = made.filter(file => file.status === 'Added').length;
    const reworked = made.filter(file => file.status !== 'Added' && file.status !== 'Deleted').length;
    const growth = [added && `added ${added} new ${added === 1 ? 'piece' : 'pieces'}`, reworked && `reworked ${reworked} ${added ? 'more' : reworked === 1 ? 'piece' : 'pieces'}`].filter(Boolean);
    if (growth.length) proof.push(`Your app grew: ${growth.join(' and ')}.`);
    else if (wave.state === 'Completed') proof.push('Nothing in your app needed to change this round.');
    const checked = wave.validationState ?? evidence.filter(item => item.kind === 'validation').at(-1)?.state;
    if (checked === 'Passed') proof.push('This round passed its checks before being handed over.');
    if (checked === 'Failed') proof.push('Something didn’t hold up during checks. Give it another look in the next wave.');
    if (!proof.length) proof.push(wave.state === 'Active' ? 'Work is underway. The finished app will be your proof.' : 'The app itself is the best way to see the result.');
    $('#wave-proof').replaceChildren(...proof.map(text => { const li = document.createElement('li'); li.textContent = text; return li; }));
    $('#wave-try').hidden = !previewUrl;
    if (previewUrl) $('#wave-try').href = previewUrl;
}
async function selectWave(number) {
    selectedWave = number;
    const version = ++selectionVersion;
    renderTimeline();
    const detail = await json(`/waves/${number}`);
    if (detail.summary.state === 'Active') {
        const live = await json('/work/evidence?after=0');
        detail.evidence = live.items;
    }
    if (version === selectionVersion) renderNotice(detail);
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
    $('#preview-caption').textContent = previewUrl ? 'Go on. Give it a try.' : 'A window onto what you’re making.';
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
    const [status, history] = await Promise.all([json('/work/status'), json('/waves')]);
    busy = status.isBusy;
    if (status.state === 'Ready') ready = true;
    const prior = waves.at(-1);
    waves = history;
    if (!selectedWave || (!prior || waves.at(-1)?.number !== prior.number)) selectedWave = waves.at(-1)?.number ?? null;
    renderTimeline();
    if (selectedWave) await selectWave(selectedWave);
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
    if (!ready || busy) return;
    clearError();
    busy = true;
    selectedWave = null;
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
    $('#publish-name').value ||= (idea.toLowerCase().match(/[a-z0-9]+/g) ?? ['my', 'daydream']).slice(0, 4).join('-');
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
        if (!owners.available) { $('#publish-result').textContent = 'Publishing isn’t connected yet. Ask the person who set up Daydream to connect an account, then try again.'; return; }
        for (const owner of [owners.login, ...(owners.organizations ?? [])]) {
            const option = document.createElement('option'); option.value = owner; option.textContent = owner; $('#publish-owner').append(option);
        }
        $('#publish-auth').textContent = `Connected as ${owners.login}. This account is shared with this Daydream setup, not signed in through your browser.`;
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
    reviewedPublication = { owner: $('#publish-owner').value, name, private: true, description: plain(idea, 'Made with Daydream').slice(0, 300) };
    $('#publish-destination').textContent = `Your app will be saved as ${reviewedPublication.owner} / ${name}. Only invited people can see it.`;
    $('#publish-review').hidden = false;
    $('#publish-confirm').focus();
}
async function publish() {
    if (publishing || !reviewedPublication) return;
    const request = reviewedPublication;
    publishing = true;
    for (const control of $('#publish-form').elements) control.disabled = true;
    $('#publish-result').hidden = false;
    $('#publish-result').textContent = 'Giving your app a home…';
    try {
        const response = await fetch(appUrl('/publish'), { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(request) });
        const result = await response.json();
        $('#publish-result').replaceChildren();
        if (response.ok && result.success && result.private === true) {
            $('#publish-result').append('Opening day! Your app has a private home. ');
            if (result.url && /^https:\/\/github\.com\//i.test(result.url)) {
                const link = document.createElement('a'); link.href = result.url; link.target = '_blank'; link.rel = 'noopener'; link.textContent = 'Visit your app’s new home ↗'; $('#publish-result').append(link);
            }
            $('#publish-dialog').classList.add('published');
        } else $('#publish-result').textContent = 'Publication isn’t complete. Check the connected account before trying again with the same name; your app may already have a home there.';
    } catch { $('#publish-result').textContent = 'We couldn’t confirm publication. Check the connected account before trying again with the same name.'; }
    finally {
        publishing = false;
        for (const control of $('#publish-form').elements) control.disabled = false;
        resetPublication();
        $('#publish-result').focus();
    }
}
$('#send-casting-turn').addEventListener('click', propose);
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
        announce('#team-status', 'Your neighborhood');
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
    $('#theme-toggle').setAttribute('aria-label', night ? 'Switch to day mode' : 'Switch to night mode');
    $('#theme-toggle .theme-glyph').textContent = night ? '☀' : '☾';
    $('#theme-toggle .theme-label').textContent = night ? 'Day' : 'Night';
}
$('#theme-toggle').addEventListener('click', () => {
    const next = isNight() ? 'light' : 'dark';
    document.documentElement.dataset.theme = next;
    try { localStorage.setItem('daydream:theme', next); } catch { }
    renderTheme();
});
systemNight.addEventListener('change', renderTheme);
renderTheme();

initialize().catch(() => { renderState(); showError('Daydream couldn’t get ready. Refresh the page to reconnect.'); });
