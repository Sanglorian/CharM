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

Compiles to **100 elements across 16 types**, no warnings.

### Ability substitution
Orcus's "you may use your class key ability instead, if higher" is implemented
via a small, **isolated** engine enhancement plus content tags. A class‑discipline
power is tagged `ability-swap` and keeps its printed key ability; each class
grants a `Key Ability Swap` element naming its key. The engine adds that ability
to the power's candidates and takes the **higher**. Equipment‑independent and
**kit‑scalable** (each character contributes only its own key). WotC content has
neither the tag nor the elements, so it's unaffected. Verified via `playtest`
with no weapon/implement: high‑Wis Priest → Wisdom, **high‑Cha Priest → Charisma
(no false negative)**, high‑Wis Commander → Charisma (no false positive),
Guardian → Strength. See `docs/orcus-mapping.md`.

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
`--class Commander --level 3`.

## What's next

Two of nine classes are in (a Defender and a Leader). Remaining work: the other
seven classes and their disciplines, all 16 cruxes / 6 heritages, feats, kits,
deities and equipment; higher-level bootstraps beyond level 3 (and the +P
prestige / +E epic power tiers).
