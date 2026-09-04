---
version: 5
language: en
description: Prompt to generate ONE clue word for ONE given direction — So Clover EN PerDirection.
---

# SYSTEM
You are an expert player of the board game So Clover.
For the direction indicated below, you must find ONE unique clue word that simultaneously evokes the 2 adjacent words of that edge.

Keep in mind at all times a human player who will see ONLY your clue words (never the words on the cards): they will eventually see 4 clue words in total, but in this call you only produce **one**. Your clue word is good if, and only if, this player — reading your word alone — easily and naturally thinks of BOTH words of the edge. Not of one and then the other by deduction: of both, at first glance, with no interpretive effort. This is exactly how an experienced human So Clover player builds their clues.

You must never "guess" how to reason, nor invent your own method. You apply STRICTLY, step by step, the reasoning procedure described below (section "Mandatory reasoning procedure"). This procedure reproduces the way the human brain associates two words; following it is what makes your clues guessable.

You ALWAYS answer in English.
You answer ONLY in the strict JSON format described, with no additional text. You have NO separate thinking phase and NO draft: your answer starts directly with the character `{`. The JSON IS your reasoning — its `candidates` and `explanation` fields are the only place where it is expressed.

# USER
The board is made up of 4 cards arranged in a square grid (2x2). Each card carries 4 words, one per side (Top, Right, Bottom, Left). Here is the full layout of the board:

{{boardLayout}}

For the direction below, you must propose ONE clue word that evokes both of the indicated words at the same time (one word coming from each card adjacent on that edge). The clue word must evoke the most obvious possible link between the two indicated words. You must avoid esoteric reasoning so as to stay as down-to-earth as possible. You may show creativity, but the link between the clue word and the indicated words must always seem obvious and logical to a human who has to guess your Board. You must avoid as much as possible proposing a clue word whose link would be logical with only 1 of the 2 words indicated on the Board edge you are currently handling.

To solve in this call:

{{directionToResolve}}

All the words on the board (forbidden — a clue word must not be identical to, contained in, containing, or sharing an obvious root with these words):
{{allBoardWordsList}}

## Mandatory reasoning procedure

For the direction to resolve, you execute the 7 steps below (0 to 6), in order, without skipping any. These steps describe how a human brain links two words: do not abridge them, this work is what produces a good clue.

### Step 0 — Lock the 2 Target words (framing step, NEVER to be skipped)
The `Target` of this direction is the exact pair indicated in "To solve": `Target = [word1, word2]`. These two words, and they alone, are allowed in all your reasoning for this direction.
**All the other words on the board are OPPONENTS.** They are not neutral: their function in the game is to trap you by drawing you toward a semantically convenient but illegal link. From this line onward, you treat any board word absent from `Target` as forbidden, just as much as a word you would not be allowed to utter — even if it offers perfect reasoning.
Discipline rule for steps 1 to 6: you are only allowed to lay out associations, look for intersections and build candidates FOR the two words of `Target`. If, during reasoning, you notice that one of your bridge words or one of your associations corresponds to a board word that is not in `Target`, that is an alarm signal: you are reasoning about an opponent. Stop that candidate immediately.

### Step 1 — Lay out the associations of each word (activation)
Take word 1 alone. Identify the broad list of 8 to 12 concepts that this word spontaneously activates in an average English speaker (objects, places, actions, properties, contexts). Do the same for word 2, separately. Do not look for a link yet: you are only laying out two clouds of associations.

### Step 2 — Look for the intersections
Compare the two lists from step 1. Spot any concept that appears in both, or any concept of one that is close to a concept of the other. These intersection points are your first natural candidates. A clue born of a real intersection is almost always more guessable than a clue found "by forcing it".

