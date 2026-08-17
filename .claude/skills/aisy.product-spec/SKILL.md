---
name: aisy.product-spec
description: Creates or updates the ProductSpec for a project or module. Reads existing context, interviews the user about vision/users/scope, and generates specs/product-spec.md following the PSPEC template (Metadata · Vision · Problem Statement · Target User · Design Principles · Architecture · Interfaces · Configuration · Operations · Deliverables · Project Structure · Out of Scope · Future · Discovery). Trigger when the user says "product spec", "create productspec", "genera productspec", or invokes /aisy.product-spec.
---

# Codex question fallback

When running in Codex, use `ask_user_question` when it is available. Only when it is unavailable, ask conversationally: show each alternative in every multiple-choice or sequential question with an explicit index, and explicitly instruct the user to reply with that index or with free-form text.

Present exactly one pending question at a time, with identifiable options and an explicit instruction that the user may choose an option or provide their own answer. Wait for and process that answer before asking another question or executing a dependent step. Treat free-form answers as valid. When the user says they do not know yet (or equivalent), record a gap and continue only with independent questions or steps.

Do not change Claude Code's existing question mechanism or behavior.

# Product Spec

## Language

Detect the language of the user's initial (or most recent) message and conduct the ENTIRE interaction in that language — every `AskUserQuestion` prompt, header, option label and description, and every message, summary, and generated file or output. Mirror the user's language exactly and never switch to another language. These instructions are written in English, but that must NOT force the interaction into English: if the user wrote in Spanish, ask and write in Spanish; if they wrote in another language, use that one. The language of the inputs determines the language of the outputs.

## Workflow

### 1. Read existing context

