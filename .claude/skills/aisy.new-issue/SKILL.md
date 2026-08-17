---
name: aisy.new-issue
description: Opens a bug or a feature request on GitHub. Automatically detects the type; if it is not clear, it asks. Bug → investigate, reproduce and document. Feature → clarify in up to 3 questions and open the issue. Trigger with /aisy.new-issue.
---

## Codex question fallback

When running in Codex, use `ask_user_question` when it is available. Only when it is unavailable, ask conversationally: show each alternative in every multiple-choice or sequential question with an explicit index, and explicitly instruct the user to reply with that index or with free-form text.

Present exactly one pending question at a time, with identifiable options and an explicit instruction that the user may choose an option or provide their own answer. Wait for and process that answer before asking another question or executing a dependent step. Treat free-form answers as valid. When the user says they do not know yet (or equivalent), record a gap and continue only with independent questions or steps.

Do not change Claude Code's existing question mechanism or behavior.

> **GitHub-only write scope (mandatory):** this skill's only write permission is GitHub — creating issues via `gh issue create`. It must never create, edit, or delete files in the repository, and it must never run `git commit` or `git push`. Using `Write`, `Edit`, `git commit`, or `git push` on repository files is explicitly forbidden at any point during this skill's execution, in both the Bug Flow and the Feature Flow.

## Language

Detect the language of the user's initial (or most recent) message and conduct the ENTIRE interaction in that language — every `AskUserQuestion` prompt, header, option label and description, and every message, summary, and generated file or output. Mirror the user's language exactly and never switch to another language. These instructions are written in English, but that must NOT force the interaction into English: if the user wrote in Spanish, ask and write in Spanish; if they wrote in another language, use that one. The language of the inputs determines the language of the outputs.

---

## General instructions

Do not write code. Do not do refactors. Do not open PRs. Your only goal is to **document and open an issue on GitHub**.

> **Title convention (mandatory):** every issue carries a prefix according to its type — `[BUG] {bug name}` or `[FEAT] {feature name}`. The prefix goes in uppercase and between brackets at the start of the title.

---

## Step 0 — Classify: bug or feature

Read the user's input (command argument or previous message) and identify the type.

**Bug signals:**
- Stack trace, error messages, `TypeError`, `Cannot read`, `500`, `404`
- Phrases like "doesn't work", "throws an error", "fails", "stopped", "breaks", "won't load"

**Feature signals:**
- Phrases like "I want to", "add", "new functionality", "be able to do", "it would be useful", "feature", "improvement"

**Decision:**

| Case | Action |
|------|--------|
| Clear **bug** signals | Go directly to the **Bug Flow** |
| Clear **feature** signals | Go directly to the **Feature Flow** |
| Ambiguous or no description | Use `AskUserQuestion` with the following question |

If it is ambiguous, use `AskUserQuestion`:
- Question: "What type of issue do you want to open?"
- Options:
  - `Bug` — something that works incorrectly or throws an error
  - `Feature` — new functionality or improvement

With the answer, go to the corresponding flow.

---

---

# BUG FLOW

---

### B0 — Get the description of the problem

The input can arrive in three ways:

1. **Command argument** (text after `/aisy.new-issue`) → use it directly.
2. **Mention in the user's message** → use it as-is.
3. **No description** → use `AskUserQuestion`:
   - Question: "What is the bug you want to report?"
   - Options: `I paste the error log now`, `I describe the behavior`, `I indicate the steps to reproduce it`

Save the input as **INITIAL_DESCRIPTION**.

Mentally classify the type of input:

| Type | Signals |
|------|---------|
| **Error log** | Stack trace, `Error:` / `TypeError:` / `at ...` |
| **Functional description** | "the button doesn't work", "won't load", "shows up blank" |
| **Reproduction steps** | Numbered list of actions that cause the failure |

---

### B1 — Investigate the code

Use `Grep`, `Glob`, `Read` to understand which part of the code is involved.

#### B1a. Extract keywords

From **INITIAL_DESCRIPTION** extract:
- Names of components, functions, routes or error messages.
- If it is a log: the error name and first lines of the stack trace.
- If it is a description: key functional terms.

