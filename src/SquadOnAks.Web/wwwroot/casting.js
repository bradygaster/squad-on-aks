const $ = selector => document.querySelector(selector);

const status = $('[role="status"]');
const alert = $('[role="alert"]');
const outcome = $('#outcome-indicator');
const phaseLede = $('#phase-lede');

const setup = $('#setup');
const setupSummary = $('#setup-summary');
const prompt = $('#casting-prompt');
const sendTurn = $('#send-casting-turn');
const castingHint = $('#casting-hint');
const confirmCast = $('#confirm-cast');
const conversation = $('[aria-label="Cast conversation"]');
const fileSelect = $('#review-file');
const fileContent = $('#review-content');
const saveReview = $('#save-review');
const activateCast = $('#activate-cast');
const validationErrors = $('[aria-label="Validation errors"]');

const workRequest = $('#work-request');
const sendWork = $('#send-work');
const cancelWork = $('#cancel-work');
const workLock = $('#work-lock');
const workHeading = $('#work-heading');
const workConversation = $('[aria-label="Work conversation"]');
const specialistActivity = $('[aria-label="Specialist activity"]');
const applicationEvidence = $('[aria-label="Application evidence"]');
const harnessEvidence = $('[aria-label="Harness evidence"]');
const validationEvidence = $('[aria-label="Validation evidence"]');

const waveList = $('[aria-label="Wave timeline"]');
const waveCount = $('#wave-count');
const wavesEmpty = $('#waves-empty');
const evidenceHeading = $('#evidence-heading');
const waveMeta = $('#wave-meta');

const openApp = $('#open-app');
const toggleSource = $('#toggle-source');
const rawFile = $('#raw-file');
const fileTree = $('#file-tree');
const fileFilter = $('#file-filter');
const fileHeading = $('#file-heading');
const fileBody = $('#file-body');
const artifactsSummary = $('#artifacts-summary');

const togglePublish = $('#toggle-publish');
const publishForm = $('#publish-form');
const publishOwner = $('#publish-owner');
const publishName = $('#publish-name');
const publishReview = $('#publish-review');
const publishDestination = $('#publish-destination');
const publishConfirm = $('#publish-confirm');
let reviewedPublication = null;
let publishing = false;
const publishSubmit = $('#publish-submit');
const publishCancel = $('#publish-cancel');
const publishResult = $('#publish-result');

const storageKey = 'squad-on-aks:conversation';
let workspaceGeneration = null;
let threadId = crypto.randomUUID();
const messages = [];
const transcript = [];
let previousRunId = null;
let turnCount = 0;

const reviewFiles = new Map();
let evidenceSequence = 0;
let evidenceTimer = null;
let wavesTimer = null;
let waves = [];
let selectedWave = null;
let selectionGeneration = 0;
let evidenceRequest = null;
let liveWave = false;
let workBusy = false;
let canTakeRequests = false;
let files = [];
let selectedFile = null;
let showingSource = false;
let ownersLoaded = false;
let liveNodes = [];
let serverIntent = null;
let intentLoadMessage = 'Loading original Squad intent…';
let progressEvent = null;
let progressContext = null;
let repeatedActivity = null;
let progressActor = null;
const liveProgress = document.createElement('p');
liveProgress.id = 'live-progress';
liveProgress.className = 'hint live-progress';
liveProgress.setAttribute('aria-live', 'polite');
liveProgress.hidden = true;
waveMeta.after(liveProgress);

