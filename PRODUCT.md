# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

People building apps with an AI team. They need to describe the goal, see who owns
the work, understand the result and request the next change. Familiar GitHub
product conventions should make the workspace understandable without requiring
knowledge of its hosting infrastructure.

## Product Purpose

A workspace for building apps with Squad. Each app has its own team, saved brief,
reopenable work waves and preview. The interface makes the team and work
informative without turning team configuration into a separate user task.

Success: describe an app, build from that same brief, inspect a working result,
understand who contributed and continue in another wave.

## Operating Context

- Browser on desktop or phone; system light/dark preference with a manual override.
- Root is an explicit app list. Opening root never selects an app or starts work.
- Describe the app → clarify if needed → **Build my app** → review the preview,
  results and contributors → request another wave.
- Team creation and activation remain internal. No roster approval or second
  "go build it" prompt.
- Existing saved apps, squads, waves and browser conversations must survive
  presentation changes. Internal storage and endpoint names stay stable.

## Capabilities and Constraints

- Native Squad proposal, refinement, confirmation and activation through existing
  `/ag-ui` and `/casting/*` endpoints.
- Per-app team names and roles come from native team/charter files through the
  read-only review endpoint. Initials are typographic markers, not real avatars.
- Contributors come from recorded wave evidence. Do not invent activity,
  presence, progress percentages or member assignments.
- Persistent wave requests, cancellation, terminal outcomes, validation and
  changed-file counts remain available. No raw transcripts, source browsers,
  diffs, command output, credentials or infrastructure diagnostics in the UI.
  The exception is deliberate and on demand: each app's **Topology** section
  (Aspire and cluster views, drawn from the app contract and rendered manifests,
  never secret values; live workload status from replica counts and Kubernetes
  reason codes, never logs or messages) and its **What's happening** deployment
  steps. The rest of
  the page stays in plain app/team/wave language.
  Activity may show short dispatch descriptions, requested tool names and
  relative changed-file paths, but never assistant reasoning or file contents.
- Preview, ZIP export, existing online publishing and private GitHub repository
  publication remain separate actions. The installation's shared GitHub account
  is identified honestly; this is not browser-based GitHub authentication.
- Each app owns its squad and history. Every app is an Aspire app with a front
  end, an API and a PostgreSQL database, deployed into its own namespace and
  updated in place after later waves. There is no built-in demo app; new apps
  start from a description, an example idea, or **Surprise me**.
- Build validation and running-app acceptance are distinct. Predictive blockers
  and actual Playwright/persistence phases are recorded, not inferred from a
  team's success claim. Server apps open only their verified managed runtime;
  a static landing page is not evidence of a functioning backend.

## Brand Commitments

- **Squad** returns as the application identity. Use it sparingly; prefer "app",
  "team" and "wave" in task copy.
- GitHub-native product styling rather than the Daydream neighborhood/mural
  metaphor: restrained surfaces, compact type, clear status and familiar controls.
- Owner direction: "make sure the experience is a little more informative and
  that it highlights the team members, without maybe 'squad' everywhere all the
  time"; "more native github and less fluffy app builder tool."
- Requested reference repositories: `github/takeoff` and `github/octave`.
  This session could not inspect them because organization SAML authorization
  was unavailable. Public GitHub Primer product foundations informed the styling;
  do not claim visual parity with those repositories or official affiliation.

## Evidence on Hand

The contract's Aspire app shape publishes with Aspire 13.6.5 and its rendered
manifests pass a server-side dry run on Docker Desktop Kubernetes. Browser
regression fixtures prove UI behavior, not real-model execution or production
hosting. A real-model build, deploy and in-place update on a cluster, and AKS
acceptance, are outstanding.
No invented customer, performance or reliability claims.

## Accessibility & Inclusion

WCAG 2.2 AA target. Keyboard navigation, persistent input labels, inline errors,
native-dialog focus/Escape behavior, responsive document flow and reduced-motion
support. Team-load failures are visible without disabling app work.
