using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// <b>이미 있는 에셋만 갱신하는</b> 임포터입니다. 에셋을 새로 만들지도, 사라진 행의 에셋을 지우지도 않습니다.
    /// CSV가 에셋의 일부 필드만 소유하고 나머지는 수작업으로 저작하는 표에 씁니다.
    /// </summary>
    /// <typeparam name="T">갱신할 ScriptableObject 타입입니다.</typeparam>
    public abstract class CsvPatchImporter<T> : CsvImportDefinition where T : ScriptableObject
    {
        /// <summary>행에서 대상 에셋을 찾을 키를 뽑습니다. 비어 있으면 그 행을 건너뜁니다.</summary>
        /// <param name="row">읽을 행입니다.</param>
        /// <returns>조회 키입니다.</returns>
        protected abstract string GetRowKey(CsvRow row);

        /// <summary>에셋에서 조회 키를 뽑습니다. 비어 있는 에셋은 색인에서 빠집니다.</summary>
        /// <param name="asset">색인할 에셋입니다.</param>
        /// <returns>조회 키입니다.</returns>
        protected abstract string GetAssetKey(T asset);

        /// <summary>행의 값을 기존 에셋에 기록합니다.</summary>
        /// <param name="row">읽을 행입니다.</param>
        /// <param name="asset">대상 에셋입니다.</param>
        /// <param name="serialized">대상 에셋의 직렬화 객체입니다. 호출 뒤 자동으로 적용됩니다.</param>
        protected abstract void Patch(CsvRow row, T asset, SerializedObject serialized);

        /// <summary>에셋 색인에 쓰는 AssetDatabase 검색 필터입니다.</summary>
        protected virtual string TypeFilter => $"t:{typeof(T).Name}";

        /// <summary>키에 해당하는 에셋이 없을 때 경고에 덧붙일 안내입니다.</summary>
        protected virtual string MissingAssetHint => null;

        /// <summary>키로 기존 에셋을 찾아 행의 값을 반영합니다.</summary>
        /// <param name="rows">파싱된 CSV 행들입니다.</param>
        /// <returns>에셋을 하나라도 건드렸으면 true입니다.</returns>
        protected override bool Process(IReadOnlyList<CsvRow> rows)
        {
            Dictionary<string, T> byKey = BuildIndex();
            int applied = 0;

            foreach (CsvRow row in rows)
            {
                string key = GetRowKey(row);
                if (string.IsNullOrEmpty(key)) continue;

                if (!byKey.TryGetValue(key, out T asset))
                {
                    string hint = MissingAssetHint;
                    Debug.LogWarning(
                        $"{LogTag} '{key}'에 해당하는 {typeof(T).Name}를 찾지 못했습니다."
                        + (string.IsNullOrEmpty(hint) ? string.Empty : $" {hint}"));
                    continue;
                }

                var serialized = new SerializedObject(asset);
                Patch(row, asset, serialized);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                applied++;
            }

            return applied > 0;
        }

        /// <summary>프로젝트의 T 에셋을 조회 키로 색인합니다.</summary>
        /// <returns>키 → 에셋 사전입니다.</returns>
        private Dictionary<string, T> BuildIndex()
        {
            var map = new Dictionary<string, T>();

            foreach (string guid in AssetDatabase.FindAssets(TypeFilter))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;

                string key = GetAssetKey(asset);
                if (!string.IsNullOrEmpty(key)) map[key] = asset;
            }
            return map;
        }
    }
}
