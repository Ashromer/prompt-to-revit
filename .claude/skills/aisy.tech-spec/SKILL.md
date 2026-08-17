---
name: aisy.tech-spec
description: "Creates or updates the TechSpec for a project or module following Spec Driven Development. Reads the existing context, interviews the user in iterative rounds about the technical how (stack, architecture, data, integrations, operations, decisions), and generates specs/tech-spec.md following the TSPEC template. The TechSpec answers how it is built; the ProductSpec already defines the what and the why."
when_to_use: "When the user says 'tech spec', 'genera techspec', 'create tech spec', or invokes /aisy.tech-spec."
---

## Codex question fallback

When running in Codex, use `ask_user_question` when it is available. Only when it is unavailable, ask conversationally: show each alternative in every multiple-choice or sequential question with an explicit index, and explicitly instruct the user to reply with that index or with free-form text.

Present exactly one pending question at a time, with identifiable options and an explicit instruction that the user may choose an option or provide their own answer. Wait for and process that answer before asking another question or executing a dependent step. Treat free-form answers as valid. When the user says they do not know yet (or equivalent), record a gap and continue only with independent questions or steps.

Do not change Claude Code's existing question mechanism or behavior.

## Language

Detect the language of the user's initial (or most recent) message and conduct the ENTIRE interaction in that language — every `AskUserQuestion` prompt, header, option label and description, and every message, summary, and generated file or output. Mirror the user's language exactly and never switch to another language. These instructions are written in English, but that must NOT force the interaction into English: if the user wrote in Spanish, ask and write in Spanish; if they wrote in another language, use that one. The language of the inputs determines the language of the outputs.

---

## Purpose

Take a project from its product definition (ProductSpec) to a complete technical specification where the stack, the architecture, the data model, the integrations, the operations, and the key decisions are explicit and justified. The process is conversational, in rounds, guided entirely with `AskUserQuestion`.

The TechSpec does NOT repeat the what or the why of the product. It focuses exclusively on the **technical how**.

---

## Execution protocol

### Phase 0 — Reading the existing context

Before any question, read the following files in **parallel** (skip the ones that do not exist):

- `specs/tech-spec.md` — to avoid duplicating work if a previous version already exists
- `specs/product-spec.md` — to understand what is being built and the design principles
- `specs/roadmap.md` — planned phases and technical constraints
- Dependency manifest (`package.json`, `requirements.txt`, `pyproject.toml`, `go.mod`, `Cargo.toml`, `pom.xml`, etc.) — extract exact versions, never make them up
- Database schema (`prisma/schema.prisma`, `schema.sql`, migrations, ORM models) — if applicable
- CI/deploy config (`vercel.json`, `.github/workflows/`, `Dockerfile`, `fly.toml`, etc.)
- Glob of the code directory (`src/`, `app/`, `lib/`, or equivalent) — to infer existing modules

Build a mental map of:

- What is already **defined in code or config** (versions, dependencies, structure)
- What is **defined in the ProductSpec** and conditions technical decisions
- What is **absent** and must be resolved in the interview
- What appears **inconsistent** between what is declared and what is implemented

Do not share this analysis with the user. Use it to generate concrete options in each `AskUserQuestion`.

---

### Phase 1 — Initial setup

Show the user the following table in markdown text (not with a tool) so they know which rounds will be covered:

```
The questions are organized into 6 rounds, all focused on the technical HOW:

| Round | Category                           | What it explores                                                    |
|-------|------------------------------------|---------------------------------------------------------------------|
| 1     | Technical scope and boundaries     | What the TechSpec covers, what is out of scope, dependencies on other docs |
| 2     | Stack and versions                 | Language, framework, libraries, versions, runtime                   |
| 3     | Architecture and modules           | Topology, modules, patterns, internal boundaries                    |
| 4     | Data and integrations              | Persistence, schema, external services, error propagation           |
| 5     | Operations                         | Deployment, CI/CD, healthcheck, logging, testing                    |
| 6     | Decisions, tradeoffs and Discovery | ADRs, discarded alternatives, limitations, open questions           |
```

Next, use `AskUserQuestion` so the user chooses the total intensity of the interview:

```
AskUserQuestion:
  question: "How many questions do you want to answer in total?"
  header: "Intensity"
  options:
    - label: 5 — Quick
      description: Only the critical parts. One question per round in the 5 most relevant, one round is skipped.
    - label: 8 — Balanced
      description: Reasonable coverage. ~1–2 questions per round, prioritizing the biggest gaps.
    - label: 12 — Exhaustive
      description: Full coverage. 2 questions per round across the 6 rounds.
```

Save the chosen number as **T** (total questions). Distribute T across the 6 rounds, prioritizing the gaps detected while reading the context:

- **T = 5**: 1 question in the 5 rounds with the biggest gap; skip the least critical round.
- **T = 8**: start with 1 question per round (6) and add 2 extra questions in the rounds with the most gaps.
- **T = 12**: 2 questions per round in all of them.

If a round has no relevant gaps for the project (e.g. no DB → Round 4 with no schema question), skip it and reassign its quota to the next round with the biggest gap, keeping T constant.

---

### Phase 2 — Question rounds

#### Rules for using AskUserQuestion

- **One question = one `AskUserQuestion` call.** Never group two questions in the same call (except the initial setup).
- **🚫 ONLY ONE `AskUserQuestion` call per turn/message.** It is strictly forbidden to issue two or more `AskUserQuestion` calls in the same turn, even if each one has a single question. It is also forbidden to combine `AskUserQuestion` with any other tool (Write, Edit, Bash, etc.) in the same turn. The turn must contain **exclusively** that single call and nothing else.
- **ALWAYS wait for the user's answer before issuing the next question.** After launching the question, the turn ends. Do not assume answers, do not get ahead to the next question, do not write any file until you have received the user's actual answer. If a question was rejected or left unanswered, **re-launch that same question**, never move on to the next one.
- **Strictly sequential flow:** launch 1 `AskUserQuestion` (complete turn) → wait for and process the answer → in the next turn launch the following one. Never parallelize.
- **Each question has exactly 3 options** that are concrete and specific to the context read. The tool automatically adds "Other" as a 4th option.
- The options must be **plausible answers given the project's real context** (versions that already appear in the manifest, patterns compatible with the ProductSpec, etc.), not generic options like "Yes / No / I don't know".
- If an answer opens a new critical gap, ask a follow-up question with `AskUserQuestion` before moving on (it counts within the total T quota).
- If a round has no relevant gaps (e.g. project with no DB → skip the schema question), skip it and notify: *"Round X: no relevant gaps for this project. Continuing."* — that round's quota is reassigned to the next one with the biggest gap.
- Respect the total T chosen in Phase 1: never exceed that question count across the whole interview.
- **Never make up versions.** If an option includes a version, it must come from the manifest that was read. If there is no manifest yet, offer plausible ranges and mark them as *to be confirmed*.

#### Format of each AskUserQuestion

```
AskUserQuestion:
  question: "[Round X · Category] Concrete question about the technical how"
  header: "R X · ShortName"   ← 12 characters maximum
  multiSelect: false
  options:
    - label: Concrete option A
      description: What it implies technically and what consequences it has
    - label: Concrete option B
      description: What it implies technically and what consequences it has
    - label: Concrete option C
      description: What it implies technically and what consequences it has
```

---

#### Round 1 — Technical scope and boundaries

Goal: delimit what the TechSpec describes and what is out of scope.

Guiding questions (select according to the quota assigned to this round):

- What system, service, or module does this TechSpec cover? Is it the full monolith, a specific service, a module?
- What technical pieces are explicitly out of scope (mobile, shared infra, internal tools)?
- Are there other sibling TechSpecs or ones this one depends on?

---

#### Round 2 — Stack and versions

Goal: pin down the language, framework, and libraries with exact versions.

Guiding questions:

- What is the main language and runtime? (offer concrete versions from the manifest if it exists)
- What main framework is used for the layer being addressed? (web framework, CLI framework, agent framework, etc.)
- Which libraries are critical for the business logic (HTTP client, ORM, validation, parsing, LLM SDK, etc.)?
- Is there a version constraint for compatibility with infrastructure, hosting, or client?

---

#### Round 3 — Architecture and modules

Goal: define the topology and internal boundaries.

Guiding questions:

