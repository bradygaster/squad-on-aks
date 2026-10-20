# Native casting boundary probe

Wave 1 work item W02 proves that Squad native Init Mode survives
`Squad.Agents.AI`, Microsoft Agent Framework, AG-UI hosting, and the AG-UI
client without a second casting engine.

Run the executable proof with:

```bash
./scripts/run-native-casting-probe.sh
```

The script creates a disposable Git repository, installs the coordinator
harness from official Squad commit
`d5076d91ac8d0bcc46c8f9c7b6d82f6774b60581`, requires the real
`.github/agents/squad.agent.md`, and starts an in-process AG-UI HTTP endpoint
backed by `Squad.Agents.AI` 1.0.0. It explicitly selects the `squad` agent and
uses the installed Copilot CLI from `PATH` with `--yolo`. Set
`COPILOT_CLI_PATH` to use another executable.

The explicit CLI path is required because GitHub Copilot SDK 1.0.14 otherwise
downloads Copilot CLI 1.0.85, which predates the `--agent` and `--allow-all`
arguments required by `SquadAgent`. The validated local CLI is 1.0.99-2.

The probe preserves one logical conversation by carrying the same AG-UI thread
ID and each preceding run ID through:

1. a streamed native cast proposal;
2. one conversational refinement;
3. explicit confirmation;
4. one post-cast Squad turn.

Proposal and refinement assert that Squad roster, routing, charter, history,
and casting-state files remain unchanged. Copilot session databases and lock
files are runtime state, not native team files, and are intentionally excluded
from that assertion. Confirmation then proves that Squad writes its native
roster, routing, charters, histories, and casting state before the final turn
succeeds through the same `SquadAgent`.

The probe does not add custom role generation, custom casting state, a second
team schema, or an out-of-band confirmation API.
