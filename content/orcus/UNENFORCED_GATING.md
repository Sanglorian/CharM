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
- **Versatile Shifting** → `category:ORCUS_SHAPE_POWER`. The 20 `Shape of the …`
  shapeshift powers are tagged with that category in the discipline generator
  (`Phase2`, by name prefix).
- **Dualclass Recruit** (and the Dabbles-in multiclass kits) → a shared
  `Secondary Class` marker is granted by each secondary-class source, and each
  requires `!Secondary Class`, so a character can have at most one secondary
  class. (Theme/kit selects honour prereqs, verified at runtime.)
- **Athame** → `category:ORCUS_FOCUS_PROFS`. Focus/implement proficiency is now
  modelled: `Focus Proficiency (Orb/Staff/Wand/Rod/Book/Holy Symbol/Martial/
  Druidic)` elements (`ORCUS_FOCUS_PROFS`), granted from each caster's "Focus
  Proficiencies" (Magician, Priest, Commander, Harlequin, Sylvan) and the Heretic
  heritage.
- **Dabbles-in kits' class-identity gate** ("you cannot take this if you belong
  to the X class") — KitGen extracts X from the Requirements and adds `, !<Class>`
  to the kit's prereq (so a Commander can't take Dabbles in Commanding, etc.).

---

## 1. Feat prerequisites

**Every feat/path prerequisite from the original audit is now enforced.** What
remains below is not prerequisite gating but effects the engine has no hook for.

## 2. Prestige path entry requirements

**All stated prestige-path requirements are now enforced**: Battlefield Healer,
Shadowsneak, Silver Tongue, Ring Fighter, Spellwright, Weapon Master, Assassin,
Darkwood Archer, Deadeye Arbalester, and Breathstealer (garrote). Blank-
requirement paths have none; **epic** path requirements are narrative roleplay
gates by design.

## 3. Conditional / always-on effects not applied

| Where | Condition / trait | Why it isn't applied |
|---|---|---|
| All classes | **Non-proficiency penalty for a focus** ("no benefit from a focus unless proficient") | The armor/shield (-2 Reflex + attack) and weapon (lose the proficiency bonus) penalties are now applied; the focus one isn't — it would mean withholding the focus's enhancement bonus from Focus powers, which needs an engine change (focus proficiency isn't indexed like weapon/implement proficiency). Low value — casters are proficient with their own focuses |
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
