# `content/orcus/` — Orcus rules content

Open Game Content transcribed from the repo's `Orcus *.md` rulebooks into the
CharM YAML authoring format. See [`../../docs/orcus-mapping.md`](../../docs/orcus-mapping.md)
for the concept mapping and [`../../docs/authoring.md`](../../docs/authoring.md)
for the format.

## Current scope — three playable classes (levels 1–30)

| File | Contents |
|---|---|
| `_internal/level.yaml` | Creation bootstrap (`ID_INTERNAL_LEVEL_1..30`): seeds Race/Class slots + core stat formulas; per-level increments (Level, Level Bonus); ability-score increases at 4/8/14/18/24/28 and +1-all at 11/21; prestige-path slot at 11, epic-path slot at 21 |
| `reference.yaml` | Sizes, vision, the 17 Orcus skills + their skill-training rows (tagged per class), racial ability-bonus rows, and per-level ability-increase pools |
| `ancestry.yaml` | Humanity (base Race) + sample Cruxes (Hero, Sage) and Heritages (Aristocrat, Seafarer) |
| `ancestries-species.yaml` | 14 species ancestries (Apefolk, Automaton, Azer, Catfolk, Deepfolk, Dromite, Frogfolk, Gnoll, Half-Giant, Hobgoblin, Mephit, Minotaur, Shadow Elf, Vishya) — each selected in the Race slot *instead* of a crux + heritage |
| `classes/guardian.yaml` | Guardian (Defender): Grants bundle, features, talents, feature powers, level-gated power selects to 30 |
| `classes/commander.yaml` | Commander (Leader): talents, armament, Lift Spirits, level-gated power selects to 30 |
| `classes/priest.yaml` | Priest (Leader, Wisdom): talents + the key-ability substitution override (shares Angel's Trumpet with the Commander) |
| `disciplines/art-of-war.yaml` | Art of War discipline (Guardian) + powers across the level range |
| `disciplines/juggernautical.yaml` | Juggernautical discipline (Guardian) + powers across the level range |
| `disciplines/angels-trumpet.yaml` | Angel's Trumpet discipline (Commander/Priest) + powers across the level range |
| `disciplines/golden-lion.yaml` | Golden Lion discipline (Commander) + powers across the level range |
| `paths/prestige.yaml` | Sample prestige paths (Assassin, Battlefield Healer, Bounty Hunter): 11th/16th features + powers at 11/12/20 |
| `paths/epic.yaml` | All six epic paths (Agent Retriever, Master, Most Dangerous, Respected, Team, Ultimate): 21st/24th/30th features + a 26th-level power |

Compiles to **290 elements across 20 types**, no warnings.

### Levels 1–30, prestige & epic paths
The bootstrap runs `ID_INTERNAL_LEVEL_1..30` cumulatively. Each level raises the
`Level` stat (so class HP/`6×Level`-style formulas scale) and, on even levels,
the `HALF-LEVEL` stat (Orcus's *Level Bonus* = `floor(level/2)`, which feeds
defenses and skills). Ability-score increases come from level-gated selects: a
`number: 2` pick at 4/8/14/18/24/28 over a fresh six-row pool per level (the
engine consumes a chosen element globally, so each level-up needs its own rows),
plus a granted "+1 to all" bundle at 11 and 21.

Each class's discipline power selects continue to 30 (encounter@7, daily@5/9,
utility@6/10/16/22), each gated with `level:` and filtered by `…,<freq>,<max>`.
The `+P`/`+E` powers in the progression table come from the paths, not the class:
the **prestige path** (chosen at 11) grants 11th/16th features and powers at
11/12/20; the **epic path** (chosen at 21) grants 21st/24th/30th features and a
26th-level power. Later path benefits carry a `level:` gate, which the engine
honours (future-level grants are deferred), so a level-11 character does not get
its 16th/20th-level path benefits early. Feats (gained every even level) are not
modelled yet — no feat content exists. Verified via `playtest`: Guardian,
Commander and Priest all build end-to-end at level 30 (and at the 11/21 tier
boundaries) with every slot filled.

### Species ancestries
A species is an alternative to Humanity's crux + heritage: it is chosen in the
same Race slot and supplies size, vision, speed, two fixed skill bonuses, a
"pick two of three +2s" ability-score choice and one or more powers. The
"pick two of three" choice reuses the shared `Race Ability Bonus` rows in
`reference.yaml`, which are tagged with `ORCUS_ABILTRIO_*` categories so each
species' select can be filtered to its specific trio. Always-on traits that
don't map to a single stat (resistances, natural weapons, swim/fly speeds, etc.)
are recorded in descriptive fields. Verified via `playtest --race <name>`: all
14 build end-to-end, with the right speed and the ability picks constrained to
each species' trio.

### Ability substitution
Orcus's "use your class key ability (and talent secondary) instead, if higher"
is implemented via a small, **isolated** engine enhancement plus content tags. A
class‑discipline power is tagged `ability-swap` and keeps its printed abilities;
each class grants a `Key Ability Swap` element (its key) and each talent a
`Secondary Ability Swap` element (its secondary). Using the discipline's
`Key Ability`/`Secondary Ability` fields, the engine adds the class key to the
*key* reference and the talent secondary to the *secondary* reference, taking the
**higher** of each — role‑scoped, equipment‑independent, and **kit‑scalable**.
WotC content has neither the tag nor the elements, so it's unaffected. Verified
via `playtest` (no weapon): high‑Cha Priest → Charisma (no false negative),
high‑Wis Commander → Charisma (no false positive), and a Con‑heavy Guardian's
*Passing Kill* resolves Constitution with Great Weapon Style but Strength with
Protection. See `docs/orcus-mapping.md`.

## Build & playtest

```bash
# Compile to a database:
dotnet run --project ../../src/CharM.Authoring.Cli -- build . -o /tmp/orcus.db

# Headlessly build a level-1 character and print computed stats + any unfillable
# slots (the engine-alignment check):
dotnet run --project ../../src/CharM.Authoring.Cli -- playtest /tmp/orcus.db
```

Both a Guardian and a Commander build end-to-end at levels 1–3: every slot
resolves and stats compute correctly, including per-level scaling (e.g. Guardian
HP 31→43 from L1→L3, half-level applied to all defenses) and the default power
progression (L2 adds a utility, L3 a second encounter). Try
`--class Commander --level 3`. Pick a species with `--race <name>` (e.g.
`--race Mephit`). The harness also accepts `--talent`/`--pick <substring>` to
bias any inner select toward a named candidate (handy for checking the
secondary‑ability swap, e.g. `--class Guardian --pick "Great Weapon Style"`, or
a specific discipline power, e.g. `--pick "Pack Pounce"`).

Weapon dice written `dW`/`NdW` are normalized to the engine's `[W]`/`N[W]`, so a
power like *The Finisher* (`3dW`) picks up the equipped weapon's die.

## What's next

Three of nine classes are in (a Defender and two Leaders), playable across the
full 1–30 range, with both Guardian disciplines (Art of War, Juggernautical) and
both Commander disciplines (Angel's Trumpet, Golden Lion), 14 species ancestries,
a sample of prestige paths and all six epic paths. Remaining work: the other six
classes and their disciplines, the rest of the species roster and prestige paths,
all 16 cruxes / 6 heritages, feats, kits, deities and equipment; power
replacements (the optional 13/15/17/… swaps) and higher discipline power levels
for richer high-level choice.
