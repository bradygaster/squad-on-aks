const status = document.querySelector('[role="status"]');
const alert = document.querySelector('[role="alert"]');
const prompt = document.querySelector('#casting-prompt');
const sendTurn = document.querySelector('#send-casting-turn');
const confirmCast = document.querySelector('#confirm-cast');
const conversation = document.querySelector('[aria-label="Cast conversation"]');
const fileSelect = document.querySelector('#review-file');
const fileContent = document.querySelector('#review-content');
const saveReview = document.querySelector('#save-review');
const activateCast = document.querySelector('#activate-cast');
const validationErrors = document.querySelector('[aria-label="Validation errors"]');
const workRequest = document.querySelector('#work-request');
const sendWork = document.querySelector('#send-work');
const cancelWork = document.querySelector('#cancel-work');
const specialistActivity = document.querySelector('[aria-label="Specialist activity"]');
const applicationEvidence = document.querySelector('[aria-label="Application evidence"]');
const harnessEvidence = document.querySelector('[aria-label="Harness evidence"]');
const validationEvidence = document.querySelector('[aria-label="Validation evidence"]');
const outcome = document.querySelector('#outcome-indicator');

const threadId = crypto.randomUUID();
const messages = [];
const reviewFiles = new Map();
let previousRunId = null;
let turnCount = 0;
let evidenceSequence = 0;
let evidenceTimer = null;

function setOutcome(value, style = 'neutral') {
    outcome.textContent = value;
    outcome.className = `outcome ${style}`;
}

function clearEvidence() {
    evidenceSequence = 0;
    specialistActivity.replaceChildren();
    applicationEvidence.replaceChildren();
    harnessEvidence.replaceChildren();
    validationEvidence.replaceChildren();
}

function appendEvidence(list, item, title, detail) {
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
        const pre = document.createElement('pre');
        pre.textContent = body;
        row.append(pre);
    }
    list.append(row);
}

function renderEvidence(item) {
    if (item.kind === 'specialist' || item.kind === 'state') {
        appendEvidence(
            specialistActivity,
            item,
            `${item.actor ? `${item.actor} · ` : ''}${item.state}`,
            [item.summary, item.source].filter(Boolean).join(' · '));
    } else if (item.kind === 'application') {
        appendEvidence(applicationEvidence, item, item.path, item.source);
    } else if (item.kind === 'harness') {
        appendEvidence(harnessEvidence, item, item.path, item.source);
    } else if (item.kind === 'validation') {
        appendEvidence(
            validationEvidence,
            item,
            `${item.command} · exit ${item.exitCode}`,
            item.state);
    }
}

async function refreshEvidence() {
    const response = await fetch(`/work/evidence?after=${evidenceSequence}`);
    if (!response.ok) {
        return;
    }

    const snapshot = await response.json();
    for (const item of snapshot.items) {
        renderEvidence(item);
    }
    evidenceSequence = snapshot.lastSequence;
}

function startEvidenceStream() {
    clearEvidence();
    clearInterval(evidenceTimer);
    evidenceTimer = setInterval(() => {
        refreshEvidence().catch(error => alert.textContent = error.message);
    }, 50);
}

async function stopEvidenceStream() {
    clearInterval(evidenceTimer);
    evidenceTimer = null;
    await refreshEvidence();
}

function appendMessage(role, text) {
    const item = document.createElement('li');
    item.className = role;
    item.textContent = text;
    conversation.append(item);
    conversation.scrollTop = conversation.scrollHeight;
    return item;
}

function setCastingBusy(busy) {
    sendTurn.disabled = busy;
    confirmCast.disabled = busy || turnCount === 0;
    sendTurn.textContent = busy ? 'Streaming…' : (turnCount === 0 ? 'Propose cast' : 'Refine proposal');
}

async function streamAgentTurn(text, phase) {
    appendMessage('user', text);
    const userMessage = { id: crypto.randomUUID(), role: 'user', content: text };
    messages.push(userMessage);
    const runId = crypto.randomUUID();
    const assistantItem = appendMessage('assistant', '');
    let assistantText = '';

    const response = await fetch('/ag-ui', {
        method: 'POST',
        headers: {
            'content-type': 'application/json',
            'X-Squad-Phase': phase
        },
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
        messages.pop();
        throw new Error(`${response.status} ${response.statusText}: ${await response.text()}`);
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
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
                assistantText += event.delta ?? '';
                assistantItem.textContent = assistantText;
            } else if (event.type === 'RUN_ERROR') {
                throw new Error(event.message ?? 'Native Squad run failed.');
            }
        }
    }

    if (assistantText.trim()) {
        messages.push({ id: crypto.randomUUID(), role: 'assistant', content: assistantText });
    }

    return assistantText;
}

