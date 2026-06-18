# CharM.Orcus.Import — Orcus content audit / migration tool

A deterministic tool that takes the LLM out of the *content-copying* path. Phase 1
(this commit) is the **verifier/audit**: it proves, mechanically, where the
hand-authored `content/orcus/*.yaml` diverges from the source rulebooks.

## What it does

```bash
dotnet run --project tools/CharM.Orcus.Import -- audit <repo-root> [report-path]
```

1. Builds one normalized text blob from every `Orcus*.md` source book (excluding
   the OGL). Normalization strips markdown emphasis, smart quotes, bullet glyphs,
   punctuation, case and whitespace — but keeps every **word and number**. A
   faithful copy survives normalization; a reworded or fabricated one does not.
2. Loads every element from `content/orcus`.
3. Reports, to `audit-report.md`:
   - **Invented Flavor** — `Flavor` fields whose text is in no source book.
   - **Fidelity flags** — any prose field (Hit/Miss/Effect/Trigger/Special/
     Attack/Target/Keywords/Description/Note/Benefit/Property/…) whose text is
     not found verbatim in any source book, pinpointed to the offending clause.
   - **Coverage** — per discipline, source power count vs transcribed, with the
     exact list of still-missing powers.
   - **Orphan powers** — YAML powers whose name appears in no source book.

Purely programmatic element types (Grants, Skill, Proficiency, ability-swap
markers, the level bootstrap, house rules, etc.) are excluded from the fidelity
scan — those are engine wiring, not copied content.

## Design

The intended end state separates the two kinds of data that are currently
tangled in the YAML:

- **Verbatim source content** (power text, discipline intros, item properties) —
  to be produced by a parser/generator from the books, never typed by hand.
- **Engine semantics** (`id`, `type`, `categories`, `rules`) — hand-authored
  overlay, keyed by id, containing no source prose.

Phase 2 (the parser + generator) regenerates the verbatim text and re-attaches the
semantic overlay, with the round-trip check as a CI gate.

## Commands

- `audit <repo-root>` — the fidelity/coverage report described above.
- `generate-discipline <repo-root> "<Name>" <DISCIPLINE_ID> <SUFFIX> <out.yaml>` —
  regenerate a whole discipline file verbatim from the book; runs the round-trip
  gate (forward: no fabricated/reworded text; backward: no dropped source word).
- `patch-class <repo-root> <classFile.yaml> "<Class Name>"` — rewrite only the
  verbatim-text fields of an existing class file (feature/talent Description prose
  and feature-power stat blocks) from the book, and delete invented `Flavor`.
  Every `rules:` block, id, category and structural field is preserved; each
  rewritten feature power is round-trip checked. This keeps the hand-authored
  engine wiring (HP formulas, selects, grants, swaps) while making the copied
  text faithful.
