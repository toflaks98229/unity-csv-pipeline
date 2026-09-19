using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CsvPipeline
{
    /// <summary>
    /// The shared parser for delimiter-separated table text.
    /// It handles RFC 4180 style quoted fields (embedded delimiters, newlines, escaped ""), BOMs, and CRLF.
    /// <para>
    /// <b>Cells keep their raw text.</b> The parser never decides a type from the value — the target
    /// field does. If the parser decided first, <c>007</c> would come back as <c>7</c> and <c>1.10</c>
    /// as <c>1.1</c>, quietly corrupting string columns.
    /// </para>
    /// </summary>
    public static class CsvReader
    {
        /// <summary>The comma delimiter. (.csv)</summary>
        public const char Comma = ',';

        /// <summary>The tab delimiter. (.tsv / .tab)</summary>
        public const char Tab = '\t';

        // ====================================================================================================
        // 진입점
        // ====================================================================================================

        /// <summary>Parses a text asset into a list of rows.</summary>
        /// <param name="csvAsset">Text asset to parse.</param>
        /// <returns>List of parsed row data.</returns>
        public static List<Dictionary<string, object>> Read(TextAsset csvAsset)
        {
            if (csvAsset == null) return new List<Dictionary<string, object>>();
            return Read(csvAsset.text);
        }

        /// <summary>Parses raw text into a list of rows. The delimiter is detected from the content.</summary>
        /// <param name="text">Raw text to parse.</param>
        /// <returns>List of parsed row data.</returns>
        public static List<Dictionary<string, object>> Read(string text) => Read(text, DetectDelimiter(text));

        /// <summary>Parses raw text with the given delimiter and returns the list of rows.</summary>
        /// <param name="text">Raw text to parse.</param>
        /// <param name="delimiter">Field delimiter.</param>
        /// <returns>List of parsed row data.</returns>
        public static List<Dictionary<string, object>> Read(string text, char delimiter)
        {
            // 이 경로는 예전부터 쓰던 것이라 대소문자 구분도, 값에 따른 타입 추론도 그대로 둡니다.
            // 부르는 쪽이 boxed int/float를 꺼내 쓰고 있을 수 있어, 동작을 조용히 바꾸지 않습니다.
            Parse(text, delimiter, StringComparer.Ordinal, inferTypes: true,
                  out _, out List<Dictionary<string, object>> cells, out _, out _);
            return cells;
        }

        /// <summary>Parses raw text into a table complete with headers. The delimiter is detected from the content.</summary>
        /// <param name="text">Raw text to parse.</param>
        /// <returns>Parsed table.</returns>
        public static CsvTable ReadTable(string text) => ReadTable(text, DetectDelimiter(text));

        /// <summary>Parses raw text with the given delimiter and returns a table complete with headers.</summary>
        /// <param name="text">Raw text to parse.</param>
        /// <param name="delimiter">Field delimiter.</param>
        /// <returns>Parsed table. Empty table when there is no content.</returns>
        public static CsvTable ReadTable(string text, char delimiter)
        {
            // 표 경로는 헤더를 대소문자 없이 찾습니다. MaxSpeed 열과 maxSpeed 필드가 붙어야 하기 때문입니다.
            // 값은 추론하지 않습니다 — 타입은 대상 필드가 정합니다.
            Parse(text, delimiter, StringComparer.OrdinalIgnoreCase, inferTypes: false, out List<string> header,
                  out List<Dictionary<string, object>> cells, out List<int> lines, out int openQuoteLine);

            var rows = new List<CsvRow>(cells.Count);
            for (int i = 0; i < cells.Count; i++) rows.Add(new CsvRow(cells[i], lines[i], header));

            string defect = openQuoteLine > 0
                ? $"The double quote opened on line {openQuoteLine} is never closed.\n"
                + "  Everything after it falls into a single cell, so the remaining rows disappear from the table. "
                + "Rows that disappear are not even counted as 'skipped', so they leave no trace — nothing was baked.\n"
                + $"  Close or delete the quote on line {openQuoteLine}. "
                + "To put a double quote inside a cell, write it twice (\"\")."
                : null;

            return new CsvTable(header, rows, defect);
        }

        /// <summary>
        /// The single parsing path. It lives here alone so that both entry points produce the same result.
        /// </summary>
        /// <param name="text">Raw text to parse.</param>
        /// <param name="delimiter">Field delimiter.</param>
        /// <param name="comparer">Header name comparer used to look cells up.</param>
        /// <param name="inferTypes">
        /// Whether to decide a type from the value. <b>Only the legacy <see cref="Read(string)"/> path passes true.</b>
        /// </param>
        /// <param name="header">Receives the header list.</param>
        /// <param name="cells">Receives one header-to-value dictionary per row. Values are the raw cell text (string).</param>
        /// <param name="lines">Receives the source line number of each row, in the same order as <paramref name="cells"/>.</param>
        /// <param name="openQuoteLine">
        /// Receives the line where an unclosed double quote started. Zero when there is none.
        /// </param>
        private static void Parse(string text, char delimiter, StringComparer comparer, bool inferTypes,
                                  out List<string> header,
                                  out List<Dictionary<string, object>> cells, out List<int> lines,
                                  out int openQuoteLine)
        {
            header = new List<string>();
            cells = new List<Dictionary<string, object>>();
            lines = new List<int>();
            openQuoteLine = 0;
            if (string.IsNullOrEmpty(text)) return;

            List<Record> records = ParseRecords(text, delimiter, out openQuoteLine);
            if (records.Count == 0) return;

            header = records[0].Fields;

            for (int r = 1; r < records.Count; r++)
            {
                List<string> values = records[r].Fields;

                // 모든 셀이 빈 행만 건너뜁니다. (구분자만 있는 빈 줄 방어)
                // 첫 열로 판정하면, 식별자가 뒷 열에 있고 앞 열이 메모 칸인 표에서 그 칸이 빈 행이
                // 통째로 사라집니다. 파서 단계에서 없어지므로 굽기 쪽은 그 행의 존재조차 모르고,
                // 건너뜀으로도 세어지지 않아 어디에도 흔적이 남지 않습니다.
                if (IsBlankRecord(values)) continue;

                var entry = new Dictionary<string, object>(comparer);
                for (int j = 0; j < header.Count && j < values.Count; j++)
                {
                    entry[header[j]] = inferTypes ? InferType(values[j]) : values[j];
                }
                cells.Add(entry);
                lines.Add(records[r].LineNumber);
            }
        }

        /// <summary>
        /// Checks whether a record has no filled cell at all. A single cell with data makes it a row.
        /// </summary>
        /// <param name="values">Fields of the record.</param>
        /// <returns>True when it is an empty line that can be dropped.</returns>
        private static bool IsBlankRecord(List<string> values)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (!string.IsNullOrEmpty(values[i])) return false;
            }
            return true;
        }

        // ====================================================================================================
        // 구분자 판별
        // ====================================================================================================

        /// <summary>
        /// Picks the delimiter from the file extension. <c>.tsv</c>/<c>.tab</c> mean tab, everything else comma.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <returns>Delimiter to use.</returns>
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
        /// Guesses the delimiter from the header line. More tabs than commas means tab.
        /// When the extension is known, <see cref="DelimiterForPath"/> is the reliable choice.
        /// </summary>
        /// <param name="text">Raw text to inspect.</param>
        /// <returns>Guessed delimiter.</returns>
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

        /// <summary>One parsed record. It carries the source line number along with the fields.</summary>
        private struct Record
        {
            public List<string> Fields;
            public int LineNumber;
        }

        /// <summary>
        /// Breaks the whole text into records (rows). Delimiters and newlines inside quoted fields are preserved.
        /// </summary>
        /// <param name="text">Raw text to break up.</param>
        /// <param name="delimiter">Field delimiter.</param>
        /// <returns>One entry per row, holding its field list and starting line number.</returns>
        private static List<Record> ParseRecords(string text, char delimiter) => ParseRecords(text, delimiter, out _);

        /// <summary>
        /// Breaks the whole text into records and also reports <b>whether it ended with an unclosed quote</b>.
        /// </summary>
        /// <param name="text">Raw text to break up.</param>
        /// <param name="delimiter">Field delimiter.</param>
        /// <param name="unterminatedQuoteLine">
        /// Receives the line where an unclosed quote started. Zero when there is none.
        /// </param>
        /// <returns>One entry per row, holding its field list and starting line number.</returns>
        private static List<Record> ParseRecords(string text, char delimiter, out int unterminatedQuoteLine)
        {
            var records = new List<Record>();
            var current = new List<string>();
            var field = new StringBuilder();

            bool inQuotes = false;
            bool fieldStarted = false;  // 이 행에서 필드 파싱이 시작됐는지 (완전 빈 마지막 줄 무시용)
            int line = 1;               // 지금 읽고 있는 물리적 줄
            int recordLine = 1;         // 지금 모으는 레코드가 시작된 줄
            int quoteOpenedLine = 0;    // 지금 열려 있는 따옴표가 시작된 줄

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
                    quoteOpenedLine = line;
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

            unterminatedQuoteLine = inQuotes ? quoteOpenedLine : 0;
            return records;
        }

        /// <summary>Closes the field in progress, appends the record to records, and resets the state.</summary>
        /// <param name="records">List that collects the records.</param>
        /// <param name="current">Field list of the row in progress.</param>
        /// <param name="field">Buffer of the field in progress.</param>
        /// <param name="fieldStarted">Whether field parsing has started on this row.</param>
        /// <param name="lineNumber">Line where this record started.</param>
        private static void EndRecord(List<Record> records, ref List<string> current, StringBuilder field,
                                      ref bool fieldStarted, int lineNumber)
        {
            current.Add(field.ToString());
            field.Clear();
            records.Add(new Record { Fields = current, LineNumber = lineNumber });
            current = new List<string>();
            fieldStarted = false;
        }

        /// <summary>
        /// Converts a string cell automatically, trying int, then float, then string.
        /// <para>
        /// <b>For the legacy <see cref="Read(string)"/> path only.</b> This conversion loses data
        /// irreversibly — <c>007</c> becomes <c>7</c>, <c>1.10</c> becomes <c>1.1</c>, and a 20-digit
        /// identifier becomes <c>1E+20</c>. That is why the table path does not use it.
        /// </para>
        /// </summary>
        /// <param name="value">Raw cell text to convert.</param>
        /// <returns>Value in the inferred type.</returns>
        private static object InferType(string value)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) return n;
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) return f;
            return value;
        }
    }
}
