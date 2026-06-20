# Orcus transcription — required knowledge for a new instance

Read this before touching anything under `content/orcus/` or `tools/CharM.Orcus.Import/`.
It captures the non-obvious rules, conventions, and gotchas that this effort runs on.

## 1. The prime directive: verbatim or blank, never paraphrase

The whole point of this project is to transcribe the **Orcus** ruleset from the
source books into CharM YAML **without an LLM ever rewording rules text**. Every
piece of rules prose must be copied verbatim from the source, or left blank — it
must **never** be paraphrased, summarized, or invented.

- Source attribution: rules content is `source: "Orcus Original"`. Weapons (from
  `Basic.html`) are `source: "Orcus Basic"`. The in-world setting name is
  "Outlaw Kingdoms" — keep that verbatim where it appears in source text, but the
  ruleset itself is attributed "Orcus Original".
- The deterministic tool (`tools/CharM.Orcus.Import`) is what keeps the LLM out
  of the copy path. **Prefer extending a generator/patcher over hand-editing
  generated YAML.** Generated files say "Do not hand-edit; regenerate instead."

## 2. Source books (repo root)

| File | Contents |
|---|---|
| `Orcus Rulebook - current.md` | Core rules, prices |
| `Orcus Classes and Powers - current.md` | Classes, disciplines, powers, kits, flux, companions |
| `Orcus Player Options - current.md` | Ancestries, cruxes, heritages, **feats**, deities |
| `Orcus Advanced Options - current.md` | Magic items (boosts, wondrous, consumables) |
| `Basic.html` | **Weapons** (Melee + Ranged tables) — source for `equipment/weapons.yaml` |
| `Orcus - Open Game License.md` | OGL — excluded from audits |

## 3. The tool: `tools/CharM.Orcus.Import` (run from repo root)

```bash
dotnet run --project tools/CharM.Orcus.Import -- <command> [args]
```

| Command | What it does |
|---|---|
| `audit .` | Fidelity (prose verbatim?) + coverage (powers transcribed?) + orphans. Writes `audit-report.md`. **Target: 0 flagged, 0 invented.** |
| `audit-all .` | Stricter: scans *every* field (incl. names) of every element vs the source blob. |
| `generate-discipline . "<Name>" <DISC_ID> <SUFFIX> <out.yaml>` | Emit a discipline's powers verbatim with a round-trip gate. |
| `generate-paths . <out.yaml>` | Emit **all** prestige (paragon-tier) paths verbatim → `content/orcus/paths/prestige.yaml`. Features + powers, level-gated, round-trip gated. |
| `generate-feats .` | Emit **all** feats from Player Options → `content/orcus/feats.yaml`. |
| `generate-boosts . <out>` / `generate-misc .` / `generate-companions .` | Magic-item boosts / wondrous+consumables / companions. |
| `patch-class . <classFile> "<Name>" [bookGlob]` | Patch one class's fields from source. |
| `patch-global . <file> <bookGlob>` | Patch fields across a file from a source book. |

### The round-trip gate (how "verbatim" is enforced mechanically)
- **Normalizer** (`Normalizer.Norm`) reduces text to lowercase letters/digits only
  (markdown, smart quotes, punctuation, whitespace all collapse to spaces).
- **Forward check** (`Phase2.TextIsFaithful`): a field value's tokens must be a
  *subsequence* of the source block's tokens → no fabrication / rewording.
- Generators fail (exit 2) and list offenders if any field isn't a subsequence of
  its own source block. Keep generators at 0 failures.

## 4. Authoring format essentials

- Elements: `id`, `name`, `type`, `source`, `categories`, `fields`, `rules`.
- Directives in `rules:`: `statadd`, `statalias`, `grant`, `select`, `replace`,
  plus `requires`.
- **`requires` grammar** (engine evaluates against active element *names*):
  whole-name match incl. spaces; `!` negate; `|` or; `&` and; parentheses.
  E.g. `"Heroic Feats | Feats and Kits"`, `"(!Feats and Kits)"`.
- **`$$KITDISC`** select variable: built from every active `Discipline Access`
  element's `_Discipline` field (`SelectVariables.cs`). Class power selects OR it
  into their discipline category so a kit's discipline becomes selectable.
- Creation bootstrap is `_internal/level.yaml` (`ID_INTERNAL_LEVEL_1..30`).

## 5. The feat / kit model (most recent work — know this)

