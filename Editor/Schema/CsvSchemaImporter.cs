using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// <see cref="CsvSchema"/> 하나를 실행하는 임포터입니다. 사용자가 코드를 쓰지 않는 경로의 본체입니다.
    /// </summary>
    public sealed class CsvSchemaImportDefinition : CsvImportDefinition
    {
        private readonly CsvSchema _schema;

        /// <summary>스키마를 실행할 임포터를 만듭니다.</summary>
        /// <param name="schema">실행할 스키마입니다.</param>
        public CsvSchemaImportDefinition(CsvSchema schema) { _schema = schema; }

        protected override string FileName => _schema.Declaration.FileName;
        protected override string OutputFolder => _schema.Declaration.OutputFolder;
        protected override IEnumerable<string> RequiredColumns => _schema.RequiredColumns;
        protected override string LogTag => $"[{_schema.AssetType.Name}]";

        /// <summary>행마다 에셋을 굽고, 표에서 사라진 산출물을 정리합니다.</summary>
        /// <param name="table">파싱된 표입니다.</param>
        /// <param name="report">건수와 문제를 기록할 리포트입니다.</param>
        protected override void Process(CsvTable table, CsvImportReport report)
        {
            string folder = _schema.Declaration.OutputFolder;
            string idColumn = _schema.Declaration.IdColumn;
            CsvAssetPipeline.EnsureFolder(folder);

            var binder = new CsvValueBinder();
            var validNames = new HashSet<string>();

            // 표에 없는 열에 연결된 필드는 매 행마다 경고할 필요가 없어 한 번만 알립니다.
            WarnUnmatchedColumns(table, report);

            foreach (CsvRow row in table.Rows)
            {
                string id = row.GetString(idColumn);
                if (string.IsNullOrEmpty(id))
                {
                    report.CountSkipped();
                    report.Warn($"'{idColumn}'이(가) 비어 있어 건너뜁니다.", row.LineNumber, idColumn);
                    continue;
                }

                string path = $"{folder}/{id}.asset";
                ScriptableObject asset = CsvAssetPipeline.CreateOrLoad(_schema.AssetType, path, out bool created);
                if (asset == null)
                {
                    report.CountSkipped();
                    report.Error($"{_schema.AssetType.Name} 에셋을 만들지 못했습니다: {path}", row.LineNumber);
                    continue;
                }

                var serialized = new SerializedObject(asset);
                BakeRow(row, table, serialized, binder, report, asset);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);

                if (created) report.CountCreated();
                else report.CountUpdated();

                validNames.Add(id);
            }

            if (_schema.Declaration.DeleteMissing)
            {
                CsvAssetPipeline.ReconcileFolderByName(
                    folder, $"t:{_schema.AssetType.Name}", validNames, LogTag, report);
            }
        }

        /// <summary>행 하나의 모든 열을 에셋에 씁니다.</summary>
        /// <param name="row">읽을 행입니다.</param>
        /// <param name="table">표 전체입니다. 열 존재 확인에 씁니다.</param>
        /// <param name="serialized">대상 에셋의 직렬화 객체입니다.</param>
        /// <param name="binder">값 변환기입니다.</param>
        /// <param name="report">문제를 기록할 리포트입니다.</param>
        /// <param name="asset">문제를 클릭해 찾아갈 대상입니다.</param>
        private void BakeRow(CsvRow row, CsvTable table, SerializedObject serialized,
                             CsvValueBinder binder, CsvImportReport report, Object asset)
        {
            foreach (CsvBinding binding in _schema.Bindings)
            {
                if (!table.HasColumn(binding.Column)) continue;   // 표에 없는 열은 조용히 넘깁니다. (위에서 한 번 알림)

                SerializedProperty property = serialized.FindProperty(binding.PropertyPath);
                if (property == null) continue;                    // 직렬화되지 않는 필드입니다.

                string raw = row.GetString(binding.Column);
                if (binder.Apply(property, binding.FieldType, raw, binding, out string error)) continue;

                if (error != null)
                {
                    report.Warn($"{error}", row.LineNumber, binding.Column, asset);
                }
            }
        }

        /// <summary>
        /// 연결된 필드 중 표에 대응 열이 없는 것들을 한 번만 알립니다.
        /// 이름이 거의 같은 열(공백·밑줄 차이)이 있으면 오타로 보고 함께 알립니다.
        /// 대소문자 차이는 매칭이 이미 흡수하므로 여기에 걸리지 않습니다.
        /// </summary>
        /// <param name="table">검사할 표입니다.</param>
        /// <param name="report">결과를 기록할 리포트입니다.</param>
        private void WarnUnmatchedColumns(CsvTable table, CsvImportReport report)
        {
            foreach (CsvBinding binding in _schema.Bindings)
            {
                if (table.HasColumn(binding.Column)) continue;

                string similar = table.FindSimilarColumn(binding.Column);
                if (similar != null)
                {
                    report.Warn($"필드 '{binding.PropertyPath}'는 열 '{binding.Column}'을 찾고 있는데 "
                              + $"표에는 '{similar}'가 있습니다. 대소문자가 다릅니다.");
                }
            }
        }
    }

    /// <summary>
    /// <see cref="CsvAssetAttribute"/>가 붙은 <b>모든</b> 타입을 한 자리에서 처리하는 프로세서입니다.
    /// 이 클래스 덕분에 표를 하나 늘려도 새 임포터를 쓸 필요가 없습니다.
    /// </summary>
    public sealed class CsvAttributeImporter : AssetPostprocessor
    {
        /// <summary>에셋 임포트 일괄 통지를 모든 스키마에 전달합니다.</summary>
        /// <param name="imported">임포트된 에셋 경로들입니다.</param>
        /// <param name="deleted">삭제된 에셋 경로들입니다.</param>
        /// <param name="moved">이동된 에셋의 새 경로들입니다.</param>
        /// <param name="movedFrom">이동된 에셋의 이전 경로들입니다.</param>
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            IReadOnlyList<CsvSchema> schemas = CsvSchema.All();
            if (schemas.Count == 0) return;

            foreach (CsvSchema schema in schemas)
            {
                new CsvSchemaImportDefinition(schema).Execute(imported, deleted, moved);
            }
        }
    }
}
