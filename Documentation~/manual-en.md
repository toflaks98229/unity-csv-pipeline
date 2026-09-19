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
12. [Generating a table from a type](#generating-a-table-from-a-type)
13. [Exporting assets back to CSV](#exporting-assets-back-to-csv)
14. [Google Sheets sync](#google-sheets-sync)
15. [Catching "edited the sheet, forgot to bake"](#catching-edited-the-sheet-forgot-to-bake)
16. [Testing your own importers](#testing-your-own-importers)
17. [Disclosures](#disclosures)
18. [License](#license)

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
- **A wrong column name is reported.** When a bound field has no matching column, the report says
   so — a missing column is never silently treated as an empty cell, and the field keeps its value.
   If a column differs only by spaces, underscores or hyphens, that name is offered as the likely
   typo. Letter case never matters in the first place. To make a column **stop the import** when it
   is absent, declare it with `[CsvColumn(Required = true)]`; the identifier column always does.
- **Duplicate column names are reported.** The later column wins and whatever you wrote in the
  earlier one is gone — a loss the counts never show. Names differing only by case count as one.
- **An unterminated double quote refuses to bake.** One stray quote pulls every following row into
  a single cell, so those rows vanish from the table. They are not even counted as skipped, so the
  table is treated as unreadable and nothing is deleted.
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

Place the package folder at `Packages/com.toflaks.csv-pipeline` in your project. Unity picks it up
on the next focus and lists it under **In Project** in the Package Manager.

You can also use **Package Manager ▸ ＋ ▸ Install package from disk…** and select the package's
`package.json`. That writes a local path into `Packages/manifest.json`:

```json
"com.toflaks.csv-pipeline": "file:../LocalPackages/com.toflaks.csv-pipeline"
```

Minimum Unity version: **2022.3**.

> **Installing from a repository URL is no longer documented.** The distribution repository is
> private, so only seat holders can reach it. To serve it over git inside your organisation, use a
> **private** repository or registry, which [the license](../LICENSE.md) §2 permits.

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

### If you use assembly definitions (asmdef)

When your data types live in **your own asmdef**, that asmdef must reference `CsvPipeline` for
`[CsvAsset]` to resolve. Without it the compiler stops with `The type or namespace name 'CsvAsset'
could not be found`.

**Inspector** — select the `.asmdef` holding your data types, add `CsvPipeline` under **Assembly
Definition References**, then **Apply**.

**By hand** — add one entry to the `.asmdef`:

```json
{
  "name": "MyGame.Data",
  "references": [ "CsvPipeline" ]
}
```

For **data types**, reference `CsvPipeline` (the runtime assembly) only. It contains attribute
declarations, no executing code, and does not even reference `UnityEngine`. Do **not** reference
`CsvPipeline.Editor` from the assembly holding your data types — it is editor-only, and referencing
it excludes that assembly from player builds.

**Importers written in code are different.** `AssetPostprocessor`, `CsvRowImporter` and `SoBaker`
(see *Attaching a table — in code*) all live in `CsvPipeline.Editor`. Put that code in an
**editor-only asmdef** and reference `CsvPipeline.Editor` there — never from the same assembly as
your data types.

```json
{
  "name": "MyGame.Data.Editor",
  "references": [ "CsvPipeline", "CsvPipeline.Editor" ],
  "includePlatforms": [ "Editor" ]
}
```

Scripts that sit in `Assets/` without an asmdef (Assembly-CSharp) need no change.

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
| `AutoMap` | Bind matching field names automatically; off means only `[CsvColumn]` fields | `true` |
| `DeleteMissing` | Clean up assets for rows that disappeared | `true` |
| `ReconcileByPath` | Match by **asset path** instead of asset name during cleanup | `false` |

Turn `ReconcileByPath` on when the output folder also holds assets of the same type that this table
did not create.

### Listing what to exclude, or what to include — `AutoMap`

By default every matching field binds automatically and you exclude fields one at a time with
`[CsvIgnore]`. `AutoMap = false` inverts that: **only fields carrying `[CsvColumn]` are bound.**

```csharp
[CsvAsset("Cards.csv", "Name", AutoMap = false, OutputFolder = "Assets/Data/Cards")]
public class CardData : ScriptableObject
{
    [CsvColumn] public int manaCost;              // the table owns this
    [CsvColumn] public int attackPower;

    public Sprite artwork;                        // untagged, so the table never touches it
    public AssetReferenceGameObject model;        // no [CsvIgnore] needed
    public List<CardEffect> cardEffects;
}
```

**The two modes fail in opposite directions.** Forgetting `[CsvIgnore]` under auto-mapping pulls the
field into the table, and the next export adds a column — which shifts the sheet header and stops sync.
Forgetting `[CsvColumn]` under tagging only leaves the field **out**; hand-authored values survive.

**A matching column in the table is not enough.** An untagged field is preserved whether or not the
column exists. That is what makes this mode worth using on types that are mostly wiring.

The cost is that a forgotten field goes missing **silently**, so two things report it:

- Generating a table (`Create Table for This Type`) lists fields that were left out but **would become columns
  if tagged**.
- Baking warns when a tagged field has no matching column. Tagging by hand is a request for that
  column, so its absence is a mistake. Under auto-mapping a field without a column is routine, so it
  is logged as information rather than a warning — but it is always in the report either way.

> On a type where most fields belong in the table, tagging each one is more annotation, not less.
> The win is on types that carry a few numbers and a lot of wiring.

### Supported types

`string` · integer types · `float` / `double` · `bool` · **enums** (by name, case insensitive) ·
`Vector2/3/4` · `Color` (`#RRGGBB`) · **object references** (resolved by asset name) ·
and **arrays and lists** of all of the above.

#### When the referenced name is ambiguous

If several assets carry the name written in the cell, **nothing is wired** and every candidate path
is reported, so you can see where they are.

```
[row 24 · Drop] There are 3 ItemData assets named 'Sword', so which one is meant cannot be settled.
Leaving the value as it is.
  Assets/Data/Items/Sword.asset
  Assets/Legacy/Sword.asset
  Assets/Mods/Sword.asset
Write the path itself in the cell, or narrow the range with [CsvColumn(ReferenceFolder = "…")].
```

Picking one would be worse than failing: search order decides the winner, that order is not
guaranteed, and **on your side the field looks correctly wired**. There are two ways to resolve it.

- **Write the path in the cell** — `Assets/Data/Items/Sword.asset` instead of `Sword`. The table
  states which asset it means, so a later duplicate cannot change the answer.
- **Narrow the search with `ReferenceFolder`** — shorter when that column always draws from one folder.

**A name that matches nothing behaves the same way**: the field is left as it is and the name is
reported. A typo in the table must not wipe out a reference wired by hand.

> These land in the bake report, which goes to the console. Baking a table yourself from the pipeline
> window (`Bake Now`) also raises one summary dialog. **Automatic imports never open a dialog** —
> a table can hold hundreds of rows, and the same bake code is what the preview and the CI drift check
> run, so merely looking at the table list must not pop up a window.

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

**`Tools ▸ CSV Pipeline ▸ Pipeline Window`**

Three tabs:

| Tab | Shows |
|---|---|
| **Tables** | What baking each table right now would change; search, filter, per-table bake |
| **Sheet Sync** | One status line per sheet config, with fetch / compare / select |
| **Settings** | The paths actually in effect, and a way to Project Settings |

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

Views: **Changed only** · **Problems only** · **Everything**.

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

What gets scanned is decided by **excluding what provably cannot hold a reference**, not by listing
what can. Images, audio, video, models, fonts, scripts, plain text, and shaders are skipped;
**everything else is scanned** — scenes, prefabs, and `.asset` files, but equally Timeline
(`.playable`), presets (`.preset`), and animator assets. A grow-the-list approach silently deletes
whatever is held by an extension nobody remembered to add.

**Embedded and local packages are scanned too.** Projects that split their own code into
`Packages/` have prefabs in there referencing baked outputs. Registry packages are read-only and
cannot, so they are skipped.

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

- **Encoding** must be UTF-8. A BOM is optional and is stripped by the parser. BOM-marked UTF-16
  is read as well. **A table that is not UTF-8 is refused, not baked.** Windows Excel's
  `CSV (Comma delimited)` save uses the system codepage, not UTF-8 — read as-is, non-ASCII text
  would be baked into your assets as mojibake, and the next export would write that corruption back
  over the source table. The tool does not fall back to the system codepage on purpose: that would
  make one table produce different values on different people's machines. In Excel, choose
  `Save As ▸ CSV UTF-8 (Comma delimited)`.
  Files this tool **writes** carry a BOM by default, because without one Excel opens them corrupted
  (Project Settings ▸ CSV Pipeline ▸ `Write Utf8 Bom`).
- **Fields containing the delimiter** are wrapped in double quotes; a literal quote is escaped as `""`.
  Newlines inside quotes are preserved. (RFC 4180)
- **List cells** split on `;` or `|`.
- **Numbers** are locale independent. The decimal separator is `.`.
- **Vectors** split on whitespace or `;` — for example `1 0 0`.

---

## Generating a table from a type

You do not have to type the column names by hand when starting a table. Pick a type and the package
reads its fields and writes out a table with the columns already in place — upload it to Drive and
start authoring.

**Project window ▸ right-click ▸ `CSV Pipeline ▸ Create Table for This Type`**
or **`Tools ▸ CSV Pipeline ▸ Create Table from ScriptableObject`**

Pick either a ScriptableObject **asset** or its **script** (`.cs`).

- **`[CsvAsset]` is not required yet.** You are making the table precisely because there isn't one.
  When the attribute *is* present, its file name, id column and column-name overrides are followed
  exactly — a value invented here that diverged from the real bake would mean the table you look at
  and the table that bakes are different tables.
- **The extension picks the format.** Save as `.csv` for commas, `.tsv` for tabs. That is the same rule
  the pipeline uses when reading a table, so there is only one place where format is decided.
- **Existing assets become rows.** If assets of that type already exist they are written out in path
  order, which is how you move data authored in the inspector into a sheet. With none, you get the
  header line only.
- **Only columns that can be read back are written.** Fields `CsvValueBinder` cannot author — `Rect`,
  `AnimationCurve`, nested structs — are left out, **and you are told which and why**. Emitting them
  would invite you to fill those columns in the sheet and get a warning per row on the way back.
- **Without a declaration you get the `[CsvAsset]` line to paste.** A table with no declaration bakes
  nothing. The dialog offers a copy button and the line is also written to the console.

> If a table of the same name already exists, its **existing column spelling is kept**. Otherwise
> regenerating would turn `MaxSpeed` into `maxSpeed`, and sync would stop to ask about the header change.

---

## Exporting assets back to CSV

Push values you tuned in the editor back into the table. **Only types declared with `[CsvAsset]`**
can round-trip — a hand-written importer knows the table's shape only in code, so it cannot be reversed
automatically.

**`Tools ▸ CSV Pipeline ▸ Export Assets to Tables`**

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
   `Tools ▸ CSV Pipeline ▸ Create Google Sheet Settings`.
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
| `Tools ▸ CSV Pipeline ▸ Pipeline Window` | Open the pipeline window |
| `Tools ▸ CSV Pipeline ▸ Rebuild All Tables` | Force a reimport of every table under the CSV root |
| `Tools ▸ CSV Pipeline ▸ Export Assets to Tables` | Regenerate tables from assets |
| `Tools ▸ CSV Pipeline ▸ Create Table from ScriptableObject` | Generate a table from the selected type's fields |
| `Tools ▸ CSV Pipeline ▸ Check for Drift` | Check whether tables and outputs are in sync |
| `Tools ▸ CSV Pipeline ▸ Pull from Google Sheets` | Pull enabled configs, writing and reimporting **only changed files** |
| `Tools ▸ CSV Pipeline ▸ Compare with Google Sheets` | Report differences without writing anything |
| `Tools ▸ CSV Pipeline ▸ Create Google Sheet Settings` | Create settings assets for tables that lack one |

Preview, expand/collapse, and the sheet settings folder live in the window rather than the menu.

"Rebuild All Tables" exists because `AssetPostprocessor` only fires when a file **changes**.
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

In the editor: `Tools ▸ CSV Pipeline ▸ Check for Drift`.

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

---

## Support

Questions, bug reports and requests go in **the reviews section of this package's Unity Asset Store
product page**. They are answered there, in public, so the answer stays where the next person with the
same question will find it.

Two things make a report answerable in one round instead of three:

- **The console log line for the table involved.** Every import prints one line per table, with the
  warnings underneath it. That line names the table, the counts, and the rows that had problems.
- **Your Unity version and how the package was installed** (Package Manager, or a local folder).

If a table is losing data, keep the table file as it was when it went wrong. The parser is strict about
encoding, quoting and duplicate columns, and the original file usually says which of the three it was.

Licence questions — seats, refunds, invoices — are handled by Unity rather than by the publisher,
because the Asset Store EULA governs that. See `LICENSE.md`.

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

**Commercial, per-seat.** One license covers one developer, across any number of projects. Baked
assets and the `Runtime/` attribute assembly may be shipped inside the products you build and sell,
with no royalty or attribution. Redistributing, reselling, or publishing the **source** is what the
license prohibits.

See [`LICENSE.md`](../LICENSE.md) for the full terms. Versions 0.13.2 and earlier were released
under the MIT License and remain MIT for anyone who lawfully obtained them.
