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

## Approval before sharing work

- Nothing is **committed, pushed or opened as a PR** without the developer's explicit approval for
  that action. Finish and verify the work first, then ask.
- Approval is per action: ask again before each of commit, push and PR. When the developer approves
  a combined "commit, push and open the PR", that one approval covers the whole sequence.
- Never amend, rebase or force-push shared history without explicit approval.

## Pull requests

- The PR title must explain the **entire scope** of the change. Do not use short milestone labels.
- The description uses these sections, in this order:

  1. `## TLDR` — only when the scope is too big. A short, human-readable summary of the whole change.
  2. `## What` — human-readable bullet points explaining what changed in the code.
  3. `## Why` — one bullet per `## What` point, in the same order, explaining why that change was made.
  4. `## Attachment` — only when pictures are needed to explain something.
  5. `## Note:` — only at the bottom, when something needs a better explanation.

- Keep `What` and `Why` aligned one-to-one: every `What` bullet has a matching `Why` bullet.
- Do not use the old `## What's included` / `## Verified` / `## Notes` layout.

Skeleton:

```markdown
## TLDR
Short summary when the scope is large.

## What
- Human-readable description of change one.
- Human-readable description of change two.

## Why
- Why change one was needed.
- Why change two was needed.

## Attachment
Images only when they help explain something.

## Note:
Extra explanation only if needed.
```

## Note

Every time new instructions are given, this file must be updated accordingly. Asking the
developer is mandatory.
