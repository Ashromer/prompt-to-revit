---
name: aisy.implement-feature
description: Orchestrator that reads one or multiple plan.md files, uses git worktrees for isolation, supports sequential or parallel (max 2 workers) execution, detects cross-plan dependencies, and marks tasks [x] or [blocked] with retry logic. Trigger when the user says "implement-feature", "develop plan", "ejecuta el plan", "implementa el plan", "desarrolla el plan", or invokes /aisy.implement-feature.
---

## Codex question fallback

When running in Codex, use `ask_user_question` when it is available. Only when it is unavailable, ask conversationally: show each alternative in every multiple-choice or sequential question with an explicit index, and explicitly instruct the user to reply with that index or with free-form text.

Present exactly one pending question at a time, with identifiable options and an explicit instruction that the user may choose an option or provide their own answer. Wait for and process that answer before asking another question or executing a dependent step. Treat free-form answers as valid. When the user says they do not know yet (or equivalent), record a gap and continue only with independent questions or steps.

Do not change Claude Code's existing question mechanism or behavior.

## Language

Detect the language of the user's initial (or most recent) message and conduct the ENTIRE interaction in that language — every `AskUserQuestion` prompt, header, option label and description, and every message, summary, and generated file or output. Mirror the user's language exactly and never switch to another language. These instructions are written in English, but that must NOT force the interaction into English: if the user wrote in Spanish, ask and write in Spanish; if they wrote in another language, use that one. The language of the inputs determines the language of the outputs.

## Instructions

You act as an **orchestrator**: you locate the plans, detect dependencies between them, ask the user how to run them, create git worktrees to isolate each plan, dispatch subagents per task, update the files on disk after each result, and manage retries and blocks.

Each plan — even if there is only one — is always executed in an **isolated worktree**. Never touch the main working tree while there are tasks in flight.

---

### Step 0 — Locate plans and verify worktree support

#### 0a. List all plans with pending tasks

Use `Glob` to find all `specs/*/plan.md`. For each one, read it and count how many `- [ ]` lines it has (pending tasks). Discard plans with no pending tasks.

If there is no `plan.md` with pending tasks in `specs/`, inform the user and stop.

#### 0b. Verify worktree support

Check that git worktree is available:

```bash
git worktree list
```

If it fails, inform the user and ignore the worktree functionality — run the plan directly on the current branch (fallback mode: legacy behavior with `git checkout -b`).

#### 0c. Make sure .worktrees/ is in .gitignore

Check whether `.worktrees/` appears in `.gitignore`. If it isn't, add it:

```bash
echo ".worktrees/" >> .gitignore
```

---

### Step 1 — Plan selection

**Always** present the plans with `AskUserQuestion` (multiSelect: true), even if there is only one. The user must explicitly confirm which plans they want to run.

**1. Present the plans found** with `AskUserQuestion`:

- `question`: `"Which plan(s) do you want to develop?"`
- `header`: `"Plans"`
- `multiSelect: true`
- Options: one option per plan found, with `label` = the plan folder name (e.g. `feat-nuevo-campo`) and `description` = `"{N} pending tasks — {relative path}"`. Always add an extra option `"All"` with `description` = `"Run all listed plans"`.

If the user selects none (empty response), inform them: `"You didn't select any plan. You can run /aisy.implement-feature again whenever you want."` and stop.

If they select only one, continue directly to Step 2. If they select several (or "All"), continue to the dependency analysis.

**2. Analyze dependencies between the selected plans**

For each pair of plans, check:

- **Shared files**: extract the file paths mentioned in each `plan.md` (lines with `src/`, `app/`, `components/`, `api/`, `lib/`, etc.). If two plans mention the same file, there is a potential dependency.
- **Cross-references**: read each `requirements.md` in the same folder and look for references to the name or folder of another plan (e.g. "requires X to be implemented").
- **Batch order**: if the name of a batch in plan A matches a dependency described in plan B, there is a direct dependency.

If you detect dependencies between plans:

> ⚠ Dependency warning detected: plans `{A}` and `{B}` modify files in common (`{list}`). Running them in parallel may generate merge conflicts. **Sequential** execution is recommended.

