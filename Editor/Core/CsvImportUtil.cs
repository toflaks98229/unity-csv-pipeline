using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// 고정 파일명 기반 CSV 임포터가 공유하는 라이프사이클 보일러플레이트 유틸입니다.
    /// (파일 매칭·터치 감지·행 읽기)
    /// </summary>
    public static class CsvImportUtil
    {
        /// <summary>경로의 파일명이 지정 파일명과 일치하는지(대소문자 무시) 여부입니다.</summary>
        /// <param name="path">검사할 에셋 경로입니다.</param>
        /// <param name="fileName">비교할 파일 이름입니다.</param>
        /// <returns>일치하면 true입니다.</returns>
        public static bool IsFile(string path, string fileName)
            => Path.GetFileName(path).Equals(fileName, StringComparison.OrdinalIgnoreCase);

        /// <summary>이번 임포트/이동 목록에 지정 CSV가 포함됐는지 여부입니다.</summary>
        /// <param name="imported">임포트된 에셋 경로들입니다.</param>
        /// <param name="moved">이동된 에셋의 새 경로들입니다.</param>
        /// <param name="fileName">찾을 파일 이름입니다.</param>
        /// <returns>포함됐으면 true입니다.</returns>
        public static bool Touched(string[] imported, string[] moved, string fileName)
            => imported.Concat(moved).Any(p => IsFile(p, fileName));

        /// <summary>CSV 에셋을 로드해 행 목록으로 파싱합니다. 로드 실패/빈 파일이면 null.</summary>
        /// <param name="csvPath">읽을 CSV 에셋 경로입니다.</param>
        /// <returns>파싱된 행 목록이거나, 읽을 것이 없으면 null입니다.</returns>
        public static List<Dictionary<string, object>> ReadRows(string csvPath)
        {
            TextAsset csv = AssetDatabase.LoadAssetAtPath<TextAsset>(csvPath);
            if (csv == null) return null;

            var rows = CsvReader.Read(csv);
            return (rows == null || rows.Count == 0) ? null : rows;
        }
    }
}