At level 1 a character makes a **compulsory, mutually-exclusive** choice: heroic
feats **or** a kit. Implemented entirely in content (`kits.yaml` + `level.yaml`):

- A compulsory `select` **"Feat or Kit"** (`type: Class Feature`,
  `category: ORCUS_HEROIC_CHOICE`, `requires: "(!Feats and Kits)"`) offers two
  options: `ORCUS_HEROIC_CHOICE_FEATS` (grants marker **"Heroic Feats"**) and
  `ORCUS_HEROIC_CHOICE_KIT` (grants marker **"Kit Path"**).
- The six heroic-tier feat slots (levels 1/2/4/6/8/10) carry
  `requires: "Heroic Feats | Feats and Kits"`.
- The kit slot carries `requires: "Kit Path | Feats and Kits"` and is **not**
  `optional`. Paragon/epic feat slots (12+) are ungated.
- The **"Feats and Kits"** house rule suppresses the meta-choice and enables both
  paths at once.
- **Kits remain `Theme` elements** (unchanged). Do **not** change how kits work
  (e.g. retyping them) without asking the user — that was explicitly rejected.

### Known limitation we deliberately accept
The web UI (`Home.razor`) hard-codes `Theme` choices as *skippable* and sorts
skippable choices **after** powers (`IsSkippableType`, `OrderPendingChoices`). So
the kit pick still appears after powers and is skippable in the app. The user
chose to **accept this** rather than make UI changes. **Do not edit the UI** for
Orcus ordering — that change was made once and reverted at the user's request.

## 6. Hard-won gotchas

- **Build only in a plain local folder.** Google Drive/OneDrive/Dropbox keep
  files memory-mapped → `CreateAppHost ... user-mapped section open`. Workaround:
  append `-p:UseAppHost=false` to `dotnet build/run` (CLI tools don't need the
  native exe).
- **The produced `.db` must be self-contained.** `RulesDbBuilder.cs` checkpoints
  WAL (`PRAGMA wal_checkpoint(TRUNCATE)` + `journal_mode = DELETE`) so the file
  isn't a 4 KB stub. If a copied db is tiny/empty, that's the cause.
- **"rules.db in use" / "not found":** the running web server holds
  `%LOCALAPPDATA%\CharM\rules.db`. Stop the server before replacing it; clear
  `rules.db*` sidecars; use absolute paths.
- **Magic weapons & armor are base + enchantment composites.** Generic
  Weapon/Armor magic families and boosts omit `Item Slot` so the
  `MagicItemClassifier` treats them as *enchantments* that must pair with a base
  (like 4e). Focus(Implement)/Cloak(Neck) keep their slot.
- **Armor proficiency match** reads `Armor Category` (Cloth/Leather/… ) vs
  `Armor Proficiency (X)` grants — not `Armor Type` (Light/Heavy, which only
  drives the AC ability-mod suppression).
- **Standard `audit` ignores** `source:` and non-prose/structural fields; it does
  not scan `"Properties"` (plural). Use `audit-all` for everything.

## 7. Build / verify / run

```bash
# Build the Orcus db from content
dotnet run --project src/CharM.Authoring.Cli -- build content/orcus -o orcus-rules.db
# Headless sanity check (same engine the app uses)
dotnet run --project src/CharM.Authoring.Cli -- playtest orcus-rules.db --class Mageblade --level 10
# Fidelity/coverage
dotnet run --project tools/CharM.Orcus.Import -- audit .
```
Current state: **1727 elements, 33 types, 0 audit flags, 0 invented.**

## 8. Working agreement with the user

- Develop on the assigned branch; commit with clear messages; push when complete.
  Do **not** open a PR unless asked.
- **Ask before structural/behavioural changes** (e.g. changing how a whole
  category like kits works). Small content changes that follow the established
  conventions are fine to make directly.
- **No UI changes** for Orcus unless explicitly approved.
- Keep model identity out of commits/PRs/code (chat only).

## 9. Still open / possible follow-ups

- Feat *mechanical* wiring is intentionally minimal (7 feats). Most feats are
  text-only; wiring more would be a curated `RulesOverlay` expansion in
  `Feats.cs` (keep text generation untouched).
- 11th/21st-level scaling on defense feats is described in Benefit text but not
  yet applied as a scaling bonus.
- Kit "associated discipline" is wired via `$$KITDISC` only for disciplines that
  exist in this content; others are descriptive.
- The app's Theme-is-skippable ordering (see §5) is unaddressed by design.
