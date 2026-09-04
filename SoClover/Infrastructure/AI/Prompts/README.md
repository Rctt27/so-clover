# Prompt Authoring Guide

This directory holds the LLM prompt templates used by the AI player feature
(Epic 05 of the AI Players Integration). Each language gets its own subfolder
(`fr/`, `en/`, `pt/`) containing the template files below.

## The three prompt files per language

| File | Used by | Selected when |
|---|---|---|
| `board-clues.md` | `GenerateAIClues.Handler` (PerBoard) | `Llm.GenerationMode = PerBoard` — one LLM call covering all remaining directions, JSON `{ clues: [...] }` |
| `board-clues-per-direction.md` | `GenerateAICluesPerDirection.Handler` | `Llm.GenerationMode = PerDirection` (**the current default in both Development and Production**) — one call per direction, JSON `{ direction, candidates, explanation, clueWord }` |
| `board-clues-per-direction.reasoning.md` | idem | `GenerationMode = PerDirection` **and** `Llm.ReasoningEnabled = true` |

**The pipeline determines the prompt** — it is never inferred from how many
directions remain, so a partial PerBoard retry cannot leak into the
per-direction file.

### The reasoning variant is a whole file, not a section

When a language's provider injects a non-null reasoning path (all three do
today), `BuildSingleDirectionCluePrompt` loads `*.reasoning.md` **instead of**
the standard file. The file *is* the reasoning variant: no `# REASONING`
section is appended to it, and a `# REASONING` section written in the standard
per-direction file would be dead code. **Fail-fast**: if the path is injected
but the file is missing from disk, `BuildSingleDirectionCluePrompt` throws
`FileNotFoundException`.

The legacy path — append the `# REASONING` section to the SYSTEM prompt —
only applies to a language whose reasoning path is `null`, and to
`board-clues.md`, which has no dedicated reasoning file and therefore *does*
carry an inline `# REASONING` section.

## File format

Sections are delimited by H1 headings, with an optional YAML-like frontmatter
at the very top. Only `version:` is parsed out of the frontmatter (as an `int`,
exposed as `AiCluePromptBundle.PromptVersion`); every other key is documentary.

```
---
version: 1
language: fr
description: <one-line description>
---

# SYSTEM
<content of the system prompt>

# USER
<content of the user prompt, may contain placeholders like {{boardLayout}},
 {{directionsToResolve}}, {{allBoardWordsList}}, {{retryFeedback}}>

# REASONING
<appended to the SYSTEM prompt when reasoning is on AND no dedicated
 .reasoning.md file is injected for this language>

# RETRY_FEEDBACK
<content injected when there are previous rejected attempts; may contain
 placeholder {{rejectedAttemptsByDirection}}>
```

`FilePromptLoader` accumulates **only** `SYSTEM`, `USER`, `REASONING` and
`RETRY_FEEDBACK`. Any other H1 is silently dropped — which is what makes a
`# NOTES` section usable for maintainer documentation inside the prompt file
itself. A missing section is tolerated and yields an empty string; a missing
file throws `FileNotFoundException`.

The `# RETRY_FEEDBACK` section is **only included** in the final user prompt
when at least one rejected attempt is provided in the context (otherwise it
is fully omitted, including any leading newline).

## Placeholders

| Placeholder | Substituted by |
|---|---|
| `{{boardLayout}}` | A bullet list of the 4 cards (TopLeft, TopRight, BottomRight, BottomLeft) with their 4 oriented words each |
| `{{directionsToResolve}}` | **PerBoard only.** A bullet list of the directions in `RemainingDirections`, each with the 2 adjacent words |
| `{{directionToResolve}}` | **PerDirection only** (singular). Same rendering, but `RemainingDirections` holds exactly one direction |
| `{{allBoardWordsList}}` | A bullet list of all 16 board words (4 cards × 4 oriented words) |
| `{{retryFeedback}}` | The fully-rendered RETRY_FEEDBACK section (or empty when no rejections) |
| `{{rejectedAttemptsByDirection}}` | Per-direction list of rejected attempts (most-recent first, max 3 per direction) |

Substitution is plain `string.Replace` — no templating engine. Unknown
placeholders are left as-is, so the test suite asserts no `{{` survives in the
final user prompt.

The surrounding wording of those lists is **not** in the `.md`: it comes from
the `AiCluePromptLabels` record carried by each provider in C#. Translating a
language therefore means translating both the `.md` files and those 7 format
strings.

## Versioning

`version:` denotes a **content generation, shared across languages** — not the
history of one file. FR, EN and PT all sit at version 5 of the per-direction
family because they carry the same rules; that is what makes the
`PromptVersion` field of the "AI clue LLM call completed" log comparable
between languages, and what makes an eval-ledger line mean anything.

Two rules follow:

- **Touching a prompt's content means bumping its `version:` in the same
  move.** Otherwise two different prompts report the same identity and every
  measurement taken across that boundary is silently incomparable.
- A language may skip numbers when it catches up with another (EN went
  straight from 1 to 5); it must never reuse a number for different content.

`SoClover.Tests/Ai/PackagedPromptGenerationTests.cs` pins the current
generation of each family for every language, so an edit without a bump shows
up as a red test.

## Hot-reload

Files are copied to the build output (`bin/Debug/.../Infrastructure/AI/Prompts/`)
by the recursive glob in `SoClover.csproj` — a new language folder needs no
csproj edit. Edit the `.md` file in `bin/` while the app is running and the
next `IAiCluePromptProvider` call sees the new content (`FilePromptLoader`
invalidates its cache on `LastWriteTimeUtc` change).

## Adding a new language

1. Create `Prompts/<langcode>/` with the **three** files above, translated from
   the `fr/` originals. Keep placeholders, direction names (`Top`/`Right`/
   `Bottom`/`Left`) and JSON keys (`direction`, `candidates`, `explanation`,
   `clueWord`) untouched — they are contract values parsed by the backend, not
   prose. Carry over the `version:` of the generation you translated.
2. Translate the **examples**, do not transpose them. The relations that hinge
   on the language itself — polysemy (relation 11), cross specialization
   (relation 12), the "language and wordplay" pass, the root example of
   absolute rule 3 — have no literal equivalent. Pick native ones with the same
   property, and record the substitution table in the file's `# NOTES` section
   (see `en/` and `pt/` for the format).
3. Create `<Language>AiCluePromptProvider : FileAiCluePromptProvider` in this
   directory, mirroring `PortugueseAiCluePromptProvider`: three default paths,
   the dictionary key as `Language`, and a localized `AiCluePromptLabels`.
4. Wire it into `AiCluePromptProviderFactory.GetFor` with a
   `TextNormalizer.Normalize` prefix branch. Normalize strips diacritics, so
   pick a prefix that covers both the dictionary key and the native spelling.
5. **Add the clue validator too**, otherwise `ClueValidatorFactory` falls back
   to `NullClueValidator`: the AI's clues are never rejected and its
   retry/`retryFeedback` loop never fires. Add
   `<Language>OffClueValidator : SubstringClueValidator` plus one entry in
   `SemanticValidationSupport.ByPrefix`, and mirror the prefix in the frontend
   `client/src/core/clueValidation.ts`.
6. Add tests under `SoClover.Tests/Ai/` mirroring
   `PortugueseAiCluePromptProviderTests`, and add the language to
   `PackagedPromptGenerationTests.Languages()` and to the `Validators` array of
   `DeterministicWordDictionaryTests`.

No UseCase or Domain flow code is impacted.
