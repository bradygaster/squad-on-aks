# Route planner fixture

The route planner must print mission phases in chronological order:

```text
LAUNCH
ORBIT
LAND
```

Run `sh tests/run.sh`. The packaged fixture intentionally reverses the phases,
so the test fails until `src/flight-plan.sh` is corrected. That source file is
the only expected application change.

The fixture also vendors the official Squad coordinator and Init Mode skill
from `bradygaster/squad` commit
`d5076d91ac8d0bcc46c8f9c7b6d82f6774b60581`. Those bootstrap files live under
`.github/`, so the initial workspace has no `.squad` directory, roster, routing,
agents, or casting state. Runtime reset never needs repository or network
access.
