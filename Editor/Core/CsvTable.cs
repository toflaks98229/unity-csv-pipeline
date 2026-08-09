using System;
using System.Collections.Generic;

namespace CsvPipeline
{
    /// <summary>
    /// 파싱된 표 하나입니다. 행뿐 아니라 <b>헤더</b>를 함께 들고 있어,
    /// 열 이름 오타를 "빈 셀"로 흘려보내지 않고 잡아낼 수 있습니다.
    /// </summary>
    public sealed class CsvTable
    {
        private readonly HashSet<string> _headerSet;

        /// <summary>표를 만듭니다.</summary>
        /// <param name="headers">헤더(열 이름) 목록입니다.</param>
        /// <param name="rows">데이터 행들입니다.</param>
        public CsvTable(IReadOnlyList<string> headers, IReadOnlyList<CsvRow> rows)
        {
            Headers = headers ?? Array.Empty<string>();
            Rows = rows ?? Array.Empty<CsvRow>();

            // 헤더는 대소문자를 가리지 않습니다. 표는 PascalCase(MaxSpeed), 필드는 camelCase(maxSpeed)로
            // 적히는 것이 보통이라, 가려서 비교하면 자동 연결이 하나도 붙지 않습니다.
            _headerSet = new HashSet<string>(Headers, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>헤더(열 이름) 목록입니다. 표에 적힌 순서를 지킵니다.</summary>
        public IReadOnlyList<string> Headers { get; }

        /// <summary>데이터 행들입니다. 헤더 행은 포함하지 않습니다.</summary>
        public IReadOnlyList<CsvRow> Rows { get; }

        /// <summary>데이터 행 수입니다.</summary>
        public int Count => Rows.Count;

        /// <summary>표에 지정 열이 있는지 여부입니다. 대소문자는 가리지 않습니다.</summary>
        /// <param name="column">확인할 열 이름입니다.</param>
        /// <returns>열이 있으면 true입니다.</returns>
        public bool HasColumn(string column) => !string.IsNullOrEmpty(column) && _headerSet.Contains(column);

        /// <summary>
        /// 요구한 열 중 표에 없는 것들을 돌려줍니다.
        /// </summary>
        /// <param name="required">있어야 하는 열 이름들입니다.</param>
        /// <returns>빠진 열 이름들입니다. 전부 있으면 빈 목록입니다.</returns>
        public List<string> FindMissingColumns(IEnumerable<string> required)
        {
            var missing = new List<string>();
            if (required == null) return missing;

            foreach (string column in required)
            {
                if (!string.IsNullOrEmpty(column) && !HasColumn(column)) missing.Add(column);
            }
            return missing;
        }

        /// <summary>
        /// 이름이 거의 같은 열을 찾습니다. 오타 안내에 씁니다.
        /// 대소문자는 이미 <see cref="HasColumn"/>이 흡수하므로, 여기서는 공백·밑줄·하이픈 차이를 봅니다.
        /// </summary>
        /// <param name="column">찾던 열 이름입니다.</param>
        /// <returns>거의 같은 실제 열 이름이거나, 없으면 null입니다.</returns>
        public string FindSimilarColumn(string column)
        {
            if (string.IsNullOrEmpty(column)) return null;

            string needle = Squash(column);
            if (needle.Length == 0) return null;

            for (int i = 0; i < Headers.Count; i++)
            {
                if (Squash(Headers[i]) == needle) return Headers[i];
            }
            return null;
        }

        /// <summary>비교용으로 이름에서 구분 기호를 걷어내고 소문자로 만듭니다.</summary>
        /// <param name="name">정규화할 이름입니다.</param>
        /// <returns>정규화된 이름입니다.</returns>
        private static string Squash(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;

            var text = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (c == ' ' || c == '_' || c == '-') continue;
                text.Append(char.ToLowerInvariant(c));
            }
            return text.ToString();
        }
    }
}
