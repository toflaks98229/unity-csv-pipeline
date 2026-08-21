using System;
using System.Collections.Generic;

namespace CsvPipeline
{
    /// <summary>이미 자리를 차지한 식별자 하나입니다.</summary>
    public readonly struct CsvIdClaim
    {
        /// <summary>자리 하나를 만듭니다.</summary>
        /// <param name="display">처음 나왔을 때의 표기 그대로입니다.</param>
        /// <param name="line">처음 나온 줄 번호입니다. 모르면 0입니다.</param>
        public CsvIdClaim(string display, int line)
        {
            Display = display;
            Line = line;
        }

        /// <summary>처음 나왔을 때의 표기 그대로입니다.</summary>
        public string Display { get; }

        /// <summary>처음 나온 줄 번호입니다. 모르면 0입니다.</summary>
        public int Line { get; }
    }

    /// <summary>
    /// 한 표 안에서 식별자가 겹치는지 지켜봅니다.
    /// <para>
    /// 식별자는 그대로 산출물의 파일 이름이 됩니다. 그래서 겹치면 <b>두 행이 같은 에셋을 가리키고,
    /// 뒤 행이 앞 행을 덮어 앞 행의 값이 사라집니다.</b> 건수로도 드러나지 않습니다 —
    /// 앞 행은 '생성', 뒤 행은 '갱신'으로 세어져 정상적인 임포트와 구분되지 않기 때문입니다.
    /// </para>
    /// <para>
    /// <b>대소문자만 다른 것도 겹침으로 봅니다.</b> 윈도우는 파일 이름의 대소문자를 가리지 않아
    /// <c>Sword</c>와 <c>sword</c>가 같은 파일이 되는데, macOS·리눅스에서는 다른 파일이 됩니다.
    /// 그대로 두면 <b>어느 PC에서 구웠느냐에 따라 결과가 갈리는</b> 표가 됩니다.
    /// </para>
    /// </summary>
    public sealed class CsvIdClaims
    {
        private readonly Dictionary<string, CsvIdClaim> _claims =
            new Dictionary<string, CsvIdClaim>(StringComparer.OrdinalIgnoreCase);

        /// <summary>지금까지 자리를 차지한 서로 다른 식별자의 수입니다.</summary>
        public int Count => _claims.Count;

        /// <summary>
        /// 이 자리를 차지해 봅니다. 처음이면 차지하고 true, 이미 임자가 있으면 그대로 두고 false입니다.
        /// </summary>
        /// <param name="key">자리를 가리는 값입니다. 보통 에셋 경로이며, 없으면 식별자입니다.</param>
        /// <param name="display">사람에게 보일 식별자 표기입니다.</param>
        /// <param name="line">지금 행의 줄 번호입니다. 모르면 0입니다.</param>
        /// <param name="taken">이미 임자가 있으면 그 자리를 받습니다.</param>
        /// <returns>처음 차지했으면 true입니다.</returns>
        public bool TryClaim(string key, string display, int line, out CsvIdClaim taken)
        {
            if (string.IsNullOrEmpty(key))
            {
                taken = default;
                return true;   // 가릴 값이 없으면 겹침을 따지지 않습니다.
            }

            if (_claims.TryGetValue(key, out taken)) return false;

            _claims[key] = new CsvIdClaim(display, line);
            return true;
        }

        /// <summary>
        /// 겹침을 사람이 읽을 한 문장으로 옮깁니다. 리포트와 미리보기가 같은 말을 하도록 여기 둡니다.
        /// </summary>
        /// <param name="display">지금 행의 식별자 표기입니다.</param>
        /// <param name="taken">이미 자리를 차지하고 있던 쪽입니다.</param>
        /// <returns>설명 문장입니다.</returns>
        public static string Describe(string display, CsvIdClaim taken)
        {
            string where = taken.Line > 0 ? $"{taken.Line}행" : "앞선 행";

            if (!string.Equals(display, taken.Display, StringComparison.Ordinal))
            {
                return $"식별자 '{display}'는 {where}의 '{taken.Display}'와 대소문자만 다릅니다. "
                     + "윈도우에서는 같은 에셋이 되어 앞 행의 값이 사라지고, macOS·리눅스에서는 "
                     + "다른 에셋이 됩니다. 한쪽으로 맞추십시오.";
            }

            return $"식별자 '{display}'가 {where}에서 이미 쓰였습니다. "
                 + $"두 행이 같은 에셋을 가리켜 뒤 행이 앞 행을 덮으므로, {where}의 값은 사라집니다.";
        }
    }
}
