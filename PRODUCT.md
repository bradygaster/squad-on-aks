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
  presentation changes. Internal Daydream storage and endpoint names stay stable.

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
- Preview, ZIP export, existing online publishing and private GitHub repository
  publication remain separate actions. The installation's shared GitHub account
  is identified honestly; this is not browser-based GitHub authentication.
- Each app owns its squad and history. Kubernetes namespace/worker boundaries
  and supported architectures are unchanged by the UI.

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

Recorded local Kubernetes eight-ball proof includes two real Squad waves and
build-stage restart recovery. Browser regression fixtures prove UI behavior,
not real-model execution or production hosting. AKS acceptance is outstanding.
No invented customer, performance or reliability claims.

## Accessibility & Inclusion

WCAG 2.2 AA target. Keyboard navigation, persistent input labels, inline errors,
native-dialog focus/Escape behavior, responsive document flow and reduced-motion
support. Team-load failures are visible without disabling app work.