#### B1b. Locate relevant files

1. If there is a path in the stack trace → read that file directly.
2. If there is a function or component name → `Grep` for that name.
3. If it is a functional description → `Glob` in the related paths.

Read the relevant files. Pay attention to:
- Event handlers, API calls, conditional logic.
- Props or states that could be undefined/null.

#### B1c. Review recent changes

```bash
git log --oneline -15
```

If any recent commit touches the relevant files:

```bash
git show {hash} --stat
git diff {hash}^ {hash} -- {file}
```

Save as **CODE_FINDINGS**: files involved, probable cause, related commits.

---

### B2 — Reproduce the error in the browser

> If the error is clearly server/build related (not UI), skip to B3.

#### B2a. Verify the development server

```powershell
$conn = Get-NetTCPConnection -LocalPort 3000 -ErrorAction SilentlyContinue
if ($conn) { Write-Host "Port 3000 active" } else { Write-Host "Port 3000 free" }
```

If it is not active, launch the server in background: `npm run dev`. Wait ~10 s.

#### B2b. Open the browser

1. `mcp__chrome-devtools__list_pages` — get the open pages.
2. If none points to localhost: `mcp__chrome-devtools__new_page`.
3. `mcp__chrome-devtools__navigate_page` → `http://localhost:3000{route}`.

#### B2c. Reproduce and attempt the failure

| Type | What to do |
|------|-----------|
| Log with a path | Navigate to the path, run the action |
| Functional description | Navigate to the section, interact |
| Reproduction steps | Follow them: `fill`, `click`, `navigate_page` |

#### B2d. Capture evidence

1. Screenshot: `mcp__chrome-devtools__take_screenshot`
2. Console: `mcp__chrome-devtools__list_console_messages` (filter `error` and `warning`)
3. Network: `mcp__chrome-devtools__list_network_requests` (look for 4xx / 5xx)
4. DOM (optional): `mcp__chrome-devtools__take_snapshot`

Save as **EVIDENCE**: steps executed, reproduced (Yes/No/Partially), console messages, failed requests, screenshot.

---

### B3 — Synthesize the report

| Field | Content |
|-------|-----------|
| **Title** | Prefix `[BUG] ` + clear phrase in the imperative: "[BUG] Button X fails when Y" |
| **Description** | What happens vs. what should happen |
| **Steps to reproduce** | Exact numbered list |
| **Expected behavior** | What the user should see |
| **Actual behavior** | What actually happens |
| **Evidence** | Console, network, screenshot |
| **Files involved** | Paths and lines from B1 |
| **Possible cause** | Hypothesis without a solution |
| **Environment** | Git branch, OS, URL |

---

### B4 — Confirm with the user

Show:

```
I am going to open the following issue:

Title: [BUG] {title}
Label: bug
Reproduced: Yes / No / Partially

Summary:
{2-3 lines of actual vs. expected behavior}

Files involved:
- {file:line}

Proceed?
```

`AskUserQuestion`:
- Question: "Do I open the issue with this content?"
- Options: `Yes, open the issue`, `Adjust the title`, `Add more context first`

If the user adjusts it, update before continuing.

---

### B5 — Create the issue on GitHub

```bash
gh issue create \
  --title "[BUG] {title}" \
  --label "bug" \
  --body "$(cat <<'EOF'
## Description

{what happens vs. what should happen}

## Steps to reproduce

{numbered list}

## Expected behavior

{what should happen}

## Actual behavior

{what actually happens}

## Evidence

### Console
\`\`\`
{console messages, or "No errors in console"}
\`\`\`

### Network requests
{failed requests, or "No failed requests"}

### Screenshot
{screenshot path, or "Not available"}

## Files involved

{list of files with paths and lines}

## Cause hypothesis

{technical analysis — without proposing a solution}

## Environment

- Branch: \`{git branch --show-current}\`
- OS: Windows 11
- Reproduction URL: \`{url}\`
- Reproduced: {Yes / No / Partially}
EOF
)"
```

