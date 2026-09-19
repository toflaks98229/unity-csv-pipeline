using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// <b>The whole table = the one asset in the project</b> importer. (when the output is a single asset, like a tier table or a balancing table)
    /// It never creates the asset. Creating one would produce an empty asset with a different GUID,
    /// while the inspector wiring in scenes and prefabs would keep pointing at the old asset and drift silently.
    /// </summary>
    /// <typeparam name="T">ScriptableObject type to update.</typeparam>
    public abstract class CsvSingletonImporter<T> : CsvImportDefinition where T : ScriptableObject
    {
        /// <summary>Asset search filter used to look up the target asset.</summary>
        protected virtual string TypeFilter => $"t:{typeof(T).Name}";

        /// <summary>Hint appended to the warning when the target asset is missing. (where it can be created)</summary>
        protected virtual string MissingAssetHint => null;

        /// <summary>
        /// Writes the values of one row into the single asset.
        /// </summary>
        /// <param name="row">Row to read.</param>
        /// <param name="asset">Target asset.</param>
        /// <param name="serialized">Serialized object of the target asset. It is applied once after every row is processed.</param>
        /// <returns>True when this row was actually applied. (counts the applied rows in the log)</returns>
        protected abstract bool BakeRow(CsvRow row, T asset, SerializedObject serialized);

        /// <summary>Finds the single asset and applies every row to it.</summary>
        /// <param name="table">Parsed table.</param>
        /// <param name="report">Report that records counts and problems.</param>
        protected override void Process(CsvTable table, CsvImportReport report)
        {
            T asset = FindSingle(report);
            if (asset == null) return;

            var serialized = new SerializedObject(asset);

            BakeEach(table.Rows, report,
                     (row, _) => BakeRow(row, asset, serialized)
                         ? CsvBakeOutcome.Updated()
                         : CsvBakeOutcome.Skipped());

            // 취소돼도 여기까지 읽은 값은 반영합니다. 이 임포터는 지우지 않으므로 절반만 반영돼도
            // 잃는 것이 없고, 되돌리면 이미 구운 행까지 버리게 됩니다.
            bool changed = serialized.ApplyModifiedPropertiesWithoutUndo();

            // 값이 하나도 달라지지 않았으면 더럽히지 않습니다. 더럽힌 에셋은 굽기 끝의
            // SaveAssets 가 전부 다시 씁니다 — 3,000행 표에서 한 칸만 고쳐도 3,000개를 다시
            // 쓰던 자리입니다. ApplyModifiedPropertiesWithoutUndo 는 실제로 바뀐 것이 있을 때만
            // true 를 돌려주므로, 그 답을 그대로 씁니다.
            if (changed) CsvAssets.Current.MarkDirty(asset);
        }

        /// <summary>
        /// Computes which asset is updated. This importer neither creates nor deletes.
        /// </summary>
        /// <param name="table">Parsed table.</param>
        /// <param name="plan">Plan to fill in.</param>
        protected override void BuildPlan(CsvTable table, CsvImportPlan plan)
        {
            List<string> paths = SortedPaths();

            if (paths.Count == 0)
            {
                string hint = MissingAssetHint;
                plan.Issues.Add(new CsvIssue(CsvIssueSeverity.Error,
                    $"There is no {typeof(T).Name} asset. This table never creates one."
                    + (string.IsNullOrEmpty(hint) ? string.Empty : $" {hint}")));
                plan.Unsupported = "There is no asset to update.";
                return;
            }

            if (paths.Count > 1)
            {
                plan.Issues.Add(new CsvIssue(CsvIssueSeverity.Warning,
                    $"There are {paths.Count} {typeof(T).Name} assets. Only the first by path is updated."));
            }

            plan.Add(CsvChangeKind.Update, paths[0], 0, $"All {table.Count} rows are applied to this single asset.");
        }

        /// <summary>Finds the target asset in the project. Reports an error and returns null when there is none.</summary>
        /// <param name="report">Report that records the results.</param>
        /// <returns>The asset that was found, or null.</returns>
        private T FindSingle(CsvImportReport report)
        {
            var found = new List<T>();
            foreach (string path in SortedPaths())
            {
                if (CsvAssets.Current.Load(path, typeof(T)) is T asset) found.Add(asset);
            }

            if (found.Count == 0)
            {
                string hint = MissingAssetHint;
                report.Error(
                    $"No {typeof(T).Name} asset was found, so this table was not applied. "
                    + "Its output is a single project-wide asset, which the importer never creates."
                    + (string.IsNullOrEmpty(hint) ? string.Empty : $" {hint}"));
                return null;
            }

            if (found.Count > 1)
            {
                // 갱신되지 않은 쪽은 낡은 값으로 남아, 어느 것을 참조하느냐에 따라 결과가 갈립니다.
                report.Warn(
                    $"There are {found.Count} {typeof(T).Name} assets. Only the first by path is updated. "
                    + "A table like this holds game-wide rules, so keep exactly one.", 0, null, found[0]);
            }

            return found[0];
        }

        /// <summary>
        /// Returns the asset paths of the target type in path order.
        /// Search order is not guaranteed, so sorting pins it down and keeps a different asset from being updated each run when there are several.
        /// </summary>
        /// <returns>The sorted asset paths.</returns>
        private List<string> SortedPaths()
        {
            var paths = new List<string>(CsvAssets.Current.FindPaths(TypeFilter));
            paths.Sort(System.StringComparer.Ordinal);
            return paths;
        }
    }
}