function renderLiveProgress() {
    const active = waves.find(wave => wave.number === selectedWave)?.state === 'Active' ||
        (liveWave && workBusy);
    liveProgress.hidden = !active;
    if (!active) {
        stopActivitySpinners();
        return;
    }
    const actor = progressActor
        ? `Latest specialist: ${progressActor.actor} · ${progressActor.state}`
        : 'Squad is working';
    const summary = progressEvent?.summary;
    const shortSummary = summary && (summary.length > 160 || /[\n*`#]/.test(summary))
        ? 'Update available in Specialist activity' : summary;
    const latest = progressEvent
        ? `${shortSummary || progressEvent.state} · ${new Date(progressEvent.timestamp).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit', second: '2-digit' })}`
        : 'Waiting for the first activity event.';
    liveProgress.textContent = [actor, progressContext !== progressEvent ? progressContext?.summary : null, latest].filter(Boolean).join(' — ');
}

// Enhance the server markup in place so live UI iterations do not restart a Squad.
const workView = document.createElement('section');
workView.id = 'work-view';
workView.className = 'workspace-view';
setup.before(workView);
workView.append(setup, $('.work-layout'));
const artifactsView = $('.artifacts-panel');
artifactsView.id = 'artifacts-view';
const navigation = document.createElement('nav');
navigation.className = 'workspace-nav';
navigation.setAttribute('aria-label', 'Workspace views');
navigation.setAttribute('role', 'tablist');
workView.before(navigation);
const viewTabs = [ ['Work', workView], ['Artifacts', artifactsView] ].map(([label, panel]) => {
    const tab = document.createElement('button');
    tab.type = 'button';
    tab.id = `${panel.id}-tab`;
    tab.setAttribute('role', 'tab');
    tab.setAttribute('aria-controls', panel.id);
    tab.textContent = label;
    panel.setAttribute('role', 'tabpanel');
    panel.setAttribute('aria-labelledby', tab.id);
    navigation.append(tab);
    tab.addEventListener('click', () => selectWorkspaceView(panel.id));
    return { tab, panel };
});

function selectWorkspaceView(id, focusTab = false) {
    for (const { tab, panel } of viewTabs) {
        const selected = panel.id === id;
        panel.hidden = !selected;
        tab.setAttribute('aria-selected', String(selected));
        tab.tabIndex = selected ? 0 : -1;
        if (selected && focusTab) {
            tab.focus();
        }
    }
}

navigation.addEventListener('keydown', event => {
    const current = viewTabs.findIndex(({ tab }) => tab === document.activeElement);
    if (current < 0 || !['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) {
        return;
    }
    event.preventDefault();
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? viewTabs.length - 1
        : (current + (event.key === 'ArrowRight' ? 1 : -1) + viewTabs.length) % viewTabs.length;
    selectWorkspaceView(viewTabs[next].panel.id, true);
});
selectWorkspaceView('work-view');

const castingControls = document.createElement('div');
castingControls.id = 'casting-controls';
prompt.before(castingControls);
castingControls.append($('label[for="casting-prompt"]'), prompt, sendTurn.parentElement, castingHint);

const originalIntent = document.createElement('section');
originalIntent.className = 'original-intent';
originalIntent.hidden = true;
const intentHeading = document.createElement('h2');
intentHeading.textContent = 'Original Squad intent';
const intentText = document.createElement('p');
originalIntent.append(intentHeading, intentText);
$('.workspace').before(originalIntent);

const castHistory = document.createElement('details');
castHistory.className = 'cast-history';
const historySummary = document.createElement('summary');
historySummary.textContent = 'Cast conversation';
conversation.before(castHistory);
castHistory.append(historySummary, conversation);
castHistory.open = true;

function updateCastHistory() {
    castHistory.hidden = ![...conversation.children].some(message => message.textContent.trim());
}

new MutationObserver(updateCastHistory).observe(conversation, {
    childList: true,
    subtree: true,
    characterData: true
});
updateCastHistory();

const viewTeam = document.createElement('button');
viewTeam.type = 'button';
viewTeam.id = 'view-team-artifacts';
viewTeam.className = 'secondary small';
viewTeam.textContent = 'View team files in Artifacts';
viewTeam.hidden = true;
originalIntent.append(viewTeam);
viewTeam.addEventListener('click', async () => {
    try {
        selectWorkspaceView('artifacts-view');
        await refreshArtifacts();
        fileFilter.value = '.squad/';
        renderTree();
        await openFile('.squad/team.md');
        fileHeading.tabIndex = -1;
        fileHeading.focus({ preventScroll: true });
        $('.artifacts-panel').scrollIntoView({ block: 'start' });
    } catch (error) {
        showError(error);
    }
});

const evidenceGroups = [validationEvidence, applicationEvidence, specialistActivity, harnessEvidence].map(list => {
    const section = list.parentElement;
    const heading = section.querySelector('h3');
    const details = document.createElement('details');
    details.className = 'evidence-group';
    const summary = document.createElement('summary');
    const label = document.createElement('span');
    label.textContent = heading.textContent;
    const count = document.createElement('span');
    count.className = 'evidence-count';
    summary.append(label, count);
    section.replaceWith(details);
    details.append(summary, list);
    details.hidden = true;
    return { list, details, count };
});
const evidenceContainer = $('.evidence-grid');
evidenceContainer.replaceChildren(...evidenceGroups.map(group => group.details));
applicationEvidence.setAttribute('role', 'tablist');
applicationEvidence.classList.add('change-tabs');
const applicationPane = document.createElement('div');
applicationPane.id = 'application-diff-pane';
applicationPane.className = 'application-diff';
applicationPane.setAttribute('role', 'tabpanel');
applicationPane.tabIndex = 0;
applicationEvidence.after(applicationPane);
let applicationTabIndex = 0;

applicationEvidence.addEventListener('keydown', event => {
    const tabs = [...applicationEvidence.querySelectorAll('[role="tab"]')];
    const current = tabs.indexOf(document.activeElement);
    if (current < 0 || !['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) {
        return;
    }
    event.preventDefault();
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? tabs.length - 1
        : (current + (event.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length;
    tabs[next].click();
    tabs[next].focus();
});

function appendApplicationChange(item) {
    const row = document.createElement('li');
    row.setAttribute('role', 'presentation');
    const tab = document.createElement('button');
    tab.type = 'button';
    tab.id = `application-change-${++applicationTabIndex}`;
    tab.setAttribute('role', 'tab');
    tab.setAttribute('aria-controls', applicationPane.id);
    tab.setAttribute('aria-selected', 'false');
    tab.tabIndex = -1;
    tab.textContent = `${item.path} · ${item.state}`;
    tab.title = tab.textContent;
    row.append(tab);
    applicationEvidence.append(row);
    const select = () => {
        for (const other of applicationEvidence.querySelectorAll('[role="tab"]')) {
            const selected = other === tab;
            other.setAttribute('aria-selected', String(selected));
            other.tabIndex = selected ? 0 : -1;
        }
        applicationPane.setAttribute('aria-labelledby', tab.id);
        applicationPane.replaceChildren();
        const caption = document.createElement('p');
        caption.className = 'hint';
        caption.textContent = `${item.path} · ${item.state} · Historical diff for this wave`;
        applicationPane.append(caption);
        if (item.diff) {
            applicationPane.append(renderDiff(item.diff, item.path));
        } else {
            const unavailable = document.createElement('p');
            unavailable.className = 'hint';
            unavailable.textContent = 'No textual diff was recorded for this file. The change metadata above is from this wave.';
            applicationPane.append(unavailable);
        }
    };
    tab.addEventListener('click', select);
    if (!applicationEvidence.querySelector('[aria-selected="true"]')) {
        select();
    }
    updateEvidenceGroups();
}

function updateEvidenceGroups() {
    for (const { list, details, count } of evidenceGroups) {
        const total = list.children.length;
        details.hidden = total === 0;
        count.textContent = String(total);
    }
}

function updateCastingContext() {
    intentText.textContent = serverIntent ?? intentLoadMessage;
    originalIntent.hidden = !canTakeRequests;
    castingControls.hidden = canTakeRequests;
    prompt.readOnly = canTakeRequests;
    fileContent.readOnly = canTakeRequests;
    saveReview.hidden = canTakeRequests;
    activateCast.hidden = canTakeRequests;
    $('.conversation-panel > .panel-heading').hidden = true;
    $('.review-panel').hidden = canTakeRequests || reviewFiles.size === 0;
    viewTeam.hidden = !canTakeRequests;
    $('#review-heading').textContent = canTakeRequests ? 'Active team files' : 'Cast review';
    $('#review-hint').textContent = canTakeRequests
        ? 'Read-only team configuration. Send new work through Next wave.'
        : 'Review team.md, routing.md, and each active charter before activation.';
    document.body.dataset.castActive = String(canTakeRequests);
    updateCastHistory();
}

async function loadOriginalIntent() {
    try {
        const response = await fetch('/casting/intent', { cache: 'no-store' });
        if (!response.headers.get('content-type')?.includes('application/json')) {
            throw new Error('Original intent service is unavailable in this running version. The server update must be activated before it can load.');
        }
        const result = await response.json();
        if (!response.ok) {
            throw new Error(result.message || `Original intent request failed (HTTP ${response.status}).`);
        }
        if (typeof result.text !== 'string' || !result.text.trim()) {
            throw new Error('The server returned an invalid original intent record.');
        }
        serverIntent = result.text;
    } catch (error) {
        intentLoadMessage = `Could not load original intent: ${error.message}`;
    }
    updateCastingContext();
}

function persist() {
    if (workspaceGeneration) {
        sessionStorage.setItem(storageKey, JSON.stringify({
            generation: workspaceGeneration, threadId, messages, transcript, previousRunId
        }));
    }
}

function restoreSession(generation) {
    workspaceGeneration = typeof generation === 'string' ? generation : null;
    try {
        const saved = JSON.parse(sessionStorage.getItem(storageKey) ?? 'null');
        if (workspaceGeneration && saved?.generation === workspaceGeneration &&
            typeof saved.threadId === 'string' && Array.isArray(saved.messages) && Array.isArray(saved.transcript)) {
            threadId = saved.threadId;
            messages.push(...saved.messages);
            transcript.push(...saved.transcript);
            previousRunId = saved.previousRunId ?? null;
            turnCount = transcript.filter(entry => entry.phase !== 'work' && entry.role === 'assistant').length;
        } else {
            sessionStorage.removeItem(storageKey);
        }
    } catch (error) {
        sessionStorage.removeItem(storageKey);
        showError(new Error(`Saved conversation could not be restored: ${error.message}`));
    }
    restoreTranscript();
}

function setOutcome(value, style = 'neutral') {
    outcome.textContent = value;
    outcome.className = `outcome ${style}`;
}

function showError(error) {
    alert.textContent = error?.message ?? String(error);
}

const ledes = {
    'Needs cast': 'Describe the work, refine the proposed cast, then confirm before Squad writes its team files.',
    Casting: 'Keep refining the proposal, or confirm it when the cast looks right.',
    'Cast review': 'Review and edit the team files, then start taking requests.',
    Ready: 'Send the next bounded request. Each request becomes a wave you can reopen later.',
    Active: 'The Squad is working on the current wave. Evidence streams in below.',
    Cancelling: 'Cancelling the active wave…'
};

function setStatus(state) {
    status.textContent = state;
    phaseLede.textContent = ledes[state] ?? ledes.Ready;
    document.body.dataset.state = state.toLowerCase().replaceAll(' ', '-');
}

function formatDuration(start, end) {
    const seconds = Math.max(0, Math.round(((end ? new Date(end) : new Date()) - new Date(start)) / 1000));
    if (seconds < 60) {
        return `${seconds}s`;
    }
    const minutes = Math.floor(seconds / 60);
    return `${minutes}m ${String(seconds % 60).padStart(2, '0')}s`;
}

function stateStyle(state) {
    return {
        Completed: 'success',
        Active: 'busy',
        Failed: 'failure',
        Cancelled: 'cancelled'
    }[state] ?? 'neutral';
}

// ---------- Conversations ----------

function setMessageText(item, role, text) {
    if (role === 'assistant') {
        item.innerHTML = renderMarkdown(text, { breaks: true });
    } else {
        item.textContent = text;
    }
}

function appendMessage(list, role, text) {
    const item = document.createElement('li');
    item.className = role;
    setMessageText(item, role, text);
    list.append(item);
    list.scrollTop = list.scrollHeight;
    return item;
}

function restoreTranscript() {
    for (const entry of transcript.filter(entry => entry.phase !== 'work')) {
        appendMessage(conversation, entry.role, entry.text);
    }
}

async function streamAgentTurn(text, phase, list) {
    const userItem = appendMessage(list, 'user', text);
    messages.push({ id: crypto.randomUUID(), role: 'user', content: text });
    const runId = crypto.randomUUID();
    const assistantItem = appendMessage(list, 'assistant pending', 'Thinking…');
    if (phase === 'work') {
        liveNodes = [userItem, assistantItem];
    }
    let assistantText = '';
    const startedAt = Date.now();
    const thinkingTimer = setInterval(() => {
        if (!assistantText) {
            const seconds = Math.floor((Date.now() - startedAt) / 1000);
            assistantItem.textContent = seconds < 5
                ? 'Thinking…'
                : `Thinking… ${seconds >= 60 ? `${Math.floor(seconds / 60)}m ` : ''}${String(seconds % 60).padStart(seconds >= 60 ? 2 : 1, '0')}s`;
        }
    }, 1000);

    try {
        const response = await fetch('/ag-ui', {
            method: 'POST',
            headers: { 'content-type': 'application/json', 'X-Squad-Phase': phase },
            body: JSON.stringify({
                threadId,
                runId,
                parentRunId: previousRunId,
                messages,
                state: {},
                tools: [],
                context: [],
                forwardedProps: {}
            })
        });

        if (!response.ok) {
            assistantItem.remove();
            userItem.remove();
            messages.pop();
            throw new Error(`${response.status} ${response.statusText}: ${await response.text()}`);
        }

        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let buffer = '';
        let currentMessageId = null;
        try {
            while (true) {
                const { value, done } = await reader.read();
                if (done) {
                    break;
                }

                buffer += decoder.decode(value, { stream: true });
                const frames = buffer.split('\n\n');
                buffer = frames.pop() ?? '';
                for (const frame of frames) {
                    const data = frame.split('\n').find(line => line.startsWith('data: '));
                    if (!data) {
                        continue;
                    }

                    const event = JSON.parse(data.substring(6));
                    if (event.type === 'RUN_STARTED') {
                        previousRunId = event.runId;
                    } else if (event.type === 'TEXT_MESSAGE_CONTENT') {
                        const messageId = event.messageId ?? currentMessageId;
                        if (messageId !== currentMessageId && assistantText && !assistantText.endsWith('\n')) {
                            assistantText += '\n\n';
                        }
                        currentMessageId = messageId;
                        assistantText += event.delta ?? '';
                        assistantItem.className = 'assistant';
                        setMessageText(assistantItem, 'assistant', assistantText);
                        list.scrollTop = list.scrollHeight;
                    } else if (event.type === 'RUN_ERROR') {
                        throw new Error(event.message ?? 'Native Squad run failed.');
                    }
                }
            }
        } finally {
            if (!assistantText) {
                assistantItem.remove();
            }
        }

        if (assistantText.trim()) {
            messages.push({ id: crypto.randomUUID(), role: 'assistant', content: assistantText });
        }
        transcript.push({ role: 'user', text, phase }, { role: 'assistant', text: assistantText, phase });
        persist();
        return assistantText;
    } finally {
        clearInterval(thinkingTimer);
    }
}

// ---------- Casting ----------

function setCastingBusy(busy) {
    const castCreated = reviewFiles.size > 0;
    sendTurn.disabled = busy || castCreated || canTakeRequests;
    prompt.disabled = busy || castCreated || canTakeRequests;
    castingHint.textContent = castCreated
        ? 'Cast confirmed. Review the team files, then start taking requests.'
        : 'Refine as often as you like. No team files are written until you confirm.';
    confirmCast.disabled = busy || turnCount === 0 || castCreated || canTakeRequests;
    sendTurn.textContent = busy ? 'Streaming…' : (turnCount === 0 ? 'Propose cast' : 'Refine proposal');
    const proposalReady = turnCount > 0 && !castCreated;
    sendTurn.classList.toggle('secondary', proposalReady || castCreated);
    confirmCast.classList.toggle('secondary', !proposalReady);
}

async function sendCastingTurn(isConfirmation = false) {
    if (canTakeRequests) {
        showError('This Squad is already active. Send new work through Next wave.');
        return;
    }
    const text = isConfirmation
        ? 'I explicitly confirm this proposal. Create the Squad now using the native Init Mode confirmation path. Only create the team files in this turn: do not start, plan in code, or write any project work yet. Each wave of work will arrive later as its own separate request.'
        : prompt.value.trim();
    if (!text) {
        showError('Describe what the Squad will do before sending a turn.');
        prompt.focus();
        return;
    }

    alert.textContent = '';
    validationErrors.replaceChildren();
    setCastingBusy(true);
    if (isConfirmation) {
        confirmCast.textContent = 'Writing team files…';
    }

    try {
        const assistantText = await streamAgentTurn(text, isConfirmation ? 'cast-review' : 'casting', conversation);
        if (!assistantText.trim()) {
            throw new Error('Native Squad returned no streamed proposal text.');
        }

        turnCount += 1;
        prompt.value = '';
        setStatus(isConfirmation ? 'Cast review' : 'Casting');
        if (isConfirmation) {
            await loadReview();
        }
    } catch (error) {
        showError(error);
    } finally {
        confirmCast.textContent = 'Confirm this cast';
        setCastingBusy(false);
    }
}

async function loadReview() {
    const response = await fetch('/casting/review');
    if (!response.ok) {
        throw new Error(await response.text());
    }

    const reviewed = await response.json();
    const previous = fileSelect.value;
    reviewFiles.clear();
    fileSelect.replaceChildren();
    for (const file of reviewed) {
        reviewFiles.set(file.path, file.content);
        const option = document.createElement('option');
        option.value = file.path;
        option.textContent = file.path;
        fileSelect.append(option);
    }

    if (reviewFiles.has(previous)) {
        fileSelect.value = previous;
    }

    const enabled = reviewed.length > 0;
    fileSelect.disabled = !enabled;
    fileContent.disabled = !enabled;
    saveReview.disabled = !enabled;
    activateCast.disabled = !enabled || canTakeRequests;
    confirmCast.disabled = enabled || turnCount === 0;
    setCastingBusy(false);
    castHistory.open = !enabled && !canTakeRequests;
    updateCastingContext();
    showSelectedReviewFile();
}

function showSelectedReviewFile() {
    fileContent.value = reviewFiles.get(fileSelect.value) ?? '';
}

async function saveSelectedReviewFile() {
    const response = await fetch('/casting/review', {
        method: 'PUT',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ path: fileSelect.value, content: fileContent.value })
    });
    const result = await response.json();
    if (!response.ok) {
        showError(result.message);
        return;
    }

    reviewFiles.set(fileSelect.value, fileContent.value);
    alert.textContent = '';
    saveReview.textContent = 'Saved';
    setTimeout(() => saveReview.textContent = 'Save review edit', 1500);
}

async function activate() {
    const response = await fetch('/casting/activate', { method: 'POST' });
    const result = await response.json();
    validationErrors.replaceChildren();
    if (!response.ok) {
        setStatus('Cast review');
        setOutcome('Blocked', 'blocked');
        for (const error of result.errors) {
            const item = document.createElement('li');
            item.textContent = error;
            validationErrors.append(item);
        }
        return;
    }

    setStatus(result.state);
    setOutcome('Ready', 'neutral');
    setReadyForWork(true);
    await loadOriginalIntent();
    setup.open = false;
    refreshArtifacts().catch(showError);
    workRequest.focus();
}

function setReadyForWork(ready) {
    const wasReady = canTakeRequests;
    canTakeRequests = ready;
    workRequest.disabled = !ready;
    sendWork.disabled = !ready || workBusy;
    activateCast.disabled = ready || reviewFiles.size === 0;
    workLock.textContent = ready ? '' : 'Unlocks after the cast is activated.';
    if (ready) {
        const count = reviewFiles.size;
        setupSummary.textContent = `Cast active${count ? ` · ${count} team file${count === 1 ? '' : 's'}` : ''}. View original intent and team.`;
        if (!wasReady) {
            castHistory.open = false;
        }
    }
    setCastingBusy(false);
    updateCastingContext();
}

// ---------- Evidence ----------

function clearEvidenceLists() {
    specialistActivity.replaceChildren();
    applicationEvidence.replaceChildren();
    applicationPane.replaceChildren();
    harnessEvidence.replaceChildren();
    validationEvidence.replaceChildren();
    progressActor = null;
    progressEvent = null;
    progressContext = null;
    repeatedActivity = null;
    renderLiveProgress();
    updateEvidenceGroups();
}

function appendCodeTokens(container, text, path = '') {
    const hashComments = /\.(sh|py|ya?ml|toml)$/i.test(path);
    const pattern = /("(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|`(?:\\.|[^`\\])*`|\/\/[^\n]*|\b(?:async|await|break|case|catch|class|const|continue|default|else|export|false|for|from|function|if|import|in|let|new|null|return|static|switch|throw|true|try|undefined|var|while|using|public|private|void)\b|\b\d+(?:\.\d+)?\b)/g;
    let offset = 0;
    for (const match of text.matchAll(pattern)) {
        container.append(document.createTextNode(text.slice(offset, match.index)));
        const token = document.createElement('span');
        const value = match[0];
        token.className = `syntax-${value.startsWith('//') ? 'comment'
            : /^["'`]/.test(value) ? 'string'
            : /^\d/.test(value) ? 'number' : 'keyword'}`;
        token.textContent = value;
        container.append(token);
        offset = match.index + value.length;
    }
    container.append(document.createTextNode(text.slice(offset)));
    if (hashComments && text.trimStart().startsWith('#')) {
        container.replaceChildren(Object.assign(document.createElement('span'), {
            className: 'syntax-comment', textContent: text
        }));
    }
}

function renderDiff(text, path = '') {
    const pre = document.createElement('pre');
    pre.className = 'diff';
    for (const line of text.split('\n')) {
        const span = document.createElement('span');
        if (line.startsWith('+++') || line.startsWith('---')) {
            span.className = 'meta';
        } else if (line.startsWith('+')) {
            span.className = 'add';
        } else if (line.startsWith('-')) {
            span.className = 'del';
        } else if (line.startsWith('@@')) {
            span.className = 'hunk';
        }
        if (span.className === 'meta' || span.className === 'hunk') {
            span.textContent = `${line}\n`;
        } else {
            appendCodeTokens(span, `${line}\n`, path);
        }
        pre.append(span);
    }
    return pre;
}

function appendEvidence(list, item, title, detail, collapsible = true) {
    const row = document.createElement('li');
    const heading = document.createElement('strong');
    heading.textContent = title;
    row.append(heading);
    if (detail) {
        const metadata = document.createElement('small');
        metadata.textContent = detail;
        row.append(metadata);
    }

    const body = item.diff ?? item.output;
    if (body) {
        const content = item.diff ? renderDiff(body) : Object.assign(document.createElement('pre'), { textContent: body });
        if (collapsible) {
            const details = document.createElement('details');
            const summary = document.createElement('summary');
            const lines = body.split('\n').length;
            summary.textContent = item.diff ? `Show diff (${lines} lines)` : `Show output (${lines} lines)`;
            details.append(summary, content);
            row.append(details);
        } else {
            row.append(content);
        }
    }
    list.append(row);
    updateEvidenceGroups();
    return row;
}

function stopActivitySpinners() {
    for (const row of specialistActivity.querySelectorAll('.activity-running')) {
        row.classList.remove('activity-running');
    }
}

function appendActivity(item) {
    const active = waves.find(wave => wave.number === selectedWave)?.state === 'Active' ||
        (liveWave && workBusy);
    // Only adjacent identical trace events coalesce; null actors are never inferred.
    const key = JSON.stringify([item.actor, item.state, item.summary, item.source]);
    const repeatable = item.state === 'Progress' && !item.output && !item.diff;
    if (repeatable && repeatedActivity?.key === key &&
        specialistActivity.lastElementChild === repeatedActivity.row) {
        repeatedActivity.count += 1;
        repeatedActivity.counter.hidden = false;
        repeatedActivity.counter.textContent = `Repeated ${repeatedActivity.count} times · latest ${new Date(item.timestamp).toLocaleTimeString()}`;
        repeatedActivity.row.classList.toggle('activity-running', active);
        return;
    }
    stopActivitySpinners();
    const row = appendEvidence(
        specialistActivity,
        item,
        `${item.actor ? `${item.actor} · ` : ''}${item.state}`,
        null);
    if (item.summary) {
        const summary = document.createElement('div');
        summary.className = 'markdown activity-summary';
        summary.innerHTML = renderMarkdown(item.summary);
        row.append(summary);
    }
    if (item.source) {
        const metadata = document.createElement('small');
        metadata.className = 'activity-source';
        metadata.textContent = item.source;
        row.append(metadata);
    }
    row.classList.toggle('activity-running', active && ['Delegated', 'Active', 'Progress'].includes(item.state));
    repeatedActivity = null;
    if (repeatable) {
        const counter = document.createElement('small');
        counter.className = 'activity-repeat';
        counter.hidden = true;
        row.append(counter);
        repeatedActivity = { key, row, counter, count: 1 };
    }
}

function renderEvidence(item) {
    if (item.kind === 'specialist') {
        progressEvent = item;
        if (item.actor && item.state !== 'Selected') {
            progressActor = item;
        }
        if (item.actor && item.summary && !item.summary.startsWith('Requested tools:')) {
            progressContext = item;
        }
        renderLiveProgress();
    }
    if (item.kind === 'specialist' || item.kind === 'state') {
        appendActivity(item);
    } else if (item.kind === 'application') {
        appendApplicationChange(item);
    } else if (item.kind === 'harness') {
        appendEvidence(harnessEvidence, item, item.path, item.state);
    } else if (item.kind === 'validation') {
        appendEvidence(validationEvidence, item, `${item.command} · exit ${item.exitCode}`, item.state);
        if (item.exitCode !== 0) {
            validationEvidence.closest('details').open = true;
        }
    }
}

async function refreshLiveEvidence() {
    if (!liveWave) {
        return;
    }
    const generation = selectionGeneration;
    const number = selectedWave;
    if (evidenceRequest?.generation === generation) {
        return;
    }
    const request = { generation };
    evidenceRequest = request;
    try {
        const response = await fetch(`/work/evidence?after=${evidenceSequence}`);
        if (!response.ok) {
            return;
        }
        const snapshot = await response.json();
        if (selectionGeneration !== generation || selectedWave !== number || !liveWave) {
            return;
        }
        for (const item of snapshot.items) {
            renderEvidence(item);
        }
        evidenceSequence = snapshot.lastSequence;
    } finally {
        if (evidenceRequest === request) {
            evidenceRequest = null;
        }
    }
}

// ---------- Waves ----------

function renderWaveList() {
    const focusedWave = waveList.contains(document.activeElement)
        ? document.activeElement.dataset.wave : null;
    waveList.replaceChildren();
    waveCount.textContent = String(waves.length);
    wavesEmpty.hidden = waves.length > 0;
    for (const wave of [...waves].reverse()) {
        const item = document.createElement('li');
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'wave-card';
        button.dataset.wave = String(wave.number);
        if (selectedWave === wave.number) {
            button.setAttribute('aria-current', 'true');
        }

        const top = document.createElement('span');
        top.className = 'wave-card-top';
        const label = document.createElement('span');
        label.className = 'wave-number';
        label.textContent = `Wave ${wave.number}`;
        const badge = document.createElement('span');
        badge.className = `badge ${stateStyle(wave.state)}`;
        badge.textContent = wave.state;
        top.append(label, badge);

        const objective = document.createElement('span');
        objective.className = 'wave-objective';
        objective.textContent = wave.objective;

        const facts = document.createElement('span');
        facts.className = 'wave-facts';
        const changed = wave.changedFiles.filter(file => file.category === 'application').length;
        facts.textContent = [
            formatDuration(wave.startedAt, wave.completedAt),
            wave.state === 'Active' ? null : `${changed} app file${changed === 1 ? '' : 's'}`,
            wave.validationState ? `tests ${wave.validationState.toLowerCase()}` : null
        ].filter(Boolean).join(' · ');

        button.append(top, objective, facts);
        button.addEventListener('click', () => selectWave(wave.number).catch(showError));
        item.append(button);
        waveList.append(item);
    }
    if (focusedWave) {
        waveList.querySelector(`[data-wave="${focusedWave}"]`)?.focus({ preventScroll: true });
    }
}

function renderWaveMeta(wave) {
    renderLiveProgress();
    if (!wave) {
        evidenceHeading.textContent = workBusy ? 'Current wave' : 'No wave selected';
        waveMeta.textContent = workBusy ? 'Streaming live evidence…' : 'Send a request to start the first wave.';
        return;
    }

    evidenceHeading.textContent = `Wave ${wave.number}`;
    const parts = [
        wave.state,
        `${wave.state === 'Active' ? 'running for' : 'took'} ${formatDuration(wave.startedAt, wave.completedAt)}`,
        new Date(wave.startedAt).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' }),
        wave.commitSha ? `commit ${wave.commitSha.substring(0, 7)}` : null,
        wave.specialists.length ? wave.specialists.join(', ') : null
    ];
    waveMeta.textContent = parts.filter(Boolean).join(' · ');
}

async function refreshWaves() {
    const response = await fetch('/waves');
    if (!response.ok) {
        return;
    }

    waves = await response.json();
    renderWaveList();
    const current = waves.find(wave => wave.number === selectedWave);
    if (current) {
        renderWaveMeta(current);
    }
}

async function selectWave(number) {
    const generation = ++selectionGeneration;
    const active = waves.find(wave => wave.number === number)?.state === 'Active';
    selectedWave = number;
    renderWaveList();
    if (active && workBusy) {
        liveWave = true;
        renderWaveMeta(waves.find(wave => wave.number === number));
        workConversation.replaceChildren(...liveNodes);
        workConversation.scrollTop = workConversation.scrollHeight;
        clearEvidenceLists();
        evidenceSequence = 0;
        await refreshLiveEvidence();
        return;
    }

    liveWave = false;
    const response = await fetch(`/waves/${number}`);
    if (generation !== selectionGeneration) {
        return;
    }
    if (!response.ok) {
        throw new Error(`Wave ${number} could not be loaded.`);
    }

    const detail = await response.json();
    if (generation !== selectionGeneration) {
        return;
    }
    renderWaveMeta(detail.summary);
    workConversation.replaceChildren();
    appendMessage(workConversation, 'user', detail.summary.objective);
    if (detail.response) {
        appendMessage(workConversation, 'assistant', detail.response);
    }
    if (detail.summary.message && detail.summary.state !== 'Completed') {
        appendMessage(workConversation, 'system', `${detail.summary.state}: ${detail.summary.message}`);
    }
    clearEvidenceLists();
    for (const item of detail.evidence) {
        renderEvidence(item);
    }
}

// ---------- Work ----------

async function sendWorkRequest() {
    const text = workRequest.value.trim();
    if (!text) {
        showError('Describe the work before sending a request.');
        workRequest.focus();
        return;
    }

    alert.textContent = '';
    setStatus('Active');
    setOutcome('Busy', 'busy');
    workBusy = true;
    sendWork.disabled = true;
    sendWork.textContent = 'Working…';
    cancelWork.disabled = false;
    workRequest.disabled = true;
    workHeading.textContent = 'Wave in progress';

    ++selectionGeneration;
    selectedWave = null;
    liveWave = true;
    workConversation.replaceChildren();
    clearEvidenceLists();
    evidenceSequence = 0;
    renderWaveMeta(null);
    clearInterval(evidenceTimer);
    evidenceTimer = setInterval(() => {
        if (liveWave) {
            refreshLiveEvidence().catch(showError);
        }
    }, 250);
    clearInterval(wavesTimer);
    wavesTimer = setInterval(async () => {
        await refreshWaves();
        const active = waves.find(wave => wave.state === 'Active');
        if (active && selectedWave === null && liveWave) {
            selectedWave = active.number;
            renderWaveList();
            renderWaveMeta(active);
        }
    }, 1000);

    try {
        await streamAgentTurn(text, 'work', workConversation);
        workRequest.value = '';
    } catch (error) {
        showError(error);
        setOutcome(error.message.startsWith('409 ') ? 'Busy' : 'Failed', error.message.startsWith('409 ') ? 'busy' : 'failure');
    } finally {
        workBusy = false;
        clearInterval(evidenceTimer);
        clearInterval(wavesTimer);
        evidenceTimer = null;
        cancelWork.disabled = true;
        sendWork.textContent = 'Send request';
        workHeading.textContent = 'Next wave';
        if (liveWave) {
            await refreshLiveEvidence();
        }
        await refreshWaves();
        if (canTakeRequests) {
            workRequest.disabled = false;
        }
        await refreshWorkStatus();
        const latest = waves.at(-1);
        if (latest && liveWave && latest.state !== 'Active') {
            await selectWave(latest.number);
        }
        await refreshArtifacts();
        if (!workRequest.disabled) {
            workRequest.focus();
        }
    }
}

async function cancelActiveWork() {
    cancelWork.disabled = true;
    const response = await fetch('/work/cancel', { method: 'POST' });
    const result = await response.json();
    if (result.requested) {
        setStatus(result.state);
        setOutcome('Cancelled', 'cancelled');
    }
}

async function refreshWorkStatus() {
    const response = await fetch('/work/status');
    if (!response.ok) {
        return;
    }

    const result = await response.json();
    setStatus(result.state);
    if (result.isBusy) {
        setOutcome('Busy', 'busy');
    }
    if (result.message && result.terminalResult) {
        alert.textContent = result.terminalResult === 'Completed'
            ? ''
            : `${result.terminalResult}: ${result.message}`;
        if (result.terminalResult === 'Completed') {
            setOutcome('Success', 'success');
        } else if (result.terminalResult === 'Failed') {
            setOutcome('Failed', 'failure');
        } else if (result.terminalResult === 'Cancelled') {
            setOutcome('Cancelled', 'cancelled');
        }
    }

    if (result.state === 'Ready') {
        setReadyForWork(true);
    }
    cancelWork.disabled = !result.isBusy || result.state === 'Cancelling';
    sendWork.disabled = result.isBusy || workRequest.disabled;
}

// ---------- Artifacts ----------

function buildTree(entries) {
    const root = { folders: new Map(), files: [] };
    for (const entry of entries) {
        const parts = entry.path.split('/');
        let node = root;
        for (const part of parts.slice(0, -1)) {
            if (!node.folders.has(part)) {
                node.folders.set(part, { folders: new Map(), files: [] });
            }
            node = node.folders.get(part);
        }
        node.files.push({ ...entry, name: parts.at(-1) });
    }
    return root;
}

function renderTreeNode(node, container, depth, expandAll) {
    const folderNames = [...node.folders.keys()].sort((a, b) => a.localeCompare(b));
    for (const name of folderNames) {
        const details = document.createElement('details');
        details.open = expandAll || (depth === 0 && !name.startsWith('.'));
        const summary = document.createElement('summary');
        summary.textContent = `${name}/`;
        details.append(summary);
        const children = document.createElement('div');
        children.className = 'tree-children';
        renderTreeNode(node.folders.get(name), children, depth + 1, expandAll);
        details.append(children);
        container.append(details);
    }

    for (const file of node.files.sort((a, b) => a.name.localeCompare(b.name))) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = `tree-file ${file.kind}`;
        button.textContent = file.name;
        button.title = `${file.path} · ${formatSize(file.size)}`;
        if (file.path === selectedFile) {
            button.setAttribute('aria-current', 'true');
        }
        button.addEventListener('click', () => openFile(file.path).catch(showError));
        container.append(button);
    }
}

function formatSize(bytes) {
    if (bytes < 1024) {
        return `${bytes} B`;
    }
    return bytes < 1024 * 1024 ? `${(bytes / 1024).toFixed(1)} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

function renderTree() {
    const filter = fileFilter.value.trim().toLowerCase();
    const visible = filter ? files.filter(file => file.path.toLowerCase().includes(filter)) : files;
    fileTree.replaceChildren();
    if (visible.length === 0) {
        const empty = document.createElement('p');
        empty.className = 'hint';
        empty.textContent = files.length === 0 ? 'The workspace is empty.' : 'No files match that filter.';
        fileTree.append(empty);
        return;
    }
    renderTreeNode(buildTree(visible), fileTree, 0, Boolean(filter));
}

async function refreshArtifacts() {
    const [filesResponse, previewResponse] = await Promise.all([
        fetch('/workspace/files'),
        fetch('/workspace/preview')
    ]);
    if (filesResponse.ok) {
        files = await filesResponse.json();
        const appFiles = files.filter(file => !file.path.startsWith('.squad/') && !file.path.startsWith('.github/'));
        artifactsSummary.textContent =
            `${files.length} file${files.length === 1 ? '' : 's'} in the workspace · ${appFiles.length} outside Squad's own team folders.`;
        renderTree();
        if (selectedFile && files.some(file => file.path === selectedFile)) {
            await openFile(selectedFile, true);
        }
    }

    if (previewResponse.ok) {
        const preview = await previewResponse.json();
        if (preview.available) {
            const base = preview.url ?? `${location.protocol}//${location.hostname}:${preview.port}`;
            openApp.href = `${base.replace(/\/$/, '')}/`;
            openApp.removeAttribute('aria-disabled');
            openApp.title = `Serves ${preview.root || 'the workspace root'}/index.html from the workspace.`;
        } else {
            openApp.href = '#';
            openApp.setAttribute('aria-disabled', 'true');
            openApp.title = preview.enabled
                ? 'Available once the Squad creates an index.html.'
                : 'App preview is not enabled for this environment.';
        }
    }
}

function escapeHtml(text) {
    return text.replace(/[&<>"']/g, character => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    })[character]);
}

function renderInline(text) {
    return escapeHtml(text)
        .replace(/`([^`]+)`/g, '<code>$1</code>')
        .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
        .replace(/(^|[^*])\*([^*]+)\*/g, '$1<em>$2</em>')
        .replace(/\[([^\]]+)\]\((https?:\/\/[^)\s]+)\)/g, '<a href="$2" target="_blank" rel="noopener">$1</a>');
}

function renderMarkdown(source, { breaks = false } = {}) {
    const html = [];
    let list = null;
    let code = null;
    let paragraph = [];
    const flushParagraph = () => {
        if (paragraph.length) {
            html.push(`<p>${paragraph.map(renderInline).join(breaks ? '<br>' : ' ')}</p>`);
            paragraph = [];
        }
    };
    const closeList = () => {
        if (list) {
            html.push(`</${list}>`);
            list = null;
        }
    };

    const lines = source.split('\n');
    const cells = row => row.trim().replace(/^\|/, '').replace(/\|$/, '').split('|').map(cell => cell.trim());
    for (let index = 0; index < lines.length; index++) {
        const line = lines[index];
        if (code !== null) {
            if (line.startsWith('```')) {
                html.push(`<pre><code>${escapeHtml(code.join('\n'))}</code></pre>`);
                code = null;
            } else {
                code.push(line);
            }
            continue;
        }
        if (line.startsWith('```')) {
            flushParagraph();
            closeList();
            code = [];
            continue;
        }
        if (line.trim().startsWith('|') && /^\s*\|?\s*:?-{3,}/.test(lines[index + 1] ?? '')) {
            flushParagraph();
            closeList();
            const header = cells(line);
            const rows = [];
            index += 2;
            while (index < lines.length && lines[index].trim().startsWith('|')) {
                rows.push(cells(lines[index]));
                index++;
            }
            index--;
            html.push(`<div class="md-table"><table><thead><tr>${header.map(cell => `<th>${renderInline(cell)}</th>`).join('')}</tr></thead><tbody>${rows.map(row => `<tr>${row.map(cell => `<td>${renderInline(cell)}</td>`).join('')}</tr>`).join('')}</tbody></table></div>`);
            continue;
        }
        const heading = /^(#{1,6})\s+(.*)$/.exec(line);
        const bullet = /^\s*[-*]\s+(.*)$/.exec(line);
        const numbered = /^\s*\d+\.\s+(.*)$/.exec(line);
        if (list && /^\s+\S/.test(line) && !bullet && !numbered) {
            html[html.length - 1] = html.at(-1).replace(/<\/li>$/, ` ${renderInline(line.trim())}</li>`);
            continue;
        }
        if (heading) {
            flushParagraph();
            closeList();
            const level = Math.min(heading[1].length + 1, 6);
            html.push(`<h${level}>${renderInline(heading[2])}</h${level}>`);
        } else if (bullet || numbered) {
            flushParagraph();
            const kind = bullet ? 'ul' : 'ol';
            if (list !== kind) {
                closeList();
                html.push(`<${kind}>`);
                list = kind;
            }
            html.push(`<li>${renderInline((bullet ?? numbered)[1])}</li>`);
        } else if (!line.trim()) {
            flushParagraph();
            closeList();
        } else if (line.startsWith('>')) {
            flushParagraph();
            closeList();
            html.push(`<blockquote>${renderInline(line.replace(/^>\s?/, ''))}</blockquote>`);
        } else {
            closeList();
            paragraph.push(line.trim());
        }
    }
    if (code !== null) {
        html.push(`<pre><code>${escapeHtml(code.join('\n'))}</code></pre>`);
    }
    flushParagraph();
    closeList();
    return html.join('\n');
}

function renderCode(text, path = '') {
    const table = document.createElement('div');
    table.className = 'code';
    const lines = text.split('\n');
    if (lines.at(-1) === '') {
        lines.pop();
    }
    const gutter = document.createElement('pre');
    gutter.className = 'gutter';
    gutter.setAttribute('aria-hidden', 'true');
    gutter.textContent = lines.map((_, index) => index + 1).join('\n');
    const body = document.createElement('pre');
    body.className = 'source';
    appendCodeTokens(body, lines.join('\n'), path);
    table.append(gutter, body);
    return table;
}

let currentFileContent = null;

function renderFile() {
    const file = currentFileContent;
    fileBody.replaceChildren();
    const isMarkdown = /\.(md|markdown)$/i.test(file.path);
    toggleSource.hidden = !isMarkdown;
    toggleSource.textContent = showingSource ? 'View rendered' : 'View source';

    if (file.kind === 'image') {
        const image = document.createElement('img');
        image.src = `/workspace/raw?path=${encodeURIComponent(file.path)}`;
        image.alt = file.path;
        image.className = 'preview-image';
        fileBody.append(image);
    } else if (file.kind === 'text' && file.text !== null) {
        if (isMarkdown && !showingSource) {
            const article = document.createElement('article');
            article.className = 'markdown';
            article.innerHTML = renderMarkdown(file.text);
            fileBody.append(article);
        } else {
            fileBody.append(renderCode(file.text, file.path));
        }
        if (file.truncated) {
            const note = document.createElement('p');
            note.className = 'hint';
            note.textContent = 'This file is large, so only the first part is shown. Open raw or download the ZIP for the full file.';
            fileBody.append(note);
        }
    } else {
        const note = document.createElement('p');
        note.className = 'hint';
        note.textContent = 'This is a binary file and cannot be shown inline. Use Open raw or the ZIP download.';
        fileBody.append(note);
    }
}

async function openFile(path, quiet = false) {
    const response = await fetch(`/workspace/file?path=${encodeURIComponent(path)}`);
    if (!response.ok) {
        if (!quiet) {
            throw new Error(`Could not open ${path}.`);
        }
        return;
    }

    if (selectedFile !== path) {
        showingSource = false;
    }
    selectedFile = path;
    currentFileContent = await response.json();
    fileHeading.textContent = `${path} · ${formatSize(currentFileContent.size)}`;
    rawFile.href = `/workspace/raw?path=${encodeURIComponent(path)}`;
    rawFile.hidden = false;
    renderFile();
    for (const button of fileTree.querySelectorAll('.tree-file')) {
        button.toggleAttribute('aria-current', button.title.startsWith(`${path} ·`));
    }
}

// ---------- Publish ----------

function defaultRepositoryName() {
    const objective = (waves[0]?.objective ?? '')
        .replace(/^\s*wave\s*\d+\s*[:.\-–—]\s*/i, '')
        .split(/[.!?\n]/)[0];
    const words = objective.toLowerCase().match(/[a-z0-9]+/g) ?? [];
    const stop = new Set(['a', 'an', 'the', 'and', 'with', 'for', 'to', 'of', 'in', 'build', 'create', 'make', 'that', 'it', 'scaffold', 'add', 'implement', 'small', 'simple', 'new']);
    const slug = words.filter(word => !stop.has(word)).slice(0, 3).join('-');
    const stamp = new Date().toISOString().slice(0, 10).replaceAll('-', '');
    return `${slug || 'squad-app'}-${stamp}`;
}

async function loadOwners() {
    const response = await fetch('/publish/owners');
    const owners = await response.json();
    publishOwner.replaceChildren();
    if (!owners.available) {
        const option = document.createElement('option');
        option.textContent = 'GitHub is not connected';
        publishOwner.append(option);
        publishOwner.disabled = true;
        publishSubmit.disabled = true;
        publishResult.textContent = owners.reason ?? 'No publishing credential is configured on the server.';
        return;
    }

    for (const owner of [owners.login, ...owners.organizations]) {
        const option = document.createElement('option');
        option.value = owner;
        option.textContent = owner === owners.login ? `${owner} (credential account)` : owner;
        publishOwner.append(option);
    }
    publishOwner.disabled = false;
    publishSubmit.disabled = false;
    $('#publish-auth').textContent = `Server-configured GitHub identity: ${owners.login}. This is not an independent browser connection. GitHub permissions determine which destinations allow creation.`;
    ownersLoaded = true;
}

async function openPublish() {
    if (publishing) return;
    const opening = publishForm.hidden;
    publishForm.hidden = !opening;
    togglePublish.setAttribute('aria-expanded', String(opening));
    if (!opening) {
        return;
    }
    if (!publishName.value) {
        publishName.value = defaultRepositoryName();
    }
    publishResult.textContent = '';
    resetPublishReview();
    if (!ownersLoaded) {
        await loadOwners();
    }
    publishName.focus();
}

function resetPublishReview() {
    reviewedPublication = null;
    publishReview.hidden = true;
}

function reviewPublication(event) {
    event.preventDefault();
    if (publishing) return;
    const name = publishName.value.trim();
    if (!/^[A-Za-z0-9._-]{1,100}$/.test(name) || name === '.' || name === '..') {
        publishResult.textContent = 'Use letters, numbers, dots, dashes, or underscores for the repository name.';
        publishName.focus();
        return;
    }
    if (publishOwner.disabled || !publishOwner.value) return;
    reviewedPublication = {
        owner: publishOwner.value,
        name,
        private: true,
        description: waves[0]?.objective?.slice(0, 300) ?? 'Built by Squad on AKS'
    };
    publishDestination.textContent = `Destination: ${reviewedPublication.owner}/${name}. Visibility: private.`;
    publishResult.textContent = '';
    publishReview.hidden = false;
    publishConfirm.focus();
}

async function publish() {
    if (publishing || !reviewedPublication) return;
    const request = reviewedPublication;
    publishing = true;
    publishConfirm.disabled = true;
    publishCancel.disabled = true;
    $('#publish-back').disabled = true;
    publishOwner.disabled = true;
    publishName.disabled = true;
    publishSubmit.disabled = true;
    publishSubmit.textContent = 'Publishing…';
    publishResult.textContent = '';
    try {
        const response = await fetch('/publish', {
            method: 'POST',
            headers: { 'content-type': 'application/json' },
            body: JSON.stringify(request)
        });
        const result = await response.json();
        publishResult.replaceChildren();
        if (response.ok && result.success && result.private === true) {
            publishResult.append('Published to private repository: ');
            const link = document.createElement('a');
            link.href = result.url;
            link.target = '_blank';
            link.rel = 'noopener';
            link.textContent = result.fullName;
            publishResult.append(link);
            publishResult.className = 'publish-result success';
            await refreshArtifacts();
        } else {
            publishResult.textContent = result.message;
            if (result.url) {
                const link = document.createElement('a');
                link.href = result.url;
                link.target = '_blank';
                link.rel = 'noopener';
                link.textContent = result.fullName ?? 'View created repository';
                publishResult.append(' ', link, '. Publication is incomplete. Do not submit again to create another repository; same-repository retry is not yet supported.');
            }
            publishResult.className = 'publish-result failure';
        }
    } catch (error) {
        publishResult.textContent = `Could not confirm publication: ${error.message}. Check GitHub before submitting again; a repository may already have been created.`;
        publishResult.className = 'publish-result failure';
    } finally {
        publishing = false;
        resetPublishReview();
        publishConfirm.disabled = false;
        publishCancel.disabled = false;
        $('#publish-back').disabled = false;
        publishOwner.disabled = false;
        publishName.disabled = false;
        publishSubmit.disabled = false;
        publishSubmit.textContent = 'Review publication';
        publishResult.tabIndex = -1;
        publishResult.focus();
    }
}

// ---------- Wiring ----------

function submitOnShortcut(element, action) {
    element.addEventListener('keydown', event => {
        if (event.key === 'Enter' && (event.metaKey || event.ctrlKey)) {
            event.preventDefault();
            action();
        }
    });
}

fileSelect.addEventListener('change', showSelectedReviewFile);
sendTurn.addEventListener('click', () => sendCastingTurn(false));
confirmCast.addEventListener('click', () => sendCastingTurn(true));
saveReview.addEventListener('click', saveSelectedReviewFile);
activateCast.addEventListener('click', activate);
sendWork.addEventListener('click', sendWorkRequest);
cancelWork.addEventListener('click', cancelActiveWork);
submitOnShortcut(prompt, () => !sendTurn.disabled && sendCastingTurn(false));
submitOnShortcut(workRequest, () => !sendWork.disabled && sendWorkRequest());
fileFilter.addEventListener('input', renderTree);
toggleSource.addEventListener('click', () => {
    showingSource = !showingSource;
    renderFile();
});
openApp.addEventListener('click', event => {
    if (openApp.getAttribute('aria-disabled') === 'true') {
        event.preventDefault();
        showError(openApp.title);
    }
});
togglePublish.addEventListener('click', () => openPublish().catch(error => publishResult.textContent = error.message));
publishCancel.addEventListener('click', () => {
    if (publishing) return;
    resetPublishReview();
    publishForm.hidden = true;
    togglePublish.setAttribute('aria-expanded', 'false');
    togglePublish.focus();
});
publishOwner.addEventListener('change', resetPublishReview);
publishName.addEventListener('input', resetPublishReview);
$('#publish-back').addEventListener('click', () => {
    resetPublishReview();
    publishName.focus();
});
publishForm.addEventListener('submit', reviewPublication);
publishConfirm.addEventListener('click', publish);
window.addEventListener('focus', () => {
    if (!workBusy) {
        refreshWorkStatus().catch(showError);
        refreshArtifacts().catch(showError);
    }
});

setCastingBusy(true);
updateCastingContext();

async function initialize() {
    const castResponse = await fetch('/casting/status', { cache: 'no-store' });
    if (!castResponse.ok) {
        throw new Error(`Workspace status could not be loaded (HTTP ${castResponse.status}).`);
    }
    if (castResponse.ok) {
        const result = await castResponse.json();
        restoreSession(result.generation);
        setStatus(result.state);
        if (result.hasCast) {
            await loadReview();
        }
        setReadyForWork(result.canTakeRequests);
        if (result.canTakeRequests) {
            setup.open = false;
            await loadOriginalIntent();
        }
    }

    await refreshWorkStatus();
    await refreshWaves();
    const active = waves.find(wave => wave.state === 'Active');
    if (active) {
        workBusy = true;
        sendWork.disabled = true;
        cancelWork.disabled = false;
        if (liveWave) {
            await selectWave(active.number);
        }
        evidenceTimer = setInterval(() => liveWave && refreshLiveEvidence().catch(showError), 250);
        wavesTimer = setInterval(async () => {
            await refreshWaves();
            if (!waves.some(wave => wave.state === 'Active')) {
                clearInterval(evidenceTimer);
                clearInterval(wavesTimer);
                workBusy = false;
                await refreshWorkStatus();
                await selectWave(active.number);
                await refreshArtifacts();
            }
        }, 1000);
    } else if (waves.length) {
        await selectWave(waves.at(-1).number);
    } else {
        renderWaveMeta(null);
    }
    await refreshArtifacts();
}

initialize().catch(showError);
