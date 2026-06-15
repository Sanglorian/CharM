# Authoring rules content (YAML → `rules.db`)

CharM can build its `rules.db` from a tree of human-authored **YAML** files. This
exists so an **open-content** database can be assembled from Open Game Content,
without the copyright-restricted WotC source data.

The YAML maps 1:1 onto the engine's `RulesElement` model and is compiled by
`CharM.RulesDb.Authoring.AuthoringCompiler` through the **same**
`RulesDbBuilder` the XML importer uses — so the resulting database is
byte-for-byte the format the app already loads.

> The encoding here mirrors the original D20Rules XML (see `docs/rules.xsd`).
> You are authoring the same mechanical vocabulary, just in a friendlier syntax.

## Quick start

```bash
# Build a database from a directory of YAML (searched recursively):
dotnet run --project src/CharM.Authoring.Cli -- build content/ -o rules.db

# Parse + validate only (no output database):
dotnet run --project src/CharM.Authoring.Cli -- lint content/
```

`build` self-checks its output by reopening it through the app's reader and
reporting the element/type counts. See `content/example/` for a worked sample.

## File shape

Each file is a **list** of elements. Every element needs `id`, `name`, `type`;
everything else is optional.

```yaml
- id: OGC_RACE_GROXLIN          # internal_id — unique, stable, OGC-namespaced
  name: Groxlin                 # display name
  type: Race                    # element type (free-form string)
  source: "Example OGC 1.0"     # optional source/book label
  prereqs: "Str 13"             # optional raw prerequisite text
  categories: [Medium]          # optional tag list (element_categories)
  fields:                       # optional ordered key/value display data
    Size: Medium
    Speed: 6 squares
    Description: >
      Multi-line prose uses YAML block scalars.
  rules:                        # optional ordered list of directives
    - statadd: Strength
      value: 2
      bonusType: Racial
```

`fields` preserve insertion order. `id`s must be unique across the whole tree.

## Directives (`rules:`)

Each rule is a mapping with **exactly one** directive key. Two styles:

- **Scalar-primary** (`statadd`, `statalias`, `grant`, `suggest`, `modify`,
  `textstring`): the key names the primary value; options are **sibling** keys.
- **Mapping** (`select`, `replace`, `drop`): fields may be **nested** under the
  key (or written as siblings — both work).

All directives also accept `level:` (int — level at which it activates) and
`requires:` (condition expression).

| Directive | Primary | Other keys |
|---|---|---|
| `statadd: <stat>` | stat name | `value` (required), `bonusType`, `condition`, `wearing`, `notWearing`, `zero`, `nonZero`, `halfPoint`, `statMin` |
| `statalias: <stat>` | stat name | `alias` (required) |
| `grant: <id>` | element id | `type` (required — the granted element's type) |
| `suggest: <id>` | element id | `type` (required) |
| `textstring: <name>` | key name | `value` (required), `condition` |
| `modify: <field>` | field name | `name`, `type`, `value`, `selectSlot`, `listAddition`, `wearing`, `dieIncrease` |
| `select:` | — | `type` (required), `number` (default 1), `category`, `name`, `displayLabel`, `prepare`, `spellbook`, `optional`, `existing`, `default`, `grant` |
| `replace:` | — | `name`, `multiclass`, `powerSwap`, `powerReplace`, `optional` |
| `drop: <id>` | element id (optional) | `selectSlot`, `name`, `type` |

Examples:

```yaml
rules:
  - grant: OGC_FEATURE_ASHEN_GUARD     # scalar-primary + sibling option
    type: Class Feature
  - textstring: Average Height
    value: 4'2"-4'8"
  - modify: Keywords                   # append to a list field
    listAddition: Fire
  - select:                            # mapping directive (nested form)
      type: Power
      number: 2
      category: "$$CLASS,at-will,1"
      name: At-Will Choice
```

## Value expressions (`statadd` `value:`)

A `statadd` `value` is either a **scalar** using the engine's compact grammar, or
a **mapping** for full control.

Scalar grammar (`ValueExpression.Parse`):

| Scalar | Meaning |
|---|---|
| `2`, `+2`, `-1` | literal integer |
| `+Strength modifier` | ability modifier of a stat |
| `+ABILITYMOD(Wisdom)` / `-ABILITYMOD(Con)` | explicit ability-mod function |
| `+HALF-LEVEL` | stat reference (accumulating) |
| `Shield Bonus` | bare name = absolute (non-stacking) stat reference |

Mapping form (use when the scalar grammar can't express it — e.g. a scale
factor other than ±1):

```yaml
value: { literal: 6 }
value: { statref: Constitution, scale: 2, abs: true }
value: { abilmod: Strength }
value: { abilmodfunc: Constitution, negate: true }
```

## Conventions

- **IDs**: namespace your own content (e.g. `OGC_*`) so it never collides with
  WotC `ID_FMP_*` ids. Internal/plumbing elements mirror `ID_INTERNAL_*` →
  `OGC_INTERNAL_*`.
- **The "Grants" indirection**: a `Race`/`Class` typically `grant`s an internal
  `Grants` element, which in turn grants size, vision, ability bonuses, and
  features. See `content/example/races.yaml`.
- **Reference rows**: enum-like types (`Size`, `Vision`, `Alignment`, …) are
  bare `id`/`name`/`type` rows that grants point at.

## Validation

`build` and `lint` both fail on **duplicate ids**, malformed YAML, and missing
required fields (with `file:line` context). Undefined `grant`/`suggest`/`select`
targets produce **warnings** (non-fatal — they may be provided by another pack).
