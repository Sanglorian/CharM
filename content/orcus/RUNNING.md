# Running the Orcus ruleset in CharM

How to build the Orcus rules database and load it in the CharM apps. Run all
commands from the repository root. Requires the **.NET 10 SDK**.

> The `NU1903` SQLite "known vulnerability" lines during a build are a harmless
> NuGet warning, not an error.

## 1. Build the Orcus rules database

This compiles every YAML file under `content/orcus/` into a single SQLite db.

**Windows (PowerShell):**
```powershell
dotnet run --project src\CharM.Authoring.Cli -- build content\orcus -o "$PWD\orcus-rules.db"
```

**macOS / Linux:**
```bash
dotnet run --project src/CharM.Authoring.Cli -- build content/orcus -o "$PWD/orcus-rules.db"
```

A correct build reports something like `Compiled 2345 element(s)` and
`Verified: 2345 element(s) across 40 type(s) load cleanly.`

## 2. Run the web app

The runnable web host is **`src/CharM.Web`** (an ASP.NET Core app).
`src/CharM.Web.UI` is only a Razor component **library** — running it directly
fails with *"The current OutputType is 'Library'."*, so always run `CharM.Web`.

Point it at the database you just built with an **absolute path**:

**Windows (PowerShell):**
```powershell
dotnet run --project src\CharM.Web -- --rules-db-path "$PWD\orcus-rules.db"
```

**macOS / Linux:**
```bash
dotnet run --project src/CharM.Web -- --rules-db-path "$PWD/orcus-rules.db"
```

Then open the URL it prints (typically `http://localhost:5000`).

> First run compiles Tailwind CSS and downloads a `three.js` asset; that needs
> internet. If it's already present or you want to skip it, add
> `-p:SkipThreeJsDownload=true -p:BuildTailwindCss=false`.

## Where to put the database file

The app loads the **first file that exists** from this list, in order:

1. `--rules-db-path <path>` (also accepts `--rules`, `--rules-db`, `--rulesdb`,
   or a bare `*.db` argument) — resolved to an absolute path from the app's
   working directory
2. `<working directory>\rules.db`
3. `<app bin folder>\rules.db`
4. an ancestor `rules.db` (searching up to 6 folders up) — from the working
   directory, then from the bin folder
5. `%LocalAppData%\CharM\rules.db` (Windows) / `~/.local/share/CharM/rules.db`

**Use an absolute path** (step 1) to avoid ambiguity — a *relative*
`--rules-db-path` is resolved from the app's working directory, which under
`dotnet run` is not necessarily the repo root, so it can silently fall through
to an old `rules.db` somewhere else.

Alternatively, install it to the canonical location so it's found with **no
arguments**:

**Windows (PowerShell):**
```powershell
mkdir "$env:LOCALAPPDATA\CharM" -Force
dotnet run --project src\CharM.Authoring.Cli -- build content\orcus -o "$env:LOCALAPPDATA\CharM\rules.db"
dotnet run --project src\CharM.Web
```

**macOS / Linux:**
```bash
mkdir -p ~/.local/share/CharM
dotnet run --project src/CharM.Authoring.Cli -- build content/orcus -o ~/.local/share/CharM/rules.db
dotnet run --project src/CharM.Web
```

## 3. Headless playtest (optional, no UI)

Quickly builds a character and prints its stats — useful for sanity checks.

```bash
dotnet run --project src/CharM.Authoring.Cli -- playtest orcus-rules.db --class Mageblade --level 10
```

Useful flags: `--race <name>`, `--weapon/--armor/--shield "<name>"`,
`--kit "<name>"`, `--feats-and-kits`, `--swaps`.

## 4. Re-run the fidelity audit (optional)

Checks every transcribed prose field verbatim against the source books.
Target: `0 flagged, 0 invented`.

```bash
dotnet run --project tools/CharM.Orcus.Import -- audit .
```

## Troubleshooting

- **"Only six kits available" (or other content missing).** The app is loading
  an **old/stale `rules.db`**, not your fresh Orcus build. A current build has
  **25 kits**; the six are the ones in `kits.yaml` (an older db built before
  `kits-extra.yaml` existed). Fixes:
  - Use an **absolute** `--rules-db-path` (above), and confirm the app's
    settings/database panel shows it pointing at your `orcus-rules.db`.
  - Find and remove the stale db that's being picked up:
    ```powershell
    Get-ChildItem "$PWD\rules.db","$env:LOCALAPPDATA\CharM\rules.db" -ErrorAction SilentlyContinue
    ```
    (also check for a stray `rules.db` in parent folders of the repo).
  - Note the kit slot only appears after you choose the **"Take a Kit"** path at
    level 1, and a *Dabbles in <your own class>* kit is intentionally hidden.

- **"The current OutputType is 'Library'."** You ran `src/CharM.Web.UI`. Run
  `src/CharM.Web` instead (the web host).

- **`NU1903` warnings.** Harmless NuGet advisory on the SQLite package.

## Desktop app (alternative)

`src/CharM.Maui` is the .NET MAUI desktop build; it uses the same database
search order. Build the db as above, then run the MAUI project per your
platform's MAUI tooling.
