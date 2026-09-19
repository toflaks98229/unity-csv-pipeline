# CSV Pipeline

Author your game data in a spreadsheet. Save it. The editor bakes each row into a ScriptableObject
asset, right there. It also pulls those tables from Google Sheets, and exports assets back to tables.

**All baking happens in the editor.** What ships with your build is the baked assets and one small
assembly holding the **declarative attributes** such as `[CsvAsset]`. That assembly has no executing
code and does not even reference `UnityEngine`.

> The attributes ship with your build on purpose. Your data types are **runtime types**, and a runtime
> assembly cannot see an editor assembly. If the attributes lived on the editor side, the example
> below would **compile in the editor and break only in a build.**

```csharp
[CsvAsset("Clues.csv", "ClueId", OutputFolder = "Assets/Data/Clues")]
public class ClueData : ScriptableObject
{
    public string title;
    public string body;
}
```

That is the whole setup. Save `Clues.csv` and every row becomes a `ClueData` asset, created and kept
up to date.

> A working example ships with the package. **Package Manager ▸ CSV Pipeline ▸ Samples ▸ Quick Start**
>
> The full manual is [`Documentation~/manual-en.md`](Documentation~/manual-en.md).

---

## Why

Editing balance numbers one field at a time in the Inspector means you can never see them as a table,
and nothing records how they changed. Make the table the single source and you author in a spreadsheet
and keep the history in git. This package handles the repetitive part in between — noticing the file,
parsing it, creating assets, writing fields, cleaning up rows that went away.

It already handles the things you end up rewriting in every hand-written importer.

- **Assets that are still referenced are never deleted.** When a row disappears from the table, the
  asset survives with a warning if its GUID is found in a scene or prefab. **Nothing is deleted at all
  when the scan cannot be trusted** — for instance while an unsaved scene is open.
- **The output folder is not wiped when the source table goes missing.** Moving a file for a moment
  must not take a folder of hand-authored data with it.
- **An empty cell preserves the existing value** by default. Fields a table cannot express — icons,
  prefabs — stay as you wired them in the Inspector while the table owns just the numbers.
- **A wrong column name is reported.** When a bound field has no matching column it goes in the report
  and the field keeps its value — a missing column is never silently treated as an empty cell. A
  column differing only by spaces, underscores or hyphens is offered as the likely typo. Letter case
  never matters in the first place. To make a missing column **stop the import**, declare it with
  `[CsvColumn(Required = true)]`; the identifier column always does.
- **Duplicate column names are reported.** The later column wins and whatever you wrote in the earlier
  one is gone — a loss the counts never show. Names differing only by case count as one.
- **An unterminated double quote refuses to bake.** One stray quote pulls every following row into a
  single cell, so those rows vanish from the table. They are not even counted as skipped, so the table
  is treated as unreadable and nothing is deleted.
- **Duplicate identifiers are reported.** Two rows with the same `Id` mean the second overwrites the
  first. The counts hide it — the first is "created", the second "updated" — so it is warned with a
  line number. Identifiers differing only by case count as duplicates too, or the same table would
  produce different results on Windows and macOS.
- **An identifier that cannot be a file name is rejected.** A value like `Item/Sword` is never quietly
  rewritten into something else. The place to fix it is the table.
- **An ambiguous asset reference is left alone.** When several assets share the name written in a
  cell, which one gets wired is decided by search order, and that order is not guaranteed. Picking one
  anyway is **worse than finding none**, because from the outside it looks correctly wired. Every
  candidate path is reported and the field is left as it was.
- **Values outside a field's range are not truncated.** `2147483648` into an `int`, or a number past
  `float` range, keeps the existing value and reports the problem — the same rule every other type
  follows.
- **Locale-independent parsing.** The same value comes out where the decimal separator is `,`.

---

## Install

Put the package folder at `Packages/com.toflaks.csv-pipeline` in your project. Open Unity and it
appears under **In Project** in the Package Manager.

You can also use **＋ ▸ Install package from disk…** and pick the package's `package.json`, which
writes a local path into `Packages/manifest.json`.