- What architectural pattern applies (modular monolith, microservices, agents, pipeline, CLI, lambda/serverless)?
- What are the main modules and the responsibility of each one in one sentence?
- How do the modules communicate (direct calls, events, message queue, FS, IPC)?
- Are there explicit abstraction layers (domain, infra, adapters, ports)?

---

#### Round 4 — Data and integrations

Goal: persistence and external boundaries.

Guiding questions:

- Is there a database? If so, what engine, what version, managed by whom?
- How are migrations managed and how are environments distinguished (dev/staging/prod)?
- What external services does the system consume (APIs, LLMs, storage, auth, queues)?
- How are errors propagated to the caller (HTTP code, exception, error return, redirect, UI message)?
- Are there rate limits, silent behaviors, or known pitfalls in the integrations?

---

#### Round 5 — Operations

Goal: how it is deployed, observed, tested, and kept alive.

Guiding questions:

- Where is it deployed and with what pipeline (Vercel, Fly, K8s, bare metal, manual)?
- Which environment variables are mandatory and where are the secrets managed?
- How is it verified that the service is alive (HTTP /health, CLI, ping to dependencies)?
- What is logged, with which library, and at what default level?
- Testing strategy: unit / integration / e2e, framework, coverage target?
- How is it run locally (one command, several, docker-compose)?

---

#### Round 6 — Decisions, tradeoffs and Discovery

Goal: capture the reasoning behind each choice and what remains pending.

Guiding questions:

- Which technical decision is the most controversial or the one that generated the most debate? (future ADR-001)
- What concrete alternative was discarded and why?
- Which limitation is a tradeoff consciously accepted (not a bug)?
- What remains as an open question or a deferred decision?
- Are there assumptions about the future (scale, team, integrations) that could invalidate what is written?

---

### Phase 3 — Additional code reading (if info is missing)

After the interview, read any source still needed to fill the spec accurately:

- Entry points, API routes, integration files — for Integration Mapping
- Deploy/CI configuration not read yet
- Any file whose content was flagged as TBD by an answer

---

### Phase 4 — Summary and confirmation before writing

Show a table summary of the collected decisions:

```
## Session summary

| Area                  | Decision                              | Status     |
|-----------------------|---------------------------------------|------------|
| Main stack            | <language + framework + versions>     | ✅ Defined  |
| Architecture          | <pattern + key modules>               | ✅ Defined  |
| Persistence           | <engine + strategy>                   | ✅ Defined  |
| Deployment            | <platform + pipeline>                 | ✅ Defined  |
| Identified ADRs       | <n decisions>                         | ✅ Defined  |
| Open questions        | <m questions>                         | *TBD*      |
```

Next, use `AskUserQuestion` to confirm the write:

```
AskUserQuestion:
  question: "Shall I proceed to write specs/tech-spec.md with these decisions?"
  header: "Write"
  options:
    - label: Yes, write now
      description: I generate the complete TechSpec with the TSPEC template and all the decisions made.
    - label: I want to review a round
      description: Before writing, I go back to a specific round to adjust answers.
    - label: Only write key sections
      description: I generate only Scope, Stack, Module Design, and ADRs. The rest stays as TBD.
```

---

### Phase 5 — Writing `specs/tech-spec.md`

Generate the file using **exactly this section order**. Omit any section that genuinely does not apply (e.g. skip Database Schema if there is no DB). Mark any undetermined field with `—` or *TBD* so the user can complete it.

---

#### Metadata callout

```
> [!abstract] Metadata
> | | |
> |---|---|
> | **Status** | 🟡 Draft |
> | **Owner** | <owner from context or user's answer> |
> | **Created** | <today YYYY-MM-DD> |
> | **Updated** | <today YYYY-MM-DD> |
> | **Version** | v0.1 |
> | **ProductSpec** | [[product-spec]] |
```

#### `## 📌 Scope`

1–2 sentences: what this document covers and its relationship to the ProductSpec. *"This document answers the technical how; the ProductSpec answers the what and the why."*

#### `## 🧱 Tech Stack`

Table with columns: **Component | Technology | Version | Rationale**. Exact versions from the dependency manifest, never made up. Close with a `> [!tip]` callout listing the direct runtime dependencies on a single line.

#### `## 🏗️ Module Design`

