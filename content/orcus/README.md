# `content/orcus/` — Orcus rules content

Open Game Content transcribed from the repo's `Orcus *.md` rulebooks into the
CharM YAML authoring format. See [`../../docs/orcus-mapping.md`](../../docs/orcus-mapping.md)
for the concept mapping and [`../../docs/authoring.md`](../../docs/authoring.md)
for the format.

## Current scope — a level-1 Guardian vertical slice

| File | Contents |
|---|---|
| `reference.yaml` | Sizes, vision, the 17 Orcus skills, Guardian skill-training rows, ability-bonus rows |
| `ancestry.yaml` | Humanity (base Race) + sample Cruxes (Hero, Sage) and Heritages (Aristocrat, Seafarer) |
| `classes/guardian.yaml` | Guardian class, its Grants bundle, features, two talents, and feature powers |
| `disciplines/art-of-war.yaml` | The Art of War discipline + its level 1–2 powers |

Compiles to **55 elements across 13 types**, no warnings.

## Build

```bash
dotnet run --project ../../src/CharM.Authoring.Cli -- build . -o /tmp/orcus.db
```

## What's next

This proves real Orcus content flows through the pipeline. Remaining work:
the other 8 classes, the rest of the disciplines, all cruxes/heritages, feats,
kits, deities and equipment — plus the engine-vocabulary alignment listed in
`docs/orcus-mapping.md` needed for a character to compute fully in-app.
