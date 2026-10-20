# Native casting boundary probe

Wave 1 work item W02 stops at the pinned runtime boundary before native Squad
Init Mode can emit an AG-UI `RUN_STARTED` event.

Run the executable proof with:

```bash
./scripts/run-native-casting-probe.sh
```

The script creates a disposable Git repository, installs the coordinator
harness from official Squad commit
`d5076d91ac8d0bcc46c8f9c7b6d82f6774b60581`, requires the real
`.github/agents/squad.agent.md`, and starts an in-process AG-UI HTTP endpoint
backed by `Squad.Agents.AI` 1.0.0. The client is prepared to preserve one
logical conversation by carrying the same AG-UI thread ID and each preceding
run ID through proposal, refinement, confirmation, and a post-cast turn.

The first request currently returns:

```text
RunErrorEvent(StreamingError: An error occurred while streaming the agent response.)
```

The probe then invokes the same `SquadAgent` directly to expose the swallowed
inner failure:

```text
System.IO.IOException: CLI process exited unexpectedly.
stderr: copilot-runtime: unsupported argument '--allow-all'
```

`Squad.Agents.AI` 1.0.0 pins GitHub Copilot SDK 1.0.14, whose build assets
download Copilot CLI 1.0.85 for this project. The adapter supplies
`--allow-all`, but that runtime rejects the argument and exits before a MAF
session can stream. Therefore proposal-before-write, refinement, confirmation,
native file creation, and the post-cast turn cannot be exercised through the
pinned stack.

Per #5, Wave 1 must stop here. The probe does not add custom role generation,
custom casting state, a second team schema, or an out-of-band confirmation API.