```json
"com.toflaks.csv-pipeline": "file:../LocalPackages/com.toflaks.csv-pipeline"
```

---

## Settings

Point the package at your table folder in **Project Settings ▸ CSV Pipeline**. With no settings asset,
`Assets/CSV` is the default. No asset is created behind your back.

| Setting | Meaning | Default |
|---|---|---|
| CSV root | Folder holding the tables to import | `Assets/CSV` |
| Sheet sync settings | Where `SheetSync_*.asset` live | *(CSV root)*`/Editor` |
| Snapshot | Last copy pulled from a sheet, for detecting local edits | `Library/CsvSheetSync` |
| Service account key | Only needed for private sheets | *(empty)* |

`.csv`, `.tsv` and `.tab` are all handled. The delimiter comes from the extension.

---

## Attaching a table — without code

Put `[CsvAsset]` on a ScriptableObject and you are done. There is no importer to write.

```csharp
[CsvAsset("Vehicles.csv", "Id", OutputFolder = "Assets/Data/Vehicles")]
public class VehicleData : ScriptableObject
{
    public float maxSpeed;          // ← MaxSpeed column (case insensitive)
    public int   trunkCapacity;     // ← TrunkCapacity column
    [SerializeField] private string ownerId;   // private works too
}
```

Field names and column names are matched **ignoring case**, so a `maxSpeed` field binds to a
`MaxSpeed` column. A field with no matching column is left untouched.

**A value that looks like an integer is still a float if the field is.** The `30` in a `MaxSpeed`
column goes in as `float`. Deciding a type from the value is where this kind of tool goes wrong most
often; here the field decides, so the two can never disagree.

Leaving `OutputFolder` empty puts the output assets in a **folder named after the type, beside the
source table**. That suits a shipped example whose install location cannot be known in advance.
Ordinarily, spell the folder out as above.

### If you use assembly definitions

When your data types live **inside an asmdef of your own**, add a `CsvPipeline` reference to it or
`[CsvAsset]` will not be visible, and compilation stops with `The type or namespace name 'CsvAsset'
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
`CsvPipeline.Editor` from the assembly holding your data types — it is editor-only, and referencing it
excludes that assembly from player builds.

**Importers written in code are different.** The `AssetPostprocessor`, `CsvRowImporter` and `SoBaker`
used under *Attaching a table — in code* all live in `CsvPipeline.Editor`. Put that code in an
**editor-only asmdef** and reference `CsvPipeline.Editor` there — never from the same assembly as your
data types.

```json
{
  "name": "MyGame.Data.Editor",
  "references": [ "CsvPipeline", "CsvPipeline.Editor" ],
  "includePlatforms": [ "Editor" ]
}
```

Scripts sitting in `Assets/` without an asmdef (Assembly-CSharp) need no change.

### When names differ, or behaviour has to change

```csharp
[CsvColumn("HP", Required = true)]      public int health;
[CsvColumn(OverwriteWhenEmpty = true)]  public string note;      // an empty cell clears it
[CsvColumn(Separators = "|")]           public List<string> tags;
[CsvColumn(ReferenceFolder = "Assets/Data/Items")] public ItemData drop;
[CsvIgnore]                             public Sprite icon;      // kept out of the table
```

| Option | What it does | Default |
|---|---|---|
| `Required` | Without this column, **nothing in the table is applied** | `false` |
| `OverwriteWhenEmpty` | An empty cell overwrites the existing value | `false` (= preserve) |
| `Separators` | Separators inside a list cell | `;` and `\|` |
| `ReferenceFolder` | Folder to resolve object references in | whole project |

`[CsvAsset]` has its own options.

| Option | What it does | Default |
|---|---|---|
| `OutputFolder` | Where output assets go. Empty means a type-named folder beside the table | *(empty)* |
| `AutoMap` | Bind fields whose names match. Off means only `[CsvColumn]` fields | `true` |
| `DeleteMissing` | Clean up assets whose row left the table | `true` |
| `ReconcileByPath` | Match cleanup by **path** instead of by name | `false` |

Turn `ReconcileByPath` on when the output folder also holds assets of the same type that this table
did not create.

### Listing what to leave out, or what to take in — `AutoMap`

The default is that **fields whose names match are bound automatically, and you list what to leave out
with `[CsvIgnore]`**. Flipping `AutoMap = false` inverts it: **you list what to take in, with
`[CsvColumn]`.**

```csharp
[CsvAsset("Cards.csv", "Name", AutoMap = false, OutputFolder = "Assets/Data/Cards")]
public class CardData : ScriptableObject
{
    [CsvColumn] public int manaCost;              // ← the table owns these
    [CsvColumn] public int attackPower;

