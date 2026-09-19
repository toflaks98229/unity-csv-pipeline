using System.Text;
using NUnit.Framework;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 표 파일의 인코딩 판정을 검사합니다.
    /// <para>
    /// 이 자리의 실패는 예외로 드러나지 않고 <b>깨진 글자로 통과</b>합니다. 그래서 "읽혔는가"가 아니라
    /// <b>"읽지 못했을 때 멈추는가"</b>를 봅니다.
    /// </para>
    /// </summary>
    public sealed class CsvTextTests
    {
        /// <summary>BOM 없는 UTF-8은 그대로 읽힙니다. 가장 흔한 형식입니다.</summary>
        [Test]
        public void BOM_없는_UTF8을_읽는다()
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes("Id,Name\nA,검\n");

            Assert.IsTrue(CsvText.TryDecode(bytes, out string text, out string problem), problem);
            Assert.AreEqual("Id,Name\nA,검\n", text);
        }

        /// <summary>UTF-8 BOM은 읽으면서 떼어 냅니다. 남으면 첫 열 이름이 보이지 않는 글자로 시작합니다.</summary>
        [Test]
        public void UTF8_BOM을_떼어_낸다()
        {
            byte[] bytes = new UTF8Encoding(true).GetPreamble();
            byte[] rest = new UTF8Encoding(false).GetBytes("Id,Name\nA,검\n");
            var all = new byte[bytes.Length + rest.Length];
            bytes.CopyTo(all, 0);
            rest.CopyTo(all, bytes.Length);

            Assert.IsTrue(CsvText.TryDecode(all, out string text, out string problem), problem);
            Assert.AreEqual("Id,Name\nA,검\n", text, "BOM이 남아 있으면 첫 열 이름이 어긋납니다.");
        }

        /// <summary>BOM이 붙은 UTF-16도 읽습니다. Excel의 '유니코드 텍스트' 저장이 이것입니다.</summary>
        [Test]
        public void BOM이_붙은_UTF16을_읽는다()
        {
            byte[] bytes = Encoding.Unicode.GetPreamble();
            byte[] rest = Encoding.Unicode.GetBytes("Id,Name\nA,검\n");
            var all = new byte[bytes.Length + rest.Length];
            bytes.CopyTo(all, 0);
            rest.CopyTo(all, bytes.Length);

            Assert.IsTrue(CsvText.TryDecode(all, out string text, out string problem), problem);
            Assert.AreEqual("Id,Name\nA,검\n", text);
        }

        /// <summary>
        /// CP949로 저장된 표는 <b>읽지 않고 멈춥니다.</b>
        /// 시스템 코드페이지로 읽어 주면 같은 파일이 기계마다 다른 값을 굽습니다.
        /// </summary>
        [Test]
        public void UTF8이_아니면_멈춘다()
        {
            // '검' 의 CP949 바이트입니다. UTF-8로는 성립하지 않는 시퀀스입니다.
            byte[] bytes = { (byte)'I', (byte)'d', (byte)0x0A, 0xB0, 0xCB };

            Assert.IsFalse(CsvText.TryDecode(bytes, out string text, out string problem));
            Assert.IsNull(text);
            StringAssert.Contains("UTF-8", problem);
            StringAssert.Contains("Excel", problem, "고치는 법이 함께 있어야 합니다.");
        }

        /// <summary>
        /// BOM 없는 UTF-16은 UTF-8 검사에 걸리지 않습니다. NUL이 UTF-8에서 유효한 바이트라
        /// 글자 사이에 NUL이 낀 문자열로 <b>조용히 통과</b>합니다. 그래서 따로 봅니다.
        /// </summary>
        [Test]
        public void BOM_없는_UTF16도_멈춘다()
        {
            byte[] bytes = Encoding.Unicode.GetBytes("Id,Name\nA,B\n");

            Assert.IsFalse(CsvText.TryDecode(bytes, out _, out string problem));
            StringAssert.Contains("UTF-16", problem);
        }

        /// <summary>빈 파일은 빈 문자열입니다. 오류가 아닙니다.</summary>
        [Test]
        public void 빈_파일은_빈_문자열이다()
        {
            Assert.IsTrue(CsvText.TryDecode(new byte[0], out string text, out string problem), problem);
            Assert.AreEqual(string.Empty, text);
        }

        /// <summary>BOM을 붙이는 인코딩과 붙이지 않는 인코딩이 실제로 갈립니다.</summary>
        [Test]
        public void BOM_설정이_쓰기에_반영된다()
        {
            Assert.AreEqual(3, CsvText.Utf8(true).GetPreamble().Length);
            Assert.AreEqual(0, CsvText.Utf8(false).GetPreamble().Length);
        }

        /// <summary>이 도구가 쓴 파일은 이 도구가 도로 읽을 수 있어야 합니다. BOM 유무와 무관합니다.</summary>
        [TestCase(true, TestName = "BOM 붙임")]
        [TestCase(false, TestName = "BOM 없음")]
        public void 쓴_것을_도로_읽는다(bool withBom)
        {
            const string original = "Id,Name\nA,검\n";
            byte[] written = CsvText.Utf8(withBom).GetPreamble();
            byte[] body = CsvText.Utf8(withBom).GetBytes(original);
            var all = new byte[written.Length + body.Length];
            written.CopyTo(all, 0);
            body.CopyTo(all, written.Length);

            Assert.IsTrue(CsvText.TryDecode(all, out string text, out string problem), problem);
            Assert.AreEqual(original, text);
            Assert.AreEqual(1, CsvReader.ReadTable(text).Count);
        }
    }
}
