# Orcus — prerequisites / conditionals / gating that are NOT enforced

A reference list of rules text that the engine records but does **not** mechanically
enforce or apply. Each row says **why**. Two broad causes recur:

- **Grammar limits** — the prereq parser (`PrereqParser`) understands: a named
  element's presence/absence, `Str 13`-style ability thresholds, `Nth level`,
  `any <source/role> class`, `proficient with <X>`, and (added) `category:<ID>`
  and `keyword:<Tag>` membership. Anything outside that ("the chosen skill",
  build-state, group proficiency) still can't be expressed.
- **No stat to apply to** — the effect has no engine hook (e.g. a non-proficiency
  penalty, a conditional resistance), so it stays descriptive on the sheet.

## Recently wired (now enforced)

A `category:<ID>` / `keyword:<Tag>` prereq form was added to `PrereqParser` +
`PrereqEvaluator` (checks whether the character has **any** active element in a
category, or with a Keywords-field tag). With it:

- **Fling Familiar** → `category:ORCUS_FAMILIAR` (you have a familiar).
- **Hardy Shift**, **Hybrid Form** → `keyword:Form` (you have a Form power).
- **Spellwright** (prestige) → `category:Arcane` (arcane class — only Mageblade
  and Magician carry the Arcane category).
- **Weapon Master** (prestige) → `keyword:Martial` (a power with the Martial tag).
- The **12 Psi / Phrenic feats** → `Wild Talent or Psi Focus` (existing `or`
  grammar). The *psi focus power* lives inside the Wild Talent feat (and the
  Channels Godmind kit's "Psi Focus" feature), so that's the gate. **Note:** the
  secondary named-power requirements on some of them (focus surge, careful focus,
  breath weapon, lucky, highblood teleport, vengeance of the pits) reference
  powers that aren't modelled as elements, so only the psi-focus gate is checked.

Everything below is still unenforced.

---

## 1. Feat prerequisites still unenforced (10)

| Feat | Prerequisite (source) | Why it isn't enforced | Fixable? |
|---|---|---|---|
| Arcane Archer | Athame (ranged or thrown weapon) | The `(ranged or thrown weapon)` qualifier stops it matching the element named "Athame" | Yes — set `prereqs: Athame` |
| Athame | Proficiency with one or more focuses | Focus/implement proficiency isn't modelled as elements (no penalty in engine) | No (focus proficiency not modelled) |
| Bashing Shield | Proficiency with light shields | "Proficiency with…" phrasing; needs a "Shield Proficiency (Light shield)" element to match | Partly |
| Toughened Shield | Proficiency with heavy shields | As Bashing Shield | Partly |
| Armor Proficiency | Str/Con + proficiency with a lower armor (varies) | Choice-dependent (which armor) + phrasing | No (choice-dependent) |
| Shield Proficiency | Str + proficient with light shields (varies) | Choice-dependent (light vs heavy) + phrasing | No (choice-dependent) |
| Night Sight | Low-light vision | Vision is a descriptive trait, not a checkable element | No (vision not modelled) |
| Skill Focus | At least one rank in the chosen skill | "the chosen skill" is this feat's own per-instance pick, invisible to prereq evaluation | No (choice-dependent) |
| Versatile Shifting | You know at least one shape of the *X* power | Templated ("X") + references the chosen power instance | No (templated/choice-dependent) |
| Dualclass Recruit | You cannot already have a secondary class | "has a secondary class" is build-state, not a named element | Fixable w/ build-state check |

## 2. Prestige (paragon) path entry requirements

Enforced: Battlefield Healer (Heal), Shadowsneak (Stealth), Silver Tongue
(Diplomacy), Ring Fighter (Unarmed Combat feat), **Spellwright** (arcane class),
**Weapon Master** (Martial-tag power). Still unenforced:

| Path | Requirement (source) | Why it isn't enforced | Fixable? |
|---|---|---|---|
| Assassin | Proficiency with simple melee and ranged weapons | Group proficiency — no single element represents "simple melee/ranged weapons" | Partly (needs group markers) |
| Breathstealer | Proficiency with garrote | "Proficiency with…" phrasing; needs a "Weapon Proficiency (Garrote)" element | Yes (rephrase + element) |
| Darkwood Archer | Proficiency with military ranged weapons | Group proficiency, as Assassin | Partly |
| Deadeye Arbalester | Proficiency with light and heavy crossbows | Two single proficiencies; phrasing + per-weapon elements | Partly |

Blank-requirement paths (Bounty Hunter, Devotee, Ironsides, Martial Arts
Champion, Ruler of Shadows, Selfless Protector, Tactician) correctly have none.
**Elocater, Invested, Pyromancer** aren't in the requirements table (no stated
prereq). **Epic** path requirements are narrative ("you must have retrieved
something of value from another plane…") — roleplay/GM gates by design.

## 3. Conditional / always-on effects not applied

| Where | Condition / trait | Why it isn't applied |
|---|---|---|
| Focus classes (Magician, etc.) | Focus/implement proficiency (orb/staff/wand/rod/book) | The engine applies no penalty for a non-proficient implement, so proficiency is recorded descriptively only |
| Species (`ancestries-species.yaml`) | Resistances, natural weapons, extra fly/swim speeds, other conditional traits | These don't map to a single stat the engine applies; recorded as descriptive fields |
| Cruxes / heritages (`ancestry.yaml`) | Always-on narrative traits | No stat hook; descriptive |
| Powers (many) | `Requirements:` line (e.g. "Requirements: you have something the target finds hard to resist") | 4e power-use requirements are situational/fiction-based; the engine never gates power **use** — player-adjudicated |

## 4. Gating not enforced

| System | Gate (source) | Why it isn't enforced |
|---|---|---|
| Arts (`arts.yaml`, as Rituals) | Practices need training in the associated skill; the Gated Arts variant needs the Incantation Caster / Practical Arts feats | The rituals/practices catalogue (`FindByType("Ritual")`) lists every art as learnable/purchasable and does **not** run prerequisites |
| Magic items | — | None of the Orcus magic items carry a prerequisite/requirement, so nothing is missed here |

---

*Generated as a manual audit; not consumed by the build. Update by hand if the
prereq grammar, proficiency model, or these elements change.*
