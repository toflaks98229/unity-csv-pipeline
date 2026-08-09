using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CsvPipeline
{
    /// <summary>
    /// 구분자로 나뉜 표 텍스트를 해석하는 공용 파서입니다.
    /// RFC 4180 스타일의 따옴표 필드(내부 구분자·개행·이스케이프된 "")·BOM·CRLF를 처리하며,
    /// 각 행을 헤더 기준으로 자동 타입 변환(int → float → string)해 돌려줍니다.
    /// </summary>
    public static class CsvReader
    {
        /// <summary>쉼표 구분자입니다. (.csv)</summary>
        public const char Comma = ',';

        /// <summary>탭 구분자입니다. (.tsv / .tab)</summary>
        public const char Tab = '\t';

        // ====================================================================================================
        // 진입점
        // ====================================================================================================

        /// <summary>텍스트 에셋을 행 목록으로 파싱합니다.</summary>
        /// <param name="csvAsset">분석할 텍스트 에셋입니다.</param>
        /// <returns>파싱된 행 데이터 목록입니다.</returns>
        public static List<Dictionary<string, object>> Read(TextAsset csvAsset)
        {
            if (csvAsset == null) return new List<Dictionary<string, object>>();
            return Read(csvAsset.text);
        }

        /// <summary>원본 문자열을 행 목록으로 파싱합니다. 구분자는 내용으로 판별합니다.</summary>
        /// <param name="text">분석할 원문입니다.</param>
        /// <returns>파싱된 행 데이터 목록입니다.</returns>
        public static List<Dictionary<string, object>> Read(string text) => Read(text, DetectDelimiter(text));

        /// <summary>원본 문자열을 지정 구분자로 파싱해 행 목록을 돌려줍니다.</summary>
        /// <param name="text">분석할 원문입니다.</param>
        /// <param name="delimiter">필드 구분자입니다.</param>
        /// <returns>파싱된 행 데이터 목록입니다.</returns>
        public static List<Dictionary<string, object>> Read(string text, char delimiter)
        {
            Parse(text, delimiter, out _, out List<Dictionary<string, object>> cells, out _);
            return cells;
        }

        /// <summary>원문을 헤더까지 갖춘 표로 파싱합니다. 구분자는 내용으로 판별합니다.</summary>
        /// <param name="text">분석할 원문입니다.</param>
        /// <returns>파싱된 표입니다.</returns>
        public static CsvTable ReadTable(string text) => ReadTable(text, DetectDelimiter(text));

        /// <summary>원문을 지정 구분자로 파싱해 헤더까지 갖춘 표로 돌려줍니다.</summary>
        /// <param name="text">분석할 원문입니다.</param>
        /// <param name="delimiter">필드 구분자입니다.</param>
        /// <returns>파싱된 표입니다. 내용이 없으면 빈 표입니다.</returns>
        public static CsvTable ReadTable(string text, char delimiter)
        {
            Parse(text, delimiter, out List<string> header,
                  out List<Dictionary<string, object>> cells, out List<int> lines);

            var rows = new List<CsvRow>(cells.Count);
            for (int i = 0; i < cells.Count; i++) rows.Add(new CsvRow(cells[i], lines[i], header));

            return new CsvTable(header, rows);
        }

        /// <summary>
        /// 파싱의 단일 경로입니다. 두 진입점이 같은 결과를 내도록 여기 하나만 둡니다.
        /// </summary>
        /// <param name="text">분석할 원문입니다.</param>
        /// <param name="delimiter">필드 구분자입니다.</param>
        /// <param name="header">헤더 목록을 받습니다.</param>
        /// <param name="cells">행별 헤더-값 사전을 받습니다. 값은 추론된 타입(int/float/string)입니다.</param>
        /// <param name="lines">행별 원본 줄 번호를 받습니다. <paramref name="cells"/>와 같은 순서입니다.</param>
        private static void Parse(string text, char delimiter, out List<string> header,
                                  out List<Dictionary<string, object>> cells, out List<int> lines)
        {
            header = new List<string>();
            cells = new List<Dictionary<string, object>>();
            lines = new List<int>();
            if (string.IsNullOrEmpty(text)) return;

            List<Record> records = ParseRecords(text, delimiter);
            if (records.Count == 0) return;

            header = records[0].Fields;

            for (int r = 1; r < records.Count; r++)
            {
                List<string> values = records[r].Fields;

                // 첫 열이 비어 있는 행은 건너뜁니다. (구분자만 있는 빈 줄 방어)
                if (values.Count == 0 || string.IsNullOrEmpty(values[0])) continue;

                var entry = new Dictionary<string, object>();
                for (int j = 0; j < header.Count && j < values.Count; j++)
                {
                    entry[header[j]] = InferType(values[j]);
                }
                cells.Add(entry);
                lines.Add(records[r].LineNumber);
            }
        }

        // ====================================================================================================
        // 구분자 판별
        // ====================================================================================================

        /// <summary>
        /// 파일 확장자로 구분자를 정합니다. <c>.tsv</c>/<c>.tab</c>은 탭, 그 밖에는 쉼표입니다.
        /// </summary>
        /// <param name="path">파일 경로입니다.</param>
        /// <returns>쓸 구분자입니다.</returns>
        public static char DelimiterForPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return Comma;

            string ext = Path.GetExtension(path);
            return ext.Equals(".tsv", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".tab", StringComparison.OrdinalIgnoreCase)
                ? Tab
                : Comma;
        }

        /// <summary>
        /// 헤더 줄에서 구분자를 추정합니다. 탭이 쉼표보다 많으면 탭입니다.
        /// 확장자를 알 수 있으면 <see cref="DelimiterForPath"/>를 쓰는 편이 확실합니다.
        /// </summary>
        /// <param name="text">분석할 원문입니다.</param>
        /// <returns>추정한 구분자입니다.</returns>
        public static char DetectDelimiter(string text)
        {
            if (string.IsNullOrEmpty(text)) return Comma;

            int commas = 0, tabs = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n' || c == '\r') break;      // 헤더 줄만 봅니다.
                if (c == Comma) commas++;
                else if (c == Tab) tabs++;
            }
            return tabs > commas ? Tab : Comma;
        }

        // ====================================================================================================
        // Parsing
        // ====================================================================================================

        /// <summary>파싱된 레코드 하나입니다. 원본 줄 번호를 함께 들고 있습니다.</summary>
        private struct Record
        {
            public List<string> Fields;
            public int LineNumber;
        }

        /// <summary>
        /// 전체 텍스트를 레코드(행) 목록으로 분해합니다. 따옴표로 감싼 필드 내부의 구분자와 개행은 보존됩니다.
        /// </summary>
        /// <param name="text">분해할 원문입니다.</param>
        /// <param name="delimiter">필드 구분자입니다.</param>
        /// <returns>행마다 필드 목록과 시작 줄 번호를 담은 목록입니다.</returns>
        private static List<Record> ParseRecords(string text, char delimiter)
        {
            var records = new List<Record>();
            var current = new List<string>();
            var field = new StringBuilder();

            bool inQuotes = false;
            bool fieldStarted = false;  // 이 행에서 필드 파싱이 시작됐는지 (완전 빈 마지막 줄 무시용)
            int line = 1;               // 지금 읽고 있는 물리적 줄
            int recordLine = 1;         // 지금 모으는 레코드가 시작된 줄

            // BOM 제거
            int start = 0;
            if (text.Length > 0 && text[0] == '﻿') start = 1;

            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // 이스케이프된 따옴표("")는 리터럴 " 로, 아니면 따옴표 종료
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        // 따옴표 안의 개행은 필드 내용이지만 줄 번호는 계속 셉니다.
                        if (c == '\n') line++;
                        field.Append(c);
                    }
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = true;
                    fieldStarted = true;
                }
                else if (c == delimiter)
                {
                    current.Add(field.ToString());
                    field.Clear();
                    fieldStarted = true;
                }
                else if (c == '\r')
                {
                    // CRLF/CR 처리: 레코드 종료 (뒤따르는 \n은 아래 케이스가 흡수하지 않도록 스킵)
                    EndRecord(records, ref current, field, ref fieldStarted, recordLine);
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    line++;
                    recordLine = line;
                }
                else if (c == '\n')
                {
                    EndRecord(records, ref current, field, ref fieldStarted, recordLine);
                    line++;
                    recordLine = line;
                }
                else
                {
                    field.Append(c);
                    fieldStarted = true;
                }
            }

            // 마지막 필드/레코드 마무리 (파일이 개행으로 끝나지 않은 경우)
            if (fieldStarted || field.Length > 0 || current.Count > 0)
            {
                current.Add(field.ToString());
                records.Add(new Record { Fields = current, LineNumber = recordLine });
            }

            return records;
        }

        /// <summary>진행 중인 필드를 확정하고 레코드를 records에 추가한 뒤 상태를 초기화합니다.</summary>
        /// <param name="records">레코드를 모으는 목록입니다.</param>
        /// <param name="current">진행 중인 행의 필드 목록입니다.</param>
        /// <param name="field">진행 중인 필드 버퍼입니다.</param>
        /// <param name="fieldStarted">이 행에서 필드 파싱이 시작됐는지 여부입니다.</param>
        /// <param name="lineNumber">이 레코드가 시작된 줄 번호입니다.</param>
        private static void EndRecord(List<Record> records, ref List<string> current, StringBuilder field,
                                      ref bool fieldStarted, int lineNumber)
        {
            current.Add(field.ToString());
            field.Clear();
            records.Add(new Record { Fields = current, LineNumber = lineNumber });
            current = new List<string>();
            fieldStarted = false;
        }

        /// <summary>문자열 셀을 int → float → string 순으로 자동 타입 변환합니다.</summary>
        /// <param name="value">변환할 셀 원문입니다.</param>
        /// <returns>추론된 타입의 값입니다.</returns>
        private static object InferType(string value)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) return n;
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) return f;
            return value;
        }
    }
}
