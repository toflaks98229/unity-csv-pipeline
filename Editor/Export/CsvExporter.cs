using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// Builds tables back out of the assets declared with <see cref="CsvAssetAttribute"/>.
    /// This is the other half of the <b>round trip</b> that returns values edited in the editor to the table.
    /// </summary>
    public static class CsvExporter
    {
        /// <summary>Log prefix tag.</summary>
        private const string TAG = "[CsvExport]";

        // ====================================================================================================
        // 메뉴
        // ====================================================================================================

        /// <summary>Rebuilds every declared table from its assets and writes it to the source file. Only what changed is written, after confirmation.</summary>
        [MenuItem("Tools/CSV Pipeline/Export Assets to Tables", false, 21)]
        public static void ExportAllMenu()
        {
            IReadOnlyList<CsvSchema> schemas = CsvSchema.All();
            if (schemas.Count == 0)
            {
                EditorUtility.DisplayDialog("No tables to export",
                    "No ScriptableObject type carries the [CsvAsset] attribute.\n\n"
                    + "An importer you wrote yourself knows the table structure only in code, so it cannot be reversed automatically.", "OK");
                return;
            }

            var changed = new List<string>();
            var same = new List<string>();
            var failed = new List<string>();
            var preview = new StringBuilder();

            foreach (CsvSchema schema in schemas)
            {
                string fileName = schema.Declaration.FileName;
                string text = Build(schema, out int rowCount);

                if (text == null) { failed.Add($"{fileName} — output folder not found"); continue; }

                string path = ResolveTargetPath(fileName);
                string existing = File.Exists(path) ? Normalize(File.ReadAllText(path)) : null;

                if (existing == Normalize(text)) { same.Add(fileName); continue; }

                changed.Add(fileName);
                preview.AppendLine($"  {fileName} — {rowCount} rows"
                                 + (existing == null ? " (new file)" : " (contents differ)"));
            }

            if (changed.Count == 0)
            {
                string message = failed.Count > 0
                    ? $"Nothing to write. {same.Count} identical / {failed.Count} failed\n\n" + string.Join("\n", failed)
                    : $"Every table matches its assets. ({same.Count})";
                EditorUtility.DisplayDialog("Export", message, "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Overwriting tables",
                $"The {changed.Count} files below get overwritten with the asset contents.\n\n{preview}\n"
                + "If you were using the tables as the authoring source, edits made on the table side are lost.\n"
                + "(git can undo this)",
                "Overwrite", "Cancel");

            if (!proceed) return;

            int written = 0;

            // 방금 쓴 표를 Refresh가 다시 임포트하면, 그 표를 만들어 낸 바로 그 에셋들을 되굽습니다.
            // 결과는 같지만 순전한 낭비이고 로그도 두 배가 되므로 쓰는 동안 자동 임포트를 멈춥니다.
            using (CsvImport.Suppress())
            {
                foreach (CsvSchema schema in schemas)
                {
                    if (!changed.Contains(schema.Declaration.FileName)) continue;

                    string text = Build(schema, out _);
                    if (text == null) continue;

                    string path = ResolveTargetPath(schema.Declaration.FileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                    File.WriteAllText(path, Normalize(text), CsvPipelineSettings.TableEncoding);
                    written++;
                }

                AssetDatabase.Refresh();
            }

            Debug.Log($"{TAG} Updated {written} tables with the asset contents. ({same.Count} identical / {failed.Count} failed)");
        }

        // ====================================================================================================
        // 생성
        // ====================================================================================================

        /// <summary>
        /// Turns the assets in the folder the schema points at into table text.
        /// </summary>
        /// <param name="schema">Schema to export.</param>
        /// <param name="rowCount">Receives the number of rows written.</param>
        /// <returns>The table text, or null when the folder does not exist.</returns>
        public static string Build(CsvSchema schema, out int rowCount)
        {
            rowCount = 0;
            ICsvAssetGateway assets = CsvAssets.Current;

            string folder = schema.ResolveOutputFolder();
            if (!assets.FolderExists(folder)) return null;

            // 원본이 있으면 그 헤더 표기를 그대로 씁니다. 그러지 않으면 MaxSpeed 가 maxSpeed 로 바뀌어
            // 내용이 같아도 매번 "다름"으로 잡히고, 시트와 헤더가 어긋나 동기화가 멈춥니다.
            string sourcePath = CsvAssetPipeline.FindCsvPath(schema.Declaration.FileName);
            CsvTable source = sourcePath == null ? null : CsvImportUtil.ReadTable(sourcePath);

            // 경로순으로 내보내야 매번 같은 파일이 나와 git 잡음이 생기지 않습니다.
            var paths = new List<string>(assets.FindPaths($"t:{schema.AssetType.Name}", folder));
            paths.Sort(StringComparer.Ordinal);

            List<CsvBinding> columns = AuthorableColumns(schema, paths, assets);
            List<string> carried = CarriedColumns(schema, source, columns);

            List<string> headers = BuildHeaders(schema, source, columns, carried);
            var writer = new CsvWriter(CsvReader.DelimiterForPath(schema.Declaration.FileName));
            writer.WriteRow(headers);

            Dictionary<string, CsvRow> sourceRows = IndexSource(schema, source, carried.Count > 0);

            var cells = new List<string>(headers.Count);
            foreach (string path in paths)
            {
                if (!(assets.Load(path, schema.AssetType) is ScriptableObject asset)) continue;

                var serialized = new SerializedObject(asset);
                string id = Path.GetFileNameWithoutExtension(path);

                cells.Clear();
                cells.Add(id);   // 식별자 열 = 에셋 이름

                foreach (CsvBinding binding in columns)
                {
                    cells.Add(CsvValueFormatter.Format(serialized.FindProperty(binding.PropertyPath), binding.Separators));
                }

                // 굽기가 읽지 않는 열은 에셋에 값이 없습니다. 원본 표에 적혀 있던 것을 그대로 옮깁니다.
                if (carried.Count > 0)
                {
                    sourceRows.TryGetValue(id, out CsvRow row);
                    foreach (string column in carried) cells.Add(row.Has(column) ? row.GetString(column) : string.Empty);
                }

                writer.WriteRow(cells);
                rowCount++;
            }

            return writer.ToString();
        }

        /// <summary>
        /// Picks only the columns that really go into the table and <b>can be read back</b>. (Identifier column excluded.)
        /// <para>
        /// <see cref="CsvTemplate"/> already applies this judgement when it builds a table — the reason being that
        /// "hand out the column and someone fills it on the sheet, and the moment it is pulled back warnings pour out
        /// row after row". Export did not apply that judgement, so it exported fields like <c>List&lt;NestedClass&gt;</c>
        /// as empty strings, and with several elements a cell holding nothing but <c>;;</c> got written into the source
        /// table. Bake that cell again and the whole list empties out. <b>Making both places call the same function</b>
        /// removes that divergence.
        /// </para>
        /// </summary>
        /// <param name="schema">Target schema.</param>
        /// <param name="paths">Output paths whose values are inspected.</param>
        /// <param name="assets">Asset gateway.</param>
        /// <returns>Bindings of the columns to export.</returns>
        private static List<CsvBinding> AuthorableColumns(CsvSchema schema, List<string> paths, ICsvAssetGateway assets)
        {
            var columns = new List<CsvBinding>(schema.Bindings.Count);

            // 프로퍼티 종류를 보려면 에셋 하나가 필요합니다. 하나도 없으면 판정할 근거가 없으므로
            // 선언을 그대로 믿습니다 — 쓸 행도 없어서 헤더만 나갑니다.
            SerializedObject sample = null;
            foreach (string path in paths)
            {
                if (assets.Load(path, schema.AssetType) is ScriptableObject asset)
                {
                    sample = new SerializedObject(asset);
                    break;
                }
            }

            foreach (CsvBinding binding in schema.Bindings)
            {
                if (string.Equals(binding.Column, schema.Declaration.IdColumn, StringComparison.OrdinalIgnoreCase)) continue;

                if (sample != null)
                {
                    SerializedProperty property = sample.FindProperty(binding.PropertyPath);
                    if (property == null || CsvTemplate.Unauthorable(property) != null) continue;
                }
                columns.Add(binding);
            }
            return columns;
        }

        /// <summary>
        /// Columns that exist only in the source table and that baking never reads. <b>They are carried over and preserved.</b>
        /// <para>
        /// Rewrite the header with only the columns in the declaration and what a person wrote into the table —
        /// a notes column, or the column of a <c>[CsvIgnore]</c> field — vanishes with one export. This package itself
        /// describes "a table whose column A is notes and whose column B is Id" as a normal shape, so erasing that
        /// column is the side that is wrong.
        /// </para>
        /// </summary>
        /// <param name="schema">Target schema.</param>
        /// <param name="source">Source table. With none, there is nothing to carry over.</param>
        /// <param name="columns">Columns already being exported.</param>
        /// <returns>Names of the columns to carry over, in the order the table writes them.</returns>
        private static List<string> CarriedColumns(CsvSchema schema, CsvTable source, List<CsvBinding> columns)
        {
            var carried = new List<string>();
            if (source == null) return carried;

            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { schema.Declaration.IdColumn };
            foreach (CsvBinding binding in columns) known.Add(binding.Column);

            foreach (string header in source.Headers)
            {
                if (string.IsNullOrEmpty(header) || !known.Add(header)) continue;
                carried.Add(header);
            }
            return carried;
        }

        /// <summary>Indexes the source table rows so they can be found by identifier.</summary>
        /// <param name="schema">Target schema.</param>
        /// <param name="source">Source table.</param>
        /// <param name="needed">Whether there are columns to carry over, so this is actually needed.</param>
        /// <returns>Identifier to row dictionary.</returns>
        private static Dictionary<string, CsvRow> IndexSource(CsvSchema schema, CsvTable source, bool needed)
        {
            var index = new Dictionary<string, CsvRow>(StringComparer.OrdinalIgnoreCase);
            if (!needed || source == null) return index;

            foreach (CsvRow row in source.Rows)
            {
                string id = row.GetString(schema.Declaration.IdColumn);
                if (!string.IsNullOrEmpty(id)) index[id] = row;
            }
            return index;
        }

        /// <summary>Header with the identifier column first, then the exported columns and the carried columns in turn.</summary>
        /// <param name="schema">Target schema.</param>
        /// <param name="source">Source table. When there is one, its header spelling is used.</param>
        /// <param name="columns">Columns to export.</param>
        /// <param name="carried">Columns carried over from the source as they are.</param>
        /// <returns>The header list.</returns>
        private static List<string> BuildHeaders(CsvSchema schema, CsvTable source,
                                                 List<CsvBinding> columns, List<string> carried)
        {
            var headers = new List<string> { Spelling(schema.Declaration.IdColumn, source) };

            foreach (CsvBinding binding in columns) headers.Add(Spelling(binding.Column, source));
            headers.AddRange(carried);

            return headers;
        }

        /// <summary>Uses the spelling written in the source table, or the declared name when there is none.</summary>
        /// <param name="column">Column name.</param>
        /// <param name="source">Source table.</param>
        /// <returns>Header spelling to write.</returns>
        private static string Spelling(string column, CsvTable source)
            => source?.ResolveHeader(column) ?? column;

        /// <summary>Decides the real path of the file to export. When a file already exists, it is written in place.</summary>
        /// <param name="fileName">Table file name.</param>
        /// <returns>Path to write to.</returns>
        private static string ResolveTargetPath(string fileName)
        {
            string existing = CsvAssetPipeline.FindCsvPath(fileName);
            if (existing != null) return Path.GetFullPath(existing);

            return Path.GetFullPath(Path.Combine(CsvPipelineSettings.Instance.CsvRootFolder, fileName));
        }

        /// <summary>Makes every line ending LF and strips the BOM. (Comparison and written form settle on one shape.)</summary>
        /// <param name="text">String to normalize.</param>
        /// <returns>The normalized string.</returns>
        private static string Normalize(string text)
            => text.TrimStart('﻿').Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd() + "\n";
    }
}
