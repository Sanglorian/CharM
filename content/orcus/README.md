# `content/orcus/` — Orcus rules content

Open Game Content transcribed from the repo's `Orcus *.md` rulebooks into the
CharM YAML authoring format. See [`../../docs/orcus-mapping.md`](../../docs/orcus-mapping.md)
for the concept mapping and [`../../docs/authoring.md`](../../docs/authoring.md)
for the format.

## Current scope — a level-1 Guardian vertical slice

| File | Contents |
|---|---|
| `_internal/level.yaml` | Creation bootstrap (`ID_INTERNAL_LEVEL_1`): seeds Race/Class slots + core stat formulas |
| `reference.yaml` | Sizes, vision, the 17 Orcus skills, Guardian skill-training rows, ability-bonus rows |
| `ancestry.yaml` | Humanity (base Race) + sample Cruxes (Hero, Sage) and Heritages (Aristocrat, Seafarer) |
| `classes/guardian.yaml` | Guardian class, its Grants bundle, features, two talents, and feature powers |
| `disciplines/art-of-war.yaml` | The Art of War discipline + its level 1–2 powers |

Compiles to **57 elements across 15 types**, no warnings.

## Build & playtest

```bash
# Compile to a database:
dotnet run --project ../../src/CharM.Authoring.Cli -- build . -o /tmp/orcus.db

# Headlessly build a level-1 character and print computed stats + any unfillable
# slots (the engine-alignment check):
dotnet run --project ../../src/CharM.Authoring.Cli -- playtest /tmp/orcus.db
```

A Humanity Guardian builds end-to-end: every slot resolves (ability bonuses,
crux, heritage, 3 trained skills, talent, 2 at-will / 1 encounter / 1 daily
power) and stats compute correctly (HP 31, Fortitude 15, Will 12 with the
Aristocrat +1, trained skills applied).

## What's next

The pattern is proven and playable. Remaining work: the other 8 classes, the
rest of the disciplines, all cruxes/heritages, feats, kits, deities and
equipment; the remaining 12 skills' computation rows; and the higher-level
`ID_INTERNAL_LEVEL_<n>` bootstraps for levels >1 (HP/level scaling, etc.).
A content tagging nicety: utility powers are currently eligible for attack
selects — tighten the discipline category tags to separate them.
