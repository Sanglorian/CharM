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

## Known engine-vocabulary divergences (full-playability follow-ups)

Orcus is close to WotC 4e but not identical. These compile/load fine today, but
**making a character compute correctly in-app** needs the engine to recognise the
Orcus names (or an alias layer). Tracked here so it isn't lost:

- **Skills:** `Endure` (≈ Endurance), `Streetsmarts` (≈ Streetwise),
  `Sleight of Hand` (≈ Thievery). Orcus's authoritative list omits Thievery;
  the Harlequin's "Thievery" is read as Sleight of Hand.
- **Recoveries** ≈ Healing Surges; **Staggered** ≈ Bloodied. Authored using the
  engine's existing stat names where the mechanic is identical.
- **Focus proficiency** is an Orcus weapon/implement category with no direct WotC
  analogue — modelled as proficiency fields for now.
- **Talents / Cruxes / Heritages / Disciplines** are new element types; the
  character-creation wizard will need to surface their selects.
- **HP/level scaling** (e.g. +6 HP/level) needs the engine's per-level machinery;
  the slice encodes level-1 base values faithfully and flags the rest.

## Status

`content/orcus/` currently holds a **level-1 Guardian vertical slice** (the
Guardian class + talents + feature powers, the Art of War discipline's level 1–2
powers, the Humanity ancestry with sample Cruxes/Heritages, and the skill/size
plumbing). It compiles and loads; see that folder's README.
