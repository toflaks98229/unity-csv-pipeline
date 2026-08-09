using UnityEngine;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CsvPipeline
{
    /// <summary>
    /// CSV 텍스트를 해석하는 공용 파서입니다.
    /// RFC 4180 스타일의 따옴표 필드(내부 콤마·개행·이스케이프된 "")·BOM·CRLF를 처리하며,
    /// 각 행을 헤더 기준으로 자동 타입 변환(int → float → string)한 "헤더-값" 딕셔너리 목록으로 반환합니다.
    /// </summary>
    public static class CsvReader
    {
        /// <summary>
        /// CSV 텍스트 에셋을 분석하여 각 행을 "헤더-값" 쌍의 딕셔너리 목록으로 변환합니다.
        /// </summary>
        /// <param name="csvAsset">분석할 CSV 텍스트 에셋입니다.</param>
        /// <returns>파싱된 행 데이터 목록을 반환합니다.</returns>
        public static List<Dictionary<string, object>> Read(TextAsset csvAsset)
        {
            if (csvAsset == null) return new List<Dictionary<string, object>>();
            return Read(csvAsset.text);
        }

        /// <summary>
        /// CSV 원본 문자열을 분석하여 각 행을 "헤더-값" 쌍의 딕셔너리 목록으로 변환합니다.
        /// </summary>
        /// <param name="text">분석할 CSV 원문입니다.</param>
        /// <returns>파싱된 행 데이터 목록을 반환합니다.</returns>
        public static List<Dictionary<string, object>> Read(string text)
        {
            var list = new List<Dictionary<string, object>>();
            if (string.IsNullOrEmpty(text)) return list;

            List<List<string>> records = ParseRecords(text);
            if (records.Count <= 1) return list;

            List<string> header = records[0];

            for (int r = 1; r < records.Count; r++)
            {
                List<string> values = records[r];

                // 첫 열이 비어 있는 행은 건너뜁니다. (구분자만 있는 빈 줄 방어)
                if (values.Count == 0 || string.IsNullOrEmpty(values[0])) continue;

                var entry = new Dictionary<string, object>();
                for (int j = 0; j < header.Count && j < values.Count; j++)
                {
                    entry[header[j]] = InferType(values[j]);
                }
                list.Add(entry);
            }
            return list;
        }

        // ====================================================================================================
        // Parsing
        // ====================================================================================================

        /// <summary>
        /// CSV 전체 텍스트를 레코드(행) 목록으로 분해합니다. 따옴표로 감싼 필드 내부의 콤마와 개행은 보존됩니다.
        /// </summary>
        /// <param name="text">분해할 CSV 원문입니다.</param>
        /// <returns>행마다 필드 목록을 담은 목록입니다.</returns>
        private static List<List<string>> ParseRecords(string text)
        {
            var records = new List<List<string>>();
            var current = new List<string>();
            var field = new StringBuilder();

            bool inQuotes = false;
            bool fieldStarted = false; // 이 행에서 필드 파싱이 시작됐는지 (완전 빈 마지막 줄 무시용)

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
                        field.Append(c);
                    }
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        fieldStarted = true;
                        break;

                    case ',':
                        current.Add(field.ToString());
                        field.Clear();
                        fieldStarted = true;
                        break;

                    case '\r':
                        // CRLF/CR 처리: 레코드 종료 (뒤따르는 \n은 아래 \n 케이스가 흡수하지 않도록 스킵)
                        EndRecord(records, ref current, field, ref fieldStarted);
                        if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                        break;

                    case '\n':
                        EndRecord(records, ref current, field, ref fieldStarted);
                        break;

                    default:
                        field.Append(c);
                        fieldStarted = true;
                        break;
                }
            }

            // 마지막 필드/레코드 마무리 (파일이 개행으로 끝나지 않은 경우)
            if (fieldStarted || field.Length > 0 || current.Count > 0)
            {
                current.Add(field.ToString());
                records.Add(current);
            }

            return records;
        }

        /// <summary>진행 중인 필드를 확정하고 레코드를 records에 추가한 뒤 상태를 초기화합니다.</summary>
        /// <param name="records">레코드를 모으는 목록입니다.</param>
        /// <param name="current">진행 중인 행의 필드 목록입니다.</param>
        /// <param name="field">진행 중인 필드 버퍼입니다.</param>
        /// <param name="fieldStarted">이 행에서 필드 파싱이 시작됐는지 여부입니다.</param>
        private static void EndRecord(List<List<string>> records, ref List<string> current, StringBuilder field, ref bool fieldStarted)
        {
            current.Add(field.ToString());
            field.Clear();
            records.Add(current);
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
