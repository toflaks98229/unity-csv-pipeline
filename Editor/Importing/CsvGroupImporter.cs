using System;
using System.Collections.Generic;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// <b>Several rows sharing one identifier = one asset</b> importer. (when one table row is one list entry inside an asset)
    /// Groups keep the order in which they first appear in the CSV.
    /// </summary>
    /// <typeparam name="T">ScriptableObject type to bake.</typeparam>
    public abstract class CsvGroupImporter<T> : CsvImportDefinition where T : ScriptableObject
    {
        /// <summary>Folder the output assets go into.</summary>
        protected abstract override string OutputFolder { get; }

        /// <summary>Pulls the group identifier (= file name) out of a row. An empty one skips that row.</summary>
        /// <param name="row">Row to read.</param>
        /// <returns>The group identifier.</returns>
        protected abstract string GetGroupId(CsvRow row);

        /// <summary>
        /// Writes the rows of one group into the asset.
        /// <b>Assign to the asset fields directly here.</b> (this path does not use <c>SerializedObject</c>)
        /// </summary>
        /// <param name="groupId">Group identifier.</param>
        /// <param name="rows">Rows belonging to this group. CSV order is kept.</param>
        /// <param name="asset">Target asset.</param>
        protected abstract void Bake(string groupId, IReadOnlyList<CsvRow> rows, T asset);

        /// <summary>Asset search filter used for output cleanup.</summary>
        protected virtual string TypeFilter => $"t:{typeof(T).Name}";

        /// <summary>Cleans up output assets that vanished from the table, matching against the asset name.</summary>
        protected sealed override CsvReconcileMode ReconcileMode => CsvReconcileMode.ByName;

        /// <summary>Search filter that finds the cleanup candidates.</summary>
        protected sealed override string ReconcileTypeFilter => TypeFilter;

        /// <summary>Builds the asset path from an identifier.</summary>
        /// <param name="groupId">Group identifier.</param>
        /// <returns>The asset path.</returns>
        protected virtual string AssetPathFor(string groupId) => $"{OutputFolder}/{groupId}.asset";

        /// <summary>Groups the rows by identifier, bakes an asset per group, and cleans up output assets that vanished from the table.</summary>
        /// <param name="table">Parsed table.</param>
        /// <param name="report">Report that records counts and problems.</param>
        protected override void Process(CsvTable table, CsvImportReport report)
        {
            CsvAssetPipeline.EnsureFolder(OutputFolder);

            List<string> order = GroupRows(table, report, out Dictionary<string, List<CsvRow>> groups);

            BakeEach(order, report, (id, _) => BakeGroup(id, groups[id]));
        }

        /// <summary>Groups the rows by identifier. Groups keep the order in which they first appear in the table.</summary>
        /// <param name="table">Parsed table.</param>
        /// <param name="report">Report that records problems.</param>
        /// <param name="groups">Receives the identifier-to-rows dictionary.</param>
        /// <returns>The identifiers in order of appearance.</returns>
        private List<string> GroupRows(CsvTable table, CsvImportReport report,
                                       out Dictionary<string, List<CsvRow>> groups)
        {
            // 대소문자만 다른 그룹 식별자는 같은 파일 이름으로 구워집니다. 따로 묶으면 뒤 그룹이
            // 앞 그룹의 에셋을 덮어써 앞 행들이 사라집니다. 한 그룹으로 봅니다.
            groups = new Dictionary<string, List<CsvRow>>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();

            foreach (CsvRow row in table.Rows)
            {
                string id = GetGroupId(row);
                if (string.IsNullOrEmpty(id))
                {
                    // 묶는 단계에서 빠진 행은 구울 단위가 되지 못하므로 여기서 세어 둡니다.
                    report.CountSkipped();
                    report.Warn("The group identifier is empty, skipping this row.", row.LineNumber);
                    continue;
                }

                string rejected = CsvAssetId.Reject(id);
                if (rejected != null)
                {
                    report.CountSkipped();
                    report.Warn(CsvAssetId.Describe(id, rejected), row.LineNumber);
                    continue;
                }

                if (!groups.TryGetValue(id, out List<CsvRow> bucket))
                {
                    bucket = new List<CsvRow>();
                    groups[id] = bucket;
                    order.Add(id);
                }
                bucket.Add(row);
            }

            return order;
        }

        /// <summary>Bakes one group into an asset.</summary>
        /// <param name="id">Group identifier.</param>
        /// <param name="rows">Rows belonging to this group.</param>
        /// <returns>The bake result.</returns>
        private CsvBakeOutcome BakeGroup(string id, List<CsvRow> rows)
        {
            T asset = CsvAssetPipeline.CreateOrLoad<T>(AssetPathFor(id), out bool created);
            if (asset == null) return CsvBakeOutcome.Skipped();

            Bake(id, rows, asset);
            CsvAssets.Current.MarkDirty(asset);
            CsvAssetPipeline.FlushIfCreated(asset, created);

            // 그룹의 첫 행을 자리로 씁니다. 같은 식별자의 되풀이는 여기 오기 전에 한 그룹으로 묶이므로,
            // 이 자리에서 부딪치는 것은 대소문자만 다른 식별자뿐입니다.
            return CsvBakeOutcome.Baked(created, id, AssetPathFor(id),
                                        rows.Count > 0 ? rows[0].LineNumber : 0);
        }

        /// <summary>Computes, per group, whether it is created or updated, and what disappears. It writes nothing.</summary>
        /// <param name="table">Parsed table.</param>
        /// <param name="plan">Plan to fill in.</param>
        protected override void BuildPlan(CsvTable table, CsvImportPlan plan)
        {
            HashSet<string> seen = NewKeySet();
            var claims = new CsvIdClaims();

            foreach (CsvRow row in table.Rows)
            {
                string id = GetGroupId(row);
                if (string.IsNullOrEmpty(id))
                {
                    plan.Add(CsvChangeKind.Skip, null, row.LineNumber, "The group identifier is empty.");
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

                // 같은 그룹의 두 번째 행부터는 에셋을 더 만들지 않습니다.
                if (!seen.Add(id)) continue;

                string path = AssetPathFor(id);
                bool exists = CsvAssets.Current.Load(path, typeof(T)) != null;

                ClaimForPlan(claims, plan, path, id, row.LineNumber);

                plan.Add(exists ? CsvChangeKind.Update : CsvChangeKind.Create, path, row.LineNumber);
            }

            PlanObsolete(plan, OutputFolder, TypeFilter, seen, false);
        }
    }
}