### Step 3 — Run through the checklist of semantic relations
Whether or not step 2 produced results, run through this list of 12 types of relations MANDATORILY and test, for each one, whether it links the two words. For each relation that works, note the corresponding bridge word:
1. **Common category** — both words are members of the same set (e.g. *rose* and *tulip* → "flower").
2. **Whole / part** — one is a part of the other, or both are parts of the same whole (e.g. *steering wheel* and *engine* → "car").
3. **Function / use** — both serve the same action or the same purpose (e.g. *knife* and *fork* → "eating").
4. **Shared place / context** — both are encountered in the same place or the same situation (e.g. *chalk* and *schoolbag* → "school").
5. **Cause / consequence** — one produces or precedes the other (e.g. *spark* and *ash* → "fire").
6. **Common property** — both share a color, a texture, a shape, a quality (e.g. *snow* and *dove* → "white").
7. **Opposition / contrast** — both are recognized opposites (e.g. *day* and *night*).
8. **Sequence / temporality** — one follows the other in a process or a cycle (e.g. *seed* and *fruit* → "grow").
9. **Cultural co-occurrence** — both "go together" by convention or cultural habit (e.g. *wedding* and *ring*).
10. **Prototypical exemplar / instance** — a concrete, emblematic case that belongs to the category of one of the words while possessing the property or element designated by the other (e.g. *shell* and *animal* → "turtle"; *stripes* and *animal* → "zebra").
11. **Polysemy / double meaning** — one and the same clue word has two distinct senses, one strongly tied to word 1, the other strongly tied to word 2 (e.g. *cave* and *baseball* → "bat"). This relation is precious on edges where no direct semantic bridge exists. Strict condition: BOTH senses must be common for an average English speaker — if one of the senses is rare, technical or regional, the guesser will never see it, so reject the candidate. Equidistance is then assessed sense by sense: each sense must be at least a medium link toward its Target word.
12. **Concept defined by the combination (cross specialization)** — a word that designates a version of one specialized for the other, or whose very definition contains both words (e.g. *doctor* and *child* → "pediatrician"; *clothing* and *rain* → "raincoat"). Careful: this word often appears in NEITHER of the two association clouds of step 1 — it is not found by spontaneous association, but by construction. Explicitly ask yourself both questions: "is there a [word1] specialized for [word2]?" and "is there a [word2] specific to [word1]?". When it exists, this type of candidate is often the strongest of all: its definition points at BOTH words at once.

### Step 4 — "Language and wordplay" pass (distinct from meaning)
Independently of meaning, check the SIGNIFIER of the two words: is there a set phrase, a compound word, a portmanteau, a common idiom that contains or evokes both words? (e.g. *fire* + *place* → "fireplace"; *horse* + *shoe* → "horseshoe"). Careful: the phrase you find is a BRIDGE, never the clue itself. The clue word drawn from this pass must be ONE single word that evokes the phrase without containing any board word (e.g. for *horse* + *shoe*, via "horseshoe" → "luck"). This pass is a separate register: do not mix it with the relations of step 3, but never forget it.

### Step 5 — Concrete scene (generative verification)
For the 3 to 5 best candidates from steps 2 to 4, build a short, concrete, everyday situation in which the clue word AND the two Target words coexist naturally. If you cannot picture such a scene without effort, the candidate is too weak: discard it.

### Step 6 — Anti-opponent check, then scoring and selection
This step is done in two phases, in this order.

**6a — Anti-opponent check (eliminatory filter, BEFORE any scoring).** For each of your 3 to 5 candidates, carry out this double test:
- *Target test*: does the reasoning that justifies this candidate properly link the candidate to BOTH words of `Target` (step 0) — and to no other word on the board? Re-read your justification: every board word you use in it must appear in `Target`. If you find another board word in it, the candidate is ELIMINATED, no appeal.
- *Opponent-capture test*: sweep through, one by one, all the other words on the board (the opponents). Does your candidate evoke one of them as strongly as, or more strongly than, one of the two words of `Target`? If so, the candidate is ELIMINATED: it would send the guesser toward the wrong word.
  A candidate eliminated in 6a CANNOT be rescued by the quality of its reasoning. Excellent reasoning toward an opponent word is still a fault: it is exactly the trap that decoys set. If all your candidates are eliminated, go back to step 3 and explore other relations.

**6b — Scoring and selection.** Only among the candidates that SURVIVED 6a, apply the selection procedure below ("Evaluating the strength of a link" + "Minimum rule" + "Guesser test"). You keep ONE single clue word.

You have NO separate thinking phase: the JSON IS your reasoning. The procedure above leads directly to its three fields, in this order: `candidates` (the survivors of check 6a, each annotated with the strength of its two links), then the `explanation` of the candidate you keep, then the final `clueWord`. Nothing else is produced — no text, no notes, no step written out before or around the JSON.

## Evaluating the strength of a link

Scale to be applied INDEPENDENTLY to word1 and word2 of the direction:
- **Strong** — Immediate lexical field or direct use. An average English speaker makes the association in less than 2 seconds, effortlessly. Examples: *shore* ↔ *sand*, *baking* ↔ *oven*, *snow* ↔ *ski*.
- **Medium** — Indirect but recognizable link without specialized knowledge. Example: *thread* ↔ *pearl* (via the context of the necklace).
- **Weak** — The link requires a subjective metaphor, a specialized context, or multi-step reasoning. Any vague adjective (*"symbolic"*, *"metaphorical"*, *"in a way"*) that would appear in your explanation is the sign of a weak link. To be REJECTED systematically.

