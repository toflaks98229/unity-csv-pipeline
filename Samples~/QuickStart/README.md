# Quick Start

The smallest example of one table becoming ScriptableObject assets. There is no importer code.

## What is in here

| File | Role |
|---|---|
| `Quests.csv` | A table holding four quests |
| `QuestData.cs` | A ScriptableObject carrying `[CsvAsset]` |

## Try it

1. Importing this sample puts it under `Assets/Samples/CSV Pipeline/<version>/Quick Start/`.
2. Once the scripts compile, **save `Quests.csv` once more**, or run
   `Tools ▸ CSV Pipeline ▸ Rebuild All Tables`.
   (`AssetPostprocessor` only fires when a file *changes*, so a file already sitting there has to be
   touched once. When your CSV root differs from the default, re-saving the file is the surer route.)
3. A **`QuestData/` folder** appears beside the table, holding one asset per row.

## What this example shows

**Field names and column names bind regardless of case.** The `recommendedLevel` field binds to the
`RecommendedLevel` column. Nothing has to be written down for that.

**A value that looks like an integer is still a float if the field is.** The `600` in `TimeLimit` goes
in as `float`. Deciding a type from the value is where this kind of tool goes wrong most often; here
the field decides, so the two cannot disagree.

**Several values in one cell** are split with `;`. The `Rewards` column becomes a `List<string>`.

**When the column name differs**, name it with `[CsvColumn("Requires")]`.

**Fields you do not author in the table** are kept out with `[CsvIgnore]`. Wire `banner` in the
Inspector and it survives a reimport.

**An empty cell does not clear the existing value.** `Requires` is empty for `Quest_FirstSteps`, and
that means "leave it alone", not "clear the prerequisite". To make emptying an authoring act, add
`[CsvColumn(OverwriteWhenEmpty = true)]`.

## Going back the other way

Edit values in the editor, then run `Tools ▸ CSV Pipeline ▸ Export Assets to Tables` and `Quests.csv`
is rewritten from the assets. What changes is shown before anything is overwritten.

## Things to try in the table

- Delete a row and save, and its asset goes away as well.
  But **if a scene or prefab still references that asset, it is kept with a warning instead.**
- Put something like `Huge` in `Difficulty` and that row alone warns, listing **the values it allows.**
- Rename the `Title` column to `Ti_tle` and the report points at the column spelled almost the same.
  A wider typo like `Titel` is reported as "no matching column for this field" — either way the field
  **keeps its value** and no default is quietly baked over it.
- Put an unmatched double quote in a cell. The table **refuses to bake entirely** rather than let the
  rows after it vanish.
