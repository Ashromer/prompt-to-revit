---
name: aisy.specify-feature
description: Detects features from a file, a URL, a repo's roadmap.md, GitHub issues, or a vague prompt, lets the user pick which ones to develop, and scaffolds a numbered specs/ folder with a spec-kit-style requirements.md per feature — evidencing gaps instead of resolving them. Trigger when the user says "specify-feature", "especifica", "define los requirements", or invokes /aisy.specify-feature.
---

## Codex question fallback

When running in Codex, use `ask_user_question` when it is available. Only when it is unavailable, ask conversationally: show each alternative in every multiple-choice or sequential question with an explicit index, and explicitly instruct the user to reply with that index or with free-form text.

Present exactly one pending question at a time, with identifiable options and an explicit instruction that the user may choose an option or provide their own answer. Wait for and process that answer before asking another question or executing a dependent step. Treat free-form answers as valid. When the user says they do not know yet (or equivalent), record a gap and continue only with independent questions or steps.

Do not change Claude Code's existing question mechanism or behavior.

## Language

Detect the language of the user's initial (or most recent) message and conduct the ENTIRE interaction in that language — every `AskUserQuestion` prompt, header, option label and description, and every message, summary, and generated file or output. Mirror the user's language exactly and never switch to another language. These instructions are written in English, but that must NOT force the interaction into English: if the user wrote in Spanish, ask and write in Spanish; if they wrote in another language, use that one. The language of the inputs determines the language of the outputs.

## Purpose

Turn one or several already-existing feature descriptions (a file, a URL, a roadmap, GitHub issues, or a raw prompt) into `requirements.md` specs. This skill never interrogates the user to resolve ambiguity: it writes down what it has and evidences everything unclear in a `DEFINITION GAP` section for the user to resolve later.

## Instructions

Follow these steps in order.

### Step 1 — Detect the input source

Determine where the feature(s) come from, in this priority order:

**1a. Explicit input** — the user passed an argument to `/aisy.specify-feature` or referenced one in their message: a file path, a URL, or free text.

Classify it:

| Input looks like | Treat as |
|---|---|
| Path to an existing file (verify with `Glob`/`Read`) | **File.** If it structurally holds several features (a roadmap-style document with multiple phase/feature tables, or a list that clearly enumerates distinct features) → **multi-feature**. Otherwise → **single feature**. |
| URL matching `github.com/{owner}/{repo}/issues/{n}` | **Single GitHub issue.** Fetch with `gh issue view {n} --json number,title,body,labels` (fallback: `WebFetch` the URL if `gh` fails). Keep the `{owner}`, `{repo}` and `{n}` captured from the URL and record `source_issue_url = https://github.com/{owner}/{repo}/issues/{n}` for this feature, in canonical form only: drop any query string, fragment, trailing slash or extra path segment. This applies even when the `gh` fetch fails and you fall back to `WebFetch`, because the values come from the URL itself, not from the fetch. |
| URL matching `github.com/{owner}/{repo}` (no issue number) | **GitHub issues source** — go to the GitHub flow in 1c, scoped to that repo. |
| Any other URL | Fetch with `WebFetch`. Apply the same single-vs-multi-feature detection as for files. |
| Free text, not a path or URL | **Vague prompt.** One feature = the whole text, unless it plainly enumerates several distinct items (bullets, numbered list, clearly separate feature names) → treat each as its own candidate. |

