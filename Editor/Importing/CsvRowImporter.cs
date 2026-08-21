using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// <b>한 행 = 한 에셋</b> 임포터입니다. 행의 식별자를 파일명으로 삼아 에셋을 만들거나 갱신하고,
    /// 표에서 사라진 행의 에셋을 정리합니다.
    /// </summary>
    /// <typeparam name="T">구울 ScriptableObject 타입입니다.</typeparam>
    public abstract class CsvRowImporter<T> : CsvImportDefinition where T : ScriptableObject
    {
        /// <summary>산출물이 놓이는 폴더입니다.</summary>
        protected abstract override string OutputFolder { get; }

        /// <summary>행에서 에셋 식별자(=파일명)를 뽑습니다. 비어 있으면 그 행을 건너뜁니다.</summary>
        /// <param name="row">읽을 행입니다.</param>
        /// <returns>식별자입니다.</returns>
        protected abstract string GetId(CsvRow row);

        /// <summary>
        /// 행의 값을 에셋에 기록합니다. <paramref name="serialized"/>에 <see cref="SoBaker"/>로 쓰십시오.
        /// </summary>
        /// <param name="row">읽을 행입니다.</param>
        /// <param name="asset">대상 에셋입니다.</param>
        /// <param name="serialized">대상 에셋의 직렬화 객체입니다. 호출 뒤 자동으로 적용됩니다.</param>
        protected abstract void Bake(CsvRow row, T asset, SerializedObject serialized);

        /// <summary>산출물 정리에 쓰는 에셋 검색 필터입니다.</summary>
        protected virtual string TypeFilter => $"t:{typeof(T).Name}";

        /// <summary>
        /// 정리 대조를 에셋 이름이 아니라 경로로 할지 여부입니다.
        /// 파생 타입별로 하위 폴더가 갈리는 등 이름만으로 대조할 수 없을 때 켭니다.
        /// </summary>
        protected virtual bool ReconcileByPath => false;

        /// <summary>표에서 사라진 산출물을 정리합니다. 대조 방식은 <see cref="ReconcileByPath"/>가 정합니다.</summary>
        protected sealed override CsvReconcileMode ReconcileMode
            => ReconcileByPath ? CsvReconcileMode.ByPath : CsvReconcileMode.ByName;

        /// <summary>정리 대상을 찾을 검색 필터입니다.</summary>
        protected sealed override string ReconcileTypeFilter => TypeFilter;

        /// <summary>식별자로부터 에셋 경로를 만듭니다.</summary>
        /// <param name="id">행의 식별자입니다.</param>
        /// <returns>에셋 경로입니다.</returns>
        protected virtual string AssetPathFor(string id) => $"{OutputFolder}/{id}.asset";

        /// <summary>
        /// 에셋을 로드하거나 만듭니다. 행의 값에 따라 만들 구체 타입이 갈릴 때 재정의하십시오.
        /// </summary>
        /// <param name="id">행의 식별자입니다.</param>
        /// <param name="row">읽을 행입니다.</param>
        /// <returns>로드하거나 만든 에셋입니다. null이면 그 행을 건너뜁니다.</returns>
        protected virtual T CreateOrLoad(string id, CsvRow row)
            => CsvAssetPipeline.CreateOrLoad<T>(AssetPathFor(id));

        /// <summary>행마다 에셋을 굽고, 표에서 사라진 산출물을 정리합니다.</summary>
        /// <param name="table">파싱된 표입니다.</param>
        /// <param name="report">건수와 문제를 기록할 리포트입니다.</param>
        protected override void Process(CsvTable table, CsvImportReport report)
        {
            CsvAssetPipeline.EnsureFolder(OutputFolder);

            BakeEach(table.Rows, report, BakeOne);
        }

        /// <summary>행 하나를 에셋으로 굽습니다.</summary>
        /// <param name="row">읽을 행입니다.</param>
        /// <param name="report">문제를 기록할 리포트입니다.</param>
        /// <returns>구운 결과입니다.</returns>
        private CsvBakeOutcome BakeOne(CsvRow row, CsvImportReport report)
        {
            string id = GetId(row);
            if (string.IsNullOrEmpty(id))
            {
                report.Warn("식별자가 비어 있어 건너뜁니다.", row.LineNumber);
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
            serialized.ApplyModifiedPropertiesWithoutUndo();
            CsvAssets.Current.MarkDirty(asset);
            CsvAssetPipeline.FlushIfCreated(asset, isNew);

            return CsvBakeOutcome.Baked(isNew, id, CsvAssets.Current.PathOf(asset), row.LineNumber);
        }

        /// <summary>행마다 만들지 갱신할지, 그리고 무엇이 사라질지를 계산합니다. 쓰지는 않습니다.</summary>
        /// <param name="table">파싱된 표입니다.</param>
        /// <param name="plan">채울 계획입니다.</param>
        protected override void BuildPlan(CsvTable table, CsvImportPlan plan)
        {
            var validNames = new HashSet<string>();
            var validPaths = new HashSet<string>();
            var claims = new CsvIdClaims();

            foreach (CsvRow row in table.Rows)
            {
                string id = GetId(row);
                if (string.IsNullOrEmpty(id))
                {
                    plan.Add(CsvChangeKind.Skip, null, row.LineNumber, "식별자가 비어 있습니다.");
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
