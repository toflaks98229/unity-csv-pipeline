# Running the tests

The EditMode tests in this package run **without opening the editor**. Value conversion, asset
creation, cleanup and round-tripping are not guaranteed by compilation, so actually running them is
the only way to check.

## 1. In the editor

**Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All**

With the package under `Packages/` it shows up as it is. When it came in through UPM, the consuming
project's `Packages/manifest.json` needs `testables` for it to appear in the list.

```json
"testables": [ "com.toflaks.csv-pipeline" ]
```

## 2. From the command line (recommended)

An editor holding that project open locks it, so this will not run. Making a **separate sandbox
project** lets you leave the editor you are working in alone.

```sh
UNITY="C:/Program Files/Unity/Hub/Editor/<version>/Editor/Unity.exe"
SB=/tmp/csvsandbox

# 1) Create an empty project
"$UNITY" -batchmode -createProject "$SB" -logFile "$SB.create.log" -quit

# 2) In manifest.json, point at the package by local path and register it under testables
#    "com.toflaks.csv-pipeline": "file:/absolute/path/com.toflaks.csv-pipeline"
#    "testables": [ "com.toflaks.csv-pipeline" ]

# 3) Run the tests
"$UNITY" -batchmode -projectPath "$SB" -runTests -testPlatform EditMode \
         -testResults "$SB/results.xml" -logFile "$SB/test.log"
```

**Do not pass `-nographics`.** The tests that check the window actually draws cannot open a screen and
skip themselves. IMGUI compiles fine with mismatched `Begin`/`End`, so drawing it is the only way to
catch that defect. If you must run headless, know that those tests are being skipped.

**The exit code is the result.** `0` = all passed, `2` = something failed, `3` = the run itself failed.
Do not pass `-quit` alongside `-runTests`; it ends before the results are written.

Failures live in `results.xml` under `<test-case result="Failed">`, with the message and the stack.

### When the sandbox goes bad, make a new one

Reusing a sandbox eventually makes it strange. These actually happened.

| Symptom | Real cause | Remedy |
|---|---|---|
| `Couldn't set project path to: <cwd>/<path>` | The `Assets` folder went missing, so it is no longer recognised as a project | `mkdir Assets` |
| `The type or namespace name 'NUnit' could not be found` | An external tool inserted a **relative-path `file:` dependency** into `manifest.json`, and package resolution failed wholesale | Remove that entry **plus delete `packages-lock.json` and empty `Library/PackageCache` and `ScriptAssemblies`** |

**Neither has anything to do with the package code.**

The second one is not fixed by editing `manifest.json` alone. **The lock file keeps holding the broken
resolution.** The Loupedeck plugin `com.logi.unity-bridge` inserted itself into every running Unity
project and produced this symptom over and over. Having the test script strip it out each time is the
better answer.

If it still fails, `rm -rf` and build the sandbox again. Recreating takes two or three minutes, and a
failure that survives that is the real one.

Pass paths in Windows form with `cygpath -w`. Handing them over as-is from Git Bash prepends the
current directory.

## 3. Running in CI

`.github/workflows/tests.yml` runs the EditMode tests on every push. The point is running on **both
the declared minimum (2022.3) and Unity 6 LTS** — the `unity` field in `package.json` is simply a lie
if it is not held to.

This repository has no `Assets/` and no `ProjectSettings/`, because **it is a package, not a project**.
`.ci/project/` is the minimal shell to hand Unity, and its `manifest.json` points at the repository
root as a local package. `.ci/README.md` has the details.

There is a second job that checks **what goes into the distributed payload** — that the internal review
documents and the development infrastructure stay out, that the manual and the sample stay in, and
that no release placeholder is left unfilled. Passing tests do not help if the product itself ships
wrong.

### The first time, a person has to do something

GameCI does not obtain a Unity licence for you. Put three secrets under the repository's
**Settings > Secrets and variables > Actions**.

| Secret | What |
|---|---|
| `UNITY_LICENSE` | The **entire contents** of the `.ulf` licence file |
| `UNITY_EMAIL` | Unity account email |
| `UNITY_PASSWORD` | Unity account password |

The `.ulf` appears once you sign in to Unity Hub and take a personal licence.
On Windows it is at `C:/ProgramData/Unity/Unity_lic.ulf`.
(A personal licence needs no `UNITY_SERIAL`; that one is for Pro.)

Without the secrets, the workflow **says so in its first step and stops.** Activation errors are hard
to read, so it asks first.

### Drawing tests are skipped in CI

