---
name: aisy.clean-feature
description: Cleans up feature/fix folders whose plan.md is complete. For each folder, it audits and aligns the root specs (product-spec, tech-spec, css-spec, ui-spec, infra-spec, security-spec, roadmap) with the changes introduced, updates the ones that are out of date, closes the associated GitHub issue (with a comment and a link to the branch) and deletes the folder. It finishes with a chore commit and a push to dev. Trigger when the user says "clean-feature", "cleanup", "limpia las carpetas", "limpia los specs", "alinea los specs", "clean up the folders", "clean up the specs", "align the specs" or invokes /aisy.clean-feature.
---

## Codex question fallback

When running in Codex, use `ask_user_question` when it is available. Only when it is unavailable, ask conversationally: show each alternative in every multiple-choice or sequential question with an explicit index, and explicitly instruct the user to reply with that index or with free-form text.

Present exactly one pending question at a time, with identifiable options and an explicit instruction that the user may choose an option or provide their own answer. Wait for and process that answer before asking another question or executing a dependent step. Treat free-form answers as valid. When the user says they do not know yet (or equivalent), record a gap and continue only with independent questions or steps.

Do not change Claude Code's existing question mechanism or behavior.

## Language

Detect the language of the user's initial (or most recent) message and conduct the ENTIRE interaction in that language — every `AskUserQuestion` prompt, header, option label and description, and every message, summary, and generated file or output. Mirror the user's language exactly and never switch to another language. These instructions are written in English, but that must NOT force the interaction into English: if the user wrote in Spanish, ask and write in Spanish; if they wrote in another language, use that one. The language of the inputs determines the language of the outputs.

## Instructions

Follow these steps in order.

---

### Step 0 — Locate folders with a completed plan

Use `Glob` to find all `specs/*/plan.md`. For each one:

1. Read it with `Read`.
2. Count the `- [ ]` lines (pending tasks).
3. Count the `- [x]` lines (completed tasks).
4. Count the `- [blocked]` lines (blocked tasks).

**"Completed" criterion:** the plan has no `- [ ]`. It may have `- [blocked]` (note it, but include it).

If there is no completed plan, inform the user and stop.

---

### Step 1 — Present and confirm with the user

#### If there is exactly 1 completed plan

Report: `"Completed folder found: {path} ({N} tasks completed{, M blocked if applicable})"`. Proceed automatically to Step 2.

#### If there are 2 or more completed plans

Use `AskUserQuestion` (multiSelect: true) to ask which ones they want to process:

```
Which completed folders do you want to clean up?
```

Options: one per folder (format `specs/{name}/ — {N} tasks completed`) + "All".

---

### Step 2 — Extract context from each selected folder

For each selected folder:

1. Read `specs/{folder}/requirements.md` (if it exists).
2. Read `specs/{folder}/plan.md`.
3. Run:
   ```bash
   git log --oneline --all -- "specs/{folder}/"
   ```
4. Extract a **summary of changes**: which files were touched, what new behavior was introduced, what was fixed. Synthesize it into 3-5 bullets. Save it as `context_{folder}` for the following steps.
5. **Extract the issue number**: search in `requirements.md` and in `plan.md` itself for a reference to the GitHub issue (patterns: `#\d+`, `issues/\d+`, `closes #\d+`, `fixes #\d+`, or an `issue:` field in the frontmatter). If you find it, save it as `issue_num_{folder}`. If there is no reference, mark it as `null`.

---

### Step 3 — Audit the alignment of the root specs

Read the following root specs with `Read`:

| File | Audit when… |
|---------|-----------------|
| `specs/product-spec.md` | The plan added user-visible functionality (feat) |
| `specs/tech-spec.md` | The plan changed the stack, technical patterns, dependencies or architecture |
| `specs/css-spec.md` | The plan modified styles, UI components or visual tokens |
| `specs/ui-spec.md` | The plan added or changed screens, interactions or flows |
| `specs/infra-spec.md` | The plan touched vercel.json, env vars, build scripts or integrations |
| `specs/security-spec.md` | The plan modified auth, validations, proxies or permissions |
| `specs/roadmap.md` | Any completed plan (mark delivered items) |

