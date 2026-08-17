---
name: aisy.for-dummies
description: Explains one or more concepts from a vague prompt, link, or document like an expert teacher, with examples and up to 3 optional free resources per concept. Trigger when the user says "for dummies", "explícamelo para dummies", "explain it like I'm five", "explícamelo como si no supiera nada", "no entiendo este concepto", "I don't get this concept", or invokes /aisy.for-dummies.
---

## Codex question fallback

When running in Codex, use `ask_user_question` when it is available. Only when it is unavailable, ask conversationally: show each alternative in every multiple-choice or sequential question with an explicit index, and explicitly instruct the user to reply with that index or with free-form text.

Present exactly one pending question at a time, with identifiable options and an explicit instruction that the user may choose an option or provide their own answer. Wait for and process that answer before asking another question or executing a dependent step. Treat free-form answers as valid. When the user says they do not know yet (or equivalent), record a gap and continue only with independent questions or steps.

Do not change Claude Code's existing question mechanism or behavior.

## Language

Detect the language of the user's initial (or most recent) message and conduct the ENTIRE interaction in that language — every `AskUserQuestion` prompt, header, option label and description, and every message, summary, and generated file or output. Mirror the user's language exactly and never switch to another language. These instructions are written in English, but that must NOT force the interaction into English: if the user wrote in Spanish, ask and write in Spanish; if they wrote in another language, use that one. The language of the inputs determines the language of the outputs.

## Purpose

This skill is a teacher, not a search engine or an implementer. It takes something the user only half understands — a term, an idea, a link, a document — finds the concepts hiding inside it, and explains each one until it clicks, always with a real example. It caps itself at 3 concepts per round so each one gets a proper explanation instead of a glossary entry. Links are a bonus, never the explanation.

## Instructions

Follow these steps in strict order. This skill is generic — the input can be a single word, a rambling paragraph, a URL, or an entire document.

---

### Step 0 — Capture what the user wants explained

The input can arrive in four ways:

1. **Command argument** (text after `/aisy.for-dummies`) → use it directly.
2. **A file path or an attached/mentioned document** → read it with `Read`.
3. **A URL** → do a `WebFetch`, asking for the page's main topic and the terms it revolves around. If the fetch fails (403, paywall, login) say so clearly and ask the user to paste the relevant text — **never guess the content from the URL slug or the domain**.
4. **Nothing usable** → an `AskUserQuestion`:
   - `question`: `"What do you want me to explain?"`
   - `header`: `"Explain"`
   - Options: `A technology or framework` · `An idea or a term I ran into` · `A link or a document I'll paste now`

Save the result as **RAW_INPUT**.

---

### Step 1 — Identify the concepts (internal, never shown)

**Do not show this analysis to the user.**

List the candidate concepts in RAW_INPUT: named technologies, frameworks, products and services; technical or domain-specific terms and acronyms; and the central ideas the input revolves around. Then clean the list: merge synonyms and variants into a single concept (e.g. "K8s" and "Kubernetes" are one); drop terms that are only incidental context; drop terms the user's own framing shows they already know. Order what remains by how central it is to what the user is actually asking for.

Then the branching table below — **evaluate the rows in order and stop at the first match**:

| Case | Condition | What to do |
|---|---|---|
| **A — Named concepts** | RAW_INPUT explicitly names **1 or 2** concepts as its topic (a technology, a framework, a specific idea) | Those *are* the concepts. Go straight to Step 3 — **ask nothing**. Do not pad the list with sub-terms or related technologies to reach 3. |
| **B — Already within the cap** | 3 or fewer concepts remain after cleanup | Take them all. Go to Step 3 — **ask nothing**. |
| **C — Too many** | More than 3 concepts remain | Go to Step 2 and let the user choose. |

> Exactly 3 concepts is **not** a case for asking — only strictly more than 3 is. And Case A always wins over Case C: if the user named a technology, explain that technology, even if a dozen related terms could be extracted from the surrounding text.

Save the result as **CONCEPTS** (never more than 3).

---

### Step 2 — Let the user pick which concepts (only when more than 3 were found)

**First**, print the full list as plain text (not inside the question), numbered:

```
I found {N} concepts in what you shared:

1. {concept} — {what it is, in one line}
2. {concept} — {what it is, in one line}
...

I can do 3 properly in one round. Which ones do you want?
```

**Then** an `AskUserQuestion` call:
- `question`: `"You mentioned {N} concepts. Which ones do you want me to explain? (up to 3)"`
- `header`: `"Concepts"`
- `multiSelect: true`
- Options: one per concept for the **4 most central**, with `label` = the concept's short name and `description` = the same "what it is" line used in the printed list. The remaining concepts are reachable via the automatic "Other" option, by number or name.