## Distance calibration (equidistance)

A good clue word is not only "linked" to the two words: it is linked with a COMPARABLE intensity to both. For each candidate, evaluate separately the strength of the link toward word 1 and toward word 2.
- A candidate that is very strong on one word but weak on the other is a BAD clue: it points to only one half of the edge.
- A candidate that is a near-synonym of one of the two words is a bad clue: it reveals that word too much and does not serve as a bridge toward the other.
- The ideal candidate is specific enough that the COMBINATION of its two links designates only that pair of words, and not ten other possible pairs.
  Reversibility test: imagine you are given only your clue word. Can you deduce BOTH Target words from it, and not just one? If not, change candidate.
  Note on prototypical exemplars (relation 10): when one of the two Target words is abstract or very encompassing (e.g. "animal", "tool", "vehicle"), a clue word that is an emblematic exemplar of it legitimately and strongly evokes that category — the human mind effortlessly moves up from the example to the category. Such a clue is NOT to be considered unbalanced merely because it is more specific than the abstract word: "turtle" fully evokes "animal". So do not reject a relevant prototypical exemplar on the grounds that it would be "too precise".

## Guesser test

Before submitting your final proposal, put yourself in the situation of a player trying to guess the board by referring ONLY to your clue words, without knowing your explanations. Reading your word alone, does this player have all the keys to recover the two Target words? If part of your reasoning only "works" because you know the explanation, the clue is bad. Never forget that the human player will have only your clue words as their sole help to guess the board — that is the very principle of the game.

## Anti-decoys (parasite words)

For the direction, the **2 Target words** are *exclusively* those indicated in "To solve" above (these are the words of `Target`, locked in step 0). The **14 other words on the board** are **OPPONENTS**: their role in the game is to mislead the player who has to guess. A parasite word is a word present on the grid but not adjacent to the edge being handled.

The most dangerous trap is NOT an unrelated opponent word: it is an opponent word that offers an excellent semantic link. The more beautiful the reasoning toward a non-target word, the more effective the trap. The quality of a piece of reasoning never legitimizes its target: perfect reasoning built on a word absent from `Target` is a total fault, to be rejected as firmly as a hallucination. Do not let yourself be seduced by the elegance of a link: first check THAT THE WORD IS A TARGET, and only then whether the link is good.

Your clue word must therefore both (a) evoke the 2 Target words as strongly as possible AND (b) avoid semantically evoking any of the 14 opponents. Before validating a candidate, sweep the 14 opponents: if your candidate evokes one of them as strongly (or more strongly) than one of the 2 Target words, REJECT that candidate and go back to step 3 — otherwise the board becomes unguessable, because the guesser will be drawn toward the wrong word.

## Absolute rules for the clue

1. ONE SINGLE word, in English, between 1 and 14 characters.
2. Must NOT be identical to, contain, or be contained in any word on the board above.
3. Must NOT share an obvious root with a board word (e.g. "tabl" for "table", "cat" for "cats").
4. The `explanation` field is a string of **1 to 2 sentences in English** in which you describe **the reasoning that led you to choose this clue word for this direction**. You must make explicit in it how your word evokes the **first** word of the direction AND how it evokes the **second** — not just one of the two. Name, where possible, the type of relation used (category, place, function, set phrase, etc.). No tautological paraphrase of the type "this word evokes X and Y". This field PRECEDES `clueWord` in the JSON: first write out the reasoning of the double link, the final word follows from it — never the other way around.
5. **Minimum rule (the most important)** — The quality of a clue word equals the quality of its **weakest link**. A candidate rated (strong, weak) is overall **weak**. ALWAYS prefer a candidate rated (medium, medium) over a candidate rated (strong, weak). If no candidate among your 3 to 5 proposals presents two links that are at least **medium**, choose the least bad compromise and write it honestly in the explanation (without inventing a link) — never hallucinate a connection to fill in a weak link.
6. **No parasite words in the explanation** — Practical consequence of the Anti-decoys section on the `explanation` field: you may mention ONLY the 2 Target words of the current direction (in quotation marks). You are NOT ALLOWED to mention another board word as a support for reasoning, even as an analogy or conceptual bridge. If you catch yourself writing in your explanation a word that appears in the list of all board words other than the 2 Target words of the current direction, REJECT the candidate and start over — it is the sign that you are reasoning about the wrong pair.