For `spec` ∈ `product-spec`, `tech-spec` only, track `spec_status_{spec}_{folder}` with these assignment rules: if the row above does not apply to this plan, or the spec file does not exist on disk (see the "Optional specs" note), set it to `not_applicable`; if the subagent responds "ALIGNED — no changes needed", set it to `aligned`; if the subagent finds one or more misalignments, set it to `pending` (resolved in Step 4). This tracking is scoped to `product-spec.md`/`tech-spec.md` only (FR-002); the other 5 specs (`css-spec`, `ui-spec`, `infra-spec`, `security-spec`, `roadmap`) keep today's behavior with no status variable.

For each relevant spec, launch a `general-purpose` subagent with the following prompt:

```
You are a technical documentation reviewer. Your goal is to identify which sections of the spec are misaligned with the actual changes introduced by an already-completed plan.

## Spec to review
Path: {spec_path}
Full content:
---
{spec content}
---

## Changes introduced by the completed plan
Folder: specs/{folder}/
Summary of changes:
{context_{folder}}

Content of plan.md:
---
{content of plan.md}
---

{if requirements.md exists:}
Content of requirements.md:
---
{content of requirements.md}
---

## Your task
Identify ONLY the sections or lines of the spec that are misaligned with the actual changes. For each misalignment found, indicate:

1. **Affected section**: name of the section or exact line
2. **What it currently says**: literal excerpt
3. **What it should say**: proposed updated text
4. **Why**: in one line

If the spec is fully aligned with the changes, respond: "ALIGNED — no changes needed."

Do not propose cosmetic changes or improvements unrelated to the analyzed plan.
Be conservative: only flag as misaligned what contradicts or ignores what the plan implemented.
```

Collect the results. If the subagent responds "ALIGNED", mark it and move on to the next one.

---

### Step 4 — Update misaligned specs

For each spec where the subagent detected misalignments, launch an `implementation-agent` subagent with:

```
Update the spec {spec_path} to align it with the changes introduced by the completed plan.

## Changes to apply
{numbered list of misalignments detected in Step 3, with current text and proposal}

## Rules
- Use `Read` to read the file before editing it.
- Use `Edit` to make each change surgically — never rewrite entire sections unless strictly necessary.
- Update the `Updated` field in the spec's metadata block (format `YYYY-MM-DD`, today's date: {today}).
- Do not add or remove sections that are not in the list of changes.
- If the change is adding an item to a list (e.g. in the roadmap: marking an item as completed), do it with a minimal Edit.
- At the end, confirm which lines you edited.
```

For the two specs (`product-spec`, `tech-spec`) whose `spec_status_{spec}_{folder}` was `pending`, resolve it now: if the subagent confirms all listed changes were applied, with the edited lines enumerated and no reported errors, set it to `updated`; if the subagent reports it could not apply a change, hits an error, or its final response does not confirm the edits, set it to `failed`. This resolution feeds Step 6.5's gate (FR-004, FR-005) and does not apply to the other optional specs.

If the spec has changes in `roadmap.md` (items to mark as delivered), apply the same criterion: minimal `Edit`, mark the item with `[x]` or add the delivery date if it fits the roadmap's format.

---

### Step 5 — Confirm before deleting

Before deleting the folders, show the user the summary of what was done:

```
## Summary before deleting

### Updated specs
- specs/product-spec.md — {N changes}
- specs/tech-spec.md — ALIGNED
- ...

### Folders to delete
- specs/{folder-A}/  ({N tasks completed)
- specs/{folder-B}/  ({N tasks completed})
```

Use `AskUserQuestion` to ask for confirmation:

```
Shall I proceed to delete the folders and make the commit?
```

Options:
- **Yes, delete and commit** — Delete the folders and push to dev
- **Don't delete yet** — Close without deleting (the specs are already updated)

If the user chooses not to delete, report that the specs are updated and stop.

---

### Step 6 — Delete folders

For each confirmed folder, run in **PowerShell**:

```powershell
Remove-Item -Recurse -Force "specs/{folder}"
```

Confirm with `Glob "specs/*/plan.md"` that the folders no longer exist.

---

### Step 6.5 — Close issues on GitHub

For each deleted folder where `issue_num_{folder}` is not `null`:

