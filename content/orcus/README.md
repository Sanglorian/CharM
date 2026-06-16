# `content/orcus/` — Orcus rules content

Open Game Content transcribed from the repo's `Orcus *.md` rulebooks into the
CharM YAML authoring format. See [`../../docs/orcus-mapping.md`](../../docs/orcus-mapping.md)
for the concept mapping and [`../../docs/authoring.md`](../../docs/authoring.md)
for the format.

## Current scope — three playable classes (levels 1–3)

| File | Contents |
|---|---|
| `_internal/level.yaml` | Creation bootstrap (`ID_INTERNAL_LEVEL_1..3`): seeds Race/Class slots + core stat formulas (defenses, all 17 skills, half-level) |
| `reference.yaml` | Sizes, vision, the 17 Orcus skills + their skill-training rows (tagged per class), ability-bonus rows |
| `ancestry.yaml` | Humanity (base Race) + sample Cruxes (Hero, Sage) and Heritages (Aristocrat, Seafarer) |
| `classes/guardian.yaml` | Guardian (Defender): Grants bundle, features, talents, feature powers, level-gated power selects |
| `classes/commander.yaml` | Commander (Leader): talents, armament, Lift Spirits, level-gated power selects |
| `classes/priest.yaml` | Priest (Leader, Wisdom): talents + the key-ability substitution override (shares Angel's Trumpet with the Commander) |
| `disciplines/art-of-war.yaml` | Art of War discipline (Guardian) + level 1–3 powers |
| `disciplines/angels-trumpet.yaml` | Angel's Trumpet discipline (Commander/Priest) + level 1–2 powers |

Compiles to **112 elements across 17 types**, no warnings.

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
`--class Commander --level 3`. The playtest harness also accepts
`--talent <substring>` to force a specific talent (handy for checking the
secondary‑ability swap, e.g. `--class Guardian --talent "Great Weapon Style"`).

Weapon dice written `dW`/`NdW` are normalized to the engine's `[W]`/`N[W]`, so a
power like *The Finisher* (`3dW`) picks up the equipped weapon's die.

## What's next

Two of nine classes are in (a Defender and a Leader). Remaining work: the other
seven classes and their disciplines, all 16 cruxes / 6 heritages, feats, kits,
deities and equipment; higher-level bootstraps beyond level 3 (and the +P
prestige / +E epic power tiers).
