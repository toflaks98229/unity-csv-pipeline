using System;

namespace CsvPipeline
{
    /// <summary>
    /// 표의 식별자를 <b>산출물의 파일 이름으로 써도 되는지</b> 가립니다.
    /// <para>
    /// 식별자는 그대로 <c>{폴더}/{식별자}.asset</c>이 됩니다. 그래서 파일 이름에 쓸 수 없는 글자가
    /// 섞이면 에셋이 만들어지지 않는데, <b>그 실패는 아무 데도 남지 않습니다</b> — 식별자가 비어 있는 것도
    /// 아니라 건너뜀으로도 세어지지 않고, 정상 경로를 타다가 저장소 쪽에서 조용히 실패합니다.
    /// </para>
    /// <para>
    /// <b>씻어 주지 않고 거절합니다.</b> <c>Item/Sword</c>를 <c>Item_Sword</c>로 바꿔 구우면 표에 적힌 이름과
    /// 에셋 이름이 갈라져, 다음에 그 표를 읽는 사람이 더 헷갈립니다. 고칠 곳은 표입니다.
    /// </para>
    /// </summary>
    public static class CsvAssetId
    {
        /// <summary>
        /// 어느 운영체제에서도 파일 이름에 쓸 수 없는 글자들입니다.
        /// <see cref="System.IO.Path.GetInvalidFileNameChars"/>를 쓰지 않는 것은 그 목록이 <b>돌아가는
        /// 운영체제마다 다르기 때문</b>입니다. 그대로 쓰면 윈도우에서 거절된 표가 macOS에서는 통과해,
        /// 같은 표가 사람에 따라 다르게 동작합니다.
        /// </summary>
        private const string Forbidden = "<>:\"/\\|?*";

        /// <summary>
        /// 파일 이름 하나의 길이 상한입니다. 폴더 경로와 <c>.asset</c>까지 더해도 여유가 남는 값으로 잡았습니다.
        /// </summary>
        private const int MaxLength = 100;

        /// <summary>윈도우가 장치 이름으로 예약해 둔 것들입니다. 확장자를 붙여도 파일로 만들 수 없습니다.</summary>
        private static readonly string[] Reserved =
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };

        /// <summary>
        /// 이 식별자를 파일 이름으로 쓸 수 없으면 그 이유입니다. 쓸 수 있으면 null입니다.
        /// <b>비어 있는 식별자는 호출부가 먼저 거릅니다.</b> 그쪽은 표를 잘못 적은 것이 아니라
        /// 행이 비어 있는 것이라, 안내가 달라야 하기 때문입니다.
        /// </summary>
        /// <param name="id">표에서 읽은 식별자입니다.</param>
        /// <returns>거절 사유이거나, 써도 되면 null입니다.</returns>
        public static string Reject(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (id.Trim() != id)
            {
                return "It has leading or trailing whitespace, which is invisible and splits into a different "
                     + "identifier on the next import.";
            }

            foreach (char c in id)
            {
                if (c < ' ' || c == (char)0x7F) return "It contains an invisible control character.";
                if (Forbidden.IndexOf(c) >= 0) return $"'{c}' cannot be used in a file name.";
            }

            if (id[id.Length - 1] == '.')
            {
                return "It ends with a period, which Windows strips, producing a different name.";
            }

            if (IsReserved(id))
            {
                return "It is a name Windows reserves for devices, so no file can have it.";
            }

            if (id.Length > MaxLength)
            {
                return $"It is longer than {MaxLength} characters ({id.Length}). An over-long path cannot be created.";
            }

            return null;
        }

        /// <summary>
        /// 거절 사유를 사람이 읽을 한 문장으로 옮깁니다. 리포트와 미리보기가 같은 말을 하도록 여기 둡니다.
        /// </summary>
        /// <param name="id">거절된 식별자입니다.</param>
        /// <param name="reason"><see cref="Reject"/>가 돌려준 사유입니다.</param>
        /// <returns>설명 문장입니다.</returns>
        public static string Describe(string id, string reason)
            => $"Identifier '{id}' becomes the output file name as-is, and cannot be used as one. "
             + $"Skipping this row. {reason}";

        /// <summary>예약된 장치 이름인지 봅니다. 마침표 뒤는 확장자로 취급되므로 앞부분만 봅니다.</summary>
        /// <param name="id">확인할 식별자입니다.</param>
        /// <returns>예약된 이름이면 true입니다.</returns>
        private static bool IsReserved(string id)
        {
            int dot = id.IndexOf('.');
            string stem = dot < 0 ? id : id.Substring(0, dot);

            foreach (string name in Reserved)
            {
                if (string.Equals(stem, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
