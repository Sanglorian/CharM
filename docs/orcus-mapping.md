# Transcribing Orcus → CharM `rules.db`

This maps **Orcus** (the OGL 4e retroclone in the repo's `Orcus *.md` files) onto
CharM's element model and the YAML authoring format (see `docs/authoring.md`).
The authored content lives under `content/orcus/`.

## Concept mapping

| Orcus concept | CharM element `type` | Notes |
|---|---|---|
| Ancestry (Humanity base) | `Race` | Grants size/speed/vision + languages; offers Crux & Heritage selects |
| Crux | `Crux` | +2 skill, traits, usually one encounter power. Chosen via a `select` on the Race |
| Heritage | `Heritage` | +2 skill, small trait. Chosen via a `select` on the Race |
| Class | `Class` | HP/defenses/proficiencies, trained-skill & power & talent selects |
| Talent (subclass) | `Class Feature` (tagged) | Sets secondary ability; chosen via a `select` |
| Named class feature | `Class Feature` | e.g. Combat Dominance, Savvy Combatant |
| Discipline (power list) | `Discipline` | Reference row; powers are tagged with its id |
| Power | `Power` | `Power Usage` = At-Will/Encounter/Daily/Utility; categories `[disciplineId, frequency, level]` |
| Feat | `Feat` | — |
| Kit | `Kit` / `Class Feature` | Packages of features (later) |
| Deity | `Deity` | — |
| Skill | `Skill` | Reference row with a `Key Ability` field |
| Skill training | `Skill Training` | `statadd "<Skill> Trained" +5` |
| Size / Vision / Ability bonus | `Size` / `Vision` / `Race Ability Bonus` | Plumbing reference rows grants point at |

## Power selection

A class lists **Class Disciplines**; powers belong to a discipline and are tagged
`categories: [<disciplineId>, <frequency>, <level>]`. The class's power `select`
filters with `<disciplineA>|<disciplineB>,<frequency>,<level>` (the engine's
`|` = OR, `,` = AND), mirroring WotC's `$$CLASS,at-will,1`.

## The creation bootstrap (`ID_INTERNAL_LEVEL_1`)

Character creation is data-driven from a hardcoded id: the engine looks up
`ID_INTERNAL_LEVEL_<n>` and runs its directives to **(a)** seed the root
Race/Class choice slots and **(b)** supply the core stat formulas (defenses,
skills, half-level). A database with no `ID_INTERNAL_LEVEL_1` builds nothing —
no Race/Class slots ever appear. `content/orcus/_internal/level.yaml` provides
it for Orcus, using the standard 4e maths Orcus inherits:

- defense = `10 + half level + best of two abilities` (the two ability mods share
  bonus type `Ability`, so the engine's non-stacking rule keeps the higher);
- skill = `key-ability mod + half level + trained (+5) + misc`;
- `statalias` maps `Fortitude`↔`Fortitude Defense` (etc.) so class/feature
  content can use either name.

## Engine-vocabulary divergences — resolved by consistency

Orcus renames some things (`Endure`≈Endurance, `Streetsmarts`≈Streetwise,
`Recoveries`≈Healing Surges, `Staggered`≈Bloodied). **These need no engine
changes**: the engine computes whatever stat names the data uses, so as long as
the content is internally consistent (the `Level1Rules` computes the same names
the skills/classes target), it just works. Remaining genuinely-new structures
(`Crux`, `Heritage`, `Discipline`, talents-as-`Class Feature`) flow through the
generic select machinery — validated below. The only UI follow-up is surfacing
these new types nicely in the creation wizard; the engine already handles them.

Still open: **HP/level scaling** (+N HP/level) and the other defenses/skills at
levels >1 need the higher `ID_INTERNAL_LEVEL_<n>` elements; the slice is level-1.

## Ability substitution (Orcus "use your key ability if higher")

Orcus lets you replace a discipline power's printed key ability with your class's
key ability (and the secondary with your talent's) when it's higher. Two layers
cover this:

- **Within a power's printed text** ("Dexterity (ranged) or Strength (melee)",
  "Highest of Strength, Constitution, Dexterity"): the engine already auto‑picks
  the highest‑modifier ability among those named — no extra work.
- **Class key‑ability substitution** (when a class uses a discipline keyed to a
  different ability — e.g. a Priest, Wisdom, using Charisma‑keyed Angel's
  Trumpet): the class emits a `textstring` named `"<disciplineId>:key ability"`
  whose value lists `"<disciplineKey>,<classKey>"`. The **stock engine's**
  key‑ability override then resolves the power's attack **and** damage to the
  higher of the listed abilities. **No engine change is required** — this is
  pure content. Verified: a Priest's *Identify Target* resolves to Wisdom while
  a Commander's stays Charisma.

  Note: the override resolves when the character has the relevant weapon/focus
  equipped (the app passes it to the power calculator). That's how every Orcus
  attack power works anyway — they're all Weapon or Focus powers — so the rule
  applies in normal play. (`charm-authoring playtest` passes a generic implement
  to mirror this.)

The secondary‑ability (talent) substitution uses the same hook and is a
follow‑up once a talent's secondary differs from a discipline's on an attack.

## Status — playability validated ✅

`content/orcus/` holds **two playable classes** (Guardian/Defender and
Commander/Leader) with their disciplines, the Humanity ancestry, the full 17-skill
foundation, and the level 1–3 creation bootstrap. `charm-authoring playtest
<db> --class <name> --level <n>` builds a character headlessly: every slot
resolves and stats compute correctly, including per-level scaling and the default
power progression (L2 utility, L3 second encounter). See
`content/orcus/README.md`.
