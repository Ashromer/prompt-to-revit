---
name: aisy.clarify-feature
description: Interrogates the user to close the gaps section of one or more requirements.md files (the `DEFINITION GAP` from specify-feature), then folds every decision into the document and deletes the section once fully resolved. Trigger when the user says "clarify-feature", "clarifica", "cierra los gaps", "resuelve los gaps", or invokes /aisy.clarify-feature.
---

## Codex question fallback

When running in Codex, use `ask_user_question` when it is available. Only when it is unavailable, ask conversationally: show each alternative in every multiple-choice or sequential question with an explicit index, and explicitly instruct the user to reply with that index or with free-form text.

Present exactly one pending question at a time, with identifiable options and an explicit instruction that the user may choose an option or provide their own answer. Wait for and process that answer before asking another question or executing a dependent step. Treat free-form answers as valid. When the user says they do not know yet (or equivalent), record a gap and continue only with independent questions or steps.

Do not change Claude Code's existing question mechanism or behavior.

## Language

Detect the language of the user's initial (or most recent) message and conduct the ENTIRE interaction in that language — every `AskUserQuestion` prompt, header, option label and description, and every message, summary, and generated file or output. Mirror the user's language exactly and never switch to another language. These instructions are written in English, but that must NOT force the interaction into English: if the user wrote in Spanish, ask and write in Spanish; if they wrote in another language, use that one. The language of the inputs determines the language of the outputs.

## Purpose

Where `specify-feature` **evidences** gaps without resolving them, `clarify-feature` is the skill that closes them: it interrogates the user about the exact open items already listed in a requirements.md's gap section, folds each decision into the right part of the document, and removes the section once nothing is left open. Unlike `grill-me` (which re-analyzes a whole document from scratch and can surface new gaps), `clarify-feature` only works the gaps that are already itemized — it does not go hunting for new ones.

## Instructions

Follow these steps in order.

### Step 0 — Resolve the target(s)

The user may point at a target in their message: a file path, a folder path, or a feature name/slug.

- **Path to a `requirements.md`** → use it directly.
- **Path to a folder** (e.g. `specs/003-feature-name/`) → use `specs/{folder}/requirements.md`.
- **Feature name or slug fragment** (e.g. "the auth feature", "003") → `Glob` `specs/*/requirements.md` and match folder names containing the fragment. If more than one matches, list them and ask which one via `AskUserQuestion`.
- **Nothing indicated** → go to Step 1 (discovery).

If a target was resolved here and the file has no gap section (see Step 1 detection), tell the user this feature has no open gaps and stop.

If a target was resolved here, skip Step 1 and go straight to Step 2 for that single file.

### Step 1 — Discover requirements.md files with open gaps

Use `Glob` to find all `specs/*/requirements.md`. Read each one and detect a gaps section by its heading — match case-insensitively on any `##` heading containing "gap" (covers `## DEFINITION GAP` from `specify-feature` and equivalent variants). A section counts as open only if it has at least one unresolved item (`- [ ]`, not `- [x]`).

Build **FOUND**: list of `{ folder, gap_heading, open_count }`.

- **If FOUND is empty** → tell the user no `requirements.md` currently has open gaps, and stop.
- **If FOUND has exactly one entry** → select it automatically, tell the user which feature was detected, and go to Step 2.
- **If FOUND has more than one entry**:
  1. Print the list as plain text: `{folder} — {open_count} open gap(s)`.
  2. Use `AskUserQuestion` (single question, single-select):
     - Question: "Do you want to go through all features with open gaps one by one, or focus on a specific one?"
     - Options: `All, one by one` (description: "Process every feature with open gaps in sequence") + up to 3 individual features as their own options (`description`: "{open_count} open gap(s)"). The rest are reachable via the automatic "Other" option by folder name.
  3. Build **TARGETS**: either all of FOUND (in the order listed) or the single chosen one.

### Step 2 — Process each target sequentially

