using System.Collections.Generic;
using System.Text;

namespace CsvPipeline
{
    /// <summary>표 텍스트를 만드는 작성기입니다. 구분자·따옴표·개행이 든 셀을 규칙대로 감쌉니다.</summary>
    public sealed class CsvWriter
    {
        private readonly StringBuilder _text = new StringBuilder();
        private readonly char _delimiter;

        /// <summary>작성기를 만듭니다.</summary>
        /// <param name="delimiter">필드 구분자입니다.</param>
        public CsvWriter(char delimiter = CsvReader.Comma) { _delimiter = delimiter; }

        /// <summary>한 줄을 씁니다.</summary>
        /// <param name="fields">쓸 셀들입니다.</param>
        public void WriteRow(IReadOnlyList<string> fields)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0) _text.Append(_delimiter);
                _text.Append(Escape(fields[i]));
            }
            _text.Append('\n');
        }

        /// <summary>지금까지 쓴 내용입니다. 줄 끝은 LF로 통일합니다.</summary>
        /// <returns>표 원문입니다.</returns>
        public override string ToString() => _text.ToString();

        /// <summary>
        /// 필요한 경우에만 큰따옴표로 감쌉니다. 안의 따옴표는 <c>""</c>로 이스케이프합니다.
        /// </summary>
        /// <param name="value">감쌀 값입니다.</param>
        /// <returns>표에 그대로 쓸 수 있는 셀 문자열입니다.</returns>
        private string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            bool needsQuotes = value.IndexOf(_delimiter) >= 0
                            || value.IndexOf('"') >= 0
                            || value.IndexOf('\n') >= 0
                            || value.IndexOf('\r') >= 0;

            if (!needsQuotes) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
