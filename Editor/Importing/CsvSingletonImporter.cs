using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// <b>표 전체 = 프로젝트에 하나뿐인 에셋</b> 임포터입니다. (등급표·밸런싱 표처럼 산출물이 단일 에셋인 경우)
    /// 에셋을 새로 만들지 않습니다. 새로 만들면 GUID가 다른 빈 에셋이 생기고,
    /// 씬·프리팹의 인스펙터 배선은 옛 에셋을 계속 가리켜 조용히 어긋나기 때문입니다.
    /// </summary>
    /// <typeparam name="T">갱신할 ScriptableObject 타입입니다.</typeparam>
    public abstract class CsvSingletonImporter<T> : CsvImportDefinition where T : ScriptableObject
    {
        /// <summary>대상 에셋 조회에 쓰는 에셋 검색 필터입니다.</summary>
        protected virtual string TypeFilter => $"t:{typeof(T).Name}";

        /// <summary>대상 에셋이 없을 때 경고에 덧붙일 안내입니다. (어디서 만들 수 있는지)</summary>
        protected virtual string MissingAssetHint => null;

        /// <summary>
        /// 한 행의 값을 단일 에셋에 기록합니다.
        /// </summary>
        /// <param name="row">읽을 행입니다.</param>
        /// <param name="asset">대상 에셋입니다.</param>
        /// <param name="serialized">대상 에셋의 직렬화 객체입니다. 전 행 처리 후 한 번에 적용됩니다.</param>
        /// <returns>이 행을 실제로 반영했으면 true입니다. (로그의 적용 행 수 집계용)</returns>
        protected abstract bool BakeRow(CsvRow row, T asset, SerializedObject serialized);

        /// <summary>단일 에셋을 찾아 전 행을 반영합니다.</summary>
        /// <param name="table">파싱된 표입니다.</param>
        /// <param name="report">건수와 문제를 기록할 리포트입니다.</param>
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
            serialized.ApplyModifiedPropertiesWithoutUndo();
            CsvAssets.Current.MarkDirty(asset);
        }

        /// <summary>
        /// 어느 에셋이 갱신될지 계산합니다. 이 임포터는 만들지도 지우지도 않습니다.
        /// </summary>
        /// <param name="table">파싱된 표입니다.</param>
        /// <param name="plan">채울 계획입니다.</param>
        protected override void BuildPlan(CsvTable table, CsvImportPlan plan)
        {
            List<string> paths = SortedPaths();

            if (paths.Count == 0)
            {
                string hint = MissingAssetHint;
                plan.Issues.Add(new CsvIssue(CsvIssueSeverity.Error,
                    $"{typeof(T).Name} 에셋이 없습니다. 이 표는 에셋을 새로 만들지 않습니다."
                    + (string.IsNullOrEmpty(hint) ? string.Empty : $" {hint}")));
                plan.Unsupported = "갱신할 에셋이 없습니다.";
                return;
            }

            if (paths.Count > 1)
            {
                plan.Issues.Add(new CsvIssue(CsvIssueSeverity.Warning,
                    $"{typeof(T).Name} 에셋이 {paths.Count}개입니다. 경로순 첫 번째만 갱신합니다."));
            }

            plan.Add(CsvChangeKind.Update, paths[0], 0, $"표 {table.Count}행이 이 에셋 하나에 반영됩니다.");
        }

        /// <summary>프로젝트에서 대상 에셋을 찾습니다. 없으면 오류로 보고하고 null입니다.</summary>
        /// <param name="report">결과를 기록할 리포트입니다.</param>
        /// <returns>찾은 에셋이거나 null입니다.</returns>
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
                    $"{typeof(T).Name} 에셋을 찾지 못해 표를 반영하지 못했습니다. "
                    + "이 표의 산출물은 프로젝트에 하나뿐인 에셋이라 임포터가 새로 만들지 않습니다."
                    + (string.IsNullOrEmpty(hint) ? string.Empty : $" {hint}"));
                return null;
            }

            if (found.Count > 1)
            {
                // 갱신되지 않은 쪽은 낡은 값으로 남아, 어느 것을 참조하느냐에 따라 결과가 갈립니다.
                report.Warn(
                    $"{typeof(T).Name} 에셋이 {found.Count}개 있습니다. 경로순 첫 번째만 갱신합니다. "
                    + "이런 표는 게임 전역의 규칙이므로 하나만 두십시오.", 0, null, found[0]);
            }

            return found[0];
        }

        /// <summary>
        /// 대상 타입의 에셋 경로들을 경로순으로 돌려줍니다.
        /// 검색 순서는 보장되지 않아, 여럿일 때 매번 다른 에셋을 갱신하지 않도록 정렬로 고정합니다.
        /// </summary>
        /// <returns>정렬된 에셋 경로들입니다.</returns>
        private List<string> SortedPaths()
        {
            var paths = new List<string>(CsvAssets.Current.FindPaths(TypeFilter));
            paths.Sort(System.StringComparer.Ordinal);
            return paths;
        }
    }
}