    public Sprite artwork;                        // untagged, so the table leaves it alone
    public AssetReferenceGameObject model;        // no need for [CsvIgnore]
    public List<CardEffect> cardEffects;
}
```

**The two fail in different directions.** Under auto-mapping, forgetting `[CsvIgnore]` drags a field
into the table, and the next export adds a column, which shifts the sheet header and stops sync.
Under tagging, forgetting `[CsvColumn]` only means the field **is left out**, and anything authored by
hand stays.

**A matching column in the table is not enough.** An untagged field is preserved whether or not the
column exists. That is what makes this mode worth using on types that are mostly wiring.

The cost is that a forgotten field goes missing **silently**, so two things report it:

- Generating a table (`Create Table for This Type`) lists fields that were left out but **would become
  columns if tagged**.
- Baking warns when a tagged field has no matching column. Tagging by hand is a request for that
  column, so its absence is a mistake. Under auto-mapping a field without a column is routine, so it
  is logged as information rather than a warning — but it is always in the report either way.

> On a type where most fields belong in the table, tagging each one is more annotation, not less.
> The win is on types that carry a few numbers and a lot of wiring.

### Supported types

`string` · integer types · `float` / `double` · `bool` · **enums** (by name, case insensitive) ·
`Vector2/3/4` · `Color` (`#RRGGBB`) · **object references** (resolved by asset name) ·
and **arrays and lists** of all of the above.

#### When a reference name is ambiguous

When several assets carry the name written in a cell, **nothing is wired and it is reported.** Every
candidate path is listed, so you can see where they are.

```
[row 24 · Drop] There are 3 ItemData assets named 'Sword', so which one is meant cannot be settled.
Leaving the value as it is.
  Assets/Data/Items/Sword.asset
  Assets/Legacy/Sword.asset
  Assets/Mods/Sword.asset
Write the path itself in the cell, or narrow the range with [CsvColumn(ReferenceFolder = "…")].
```

There are two ways out.

- **Write the path in the cell.** `Assets/Data/Items/Sword.asset` instead of `Sword`. The table then
  says which one it means, and stays right when another asset of that name appears later.
- **Narrow it with `ReferenceFolder`.** Shorter, when that column always comes from one folder.

**Finding none behaves the same way** — the value is left alone, named, and reported. A typo in the
table must not wipe out a reference wired by hand.

> These go into the **bake report** in the console. When you bake from the pipeline window with
> `Bake Now`, one summary dialog is raised as well. **Automatic imports never open a dialog** — a
> table can have hundreds of rows, and the same baking code is also crossed by the preview and by the
> CI drift check. Just looking at the list must not raise a dialog.

---

## Attaching a table — in code

Some tables cannot be expressed with attributes: the meaning of a value depends on another column, the
concrete type to create varies by row, or several rows become one list on one asset. Then inherit from
one of the four bases.

| Base | Shape of the table |
|---|---|
| `CsvRowImporter<T>` | One row = one asset |
| `CsvGroupImporter<T>` | Rows sharing an identifier = one asset (rows become list items) |
| `CsvPatchImporter<T>` | Updates some fields on existing assets (creates and deletes nothing) |
| `CsvSingletonImporter<T>` | The whole table = the one asset in the project |

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

When the type to create varies by row, override `CreateOrLoad`. Returning null skips that row.