Read these files **in parallel** (skip any that don't exist):

- `specs/product-spec.md` — to avoid duplicating work if one already exists
- `specs/tech-spec.md` — for architecture and tech decisions already documented
- `specs/roadmap.md` — for planned phases and scope boundaries
- `README.md` — for project description, installation, and usage
- Dependency manifest (`package.json`, `requirements.txt`, `go.mod`, etc.) — for project name and dependency list

Note what already exists so the spec extends rather than contradicts it.

### 2. Interview the user — BEFORE writing any files

Use `AskUserQuestion` with exactly **3 questions in one call**:

| Header | Question focus |
|--------|---------------|
| **Scope** | What product, service, or module does this ProductSpec cover? What is explicitly included and what is out of scope? What are the main interfaces or operations it exposes? |
| **Decisions** | Who is the primary user and what pain do they feel today? What design principles or constraints guide every product decision? Are there features consciously excluded? |
| **Context** | What tone, naming conventions, or style rules apply to user-facing content? Are there open questions or unresolved decisions to capture in Discovery? What future features are in consideration but out of current scope? |

Do **not** write any files until the user has answered all three questions.

### 3. Read the codebase (if needed)

After the interview, read any sources still needed to fill the spec accurately:

- Source directory (`src/`, `app/`, `lib/`, or equivalent) — to derive the Project Structure section
- Entry points, API routes, or CLI handlers — for the Interfaces section
- Config or env-var files — for the Configuration section

### 4. Write `specs/product-spec.md`

Generate the file filling **every section of the PSPEC template** in exactly this order. Omit a section only if it genuinely does not apply (e.g. skip `Configuration` if there are no env vars). Mark any field that cannot be determined with `—` so the user can fill it in.

---

#### Metadata callout

```
> [!abstract] Metadata
> | | |
> |---|---|
> | **Status** | 🟡 Draft |
> | **Owner** | <owner from context or user answer> |
> | **Created** | <today YYYY-MM-DD> |
> | **Updated** | <today YYYY-MM-DD> |
> | **Version** | v0.1 |
```

#### `## 🎯 Vision`

1–2 sentences: what the product **is**, what it **does**, and **for whom**. No technical details — those belong in the TechSpec.

#### `## 🔥 Problem Statement`

Table with the concrete problems this product solves. Each row must be a real user pain, not a feature in disguise.

| Pain | Root Cause |
|------|-----------|
| Observable user problem | Why it happens at a technical or organisational level |

#### `## 👤 Target User`

User profiles ordered by priority. Draw from the user's interview answers.

- 🎯 **Primary** — Main user, their role and usage context
- 👥 **Secondary** — Other users who benefit without being the focus
- 🌍 **Stretch** — Future reach if the product grows (omit if not applicable)

#### `## 💎 Design Principles`

3–6 rules that guide every product decision. When features conflict, these principles decide. Format each as: **Short name** — what it means in practice and why it matters.

#### `## 🏗️ Architecture`

One **Mermaid flowchart** showing the main components and how they communicate. No internal module detail — that goes in the TechSpec. Follow the diagram with a bullet list: each component's **role** (what it does) and **responsibility boundary** (what it does not do).

#### `## 🛠️ Interfaces`

One subsection per public operation (endpoint, tool, CLI command, screen, etc.). Group by type if there are many (read vs write, admin vs user). For each operation include: a one-line description, a parameter table, and optionally a collapsible response example or a warning callout for side effects.

Parameter table columns: **Param | Type | Default | Description**. Use `✳️ required` as the default value for mandatory params.

#### `## ⚙️ Configuration`

Describe the configuration mechanism (env vars, `.env`, YAML, config file, etc.) and list every variable. Mark required ones and note sensible defaults. End with how many variables are needed at minimum to run the project.

| Variable | Default | Description |
|----------|---------|-------------|
| `VAR_NAME` | — | Description (required) |

#### `## 🩺 Operations`

**Healthcheck** subsection — how to verify the service is alive and its dependencies are reachable. Include the expected response format.

**Logging** subsection — what is logged (operations, errors, metrics), in what format, and how to configure the level. No technical detail — that goes in the TechSpec.

#### `## 📦 Deliverables`

Table of artefacts delivered as part of the product. Each deliverable must have a clear criterion for what it includes.

| Deliverable | Description |
|:-----------:|-------------|
| 💻 **Source code** | Location and main modules |
| 📚 **README** | Installation, configuration, and usage |

#### `## 🗂️ Project Structure`

File tree as distributed, inside a collapsible callout. Annotate the role of each relevant file or folder with an inline comment.

```
> [!abstract]- File tree
> ```
> project-name/
> ├── src/
> │   └── module/       # Description
> └── README.md
> ```
```

#### `## 🚫 Out of Scope`

Features **consciously excluded**. Document them so they are not requested during development. Include the reason for exclusion to make clear it is a decision, not an oversight.

- **Feature X** — Reason for exclusion

#### `## 🔮 Future`

Features validated for future versions but not in current scope. Not commitments — ideas worth documenting.

- **Feature X** — Brief description and why it is deferred

#### `## ❓ Discovery`

Open product decisions as a checkbox list. Use `AskUserQuestion` with possible pending product decisions not covered in the main questions. Give the user the choice "I don't know yet" and leave the question open if they use that choice.

- [ ] Open question
- [x] ~~Resolved question~~ → Decision taken

### 5. Next step

Show this block only when the run finished successfully, meaning `specs/product-spec.md` was actually written. If the run ended any other way (the user aborted the interview or rejected the write), show nothing: no block, no next step line.

Skip this block entirely when this skill was invoked as a step of `/aisy.constitution` rather than directly by the user. In that case `/aisy.constitution` prints the closing block for the whole run.

Then close with:

```
✅ Done. Suggested next step:

🏗️ /aisy.tech-spec to write the technical side: stack, architecture and decisions.
```

Write the block in the user's language, following the `## Language` section at the top of this file. Keep the skill name (`/aisy.tech-spec`) and the emoji exactly as it is, only the words around it get translated.

This block only suggests. Do not run the suggested skill yourself and do not chain into it: stop here and wait for the user to invoke it.

---

## Constraints

- Write in the same language the user uses in their answers
- Use Mermaid for every diagram — no ASCII art
- Output is a **single file** — no directory, no sub-files
- Do not invent technical details that belong in the TechSpec
- Keep Vision to 1–2 sentences — no feature lists
- Design Principles must be actionable rules, not aspirational values
- Do not add sections the project does not need