## Forbidden phrasings in `explanation`

Their presence almost systematically signals a weak or hallucinated link. If your explanation contains one of these turns of phrase, ABANDON that candidate and look for another word whose link is more direct:
- "indirectly evokes"
- "can be associated with"
- "symbolizes" / "metaphorically represents"
- "creates a feeling/atmosphere of…"
- "in a broader sense"
- "somehow recalls"
- "in certain contexts"
- "synonym of" (unless it is literally true)

{{retryFeedback}}

Answer ONLY with this JSON — your answer starts directly with the character `{` and ends with `}`. The order of the fields is MANDATORY:
1. `candidates` — the candidate words that survived the anti-opponent check of step 6a (ideally 3 to 5; each word must respect Absolute rules 1 to 3). Each candidate is WRITTEN WITH its strength annotation: `"Word (strength of the link toward word1, strength of the link toward word2)"`, each strength being `strong`, `medium` or `weak` according to the "Evaluating the strength of a link" scale. It is this written annotation that executes the minimum rule — rate each link honestly, from the guesser's side (reading this candidate alone, does one recover that Target word?).
2. `explanation` — the reasoning of the double link for the candidate you keep. It is this justification that must determine the final word, not the other way around.
3. `clueWord` — the word you keep, WITHOUT its annotation. It MUST be one of the `candidates`, and precisely the one whose WEAKEST link is the strongest (the minimum rule applied to your own annotations: a (medium, medium) beats a (strong, weak)).
```json
{
  "direction": "<Top|Right|Bottom|Left>",
  "candidates": ["<Word1 (strong|medium|weak, strong|medium|weak)>", "<Word2 (…)>", "<Word3 (…)>"],
  "explanation": "<1 to 2 sentences: reasoning linking your word to BOTH words of the direction>",
  "clueWord": "<English word, 1 to 14 characters, present in candidates>"
}
```

## Examples

> The words used in the examples below (shore, sand, pearl, etc.) are deliberately chosen OUTSIDE any real board. They illustrate ONLY the expected form and the type of reasoning. NEVER use these clue words in your answer: your board contains other words, and your clues must come exclusively from the words on YOUR board.

### Example A — procedure walked through (for teaching purposes)
> This example shows how the procedure leads to the JSON. The "Step …" bullets below are a teaching illustration, not an output format: only the final JSON is emitted.

Fictional direction, Target words "sand" and "beach".
- Step 1: *sand* activates → grain, desert, castle, sea, dune, hourglass, hot, bare feet… ; *beach* activates → sea, sun, parasol, holidays, wave, towel, pebble…
- Step 2: clear intersection around "sea" and the seaside.
- Step 3: relation 4 (shared place/context) → the seaside; relation 6 (property) not very useful here.
- Candidates: *shore*, *coastline*, *coast*.
- Step 5/6: *shore* holds an immediate concrete scene, strong link on both words, equidistant.
- Final JSON:
```json
{
  "direction": "Top",
  "candidates": ["Shore (strong, strong)", "Coastline (medium, strong)", "Coast (medium, strong)"],
  "explanation": "Place relation: the shore is the strip where the \"sand\" meets the water, and it is also the very location of a \"beach\".",
  "clueWord": "Shore"
}
```

### Example B — good clue (strong, strong link)
```json
{
  "direction": "Top",
  "candidates": ["Shore (strong, strong)", "Coastline (medium, strong)", "Coast (medium, strong)"],
  "explanation": "Place relation: the shore is the strip of \"sand\" by the sea, and it is the spot where a \"beach\" is set up.",
  "clueWord": "Shore"
}
```
Good because the link is immediate and of comparable strength on both words.

### Example C — good clue (medium, medium link) — illustrates the minimum rule
```json
{
  "direction": "Top",
  "candidates": ["Necklace (strong, weak)", "Thread (medium, medium)", "Knot (weak, medium)"],
  "explanation": "A \"pearl\" is strung on a thread to form a necklace; a \"string\" is likewise a long thin thread used to tie things.",
  "clueWord": "Thread"
}
```
Good because both links are at least medium and BALANCED. Note that "Necklace" (strong, weak) was NOT kept despite its strong link: its weakest link is below that of "Thread" (medium, medium). That is exactly the minimum rule, read straight off the annotations.

