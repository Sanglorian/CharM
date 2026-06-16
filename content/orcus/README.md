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
| `feats.yaml` | A sample of 22 heroic-tier general feats (the pool for the level 1 + even-level feat slots) |
| `classes/guardian.yaml` | Guardian (Defender): Grants bundle, features, talents, feature powers, level-gated power selects to 30 |
| `classes/commander.yaml` | Commander (Leader): talents, armament, Lift Spirits, level-gated power selects to 30 |
| `classes/priest.yaml` | Priest (Leader, Wisdom): talents + the key-ability substitution override (shares Angel's Trumpet with the Commander) |
| `disciplines/art-of-war.yaml` | Art of War discipline (Guardian) + powers across the level range |
| `disciplines/juggernautical.yaml` | Juggernautical discipline (Guardian) + powers across the level range |
| `disciplines/angels-trumpet.yaml` | Angel's Trumpet discipline (Commander/Priest) + powers across the level range |
| `disciplines/golden-lion.yaml` | Golden Lion discipline (Commander) — **all** powers, levels 1–29 |
| `paths/prestige.yaml` | Sample prestige paths (Assassin, Battlefield Healer, Bounty Hunter): 11th/16th features + powers at 11/12/20 |
| `paths/epic.yaml` | All six epic paths (Agent Retriever, Master, Most Dangerous, Respected, Team, Ultimate): 21st/24th/30th features + a 26th-level power |
| `equipment/weapons.yaml` | 19 weapons (simple/martial/exotic, melee & ranged) as `Weapon` elements — supply the `[W]` die, proficiency and group |
| `equipment/armor.yaml` | Light & heavy armor + shields as `Armor` elements (AC / Reflex / speed / armor-check contributions) |
| `equipment/gear.yaml` | Adventuring gear (`Gear`) and focuses (`Focus`) |
| `equipment/proficiencies.yaml` | Weapon/armor `Proficiency` elements + per-category `Grants` bundles the classes pull in |
| `equipment/magic-items.yaml` | The four generic enchanted-item families (Weapon, Focus, Armor, Cloak) at +1…+6 as `Magic Item` elements |
| `kits.yaml` | Kits (mapped to the engine's `Theme` type) + the "Has Kit" marker and "Feats and Kits" house-rule elements |
| `deities.yaml` | The four gods (`Deity`) referenced by the "Worships the God of …" kits |

Compiles to **458 elements across 31 types**, no warnings.

### Kits and the feats-vs-kits house rule
Kits (Orcus's "themes", mapped to the engine's `Theme` type so the optional slot
surfaces) are chosen at level 1 and grant features at levels 1/5/10. By default a
character takes a kit **instead of** the six heroic-tier feats (levels 1/2/4/6/8/10)
— kit XOR feats. This is enforced by content: every kit grants a "Has Kit" marker,
and the six heroic feat slots carry `requires: "(!Has Kit) | Feats and Kits"`, so
they vanish once a kit is taken. The **"Feats and Kits" house rule** is an element
that, when present, satisfies the `requires` and re-enables the feats — so you get
both. Paragon/epic feats (level 12+) are never affected. Verified via `playtest`:
no kit → 6 heroic feats; `--kit "Embodies Strength"` → 0 heroic feats (kit
features instead); `--kit … --feats-and-kits` → kit **and** 6 feats; at level 30 a
kitted character still gets its 10 paragon/epic feats. The "Worships the God of …"
kits are the deity/domain mechanic (Channel Divinity + a Blessing feat), pointing
at the gods in `deities.yaml`.

### Equipment
Weapons are `Weapon` elements: equipping one feeds power damage (the `Damage`
field supplies the `[W]` die — e.g. *In Their Face* reads `2[W]`, which becomes
`2d10` with a greatsword or `2d6` with a sling), and `Proficiency Bonus` / `Group`
drive attack bonuses and weapon-group rules. Armor and shields are `Armor`
elements whose `statadd` rules contribute to AC (and Reflex, speed, and the
physical skills). The AC maths stays faithful to 4e: each armor adds its bonus
over cloth's base 10, and the bootstrap's Dex/Int-to-AC contributions carry
`notWearing: armor:heavy`, so **light armor keeps your ability bonus to AC while
heavy armor suppresses it** (the engine registers `armor:heavy` from the armor's
`Armor Type`). Verified via `playtest`: unarmored AC 11 → 20 in plate + heavy
shield (Dex/Int dropped, +8 +2 added), leather → AC 13 (ability kept), chainmail
→ AC 16 (ability dropped); speed drops 6→5 in plate; and weapon dice flow into
power damage. Try `playtest --weapon Greatsword --armor "Plate armor" --shield "Heavy shield"`.

**Proficiency** is wired functionally. The engine tracks weapon training by exact
weapon name (a `Weapon Proficiency (<weapon>)` `Proficiency` element), and only
adds a weapon's `Proficiency Bonus` to the attack roll when the wielder is
trained. So each class's category proficiency is expanded (in
`proficiencies.yaml`) into per-weapon elements grouped into `Grants` bundles the
class grants. Verified: a Guardian wielding a longsword attacks at +6 (ability +3
**+3 longsword proficiency**); with the exotic garrote it drops to +3 (the
Guardian isn't proficient, so no bonus). Armor proficiencies are recorded too,
but the engine has no armor non-proficiency penalty yet, so those are declarative.

**Magic items** follow Orcus's generic "enchanted item" model — four families
(Weapon, Focus, Armor, Cloak), each a +X enhancement appearing at levels
1/6/11/16/21/26 (`magic-items.yaml`). Enchanted armor and cloaks carry `statadd`
rules, so equipping them raises AC / the three defenses; enchanted weapons and
focuses carry an `Enhancement` field, which the calculator adds to attack and
damage. Verified: a +3 cloak adds +3 to Fortitude/Reflex/Will, +3 armor adds +3
AC, and a +3 longsword takes *In Their Face* from +12 / 2d8 to +15 / 2d8+3. Try
`playtest --weapon Longsword --magic "Enchanted Weapon +3"` or
`--magic "Enchanted Cloak +3"`. The level-scaling "boost" variants (which only
raise item level) and the enchanted-armor light/heavy AC scaling are recorded in
the item Description rather than computed.

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
its 16th/20th-level path benefits early. Feats are gained at level 1 and every
even level (16 by level 30); the slots live in `ID_INTERNAL_LEVEL_1` with per-slot
`level:` gates and draw from `feats.yaml`. Defense/skill/HP feats apply a `Feat`
bonus (which, being same-named, does not stack between feats — matching the
rules); tier scaling at 11/21 and the narrative-only feats are kept in the Benefit
text. Verified via `playtest`: Guardian, Commander and Priest all build end-to-end
at level 30 (and at the 11/21 tier boundaries) with every slot — powers, ability
increases, paths and all 16 feats — filled.

### Power-replacement swaps
The progression table's optional swaps — give up one class attack power and gain
one of the level you're reaching — are declared on each class with the engine's
`replace` directive (encounter at 13/17/23/27, daily at 15/19/25/29; each carries
a `powerSwap` filter `"<disciplines>,<freq>,<maxlevel>"` and is `optional`). The
headless builder doesn't yet auto-generate candidates from a `replace` directive
(that wiring is deferred in the engine — `PowerSwap` is currently declarative), so
swaps are applied through the working `ElementReplacement` retrain path. The
`playtest --swaps` flag demonstrates this end-to-end: at each replacement level it
drops the lowest-level held attack power of the right frequency and swaps in the
highest-level one from your disciplines, including **chained** re-swaps at epic
levels (e.g. a Commander's Charge of the Battle Cat → Lion Lord's Agony at 27).
Try `playtest --class Commander --level 30 --swaps`.

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
a specific discipline power, e.g. `--pick "Pack Pounce"`). `--swaps` applies a
sample power-replacement chain (levels 13–29) and prints the resulting powers.
`--weapon`/`--armor`/`--shield` equip gear by name and show its effect on AC,
defenses, speed and weapon damage; `--magic` (repeatable) applies an enchanted
item (weapon/focus enhancement, or armor/cloak when equipped). `--kit <name>`
takes a kit and `--feats-and-kits` enables the house rule that grants both.

Weapon dice written `dW`/`NdW` are normalized to the engine's `[W]`/`N[W]`, so a
power like *The Finisher* (`3dW`) picks up the equipped weapon's die.

## What's next

Three of nine classes are in (a Defender and two Leaders), playable across the
full 1–30 range, with both Guardian disciplines (Art of War, Juggernautical) and
both Commander disciplines (Angel's Trumpet, Golden Lion — the latter transcribed
in full to level 29), 14 species ancestries, a sample of feats, a sample of
prestige paths, all six epic paths, and a starter set of weapons, armor and gear.
Remaining work: the other six classes and their disciplines, full power lists for
the remaining disciplines, the rest of the species roster, prestige paths and
feats (including paragon/epic-tier and multi-take feats), all 16 cruxes /
6 heritages, the rest of the kits (incl. the "Dabbles in …" multiclass kits) and
their associated-discipline power access, the magic-item boost variants, and the
rest of the equipment list. The
power-replacement swaps are declared on the classes and demonstrated via
`--swaps`; wiring a `replace` directive's `PowerSwap` into auto-generated wizard
candidates is an engine-side follow-up.