> Always sequential, never parallel — this step interrogates a human, so each feature's round must finish (and the file must be saved) before the next one starts.

For each target `requirements.md` in **TARGETS**:

#### 2a. Read and extract the open gap items

Read the file. Extract every unresolved bullet (`- [ ]`) under the detected gap heading, in order, as **GAP_ITEMS**. Keep the rest of the document in mind as context (it usually explains *why* each gap matters — a `[NEEDS CLARIFICATION: ...]` marker elsewhere in the file often corresponds 1:1 to a gap bullet).

#### 2b. Build the questions

One question per gap item, unless two items are so tightly coupled that answering one trivially answers the other (then merge them into a single question and note it covers both). For each question:

- Phrase it directly from the gap bullet's wording — do not soften or generalize it.
- Propose 2-4 plausible options grounded in the rest of the document's context (existing requirements, assumptions, entities). The user can always answer via "Other".
- **Always include an explicit "not sure yet / decide later" option.** If the user picks it, that gap stays open — this is the only way an item survives the interrogation unresolved.

#### 2c. Launch the interrogation in rounds

Split the questions into rounds of **at most 4 per `AskUserQuestion` call**. Before the first round, tell the user briefly: how many gaps this feature has and that you're starting.

Wait for each round's answers before launching the next. Accumulate as **ANSWERS** (mapping gap item → answer, including "not sure yet" where chosen).

#### 2d. Fold the decisions into the document

For each answered gap item:

1. Find where it applies in the document — a `[NEEDS CLARIFICATION: ...]` marker in Functional Requirements, an ambiguous Edge Case, a missing Assumption, etc. — and rewrite that part with the decision made, removing the marker.
2. If the gap doesn't map to an existing line (it was a standalone open question), add the decision to the most fitting section (`Assumptions` for scope/dependency decisions, `Functional Requirements` for behavior decisions, `Edge Cases` for edge-case decisions).
3. Do not invent extra detail beyond what the user answered.

For gap items left as "not sure yet": keep their bullet under the gap heading, unchanged.

#### 2e. Update or remove the gap section

- **If every item in this feature was resolved** → delete the entire gap heading and its bullets from the file.
- **If some items remain open** → keep the heading with only the still-open bullets (remove the resolved ones).

Write the file with `Edit`/`Write`.

#### 2f. Per-feature confirmation

If processing multiple targets, print a short line before moving on:

```
✓ {folder}: {resolved}/{total} gaps closed{" — section removed" if fully closed}
```

### Step 3 — Final summary

When all targets are processed, show:

```
## Clarify summary

| Feature | Gaps resolved | Gaps still open |
|---|---|---|
| {folder} | {n}/{total} | {n} |
```

If any feature still has open gaps, remind the user they can run `/aisy.clarify-feature` again on that feature once they have an answer.

Show this block only when the run finished successfully, meaning the interrogation ran to completion on at least one target, gaps still open included. If the run ended any other way (Step 0 or Step 1 found no target and the skill stopped), show nothing: no block, no next step line.

Then close with:

```
✅ Done. Suggested next step:

📋 /aisy.plan-feature to break the feature into an ordered plan of tasks.
```

Write the block in the user's language, following the `## Language` section at the top of this file. Keep the skill name (`/aisy.plan-feature`) and the emoji exactly as it is, only the words around it get translated.

This block only suggests. Do not run the suggested skill yourself and do not chain into it: stop here and wait for the user to invoke it.

## Constraints

- **Never invent answers.** Every resolved gap must trace back to an explicit user answer in this session.
- **Never widen scope.** Do not analyze the document for new gaps beyond what the existing gap section already lists — that is `grill-me`'s job, not this skill's.
- **Sequential only.** Do not dispatch parallel subagents for this skill; the interrogation requires the user's live input feature by feature.
- **Minimal diffs.** Only touch the gap section and the specific lines each decision resolves — do not rewrite unrelated parts of the document.
