const state = document.querySelector('[role="status"]');
const alert = document.querySelector('[role="alert"]');
const conversation = document.querySelector('[aria-label="Conversation stream"]');
const activity = document.querySelector('[aria-label="Specialist activity"]');
const harnessEvidence = document.querySelector('[aria-label="Harness evidence"]');
const applicationEvidence = document.querySelector('[aria-label="Application evidence"]');
const threadId = crypto.randomUUID();

function append(list, text) {
    const item = document.createElement('li');
    item.textContent = text;
    list.append(item);
}

async function initialize() {
    const response = await fetch('/harness/state');
    const payload = await response.json();
    state.textContent = payload.state === 'Starting' ? 'Needs cast' : payload.state;
}

async function runAgUi(action) {
    alert.textContent = '';
    const prompt = action === 'refine'
        ? document.querySelector('#refinement').value
        : document.querySelector('#work-prompt').value;
    const response = await fetch('/ag-ui', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({
            threadId,
            runId: crypto.randomUUID(),
            messages: [{
                id: crypto.randomUUID(),
                role: 'user',
                content: prompt
            }],
            state: {},
            tools: [],
            context: [],
            forwardedProps: { action }
        })
    });

    if (!response.ok) {
        const failure = await response.json();
        alert.textContent = `${response.status} ${response.statusText}: ${failure.message}`;
        return;
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

            renderEvent(JSON.parse(data.substring(6)));
        }
    }
}

function renderEvent(item) {
    if (item.type === 'TEXT_MESSAGE_CONTENT') {
        append(conversation, item.delta);
    } else if (item.type === 'CUSTOM' && item.name === 'state') {
        state.textContent = item.value.state;
    } else if (item.type === 'CUSTOM' && item.name === 'activity') {
        append(activity, `${item.value.actor}: ${item.value.text}`);
    } else if (item.type === 'CUSTOM' && item.name === 'evidence') {
        const list = item.value.category === 'harness' ? harnessEvidence : applicationEvidence;
        append(list, item.value.path ? `${item.value.text}: ${item.value.path}` : item.value.text);
    }
}

document.querySelectorAll('[data-action]').forEach(button => {
    button.addEventListener('click', () => runAgUi(button.dataset.action));
});

document.querySelector('#save-file').addEventListener('click', async () => {
    const response = await fetch('/harness/files', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({
            path: document.querySelector('#file-path').value,
            content: document.querySelector('#file-content').value
        })
    });
    const payload = await response.json();
    (response.ok ? conversation : alert).textContent = payload.message;
});

document.querySelector('#make-malformed').addEventListener('click', async () => {
    await fetch('/harness/malformed', { method: 'POST' });
    state.textContent = 'Cast review';
});

document.querySelector('#activate').addEventListener('click', async () => {
    const response = await fetch('/harness/activate', { method: 'POST' });
    const payload = await response.json();
    if (response.ok) {
        state.textContent = payload.state;
        alert.textContent = '';
    } else {
        alert.textContent = payload.message;
    }
});

document.querySelector('#cancel').addEventListener('click', async () => {
    await fetch('/harness/cancel', { method: 'POST' });
});

initialize();
