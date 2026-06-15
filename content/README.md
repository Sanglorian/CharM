# `content/` — authored rules content

YAML rules content compiled into a CharM `rules.db` by the authoring pipeline.
This is the home for an **open-content** rules database built from Open Game
Content — no copyright-restricted WotC data belongs here.

- Format reference: [`docs/authoring.md`](../docs/authoring.md)
- Compiler / CLI: `src/CharM.Authoring.Cli`
- Worked example: [`example/`](example/) — a small, self-consistent vertical
  slice (a race with the "Grants" indirection, a class, a feature, a power, a
  feat, a magic item, and the size/vision plumbing) that exercises every
  directive and value-expression form.

## Build it

```bash
# Validate without producing a database:
dotnet run --project ../src/CharM.Authoring.Cli -- lint example/

# Compile to a database (and self-check that it loads):
dotnet run --project ../src/CharM.Authoring.Cli -- build example/ -o /tmp/example-rules.db
```

The example is named with fictional placeholders on purpose. Real content should
use stable, namespaced ids (e.g. `OGC_*`) — see the conventions section of the
format reference.
