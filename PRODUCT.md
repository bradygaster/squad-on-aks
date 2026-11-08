# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

Everyday people with an idea, not developers: a club organizer who wants a sign-up app, a shop owner who wants a stock tracker, a teacher who wants a classroom game, a kid who wants to build Breakout. They arrive with an intent in their own words and no wish to read source code, logs, diffs, or infrastructure. Their job is to describe what they imagine, clarify what the app should do, watch it come to life in rounds, try it, and keep it.

## Product Purpose

An imagination tool: people prompt their apps into existence. The person describes an idea; a team of AI agents is assembled to fit that idea; the team delivers it in waves, each wave a bounded request with plain-language proof it was delivered; finished apps run alongside the builder and collect in a personal library of everything the person has made. Think PowerApps or Lotus Notes, a normal person's app suite, but built by agents from imagination.

Success: a non-technical person goes from "I wish I had…" to a running app they can open, share, and keep, without ever seeing code or infrastructure.

## Positioning

Not a coding assistant with a chat box and a code pane. The person manages an idea, not files or a team. Each app gets its own internally organized team with clear ownership and validation responsibility; that setup is not a user-facing approval step. Every round of work is a visible wave the person can revisit. Apps live where the builder lives, ready to open.

## Operating Context

- Used in a browser on a laptop or phone, at home or at work, in short sessions between other things.
- Entry: root is "Your apps," a neighborhood overview with each app's status, latest request, and time. Opening an app is explicit; root never selects one or starts work.
- Core loop: describe an idea → answer an essential app question or add detail if needed → choose "Build my app" → see proof and try the app → request another wave. The saved brief starts the first wave; the person does not retype it or approve an internal team.
- Keeping and sharing: open the running app, download it, publish it to GitHub (a new repository the person owns or an organization they belong to).
- People who do want code go to GitHub; the product does not show code.

## Capabilities and Constraints

Confirmed (existing backend, must remain available in the new UI):
- Propose a team from a description, refine it conversationally, confirm it, activate it (`/ag-ui`, `/casting/*`).
- Send a wave request, cancel the active wave, stream progress and evidence, reopen past waves (`/work/*`, `/waves`).
- Open the live app preview (`/workspace/preview`), download the app as a ZIP (`/workspace/export.zip`), publish to a new GitHub repository with an owner picker (`/publish`, `/publish/owners`).

Product rules:
- No code display anywhere: no diffs, source views, file trees, raw Markdown, test logs, commit SHAs, or command output. Evidence is translated into plain language (what was asked, what changed in terms the person cares about, whether it works, try it).
- The person never needs to code, know what AKS, instances, or containers are, or know how to use git. Kubernetes, AKS, Aspire, containers, git vocabulary, Squad branding, team rosters and casting mechanics are implementation details and never appear in the interface.
- GitHub publication is labelled by name (e.g. "Publish to GitHub") so people recognize where their app goes; no git concepts appear around it.
- Waves stay: each request is a persistent, reopenable wave with its objective, outcome, and evidence.

Multi-app library:
- "Dream up something new" starts another app. Each app keeps its own internal squad, reopenable waves, preview, download and GitHub publication. Return via the Daydream brand or "Back to the neighborhood"; do not duplicate the library within an app.
- Switching library entries restores that app; other apps and active work stay separate.
- When Daydream runs in the cloud, "Put it online" gives each static app a running copy alongside Daydream. Local-only setups explain that this action needs the cloud; trying apps here remains available.

## Brand Commitments

- Not called "Squad on AKS". Product name: **Daydream** (owner-confirmed).
- Voice: warm, encouraging, imaginative, plain words. Never robotic, never jargon. The person is the dreamer; the team are the makers.
- Owner direction, verbatim: "this isn't a developer tool. this is an imagination tool. people should prompt their apps into existence." "don't just be robotic."

## Evidence on Hand

- Real runs produced working apps (Pong, Breakout) that open in the preview.
- No customers, testimonials, metrics, or pricing exist; do not invent them.

## Product Principles

1. Imagination first: every screen starts from the person's idea, in their words.
2. Apps, not plumbing: show what changed for the person, never team mechanics.
3. Proof you can touch: the best evidence is the app itself, open and playable.
4. Every wave is a chapter: work is a story the person can reread.
5. It's theirs: easy to open, keep, download, and publish what they made.

## Accessibility & Inclusion

WCAG 2.2 AA. Fully keyboard operable, responsive from phone to wide desktop, reduced-motion respected, plain-language copy suitable for non-technical and younger users.
