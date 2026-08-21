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
        protected override string OutputFolder => _schema.ResolveOutputFolder();
        protected override IEnumerable<string> RequiredColumns => _schema.RequiredColumns;
        protected override string LogTag => $"[{_schema.AssetType.Name}]";

        /// <summary>행마다 에셋을 굽고, 표에서 사라진 산출물을 정리합니다.</summary>
        /// <param name="table">파싱된 표입니다.</param>
        /// <param name="report">건수와 문제를 기록할 리포트입니다.</param>
        /// <summary>선언이 정리를 허용할 때만, 에셋 이름으로 대조해 정리합니다.</summary>
        protected override CsvReconcileMode ReconcileMode
            => _schema.Declaration.DeleteMissing ? CsvReconcileMode.ByName : CsvReconcileMode.None;

        /// <summary>정리 대상을 찾을 검색 필터입니다.</summary>
        protected override string ReconcileTypeFilter => $"t:{_schema.AssetType.Name}";

        protected override void Process(CsvTable table, CsvImportReport report)
        {
            string folder = _schema.ResolveOutputFolder();
            if (string.IsNullOrEmpty(folder))
            {
                report.Error("산출물 폴더를 정하지 못했습니다. [CsvAsset]의 OutputFolder를 지정하십시오.");
                return;
            }

            CsvAssetPipeline.EnsureFolder(folder);

            // 표에 없는 열에 연결된 필드는 매 행마다 경고할 필요가 없어 한 번만 알립니다.
            WarnUnmatchedColumns(table, report);

            var binder = new CsvValueBinder();

            BakeEach(table.Rows, report, (row, rep) => BakeOne(row, table, folder, binder, rep));
        }

        /// <summary>행 하나를 에셋으로 굽습니다.</summary>
        /// <param name="row">읽을 행입니다.</param>
        /// <param name="table">표 전체입니다. 열 존재 확인에 씁니다.</param>
        /// <param name="folder">산출물 폴더입니다.</param>
        /// <param name="binder">값 변환기입니다.</param>
        /// <param name="report">문제를 기록할 리포트입니다.</param>
        /// <returns>구운 결과입니다.</returns>
        private CsvBakeOutcome BakeOne(CsvRow row, CsvTable table, string folder,
                                       CsvValueBinder binder, CsvImportReport report)
        {
            string idColumn = _schema.Declaration.IdColumn;
            string id = row.GetString(idColumn);

            if (string.IsNullOrEmpty(id))
            {
                report.Warn($"'{idColumn}'이(가) 비어 있어 건너뜁니다.", row.LineNumber, idColumn);
                return CsvBakeOutcome.Skipped();
            }

            string rejected = CsvAssetId.Reject(id);
            if (rejected != null)
            {
                report.Warn(CsvAssetId.Describe(id, rejected), row.LineNumber, idColumn);
                return CsvBakeOutcome.Skipped();
            }

            string path = $"{folder}/{id}.asset";
            ScriptableObject asset = CsvAssetPipeline.CreateOrLoad(_schema.AssetType, path, out bool created);
            if (asset == null)
            {
                report.Error($"{_schema.AssetType.Name} 에셋을 만들지 못했습니다: {path}", row.LineNumber);
                return CsvBakeOutcome.Skipped();
            }

            var serialized = new SerializedObject(asset);
            BakeRow(row, table, serialized, binder, report, asset);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            CsvAssets.Current.MarkDirty(asset);
            CsvAssetPipeline.FlushIfCreated(asset, created);

            return CsvBakeOutcome.Baked(created, id, path, row.LineNumber);
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
        /// 행마다 무엇이 달라지는지 <b>필드 단위로</b> 계산합니다. 아무것도 쓰지 않습니다.
        /// 사본을 만들어 실제 변환기로 구워 본 뒤 값을 비교하므로, 미리보기와 실제 결과가 어긋나지 않습니다.
        /// </summary>
        /// <param name="table">파싱된 표입니다.</param>
        /// <param name="plan">채울 계획입니다.</param>
        protected override void BuildPlan(CsvTable table, CsvImportPlan plan)
        {
            string folder = _schema.ResolveOutputFolder();
            string idColumn = _schema.Declaration.IdColumn;

            if (string.IsNullOrEmpty(folder))
            {
                plan.Unsupported = "산출물 폴더를 정하지 못했습니다.";
                return;
            }

            var binder = new CsvValueBinder();
            var validNames = new HashSet<string>();
            var claims = new CsvIdClaims();

            foreach (CsvRow row in table.Rows)
            {
                string id = row.GetString(idColumn);
                if (string.IsNullOrEmpty(id))
                {
                    plan.Add(CsvChangeKind.Skip, null, row.LineNumber, $"'{idColumn}'이(가) 비어 있습니다.");
                    continue;
                }

                string rejected = CsvAssetId.Reject(id);
                if (rejected != null)
                {
                    plan.Add(CsvChangeKind.Skip, null, row.LineNumber, CsvAssetId.Describe(id, rejected));
                    plan.Issues.Add(new CsvIssue(CsvIssueSeverity.Warning,
                                                 CsvAssetId.Describe(id, rejected), row.LineNumber, idColumn));
                    continue;
                }

                validNames.Add(id);
                string path = $"{folder}/{id}.asset";

                ClaimForPlan(claims, plan, path, id, row.LineNumber);

                var existing = CsvAssets.Current.Load(path, _schema.AssetType) as ScriptableObject;
                if (existing == null)
                {
                    plan.Add(CsvChangeKind.Create, path, row.LineNumber);
                    continue;
                }

                List<CsvFieldChange> changes = DiffRow(row, table, existing, binder, plan);

                // 값이 하나도 달라지지 않으면 굽더라도 결과가 같습니다. 목록에 올리지 않아야
                // "무엇이 실제로 바뀌는가"가 드러납니다.
                if (changes.Count == 0) continue;

                CsvPlannedChange planned = plan.Add(CsvChangeKind.Update, path, row.LineNumber);
                planned.Fields.AddRange(changes);
            }

            if (_schema.Declaration.DeleteMissing)
            {
                PlanObsolete(plan, folder, $"t:{_schema.AssetType.Name}", validNames, false);
            }
        }

        /// <summary>
        /// 사본에 구워 보고 달라지는 필드만 추립니다. 원본은 건드리지 않습니다.
        /// </summary>
        /// <param name="row">읽을 행입니다.</param>
        /// <param name="table">표 전체입니다. 열 존재 확인에 씁니다.</param>
        /// <param name="existing">비교 대상인 기존 에셋입니다.</param>
        /// <param name="binder">값 변환기입니다.</param>
        /// <param name="plan">변환 실패를 기록할 계획입니다.</param>
        /// <returns>달라지는 필드들입니다.</returns>
        private List<CsvFieldChange> DiffRow(CsvRow row, CsvTable table, ScriptableObject existing,
                                             CsvValueBinder binder, CsvImportPlan plan)
        {
            var changes = new List<CsvFieldChange>();
            ScriptableObject probe = Object.Instantiate(existing);

            try
            {
                var before = new SerializedObject(existing);
                var after = new SerializedObject(probe);

                foreach (CsvBinding binding in _schema.Bindings)
                {
                    if (!table.HasColumn(binding.Column)) continue;

                    SerializedProperty target = after.FindProperty(binding.PropertyPath);
                    if (target == null) continue;

                    string raw = row.GetString(binding.Column);
                    if (!binder.Apply(target, binding.FieldType, raw, binding, out string error) && error != null)
                    {
                        plan.Issues.Add(new CsvIssue(CsvIssueSeverity.Warning, error,
                                                     row.LineNumber, Spelling(table, binding)));
                    }
                }

                after.ApplyModifiedPropertiesWithoutUndo();

                foreach (CsvBinding binding in _schema.Bindings)
                {
                    if (!table.HasColumn(binding.Column)) continue;

                    string from = CsvValueFormatter.Format(before.FindProperty(binding.PropertyPath), binding.Separators);
                    string to = CsvValueFormatter.Format(after.FindProperty(binding.PropertyPath), binding.Separators);
                    if (from == to) continue;

                    changes.Add(new CsvFieldChange
                    {
                        Column = Spelling(table, binding),
                        Field = binding.PropertyPath,
                        From = from,
                        To = to
                    });
                }
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }

            return changes;
        }

        /// <summary>
        /// 표에 적힌 그대로의 열 이름입니다. 자동 연결은 필드 이름을 열 이름으로 삼는데,
        /// 사람에게는 자기가 적은 표기(<c>MaxSpeed</c>)를 보여 줘야 합니다.
        /// </summary>
        /// <param name="table">원본 표입니다.</param>
        /// <param name="binding">열 연결입니다.</param>
        /// <returns>표시할 열 이름입니다.</returns>
        private static string Spelling(CsvTable table, CsvBinding binding)
            => table.ResolveHeader(binding.Column) ?? binding.Column;

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
