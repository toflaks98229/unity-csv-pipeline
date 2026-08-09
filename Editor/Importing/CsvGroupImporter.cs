using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// <b>같은 식별자를 가진 여러 행 = 한 에셋</b> 임포터입니다. (표의 한 행이 에셋 안의 리스트 항목 하나인 경우)
    /// 그룹은 CSV에 처음 등장한 순서를 지킵니다.
    /// </summary>
    /// <typeparam name="T">구울 ScriptableObject 타입입니다.</typeparam>
    public abstract class CsvGroupImporter<T> : CsvImportDefinition where T : ScriptableObject
    {
        /// <summary>산출물이 놓이는 폴더입니다.</summary>
        protected abstract override string OutputFolder { get; }

        /// <summary>행에서 그룹 식별자(=파일명)를 뽑습니다. 비어 있으면 그 행을 건너뜁니다.</summary>
        /// <param name="row">읽을 행입니다.</param>
        /// <returns>그룹 식별자입니다.</returns>
        protected abstract string GetGroupId(CsvRow row);

        /// <summary>
        /// 한 그룹의 행들을 에셋에 기록합니다.
        /// <b>여기서는 에셋 필드에 직접 대입하십시오.</b> (<see cref="SerializedObject"/>를 쓰지 않는 경로입니다)
        /// </summary>
        /// <param name="groupId">그룹 식별자입니다.</param>
        /// <param name="rows">이 그룹에 속한 행들입니다. CSV 순서를 지킵니다.</param>
        /// <param name="asset">대상 에셋입니다.</param>
        protected abstract void Bake(string groupId, IReadOnlyList<CsvRow> rows, T asset);

        /// <summary>산출물 정리에 쓰는 AssetDatabase 검색 필터입니다.</summary>
        protected virtual string TypeFilter => $"t:{typeof(T).Name}";

        /// <summary>식별자로부터 에셋 경로를 만듭니다.</summary>
        /// <param name="groupId">그룹 식별자입니다.</param>
        /// <returns>에셋 경로입니다.</returns>
        protected virtual string AssetPathFor(string groupId) => $"{OutputFolder}/{groupId}.asset";

        /// <summary>행들을 식별자로 묶어 그룹마다 에셋을 굽고, 표에서 사라진 산출물을 정리합니다.</summary>
        /// <param name="table">파싱된 표입니다.</param>
        /// <param name="report">건수와 문제를 기록할 리포트입니다.</param>
        protected override void Process(CsvTable table, CsvImportReport report)
        {
            string folder = OutputFolder;
            CsvAssetPipeline.EnsureFolder(folder);

            var groups = new Dictionary<string, List<CsvRow>>();
            var order = new List<string>();

            foreach (CsvRow row in table.Rows)
            {
                string id = GetGroupId(row);
                if (string.IsNullOrEmpty(id))
                {
                    report.CountSkipped();
                    report.Warn("그룹 식별자가 비어 있어 건너뜁니다.", row.LineNumber);
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

            var validNames = new HashSet<string>();
            foreach (string id in order)
            {
                T asset = CsvAssetPipeline.CreateOrLoad<T>(AssetPathFor(id), out bool created);
                if (asset == null)
                {
                    report.CountSkipped();
                    continue;
                }

                Bake(id, groups[id], asset);
                EditorUtility.SetDirty(asset);
                CsvAssetPipeline.FlushIfCreated(asset, created);

                if (created) report.CountCreated();
                else report.CountUpdated();

                validNames.Add(id);
            }

            CsvAssetPipeline.ReconcileFolderByName(folder, TypeFilter, validNames, LogTag, report);
        }

        /// <summary>그룹마다 만들지 갱신할지, 그리고 무엇이 사라질지를 계산합니다. 쓰지는 않습니다.</summary>
        /// <param name="table">파싱된 표입니다.</param>
        /// <param name="plan">채울 계획입니다.</param>
        protected override void BuildPlan(CsvTable table, CsvImportPlan plan)
        {
            var seen = new HashSet<string>();

            foreach (CsvRow row in table.Rows)
            {
                string id = GetGroupId(row);
                if (string.IsNullOrEmpty(id))
                {
                    plan.Add(CsvChangeKind.Skip, null, row.LineNumber, "그룹 식별자가 비어 있습니다.");
                    continue;
                }

                // 같은 그룹의 두 번째 행부터는 에셋을 더 만들지 않습니다.
                if (!seen.Add(id)) continue;

                string path = AssetPathFor(id);
                bool exists = AssetDatabase.LoadAssetAtPath<T>(path) != null;
                plan.Add(exists ? CsvChangeKind.Update : CsvChangeKind.Create, path, row.LineNumber);
            }

            PlanObsolete(plan, OutputFolder, TypeFilter, seen, false);
        }
    }
}
