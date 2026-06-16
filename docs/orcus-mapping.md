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
  Trumpet). Done with **no engine change and no equipment dependency**. The
  shared discipline power names every candidate ability in its attack/damage
  text — `Attack: "Charisma or Wisdom vs Will"` — and the engine's
  `ResolveAttackAbility`/`ResolveDamageAbility` pick among them (weapon, focus
  **and** weaponless powers). Two cases, because the rule ("you *may* replace
  the discipline key with your class key") means *use the higher*:

  - **Class key ≠ discipline key** (Priest): grant **no** Ability Choice. The
    named set `{disciplineKey, classKey}` is exactly the Priest's legal set, so
    the engine's highest‑modifier fallback uses the higher of the two — Wisdom
    when it leads, **Charisma when a Priest's Charisma is higher** (no false
    negative).
  - **Class key = discipline key** (Commander): grant an **"Ability Choice"**
    element naming the key (e.g. `Commander Key Charisma`, category
    `Ability Choice`). This pins the class to its key so it does **not** pick up
    the other class's key (Wisdom) that only appears because the text is shared
    (no false positive).

  Verified on the unmodified engine, no weapon/implement: high‑Wis Priest →
  Wisdom; **high‑Cha Priest → Charisma**; Commander (even with high Wisdom) →
  Charisma; Guardian → Strength.

  Scope / limitation: this content‑only scheme is correct for the **nine base
  classes**, where every discipline maps to exactly one class except Angel's
  Trumpet (Commander + Priest = two keys). It does **not** scale to **kits**.
  Per the Kits chapter, a kit's associated discipline is used "as if it were one
  of your class disciplines" — i.e. with *your* class key — and kits are open to
  any class. So once kits exist, most disciplines become reachable by classes of
  many different keys (up to all six). Naming the full set in the power text then
  over‑grants (a Guardian could pick Charisma off a shared "Cha or Wis or …"
  line), and `ChosenAbilities` can only *force* one ability, not take the higher
  of a per‑character pair.

  The general, correct, scalable fix is a small **engine** enhancement: union
  the character's class key (and talent secondary) into the power's candidate
  set and take the highest — exactly what the engine already does for *basic*
  attacks (`SelectBestBasicAttackAbility`), generalised to discipline powers and
  made equipment‑independent. Until then the content‑only scheme stands for base
  classes; kits will need that engine change (or accept `ChosenAbilities` force
  semantics, which reintroduces the high‑Charisma‑Priest false negative).

The secondary‑ability (talent) substitution works the same way (the power names
the secondary options) and is a follow‑up once an authored power puts the
secondary on an attack.

## Status — playability validated ✅

`content/orcus/` holds **two playable classes** (Guardian/Defender and
Commander/Leader) with their disciplines, the Humanity ancestry, the full 17-skill
foundation, and the level 1–3 creation bootstrap. `charm-authoring playtest
<db> --class <name> --level <n>` builds a character headlessly: every slot
resolves and stats compute correctly, including per-level scaling and the default
power progression (L2 utility, L3 second encounter). See
`content/orcus/README.md`.