The container has no graphics device, so no window can be drawn. The drawing tests look at
`SystemInfo.graphicsDeviceType` and **skip themselves** rather than letting a real defect hide behind
"there was no screen".

**So when you have touched the window, do not trust CI alone.** Run section 2 once on a machine with a
display.

### Catching a commit that edits a table and forgets to bake

This one is about the consuming project. A changed table file shows up in the diff, but **an unchanged
output asset does not.** Absence is not noticeable.

```sh
Unity -batchmode -projectPath . -executeMethod CsvPipeline.CsvDriftCheck.Run
```

A table that has drifted gives **exit code 1** and logs which table drifted and why. Nothing is
written. To check it in the editor, use `Tools ▸ CSV Pipeline ▸ Check for Drift`.

The judgement is **the same one the pipeline window makes.** If a table showing "nothing changes" on
screen failed in CI, one of the two would be lying and nobody could tell which. A test holds them
together.

---

## 4. Checking compilation quickly

To check compilation without launching Unity, reuse the `<Reference>` blocks from a `.csproj` the
editor generated, build a temporary project from them, and `dotnet build`.
**Building the package on its own is the point** — building it merged into a consuming project hides
the case where the package references that project.

## What the tests hold in place

| What | Why |
|---|---|
| The preview **writes nothing** | That is the premise of the feature. Building a plan must not create an asset or change a value |
| The field type beats the shape of the value | `30` goes into a `float` field. Deciding a type from the value is where this kind of tool goes wrong most often |
| Assets that are still referenced are not deleted | Deleting one loses the GUID, and even git will not bring the wiring back |
| An empty cell preserves the existing value | A value authored in the Inspector must not be lost to a table |
| A missing required column bakes nothing | Stopping beats baking defaults over an empty cell |
| An exported table round-trips with the original | A changed header spelling drifts from the sheet and stops sync |
| Identifiers differing only in case are not deleted | The asset store ignores case, so the row just updated would be read as one that vanished |
| An unterminated quote refuses to bake | The rows it swallowed are not counted as skipped, so nothing would show the loss |
| Values outside a field's range are not truncated | `2147483648` reaching an `int` field as `2147483647` leaves no error anywhere |
| A row whose values did not change is not dirtied | Everything dirtied is rewritten at the end, so one edited cell would rewrite the whole table's assets |
| Extensions that can hold a reference are never skipped | One wrong entry in that list silently deletes whatever those files were holding |

## Write new tests on top of memory

Baking rules can be checked **without creating a single asset**. Plug in `MemoryAssetGateway` and both
the table and the outputs stay in memory. With no temp folder there is nothing for tests to tangle on.

```csharp
using var assets = new MemoryAssetGateway().WithTable("Assets/Memory/Widgets.csv", csvText);
using (CsvAssets.Use(assets))
{
    CsvImportReport report = new CsvSchemaImportDefinition(CsvSchema.For(typeof(WidgetData)))
        .Run("Assets/Memory/Widgets.csv");

    Assert.AreEqual(2, report.Created);
    Assert.AreEqual("First Widget", assets.Get<WidgetData>("Assets/Memory/WidgetData/Widget_A.asset").title);
}
```

This gateway is **for the consuming project too.** It lets you check the rules of an importer you wrote
without launching Unity.

A few things that help:

| What | For |
|---|---|
| `WithTable(path, text)` | Places the table text. The folder comes with it |
| `WithAsset(path, asset)` · `Add<T>(path)` | Places an output asset that already exists |
| `Get<T>(path)` | Reads what was baked |
| `Referenced` | Paths put here count as still referenced, so cleanup preserves them |
| `SaveCount` | How many times saving happened. Use it to check the preview writes nothing |
| `DirtyCount` | How many assets were dirtied. Use it to check unchanged rows are left alone |
| `BatchCount` · `BatchDepth` | Whether baking runs inside a deferred-reimport scope, and that it closes |

## Integration tests only for what needs the real AssetDatabase

Three things memory cannot show. **Writing to disk** (an asset whose script link is broken keeps no
values at all), **recreating in a deleted path** (where a reimport used to cut in and throw away the
in-memory edit), and **scanning GUID references**. Only those remain in `CsvRoundTripTests`.

If you add tests there, use **a different temp folder for each**. Deleting and recreating the same path
within one session leaves the AssetDatabase still holding the deleted entry, and results start
depending on execution order. A passing test once failed exactly because of that tangle when another
test was added. `CsvTestFolder.Create()` handles it.
