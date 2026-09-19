using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// Importer that <b>only updates assets that already exist</b>. It never creates an asset, and never deletes the asset of a vanished row.
    /// Use it for a table where the CSV owns only some fields of the asset and the rest are authored by hand.
    /// </summary>
    /// <typeparam name="T">ScriptableObject type to update.</typeparam>
    public abstract class CsvPatchImporter<T> : CsvImportDefinition where T : ScriptableObject
    {
        /// <summary>Pulls the key that finds the target asset out of a row. An empty one skips that row.</summary>
        /// <param name="row">Row to read.</param>
        /// <returns>The lookup key.</returns>
        protected abstract string GetRowKey(CsvRow row);

        /// <summary>Pulls the lookup key out of an asset. An asset with an empty key drops out of the index.</summary>
        /// <param name="asset">Asset to index.</param>
        /// <returns>The lookup key.</returns>
        protected abstract string GetAssetKey(T asset);

        /// <summary>Writes the row values into an existing asset.</summary>
        /// <param name="row">Row to read.</param>
        /// <param name="asset">Target asset.</param>
        /// <param name="serialized">Serialized object of the target asset. It is applied automatically after the call.</param>
        protected abstract void Patch(CsvRow row, T asset, SerializedObject serialized);

        /// <summary>Asset search filter used to build the asset index.</summary>
        protected virtual string TypeFilter => $"t:{typeof(T).Name}";

        /// <summary>Hint appended to the warning when no asset matches the key.</summary>
        protected virtual string MissingAssetHint => null;

        /// <summary>Finds the existing asset by key and applies the row values to it.</summary>
        /// <param name="table">Parsed table.</param>
        /// <param name="report">Report that records counts and problems.</param>
        protected override void Process(CsvTable table, CsvImportReport report)
        {
            Dictionary<string, T> byKey = BuildIndex();

            BakeEach(table.Rows, report, (row, rep) => PatchOne(row, byKey, rep));
        }

        /// <summary>Applies one row to an asset that already exists.</summary>
        /// <param name="row">Row to read.</param>
        /// <param name="byKey">Assets indexed by lookup key.</param>
        /// <param name="report">Report that records problems.</param>
        /// <returns>The bake result.</returns>
        private CsvBakeOutcome PatchOne(CsvRow row, Dictionary<string, T> byKey, CsvImportReport report)
        {
            string key = GetRowKey(row);
            if (string.IsNullOrEmpty(key))
            {
                report.Warn("The lookup key is empty, skipping this row.", row.LineNumber);
                return CsvBakeOutcome.Skipped();
            }

            if (!byKey.TryGetValue(key, out T asset))
            {
                string hint = MissingAssetHint;
                report.Warn($"No {typeof(T).Name} found for '{key}'."
                            + (string.IsNullOrEmpty(hint) ? string.Empty : $" {hint}"), row.LineNumber);
                return CsvBakeOutcome.Skipped();
            }

            var serialized = new SerializedObject(asset);
            Patch(row, asset, serialized);
            bool changed = serialized.ApplyModifiedPropertiesWithoutUndo();

            // 값이 하나도 달라지지 않았으면 더럽히지 않습니다. 더럽힌 에셋은 굽기 끝의
            // SaveAssets 가 전부 다시 씁니다 — 3,000행 표에서 한 칸만 고쳐도 3,000개를 다시
            // 쓰던 자리입니다. ApplyModifiedPropertiesWithoutUndo 는 실제로 바뀐 것이 있을 때만
            // true 를 돌려주므로, 그 답을 그대로 씁니다.
            if (changed) CsvAssets.Current.MarkDirty(asset);

            // 이 임포터는 정리를 하지 않아 이름·경로가 대조에 쓰이지 않습니다. 그래도 넘기는 것은
            // 같은 에셋을 두 행이 덮어쓰는 것을 골격이 알아채게 하기 위해서입니다.
            return CsvBakeOutcome.Updated(key, CsvAssets.Current.PathOf(asset), row.LineNumber);
        }

        /// <summary>
        /// Computes which assets are updated. This importer neither creates nor deletes, so only updates and skips come out.
        /// </summary>
        /// <param name="table">Parsed table.</param>
        /// <param name="plan">Plan to fill in.</param>
        protected override void BuildPlan(CsvTable table, CsvImportPlan plan)
        {
            Dictionary<string, T> byKey = BuildIndex();
            var claims = new CsvIdClaims();

            foreach (CsvRow row in table.Rows)
            {
                string key = GetRowKey(row);
                if (string.IsNullOrEmpty(key))
                {
                    plan.Add(CsvChangeKind.Skip, null, row.LineNumber, "The lookup key is empty.");
                    continue;
                }

                if (!byKey.TryGetValue(key, out T asset))
                {
                    plan.Add(CsvChangeKind.Skip, null, row.LineNumber,
                             $"No {typeof(T).Name} exists for '{key}'.");
                    continue;
                }

                string path = CsvAssets.Current.PathOf(asset);
                ClaimForPlan(claims, plan, path, key, row.LineNumber);

                plan.Add(CsvChangeKind.Update, path, row.LineNumber);
            }
        }

        /// <summary>Indexes the project's T assets by lookup key.</summary>
        /// <returns>The key-to-asset dictionary.</returns>
        private Dictionary<string, T> BuildIndex()
        {
            var map = new Dictionary<string, T>();

            foreach (string path in CsvAssets.Current.FindPaths(TypeFilter))
            {
                if (!(CsvAssets.Current.Load(path, typeof(T)) is T asset)) continue;

                string key = GetAssetKey(asset);
                if (!string.IsNullOrEmpty(key)) map[key] = asset;
            }
            return map;
        }
    }
}
