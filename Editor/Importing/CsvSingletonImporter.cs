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
        /// <summary>대상 에셋 조회에 쓰는 AssetDatabase 검색 필터입니다.</summary>
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

            foreach (CsvRow row in table.Rows)
            {
                if (BakeRow(row, asset, serialized)) report.CountUpdated();
                else report.CountSkipped();
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        /// <summary>프로젝트에서 대상 에셋을 찾습니다. 없으면 오류로 보고하고 null입니다.</summary>
        /// <param name="report">결과를 기록할 리포트입니다.</param>
        /// <returns>찾은 에셋이거나 null입니다.</returns>
        private T FindSingle(CsvImportReport report)
        {
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets(TypeFilter))
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            // FindAssets의 순서는 보장되지 않습니다. 여럿일 때 매번 다른 에셋을 갱신하지 않도록 경로로 고정합니다.
            paths.Sort(System.StringComparer.Ordinal);

            var found = new List<T>();
            foreach (string path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) found.Add(asset);
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
    }
}
