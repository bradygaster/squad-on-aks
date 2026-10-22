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

const threadId = crypto.randomUUID();
const messages = [];
const reviewFiles = new Map();
let previousRunId = null;
let turnCount = 0;

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
    sendWork.disabled = true;
    cancelWork.disabled = false;

    try {
        await streamAgentTurn(text, 'work');
        workRequest.value = '';
    } catch (error) {
        alert.textContent = error.message;
    } finally {
        cancelWork.disabled = true;
        await refreshWorkStatus();
    }
}

async function cancelActiveWork() {
    const response = await fetch('/work/cancel', { method: 'POST' });
    const result = await response.json();
    if (result.requested) {
        status.textContent = result.state;
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
        for (const error of result.errors) {
            const item = document.createElement('li');
            item.textContent = error;
            validationErrors.append(item);
        }
        return;
    }

    status.textContent = result.state;
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