async function sendCastingTurn(isConfirmation = false) {
    const text = isConfirmation
        ? 'I explicitly confirm this proposal. Create the Squad now using the native Init Mode confirmation path.'
        : prompt.value.trim();
    if (!text) {
        alert.textContent = 'Describe what the Squad will do before sending a turn.';
        return;
    }

    alert.textContent = '';
    validationErrors.replaceChildren();
    setCastingBusy(true);

    try {
        const assistantText = await streamAgentTurn(
            text,
            isConfirmation ? 'cast-review' : 'casting');
        if (!assistantText.trim()) {
            throw new Error('Native Squad returned no streamed proposal text.');
        }

        turnCount += 1;
        prompt.value = '';
        status.textContent = isConfirmation ? 'Cast review' : 'Casting';
        if (isConfirmation) {
            await loadReview();
        }
    } catch (error) {
        alert.textContent = error.message;
    } finally {
        setCastingBusy(false);
    }
}

async function sendWorkRequest() {
    const text = workRequest.value.trim();
    if (!text) {
        alert.textContent = 'Describe the work before sending a request.';
        return;
    }

    alert.textContent = '';
    status.textContent = 'Active';
    setOutcome('Busy', 'busy');
    sendWork.disabled = true;
    cancelWork.disabled = false;
    startEvidenceStream();

    try {
        await streamAgentTurn(text, 'work');
        workRequest.value = '';
    } catch (error) {
        alert.textContent = error.message;
        if (error.message.startsWith('409 ')) {
            setOutcome('Busy', 'busy');
        } else {
            setOutcome('Failed', 'failure');
        }
    } finally {
        cancelWork.disabled = true;
        await stopEvidenceStream();
        await refreshWorkStatus();
    }
}

async function cancelActiveWork() {
    const response = await fetch('/work/cancel', { method: 'POST' });
    const result = await response.json();
    if (result.requested) {
        status.textContent = result.state;
        setOutcome('Cancelled', 'cancelled');
        cancelWork.disabled = true;
    }
}

async function refreshWorkStatus() {
    const response = await fetch('/work/status');
    if (!response.ok) {
        return;
    }

    const result = await response.json();
    status.textContent = result.state;
    if (result.message && result.terminalResult) {
        alert.textContent = `${result.terminalResult}: ${result.message}`;
        if (result.terminalResult === 'Completed') {
            setOutcome('Success', 'success');
        } else if (result.terminalResult === 'Failed') {
            setOutcome('Failed', 'failure');
        } else if (result.terminalResult === 'Cancelled') {
            setOutcome('Cancelled', 'cancelled');
        }
    }

    if (result.state === 'Ready') {
        workRequest.disabled = false;
    }
    cancelWork.disabled = !result.isBusy || result.state === 'Cancelling';
    sendWork.disabled = result.isBusy || workRequest.disabled;
}

async function loadReview() {
    const response = await fetch('/casting/review');
    if (!response.ok) {
        throw new Error(await response.text());
    }

    const files = await response.json();
    reviewFiles.clear();
    fileSelect.replaceChildren();
    for (const file of files) {
        reviewFiles.set(file.path, file.content);
        const option = document.createElement('option');
        option.value = file.path;
        option.textContent = file.path;
        fileSelect.append(option);
    }

    const enabled = files.length > 0;
    fileSelect.disabled = !enabled;
    fileContent.disabled = !enabled;
    saveReview.disabled = !enabled;
    activateCast.disabled = !enabled;
    showSelectedFile();
}

function showSelectedFile() {
    fileContent.value = reviewFiles.get(fileSelect.value) ?? '';
}

async function saveSelectedFile() {
    const response = await fetch('/casting/review', {
        method: 'PUT',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ path: fileSelect.value, content: fileContent.value })
    });
    const result = await response.json();
    if (!response.ok) {
        alert.textContent = result.message;
        return;
    }

    reviewFiles.set(fileSelect.value, fileContent.value);
    alert.textContent = '';
}

async function activate() {
    const response = await fetch('/casting/activate', { method: 'POST' });
    const result = await response.json();
    validationErrors.replaceChildren();
    if (!response.ok) {
        status.textContent = 'Cast review';
        setOutcome('Blocked', 'blocked');
        for (const error of result.errors) {
            const item = document.createElement('li');
            item.textContent = error;
            validationErrors.append(item);
        }
        return;
    }

    status.textContent = result.state;
    setOutcome('Ready', 'neutral');
    workRequest.disabled = false;
    sendWork.disabled = false;
}

fileSelect.addEventListener('change', showSelectedFile);
sendTurn.addEventListener('click', () => sendCastingTurn(false));
confirmCast.addEventListener('click', () => sendCastingTurn(true));
saveReview.addEventListener('click', saveSelectedFile);
activateCast.addEventListener('click', activate);
sendWork.addEventListener('click', sendWorkRequest);
cancelWork.addEventListener('click', cancelActiveWork);
window.addEventListener('online', refreshWorkStatus);
window.addEventListener('focus', refreshWorkStatus);

fetch('/casting/status')
    .then(response => {
        if (!response.ok) {
            return null;
        }

        return response.json();
    })
    .then(result => {
        if (!result) {
            return;
        }

        status.textContent = result.state;
        if (result.hasCast) {
            loadReview().catch(error => alert.textContent = error.message);
        }
        workRequest.disabled = !result.canTakeRequests;
        sendWork.disabled = !result.canTakeRequests;
    })
    .catch(error => alert.textContent = error.message);

refreshWorkStatus().catch(error => alert.textContent = error.message);
