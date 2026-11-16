# GIT_GALATEO.md

Git workflow rules for this repository. Follow these for every change.

## Branching

- Never commit directly to `main`.
- Always branch off `main` before starting work.
- Give every branch a meaningful, descriptive name.

Suggested prefixes:

- `feature/` — new functionality (e.g. `feature/m3-combat-loop`).
- `fix/` — bug fixes.
- `chore/` — maintenance, tooling, dependencies.
- `docs/` — documentation only.

## Commits

- A commit message is **one line**. Never more than one line.
- If a change cannot be explained in a single line, that is a sign the commit should be
  broken down into smaller, focused commits.
- Each commit is one logical change that stands on its own.

Examples of good one-line messages:

- `Add a generic prefab pool for pooled spawns`
- `Add health, damage and contact-damage primitives`
- `Add combat HUD and game-over flow`

## Note

Every time new instructions are given, this file must be updated accordingly. Asking the
developer is mandatory.