> **`CsvGroupImporter` is the one that works the other way round.** The other three have the base
> create the `SerializedObject` and apply it after the call, so **assigning to a field directly on
> that path is undone when it applies.** Write through `SoBaker`. The group importer replaces whole
> lists, so it assigns to asset fields directly.

---

## One window for all of it

**`Tools ▸ CSV Pipeline ▸ Pipeline Window`**

It has three tabs.

| Tab | What it shows |
|---|---|
| **Tables** | What baking each table right now would change. Search, filter, expand, per-table `Bake Now` |
| **Sheet Sync** | One status line per config, with pull, compare and select |
| **Settings** | The paths actually in effect, and a way to Project Settings |

**This window acts only when a person presses a button.** Leaving it open changes nothing.

The Tables tab works **entirely without the mouse.**

| Key | What it does |
|---|---|
| `↑` `↓` · `Home` `End` | Select a table |
| `→` `←` | Expand · collapse |
| `Space` | Toggle expansion |
| `Enter` | Bake the selected table |
| `Ctrl`(`⌘`)`+F` | Find |
| `Esc` | Clear the search |
| Right click | That table's menu (bake · open table · output folder · copy path) |

There are three views: **Changed only** · **Problems only** · **Everything**.

The Tables tab shows what would be created, changed and deleted before anything is applied.

```
QuestData · Quests.csv                    created 1 / updated 2 / deleted 1 / preserved 1
  ＋ Create    Quest_NightWatch      #5
  ·  Update    Quest_DeepWell        #4
       TimeLimit    900  →  1200
       Difficulty   Normal  →  Hard
  －  Delete    Quest_Removed
  ◦  Preserve  Quest_Old             still referenced elsewhere, so it is preserved
```

Tables declared with `[CsvAsset]` show **which column goes from what to what**. The comparison bakes a
copy with the real converter, so the preview and the result cannot disagree.

Importers written by hand appear in the list too, showing creates, updates, deletes and preserves per
asset. They cannot tell **whether a value actually differs**, so every existing row counts as
"updated" — the skeleton has no way to know what `Bake` is going to write. Only tables declared with
attributes can say "nothing changes".

Rows where nothing changes are left out, so what does change stands out.

### ⚠️ An import cannot be undone with Ctrl+Z

**This is deliberate.** A single import edits fields, creates assets and deletes assets together.
Unity's Undo can only take back the field edits, so accepting Ctrl+Z would leave you with **values
rolled back while created and deleted assets stayed** — an inconsistent state. That is worse than not
undoing at all, because you would move on believing it was undone.

Two things stand in its place.

- **Preview** — see what changes before it is applied. The point is to make undo unnecessary.
- **git** — the outputs are asset files, so a commit takes you back whenever. Restoring the `.asset`
  **and** its `.meta` together keeps the GUID, so the wiring comes back too. That is also why this
  package refuses to delete a referenced asset: baking again into a deleted path mints a new GUID, the
  wiring breaks, and fixing the table will not bring it back.

Baking a large table raises a progress bar you can cancel. **Cancelling applies what was read so far
and skips the cleanup of rows that left the table** — the assets of rows not yet read must not be
mistaken for rows that went away.

---

### How the reference scan works

"Assets that are still referenced are never deleted" is answered by the **dependency graph the
AssetDatabase builds at import time**. It does not care whether files are stored as text or binary,
and the **preloaded assets list** in project settings is checked as well.

What gets scanned is decided by **excluding what cannot hold a reference, not by listing what can.**
Only images, audio, video, models, fonts, scripts, plain text and shaders are excluded; **everything
else** is scanned — not just scenes, prefabs and `.asset`, but Timeline (`.playable`), presets
(`.preset`) and animators too. Growing a list of what to include means an extension missing from it
silently takes its output assets with it.

**Embedded and local packages are scanned too.** Projects that split their own code into a package
under `Packages/` have prefabs in there referencing output assets. Registry packages are read-only, so
they cannot, and they are skipped.

References among the candidates themselves do not count. If things about to disappear held each other
up, nothing would ever be cleaned.