One or two **Mermaid diagrams** (infrastructure topology + application module graph, no ASCII art). Then, one subsection per main module: name as `#### \`path/to/module\` — Descriptive name`, with its responsibility in exactly one sentence.

#### `## 🗄️ Database Schema` *(only if there is a DB)*

**Mermaid ER diagram** derived from the schema. Then, one subsection per model with a column table (Column | Type | Constraints | Notes). Close with a table of migration commands and a `> [!warning]` callout about connection URL distinctions if applicable.

#### `## 🔄 Integration Mapping`

Table: **Internal operation | Method | External service | Notes**. Cover every external API or service the app consumes. Add a `> [!warning]` callout for non-obvious behaviors (rate limits, silent errors, dual URLs, etc.).

#### `## ⚠️ Error Handling`

**Expected errors** subsection with a table: Source | Error | Action | Description. **Propagation** subsection: how errors are exposed to the caller (HTTP, redirect, inline message, exception, etc.).

#### `## 🩺 Healthcheck`

How it is verified that the service is alive and its dependencies are reachable. It can be an HTTP endpoint, a CLI command, or a manual step.

#### `## 📋 Logging`

Library or approach, format, default level. Table of logged events: Event | Level | Fields.

#### `## 🧪 Testing Strategy`

**Unit Tests** subsection with a table: Module | What is tested | Mock/stub. **Integration Tests** subsection with prerequisites and the verified flow. **Tools** subsection with the framework, coverage target, and manual tools.

#### `## 🔌 Deployment`

**Mermaid CI/CD diagram** showing the pipeline. Then:
- Build command (code block)
- **Environment variables** table: Variable | Purpose
- **Local development** subsection with the exact commands to start up locally

#### `## 📦 Dependencies`

Two fenced blocks — **Runtime** and **Dev** — with exact versions from the manifest.

#### `## 📐 ADRs (Architecture Decision Records)`

One `### ADR-00N: Title` per key decision that arose in the interview. Format per ADR:

```
**Decision**: What was decided (1 sentence).
**Context**: Alternatives considered and why this choice.
**Consequences**:
- (+) Advantage
- (-) Disadvantage
- Mitigation if applicable
```

#### `## ⚠️ Known Limitations`

Bullet list of limitations that are **consciously accepted tradeoffs**, not bugs. Draw from the Round 6 answers.

#### `## ❓ Discovery`

Checkbox list with open questions from the interview. Format for resolved items:
`- [x] ~~Open question~~ → Decision made`

---

### Phase 6 — Next step

Show this block only when the run finished successfully, meaning `specs/tech-spec.md` was actually written. If the run ended any other way (the user rejected the Phase 4 confirmation), show nothing: no block, no next step line.

Skip this block entirely when this skill was invoked as a step of `/aisy.constitution` rather than directly by the user. In that case `/aisy.constitution` prints the closing block for the whole run.

Then close with:

```
✅ Done. Suggested next step:

🗺️ /aisy.roadmap (optional) to turn the specs into a phased plan. Skip it if you already know what comes first.
🎯 /aisy.specify-feature to turn what you want to build next into a feature spec.
```

Write the block in the user's language, following the `## Language` section at the top of this file. Keep the skill names (`/aisy.roadmap`, `/aisy.specify-feature`) and the emojis exactly as they are, only the words around them get translated.

This block only suggests. Do not run the suggested skill yourself and do not chain into it: stop here and wait for the user to invoke it.

---

## Constraints

- **Never make up versions.** All versions come from real config files. If the manifest does not exist yet, mark as *TBD* instead of approximating.
- **Mermaid for all diagrams** — no ASCII art.
- **Write in the same language** the user used in their answers.
- **Output is a single file** — no directories, no sub-files.
- **Do not add sections the project does not need.** Better to omit than to fill with *TBD* in bulk.
- **Module subsections: one sentence of responsibility.** No implementation detail — that goes in the code.
- **The TechSpec does not repeat the ProductSpec.** If a user question points to the what or the why, explicitly redirect to the ProductSpec and do not include it here.

## Formatting notes

- Obsidian markdown conventions: no H1, hierarchy starting from H2.
- Do not use em dashes. Use commas or standard punctuation.
- Pending items in italics: *TBD*.
- Wikilinks `[[product-spec]]`, `[[roadmap]]` when applicable.
