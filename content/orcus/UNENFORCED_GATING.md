# Orcus — prerequisites / conditionals / gating that are NOT enforced

A reference list of rules text the engine records but does **not** mechanically
enforce, each with **why**. An earlier version of this file was too pessimistic:
vision, weapon groups/categories, and the "named ancestry power" prerequisites
**are** modelled as elements, and are now enforced. What remains below is the
genuinely-unmodelled / inexpressible residue.

How prereqs are checked: `PrereqParser` understands a named element's
presence/absence, `Str 13` ability thresholds, `Nth level`, `any <source/role>
class`, `proficient with <X>`, `or` / comma-AND, and `category:<ID>` /
`keyword:<Tag>` membership. Enforcement runs through `CreatePrereqFilter(state)`
during selection (verified at runtime — e.g. a Magician can take Spellwright but
not Weapon Master or Assassin; a Guardian vice-versa; Night Sight needs a
low-light species).

## Now enforced (was previously listed here as "not modelled")

- **Vision** — `Vision` elements (Normal / Low-light / Darkvision, granted by
  species). `Night Sight` → `prereqs: "Low-light"`.
- **Weapon groups / categories** — each class grants per-category `Grants`
  bundles ("Simple Melee Weapon Proficiency", "Martial Ranged Weapon
  Proficiency", …) plus per-weapon `Proficiency` elements. So: Assassin →
  `Simple Melee Weapon Proficiency, Simple Ranged Weapon Proficiency`; Darkwood
  Archer → `Martial Ranged Weapon Proficiency`; Deadeye Arbalester →
  `Weapon Proficiency (Light crossbow), Weapon Proficiency (Heavy crossbow)`;
  Bashing/Toughened Shield → `Armor Proficiency (Light/Heavy Shield)`.
- **Named ancestry powers** — Careful Focus, Lucky, Breath Weapon, Highblood
  Teleport, Vengeance of the Pits all exist as elements and are now AND-ed into
  the relevant Phrenic feats. (Spellwright/Weapon Master/Fling Familiar/Hardy
  Shift/Hybrid Form and the psi-focus gate were wired in the previous pass.)
- **Transcribed the last three missing elements** and wired them:
  - **Garrote** (`equipment/garrote.yaml`) — the exotic special weapon + a
    `Weapon Proficiency (Garrote)` element (tagged `ORCUS_WEAPON_PROFS`, so the
    Weapon Proficiency feat can grant it). Breathstealer → that proficiency.
  - **Dabbler** (`ORCUS_FEATURE_DABBLER`, granted by the Destined crux) — Phrenic
    Reservoir's "Dabbler ancestry feature" clause.
  - **focus surge / the Meditate action** both come from the existing **Focused**
    feature (no new element needed): Mind and Body and Phrenic Meditation now
    AND-in `Focused`.
- **Choice-dependent proficiency/skill feats** (Skill Focus, Armor Proficiency,
  Shield Proficiency) are now enforced by moving the prereq onto each *option*
  the feat selects (the requirement depends on *which* skill/armor/shield you
  pick, which a feat-level prereq can't see). The key engine fact: **`select`
  candidates are prereq-filtered, but `grant`s are not** — so a per-option prereq
  gates the feat's pick without affecting a class's grant of the same element.
  - **Skill Focus** — each `Skill Focus (X)` option carries `prereqs: "X"`, so
    only trained skills are offered.
  - **Armor Proficiency** — each armor's `Proficiency` element carries its prereq
    (Hide → `Str 13, Con 13, Armor Proficiency (Leather)`; Chainmail →
    `… Leather or … Hide`; Scale → `… Chainmail`; Plate →
    `Str 15, Con 15, Armor Proficiency (Scale)`). The lower-armor chain resolves
    naturally; class-granted armor is unaffected.
  - **Shield Proficiency** — Light → `Str 13`; Heavy →
    `Str 15, Armor Proficiency (Light Shield)`.

---

## 1. Feat prerequisites still unenforced

| Feat | Prerequisite (source) | Why it isn't enforced |
|---|---|---|
| Athame | Proficiency with one or more focuses | Focus/implement proficiency is a descriptive field on the class, not a grantable element (the engine applies no non-proficiency penalty) |
| Versatile Shifting | You know at least one shape of the *X* power | The "shape of the X" power family isn't tagged with a category yet (fixable: tag the 20 `Shape of the …` powers + `category:` prereq) |
| Dualclass Recruit | You cannot already have a secondary class | No element represents "has a secondary class" yet (fixable: a shared `Secondary Class` marker granted by the dualclass feat + Dabbles-in kits, gated `!Secondary Class`) |

## 2. Prestige path entry requirements

**All stated prestige-path requirements are now enforced**: Battlefield Healer,
Shadowsneak, Silver Tongue, Ring Fighter, Spellwright, Weapon Master, Assassin,
Darkwood Archer, Deadeye Arbalester, and Breathstealer (garrote). Blank-
requirement paths have none; **epic** path requirements are narrative roleplay
gates by design.

## 3. Conditional / always-on effects not applied

| Where | Condition / trait | Why it isn't applied |
|---|---|---|
| Focus classes (Magician, etc.) | Focus/implement proficiency (orb/staff/wand/rod/book) | The engine applies no penalty for a non-proficient implement, so it's a descriptive field |
| Species (`ancestries-species.yaml`) | Conditional resistances, natural weapons, extra fly/swim speeds | These don't map to a single stat the engine applies; recorded as descriptive fields |
| Cruxes / heritages (`ancestry.yaml`) | Always-on narrative traits | No stat hook; descriptive |
| Powers (many) | `Requirements:` line (situational, e.g. "you have something the target finds hard to resist") | 4e power-use requirements are fiction-based; the engine never gates power **use** — player-adjudicated |

## 4. Gating not enforced

| System | Gate (source) | Why it isn't enforced |
|---|---|---|
| Arts (`arts.yaml`, as Rituals) | Practices need training in the associated skill; Gated Arts variant needs the Incantation Caster / Practical Arts feats | The rituals/practices catalogue (`FindByType("Ritual")`) lists every art as learnable/purchasable and does **not** run prerequisites |

---

*Manual audit; not consumed by the build. Update by hand if the prereq grammar,
proficiency/vision model, or these elements change.*
