---
name: aisy.plan-feature
description: Given a requirements.md path, evaluates gaps, asks up to 3 critical questions if needed, discovers the repo's subagents, then invokes the planner subagent to generate a plan.md whose tasks are attributed to the best-fitting agent (`@agent`). Trigger when the user says "plan-feature", "genera el plan", "planifica esta feature", "crea el plan.md", or invokes /aisy.plan-feature.
---

## Codex question fallback

When running in Codex, use `ask_user_question` when it is available. Only when it is unavailable, ask conversationally: show each alternative in every multiple-choice or sequential question with an explicit index, and explicitly instruct the user to reply with that index or with free-form text.

Present exactly one pending question at a time, with identifiable options and an explicit instruction that the user may choose an option or provide their own answer. Wait for and process that answer before asking another question or executing a dependent step. Treat free-form answers as valid. When the user says they do not know yet (or equivalent), record a gap and continue only with independent questions or steps.

Do not change Claude Code's existing question mechanism or behavior.

## Language

Detect the language of the user's initial (or most recent) message and conduct the ENTIRE interaction in that language — every `AskUserQuestion` prompt, header, option label and description, and every message, summary, and generated file or output. Mirror the user's language exactly and never switch to another language. These instructions are written in English, but that must NOT force the interaction into English: if the user wrote in Spanish, ask and write in Spanish; if they wrote in another language, use that one. The language of the inputs determines the language of the outputs.

## Instructions

Follow these steps in order.

### Step 1 — Locate the requirements.md

The user may pass the path to the `requirements.md` as an argument to the skill, or mention it in their message.

- If a path was provided, use it directly.
- If none was provided, use `Glob` to search for all `specs/*/requirements.md` and ask the user which one they want to plan with `AskUserQuestion` (a single question, options = the paths found).
- If no `requirements.md` exists under `specs/`, inform the user and stop.

Read the selected `requirements.md` with `Read`.

### Step 2 — Evaluate critical gaps

Read the requirements carefully. Evaluate whether there are ambiguities or missing information that the planner agent **cannot resolve on its own by reading the code**. Consider it a critical gap only if it prevents writing a concrete task in the plan:

| Gap type | Examples that ARE critical | Examples that are NOT critical |
|-------------|------------------------------|------------------------------|
| Unspecified behavior for a relevant edge case | "What happens if the user saves without changes?" | "What happens if there are 0 records?" (the planner can infer it) |
| Technical decision with two equally valid options and different consequences | "Do we manage state in the parent component or in a context?" | "Do we use `useState` or `useReducer`?" (the planner chooses) |
| Ambiguous scope that could double the size of the feature | "Does the fix apply only to component A or also to component B?" | Unspecified style details (the planner reads css-spec) |

**Criterion**: if the planner can resolve it by reading the code, the css-spec, or existing patterns → **it is not a gap, do not ask**.

If you detect **0 critical gaps**, skip directly to Step 4.

If you detect **1-3 critical gaps**, continue with Step 3.

### Step 3 — Ask the user (only if there are gaps)

Use `AskUserQuestion` with **a single call** that groups all detected gaps (maximum 3 questions in the same call). The questions must be:
- Concrete and closed whenever possible (predefined options + "Other")
- Ordered from highest to lowest impact on the plan

After receiving the user's answers, keep them in mind as additional context for the planner. Do not edit the `requirements.md`.

### Step 4 — Discover the repository's agents

Before invoking the planner, check whether the repository defines its own subagents:

- Use `Glob` to search for all `.claude/agents/*.md` files.
- **If at least one exists**, read the frontmatter of each file (`name` and `description`) and build an **agent catalog** `{ @name → description }`. This catalog is passed to the planner so it attributes each task to the agent that best fits by its nature (e.g. front-end/UI → the UI agent, logic/backend → the code agent, tests → the tests agent, discovery/design → the architect).
- **If none exists**, there is no catalog: the planner will write the tasks **without** the `@agent-name` section.

Do not make up agent names: use only the real `name` values found in `.claude/agents/`.

### Step 5 — Invoke the planner agent

Invoke the `planner` subagent with a prompt that includes:

1. The **absolute path** of the `requirements.md`
2. The **full content** of the `requirements.md` (so the agent does not need to request it again)
3. If there were questions in Step 3: the **user's answers** as additional context, clearly marked as "User clarifications:" before the answers
4. The **agent catalog** from Step 4 (or the note that there are no agents)
5. The mandatory **task format** (see below)
6. The instruction to write the `plan.md` in **the same folder** as the `requirements.md`

#### Format of each plan.md task

Each task is a `- [ ]` checkbox (essential: `implement-feature` and `agentic-loop` use `- [ ]` / `- [x]` for progress tracking).

- **With agents available** — cite the agent that will develop it right after the checkbox, separated by `·`:

  ```
  - [ ] @agent-name · Short task name: detailed description.
  ```

  `@agent-name` is the `name` from the catalog (Step 4) that best fits the task. `implement-feature` routes the task to that subagent via the `@tag`.

- **Without agents available** — the `@agent-name ·` section is omitted and the task is written directly:

  ```
  - [ ] Short task name: detailed description.
  ```

Example prompt to the planner:

```
Generate the plan.md for the feature in specs/{folder}/.

Requirements path: specs/{folder}/requirements.md

Requirements content:
---
{full content}
---

{if there were clarifications:}
User clarifications:
- Question: {question 1} → Answer: {answer 1}
- Question: {question 2} → Answer: {answer 2}

{if there are agents in the repo:}
Agents available in this repository (attribute each task to the one that best fits):
- @{agent 1}: {description of agent 1}
- @{agent 2}: {description of agent 2}
- @{…}

Format of EACH plan.md task:
- [ ] @agent-name · Short task name: detailed description.
Use only the @names from the agent list above, choosing for each task the one that best fits its nature.

{if there are NO agents in the repo:}
This repository does not define its own subagents. Write each task WITHOUT agent attribution, using this format:
- [ ] Short task name: detailed description.

Write the plan.md to specs/{folder}/plan.md.
```

### Step 6 — Confirm to the user

When the planner agent finishes, inform the user of:
- The path of the generated `plan.md`
- The number of batches and tasks created
- If the plan attributes agents: the agents used and how many tasks it assigned to each; if there were no agents in the repo, note that
- If there were clarifications incorporated into the plan, mention them briefly

Show this block only when the run finished successfully, meaning the planner agent finished and `plan.md` was written. If the run ended any other way (no `requirements.md` exists under `specs/` and the skill stopped at Step 1), show nothing: no block, no next step line.

Then close with:

```
✅ Done. Suggested next step:

🚀 /aisy.implement-feature to run the plan and open the PR.
```

Write the block in the user's language, following the `## Language` section at the top of this file. Keep the skill name (`/aisy.implement-feature`) and the emoji exactly as it is, only the words around it get translated.

This block only suggests. Do not run the suggested skill yourself and do not chain into it: stop here and wait for the user to invoke it.