**3. Ask for the execution mode** with `AskUserQuestion`:

```
How do you want to run the {N} selected plans?
```

Options (show the dependency warning in the description if applicable):

| Option | Description |
|--------|-------------|
| **Parallel (2 workers)** | Runs up to 2 plans at a time in independent worktrees. [If there are dependencies: "⚠ Not recommended — there are shared files"] |
| **Sequential** | Runs one plan after another. Slower but with no risk of conflicts. [If there are dependencies: "✅ Recommended"] |

If the user chooses parallel while dependencies have been detected, add a final notice: `"Running in parallel despite the dependencies. If there are merge conflicts they will be reported at the end."` but continue.

---

### Step 2 — Create worktree(s)

For each plan to run, derive the branch name and the worktree path:

- Take the folder name of the `plan.md` (e.g. `fix-campo-descripcion`)
- Branch prefix: first segment (`fix`, `feat`, `chore`)
- Slug: the rest → branch = `{prefix}/{slug}`
- Worktree path: `.worktrees/{slug}` (relative to the project root)

Get the project root:

```bash
git rev-parse --show-toplevel
```

Create the worktree (use `--track` so the new branch starts from the current branch):

```powershell
# PowerShell (Windows)
$root = git rev-parse --show-toplevel
git worktree add "$root/.worktrees/{slug}" -b "{prefix}/{slug}"
```

```bash
# Bash (Unix)
root=$(git rev-parse --show-toplevel)
git worktree add "$root/.worktrees/{slug}" -b "{prefix}/{slug}"
```

If the branch already exists: `git worktree add "$root/.worktrees/{slug}" "{prefix}/{slug}"`.

If it fails for any reason, inform the user and use the fallback mode (`git checkout -b`).

Record the **absolute path** of the worktree for each plan — all the subagents for that plan will receive this path as their working directory.

---

### Step 3 — Execution mode

#### SEQUENTIAL mode (one plan at a time)

For each plan in order, run the **Task loop** (Step 4) until it is completed or blocked. Only when a plan is finished (or definitively blocked) do you start the next one.

#### PARALLEL mode (up to 2 simultaneous workers)

Split the plans into batches of 2. For each batch:

1. **Launch 2 agents in the background** with `run_in_background: true` — one per plan:
   - `subagent_type`: `implementation-agent`
   - `description`: `"Plan {slug}: run all tasks"`
   - `prompt`: the **Worker Prompt** (see below)

2. Wait for both agents to complete (you will be notified automatically).

3. After receiving the results, process each worker's summary and update the state in the respective `plan.md`.

4. Move on to the next batch of 2 if there are more plans.

**Worker Prompt** (for each plan in parallel mode):

```
You are a development task orchestrator. Your goal is to run ALL the pending tasks of the given plan, in the assigned git worktree.

## Plan to run
Plan path: {plan_path}
Worktree path: {worktree_path}
Branch: {branch}

## Instructions

1. Read the plan.md with the Read tool.
2. Extract all `- [ ]` lines (pending tasks).
3. For each task, in order:
   a. If the tag is `@human` (write/migration/deletion on PROD, the merge that triggers the deploy, go/no-go, or a read only possible from a dashboard): do NOT dispatch a subagent nor write to PROD. In parallel mode the worker cannot pause for interaction, so stop the worker and return in the summary that there is a `@human` task pending user action (FINAL_STATUS: BLOCKED, reason: pending human action).
   a-bis. For the rest: dispatch a subagent (Agent tool) with the type indicated by @tag or `general-purpose`.
   b. The subagent must work in the directory: {worktree_path}
   c. The prompt to the subagent includes: task, batch, plan excerpt, and working directory.
   d. If the subagent responds COMPLETED → edit plan.md: `- [ ]` → `- [x]`.
   e. If it is BLOCKED → retry once. If it fails again → `- [blocked]` + reason. Stop.
   f. After completing the last `- [ ]` of a batch → git add -A && git commit (working from {worktree_path}).

4. When all tasks are done:
   - Push the branch from the worktree:
     cd {worktree_path} && git push -u origin {branch}
   - Create the PR against dev with gh pr create.
   - Remove the worktree:
     git worktree remove {worktree_path} --force
   - Read the plan's requirements.md (if it exists) and close the linked issue.

5. Return a final summary with:
   - Completed tasks: N
   - Blocked tasks: N (with reasons)
   - PR URL (or the reason it wasn't created)
   - FINAL_STATUS: COMPLETED | BLOCKED
```

