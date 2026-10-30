# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

Everyday people with an idea, not developers: a club organizer who wants a sign-up app, a shop owner who wants a stock tracker, a teacher who wants a classroom game, a kid who wants to build Breakout. They arrive with an intent in their own words and no wish to read source code, logs, diffs, or infrastructure. Their job is to describe what they imagine, meet the team that will build it, watch it come to life in rounds, try it, and keep it.

## Product Purpose

An imagination tool: people prompt their apps into existence. The person describes an idea; a team of AI agents is assembled to fit that idea; the team delivers it in waves, each wave a bounded request with plain-language proof it was delivered; finished apps run alongside the builder and collect in a personal library of everything the person has made. Think PowerApps or Lotus Notes, a normal person's app suite, but built by agents from imagination.

Success: a non-technical person goes from "I wish I had…" to a running app they can open, share, and keep, without ever seeing code or infrastructure.

## Positioning

Not a coding assistant with a chat box and a code pane. The person never manages files; they manage a team and an idea. Each app gets its own team cast for that intent, and every round of work is a visible wave the person can revisit. Apps live where the builder lives, ready to open.

## Operating Context

- Used in a browser on a laptop or phone, at home or at work, in short sessions between other things.
- Core loop: describe an idea → meet the proposed team, adjust it in conversation → cast the team → ask for a wave of work → watch the team work → see proof and try the app → ask for the next wave.
- Keeping and sharing: open the running app, download it, publish it to GitHub (a new repository the person owns or an organization they belong to).
- People who do want code go to GitHub; the product does not show code.

## Capabilities and Constraints

Confirmed (existing backend, must remain available in the new UI):
- Propose a team from a description, refine it conversationally, confirm it, activate it (`/ag-ui`, `/casting/*`).
- Send a wave request, cancel the active wave, stream progress and evidence, reopen past waves (`/work/*`, `/waves`).
- Open the live app preview (`/workspace/preview`), download the app as a ZIP (`/workspace/export.zip`), publish to a new GitHub repository with an owner picker (`/publish`, `/publish/owners`).

Product rules:
- No code display anywhere: no diffs, source views, file trees, raw Markdown, test logs, commit SHAs, or command output. Evidence is translated into plain language (what was asked, what changed in terms the person cares about, whether it works, try it).
- The person never needs to code, know what AKS, instances, or containers are, or know how to use git. Kubernetes, AKS, Aspire, containers, instances, git vocabulary (commit, branch, repository internals) are implementation details and never appear in the interface. The agent team is called "your squad" (owner-chosen word; "Squad on AKS" is never shown).
- GitHub is also an implementation detail, but the publish action is labelled with GitHub by name (e.g. "Publish to GitHub") so people recognize where their app goes; no git concepts appear around it. The team is "your team"; members are named people with roles.
- Waves stay: each request is a persistent, reopenable wave with its objective, outcome, and evidence.

Open decisions (inferred, not yet confirmed with the owner):
- Multi-app library: the backend currently hosts one app per workspace. The UI presents a library of apps; until multiple workspaces exist, the library honestly shows the apps that exist (one) plus "Dream up a new app" as an explained, not-yet-available action.
- Per-app deployment into the cluster alongside the builder is planned backend work, not yet implemented.

## Brand Commitments

- Not called "Squad on AKS". Product name: **Daydream** (owner-confirmed).
- Voice: warm, encouraging, imaginative, plain words. Never robotic, never jargon. The person is the dreamer; the team are the makers.
- Owner direction, verbatim: "this isn't a developer tool. this is an imagination tool. people should prompt their apps into existence." "don't just be robotic."

## Evidence on Hand

- Real runs produced working apps (Pong, Breakout) that open in the preview.
- No customers, testimonials, metrics, or pricing exist; do not invent them.

## Product Principles

1. Imagination first: every screen starts from the person's idea, in their words.
2. People, not plumbing: show the team and what they did, never the machinery.
3. Proof you can touch: the best evidence is the app itself, open and playable.
4. Every wave is a chapter: work is a story the person can reread.
5. It's theirs: easy to open, keep, download, and publish what they made.

## Accessibility & Inclusion

WCAG 2.2 AA. Fully keyboard operable, responsive from phone to wide desktop, reduced-motion respected, plain-language copy suitable for non-technical and younger users.
