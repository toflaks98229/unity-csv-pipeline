# CSV Pipeline — Manual

Author your game data in a spreadsheet. Save it. The editor bakes each row into a ScriptableObject
asset, right there, with no importer code.

```csharp
[CsvAsset("Clues.csv", "ClueId", OutputFolder = "Assets/Data/Clues")]
public class ClueData : ScriptableObject
{
    public string title;
    public string body;
}
```

That is the whole setup. Save `Clues.csv` and one `ClueData` asset appears per row, and is kept up to
date from then on.

> A working example ships with the package:
> **Package Manager ▸ CSV Pipeline ▸ Samples ▸ Quick Start**

**Note on language.** This manual is in English. The **editor UI and log messages are currently in
Korean**, and menu paths below are given as they actually appear, with an English gloss.

---

## Contents

1. [Why this exists](#why-this-exists)
2. [What ships in a player build](#what-ships-in-a-player-build)
3. [Install](#install)
4. [Settings](#settings)
5. [Declaring a table — no code](#declaring-a-table--no-code)
6. [Declaring a table — with code](#declaring-a-table--with-code)
7. [The pipeline window](#the-pipeline-window)
8. [Reading the result](#reading-the-result)
9. [Reading cells — `CsvRow`](#reading-cells--csvrow)
10. [Writing fields — `SoBaker`](#writing-fields--sobaker)
11. [Authoring conventions](#authoring-conventions)
12. [Exporting assets back to CSV](#exporting-assets-back-to-csv)
13. [Google Sheets sync](#google-sheets-sync)
14. [Catching "edited the sheet, forgot to bake"](#catching-edited-the-sheet-forgot-to-bake)
15. [Testing your own importers](#testing-your-own-importers)
16. [Disclosures](#disclosures)
17. [License](#license)

---

## Why this exists

Editing balance numbers one inspector field at a time means you can never see the table as a table,
and the history of those edits is lost. Making the spreadsheet the single source of truth gives you
both. This package handles the repetitive part in between — watching the file, parsing it, creating
assets, writing fields, and cleaning up rows that went away.

What it does that a hand-rolled importer usually does not:

- **Assets that are still referenced are never deleted.** When a row disappears, the pipeline asks
  the AssetDatabase whether anything still uses that asset. If a scene or prefab does, it keeps the
  asset and logs a warning instead. Deleting it would destroy the GUID, and restoring the file from
  git would not restore the wiring.
- **A missing source table never deletes the output folder.** Temporarily moving a CSV should not
  wipe hand-authored data.
- **Empty cells preserve existing values by default.** Fields a spreadsheet cannot express — icons,
  prefabs — stay as you authored them in the inspector while the table owns only the numbers.
- **A wrong column name stops the import.** A missing column is never silently treated as an empty
  cell. If a column differs only by letter case, that is reported as the likely typo it is.
- **Duplicate identifiers are reported.** Two rows with the same id point at the same asset, so the
  later row overwrites the earlier one. That loss does not show up in the counts — the first row
  reads as "created" and the second as "updated" — so it is called out explicitly, with line numbers.
  Ids differing only by letter case count as duplicates too: they are the same file on Windows and
  different files on macOS and Linux.
- **Identifiers that cannot be file names are rejected, not sanitized.** `Item/Sword` is refused with
  a reason rather than quietly rewritten to `Item_Sword`, which would split the name in the table from
  the name on disk.
- **Parsing is locale independent.** A machine whose decimal separator is `,` produces the same values.

---

## What ships in a player build

Baking happens entirely in the editor. What ends up in a build is the baked assets plus **one small
assembly containing nothing but the declaration attributes** (`CsvPipeline`). It has no executable
code and does not even reference `UnityEngine`.

The attributes have to ship because your data types are runtime types, and a runtime assembly cannot
reference an editor-only one. If the attributes lived on the editor side, the example at the top of
this page would compile in the editor and then fail the player build.

Everything else — parsing, baking, the window, sheet sync — lives in `CsvPipeline.Editor` and never
enters a build.

---

## Install

Add the package to `Packages/manifest.json`:

```json
"com.toflaks.csv-pipeline": "https://github.com/toflaks98229/unity-csv-pipeline.git"
```

Append a tag such as `#v0.13.0` to pin a version. To vendor it instead, place it at
`Packages/com.toflaks.csv-pipeline`.

Minimum Unity version: **2022.3**.

`git` must be on the PATH that **Unity** sees, not only inside a shell. Verify before opening Unity;
exit code 0 means you are fine:

```sh
GIT_TERMINAL_PROMPT=0 git ls-remote https://github.com/toflaks98229/unity-csv-pipeline.git
```

This does not apply when the package is vendored — that path uses the working tree as-is.

---

## Settings

Point the pipeline at your table folder in **Project Settings ▸ CSV Pipeline**. With no settings asset
present, `Assets/CSV` is used as the default. **No asset is created behind your back.**

| Setting | Meaning | Default |
|---|---|---|
| CSV root | Folder holding the tables to import | `Assets/CSV` |
| Sheet settings folder | Where `SheetSync_*.asset` files live | *(CSV root)*`/Editor` |
| Snapshot | Last downloaded copy of each sheet, used to detect local edits | `Library/CsvSheetSync` |
| Service account key | Only needed for private sheets | *(empty)* |

Both `.csv` and `.tsv` / `.tab` are handled. The delimiter comes from the extension.

---

## Declaring a table — no code

Put `[CsvAsset]` on a ScriptableObject. There is no importer to write.

```csharp
[CsvAsset("Vehicles.csv", "Id", OutputFolder = "Assets/Data/Vehicles")]
public class VehicleData : ScriptableObject
{
    public float maxSpeed;                      // ← MaxSpeed column (case insensitive)
    public int   trunkCapacity;                 // ← TrunkCapacity column
    [SerializeField] private string ownerId;    // private serialized fields work too
}
```

Field names are matched to column names **ignoring case**, so `maxSpeed` binds to `MaxSpeed`.
A field with no matching column is left alone.

**The field decides the type, not the value.** `30` in the `MaxSpeed` column becomes a `float` because
the field is a `float`. Tools that infer types from values get this wrong constantly; here there is
nothing to infer.

Leaving `OutputFolder` empty puts the output in a folder named after the type, next to the source
table. That is for distributable samples whose install path is not known in advance. Normally, name
the folder as above.

### When names differ, or behaviour needs to change

```csharp
[CsvColumn("HP", Required = true)]                 public int health;
[CsvColumn(OverwriteWhenEmpty = true)]             public string note;   // empty cell clears it
[CsvColumn(Separators = "|")]                      public List<string> tags;
[CsvColumn(ReferenceFolder = "Assets/Data/Items")] public ItemData drop;
[CsvIgnore]                                        public Sprite icon;   // never bound
```

| `[CsvColumn]` option | Effect | Default |
|---|---|---|
| `Required` | If the column is absent, **nothing from this table is applied** | `false` |
| `OverwriteWhenEmpty` | An empty cell overwrites the existing value | `false` (preserve) |
| `Separators` | Delimiters inside a list cell | `;` and `\|` |
| `ReferenceFolder` | Folder to resolve object references in | whole project |

| `[CsvAsset]` option | Effect | Default |
|---|---|---|
| `OutputFolder` | Where baked assets go; empty means beside the table | *(empty)* |
| `AutoMap` | Bind matching field names automatically | `true` |
| `DeleteMissing` | Clean up assets for rows that disappeared | `true` |
| `ReconcileByPath` | Match by **asset path** instead of asset name during cleanup | `false` |

Turn `ReconcileByPath` on when the output folder also holds assets of the same type that this table
did not create.

### Supported types

`string` · integer types · `float` / `double` · `bool` · **enums** (by name, case insensitive) ·
`Vector2/3/4` · `Color` (`#RRGGBB`) · **object references** (resolved by asset name) ·
and **arrays and lists** of all of the above.

---

## Declaring a table — with code

Some tables cannot be expressed by attributes: a value whose meaning depends on another column, a row
whose concrete type varies, several rows collapsing into one asset's list. Derive from one of four bases.

| Base | Shape of the table |
|---|---|
| `CsvRowImporter<T>` | one row = one asset |
| `CsvGroupImporter<T>` | several rows sharing an id = one asset (rows become list entries) |
| `CsvPatchImporter<T>` | update some fields of existing assets (never creates or deletes) |
| `CsvSingletonImporter<T>` | the whole table = a single project-wide asset |

```csharp
public sealed class ClueImporter : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        => CsvImport.Run<Definition>(imported, deleted, moved);

    private sealed class Definition : CsvRowImporter<ClueData>
    {
        protected override string FileName     => "Clues.csv";
        protected override string OutputFolder => "Assets/Data/Clues";

        protected override string GetId(CsvRow row) => row.GetString("ClueId");

        protected override void Bake(CsvRow row, ClueData asset, SerializedObject serialized)
        {
            SoBaker.SetStringIf(serialized, "title", row.GetString("Title"));
            SoBaker.SetStringIf(serialized, "body", row.GetString("Body"));
        }
    }
}
```

Override `CreateOrLoad` when the concrete type varies per row. Returning `null` skips that row.

> **`CsvGroupImporter` is the one exception to the write rule.** The other three bases hand you a
> `SerializedObject` and apply it after the call, so **assigning to fields directly is reverted at
> apply time** — use `SoBaker`. The group importer replaces whole lists, so it assigns to asset fields
> directly.

---

## The pipeline window

**`Tools ▸ CSV Pipeline ▸ CSV 파이프라인`** (CSV Pipeline)

Three tabs:

| Tab | Shows |
|---|---|
| **표** (Tables) | What baking each table right now would change; search, filter, per-table bake |
| **시트 연동** (Sheet sync) | One status line per sheet config, with fetch / compare / select |
| **설정** (Settings) | The paths actually in effect, and a way to Project Settings |

**The window only acts when you press something.** Leaving it open changes nothing.

The Tables tab shows, before you apply anything, what would be created, changed, deleted, or kept:

```
QuestData · Quests.csv                       create 1 / update 2 / delete 1 / keep 1
  ＋ create  Quest_NightWatch      row 5
  ·  update  Quest_DeepWell        row 4
       TimeLimit    900  →  1200
       Difficulty   Normal  →  Hard
  －  delete  Quest_Removed
  ◦  keep    Quest_Old             still referenced elsewhere, so not deleted
```

For `[CsvAsset]` tables it goes down to **which column changes from what to what**. The preview bakes
a copy with the real converter and then compares, so what you see and what happens cannot diverge.
Hand-written importers appear too, at asset granularity.

Rows where nothing would change are not listed. That is what makes the real changes visible.

### Keyboard

The Tables tab is fully usable without a mouse.

| Key | Action |
|---|---|
| `↑` `↓` · `Home` `End` | Move the selection |
| `→` `←` | Expand · collapse |
| `Space` | Toggle expansion |
| `Enter` | Bake the selected table |
| `Ctrl`(`⌘`)`+F` | Focus search |
| `Esc` | Clear search |
| Right-click | Per-table menu (bake · open table · open output folder · copy path) |

Views: **바뀌는 것만** (changed only) · **손볼 것만** (problems only) · **전부** (everything).

### Imports cannot be undone with Ctrl+Z

**This is deliberate.** A single import edits fields, creates assets, and deletes assets together.
Unity's Undo can only revert the field edits, so accepting Ctrl+Z would leave you with reverted values
next to assets that were still created and still deleted. That is worse than no undo, because it looks
like it worked.

Two things stand in its place:

- **The preview** — see what changes before applying. The point is to make undo unnecessary.
- **git** — the outputs are asset files. Commit them and you can always go back. This is also why the
  pipeline refuses to delete referenced assets: once a GUID is gone, git cannot bring the wiring back.

Baking a large table shows a progress bar you can cancel. **Cancelling applies only what was read so
far and skips cleanup entirely** — rows not yet read must never be mistaken for rows that disappeared.

### How the reference check works

"Referenced assets are never deleted" asks the **dependency graph the AssetDatabase builds at import
time**. It is independent of whether assets are serialized as text or binary, and it also covers the
project's **preloaded assets** list.

References among the assets being removed together do not count. If they propped each other up,
nothing would ever be cleaned up.

---

## Reading the result

Each table reports **one log line**, so there is nothing to hunt for.

```
[ClueData] Clues.csv — create 2 / update 14 / skip 1 / keep 1
  [warning] row 23 · Tier — 'Huge' is not a valid value. (Small/Medium/Large)
  [warning] row 41 — 'ClueId' is empty, skipping.
  [warning] Gone from the table but still referenced, so kept: Assets/Data/Clues/Clue_Old.asset
```

Problems escalate the log to warning or error, and clicking it jumps to the source table or the
offending asset.

---

## Reading cells — `CsvRow`

| Method | Effect |
|---|---|
| `GetString(key)` | Trimmed string; empty string when absent |
| `GetInt` / `GetFloat(key, fallback)` | Locale-independent parse; `fallback` on failure |
| `TryGetInt` / `TryGetFloat(key, out v)` | For preserving the existing value when the column is absent |
| `GetBool(key, fallback)` | `TRUE`/`1` true, `FALSE`/`0` false |
| `GetList(key)` | Tokens split on `;` or `\|` |
| `Has(key)` / `HasColumn(key)` | Cell present in this row / column present in the table |
| `LineNumber` | Source line, for attaching a location to messages |

## Writing fields — `SoBaker`

`Set*` writes the value as given. **`Set*If` skips empty cells, preserving what is already there.**

`SetString(If)` · `SetInt(If)` · `SetFloat(If)` · `SetBool(If)` · `SetEnumIf` · `SetObjectRef` ·
`SetVector3If` · `SetColorIf`

---

## Authoring conventions

- **Encoding** UTF-8. A BOM is stripped by the parser.
- **Fields containing the delimiter** are wrapped in double quotes; a literal quote is escaped as `""`.
  Newlines inside quotes are preserved. (RFC 4180)
- **List cells** split on `;` or `|`.
- **Numbers** are locale independent. The decimal separator is `.`.
- **Vectors** split on whitespace or `;` — for example `1 0 0`.

---

## Exporting assets back to CSV

Push values you tuned in the editor back into the table. **Only types declared with `[CsvAsset]`**
can round-trip — a hand-written importer knows the table's shape only in code, so it cannot be reversed
automatically.

**`Tools ▸ CSV Pipeline ▸ 에셋을 표로 내보내기`** (Export assets to tables)

The list of files that would change is shown first, and nothing is written until you confirm. Tables
whose content is unchanged are left untouched, so no git noise.

---

## Google Sheets sync

Author in a sheet instead of editing files by hand. The pipeline is unchanged — sync overwrites the
local table and forces a reimport, and baking proceeds down the usual path.

```
Google Sheets ──(editor pulls periodically)──▶ CSV root/*.csv
                                                    │ (AssetPostprocessor)
                                                    ▼
                                            ScriptableObjects rebuilt
```

### Public sheets

1. Share the sheet as **Anyone with the link ▸ Viewer**.
2. Open the target tab and **copy the address exactly as it appears**.
3. Create a settings asset per table with
   `Tools ▸ CSV Pipeline ▸ Google Sheet 설정 만들기` (Create sheet settings).
4. Paste into `Sheet Url` and enable `Enabled`.

The sheet id and gid are deliberately not asked for separately. Transcribing those two by hand is
where mistakes happen — and a wrong gid **silently imports the contents of the wrong tab**.

### Private sheets

Use a **service account**. There is no browser login flow, so it works in batch mode.

1. Create a Google Cloud project and enable the **Google Drive API**.
2. Create a **service account** and download its **JSON key**.
3. Put the key file **outside `Assets`** and **exclude it from version control** (for example a
   `.secrets/` folder at the project root).
4. Enter that path under **Project Settings ▸ CSV Pipeline ▸ service account key**.
5. Share the sheet with the service account's **email address**. Viewer access is enough.

> The key file's contents are never written to a log. Committing the key, however, would let anyone
> with the repository read your sheets — check that it is in `.gitignore`.

### Menu items

| Menu | Effect |
|---|---|
| `Tools ▸ CSV Pipeline ▸ CSV 파이프라인` | Open the pipeline window |
| `Tools ▸ CSV Pipeline ▸ 전체 다시 굽기` | Force a reimport of every table under the CSV root |
| `Tools ▸ CSV Pipeline ▸ 에셋을 표로 내보내기` | Regenerate tables from assets |
| `Tools ▸ CSV Pipeline ▸ 표와 산출물이 어긋나는지 확인` | Check whether tables and outputs are in sync |
| `Tools ▸ CSV Pipeline ▸ Google Sheet에서 받기` | Pull enabled configs, writing and reimporting **only changed files** |
| `Tools ▸ CSV Pipeline ▸ Google Sheet와 비교만` | Report differences without writing anything |
| `Tools ▸ CSV Pipeline ▸ Google Sheet 설정 만들기` | Create settings assets for tables that lack one |

Preview, expand/collapse, and the sheet settings folder live in the window rather than the menu.

"전체 다시 굽기" (rebuild all) exists because `AssetPostprocessor` only fires when a file **changes**.
Use it after editing an importer, or to bake a new table for the first time. The
`CsvRebuildMenu.AfterRebuildAll` event fires when it finishes, so project-specific follow-up work can
hook in.

### Safeguards

- **HTML responses are rejected.** Without access, Google returns a **login page as HTTP 200**, not an
  error. Writing that to disk would replace your table with HTML and destroy every asset it bakes.
- **Header mismatch confirmation.** A different first line means the columns changed or the wrong tab
  is targeted, so confirmation is requested. (The automatic pull path cannot show a dialog, so it skips
  the file and warns instead.)
- **Identical content is not written**, avoiding pointless reimports and git noise.
- **Local edits are detected** by comparing against the last synced snapshot.

### Enabling sync moves ownership of the truth

For an enabled file, **the sheet is the source, the local table is a copy, and git is the history.**
Editing both sides makes them diverge, and the next sync discards the local edit.

**If you add a column in code, add it to the sheet too.** While the headers disagree the automatic
pull skips that file; pulling anyway would drop the new column entirely, because the sheet does not
have it yet.

---

## Catching "edited the sheet, forgot to bake"

A changed table file shows up in a diff. **An output that did not change does not** — absence is
invisible. So a commit that edits data without baking it slips through silently.

```sh
Unity -batchmode -projectPath . -executeMethod CsvPipeline.CsvDriftCheck.Run
```

Exit code **1** if anything is out of sync, along with a log saying which table and why. **It writes
nothing.** The verdict is the same one the pipeline window uses, so a table that reads as "no changes"
on screen will not fail in CI.

In the editor: `Tools ▸ CSV Pipeline ▸ 표와 산출물이 어긋나는지 확인`.

---

## Testing your own importers

Baking rules can be verified **without creating a single asset**. Install `MemoryAssetGateway` and both
the tables and the outputs live only in memory — no temp folders, no reimports.

```csharp
[Test]
public void Values_from_the_table_reach_the_asset()
{
    const string path = "Assets/Memory/Quests.csv";

    using var assets = new MemoryAssetGateway()
        .WithTable(path, "Id,Title,Reward\nQ_01,First job,100\n");

    using (CsvAssets.Use(assets))
    {
        CsvImportReport report = new QuestImporter().Run(path);

        Assert.AreEqual(1, report.Created);
        Assert.AreEqual("First job", assets.Get<QuestData>("Assets/Memory/QuestData/Q_01.asset").title);
    }
}
```

Helpers:

| Member | Use |
|---|---|
| `WithTable(path, text)` | Place a table's text; the folder is created too |
| `WithAsset(path, asset)` · `Add<T>(path)` | Place an existing output |
| `Get<T>(path)` | Read what was baked |
| `Referenced` | Paths added here count as "still referenced" and survive cleanup |
| `WithReferenceScanBlocked(reason)` | Simulate a gateway that cannot answer, so cleanup must stop |
| `SaveCount` | How many saves happened |
| `FindPathsCount` | How many project-wide searches happened |

For tests to appear in the Test Runner, the consuming project's `Packages/manifest.json` needs
`testables`:

```json
"testables": [ "com.toflaks.csv-pipeline" ]
```

---

## Disclosures

**Network access.** The optional Google Sheets integration is the only thing that makes outbound
requests, and only once you enable it. It contacts exactly **two hosts**:

| Host | When |
|---|---|
| `docs.google.com` | Downloading sheet contents |
| `oauth2.googleapis.com` | Obtaining an access token, only when a service account is configured |

**Nothing is sent anywhere unless you create a sheet settings asset and enable it.** Automatic pulling
is off by default (`autoPull = false`). No telemetry or analytics of any kind is collected.
(The window's `?` button opens this repository's README in a browser — only when clicked.)

**Credential storage.** For private sheets, the package reads a Google service account JSON key from a
**path you configure**. The key is **never copied into the project, never embedded in a build, and
never written to a log** — only the file path is stored, in the project settings asset. Keeping the key
file outside `Assets` and out of version control is your responsibility, and the manual says so above.

**Third-party dependencies.** None. The package has no package dependencies and bundles no third-party
code.

**AI assistance.** This package was developed with the assistance of AI tooling. All code is
human-reviewed, plain and unobfuscated, and covered by the automated test suite that ships with it.

---

## License

MIT. See `LICENSE.md`.