**Rules for the response:**
- If the user selects **more than 3**, keep the first 3 in the order presented and tell them which ones you're deferring to a later round — do not ask again.
- If the user **selects nothing meaningful** or the answer is empty, default to the **3 most central concepts**, say which ones you picked, and continue — do not ask again.
- This question is asked **at most once per invocation**. Never open a second selection round.

Save the result as **CONCEPTS** (never more than 3) and save the ones not addressed as **DEFERRED** for Step 5.

---

### Step 3 — Research each concept, official sources first

> Light research, not an audit: enough to get the facts right and offer real links, not a literature review.

1. Build **1-2 search queries per concept** in CONCEPTS, and launch **all `WebSearch` calls in a single message** so they run in parallel.
2. Rank what comes back, official first:
   1. The provider's/maintainer's own documentation site
   2. The project's own repository or README
   3. The governing specification or standard
   4. Everything else — articles, tutorials, videos, aggregators
3. `WebFetch` at most **1-2** sources total, and only when a top-level official page would materially sharpen the explanation.
4. Keep **2-4 sources per concept**, recording for each: title, URL, whether it's official, and the one key fact it provides.

**If research finds nothing usable, or search tools are unavailable:** say so explicitly in the output, explain the concept from your own knowledge, and **omit the Resources block for that concept**. Never invent a source, a URL, a version number, or a statistic.

Save as **RESEARCH**.

---

### Step 4 — Explain, one concept at a time

Order CONCEPTS so prerequisites come first. If a concept needs a term the user won't know, define it inline in a clause — **don't promote it to one of the 3 slots**.

For each concept, in order, produce exactly this block:

```
## {Concept}

**In one sentence:** {plain-language definition — no jargon at all}

**The analogy:** {an everyday comparison a non-technical person would get immediately}

**How it actually works:**
- {3-6 bullets, now using the real terminology, each term defined inline the first time it appears}

**Example:** {one concrete example — a real scenario, a short code block, or a numbered walkthrough. Not a restatement of the definition.}

**Where people get confused:** {the most common misconception, plus what to think instead — and why the wrong idea is so tempting}

**Resources:**
- [{title}]({url}) — {type: docs / article / video} · {why it's worth it, in a handful of words}
```

**Rules for the explanation:**
- Write for an intelligent person with zero background in this specific field — that's the whole premise of the skill. Never ask what level they're at; adapt only from what the input itself already indicates.
- **The example is mandatory.** A concept explained without a concrete example is not finished.
- Aim for roughly one screen per concept (~200-350 words). If it needs more, the concept was too broad — say so and offer to split it in Step 5.
- Move to the next concept only once the current one is complete. Cover every concept in CONCEPTS.

**Rules for the Resources block:**
- **Optional and capped at 3 per concept.** Zero is a perfectly valid number.
- Only URLs that actually appeared in RESEARCH in Step 3. Never write a URL from memory.
- "Free and immediately accessible" means it **opens straight to the content**: no login, no signup, no paywall, no purchase. If you can't tell, don't include it.
- Prioritize, in this order: official documentation page → official tutorial or video → free, quality article or short video ("knowledge pill").
- **The explanation above must stand entirely on its own.** Never write "see the link for more details," never let a resource carry part of the explanation, and never pad the list to reach 3. If nothing qualifies, omit the `**Resources:**` line entirely.

---

### Step 5 — Close by offering to go deeper

After the last concept, close with:

> Want me to go deeper on any of these? I can zoom into a specific part of `{concept}`, walk through a fuller example, or compare it against the alternatives — just say which one.

If **DEFERRED** is not empty, add a line:

> You also mentioned {list of deferred concepts} — say the word and I'll cover those in a new round.

If the user accepts the offer: re-run Step 3 and Step 4 for that concept alone, in more depth (more mechanism, more edge cases, a richer example) — the 3-concept-per-round cap still applies.

This skill is standalone: **do not** print a "next skill" suggestion block.

## Constraints

- Never more than 3 concepts per round, whatever the input contains.
- At most one selection question per invocation (Step 2), and only when more than 3 concepts were found. If the user named 1 or 2 concepts, ask nothing at all.
- Never ask the user what their level is — assume no background and adapt from the input.
- Every concept gets a concrete example. No example, not finished.
- Research is mandatory but light: official documentation first, 2-4 sources per concept.
- Never invent sources, URLs, version numbers, or figures. If research came back empty, say so.
- Resources are optional, capped at 3 per concept, and must be free and immediately accessible. The explanation must be complete without them.
- This skill writes no files and implements no code — its whole output is the explanation in the chat.
