using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// <see cref="CsvAssetAttribute"/>로 선언된 에셋들을 다시 표로 뽑아냅니다.
    /// 에디터에서 손본 값을 표에 되돌리는 <b>왕복</b>의 나머지 절반입니다.
    /// </summary>
    public static class CsvExporter
    {
        /// <summary>로그 접두 태그입니다.</summary>
        private const string TAG = "[CsvExport]";

        // ====================================================================================================
        // 메뉴
        // ====================================================================================================

        /// <summary>선언된 모든 표를 에셋에서 다시 뽑아 원본 파일에 씁니다. 바뀐 것만 확인 후 기록합니다.</summary>
        [MenuItem("Tools/CSV Pipeline/에셋을 표로 내보내기", false, 21)]
        public static void ExportAllMenu()
        {
            IReadOnlyList<CsvSchema> schemas = CsvSchema.All();
            if (schemas.Count == 0)
            {
                EditorUtility.DisplayDialog("내보낼 표가 없습니다",
                    "[CsvAsset] 특성이 붙은 ScriptableObject 타입이 없습니다.\n\n"
                    + "직접 작성한 임포터는 표의 구조를 코드로만 알고 있어 자동으로 되돌릴 수 없습니다.", "확인");
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

                if (text == null) { failed.Add($"{fileName} — 산출물 폴더를 찾지 못했습니다"); continue; }

                string path = ResolveTargetPath(fileName);
                string existing = File.Exists(path) ? Normalize(File.ReadAllText(path)) : null;

                if (existing == Normalize(text)) { same.Add(fileName); continue; }

                changed.Add(fileName);
                preview.AppendLine($"  {fileName} — {rowCount}행"
                                 + (existing == null ? " (새 파일)" : " (내용 다름)"));
            }

            if (changed.Count == 0)
            {
                string message = failed.Count > 0
                    ? $"쓸 것이 없습니다. 동일 {same.Count} / 실패 {failed.Count}\n\n" + string.Join("\n", failed)
                    : $"모든 표가 에셋과 일치합니다. ({same.Count}개)";
                EditorUtility.DisplayDialog("내보내기", message, "확인");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "표를 덮어씁니다",
                $"아래 {changed.Count}개 파일을 에셋 내용으로 덮어씁니다.\n\n{preview}\n"
                + "표를 저작 원본으로 쓰고 있었다면 표 쪽 편집분이 사라집니다.\n"
                + "(git으로 되돌릴 수 있습니다)",
                "덮어쓰기", "취소");

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
                    File.WriteAllText(path, Normalize(text), new UTF8Encoding(false));
                    written++;
                }

                AssetDatabase.Refresh();
            }

            Debug.Log($"{TAG} {written}개 표를 에셋 내용으로 갱신했습니다. (동일 {same.Count} / 실패 {failed.Count})");
        }

        // ====================================================================================================
        // 생성
        // ====================================================================================================

        /// <summary>
        /// 스키마가 가리키는 폴더의 에셋들을 표 텍스트로 만듭니다.
        /// </summary>
        /// <param name="schema">내보낼 스키마입니다.</param>
        /// <param name="rowCount">쓴 행 수를 받습니다.</param>
        /// <returns>표 원문이거나, 폴더가 없으면 null입니다.</returns>
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

            List<string> headers = BuildHeaders(schema, source);
            var writer = new CsvWriter(CsvReader.DelimiterForPath(schema.Declaration.FileName));
            writer.WriteRow(headers);

            // 경로순으로 내보내야 매번 같은 파일이 나와 git 잡음이 생기지 않습니다.
            var paths = new List<string>(assets.FindPaths($"t:{schema.AssetType.Name}", folder));
            paths.Sort(StringComparer.Ordinal);

            var cells = new List<string>(headers.Count);
            foreach (string path in paths)
            {
                if (!(assets.Load(path, schema.AssetType) is ScriptableObject asset)) continue;

                var serialized = new SerializedObject(asset);
                cells.Clear();
                cells.Add(Path.GetFileNameWithoutExtension(path));   // 식별자 열 = 에셋 이름

                foreach (CsvBinding binding in schema.Bindings)
                {
                    if (string.Equals(binding.Column, schema.Declaration.IdColumn, StringComparison.OrdinalIgnoreCase)) continue;
                    cells.Add(CsvValueFormatter.Format(serialized.FindProperty(binding.PropertyPath), binding.Separators));
                }

                writer.WriteRow(cells);
                rowCount++;
            }

            return writer.ToString();
        }

        /// <summary>식별자 열을 맨 앞에 두고 나머지 열을 이어 붙인 헤더입니다.</summary>
        /// <param name="schema">대상 스키마입니다.</param>
        /// <param name="source">원본 표입니다. 있으면 그 헤더 표기를 씁니다.</param>
        /// <returns>헤더 목록입니다.</returns>
        private static List<string> BuildHeaders(CsvSchema schema, CsvTable source)
        {
            var headers = new List<string> { Spelling(schema.Declaration.IdColumn, source) };

            foreach (CsvBinding binding in schema.Bindings)
            {
                if (string.Equals(binding.Column, schema.Declaration.IdColumn, StringComparison.OrdinalIgnoreCase)) continue;
                headers.Add(Spelling(binding.Column, source));
            }
            return headers;
        }

        /// <summary>원본 표에 적힌 표기를 쓰고, 없으면 선언된 이름을 그대로 씁니다.</summary>
        /// <param name="column">열 이름입니다.</param>
        /// <param name="source">원본 표입니다.</param>
        /// <returns>쓸 헤더 표기입니다.</returns>
        private static string Spelling(string column, CsvTable source)
            => source?.ResolveHeader(column) ?? column;

        /// <summary>내보낼 파일의 실제 경로를 정합니다. 기존 파일이 있으면 그 자리에 씁니다.</summary>
        /// <param name="fileName">표 파일 이름입니다.</param>
        /// <returns>쓸 경로입니다.</returns>
        private static string ResolveTargetPath(string fileName)
        {
            string existing = CsvAssetPipeline.FindCsvPath(fileName);
            if (existing != null) return Path.GetFullPath(existing);

            return Path.GetFullPath(Path.Combine(CsvPipelineSettings.Instance.CsvRootFolder, fileName));
        }

        /// <summary>줄 끝을 LF로 통일하고 BOM을 제거합니다. (비교와 기록 형식을 한 가지로 맞춤)</summary>
        /// <param name="text">정규화할 문자열입니다.</param>
        /// <returns>정규화된 문자열입니다.</returns>
        private static string Normalize(string text)
            => text.TrimStart('﻿').Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd() + "\n";
    }
}