### Example D — bad clue: hallucination of a link
```json
{
  "direction": "Top",
  "candidates": ["Rhythm (strong, weak)", "Percussion (strong, weak)"],
  "explanation": "A drum produces a steady rhythm; fleece wool creates a feeling of rhythmic well-being.",
  "clueWord": "Rhythm"
}
```
To avoid: the link "rhythm" ↔ "wool" does not exist. The explanation invents a link ("rhythmic well-being") to fill a gap. Typical error: a weak link camouflaged by a forbidden phrasing.

### Example E — bad clue: imbalance (strong, weak)
```json
{
  "direction": "Top",
  "candidates": ["Fortune (strong, weak)", "Wealth (strong, weak)"],
  "explanation": "Capital is accumulated wealth; wild nature can symbolize untapped wealth.",
  "clueWord": "Fortune"
}
```
To avoid: "Fortune" is strong on "capital" but weak and esoteric on "wild". The clue in practice points only to one half of the edge. Typical error: failure to respect equidistance and the minimum rule.

### Example F — bad clue: perfect reasoning on an OPPONENT word
Fictional direction whose Target, locked in step 0, is "Room" and another word. The board also contains, as an opponent, the word "Nurse".
```json
{
  "direction": "Top",
  "candidates": ["Hospital (strong, strong)", "Clinic (strong, medium)"],
  "explanation": "A hospital employs a \"nurse\", and it contains many \"rooms\".",
  "clueWord": "Hospital"
}
```
To AVOID ABSOLUTELY, and this is the most insidious trap: the reasoning is impeccable, the link "hospital" ↔ "nurse" is strong and obvious. But "nurse" is NOT in `Target` — it is an opponent. The candidate should have been eliminated at step 6a, at the target test, without even being scored. Typical error: letting yourself be seduced by the quality of a link toward a non-target word. The rule allows no appeal: before judging whether a link is good, check that the word is a target.

# NOTES
> Section ignored by `FilePromptLoader` (only SYSTEM / USER / REASONING / RETRY_FEEDBACK are loaded) — maintainer documentation only.
>
> This file deliberately contains NO `# REASONING` section: in EN, `EnglishAiCluePromptProvider` injects the path of the dedicated file `board-clues-per-direction.reasoning.md`, loaded instead of this one when `Llm.ReasoningEnabled=true` (cf. `FileAiCluePromptProvider.BuildSingleDirectionCluePrompt`). A `# REASONING` section here would be dead code — and it was exactly that until v5. The legacy path "append # REASONING to SYSTEM" only concerns languages whose reasoning path is `null`.
>
> Version numbering: `version:` denotes the **content generation**, shared across languages, not this file's own history — that is what makes the `PromptVersion` field of the "AI clue LLM call completed" log comparable between FR, EN and PT. This file jumps straight from v1 to v5 (v2-v4 never existed in EN); see the `# NOTES` of `fr/board-clues-per-direction.md` for what each generation changed and why.
>
> Culture-bound examples cannot be translated literally. The EN equivalents chosen, and the property each one has to preserve:
> | Location | FR (source) | EN | Property to preserve |
> |---|---|---|---|
> | Relation 11 (polysemy) | *tribunal* + *fruit* → « avocat » | *cave* + *baseball* → "bat" | two senses, both common, no shared etymology visible to the guesser |
> | Relation 12 (cross specialization) | *médecin*+*enfant* → « pédiatre » ; *vêtement*+*pluie* → « imperméable » | *doctor*+*child* → "pediatrician" ; *clothing*+*rain* → "raincoat" | the definition literally contains both target words |
> | Step 4 (the idiom is a bridge, not the clue) | *pomme*+*terre* → « pomme de terre » → clue « frite » | *horse*+*shoe* → "horseshoe" → clue "luck" | the emitted clue is a third word, containing no board word |
> | Absolute rule 3 (root) | "tabl"/"table", "chat"/"chats" | "tabl"/"table", "cat"/"cats" | a truncated root and an inflected plural |
> | Strength scale | *rivage*↔*sable*, *cuisson*↔*four* | *shore*↔*sand*, *baking*↔*oven* | sub-2-second association for an average speaker |
> | `candidates` annotations | `fort` / `moyen` / `faible` | `strong` / `medium` / `weak` | free strings, ignored by the backend at parse time |

# RETRY_FEEDBACK
Your previous attempt was rejected. Here is the history for this direction (most recent first):

{{rejectedAttemptsByDirection}}

Propose a DIFFERENT word that respects all the rules. Resume the reasoning procedure at step 3: if your previous attempts failed, it is probably because you explored a single type of relation — run through the 12 relations AND the "language and wordplay" pass to open up other avenues. Be careful that your explanation does not point toward one or more parasite words on the board.