---

### Step 4 — Task loop (sequential mode / single plan)

> This step applies to the current plan in its worktree. The **working directory** for all git commands and subagents is the **worktree path**, not the main repo.

Read the `plan.md` file with `Read`. Extract the `- [ ]` lines. Tell the user how many tasks there are in total and how many are already completed.

Repeat the sub-steps for each pending task in order:

#### 4a. Build the context for the subagent

Prepare a prompt that includes:

1. **The specific task** — exact text (without `- [ ]` or the `@` tag)
2. **The batch it belongs to** — title of the `##` under which the task sits
3. **Plan context** — first 40 lines of the plan.md
4. **Working directory** — absolute path of the **worktree** (not the main repo)
5. **Completion instruction** — clearly indicate whether it is `COMPLETED` or `BLOCKED: <reason>`

```
Task to perform:
"{exact task text}"

Belongs to batch: "{batch title}"

Plan context (excerpt):
---
{first 40 lines of the plan.md}
---

Working directory: {worktree_path}

When you finish, indicate in your final response:
- COMPLETED — if you performed the task successfully
- BLOCKED: <reason> — if you cannot complete it
```

#### 4a-bis. Intercept `@human` tasks (NEVER executed by the agent)

**Before dispatching anything**, look at the task's tag. If it is `· @human` (write/migration/deletion on PROD or its hosting/database dashboard, the merge that triggers the deploy, go/no-go decisions, or reads only possible from a web dashboard):

- **Do not dispatch any subagent** (not even `general-purpose`) — see `CLAUDE.md` § "Production data safety". It is an operation that only the human executes.
- **Present the exact steps to the user** (the plan itself details them) and **pause**, waiting for their confirmation.
- If the user confirms they did it → mark `- [ ]` → `- [x]` and continue. If they postpone it or can't → mark `- [blocked]` with `  - Reason: pending human action` and stop the loop (what follows usually depends on it).
- **Never** write/migrate/delete on PROD yourself to "unblock" the task.

Only if the task is **not** `@human`, continue with 4b.

#### 4b. Dispatch the subagent (first attempt) — non-`@human` tasks only

Invoke with `Agent`:
- `subagent_type`: agent detected by `@tag` or `general-purpose`
- `description`: short text ≤ 60 characters
- `prompt`: the prompt from 4a

#### 4c. Evaluate the result

| Result | Criterion |
|-----------|----------|
| **Completed** | Contains `COMPLETED` or clearly describes that the work was done |
| **Blocked** | Contains `BLOCKED:` or the agent couldn't make progress |
| **Failed** | No useful result — treat the same as blocked |

#### 4d. If COMPLETED

Edit the `plan.md` with `Edit`: `- [ ] {text}` → `- [x] {text}`.

**Commit when closing each batch:** if there are no `- [ ]` left in the same batch, run from the worktree:

```bash
cd {worktree_path} && git add -A && git commit -m "{type}: {batch-slug}"
```

In PowerShell:
```powershell
Set-Location {worktree_path}; git add -A; git commit -m "{type}: {batch-slug}"
```

#### 4e. If BLOCKED or FAILED — retry

Inform: `"Task blocked, retrying: {text}"`.

Repeat 4b with the same prompt plus:

```
⚠ First attempt failed. Reason: {reason}.
Try to resolve the block or find an alternative path.
```

#### 4f. Evaluate the retry

- **COMPLETED**: edit the plan (`- [x]`), continue.
- **Fails again**:
  1. `Edit` in plan.md: `- [ ]` → `- [blocked]`, add a line `  - Reason: {reason}`
  2. Inform the user of the definitive block.
  3. **Stop the loop.**

---

### Step 5 — Finalization of each plan

> Always run from the **worktree path**, not from the main repo.