**1b. No explicit input — look for `specs/roadmap.md`** (`Glob`). If it exists, read it and extract every feature listed under its `## 🚀 Phase N` tables (`| # | Feature | Depends on | Notes |` rows, per the `roadmap` skill's RMAP template).

Exclude features that are already marked as done: a checked box (`[x]`), a `✅`/`Done`/`Completed` marker, strikethrough, or a linked tracking issue that is closed. If completion status is ambiguous for a row, include it and note the ambiguity in its future `DEFINITION GAP`.

**1c. No explicit input and no `specs/roadmap.md` — check for GitHub issues.** If the repo has a GitHub remote and `gh` is available (`gh repo view` succeeds), fetch open issues:

```
gh issue list --state open --json number,title,body,labels --limit 50
```

Then resolve the repo slug, so every candidate taken from this list can carry its full issue URL:

```
gh repo view --json nameWithOwner
```

Combine the returned `{owner}/{repo}` with each issue's `number` and record `source_issue_url = https://github.com/{owner}/{repo}/issues/{number}` on that candidate, in the same canonical form as 1a (no query string, no fragment, no trailing slash). If you reached 1c from the repo-URL row of 1a, take `{owner}/{repo}` straight from that URL and skip this command. If the command fails or returns no `nameWithOwner`, leave `source_issue_url = null` for these candidates and carry on — do not rebuild the URL from the git remote, the folder name, or the issue title.

**1d. Nothing above resolved it** — ask the user with `AskUserQuestion`:

- Question: "Where are the features you want to specify?"
- Options: `A file in the repo` (ask the path as follow-up via "Other" if needed), `GitHub issues`, `I'll paste the description/URL now`

If the user still gives nothing usable, inform them and stop.

### Step 2 — Build the candidate list and confirm the selection

Normalize every detected feature into `{ title, raw_description, source, source_issue_url }`.

`source_issue_url` is set **only** when the feature comes from a real GitHub issue this run actually resolved: a single-issue URL in 1a, or an issue picked from the 1c list. It is `null` for every other source — files, `specs/roadmap.md` rows (even if a row mentions or links an issue), generic URLs, and free-text prompts — and it stays `null` when 1c could not resolve `{owner}/{repo}`. Never infer or reconstruct it from anything else.

- **If Step 1 already resolved to exactly one unambiguous feature** (a single file, a single issue URL, a single-feature prompt) — treat it as selected automatically, no question needed. Tell the user which feature was detected and that you're proceeding with it.
- **Otherwise** (roadmap with several pending features, open issues list, a multi-feature file/URL, or a prompt enumerating several items):
  1. Print the full candidate list as plain text (title + short source tag, e.g. `roadmap · F1.2`, `#132`, `file`).
  2. Use `AskUserQuestion` with `multiSelect: true`. Include the first 4 candidates as options plus an `All` option (`description`: "Develop all detected features"). The rest are reachable via the automatic "Other" option by number/name.
  3. If the user's original message already expressed a preference (e.g., named a subset), pre-filter the options to that subset before asking, but still confirm.
  4. If the user selects nothing meaningful, default to **All**.

### Step 3 — Assign branch numbers and slugs

For the selected features, compute names **before** creating anything, so parallel writes never collide:

1. `Glob` for existing `specs/*` folders and find the highest `###-` numeric prefix already used (3-digit, zero-padded). Start at `001` if none exist.
2. For each selected feature, in the order presented: build a kebab-case slug from its title (lowercase, spaces/special chars → `-`, collapse double hyphens, trim, max 50 chars), then assign the next sequential number.
3. Folder name: `specs/{NNN}-{slug}/`. Branch name (used inside the template): `{NNN}-{slug}`.
4. If `specs/{NNN}-{slug}/` already exists for a feature that logically matches (e.g., re-running specify on the same roadmap row), skip it and tell the user it already has a folder.

### Step 4 — Generate `requirements.md` for each selected feature (in parallel)

Features are independent of each other — dispatch **one subagent per feature in parallel** (single message, multiple `Agent` tool calls) rather than looping sequentially. Each subagent receives:

- The absolute path to write: `specs/{NNN}-{slug}/requirements.md`
- The feature's `raw_description` (issue body, roadmap row + surrounding phase context, file excerpt, or the user's original prompt) — this is what `$ARGUMENTS` in the template stands for
- The feature's `source_issue_url` when it is set — the subagent writes it verbatim into the `Source Issue:` line of the template. When it is `null`, state explicitly that there is no source issue and that the `Source Issue:` line must be deleted from the output
- The exact template below and the instruction to fill it **using only the information available**, without inventing decisions or trying to resolve ambiguity

Template (fill every bracketed placeholder; keep section order and headings exactly as shown):

