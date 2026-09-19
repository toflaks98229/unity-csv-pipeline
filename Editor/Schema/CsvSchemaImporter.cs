using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// Importer that runs one <see cref="CsvSchema"/>. It is the body of the path where the user writes no code.
    /// </summary>
    public sealed class CsvSchemaImportDefinition : CsvImportDefinition
    {
        private readonly CsvSchema _schema;

        /// <summary>Builds an importer that runs a schema.</summary>
        /// <param name="schema">Schema to run.</param>
        public CsvSchemaImportDefinition(CsvSchema schema) { _schema = schema; }

        protected override string FileName => _schema.Declaration.FileName;
        protected override string OutputFolder => _schema.ResolveOutputFolder();
        protected override IEnumerable<string> RequiredColumns => _schema.RequiredColumns;
        protected override string LogTag => $"[{_schema.AssetType.Name}]";

        /// <summary>Bakes an asset per row and cleans up output assets that vanished from the table.</summary>
        /// <param name="table">Parsed table.</param>
        /// <param name="report">Report that records counts and problems.</param>
        /// <summary>Cleans up only when the declaration allows it. The declaration settles how to match.</summary>
        protected override CsvReconcileMode ReconcileMode
        {
            get
            {
                if (!_schema.Declaration.DeleteMissing) return CsvReconcileMode.None;

                return _schema.Declaration.ReconcileByPath
                    ? CsvReconcileMode.ByPath
                    : CsvReconcileMode.ByName;
            }
        }

        /// <summary>Search filter that finds the cleanup candidates.</summary>
        protected override string ReconcileTypeFilter => $"t:{_schema.AssetType.Name}";

        protected override void Process(CsvTable table, CsvImportReport report)
        {
            string folder = _schema.ResolveOutputFolder();
            if (string.IsNullOrEmpty(folder))
            {
                report.Error("Could not settle the output folder. Specify OutputFolder on [CsvAsset].");
                return;
            }

            CsvAssetPipeline.EnsureFolder(folder);

            // 표에 없는 열에 연결된 필드는 매 행마다 경고할 필요가 없어 한 번만 알립니다.
            WarnUnmatchedColumns(table, report);

            var binder = new CsvValueBinder();

            BakeEach(table.Rows, report, (row, rep) => BakeOne(row, table, folder, binder, rep));
        }

        /// <summary>Bakes one row into an asset.</summary>
        /// <param name="row">Row to read.</param>
        /// <param name="table">Whole table. Used to check that a column exists.</param>
        /// <param name="folder">Output folder.</param>
        /// <param name="binder">Value converter.</param>
        /// <param name="report">Report that records problems.</param>
        /// <returns>The bake result.</returns>
        private CsvBakeOutcome BakeOne(CsvRow row, CsvTable table, string folder,
                                       CsvValueBinder binder, CsvImportReport report)
        {
            string idColumn = _schema.Declaration.IdColumn;
            string id = row.GetString(idColumn);

            if (string.IsNullOrEmpty(id))
            {
                report.Warn($"'{idColumn}' is empty, skipping this row.", row.LineNumber, idColumn);
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
                report.Error($"Could not create the {_schema.AssetType.Name} asset: {path}", row.LineNumber);
                return CsvBakeOutcome.Skipped();
            }

            var serialized = new SerializedObject(asset);
            BakeRow(row, table, serialized, binder, report, asset);
            bool changed = serialized.ApplyModifiedPropertiesWithoutUndo();

            // 값이 하나도 달라지지 않았으면 더럽히지 않습니다. 더럽힌 에셋은 굽기 끝의
            // SaveAssets 가 전부 다시 씁니다 — 3,000행 표에서 한 칸만 고쳐도 3,000개를 다시
            // 쓰던 자리입니다. ApplyModifiedPropertiesWithoutUndo 는 실제로 바뀐 것이 있을 때만
            // true 를 돌려주므로, 그 답을 그대로 씁니다.
            if (created || changed)
            {
                CsvAssets.Current.MarkDirty(asset);
                CsvAssetPipeline.FlushIfCreated(asset, created);
            }

            return CsvBakeOutcome.Baked(created, id, path, row.LineNumber);
        }

        /// <summary>Writes every column of one row into the asset.</summary>
        /// <param name="row">Row to read.</param>
        /// <param name="table">Whole table. Used to check that a column exists.</param>
        /// <param name="serialized">Serialized object of the target asset.</param>
        /// <param name="binder">Value converter.</param>
        /// <param name="report">Report that records problems.</param>
        /// <param name="asset">Object a problem pings when clicked.</param>
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
        /// Computes what changes per row, <b>field by field</b>. It writes nothing.
        /// It bakes a copy with the real converter and then compares values, so the preview never drifts from the real result.
        /// </summary>
        /// <param name="table">Parsed table.</param>
        /// <param name="plan">Plan to fill in.</param>
        protected override void BuildPlan(CsvTable table, CsvImportPlan plan)
        {
            string folder = _schema.ResolveOutputFolder();
            string idColumn = _schema.Declaration.IdColumn;

            if (string.IsNullOrEmpty(folder))
            {
                plan.Unsupported = "Could not settle the output folder.";
                return;
            }

            var binder = new CsvValueBinder();
            HashSet<string> validNames = NewKeySet();
            HashSet<string> validPaths = NewKeySet();
            var claims = new CsvIdClaims();

            foreach (CsvRow row in table.Rows)
            {
                string id = row.GetString(idColumn);
                if (string.IsNullOrEmpty(id))
                {
                    plan.Add(CsvChangeKind.Skip, null, row.LineNumber, $"'{idColumn}' is empty.");
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
                validPaths.Add(path);

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
                bool byPath = _schema.Declaration.ReconcileByPath;

                PlanObsolete(plan, folder, $"t:{_schema.AssetType.Name}",
                             byPath ? (ICollection<string>)validPaths : validNames, byPath);
            }
        }

        /// <summary>
        /// Bakes into a copy and keeps only the fields that change. The original is never touched.
        /// </summary>
        /// <param name="row">Row to read.</param>
        /// <param name="table">Whole table. Used to check that a column exists.</param>
        /// <param name="existing">Existing asset to compare against.</param>
        /// <param name="binder">Value converter.</param>
        /// <param name="plan">Plan that records conversion failures.</param>
        /// <returns>The fields that change.</returns>
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
        /// The column name exactly as it is spelled in the table. Automatic binding takes the field name as the
        /// column name, but a person has to see the spelling they wrote (<c>MaxSpeed</c>).
        /// </summary>
        /// <param name="table">Source table.</param>
        /// <param name="binding">Column binding.</param>
        /// <returns>Column name to display.</returns>
        private static string Spelling(CsvTable table, CsvBinding binding)
            => table.ResolveHeader(binding.Column) ?? binding.Column;

        /// <summary>
        /// Reports once for every bound field that has no matching column in the table.
        /// <para>
        /// <b>It reports for automatic binding too.</b> It used to speak only for declarations tagged by hand, so with
        /// the default settings, misspelling <c>Title</c> as <c>Titel</c> went through <b>without a single warning</b> —
        /// that field was skipped, and a newly created asset got the C# default baked in. Three places in the docs
        /// said "it tells you when a column name is wrong", and it did not.
        /// </para>
        /// <para>
        /// It does <b>grade the severity</b>, though. A tag put on by hand asks for that column, so its absence is a
        /// mistake and the message is a warning. With automatic binding, a column not following along every time a
        /// field is added is normal, and warning at the same weight would only pile noise onto a healthy table — so it
        /// stays an info message. When a column with a nearly identical name exists, it is read as a typo and warns
        /// either way.
        /// </para>
        /// </summary>
        /// <param name="table">Table to inspect.</param>
        /// <param name="report">Report that records the results.</param>
        private void WarnUnmatchedColumns(CsvTable table, CsvImportReport report)
        {
            bool optIn = _schema.OptIn;

            foreach (CsvBinding binding in _schema.Bindings)
            {
                if (table.HasColumn(binding.Column)) continue;

                string similar = table.FindSimilarColumn(binding.Column);
                if (similar != null)
                {
                    // 대소문자 차이는 HasColumn 이 이미 흡수하므로 여기 걸리는 것은 언제나
                    // 공백·밑줄·하이픈 차이입니다. 원인을 바르게 말해야 사람이 고칠 자리를 찾습니다.
                    report.Warn($"Field '{binding.PropertyPath}' is looking for column '{binding.Column}', "
                              + $"but the table has '{similar}'. (differs by space, underscore or hyphen) "
                              + "This field is left untouched in this bake.");
                }
                else if (optIn)
                {
                    report.Warn($"Field '{binding.PropertyPath}' carries [CsvColumn], but the table has no "
                              + $"'{binding.Column}' column. This field is left untouched in this bake.");
                }
                else
                {
                    report.Info($"The table has no '{binding.Column}' column matching field '{binding.PropertyPath}'. "
                              + "This field is left untouched in this bake. "
                              + "(when the column name differs use [CsvColumn(\"ActualName\")], for a field you do not author in the table use [CsvIgnore])");
                }
            }
        }
    }

    /// <summary>
    /// Postprocessor that handles <b>every</b> type carrying <see cref="CsvAssetAttribute"/> in one place.
    /// Thanks to this class, adding another table never means writing another importer.
    /// </summary>
    public sealed class CsvAttributeImporter : AssetPostprocessor
    {
        /// <summary>Passes the batched asset import notification on to every schema.</summary>
        /// <param name="imported">Paths of the imported assets.</param>
        /// <param name="deleted">Paths of the deleted assets.</param>
        /// <param name="moved">New paths of the moved assets.</param>
        /// <param name="movedFrom">Previous paths of the moved assets.</param>
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
