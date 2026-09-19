using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace CsvPipeline
{
    /// <summary>
    /// Skeleton of an importer that watches one table with a fixed file name and bakes it.
    /// It holds the repeating procedure — detecting the file, warning about a removed source table,
    /// parsing, validating columns, reporting the result, saving — and leaves only the actual baking
    /// to the derived class.
    /// </summary>
    public abstract class CsvImportDefinition
    {
        /// <summary>File name of the source table to watch. (extension included)</summary>
        protected abstract string FileName { get; }

        /// <summary>
        /// Folder the output assets are placed in. When null, no warning is raised if the source table
        /// disappears. (for importers that only update existing assets and never create new ones)
        /// </summary>
        protected virtual string OutputFolder => null;

        /// <summary>
        /// Columns the table must have. If even one is missing, <b>nothing is applied</b> and the run
        /// reports an error. This prevents the accident where a missing column is treated as an empty
        /// cell and default values are baked in silence.
        /// </summary>
        protected virtual IEnumerable<string> RequiredColumns => null;

        /// <summary>
        /// Log prefix tag. It defaults to the name of the class that encloses this definition, or to its
        /// own name when there is none.
        /// </summary>
        protected virtual string LogTag
        {
            get
            {
                System.Type type = GetType();
                return $"[{(type.DeclaringType != null ? type.DeclaringType.Name : type.Name)}]";
            }
        }

        /// <summary>
        /// Takes the batched asset import notification and bakes only when the table this importer
        /// watches is involved.
        /// </summary>
        /// <param name="imported">Paths of the imported assets.</param>
        /// <param name="deleted">Paths of the deleted assets.</param>
        /// <param name="moved">New paths of the moved assets.</param>
        public void Execute(string[] imported, string[] deleted, string[] moved)
        {
            // 표를 여러 장 써 넣는 도중이면 굽지 않습니다. 자동 경로만 막고, 경로를 직접 넘기는
            // Run은 명시적 호출이라 그대로 돕니다.
            if (CsvImport.IsSuppressed) return;

            string fileName = FileName;

            // 산출물 폴더를 묻는 일은 비쌉니다 — 선언에 적혀 있지 않으면 표를 프로젝트 전체에서 찾습니다.
            // 이 통지는 프로젝트의 모든 임포트마다 오고 그중 이 표와 관계있는 것은 거의 없으므로,
            // 값싼 판정을 먼저 하고 정말 물어야 할 때만 묻습니다.
            if (WasRemoved(deleted, fileName))
            {
                string outputFolder = OutputFolder;
                if (outputFolder != null) CsvAssetPipeline.WarnSourceRemoved(outputFolder, fileName, LogTag);
            }

            if (!CsvImportUtil.Touched(imported, moved, fileName)) return;

            string csvPath = CsvAssetPipeline.FindCsvPath(fileName);
            if (csvPath == null) return;

            Run(csvPath);
        }

        /// <summary>Whether this table was deleted in this notification.</summary>
        /// <param name="deleted">Paths of the deleted assets.</param>
        /// <param name="fileName">File name of the table being watched.</param>
        /// <returns>True when it was deleted.</returns>
        private static bool WasRemoved(string[] deleted, string fileName)
        {
            if (deleted == null) return false;

            for (int i = 0; i < deleted.Length; i++)
            {
                if (CsvImportUtil.IsFile(deleted[i], fileName)) return true;
            }
            return false;
        }

        /// <summary>Minimum row count that turns the progress bar on. On small tables, drawing it costs more than baking.</summary>
        private const int ProgressThreshold = 200;

        /// <summary>Whether a progress bar was ever shown. Once shown, it must be cleared.</summary>
        private bool _progressShown;

        /// <summary>Whether a person pressed cancel.</summary>
        private bool _cancelled;

        /// <summary>Whether this bake was cancelled. The cleanup step reads this value and skips itself.</summary>
        protected bool IsCancelled => _cancelled;

        /// <summary>
        /// Stops this bake without baking the remaining units. <b>The cleanup step is skipped as well.</b>
        /// Use it when you notice mid-bake a condition that makes any further result wrong — the source of
        /// a reference having disappeared, for instance. Deleting the rest while only half the table has
        /// been applied leaves a person with nothing to undo.
        /// </summary>
        protected void CancelCurrentRun() => _cancelled = true;

        /// <summary>
        /// Reads the table at the given path, bakes it, and reports the result. A menu item can call it
        /// directly too.
        /// </summary>
        /// <param name="csvPath">Asset path of the table to read.</param>
        /// <returns>The import report, or null when there is nothing to read.</returns>
        public CsvImportReport Run(string csvPath)
        {
            CsvTable table = CsvImportUtil.ReadTable(csvPath, out string problem, out bool unreadable);
            if (table == null)
            {
                // 인코딩이 틀린 표는 읽지 '못한' 것이지 읽을 것이 '없는' 것이 아닙니다.
                // 조용히 돌아가면 사람은 저장이 반영된 줄 압니다.
                if (!unreadable) return null;

                var failure = new CsvImportReport(FileName, LogTag);
                failure.Error(problem);
                failure.Emit();
                return failure;
            }

            var report = new CsvImportReport(FileName, LogTag);

            if (!ValidateColumns(table, report))
            {
                report.Emit();
                return report;
            }

            _progressShown = false;
            _cancelled = false;

            try
            {
                Process(table, report);
            }
            finally
            {
                if (_progressShown) EditorUtility.ClearProgressBar();
            }

            if (_cancelled)
            {
                report.Warn("Cancelled, so only part of the table was applied. Cleanup of removed rows was not run.");
            }

            report.Emit();

            if (report.Touched) CsvAssets.Current.SaveAll();
            return report;
        }

        // ====================================================================================================
        // 굽기 골격
        // ====================================================================================================

        /// <summary>
        /// Set that holds the names and paths this bake confirmed. <b>It ignores letter case.</b>
        /// <para>
        /// Cleanup matches the file names in the folder against this set to find "rows that disappeared
        /// from the table". But the asset stores on Windows and macOS ignore letter case, so changing
        /// <c>Sword</c> to <c>sword</c> in the table makes <b>the very asset just updated</b> look like a
        /// disappeared row — and the report prints "updated 1 / deleted 1", which reads as success.
        /// The collision check (<see cref="CsvIdClaims"/>) already ignored letter case; only the cleanup
        /// check was missing it.
        /// </para>
        /// <para>
        /// On a case-sensitive file system, where <c>Sword.asset</c> and <c>sword.asset</c> really are two
        /// files, this leaves one of them undeletable. That is the same direction taken everywhere else in
        /// this package — <b>erring toward keeping costs a little extra, erring toward deleting loses
        /// data.</b>
        /// </para>
        /// </summary>
        /// <returns>An empty set.</returns>
        protected static HashSet<string> NewKeySet() => new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>What to match output assets against to find the ones that disappeared from the table. The default is no cleanup.</summary>
        protected virtual CsvReconcileMode ReconcileMode => CsvReconcileMode.None;

        /// <summary>Search filter used to find cleanup candidates. Required when <see cref="ReconcileMode"/> is not None.</summary>
        protected virtual string ReconcileTypeFilter => null;

        /// <summary>Output folder to clean up. Defaults to <see cref="OutputFolder"/>.</summary>
        protected virtual string ReconcileFolder => OutputFolder;

        /// <summary>
        /// Calls <paramref name="bake"/> for each unit and handles progress, cancellation, counting,
        /// collision checks, and cleanup in one place.
        /// <para>
        /// <b>Cleanup runs only when the bake was not cancelled.</b> After a cancel, units remain unread,
        /// and their output assets would be mistaken for "gone from the table" and deleted. Restating this
        /// rule in every derived class means one omission turns the cancel button into a delete button,
        /// so it lives here.
        /// </para>
        /// <para>
        /// <b>It reports a place claimed twice.</b> A collided identifier overwrites the earlier row and
        /// erases its values, but the counts are kept in a <c>HashSet</c> so the numbers never show it.
        /// This too is not something each derived class should restate; it is a contract the skeleton keeps.
        /// </para>
        /// </summary>
        /// <typeparam name="TUnit">Type of the unit to bake. (a row, or a group identifier)</typeparam>
        /// <param name="units">Units to bake.</param>
        /// <param name="report">Report that records counts and problems.</param>
        /// <param name="bake">Function that bakes one unit and returns the outcome.</param>
        protected void BakeEach<TUnit>(IReadOnlyList<TUnit> units, CsvImportReport report,
                                       Func<TUnit, CsvImportReport, CsvBakeOutcome> bake)
        {
            HashSet<string> validNames = NewKeySet();
            HashSet<string> validPaths = NewKeySet();
            var claims = new CsvIdClaims();

            // 되임포트를 미루지 않으면 행마다 에셋 파이프라인이 한 번씩 돕니다. 정리(삭제)는 범위
            // 밖에서 합니다 — 지우는 일은 건수가 적고, 배치 안에서 지우면 그 뒤의 참조 조사가
            // 아직 반영되지 않은 상태를 보게 됩니다.
            using (CsvAssets.Current.BatchEdits())
            {
                for (int i = 0; i < units.Count; i++)
                {
                    if (ReportRowProgress(i, units.Count)) break;

                    CsvBakeOutcome outcome = bake(units[i], report);

                    switch (outcome.Kind)
                    {
                        case CsvBakeKind.Created: report.CountCreated(); break;
                        case CsvBakeKind.Updated: report.CountUpdated(); break;
                        default: report.CountSkipped(); continue;
                    }

                    WarnIfClaimed(claims, outcome, report);

                    if (outcome.Name != null) validNames.Add(outcome.Name);
                    if (outcome.Path != null) validPaths.Add(outcome.Path);
                }
            }

            if (IsCancelled) return;

            Reconcile(validNames, validPaths, report);
        }

        /// <summary>
        /// Warns when this unit points at the same asset as an earlier one.
        /// A place is identified by its <b>path</b>. Two rows with the same identifier can land in
        /// different folders depending on the derived type, and then they are different assets — warning
        /// there would report a problem that does not exist.
        /// </summary>
        /// <param name="claims">Ledger of claimed places for this table.</param>
        /// <param name="outcome">Outcome of the unit just baked.</param>
        /// <param name="report">Report that records the warning.</param>
        private static void WarnIfClaimed(CsvIdClaims claims, CsvBakeOutcome outcome, CsvImportReport report)
        {
            string key = outcome.Path ?? outcome.Name;
            string display = outcome.Name ?? outcome.Path;
            if (key == null) return;

            if (claims.TryClaim(key, display, outcome.Line, out CsvIdClaim taken)) return;

            report.Warn(CsvIdClaims.Describe(display, taken), outcome.Line);
        }

        /// <summary>
        /// Cleans up the output assets this table did not confirm. Assets that are still referenced are
        /// preserved.
        /// </summary>
        /// <param name="validNames">Asset names confirmed by this run.</param>
        /// <param name="validPaths">Asset paths confirmed by this run.</param>
        /// <param name="report">Report that records the result.</param>
        private void Reconcile(HashSet<string> validNames, HashSet<string> validPaths, CsvImportReport report)
        {
            CsvReconcileMode mode = ReconcileMode;
            if (mode == CsvReconcileMode.None) return;

            string folder = ReconcileFolder;
            string filter = ReconcileTypeFilter;
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(filter)) return;

            if (mode == CsvReconcileMode.ByPath)
                CsvAssetPipeline.ReconcileFolderByPath(folder, filter, validPaths, LogTag, report);
            else
                CsvAssetPipeline.ReconcileFolderByName(folder, filter, validNames, LogTag, report);
        }

        /// <summary>
        /// Reports row progress and returns whether the run was cancelled. The bar appears only on large
        /// tables. <b>On true, the loop must break immediately.</b> Skipping the cleanup is handled for
        /// you by <see cref="BakeEach{TUnit}"/>.
        /// </summary>
        /// <param name="index">Index of the row about to be processed. (zero-based)</param>
        /// <param name="total">Total number of rows.</param>
        /// <returns>True when cancelled.</returns>
        protected bool ReportRowProgress(int index, int total)
        {
            if (_cancelled) return true;
            if (total < ProgressThreshold) return false;

            // 매 행마다 그리면 그리는 데 드는 값이 굽는 값을 넘습니다.
            if (index != 0 && (index & 31) != 0) return false;

            _progressShown = true;
            _cancelled = EditorUtility.DisplayCancelableProgressBar(
                $"Baking {FileName}", $"{index} / {total} rows", (float)index / total);

            return _cancelled;
        }

        /// <summary>
        /// Checks that every required column is present. For a missing column, it also names a candidate
        /// whose spelling is nearly the same.
        /// </summary>
        /// <param name="table">Table to check.</param>
        /// <param name="report">Report that records the result.</param>
        /// <returns>True when the run may continue.</returns>
        private bool ValidateColumns(CsvTable table, CsvImportReport report)
        {
            WarnDuplicateColumns(table, report);

            List<string> missing = table.FindMissingColumns(RequiredColumns);
            if (missing.Count == 0) return true;

            foreach (string column in missing)
            {
                string similar = table.FindSimilarColumn(column);

                // 대소문자 차이는 헤더 대조가 이미 흡수하므로 여기까지 오지 않습니다. 여기 걸리는
                // 것은 언제나 공백·밑줄·하이픈 차이입니다 — "대소문자가 다릅니다"라고 적으면
                // 사람은 멀쩡한 표기를 들여다보며 없는 문제를 찾습니다.
                report.Error(similar != null
                    ? $"Column '{column}' is missing. There is a '{similar}', spelled almost the same. (a space, underscore, or hyphen differs)"
                    : $"Column '{column}' is missing. Headers in the table: {string.Join(", ", table.Headers)}");
            }

            report.Info("This table was not applied, so that missing columns are not baked as empty cells.");
            return false;
        }

        /// <summary>
        /// Reports when two or more columns share a name. <b>It does not stop the run.</b>
        /// <para>
        /// The row dictionary keys values by name, so the later column overwrites the earlier one — the
        /// value written in the earlier column survives nowhere, and the counts never showed it. The
        /// later column's value does bake correctly, though, so stopping the bake would leave a perfectly
        /// good table unapplied. Saying what is being lost is the right call.
        /// </para>
        /// </summary>
        /// <param name="table">Table to check.</param>
        /// <param name="report">Report that records the warning.</param>
        private static void WarnDuplicateColumns(CsvTable table, CsvImportReport report)
        {
            foreach (string column in table.DuplicateHeaders)
            {
                report.Warn($"There are two or more '{column}' columns. Only the last one's values are used, and what is written in the earlier ones is discarded. "
                          + "(columns differing only by letter case count as the same column) Delete one, or rename it.");
            }
        }

        /// <summary>
        /// Takes the parsed table and bakes the assets.
        /// </summary>
        /// <param name="table">The parsed table. It has at least one row.</param>
        /// <param name="report">Report that records counts and problems.</param>
        protected abstract void Process(CsvTable table, CsvImportReport report);

        // ====================================================================================================
        // 미리보기
        // ====================================================================================================

        /// <summary>Name shown in the preview. It defaults to the log tag with the brackets stripped.</summary>
        protected virtual string PlanLabel => LogTag.Trim('[', ']');

        /// <summary>
        /// Works out <b>what would change</b> if the table were baked right now. <b>It writes nothing.</b>
        /// </summary>
        /// <param name="csvPath">Path of the table to read. When null, the table is located by file name.</param>
        /// <returns>The plan. When no plan could be built, <see cref="CsvImportPlan.Unsupported"/> holds the reason.</returns>
        public CsvImportPlan Plan(string csvPath = null)
        {
            var plan = new CsvImportPlan(FileName, PlanLabel);

            string path = csvPath ?? CsvAssetPipeline.FindCsvPath(FileName);
            if (path == null)
            {
                plan.Unsupported = "The table file was not found.";
                return plan;
            }

            plan.OutputFolder = OutputFolder;

            CsvTable table = CsvImportUtil.ReadTable(path, out string problem, out bool unreadable);
            if (table == null)
            {
                plan.Unsupported = problem ?? "The table could not be read, or it has no data rows.";

                // 데이터가 없는 것은 표를 아직 안 채운 것이라 손볼 것이 아닙니다.
                // 글자를 못 읽은 것은 고쳐야 할 문제라 창에서 눈에 띄어야 합니다.
                if (unreadable) plan.Issues.Add(new CsvIssue(CsvIssueSeverity.Error, problem));
                return plan;
            }

            List<string> missing = table.FindMissingColumns(RequiredColumns);
            if (missing.Count > 0)
            {
                foreach (string column in missing)
                {
                    string similar = table.FindSimilarColumn(column);
                    plan.Issues.Add(new CsvIssue(CsvIssueSeverity.Error, similar != null
                        ? $"Column '{column}' is missing. There is a '{similar}', which has a similar name."
                        : $"Column '{column}' is missing."));
                }
                plan.Unsupported = "A required column is missing, so nothing is baked.";
                return plan;
            }

            BuildPlan(table, plan);
            return plan;
        }

        /// <summary>
        /// Reads the table and fills in the changes it would make. An importer that does not override
        /// this is treated as one that does not support preview.
        /// <b>Do not write assets here.</b>
        /// </summary>
        /// <param name="table">The parsed table.</param>
        /// <param name="plan">The plan to fill in.</param>
        protected virtual void BuildPlan(CsvTable table, CsvImportPlan plan)
            => plan.Unsupported = "This importer does not support preview.";

        /// <summary>
        /// Records a warning in the plan when two rows point at the same asset during planning.
        /// It must use the same check as <see cref="BakeEach{TUnit}"/> on the baking side, so that
        /// <b>the preview and the real run never disagree.</b>
        /// </summary>
        /// <param name="claims">Ledger of claimed places for this table.</param>
        /// <param name="plan">The plan that records the warning.</param>
        /// <param name="key">Value that identifies the place. Usually the asset path.</param>
        /// <param name="display">Identifier spelling to show a person.</param>
        /// <param name="line">Line number of the current row.</param>
        /// <returns>True when this is the first claim on the place.</returns>
        protected static bool ClaimForPlan(CsvIdClaims claims, CsvImportPlan plan,
                                           string key, string display, int line)
        {
            if (claims.TryClaim(key, display, line, out CsvIdClaim taken)) return true;

            plan.Issues.Add(new CsvIssue(CsvIssueSeverity.Warning,
                                         CsvIdClaims.Describe(display, taken), line));
            return false;
        }

        /// <summary>
        /// Adds the output assets that would disappear from the table to the plan. Assets that are still
        /// referenced are classified as preserved.
        /// </summary>
        /// <param name="plan">The plan to fill in.</param>
        /// <param name="folder">Folder to clean up.</param>
        /// <param name="typeFilter">Asset search filter.</param>
        /// <param name="valid">Names or paths confirmed by this table.</param>
        /// <param name="byPath">Whether to match against paths.</param>
        protected static void PlanObsolete(CsvImportPlan plan, string folder, string typeFilter,
                                           ICollection<string> valid, bool byPath)
        {
            CsvAssetPipeline.PlanReconcile(folder, typeFilter, valid, byPath,
                                           out List<string> deletable, out List<string> preserved);

            foreach (string path in deletable) plan.Add(CsvChangeKind.Delete, path);
            if (preserved.Count == 0) return;

            // 조사할 수 없어 보존한 것과 참조가 남아 보존한 것은 뜻이 다릅니다. 같은 말로 적으면
            // 안전장치가 꺼져 있다는 사실이 "잘 지켜지고 있다"로 읽힙니다.
            string blocked = CsvAssetPipeline.ReferenceScanBlocked;
            string note = blocked ?? "Still referenced somewhere else, so it is not deleted.";

            foreach (string path in preserved) plan.Add(CsvChangeKind.Preserve, path, 0, note);

            if (blocked != null) plan.Issues.Add(new CsvIssue(CsvIssueSeverity.Warning, blocked));
        }

        /// <summary>
        /// Finds every importer definition with a parameterless constructor in the loaded assemblies and
        /// creates one of each. The preview window uses it so that hand-written importers also appear in
        /// the list.
        /// </summary>
        /// <returns>The definitions found, ordered by table file name.</returns>
        public static List<CsvImportDefinition> DiscoverAll()
        {
            if (_definitionTypes == null) _definitionTypes = ScanDefinitionTypes();

            var found = new List<CsvImportDefinition>(_definitionTypes.Count);
            foreach (Type type in _definitionTypes)
            {
                try { found.Add((CsvImportDefinition)Activator.CreateInstance(type, true)); }
                catch (Exception) { /* 만들 수 없게 된 정의는 목록에서 빠집니다. */ }
            }

            found.Sort((a, b) => string.CompareOrdinal(a.FileName, b.FileName));
            return found;
        }

        /// <summary>
        /// The definition types found so far. <b>It holds types, not instances</b> — a definition carries
        /// cancellation state while it bakes, so sharing one drags the state of the previous run along.
        /// Editing a script reloads the domain, and this value disappears with it.
        /// </summary>
        private static List<Type> _definitionTypes;

        /// <summary>Drops the definition list being held.</summary>
        public static void InvalidateCache() => _definitionTypes = null;

        /// <summary>
        /// Walks every loaded assembly to find the definition types. <b>This is the expensive half.</b>
        /// The result is held so that refreshing the window does not repeat it.
        /// </summary>
        /// <returns>The definition types found.</returns>
        private static List<Type> ScanDefinitionTypes()
        {
            var found = new List<Type>();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // 검사용 임포터는 소비 프로젝트의 것이 아닙니다. (CsvAssemblies 참고)
                if (CsvAssemblies.IsTestAssembly(assembly)) continue;

                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }

                foreach (Type type in types)
                {
                    if (type == null || type.IsAbstract || type.IsGenericTypeDefinition) continue;
                    if (!typeof(CsvImportDefinition).IsAssignableFrom(type)) continue;
                    if (type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                            null, Type.EmptyTypes, null) == null) continue;

                    found.Add(type);
                }
            }

            return found;
        }
    }
}
