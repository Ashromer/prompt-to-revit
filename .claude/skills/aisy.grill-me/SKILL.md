---
name: aisy.grill-me
description: Critical interrogation of a document to reduce gaps, clarify decisions, and detect inconsistencies. When finished, it rewrites the document with everything learned. Requires an input document. Trigger when the user says "grill me", "interrógate", "interrogatorio", "analiza gaps", "interrogate me", "analyze gaps", or invokes /aisy.grill-me.
---

## Codex question fallback

When running in Codex, use `ask_user_question` when it is available. Only when it is unavailable, ask conversationally: show each alternative in every multiple-choice or sequential question with an explicit index, and explicitly instruct the user to reply with that index or with free-form text.

Present exactly one pending question at a time, with identifiable options and an explicit instruction that the user may choose an option or provide their own answer. Wait for and process that answer before asking another question or executing a dependent step. Treat free-form answers as valid. When the user says they do not know yet (or equivalent), record a gap and continue only with independent questions or steps.

Do not change Claude Code's existing question mechanism or behavior.

## Language

Detect the language of the user's initial (or most recent) message and conduct the ENTIRE interaction in that language — every `AskUserQuestion` prompt, header, option label and description, and every message, summary, and generated file or output. Mirror the user's language exactly and never switch to another language. These instructions are written in English, but that must NOT force the interaction into English: if the user wrote in Spanish, ask and write in Spanish; if they wrote in another language, use that one. The language of the inputs determines the language of the outputs.

## Instructions

Follow these steps in strict order. This skill is generic: it works with any type of document (spec, note, sentence, idea, roadmap, design, etc.).

---

### Step 0 — Obtain the input document

The document can arrive in three ways:

1. **File path** passed as an argument or mentioned in the message → read it with `Read`.
2. **Content pasted directly** into the user's message → use it as-is.
3. **No document** → use `AskUserQuestion` with a free-text question:
   - Question: "What is the document or text you want me to analyze? Paste the content or provide the file path."
   - Suggested options: `I'll paste the content now`, `I'll provide the file path`
   - If the user provides a path, read it with `Read`. If they paste content, use it.

Once you have the document, keep it in mind as **ORIGINAL_DOCUMENT** (along with its path if there is one).

---

### Step 1 — Determine the depth of the interrogation

Use `AskUserQuestion` with a single question:

**Question:** "How deep do you want the interrogation to be?"

| Option | Description |
|--------|-------------|
| **4 questions** | Only the most critical. Blocking decisions and fatal gaps. Quick session (5 min). |
| **6 questions** | Critical + important decisions. Balance between speed and coverage. |
| **12 questions** | Exhaustive. Critical, important, and edge cases. Full interrogation. |

Save the chosen number as **N_QUESTIONS**.

---

### Step 2 — Analyze the document internally

**Do not show this analysis to the user.** It is internal work to prepare the questions.

Read the document carefully and detect:

1. **Fatal gaps** — missing information without which the document cannot be executed. Examples: undefined objective, missing target user, undefined scope, technical decision not made.

2. **Internal inconsistencies** — points that contradict each other within the same document. Examples: a requirement that clashes with a constraint, a flow that describes two different behaviors for the same case.

3. **Decisions not made** — places where the document assumes something but does not make it explicit, or where there are two equally valid options and neither was chosen. Examples: "X or Y will be used as appropriate" with no selection criteria.

4. **Implicit assumptions** — things the author takes for granted but that an external reader could not infer without additional information.

5. **Scope ambiguities** — sections where it is not clear what is inside and what is outside the scope.

Create an internal list of **all findings**, ordered by criticality:

```
HIGH CRITICALITY → fatal gaps + inconsistencies
MEDIUM CRITICALITY → decisions not made + implicit assumptions
LOW CRITICALITY → scope ambiguities + minor details
```

From that list, select the **N_QUESTIONS** most critical ones. If N=4, take only the 4 highest. If N=12, also cover the medium and low ones.

For each selected question, prepare:
- The question itself (clear, direct, without the author's jargon)
- 2-4 plausible options based on the document's context (the user can always choose "Other" for a free-form answer)

---

### Step 3 — Launch the interrogation in rounds

Split the N_QUESTIONS into rounds of **at most 4 questions per call** to `AskUserQuestion`.

| N_QUESTIONS | Rounds |
|-------------|--------|
| 4 | 1 round of 4 |
| 6 | 1 round of 4 + 1 round of 2 |
| 12 | 3 rounds of 4 |

**Before the first round**, write the user a brief message with:
- How many questions are coming in total
- The ordering criterion: "From the most critical to the most granular"
- One line indicating what type of gaps you have detected (without revealing the questions yet)

Example:
> I've detected 2 inconsistencies, 3 decisions not made, and 1 scope gap. Let's start with the 6 most critical questions.

**Rules for phrasing the questions:**
- Direct and to the point. This is an interrogation, not a courtesy interview.
- Each question targets a single concrete finding.
- The options should be mutually exclusive whenever possible.
- If the question is about an inconsistency, name it explicitly: "The document says X in section A, but Y in section B. Which is the correct version?"

Wait for the answers of each round before launching the next. Accumulate all the answers as **ACCUMULATED_ANSWERS**.

---

### Step 4 — Rewrite the document

Once all rounds are complete, rewrite the **ORIGINAL_DOCUMENT** integrating:

1. All the **ACCUMULATED_ANSWERS** from the interrogation.
2. Resolution of the detected inconsistencies (use the user's answer; if there is none, mark the inconsistency as `[UNRESOLVED]`).
3. The implicit assumptions, now made explicit as direct statements.
4. Gaps that remain open marked with `> ⚠ Unresolved gap: {brief description}`.

**Rewriting principles:**
- Keep the structure and tone of the original document.
- Do not add empty sections or boilerplate that did not exist before.
- If the original document was a sentence or brief note, rewrite it as an equally brief document but without gaps.
- If it was an extensive spec, keep the same level of detail and length, only enriching it.
- Do not invent information the user did not provide. If a gap remains open after the interrogation, mark it with `⚠`.

**Destination of the rewritten document:**
- If there was a **file path**: write the result to that same path with `Write` (overwrite). Inform the user before doing so.
- If the document arrived as pasted text: show the result directly in the chat, formatted in a markdown code block.

---

### Step 5 — Final summary

When finished, write the user a concise summary:

```
✓ Document rewritten: {path or "shown in chat"}
Gaps resolved: {n}
Gaps open: {n} (marked with ⚠)
Inconsistencies resolved: {n}
```

If open gaps with `⚠` remain, briefly list which ones they are so the user knows what is left to complete.
