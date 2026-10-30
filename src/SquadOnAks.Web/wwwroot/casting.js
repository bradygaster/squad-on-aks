const $ = selector => document.querySelector(selector);
function announce(selector, text) {
    const region = $(selector);
    if (region.textContent !== text) region.textContent = text;
}
const reviewFiles = new Map();
const storageKey = 'daydream:conversation';
let threadId = crypto.randomUUID();
let previousRunId = null;
let messages = [];
let proposalText = '';
let appTitle = '';
let idea = '';
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
let lastSquad = '';

// Model text is untrusted: technical material is never displayed, even as progress.
const technical = /```|`|<[^>]+>|https?:|(?:\b(?:AKS|Kubernetes|cluster|pods?|containers?|instances?|git|GitHub|commits?|branches|branch|repos?|repository|repositories|diffs?|PR|pull request|deploy\w*|workspace|terminal|stdout|stderr|bash|npm|dotnet|tests?\/|charter|routing|implementation|fixture|script|trace|logs?|source code)\b)|(?:[\w.-]+\/(?:[\w.-]+\/)*[\w.-]+)|\b[a-f0-9]{12,}\b|[{}]|^\s*[#|]/i;
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
const titleRequest = '\n\n(Also give this app a short, catchy name of two to four words, the kind you would paint on a shop sign. Put it on the very first line of your reply as "App name: <name>".)';
function proposedTitle(text) {
    const match = /^\W*app name\W*[:—–-]\s*\W*(.+?)\W*$/im.exec(text ?? '');
    const name = match?.[1].replace(/[*_`"“”]/g, '').trim();
    return name && name.length <= 40 && plain(name) ? name : '';
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
function listOf(items) {
    if (items.length <= 2) return items.join(' and ');
    return `${items.slice(0, -1).join(', ')} and ${items.at(-1)}`;
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
function showError(message = 'We couldn’t reach your squad. Please try again in a moment.') {
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
    const response = await fetch(url, { cache: 'no-store', ...options });
    if (!response.ok) throw new Error('Request unavailable');
    return response.json();
}
function persist() {
    try { sessionStorage.setItem(storageKey, JSON.stringify({ generation, threadId, previousRunId, messages, proposalText, idea, appTitle })); }
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
        } else sessionStorage.removeItem(storageKey);
    } catch { sessionStorage.removeItem(storageKey); }
}
function roleName(role, name = '') {
    // Built-in members often carry placeholder roles like "(silent)", so their names count too.
    if (/scribe|memory/i.test(name)) return 'Team memory keeper';
    if (/fact/i.test(name)) return 'Fact checker';
    if (/ralph|monitor/i.test(name)) return 'Keeps things moving';
    if (/^rai$/i.test(name) || /safety|\brai\b/i.test(role)) return 'Keeps things safe';
    if (/lead|coordinator|flight/i.test(role)) return 'Squad lead';
    if (/review|test|quality|retro|\bqa\b/i.test(role)) return 'Quality checker';
    if (/design|experience|visual/i.test(role)) return 'Experience designer';
    if (/plan|analyst|research/i.test(role)) return 'Idea planner';
    if (/scribe|memory/i.test(role)) return 'Team memory keeper';
    if (/monitor/i.test(role)) return 'Keeps things moving';
    if (/fact/i.test(role)) return 'Fact checker';
    return 'App maker';
}
// Squad members are reported by their internal ids; only show the names of people actually cast.
function castName(id) {
    const key = String(id ?? '').trim().toLowerCase().replace(/[\s_]+/g, '-');
    const member = castMembers().find(m => m.name.trim().toLowerCase().replace(/[\s_]+/g, '-') === key);
    return member && !/scribe|ralph|monitor/i.test(member.name) ? plain(member.name) : '';
}
function castMembers() {
    const team = reviewFiles.get('.squad/team.md');
    if (!team) {
        return [];
    }

    const lines = team.split('\n');
    const heading = lines.findIndex(line => /^## Members\s*$/i.test(line.trim()));
    if (heading < 0) {
        return [];
    }

    let header = null;
    const members = [];
    const addMember = (name, role, charter) => {
        if (name) {
            const charterText = reviewFiles.get(charter) ?? '';
            const excerpt = charterText.split('\n')
                .map(line => line.trim())
                .find(line => line && !line.startsWith('#') && !line.startsWith('---'))
                ?.replace(/^[-*]\s+/, '')
                .replace(/[`*_]/g, '') ?? '';
            members.push({ name, role: role || excerpt, charter });
        }
    };
    for (const line of lines.slice(heading + 1)) {
        if (/^##\s/.test(line)) {
            break;
        }
        if (line.trim().startsWith('|')) {
            const cells = line.trim().replace(/^\|/, '').replace(/\|$/, '').split('|')
                .map(cell => cell.trim().replace(/[`*_]/g, ''));
            if (cells.every(cell => /^:?-{3,}:?$/.test(cell))) {
                continue;
            }
            if (!header) {
                header = cells.map(cell => cell.toLowerCase());
                continue;
            }

            const name = cells[header.indexOf('name')] ?? cells[0];
            const role = cells[header.indexOf('role')] ?? cells[1] ?? '';
            const charter = cells.find(cell => /^\.squad\/agents\/[a-zA-Z0-9][a-zA-Z0-9-]*\/charter\.md$/i.test(cell));
            addMember(name, role, charter);
            continue;
        }
        const bullet = /^\s*[-*]\s+([^:|]+?)\s*:\s*(.*?)\s*$/.exec(line);
        if (bullet) {
            const detail = bullet[2].replace(/[`*_]/g, '').trim();
            const charter = /^\.squad\/agents\/[a-zA-Z0-9][a-zA-Z0-9-]*\/charter\.md$/i.test(detail)
                ? detail
                : '';
            addMember(bullet[1].replace(/[`*_]/g, '').trim(), charter ? '' : detail, charter);
        }
    }
    return members;
}

function proposedCastMembers(text) {
    const members = new Map();
    const add = (name, role) => {
        const cleanName = name.trim().replace(/^[^\p{L}]+/u, '');
        const cleanRole = role.trim().replace(/[*_`]/g, '');
        const words = cleanName.split(/\s+/u).filter(Boolean);
        if (!/^\p{L}/u.test(cleanName) || cleanName.length > 60 || words.length > 6 || !cleanRole) {
            return;
        }
        const key = cleanName.toLocaleLowerCase();
        members.set(key, { name: cleanName, role: cleanRole });
    };
    const parseDelimited = (name, role) => {
        const cleanName = name.replace(/[*_`]/g, '').trim().replace(/^[^\p{L}]+/u, '');
        const [label, ...detail] = role.replace(/[*_`]/g, '').trim().split(/\s{2,}/u);
        const roleWords = label?.trim().split(/\s+/u).filter(Boolean) ?? [];
        if (roleWords.length <= 4 || detail.length) {
            add(cleanName, detail.length ? `${label.trim()} · ${detail.join(' ').trim()}` : (label ?? ''));
        }
    };
    const tableRows = text.split('\n').filter(line => /^\s*\|/.test(line));
    let headers = null;
    for (const line of tableRows) {
        const cells = line.trim().replace(/^\|/, '').replace(/\|$/, '').split('|')
            .map(cell => cell.trim().replace(/[`*_]/g, ''));
        if (cells.every(cell => /^:?-{3,}:?$/.test(cell))) continue;
        if (!headers && cells.some(cell => /^(?:name|member|specialist)$/i.test(cell))) {
            headers = cells.map(cell => cell.toLowerCase());
            continue;
        }
        const nameIndex = headers?.findIndex(cell => /^(?:name|member|specialist)$/.test(cell)) ?? -1;
        const roleIndex = headers?.findIndex(cell => /^(?:role|specialty|responsibility|focus)$/.test(cell)) ?? -1;
        if (nameIndex >= 0 && roleIndex >= 0 && cells[nameIndex] && cells[roleIndex]) {
            const roleText = cells[roleIndex].split(/\s{2,}/u);
            add(cells[nameIndex], roleText.length > 1 ? `${roleText[0]} · ${roleText.slice(1).join(' ')}` : roleText[0]);
        } else {
            const content = cells.find(cell => /[ \t]+[—–][ \t]+/.test(cell));
            if (content) {
                const pieces = content.split(/[ \t]+[—–][ \t]+/u);
                parseDelimited(pieces.slice(0, -1).join(' — '), pieces.at(-1));
            }
        }
    }

    for (const line of text.split('\n')) {
        if (/^\s*\|/.test(line)) continue;
        const cleaned = line.replace(/^\s*[-*]\s+/, '').replace(/^\s*\d+[.)]\s+/, '').trim();
        const pieces = cleaned.split(/[ \t]+[—–][ \t]+/u);
        if (pieces.length > 1) {
            parseDelimited(pieces.slice(0, -1).join(' — '), pieces.at(-1));
        }
    }
    if (members.size) return [...members.values()];

    const stopWords = new Set(['a', 'an', 'and', 'the', 'or', 'one', 'each', 'my', 'your', 'our', 'this', 'that']);
    const rolePattern = /\b([\p{L}][\p{L}\p{N}_-]{1,})\s+((?:[\p{Ll}][\p{L}-]*\s+){0,2}(?:lead|coordinator|specialist|reviewer|engineer|tester|designer|planner|analyst|architect))\b/giu;
    for (const match of text.matchAll(rolePattern)) {
        if (!stopWords.has(match[1].toLocaleLowerCase())) add(match[1], match[2]);
    }
    return [...members.values()];
}


function renderSquad() {
    const members = hasCast ? castMembers() : proposedCastMembers(proposalText);
    if (hasCast && !members.some(member => /lead|coordinator/i.test(member.role))) members.unshift({ name: 'Your squad lead', role: 'lead' });
    const key = JSON.stringify([members, ready]);
    if (key === lastSquad) return;
    lastSquad = key;
    $('#squad-board').hidden = !members.length && !hasCast;
    $('#squad-nav').hidden = $('#squad-board').hidden;
    $('#squad-heading').textContent = ready ? 'Your squad is on site.' : 'Meet your squad.';
    $('#squad-description').textContent = ready ? 'Different talents. One shared idea: yours.' : 'A team picked for your idea. Refine your idea above, or invite them to get started.';
    $('#squad-members').replaceChildren();
    for (const [index, member] of members.entries()) {
        const row = document.createElement('li');
        const avatar = document.createElement('span');
        avatar.className = `avatar avatar-${index % 4}`;
        avatar.setAttribute('aria-hidden', 'true');
        avatar.textContent = member.name.slice(0, 1).toUpperCase();
        const identity = document.createElement('div');
        const name = document.createElement('strong');
        name.textContent = plain(member.name, 'Your teammate');
        const role = document.createElement('span');
        role.textContent = roleName(member.role || (index === 0 ? 'lead' : 'maker'), member.name);
        identity.append(name, role);
        row.append(avatar, identity);
        $('#squad-members').append(row);
    }
}
function renderState() {
    const started = Boolean(idea || hasCast || waves.length);
    document.body.classList.toggle('has-squad', ready);
    $('#squad-feedback').textContent = ready && !waves.length ? 'You’re all here. Give your squad their first wave of work.' : '';
    const name = appTitle || ideaTitle(idea || asked(waves[0]?.objective), 'Your next big idea.');
    const heading = started ? name : 'What do you want to make?';
    $('#idea-heading').textContent = heading;
    $('#idea-heading').classList.toggle('is-long', heading.length > 28);
    $('#idea-description').textContent = ready ? 'You dream it. Your squad makes it happen. A little more real with every wave.' : "That thing you wish existed? Let's make it real. Describe your idea and we'll find a squad to bring it to life.";
    $('#casting-controls').hidden = ready || hasCast;
    $('#casting-prompt').disabled = castingBusy;
    $('#send-casting-turn').disabled = castingBusy;
    $('#send-casting-turn').textContent = castingBusy ? 'Casting your squad…' : proposalText ? 'Refine my squad ↗' : 'Meet my squad ↗';
    $('#cast-squad').hidden = ready;
    $('#cast-squad').disabled = castingBusy || (!proposalText && !hasCast);
    $('#cast-squad').textContent = castingBusy ? 'Getting your squad ready…' : 'Cast this squad ↗';
    $('#jump-to-wave').hidden = !ready;
    $('#making').hidden = !ready && !waves.length;
    $('#keep-section').hidden = !ready && !waves.length;
    $('#work-request').disabled = !ready || busy;
    $('#send-work').disabled = !ready || busy;
    $('#send-work').textContent = busy ? 'Your squad is working…' : 'Start a wave ↗';
    $('#cancel-work').hidden = !busy;
    $('#work-heading').textContent = busy ? 'Good things are taking shape.' : 'What shall we make happen?';
    announce('#team-status', castingBusy ? 'Casting your squad' : busy ? 'Your squad is working' : ready ? 'Your squad is ready' : proposalText ? 'Your squad is waiting' : 'A little possibility');
    $('#library-app-name').textContent = started ? (appTitle || ideaTitle(idea || asked(waves[0]?.objective), 'Your app')) : 'Your first idea';
    $('#library-app-state').textContent = previewUrl ? 'Ready to try · Open your app' : ready ? 'Taking shape with your squad' : 'An empty plot, full of possibility';
    renderBuilding();
    renderSquad();
}
function renderBuilding() {
    const building = busy && ready;
    $('#viewing-window').classList.toggle('is-building', building);
    $('#viewing-window').setAttribute('aria-busy', String(building));
    $('#preview-status').textContent = building ? 'Your squad is building…' : previewUrl ? 'Your idea, coming to life' : 'Room for something wonderful';
    $('#preview-empty-title').textContent = building ? 'Your squad is building it.' : 'Your idea goes here.';
    $('#preview-empty-note').textContent = building ? 'Hang tight. This window turns into your app the moment there’s something to try.' : 'As your squad builds, this window becomes your app.';
}

// Keep the existing AG-UI conversation protocol, but never render its raw transcript.
async function streamAgentTurn(text, phase) {
    const message = { id: crypto.randomUUID(), role: 'user', content: text };
    messages.push(message);
    let answer = '';
    try {
        const response = await fetch('/ag-ui', {
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
    if (castingBusy || ready) return;
    const input = $('#casting-prompt');
    const text = input.value.trim();
    if (!text) return fieldError(input, $('#casting-prompt-error'), 'Tell us a little about your idea so we can find the right people.');
    clearField(input, $('#casting-prompt-error'));
    clearError();
    castingBusy = true;
    if (!idea) idea = text;
    renderState();
    try {
        proposalText = await streamAgentTurn(text + titleRequest, 'casting');
        if (!proposalText.trim()) throw new Error('No proposal');
        appTitle = proposedTitle(proposalText) || appTitle;
        if (appTitle) json('/casting/title', { method: 'PUT', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ title: appTitle }) }).catch(() => { });
        input.value = '';
        $('#casting-hint').textContent = 'Want a different mix? Tell us what to change, or cast your squad below.';
        persist();
    } catch { showError('We couldn’t put your squad together yet. Try describing your idea again.'); }
    finally { castingBusy = false; renderState(); }
}
async function loadReview() {
    const files = await json('/casting/review');
    reviewFiles.clear();
    for (const file of files) reviewFiles.set(file.path, file.content);
    hasCast = files.length > 0;
}
async function castSquad() {
    if (castingBusy || ready) return;
    castingBusy = true;
    clearError();
    renderState();
    try {
        const confirm = 'I explicitly confirm this proposal. Create the Squad now using the native Init Mode confirmation path. Only create the team files in this turn: do not start, plan in code, or write any project work yet. Each wave of work will arrive later as its own separate request.';
        if (!hasCast) {
            await streamAgentTurn(confirm, 'cast-review');
            await loadReview();
        }
        const activation = await fetch('/casting/activate', { method: 'POST', cache: 'no-store' });
        if (!activation.ok) {
            // The squad sometimes writes part of its team early; ask it to finish the setup, then try once more.
            await streamAgentTurn(confirm + ' Some team files already exist: complete every missing piece, including each member’s history file.', 'cast-review');
            await loadReview();
            await json('/casting/activate', { method: 'POST' });
        }
        ready = true;
        await loadIntent();
        $('#squad-feedback').textContent = 'You’re all here. Give your squad their first wave of work.';
    } catch { showError('Your squad isn’t ready to start yet. Try casting them again. If this keeps happening, ask the person who set up Daydream for help.'); }
    finally { castingBusy = false; renderState(); if (ready) $('#work-request').focus({ preventScroll: true }); }
}
async function loadIntent() {
    const response = await fetch('/casting/intent', { cache: 'no-store' });
    if (response.ok) { const result = await response.json(); idea = result.text || idea; persist(); }
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
        Active: 'Your squad is working through this request. You can keep this page open or come back to this wave later.',
        Completed: 'Your squad finished this round of work. Try the app to see what’s changed, then tell them what you’d like next.',
        Failed: 'This round didn’t finish. Your earlier waves are still here. Try a smaller request, or ask your squad to give this another go.',
        Cancelled: 'You stopped this round. Anything already made is still yours. Start another wave whenever you’re ready.'
    };
    announce('#wave-result', defaults[wave.state] ?? 'Your squad is wrapping up this round.');
    const evidence = detail.evidence ?? [];
    const highlights = squadHighlights(detail.response);
    $('#wave-story').hidden = !highlights.length || wave.state === 'Active';
    $('#wave-highlights').replaceChildren(...highlights.map(text => { const li = document.createElement('li'); li.textContent = text; return li; }));
    const proof = [];
    const activeActor = [...evidence].reverse().find(item => item.kind === 'specialist' && item.actor && /Active|Progress|Delegated/.test(item.state));
    const names = [...new Set([...(wave.specialists ?? []), ...evidence.filter(item => item.kind === 'specialist').map(item => item.actor)]
        .map(castName).filter(Boolean))];
    if (wave.state === 'Active' && activeActor) proof.push(`${castName(activeActor.actor) || 'A teammate'} is working on this wave right now.`);
    else if (names.length) proof.push(wave.state === 'Active'
        ? `${listOf(names)} ${names.length === 1 ? 'is' : 'are'} on the case.`
        : `${listOf(names)} took part in this round.`);
    const made = (wave.changedFiles ?? []).filter(file => file.category === 'application');
    const added = made.filter(file => file.status === 'Added').length;
    const reworked = made.filter(file => file.status !== 'Added' && file.status !== 'Deleted').length;
    const growth = [added && `added ${added} new ${added === 1 ? 'piece' : 'pieces'}`, reworked && `reworked ${reworked} ${added ? 'more' : reworked === 1 ? 'piece' : 'pieces'}`].filter(Boolean);
    if (growth.length) proof.push(`Your app grew: they ${growth.join(' and ')}.`);
    else if (wave.state === 'Completed') proof.push('Nothing in your app needed to change this round.');
    const checked = wave.validationState ?? evidence.filter(item => item.kind === 'validation').at(-1)?.state;
    if (checked === 'Passed') proof.push('They checked their work before handing it over, and everything held up.');
    if (checked === 'Failed') proof.push('Something didn’t hold up when they checked. Ask your squad to take another look in the next wave.');
    if (!proof.length) proof.push(wave.state === 'Active' ? 'Your squad is on the case. The finished app will be your proof.' : 'The app itself is the best way to see the result.');
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
    const preview = await json('/workspace/preview');
    const base = preview.url || (preview.port ? `${location.protocol}//${location.hostname}:${preview.port}` : null);
    previewUrl = preview.available && base && /^https?:\/\//i.test(new URL(base, location.href).href) ? `${base.replace(/\/$/, '')}/` : null;
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
}
async function refresh() {
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
    if (!ready || busy) return;
    const input = $('#work-request');
    const text = input.value.trim();
    if (!text) return fieldError(input, $('#work-request-error'), 'Give your squad one thing to make or improve.');
    clearField(input, $('#work-request-error'));
    clearError();
    busy = true;
    selectedWave = null;
    renderState();
    try { await streamAgentTurn(text + recapRequest, 'work'); input.value = ''; }
    catch { showError('This wave didn’t finish as expected. Check its notice below, then try another wave.'); }
    finally {
        try { await refresh(); if (previewUrl) $('#app-preview').src = previewUrl; }
        catch { busy = false; renderState(); showError(); }
    }
}
async function cancelWork() {
    $('#cancel-work').disabled = true;
    try {
        const result = await json('/work/cancel', { method: 'POST' });
        $('#team-status').textContent = result.requested ? 'Your squad is stopping' : 'This wave has already finished';
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
        const response = await fetch('/publish', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(request) });
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
$('#cast-squad').addEventListener('click', castSquad);
$('#send-work').addEventListener('click', sendWork);
$('#cancel-work').addEventListener('click', cancelWork);
$('#open-app').addEventListener('click', event => { if (!previewUrl) event.preventDefault(); });
$('#toggle-publish').addEventListener('click', openPublish);
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
    const status = await json('/casting/status');
    restore(status.generation);
    appTitle = status.appTitle || appTitle;
    ready = status.canTakeRequests;
    hasCast = status.hasCast;
    if (hasCast) await loadReview();
    if (hasCast) await loadIntent();
    await refresh();
    renderState();
    setInterval(async () => {
        if (polling || document.hidden) return;
        polling = true;
        try { await refresh(); } catch { showError(); } finally { polling = false; }
    }, 2000);
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

initialize().catch(() => { renderState(); showError('Daydream couldn’t get ready. Refresh the page to reconnect with your squad.'); });
