using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// 고정 파일명 기반 임포터가 공유하는 라이프사이클 보일러플레이트 유틸입니다.
    /// (파일 매칭·터치 감지·읽기)
    /// </summary>
    public static class CsvImportUtil
    {
        /// <summary>표로 다루는 확장자들입니다.</summary>
        public static readonly string[] TableExtensions = { ".csv", ".tsv", ".tab" };

        /// <summary>경로의 파일명이 지정 파일명과 일치하는지(대소문자 무시) 여부입니다.</summary>
        /// <param name="path">검사할 에셋 경로입니다.</param>
        /// <param name="fileName">비교할 파일 이름입니다.</param>
        /// <returns>일치하면 true입니다.</returns>
        public static bool IsFile(string path, string fileName)
            => Path.GetFileName(path).Equals(fileName, StringComparison.OrdinalIgnoreCase);

        /// <summary>이번 임포트/이동 목록에 지정 파일이 포함됐는지 여부입니다.</summary>
        /// <param name="imported">임포트된 에셋 경로들입니다.</param>
        /// <param name="moved">이동된 에셋의 새 경로들입니다.</param>
        /// <param name="fileName">찾을 파일 이름입니다.</param>
        /// <returns>포함됐으면 true입니다.</returns>
        public static bool Touched(string[] imported, string[] moved, string fileName)
            => imported.Concat(moved).Any(p => IsFile(p, fileName));

        /// <summary>경로의 확장자가 표로 다루는 것인지 여부입니다.</summary>
        /// <param name="path">검사할 경로입니다.</param>
        /// <returns>표 확장자면 true입니다.</returns>
        public static bool IsTableFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            string ext = Path.GetExtension(path);
            foreach (string candidate in TableExtensions)
            {
                if (ext.Equals(candidate, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// 표 파일의 원문을 읽습니다.
        /// TextAsset으로 먼저 읽고, 실패하면 디스크에서 직접 읽습니다.
        /// Unity가 <c>.tsv</c>를 TextAsset으로 임포트하지 않기 때문에 폴백이 필요합니다.
        /// </summary>
        /// <param name="assetPath">읽을 에셋 경로입니다.</param>
        /// <returns>원문이거나, 읽지 못했으면 null입니다.</returns>
        public static string ReadText(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;

            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (asset != null) return asset.text;

            try
            {
                string full = Path.GetFullPath(assetPath);
                return File.Exists(full) ? File.ReadAllText(full) : null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        /// <summary>표 파일을 읽어 헤더까지 갖춘 표로 파싱합니다. 구분자는 확장자로 정합니다.</summary>
        /// <param name="assetPath">읽을 에셋 경로입니다.</param>
        /// <returns>파싱된 표이거나, 읽을 것이 없으면 null입니다.</returns>
        public static CsvTable ReadTable(string assetPath)
        {
            string text = ReadText(assetPath);
            if (string.IsNullOrEmpty(text)) return null;

            CsvTable table = CsvReader.ReadTable(text, CsvReader.DelimiterForPath(assetPath));
            return table.Count == 0 ? null : table;
        }

        /// <summary>표 파일을 읽어 행 목록으로 파싱합니다. 로드 실패/빈 파일이면 null.</summary>
        /// <param name="csvPath">읽을 에셋 경로입니다.</param>
        /// <returns>파싱된 행 목록이거나, 읽을 것이 없으면 null입니다.</returns>
        public static List<Dictionary<string, object>> ReadRows(string csvPath)
        {
            string text = ReadText(csvPath);
            if (string.IsNullOrEmpty(text)) return null;

            var rows = CsvReader.Read(text, CsvReader.DelimiterForPath(csvPath));
            return (rows == null || rows.Count == 0) ? null : rows;
        }
    }
}
