using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CsvPipeline
{
    /// <summary>
    /// <see cref="CsvReader"/>가 파싱한 한 행(<c>Dictionary&lt;string,object&gt;</c>)을 감싸,
    /// 임포터들이 공통으로 쓰는 셀 접근·타입 파싱·리스트 분리를 한 곳으로 모은 접근자입니다.
    /// </summary>
    public readonly struct CsvRow
    {
        /// <summary>리스트형 셀(효과/조건/자식 등)을 나눌 때 쓰는 구분자입니다. (CSV의 ','와 충돌 방지)</summary>
        public static readonly char[] ListSeparators = { ';', '|' };

        private readonly Dictionary<string, object> _cells;

        /// <summary>파싱된 행 딕셔너리를 감쌉니다.</summary>
        /// <param name="cells">헤더-값 쌍입니다.</param>
        public CsvRow(Dictionary<string, object> cells) { _cells = cells; }

        /// <summary>지정 키가 존재하는지 여부입니다.</summary>
        /// <param name="key">확인할 헤더 이름입니다.</param>
        /// <returns>존재하면 true입니다.</returns>
        public bool Has(string key) => _cells != null && _cells.ContainsKey(key);

        /// <summary>이 행의 모든 헤더(열 이름)입니다. (동적 컬럼을 순회하는 임포터용)</summary>
        public IEnumerable<string> Keys => _cells != null ? _cells.Keys : Enumerable.Empty<string>();

        /// <summary>
        /// 셀 값을 앞뒤 공백을 제거한 문자열로 반환합니다. 없으면 빈 문자열입니다.
        /// 파서가 숫자를 boxed int/float로 저장하므로, 숫자는 로케일 독립적(InvariantCulture)으로 포매팅해
        /// 하위 <see cref="int.TryParse(string,out int)"/>/<c>float.TryParse</c>와 일관되게 합니다.
        /// </summary>
        /// <param name="key">읽을 헤더 이름입니다.</param>
        /// <returns>셀 문자열입니다.</returns>
        public string GetString(string key)
        {
            if (_cells == null || !_cells.TryGetValue(key, out object o) || o == null) return string.Empty;
            switch (o)
            {
                case float f: return f.ToString(CultureInfo.InvariantCulture);
                case int n: return n.ToString(CultureInfo.InvariantCulture);
                default: return o.ToString().Trim();
            }
        }

        /// <summary>셀이 비어 있지 않으면 true와 값을 반환합니다.</summary>
        /// <param name="key">읽을 헤더 이름입니다.</param>
        /// <param name="value">읽어 낸 값입니다.</param>
        /// <returns>비어 있지 않으면 true입니다.</returns>
        public bool TryGetString(string key, out string value)
        {
            value = GetString(key);
            return !string.IsNullOrEmpty(value);
        }

        /// <summary>셀을 로케일 독립적 int로 파싱합니다. 실패 시 fallback입니다.</summary>
        /// <param name="key">읽을 헤더 이름입니다.</param>
        /// <param name="fallback">파싱 실패 시 쓸 값입니다.</param>
        /// <returns>파싱된 값입니다.</returns>
        public int GetInt(string key, int fallback = 0)
            => int.TryParse(GetString(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

        /// <summary>셀을 로케일 독립적 float로 파싱합니다. 실패 시 fallback입니다.</summary>
        /// <param name="key">읽을 헤더 이름입니다.</param>
        /// <param name="fallback">파싱 실패 시 쓸 값입니다.</param>
        /// <returns>파싱된 값입니다.</returns>
        public float GetFloat(string key, float fallback = 0f)
            => float.TryParse(GetString(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;

        /// <summary>셀이 존재하고 int로 파싱되면 true와 값을 반환합니다. (직접 필드대입 임포터용: 컬럼 없으면 기존값 보존)</summary>
        /// <param name="key">읽을 헤더 이름입니다.</param>
        /// <param name="value">파싱된 값입니다.</param>
        /// <returns>파싱에 성공하면 true입니다.</returns>
        public bool TryGetInt(string key, out int value)
            => int.TryParse(GetString(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        /// <summary>셀이 존재하고 float로 파싱되면 true와 값을 반환합니다. (직접 필드대입 임포터용: 컬럼 없으면 기존값 보존)</summary>
        /// <param name="key">읽을 헤더 이름입니다.</param>
        /// <param name="value">파싱된 값입니다.</param>
        /// <returns>파싱에 성공하면 true입니다.</returns>
        public bool TryGetFloat(string key, out float value)
            => float.TryParse(GetString(key), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        /// <summary>"TRUE"/"1"은 true, "FALSE"/"0"은 false로 해석합니다. 그 외/빈 값은 fallback입니다.</summary>
        /// <param name="key">읽을 헤더 이름입니다.</param>
        /// <param name="fallback">해석할 수 없을 때 쓸 값입니다.</param>
        /// <returns>해석된 값입니다.</returns>
        public bool GetBool(string key, bool fallback = false)
        {
            string raw = GetString(key);
            if (string.IsNullOrEmpty(raw)) return fallback;
            if (raw.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || raw == "1") return true;
            if (raw.Equals("FALSE", StringComparison.OrdinalIgnoreCase) || raw == "0") return false;
            return fallback;
        }

        /// <summary>';'/'|'로 구분된 리스트 셀을 트림된 토큰 배열로 반환합니다. (빈 토큰 제거)</summary>
        /// <param name="key">읽을 헤더 이름입니다.</param>
        /// <returns>토큰 배열입니다.</returns>
        public string[] GetList(string key)
        {
            string raw = GetString(key);
            return string.IsNullOrEmpty(raw)
                ? Array.Empty<string>()
                : raw.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries)
                     .Select(s => s.Trim())
                     .Where(s => s.Length > 0)
                     .ToArray();
        }
    }
}