```markdown
# [FEATURE NAME]
Feature Branch: [NNN-feature-name]
Source Issue: [full GitHub issue URL, e.g. https://github.com/{owner}/{repo}/issues/{n} — delete this entire line if the feature did not come from a GitHub issue]

Created: [DATE]

Status: Draft

Input: User description: "$ARGUMENTS"

## User Scenarios & Testing (mandatory)

### User Story 1 - [Brief Title] (Priority: P1)

[Describe this user journey in plain language]

Why this priority: [Explain the value and why it has this priority level]

Independent Test: [Describe how this can be tested independently - e.g., "Can be fully tested by [specific action] and delivers [specific value]"]

Acceptance Scenarios:

1. Given [initial state], When [action], Then [expected outcome]
2. Given [initial state], When [action], Then [expected outcome]

### User Story 2 - [Brief Title] (Priority: P2)

[same structure as User Story 1]

### User Story 3 - [Brief Title] (Priority: P3)

[same structure as User Story 1]

[Add more user stories as needed, each with an assigned priority. Only include as many as the source material actually supports.]

## Edge Cases

- What happens when [boundary condition]?
- How does system handle [error scenario]?

## Requirements (mandatory)

### Functional Requirements

- FR-001: System MUST [specific capability]
- FR-002: System MUST [specific capability]
- FR-003: Users MUST be able to [key interaction]

Mark anything not resolvable from the source with the inline marker, e.g.:

- FR-00N: System MUST authenticate users via [NEEDS CLARIFICATION: auth method not specified - email/password, SSO, OAuth?]

### Key Entities (include if feature involves data)

- [Entity 1]: [What it represents, key attributes without implementation]
- [Entity 2]: [What it represents, relationships to other entities]

## Success Criteria (mandatory)

### Measurable Outcomes

- SC-001: [Measurable metric]
- SC-002: [Measurable metric]
- SC-003: [User satisfaction metric]
- SC-004: [Business metric]

## Assumptions

- [Assumption about target users]
- [Assumption about scope boundaries]
- [Assumption about data/environment]
- [Dependency on existing system/service]

## DEFINITION GAP

- [ ] {open question or missing decision the source material does not resolve — worded so the user can answer it directly}
- [ ] {dependency on another feature, system, or team not yet confirmed}
```

Rules for filling it:

- **Never invent** what the source doesn't say. If a section has nothing to draw from (e.g., no data entities mentioned), state so briefly rather than fabricating content, or omit that subsection if the template marks it optional (`Key Entities`).
- **Do not try to resolve ambiguity.** Every open question, missing decision, or unconfirmed dependency goes into `## DEFINITION GAP` instead of being guessed at.
- `DEFINITION GAP` always has at least one bullet. If nothing is genuinely unclear, write a single bullet: `- [x] No blocking gaps detected — source material is self-contained.`
- Split `DEFINITION GAP` bullets naturally into clarification questions and dependency callouts; do not label them as separate subsections, just keep each bullet self-explanatory.
- `Created` uses today's date (`YYYY-MM-DD`).
- `Priority` P1/P2/P3 is inferred from how the source material orders or emphasizes journeys; if the source only describes one journey, write a single User Story and drop the rest.
- `Source Issue:` is written **only** when the feature carries a non-null `source_issue_url` (Step 2) — paste that exact URL, unchanged. If there is no `source_issue_url`, delete the whole line: no `Source Issue: N/A`, no empty label, no leftover placeholder, and never reconstruct a link from the issue number, the repo name, the branch name or the wording of the source — a guessed link is worse than no link. Keep the label in English (`Source Issue:`), like `Feature Branch:`, `Created:`, `Status:` and `Input:`, whatever the user's language.

### Step 5 — Summary

After all subagents finish, show the user:

- The folders created (`specs/{NNN}-{slug}/requirements.md`), one per feature.
- Any candidates skipped because they already had a folder.
- For each generated `requirements.md`: the number of `DEFINITION GAP` bullets (or "no gaps detected").

Show this block only when the run finished successfully, meaning at least one `requirements.md` was generated. If the run ended any other way (every candidate was skipped, or the user aborted the Step 2 confirmation), show nothing: no block, no next step line.

Then close with:

```
✅ Done. Suggested next step:

❓ /aisy.clarify-feature (optional) to close the open gaps before planning. Skip it if the spec is already clear.
📋 /aisy.plan-feature to break the feature into an ordered plan of tasks.
```

Write the block in the user's language, following the `## Language` section at the top of this file. Keep the skill names (`/aisy.clarify-feature`, `/aisy.plan-feature`) and the emojis exactly as they are, only the words around them get translated.

This block only suggests. Do not run the suggested skill yourself and do not chain into it: stop here and wait for the user to invoke it.

Do not invoke `/aisy.clarify-feature` or `/aisy.plan-feature` automatically: `specify-feature` always stops after generating the `requirements.md` files.