**Nothing is deleted while the scan cannot be trusted.** With an unsaved scene open, or a prefab stage
in progress, wiring you just made by hand is not in the dependency graph yet — it lives only in editor
memory. Cleanup stops and says so instead of deleting on a stale answer.

> The old approach read files as text looking for GUID strings. In a project whose Asset Serialization
> is not `Force Text`, that returned **"no references" to every question.** Measured on a real project
> it found **0 of 300** references. The current approach finds all 300 in the same project.

---

## Reading results

Import results come out as **one log line per table**. There is no scattered logging to dig through.

```
[ClueData] Clues.csv — created 2 / updated 14 / skipped 1 / preserved 1
  [warn] row 23 · Tier — 'Huge' is not one of the values. (allowed: Small/Medium/Large)
  [warn] row 41 — 'ClueId' is empty, skipping this row.
  [warn] Gone from the table but still referenced, so it is preserved: Assets/Data/Clues/Clue_Old.asset
```

Problems raise the log to a warning or an error, and clicking one takes you to the source table or to
the asset that had the problem.

---

## Reading cells — `CsvRow`

| Method | What it does |
|---|---|
| `GetString(key)` | Trimmed string; empty string when absent |
| `GetInt / GetFloat(key, fallback)` | Locale-independent parsing; the fallback on failure |
| `TryGetInt / TryGetFloat(key, out v)` | For preserving the existing value when the column is absent |
| `GetBool(key, fallback)` | `TRUE`/`1` is true, `FALSE`/`0` is false |
| `GetList(key)` | Tokens split on `;` or `\|` |
| `Has(key)` / `HasColumn(key)` | Whether this row has the cell / the table has the column |
| `LineNumber` | Source line number, for putting a position in an error message |

## Writing fields — `SoBaker`

`Set*` writes the value as given. **`Set*If` skips an empty cell, preserving the existing value.**

`SetString(If)` · `SetInt(If)` · `SetFloat(If)` · `SetBool(If)` · `SetEnumIf` · `SetObjectRef` ·
`SetVector3If` · `SetColorIf`

## Table conventions

- **Encoding** must be UTF-8. A BOM is optional and the parser strips it. UTF-16 with a BOM is read
  too. **A table that is not UTF-8 refuses to bake.** Windows Excel's `CSV (Comma delimited)` saves in
  the system code page (CP949 on Korean Windows), not UTF-8; reading that as-is bakes mojibake into
  your assets, and the next export writes the mojibake back over the source table. It is not read
  through the system code page because then the same table would bake different values on different
  machines. In Excel choose `Save As ▸ CSV UTF-8 (Comma delimited)`.
  Files this tool **writes** get a BOM by default — without one, Excel opens them broken.
  (Project Settings ▸ CSV Pipeline ▸ `Write Utf8 Bom`)
- **Fields containing the delimiter** are wrapped in double quotes, and a quote itself is escaped as
  `""`. Newlines inside quotes are preserved. (RFC 4180)
- **List cells** are split on `;` or `|`.
- **Numbers** are locale independent. The decimal separator is `.`.
- **Vectors** split on whitespace or `;` — for example `1 0 0`.

---

## Generating a table from a type — a draft for the sheet

You do not have to type column names by hand to start a table. Pick a type and its **fields are read**
into a table with the columns already in place. Put it on your drive and start authoring.

**Right click in the Project window ▸ `CSV Pipeline ▸ Create Table for This Type`**
(or **`Tools ▸ CSV Pipeline ▸ Create Table from ScriptableObject`**)

Pick either a ScriptableObject **asset** or its **script (`.cs`)**.

- **`[CsvAsset]` does not have to be there yet.** Before the table exists, having no declaration is
  the normal state. When one is there, its file name, identifier column and column names are followed
  exactly.
- **The format follows the extension.** Save it as `.csv` for commas, `.tsv` for tabs. That is the
  same rule used when reading a table, so the two cannot drift apart.
- **Existing assets fill in rows.** Use it to move data you have been authoring in the Inspector onto
  a sheet. With no assets you get the header line alone.
