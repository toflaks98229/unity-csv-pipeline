using System;
using System.Collections.Generic;
using System.Text;

namespace CsvPipeline
{
    /// <summary>
    /// 두 표 원문의 차이를 사람이 읽을 수 있게 설명합니다.
    /// <b>파일도 네트워크도 Unity도 건드리지 않는 순수 함수만</b> 둡니다. 그래서 그대로 검사할 수 있습니다.
    /// </summary>
    public static class SheetDiff
    {
        /// <summary>차이 목록에 이름을 몇 개까지 늘어놓을지입니다.</summary>
        private const int MaxShown = 8;

        /// <summary>
        /// 두 표 원문의 차이를 설명합니다. <b>같으면 null입니다.</b>
        /// </summary>
        /// <param name="local">로컬 표 내용입니다.</param>
        /// <param name="sheet">시트에서 받은 내용입니다.</param>
        /// <returns>차이 설명이거나, 같으면 null입니다.</returns>
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
                text.AppendLine("      헤더가 다릅니다 (받기가 확인을 묻습니다)");
                text.AppendLine($"        로컬: {localHeader}");
                text.AppendLine($"        시트: {sheetHeader}");
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
            text.AppendLine("      헤더는 같고 내용이 다릅니다 (받으면 경고 없이 덮입니다)");

            if (onlyLocal.Count > 0) text.AppendLine($"        로컬에만 있는 행 {onlyLocal.Count}: {Join(onlyLocal)}");
            if (onlySheet.Count > 0) text.AppendLine($"        시트에만 있는 행 {onlySheet.Count}: {Join(onlySheet)}");
            if (changed.Count > 0) text.AppendLine($"        값이 다른 행 {changed.Count}: {Join(changed)}");

            // 첫 열이 비어 있거나 겹치면 행 대조가 성립하지 않습니다. 그때는 줄 수만 알립니다.
            // 색인 크기가 아니라 실제 줄 수를 씁니다. 색인은 겹친 식별자를 접어 버려,
            // 이 갈래가 열리는 상황에서는 바로 그 숫자가 양쪽 다 같게 나옵니다.
            if (onlyLocal.Count == 0 && onlySheet.Count == 0 && changed.Count == 0)
            {
                text.AppendLine($"        (행 대조로는 차이를 찾지 못했습니다. 식별자가 겹치거나 비어 있는 표입니다. "
                              + $"줄 수 로컬 {CountDataLines(local)} / 시트 {CountDataLines(sheet)})");
            }

            return text.ToString();
        }

        /// <summary>
        /// 줄 끝을 LF로 통일하고 BOM을 제거합니다.
        /// 비교와 기록이 같은 형식을 보게 해, 줄 끝 문자만 달라도 "바뀜"으로 잡히는 일을 막습니다.
        /// </summary>
        /// <param name="text">정규화할 문자열입니다.</param>
        /// <returns>정규화된 문자열입니다.</returns>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "\n";

            return text.TrimStart('﻿').Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd() + "\n";
        }

        /// <summary>첫 줄(헤더)입니다.</summary>
        /// <param name="text">대상 문자열입니다.</param>
        /// <returns>첫 줄입니다.</returns>
        public static string FirstLine(string text)
        {
            if (text == null) return string.Empty;

            int index = text.IndexOf('\n');
            return index < 0 ? text : text.Substring(0, index);
        }

        /// <summary>헤더와 빈 줄을 뺀 실제 데이터 줄 수입니다.</summary>
        /// <param name="text">표 전체 내용입니다.</param>
        /// <returns>데이터 줄 수입니다.</returns>
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

        /// <summary>헤더를 뺀 각 줄을 첫 열(식별자) 기준으로 색인합니다.</summary>
        /// <param name="text">표 전체 내용입니다.</param>
        /// <returns>식별자 → 줄 전체 사전입니다.</returns>
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
                if (string.IsNullOrEmpty(key)) key = $"(빈 식별자 {i}행)";

                // 식별자가 겹치면 뒤엣것이 이깁니다. (임포터도 같은 순서로 덮어씁니다)
                map[key] = line;
            }

            return map;
        }

        /// <summary>한 줄에서 첫 열의 값을 뽑습니다. 큰따옴표로 감싼 필드를 인식합니다.</summary>
        /// <param name="line">표 한 줄입니다.</param>
        /// <returns>첫 열의 값입니다.</returns>
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

        /// <summary>목록을 보기 좋게 잇습니다. 너무 길면 뒤를 줄입니다.</summary>
        /// <param name="items">이어 붙일 항목들입니다.</param>
        /// <returns>쉼표로 이은 문자열입니다.</returns>
        public static string Join(List<string> items)
        {
            if (items == null || items.Count == 0) return string.Empty;
            if (items.Count <= MaxShown) return string.Join(", ", items);

            return string.Join(", ", items.GetRange(0, MaxShown)) + $" 외 {items.Count - MaxShown}개";
        }

        /// <summary>응답이 표가 아니라 HTML 페이지인지 판별합니다.</summary>
        /// <param name="body">응답 본문입니다.</param>
        /// <returns>HTML로 보이면 true입니다.</returns>
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
