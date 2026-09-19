using System;
using System.Text;

namespace CsvPipeline
{
    /// <summary>
    /// Moves between the bytes of a table file and a string. <b>Encoding detection lives here and nowhere else.</b>
    /// <para>
    /// This place exists on its own because the failure is silent. Windows Excel saves CSV as <b>ANSI</b>
    /// (CP949 in Korea) by default, not UTF-8. Reading such a file as UTF-8 does not throw — <b>the mangled
    /// characters pass straight through</b>. They are baked into the assets as they are, and the next export
    /// writes those mangled values back into the table, overwriting the source. No error is left anywhere.
    /// </para>
    /// <para>
    /// So this <b>stops instead of fixing it up</b>. Reading with the system code page would make the same
    /// file open one way on a Korean developer's machine and another way on a German developer's. One table
    /// baking different values depending on the person is worse than mangled characters. The file is what
    /// needs fixing, and the message says how.
    /// </para>
    /// </summary>
    public static class CsvText
    {
        /// <summary>How much of the head to scan for NUL bytes when detecting UTF-16.</summary>
        private const int SniffLength = 512;

        /// <summary>
        /// Turns the bytes of a table file into a string.
        /// </summary>
        /// <param name="bytes">Raw bytes of the file.</param>
        /// <param name="text">Receives the decoded string. Null on failure.</param>
        /// <param name="problem">Receives the reason it could not be read. Null on success.</param>
        /// <returns>True when it was read.</returns>
        public static bool TryDecode(byte[] bytes, out string text, out string problem)
        {
            text = null;
            problem = null;

            if (bytes == null) return false;
            if (bytes.Length == 0) { text = string.Empty; return true; }

            // BOM이 있으면 그것이 답입니다. 추측할 일이 없습니다.
            Encoding declared = BomEncoding(bytes, out int bomLength);
            if (declared != null)
            {
                try
                {
                    text = declared.GetString(bytes, bomLength, bytes.Length - bomLength);
                    return true;
                }
                catch (Exception e) when (e is ArgumentException || e is DecoderFallbackException)
                {
                    problem = $"The file carries a {declared.EncodingName} BOM, but its content is not in that encoding.\n"
                            + FixHint;
                    return false;
                }
            }

            // BOM 없는 UTF-16은 UTF-8로도 '읽힙니다' — NUL이 UTF-8에서 유효한 바이트라 예외가 나지 않고,
            // 글자 사이에 NUL이 끼어든 문자열이 됩니다. 엄격 검사에 걸리지 않으므로 먼저 봅니다.
            if (HasNul(bytes))
            {
                problem = "This looks like it was saved as UTF-16. (There are NUL bytes between the characters)\n"
                        + FixHint;
                return false;
            }

            try
            {
                text = StrictUtf8.GetString(bytes);
                return true;
            }
            catch (DecoderFallbackException)
            {
                problem = "This is not UTF-8. Saving as 'CSV (Comma delimited)' in Windows Excel does not write UTF-8; "
                        + "it writes the system default encoding (CP949 on Korean Windows).\n"
                        + FixHint;
                return false;
            }
        }

        /// <summary>How to fix a mangled file. Wording it differently in each message would read as a different problem.</summary>
        private const string FixHint =
            "  How to fix it — Excel: File ▸ Save As ▸ set the format to 'CSV UTF-8 (Comma delimited)'.\n"
            + "  Files downloaded from Google Sheets are already UTF-8 and work as they are.\n"
            + "  In Notepad or VS Code, switch the encoding to 'UTF-8' and save again.";

        /// <summary>A UTF-8 encoding that reports bad bytes as an exception. The default UTF8Encoding substitutes them silently.</summary>
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);

        /// <summary>
        /// The UTF-8 encoding used to write files.
        /// </summary>
        /// <param name="withBom">Whether to prepend a BOM.</param>
        /// <returns>Encoding to write with.</returns>
        public static UTF8Encoding Utf8(bool withBom) => withBom ? Utf8Bom : Utf8Plain;

        private static readonly UTF8Encoding Utf8Bom = new UTF8Encoding(true);
        private static readonly UTF8Encoding Utf8Plain = new UTF8Encoding(false);

        // ====================================================================================================
        // 판정
        // ====================================================================================================

        /// <summary>The encoding declared by the BOM. Null when there is none.</summary>
        /// <param name="bytes">Raw bytes of the file.</param>
        /// <param name="bomLength">Receives the length of the BOM.</param>
        /// <returns>The declared encoding, or null.</returns>
        private static Encoding BomEncoding(byte[] bytes, out int bomLength)
        {
            // UTF-32는 UTF-16 BOM으로 시작하므로 먼저 봐야 합니다. (FF FE 00 00 의 앞 두 바이트가 FF FE)
            if (StartsWith(bytes, 0xFF, 0xFE, 0x00, 0x00)) { bomLength = 4; return new UTF32Encoding(false, true); }
            if (StartsWith(bytes, 0x00, 0x00, 0xFE, 0xFF)) { bomLength = 4; return new UTF32Encoding(true, true); }
            // BOM 이 UTF-8 이라고 선언해도 본문이 그렇지 않을 수 있습니다. 치환 폴백으로 읽으면
            // 깨진 바이트가 U+FFFD 로 조용히 바뀌어 통과합니다 — 이 클래스가 막으려는 바로 그 실패라,
            // 여기서도 엄격판을 씁니다. (이 자리 때문에 아래 catch 가 도달 불가능한 죽은 코드였습니다)
            if (StartsWith(bytes, 0xEF, 0xBB, 0xBF)) { bomLength = 3; return StrictUtf8; }
            if (StartsWith(bytes, 0xFF, 0xFE)) { bomLength = 2; return Encoding.Unicode; }
            if (StartsWith(bytes, 0xFE, 0xFF)) { bomLength = 2; return Encoding.BigEndianUnicode; }

            bomLength = 0;
            return null;
        }

        /// <summary>Checks whether the byte sequence starts with the given bytes.</summary>
        /// <param name="bytes">Byte sequence to inspect.</param>
        /// <param name="prefix">Bytes it is expected to start with.</param>
        /// <returns>True when they match.</returns>
        private static bool StartsWith(byte[] bytes, params byte[] prefix)
        {
            if (bytes.Length < prefix.Length) return false;

            for (int i = 0; i < prefix.Length; i++)
            {
                if (bytes[i] != prefix[i]) return false;
            }
            return true;
        }

        /// <summary>Checks the head for NUL bytes. A table file has no reason to contain one.</summary>
        /// <param name="bytes">Byte sequence to inspect.</param>
        /// <returns>True when one is present.</returns>
        private static bool HasNul(byte[] bytes)
        {
            int end = Math.Min(bytes.Length, SniffLength);
            for (int i = 0; i < end; i++)
            {
                if (bytes[i] == 0x00) return true;
            }
            return false;
        }
    }
}
