# Prompt Authoring Guide

This directory holds the LLM prompt templates used by the AI player feature
(Epic 05 of the AI Players Integration). Each language gets its own subfolder
(`fr/`, `en/`, ...) containing one or more `.md` template files.

## File format

Each prompt file has 3 sections delimited by H1 headings, with an optional
YAML-like frontmatter at the very top (the frontmatter is purely documentary —
the loader ignores it):

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

# RETRY_FEEDBACK
<content injected when there are previous rejected attempts; may contain
 placeholder {{rejectedAttemptsByDirection}}>
```

The `# RETRY_FEEDBACK` section is **only included** in the final user prompt
when at least one rejected attempt is provided in the context (otherwise it
is fully omitted, including any leading newline).

## Placeholders

| Placeholder | Substituted by |
|---|---|
| `{{boardLayout}}` | A bullet list of the 4 cards (TopLeft, TopRight, BottomRight, BottomLeft) with their 4 oriented words each |
| `{{directionsToResolve}}` | A bullet list of the directions in `RemainingDirections`, each with the 2 adjacent words (and their source card+face) |
| `{{allBoardWordsList}}` | A bullet list of all 16 board words (4 cards × 4 oriented words) |
| `{{retryFeedback}}` | The fully-rendered RETRY_FEEDBACK section (or empty when no rejections) |
| `{{rejectedAttemptsByDirection}}` | Per-direction list of rejected attempts (most-recent first, max 3 per direction) |

Substitution is plain `string.Replace` — no templating engine. Unknown
placeholders are left as-is, so the test suite asserts no `{{` survives in the
final user prompt.

## Hot-reload

Files are copied to the build output (`bin/Debug/.../Infrastructure/AI/Prompts/`).
Edit the `.md` file there while the app is running — the next call to
`IAiCluePromptProvider.BuildBoardCluesPrompt` will see the new content
(`FilePromptLoader` invalidates its cache on `LastWriteTimeUtc` change).

## Adding a new language

1. Create `Prompts/<langcode>/board-clues.md` (e.g. `Prompts/en/board-clues.md`).
2. Create a `<Language>AiCluePromptProvider : IAiCluePromptProvider` class in
   `SoClover/Infrastructure/AI/Prompts/`. Reuse `FilePromptLoader` and the same
   placeholder substitution logic as `FrenchAiCluePromptProvider`.
3. Wire the new provider into `AiCluePromptProviderFactory.GetFor` (add a
   matching `TextNormalizer.Normalize` prefix branch).
4. Add tests under `SoClover.Tests/AI/` mirroring `FrenchAiCluePromptProviderTests`.

No UseCase or Domain code is impacted.