- **Only columns that can be read back are generated.** Fields a table cannot author — `Rect`,
  `AnimationCurve` — are left out, and **what was left out and why is reported.** Generating them
  would have you fill the sheet in and then drown in per-row warnings on the way back. Not generating
  a column beats generating one that fails later.
- **With no declaration, a `[CsvAsset]` line to paste comes with it.** Generating the table and
  forgetting the declaration leaves a table that bakes nothing. There is a copy button on the dialog,
  and it goes to the console as well.

> When a table of the same name already exists, **the column spellings written in it are reused.**
> If `MaxSpeed` turned into `maxSpeed` on every regeneration, the header would drift from the sheet
> and sync would stop.

---

## Assets back to a table — export

Puts values you edited in the editor back into the table. **Only types declared with `[CsvAsset]`**
can do this — a hand-written importer knows the table's shape only in code, so it cannot be reversed
automatically.

**`Tools ▸ CSV Pipeline ▸ Export Assets to Tables`**

The list of files that change is shown first, and nothing is written until you confirm. Tables whose
content matches are left alone, so no git noise appears.

**Columns the table has but baking does not read — a notes column, the column of a `[CsvIgnore]`
field — are carried through with their values.** Only what this package owns is rewritten.

---

## Google Sheets sync (optional)

Instead of editing tables by hand, author in a sheet and pull. The pipeline does not change — the sync
tool overwrites the table with the sheet's content and forces a reimport, and asset creation takes the
same path it always did.

```
Google Sheets ──(the editor pulls periodically)──▶ CSV root/*.csv
                                                        │ (AssetPostprocessor)
                                                        ▼
                                              ScriptableObjects rebuilt
```

### Using a public sheet

1. Set the sheet to **Share → Anyone with the link → Viewer**.
2. **Copy the address as-is** with the target tab open.
3. Create a settings asset per table with `Tools ▸ CSV Pipeline ▸ Create Google Sheet Settings`.
4. Paste into the asset's `Sheet Url` and turn `Enabled` on.

The sheet ID and gid are not asked for separately because copying those two by hand is where mistakes
happen most. A wrong gid in particular means **the contents of the wrong tab arrive silently.**

### Using a private sheet

When the data is internal and cannot be public, use a **service account**. There is no browser login
flow, so it works in batch mode too.

1. Create a project in the Google Cloud Console and enable the **Google Drive API**.
2. Create a **service account** and download a **JSON key**.
3. Put the key file **outside `Assets`** and **out of version control** — for example `.secrets/` at
   the project root.
4. Point **Service account key** in Project Settings ▸ CSV Pipeline at that path.
5. **Share the sheet with the service account's email address.** Viewer is enough.

> The key's contents never appear in a log. Committing the key would let anyone with the repository
> read the sheet, so check that it is in `.gitignore`.

### Menus

| Menu | What it does |
|---|---|
| `Tools ▸ CSV Pipeline ▸ Pipeline Window` | Open the pipeline window |
| `Tools ▸ CSV Pipeline ▸ Rebuild All Tables` | Force a reimport of every table under the CSV root |
| `Tools ▸ CSV Pipeline ▸ Export Assets to Tables` | Regenerate tables from assets |
| `Tools ▸ CSV Pipeline ▸ Create Table from ScriptableObject` | Draft a table from the selected type's fields |
| `Tools ▸ CSV Pipeline ▸ Check for Drift` | Check that you did not edit a table and forget to bake |
| `Tools ▸ CSV Pipeline ▸ Pull from Google Sheets` | Pull enabled configs, writing and reimporting **only changed files** |
| `Tools ▸ CSV Pipeline ▸ Compare with Google Sheets` | Report differences without writing anything |
| `Tools ▸ CSV Pipeline ▸ Create Google Sheet Settings` | Create settings assets for tables that lack one |

Preview, expand-all and open-settings-folder are **inside the window**, not in the menu. Generating a
table is also on the Project window's right-click menu
(`CSV Pipeline ▸ Create Table for This Type`).

