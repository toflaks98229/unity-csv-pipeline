using System;
using System.Collections.Generic;
using System.Text;

namespace CsvPipeline
{
    /// <summary>
    /// Describes the difference between two raw table texts in a way a human can read.
    /// It holds <b>only pure functions that touch no file, no network, and no Unity API</b>.
    /// That is what makes it testable as it is.
    /// </summary>
    public static class SheetDiff
    {
        /// <summary>How many names the difference list spells out before it trims.</summary>
        private const int MaxShown = 8;

        /// <summary>
        /// Describes the difference between two raw table texts. <b>Null when they are the same.</b>
        /// </summary>
        /// <param name="local">Local table content.</param>
        /// <param name="sheet">Content pulled from the sheet.</param>
        /// <returns>The description of the difference, or null when they are the same.</returns>
        public static string Describe(string local, string sheet)
        {
            if (local == sheet) return null;

            var text = new StringBuilder();
            string localHeader = FirstLine(local);
            string sheetHeader = FirstLine(sheet);

            if (localHeader != sheetHeader)
            {
                // 헤더가 다르면 받기가 확인을 묻고 막아 줍니다. 열 구성이 바뀐 것인지,
                // 엉뚱한 탭을 가리키는 것인지는 사람이 봐야 알 수 있습니다.
                text.AppendLine("      Headers differ (the pull asks for confirmation)");
                text.AppendLine($"        Local: {localHeader}");
                text.AppendLine($"        Sheet: {sheetHeader}");
                return text.ToString();
            }

            Dictionary<string, string> localRows = IndexByFirstField(local);
            Dictionary<string, string> sheetRows = IndexByFirstField(sheet);

            var onlyLocal = new List<string>();
            var onlySheet = new List<string>();
            var changed = new List<string>();

            foreach (KeyValuePair<string, string> pair in localRows)
            {
                if (!sheetRows.TryGetValue(pair.Key, out string sheetRow)) onlyLocal.Add(pair.Key);
                else if (sheetRow != pair.Value) changed.Add(pair.Key);
            }
            foreach (string key in sheetRows.Keys)
            {
                if (!localRows.ContainsKey(key)) onlySheet.Add(key);
            }

            // 헤더가 같으면 받기가 아무 경고 없이 덮습니다. 그래서 여기서 분명히 알려야 합니다.
            text.AppendLine("      Headers match but the content differs (a pull overwrites without warning)");

            if (onlyLocal.Count > 0) text.AppendLine($"        Rows only in local {onlyLocal.Count}: {Join(onlyLocal)}");
            if (onlySheet.Count > 0) text.AppendLine($"        Rows only in sheet {onlySheet.Count}: {Join(onlySheet)}");
            if (changed.Count > 0) text.AppendLine($"        Rows with different values {changed.Count}: {Join(changed)}");

            // 첫 열이 비어 있거나 겹치면 행 대조가 성립하지 않습니다. 그때는 줄 수만 알립니다.
            // 색인 크기가 아니라 실제 줄 수를 씁니다. 색인은 겹친 식별자를 접어 버려,
            // 이 갈래가 열리는 상황에서는 바로 그 숫자가 양쪽 다 같게 나옵니다.
            if (onlyLocal.Count == 0 && onlySheet.Count == 0 && changed.Count == 0)
            {
                text.AppendLine($"        (Matching row by row found no difference. The table has duplicate or empty identifiers. "
                              + $"Line count local {CountDataLines(local)} / sheet {CountDataLines(sheet)})");
            }

            return text.ToString();
        }

        /// <summary>
        /// Normalizes line endings to LF and strips the BOM.
        /// This makes the comparison and the write see the same shape, so a table is not reported as
        /// "changed" when only its line ending characters differ.
        /// </summary>
        /// <param name="text">Text to normalize.</param>
        /// <returns>The normalized text.</returns>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "\n";

            return text.TrimStart('﻿').Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd() + "\n";
        }

        /// <summary>The first line, the header.</summary>
        /// <param name="text">Text to read.</param>
        /// <returns>The first line.</returns>
        public static string FirstLine(string text)
        {
            if (text == null) return string.Empty;

            int index = text.IndexOf('\n');
            return index < 0 ? text : text.Substring(0, index);
        }

        /// <summary>The number of real data lines, excluding the header and blank lines.</summary>
        /// <param name="text">Whole table content.</param>
        /// <returns>Number of data lines.</returns>
        public static int CountDataLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            string[] lines = text.Split('\n');
            int count = 0;

            for (int i = 1; i < lines.Length; i++)   // 0번은 헤더
            {
                if (!string.IsNullOrWhiteSpace(lines[i])) count++;
            }
            return count;
        }

        /// <summary>Indexes every line except the header by its first column, the identifier.</summary>
        /// <param name="text">Whole table content.</param>
        /// <returns>A dictionary of identifier to the whole line.</returns>
        public static Dictionary<string, string> IndexByFirstField(string text)
        {
            var map = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(text)) return map;

            string[] lines = text.Split('\n');

            for (int i = 1; i < lines.Length; i++)   // 0번은 헤더
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                string key = FirstField(line);
                if (string.IsNullOrEmpty(key)) key = $"(empty identifier, row {i})";

                // 식별자가 겹치면 뒤엣것이 이깁니다. (임포터도 같은 순서로 덮어씁니다)
                map[key] = line;
            }

            return map;
        }

        /// <summary>Extracts the first column value from one line. It understands double-quoted fields.</summary>
        /// <param name="line">One table line.</param>
        /// <returns>The first column value.</returns>
        public static string FirstField(string line)
        {
            if (string.IsNullOrEmpty(line)) return string.Empty;

            if (line[0] != '"')
            {
                int comma = line.IndexOf(',');
                return (comma < 0 ? line : line.Substring(0, comma)).Trim();
            }

            var buffer = new StringBuilder();
            for (int i = 1; i < line.Length; i++)
            {
                if (line[i] != '"') { buffer.Append(line[i]); continue; }

                // 이스케이프된 따옴표("")는 리터럴 " 로, 아니면 필드 종료입니다.
                if (i + 1 < line.Length && line[i + 1] == '"') { buffer.Append('"'); i++; continue; }
                break;
            }

            return buffer.ToString().Trim();
        }

        /// <summary>Joins a list for display. It trims the tail when the list runs long.</summary>
        /// <param name="items">Items to join.</param>
        /// <returns>The comma-joined text.</returns>
        public static string Join(List<string> items)
        {
            if (items == null || items.Count == 0) return string.Empty;
            if (items.Count <= MaxShown) return string.Join(", ", items);

            return string.Join(", ", items.GetRange(0, MaxShown)) + $" and {items.Count - MaxShown} more";
        }

        /// <summary>Tells whether the response is an HTML page instead of a table.</summary>
        /// <param name="body">Response body.</param>
        /// <returns>True when it looks like HTML.</returns>
        public static bool LooksLikeHtml(string body)
        {
            if (string.IsNullOrEmpty(body)) return false;

            string trimmed = body.TrimStart();
            string head = trimmed.Substring(0, Math.Min(200, trimmed.Length));

            return head.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
                || head.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
                || head.IndexOf("<meta ", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