> If the `bug` label does not exist, omit `--label "bug"` and warn the user.

---

### B6 — Confirm and close

```
Issue created: #{number} — {title}
URL: {url}

Evidence collected:
- Files analyzed: {n}
- Reproduced: Yes / No / Partially
- Console errors: {n}
- Failed requests: {n}

To work on the fix: /aisy.specify-feature → select #{number}
```

---

---

# FEATURE FLOW

---

### F0 — Get the description of the feature

The input can arrive in three ways:

1. **Command argument** → use it directly.
2. **Mention in the message** → use it as-is.
3. **No description** → use `AskUserQuestion`:
   - Question: "What functionality do you want to add or improve?"
   - Options: `I describe the functionality`, `I have a specific use case`, `It is an improvement to something existing`

Save as **FEATURE_DESCRIPTION**.

---

### F1 — Evaluate whether the feature is well defined

Analyze **FEATURE_DESCRIPTION** and identify which dimensions are present:

| Dimension | Is it clear? | Clarifying question if missing |
|-----------|-------------|-------------------------------------|
| **What** — concrete behavior | Is it known exactly what it does? | "What exactly should happen when this function is used?" |
| **Why** — the user's goal | Is it known what it is for? | "What problem does it solve or what flow does it improve for the user?" |
| **Scope** — limits and edge cases | Is it clear what it does NOT include? | "Are there special cases or constraints we should take into account?" |

**If all 3 dimensions are clear** → skip directly to F3.

**If any is missing** → go to F2 with the necessary questions (maximum 3).

---

### F2 — Clarification round (maximum 3 questions)

Use **a single `AskUserQuestion` call** with all the questions that are missing (maximum 3).

Build only the questions for the dimensions that are incomplete. Examples:

- If the **What** is missing: "What exactly should happen when the user uses this function? Describe the behavior step by step."
- If the **Why** is missing: "What problem does this solve for the user? In what flow or situation would they use it?"
- If the **Scope** is missing: "Are there constraints, special cases, or things that are explicitly out of scope for this feature?"

After receiving the answers, incorporate the new context into **FEATURE_DESCRIPTION** and continue to F3.

---

### F3 — Synthesize the feature request

With all the information gathered, build:

| Field | Content |
|-------|-----------|
| **Title** | Prefix `[FEAT] ` + phrase in the imperative: "[FEAT] Add X", "[FEAT] Allow the user to Y" |
| **Description** | What the feature does and what it is for |
| **Desired behavior** | Steps or states the user experiences |
| **Acceptance criteria** | List of verifiable conditions indicating the feature is complete |
| **Additional context** | Affected screens, constraints, relationship with other features |

---

### F4 — Confirm with the user

Show:

```
I am going to open the following issue:

Title: [FEAT] {title}
Label: enhancement

Description:
{2-3 lines of what it does and why}

Acceptance criteria:
- {criterion 1}
- {criterion 2}
- {criterion 3}

Proceed?
```

`AskUserQuestion`:
- Question: "Do I open the issue with this content?"
- Options: `Yes, open the issue`, `Adjust the title or description`, `Add or change a criterion`

If the user adjusts it, update before continuing.

---

### F5 — Create the issue on GitHub

```bash
gh issue create \
  --title "[FEAT] {title}" \
  --label "enhancement" \
  --body "$(cat <<'EOF'
## Description

{what the feature does and what it is for}

## Desired behavior

{steps or states the user experiences with the feature active}

## Acceptance criteria

- [ ] {verifiable criterion 1}
- [ ] {verifiable criterion 2}
- [ ] {verifiable criterion 3}

## Additional context

{affected screens, constraints, relationship with other features, or "No additional context"}

## Development branch

- Base: \`dev\`
- Suggested branch: \`feat/{short-kebab-case-name}\`
EOF
)"
```

> If the `enhancement` label does not exist in the repository, try `feature`. If neither exists, omit `--label` and warn the user so they create the label manually.

---

### F6 — Confirm and close

```
Issue created: #{number} — {title}
URL: {url}

To plan the implementation: /aisy.specify-feature → select #{number}
```