#### 5a. Clean up development ports

```powershell
# Windows
foreach ($port in @(3000, 3001, 4000, 4321, 5173, 8080)) {
  $conn = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
  if ($conn) { Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue }
}
```

#### 5b. Push, PR and worktree cleanup

**If the plan is 100% completed** (all tasks in `[x]`):

1. Push from the worktree:
   ```bash
   cd {worktree_path} && git push -u origin {branch}
   ```

2. Create a PR against `dev`:
   ```bash
   gh pr create --base dev --title "{type}: {clean plan title}" --body "$(cat <<'EOF'
   ## Changes
   {list of completed batches as bullet points}

   ## Plan
   Generated from `{plan.md path}`

   🤖 Generated with [Claude Code](https://claude.com/claude-code)
   EOF
   )"
   ```

3. Show the PR URL to the user.

4. Close the linked issue (if `requirements.md` contains `> GitHub: #{number}`):
   ```bash
   gh issue comment {number} --body "Implemented in PR {url}. Closing."
   gh issue close {number}
   ```

5. **Remove the worktree** (from the main repo):
   ```bash
   git worktree remove {worktree_path} --force
   ```

**If the plan ended with blocks**: do not create a PR. Remove the worktree anyway:
```bash
git worktree remove {worktree_path} --force
```

#### 5c. Final summary (per plan)

```
## Result: {plan slug}

- Tasks completed in this session: N
- Tasks already completed previously: N
- Blocked tasks: N  ← with reason if applicable
- Remaining pending tasks: N
- PR: {url or "Not created — plan with blocks"}
```

If this was the only plan in the run, close with the next-step block described at the end of Step 6 (print it once, and only if this plan finished with all its tasks done).

---

### Step 6 — Global summary (if multiple plans were run)

After processing all the selected plans, show a consolidated summary:

```
## Global summary

| Plan | Completed | Blocked | PR |
|------|-----------|---------|----|
| {slug-A} | N/M | N | {url or —} |
| {slug-B} | N/M | N | {url or —} |

Worktrees removed: {list}
```

If any plan remained blocked: `"Review the blocks and run /aisy.implement-feature again to continue."`

Show the block below exactly once per run, never once per plan (D-07): print it here, right after this Step 6 global summary, when several plans were run. When only one plan was run and Step 6 is skipped, print it right after that plan's 5c final summary instead, still only once, not after every 5c in a multi-plan run.

Show it only when every plan in the run finished successfully, meaning every plan completed with all its tasks done. If any plan ended with blocked, failed or still pending tasks, show nothing: the "Review the blocks and run /aisy.implement-feature again to continue." line above is what the user sees instead, not this block.

Then close with:

```
✅ Done. Suggested next step:

🧹 /aisy.clean-feature to sync the specs, close the issue and delete the feature folder.
```

Write the block in the user's language, following the `## Language` section at the top of this file. Keep the skill name (`/aisy.clean-feature`) and the emoji exactly as it is, only the words around it get translated.

This block only suggests. Do not run the suggested skill yourself and do not chain into it: stop here and wait for the user to invoke it.

---

### Notes for the orchestrator

- **Worktree always**: even if it's a single plan. Never touch the main working tree while there are tasks in flight.
- **Worktree path**: always use the absolute path to the worktree as the working directory in the prompts to subagents.
- **Do not edit code directly**: you are the orchestrator. Delegate to subagents.
- **One task = one subagent**: do not group tasks.
- **Persist the state to disk after each task**: edit the `plan.md` immediately.
- **If git worktree fails**: use the fallback mode with `git checkout -b` (without worktree) and inform the user.
- **If the subagent_type does not exist**: use `general-purpose` and mention it in the summary.
- **`@human` tasks = never a subagent**: write/migration/deletion operations on PROD, the merge that triggers the deploy, and go/no-go decisions are executed **only by the user** (see `CLAUDE.md` § "Production data safety"). The orchestrator presents the steps, pauses, and waits for confirmation; it never delegates them nor executes them itself.
- **Parallel with dependencies**: if the user chooses parallel with a dependency warning, proceed but monitor the merge results — if there are conflicts, report them clearly at the end.