`Rebuild All Tables` exists because `AssetPostprocessor` only fires when a file **changes**. Use it to
rebake outputs after editing an importer, or to bake a new table for the first time. The
`CsvRebuildMenu.AfterRebuildAll` event fires when it finishes, so you can attach project-specific
follow-up work.

### Safeguards

- **HTML responses are rejected** — without permission to reach the sheet, Google returns a **login
  page as HTTP 200**, not an error. Writing that through would overwrite the table with HTML and
  destroy the assets, so it is detected and refused.
- **Header mismatch confirmation** — a different first line means the columns changed or the wrong tab
  is targeted, so you are asked. The automatic path cannot raise a dialog, so it skips the pull and
  warns instead.
- **Identical content is not written** — no needless reimports, no git noise.
- **Local edit detection** — compared against the last synced copy, so edits made locally are reported.

### ⚠️ Turning sync on moves ownership of the truth

For an enabled file, **the sheet is the source, the local table is a copy, and git is the history.**
Editing both sides makes the values diverge, and the moment sync runs, the table-side edit is gone.

**Adding a column in code means adding it to the sheet too.** While the headers disagree the automatic
pull skips that file, and pulling anyway drops the new column entirely — the sheet does not have it
yet.

---

## Testing importers you wrote

Baking rules can be checked **without creating a single asset**. Plug in `MemoryAssetGateway` and both
the table and the outputs stay in memory — no temp folder, no reimport.

```csharp
[Test]
public void Values_from_the_table_reach_the_asset()
{
    const string path = "Assets/Memory/Quests.csv";

    using var assets = new MemoryAssetGateway()
        .WithTable(path, "Id,Title,Reward\nQ_01,First Errand,100\n");

    using (CsvAssets.Use(assets))
    {
        CsvImportReport report = new QuestImporter().Run(path);

        Assert.AreEqual(1, report.Created);
        Assert.AreEqual("First Errand", assets.Get<QuestData>("Assets/Memory/QuestData/Q_01.asset").title);
    }
}
```

A few things that help:

| What | For |
|---|---|
| `WithTable(path, text)` | Places the table text. The folder comes with it |
| `WithAsset(path, asset)` · `Add<T>(path)` | Places an output asset that already exists |
| `Get<T>(path)` | Reads what was baked |
| `Referenced` | Paths put here count as still referenced, so cleanup preserves them |
| `SaveCount` · `DirtyCount` · `BatchCount` | How often saving, dirtying and batching happened |

For the tests to appear in the list, the consuming project's `Packages/manifest.json` needs
`testables`.

```json
"testables": [ "com.toflaks.csv-pipeline" ]
```

---

## Editing a table and forgetting to bake — checking in CI

A changed table file shows up in the diff. **An unchanged output asset does not.** Absence is not
noticeable, so a commit that edits the table and forgets to bake slips through quietly.

```sh
Unity -batchmode -projectPath . -executeMethod CsvPipeline.CsvDriftCheck.Run
```

A table that has drifted gives **exit code 1** and logs which table drifted and why. **Nothing is
written.** The judgement is the same one the pipeline window makes, so a table showing "nothing
changes" on screen will not fail in CI.

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
because the Asset Store EULA governs that. See [`LICENSE.md`](LICENSE.md).

---

## Disclosures

**Network.** Google Sheets sync is optional, and requests happen only once you turn it on. There are
**exactly two** hosts.

| Host | When |
|---|---|
| `docs.google.com` | Fetching a sheet's contents |
| `oauth2.googleapis.com` | Getting an access token, when a service account is configured |

**Before you create a sync settings asset and enable it, nothing is sent anywhere.** Automatic pulling
is off by default (`autoPull = false`). There is no telemetry and no analytics.
The window's `?` button opens the documentation that ships inside the package — only when clicked.

**Credential handling.** For a private sheet, the Google service account JSON key is read from **a path
you specify**. The key is **never copied into your project, never included in a build, and never
written to a log.** What the settings asset holds is the file path. Keeping the key file outside
`Assets` and out of version control is yours to do, and how is written under
[Using a private sheet](#using-a-private-sheet) above.

**External dependencies.** None. The package declares no dependencies and contains no third-party code.
