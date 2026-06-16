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

## Ability substitution (Orcus "use your key ability, if higher")

Orcus lets you replace a discipline power's printed key ability with your class's
key ability (and the secondary with your talent's) when it's higher. Two layers
cover this:

- **Within a power's printed text** ("Dexterity (ranged) or Strength (melee)",
  "Highest of Strength, Constitution, Dexterity"): the engine already auto‑picks
  the highest‑modifier ability among those named — no extra work.
- **Class key / talent secondary substitution** — implemented with a small,
  isolated engine enhancement (see "Engine enhancement" below). The character's
  class key is added to a discipline power's *key*-ability reference and the
  talent's secondary to its *secondary*-ability reference; the **higher** is used
  in each role.

### Authoring it

1. Tag each class‑discipline attack/damage power with the **`ability-swap`**
   category (alongside the discipline id), e.g. Angel's Trumpet's *Identify
   Target*. Leave the printed abilities as the discipline's key/secondary.
2. Give the discipline element a `Key Ability` and `Secondary Ability` field (it
   already has these) so the engine knows which printed ability is which.
3. Have each class grant a **`Key Ability Swap`** element whose name ends in the
   class's key ability (`Priest Key Wisdom`), and each talent grant a
   **`Secondary Ability Swap`** element ending in its secondary ability
   (`Great Weapon Style Secondary Constitution`).

Because the substitution is role‑scoped, it never bleeds across roles:

- **Priest** (Wisdom) on *Identify Target* (printed key Charisma): uses **Wisdom**
  when higher, **Charisma when the Priest's Charisma is higher** — no false
  negative.
- **Commander** (Charisma = the discipline key): its only key swap is Charisma, so
  it stays on Charisma even with high Wisdom — no false positive.
- **Guardian** + *Great Weapon Style* (secondary → Constitution) on Art of War's
  *Passing Kill* ("Dexterity (ranged) or Strength (melee)"): the **secondary**
  Dexterity may become Constitution, while the **key** Strength is untouched —
  verified Con‑heavy Guardian resolves Constitution with Great Weapon Style but
  Strength with Protection.

All three classes' talents grant their secondary swap: the Guardian's *Great
Weapon Style* (Constitution) and *Protection* — whose secondary is "Dexterity OR
Wisdom (your choice)", modelled as a one‑of `select` between two
`Secondary Ability Swap` options — plus the four Commander Tactics and four
Priest "Worships the God of …" talents. (Commander/Priest secondaries currently
appear in *effect* text rather than attack/damage, so they're inert on today's
Angel's Trumpet powers but correct for any secondary‑referencing power or kit.)

This **scales to kits**: a discipline reached by any number of classes (via kit
access) is always "higher of {printed ability, *this* character's role ability}",
because each character contributes only its own key/secondary — never another
class's.

### Engine enhancement (isolated, additive)

`StatBlock.KeyAbilitySwaps` / `SecondaryAbilitySwaps` are populated from active
`Key Ability Swap` / `Secondary Ability Swap` elements
(`CharacterBuilder.IndexKeyAbilitySwaps`). In `PowerStatCalculator`, when a power
carries the `ability-swap` category **and** the character has swaps, the power's
discipline (resolved from its category id) supplies the printed key/secondary
ability names; the engine then weaves the matching swap abilities in after each
as "or X" alternatives and the existing highest‑modifier resolver does the rest.
A no‑op without both the tag and the elements, so WotC content (which has
neither) is completely unaffected, and it needs no weapon/implement equipped.

### Weapon-die shorthand (`dW` = `[W]`)

Orcus writes weapon damage dice as `dW` / `NdW`; 4e's canonical token is `[W]` /
`N[W]` (the equipped weapon's damage die). `PowerFieldParser` normalizes the
former to the latter (word‑boundary‑anchored, so only standalone `dW`/`NdW` is
touched), after which the existing weapon‑dice machinery substitutes the real die
when a weapon is equipped — e.g. Art of War's *The Finisher* (`3dW`) renders
`3[W]` with no weapon and `3d10` etc. with one. WotC powers don't use the `dW`
spelling, so they're unaffected.

## Status — playability validated ✅

`content/orcus/` holds **two playable classes** (Guardian/Defender and
Commander/Leader) with their disciplines, the Humanity ancestry, the full 17-skill
foundation, and the level 1–3 creation bootstrap. `charm-authoring playtest
<db> --class <name> --level <n>` builds a character headlessly: every slot
resolves and stats compute correctly, including per-level scaling and the default
power progression (L2 utility, L3 second encounter). See
`content/orcus/README.md`.
