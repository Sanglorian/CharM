# Orcus — prerequisites / conditionals / gating that are NOT enforced

A reference list of rules text that the engine records but does **not** mechanically
enforce or apply. Each row says **why**. Two broad causes recur:

- **Grammar limits** — the prereq parser (`PrereqParser`) only understands: a named
  element's presence/absence, `Str 13`-style ability thresholds, `Nth level`,
  `any <source/role> class`, and `proficient with <X>`. Anything outside that
  (keyword/category membership, "the chosen skill", build-state) can't be expressed.
- **No stat to apply to** — the effect has no engine hook (e.g. a non-proficiency
  penalty, a conditional resistance), so it stays descriptive on the sheet.

Some rows are marked **Fixable** — they could be wired with a small change
(rephrasing the `prereqs:` text, tagging elements with a category, or extending
the grammar). Others are inherent to how the engine models characters.

---

## 1. Feat prerequisites (26 feats with a Prerequisite the engine ignores)

| Feat | Prerequisite (source) | Why it isn't enforced | Fixable? |
|---|---|---|---|
| Arcane Archer | Athame (ranged or thrown weapon) | The `(ranged or thrown weapon)` qualifier means it doesn't match the element named "Athame"; parsed as a literal name that doesn't exist | Yes — set `prereqs: Athame` |
| Athame | Proficiency with one or more focuses | Focus/implement proficiency isn't modelled as elements (no penalty in engine), so there's nothing to check | No (focus proficiency not modelled) |
| Bashing Shield | Proficiency with light shields | Text is "Proficiency with…"; parser needs "proficient with…". Even fixed, needs a "Shield Proficiency (Light shield)" element to match | Partly |
| Toughened Shield | Proficiency with heavy shields | Same as Bashing Shield | Partly |
| Armor Proficiency | Str/Con + proficiency with a lower armor (varies by armor) | Choice-dependent (which armor) **and** "proficiency with…" phrasing; the requirement differs per armor picked | No (choice-dependent) |
| Shield Proficiency | Str + proficient with light shields (varies) | Choice-dependent (light vs heavy) + phrasing | No (choice-dependent) |
| Fling Familiar | You have a familiar | "has a familiar" is category/keyword membership, not a named element; grammar can't express it | Fixable w/ category check |
| Blessing of the God | You have the Channel Divinity feature | No element literally named "Channel Divinity feature"; keyword/feature membership not expressible | Fixable w/ category check |
| Night Sight | Low-light vision | Vision is a descriptive trait, not a checkable element | No (vision not modelled as element) |
| Skill Focus | At least one rank in the chosen skill | "the chosen skill" refers to this feat's own per-instance pick, which prereq evaluation can't see | No (choice-dependent) |
| Hardy Shift | You know a Form power | "any power with the Form keyword" = keyword membership, not a named element | Fixable w/ category check |
| Hybrid Form | At least one power with the Form keyword | Same as Hardy Shift | Fixable w/ category check |
| Versatile Shifting | You know at least one shape of the *X* power | Templated ("X") + references the chosen power instance | No (templated/choice-dependent) |
| Dualclass Recruit | You cannot already have a secondary class | "has a secondary class" is build-state, not a named element | Fixable w/ build-state check |
| Adaptation | Psi focus power | "has a Psi focus power" = category membership, not a named element | Fixable w/ category check |
| Immovable Dominion | Psi focus power | category membership | Fixable w/ category check |
| Mind and Body | Psi focus power, focus surge | category + named-power membership | Fixable w/ category check |
| Mind-Eye Accuracy | Psi focus power, careful focus power | category + named power | Fixable w/ category check |
| Phrenic Breath | Psi focus power, breath weapon power | category + named power | Fixable w/ category check |
| Phrenic Dodge | Psi focus power, lucky power | category + named power | Fixable w/ category check |
| Phrenic Meditation | Psi focus power, can perform the Meditate action | category + an action capability (not an element) | Partly |
| Phrenic Reservoir | Psi focus power, Dabbler ancestry feature | category + a feature | Fixable w/ category check |
| Phrenic Talent | Psi focus power | category membership | Fixable w/ category check |
| Phrenic Teleport | Psi focus power, highblood teleport power | category + named power | Fixable w/ category check |
| Phrenic Wrath | Psi focus power, vengeance of the pits power | category + named power | Fixable w/ category check |
| Surging Mind | Psi focus power | category membership | Fixable w/ category check |

> The 12 Psi/Phrenic + Form/familiar/Channel-Divinity feats all share one root
> cause: they require "you have **a** power/feature of kind X", and the grammar
> only checks for **one specific named** element. Tagging those powers with a
> category and adding a `has-category` prereq form would wire most of them at once.

## 2. Prestige (paragon) path entry requirements

Enforced today: **Battlefield Healer** (Heal), **Shadowsneak** (Stealth),
**Silver Tongue** (Diplomacy), **Ring Fighter** (Unarmed Combat feat).

| Path | Requirement (source) | Why it isn't enforced | Fixable? |
|---|---|---|---|
| Assassin | Proficiency with simple melee and ranged weapons | Group proficiency — no single element represents "simple melee/ranged weapons"; also "Proficiency with…" phrasing | Partly (needs group markers) |
| Breathstealer | Proficiency with garrote | "Proficiency with…" phrasing; needs a "Weapon Proficiency (Garrote)" element to match | Yes (rephrase + element) |
| Darkwood Archer | Proficiency with military ranged weapons | Group proficiency, as Assassin | Partly |
| Deadeye Arbalester | Proficiency with light and heavy crossbows | Two single proficiencies; phrasing + per-weapon elements | Partly |
| Spellwright | Arcane class | Parser needs "**any** arcane class"; "Arcane class" alone parses as a (missing) element name | Yes — phrase as "any arcane class" |
| Weapon Master | One or more of your powers has the Martial tag | Keyword membership over the power list — not expressible in the grammar | Fixable w/ keyword check |

Blank-requirement paths (Bounty Hunter, Devotee, Ironsides, Martial Arts
Champion, Ruler of Shadows, Selfless Protector, Tactician) correctly have none.
**Elocater, Invested, Pyromancer** aren't in the requirements table (no stated
prereq). **Epic** path requirements are narrative ("you must have retrieved
something of value from another plane…") and are roleplay/GM gates by design —
nothing mechanical to enforce.

## 3. Conditional / always-on effects not applied

| Where | Condition / trait | Why it isn't applied |
|---|---|---|
| Focus classes (Magician, etc.) | Focus/implement proficiency (orb/staff/wand/rod/book) | The engine applies no penalty for using a non-proficient implement, so proficiency is recorded descriptively only |
| Species (`ancestries-species.yaml`) | Resistances, natural weapons, extra fly/swim speeds, other conditional traits | These don't map to a single stat the engine applies (e.g. conditional/typed resistance, a natural weapon as a wieldable weapon); recorded as descriptive fields |
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