0. **Alignment gate**: check `spec_status_product-spec_{folder}` and `spec_status_tech-spec_{folder}` for this folder. The issue may close only if **both** are in `{not_applicable, aligned, updated}`. If **either** is `failed`, skip steps 1-3 entirely for this folder, and instead append an entry to the `pending_alignment_folders` list (initialized empty once, before the per-folder loop) with the folder path, `issue_num_{folder}`, and the name(s) of the failed spec(s) (`product-spec.md` and/or `tech-spec.md`). This gate is evaluated independently per folder — a `failed` status in one folder does not affect the gate outcome for any other folder. This gate does not affect Step 6's folder deletion, which already ran and was already confirmed in Step 5; it only controls whether the issue is closed in the steps below. If no status was assigned for a spec, treat it as `not_applicable`.

If the gate above passed for this folder, continue:

1. Check whether the issue is open:
   ```bash
   gh issue view {issue_num} --json state --jq '.state'
   ```
   If it returns `CLOSED`, skip it.

2. If it is open, verify whether the development branch is linked to the issue. Check whether there is any PR that references it:
   ```bash
   gh pr list --search "#{issue_num}" --json number,title,headRefName,state
   ```
   If there is no closed or merged PR referencing it, add the link manually with:
   ```bash
   gh issue develop {issue_num} --branch "dev" --repo {owner}/{repo}
   ```
   (This links the `dev` branch to the issue on GitHub. If the command fails because the `gh` version does not support it, skip it and note it in the final summary.)

3. Close the issue with a comment that includes the summary of what was implemented:
   ```bash
   gh issue close {issue_num} --comment "$(cat <<'EOF'
   ✅ Implemented and closed.

   {context_{folder} — the 3-5 bullets of the summary of changes}

   Specs folder deleted: `specs/{folder}/`
   Related commits:
   {output of git log --oneline --all -- "specs/{folder}/", maximum 5 lines}
   EOF
   )"
   ```

If any issue fails to close, report the error but continue with the rest.

---

### Step 7 — Commit and push to dev

Build the commit message listing the deleted folders and the modified specs:

```powershell
git add -A
git commit -m "$(cat <<'EOF'
chore: clean up completed folders and update specs

Deleted folders:
{list of folders, one per line with a dash}

Updated specs:
{list of modified specs, one per line with a dash; omit if none}

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

In PowerShell, use a here-string `@'...'@`:

```powershell
git commit -m @'
chore: clean up completed folders and update specs

Deleted folders:
- specs/{folder-A}
- specs/{folder-B}

Updated specs:
- specs/tech-spec.md
- specs/infra-spec.md

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
'@
```

Then:

```powershell
git push origin dev
```

If the push fails due to divergence, inform the user with the exact error and stop — do not do `--force`.

---

### Step 8 — Final summary

```
## Cleanup completed

Folders deleted: {N}
{list}

Specs updated: {N} / {total audited}
{list with number of changes per spec}

Issues closed: {N}
{list: "#NNN — title — linked to branch: Yes/No"}

Specs pending alignment: {N}
{one line per entry: "specs/{folder}/ — issue #{issue_num} kept open — pending: {comma-separated failed spec names}"}

Commit: {short hash} — {message}
Push: ✅ dev updated
```

The "Specs pending alignment" section is populated from the `pending_alignment_folders` list built in Step 6.5; omit the section entirely when that list is empty. The "Issues closed" count must exclude any folder listed in "Specs pending alignment".

If the user chose not to delete in Step 5: omit the folders section and the commit.

This is the last step of the loop. Do not print a next-step suggestion block and do not point the user at another skill.

---

### Notes

- **Only delete what is `[x]`**: if a plan has `- [blocked]` but no `- [ ]`, include it but warn the user in Step 1.
- **Do not modify specs for changes that do not come from the plan**: the audit is surgical, not a general refactor.
- **Today's date**: get the current date with `Get-Date -Format "yyyy-MM-dd"` before starting.
- **Optional specs**: if any of the specs listed in Step 3 does not exist on disk, skip it silently.
- **One subagent per spec**: do not try to audit multiple specs in the same subagent.
- **Issue not found**: if there is no `issue_num` for a folder, silently skip the closing step for that folder.
- **`gh issue develop` optional**: if the `gh` version does not support the `develop` subcommand, skip it and note it in the final summary as "linked to branch: No (command not available)".
- **Alignment gate scope**: the Step 6.5 gate only blocks issue closing on an explicit `failed` status for `product-spec.md`/`tech-spec.md` — it never blocks on `not_applicable` or `aligned`. It is scoped strictly to those two specs (FR-002) and never blocks folder deletion (Step 6), only issue closing (Step 6.5).
