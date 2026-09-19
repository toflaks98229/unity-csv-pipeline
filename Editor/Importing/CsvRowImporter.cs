using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// <b>One row = one asset</b> importer. It takes the row identifier as the file name to create or update an asset,
    /// and cleans up the assets of rows that vanished from the table.
    /// </summary>
    /// <typeparam name="T">ScriptableObject type to bake.</typeparam>
    public abstract class CsvRowImporter<T> : CsvImportDefinition where T : ScriptableObject
    {
        /// <summary>Folder the output assets go into.</summary>
        protected abstract override string OutputFolder { get; }

        /// <summary>Pulls the asset identifier (= file name) out of a row. An empty one skips that row.</summary>
        /// <param name="row">Row to read.</param>
        /// <returns>The identifier.</returns>
        protected abstract string GetId(CsvRow row);

        /// <summary>
        /// Writes the row values into the asset. Write to <paramref name="serialized"/> with <see cref="SoBaker"/>.
        /// </summary>
        /// <param name="row">Row to read.</param>
        /// <param name="asset">Target asset.</param>
        /// <param name="serialized">Serialized object of the target asset. It is applied automatically after the call.</param>
        protected abstract void Bake(CsvRow row, T asset, SerializedObject serialized);

        /// <summary>Asset search filter used for output cleanup.</summary>
        protected virtual string TypeFilter => $"t:{typeof(T).Name}";

        /// <summary>
        /// Whether cleanup matches against the asset path instead of the asset name.
        /// Turn it on when the name alone cannot match, such as when derived types split into subfolders.
        /// </summary>
        protected virtual bool ReconcileByPath => false;

        /// <summary>Cleans up output assets that vanished from the table. <see cref="ReconcileByPath"/> settles how to match.</summary>
        protected sealed override CsvReconcileMode ReconcileMode
            => ReconcileByPath ? CsvReconcileMode.ByPath : CsvReconcileMode.ByName;

        /// <summary>Search filter that finds the cleanup candidates.</summary>
        protected sealed override string ReconcileTypeFilter => TypeFilter;

        /// <summary>Builds the asset path from an identifier.</summary>
        /// <param name="id">Row identifier.</param>
        /// <returns>The asset path.</returns>
        protected virtual string AssetPathFor(string id) => $"{OutputFolder}/{id}.asset";

        /// <summary>
        /// Loads or creates the asset. Override it when the concrete type to create depends on the row values.
        /// </summary>
        /// <param name="id">Row identifier.</param>
        /// <param name="row">Row to read.</param>
        /// <returns>The asset that was loaded or created. Null skips that row.</returns>
        protected virtual T CreateOrLoad(string id, CsvRow row)
            => CsvAssetPipeline.CreateOrLoad<T>(AssetPathFor(id));

        /// <summary>Bakes an asset per row and cleans up output assets that vanished from the table.</summary>
        /// <param name="table">Parsed table.</param>
        /// <param name="report">Report that records counts and problems.</param>
        protected override void Process(CsvTable table, CsvImportReport report)
        {
            CsvAssetPipeline.EnsureFolder(OutputFolder);

            BakeEach(table.Rows, report, BakeOne);
        }

        /// <summary>Bakes one row into an asset.</summary>
        /// <param name="row">Row to read.</param>
        /// <param name="report">Report that records problems.</param>
        /// <returns>The bake result.</returns>
        private CsvBakeOutcome BakeOne(CsvRow row, CsvImportReport report)
        {
            string id = GetId(row);
            if (string.IsNullOrEmpty(id))
            {
                report.Warn("The identifier is empty, skipping this row.", row.LineNumber);
                return CsvBakeOutcome.Skipped();
            }

            string rejected = CsvAssetId.Reject(id);
            if (rejected != null)
            {
                report.Warn(CsvAssetId.Describe(id, rejected), row.LineNumber);
                return CsvBakeOutcome.Skipped();
            }

            bool isNew = CsvAssets.Current.Load(AssetPathFor(id), typeof(T)) == null;

            T asset = CreateOrLoad(id, row);
            if (asset == null) return CsvBakeOutcome.Skipped();

            var serialized = new SerializedObject(asset);
            Bake(row, asset, serialized);
            bool changed = serialized.ApplyModifiedPropertiesWithoutUndo();

            // 값이 하나도 달라지지 않았으면 더럽히지 않습니다. 더럽힌 에셋은 굽기 끝의
            // SaveAssets 가 전부 다시 씁니다 — 3,000행 표에서 한 칸만 고쳐도 3,000개를 다시
            // 쓰던 자리입니다. ApplyModifiedPropertiesWithoutUndo 는 실제로 바뀐 것이 있을 때만
            // true 를 돌려주므로, 그 답을 그대로 씁니다.
            if (isNew || changed) CsvAssets.Current.MarkDirty(asset);
            CsvAssetPipeline.FlushIfCreated(asset, isNew);

            return CsvBakeOutcome.Baked(isNew, id, CsvAssets.Current.PathOf(asset), row.LineNumber);
        }

        /// <summary>Computes, per row, whether it is created or updated, and what disappears. It writes nothing.</summary>
        /// <param name="table">Parsed table.</param>
        /// <param name="plan">Plan to fill in.</param>
        protected override void BuildPlan(CsvTable table, CsvImportPlan plan)
        {
            HashSet<string> validNames = NewKeySet();
            HashSet<string> validPaths = NewKeySet();
            var claims = new CsvIdClaims();

            foreach (CsvRow row in table.Rows)
            {
                string id = GetId(row);
                if (string.IsNullOrEmpty(id))
                {
                    plan.Add(CsvChangeKind.Skip, null, row.LineNumber, "The identifier is empty.");
                    continue;
                }

                string rejected = CsvAssetId.Reject(id);
                if (rejected != null)
                {
                    plan.Add(CsvChangeKind.Skip, null, row.LineNumber, CsvAssetId.Describe(id, rejected));
                    plan.Issues.Add(new CsvIssue(CsvIssueSeverity.Warning,
                                                 CsvAssetId.Describe(id, rejected), row.LineNumber));
                    continue;
                }

                string path = AssetPathFor(id);
                bool exists = CsvAssets.Current.Load(path, typeof(T)) != null;

                ClaimForPlan(claims, plan, path, id, row.LineNumber);

                plan.Add(exists ? CsvChangeKind.Update : CsvChangeKind.Create, path, row.LineNumber);
                validNames.Add(id);
                validPaths.Add(path);
            }

            PlanObsolete(plan, OutputFolder, TypeFilter,
                         ReconcileByPath ? (ICollection<string>)validPaths : validNames, ReconcileByPath);
        }
    }
}
