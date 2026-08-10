using System.Collections.Generic;
using NUnit.Framework;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 시트 비교의 판정 규칙을 확인합니다.
    /// 이 판정이 "받으면 무엇이 사라지는가"를 사람에게 알리는 유일한 통로라,
    /// 틀리면 조용히 덮어쓴 뒤에야 드러납니다.
    /// </summary>
    public sealed class SheetDiffTests
    {
        private const string Header = "Id,Name,HP";

        /// <summary>줄들을 표 원문으로 잇습니다.</summary>
        /// <param name="rows">헤더를 뺀 줄들입니다.</param>
        /// <returns>표 원문입니다.</returns>
        private static string Table(params string[] rows) => Header + "\n" + string.Join("\n", rows) + "\n";

        // ====================================================================================================
        // 정규화
        // ====================================================================================================

        /// <summary>줄 끝 문자만 다른 두 표는 같은 것으로 봅니다.</summary>
        [Test]
        public void 줄끝_문자는_차이가_아니다()
        {
            string crlf = "Id,Name\r\nA,갑\r\n";
            string lf = "Id,Name\nA,갑\n";

            Assert.AreEqual(SheetDiff.Normalize(lf), SheetDiff.Normalize(crlf));
            Assert.IsNull(SheetDiff.Describe(SheetDiff.Normalize(crlf), SheetDiff.Normalize(lf)));
        }

        /// <summary>BOM과 끝의 빈 줄은 차이가 아닙니다.</summary>
        [Test]
        public void BOM과_끝_빈줄은_차이가_아니다()
        {
            string withBom = "﻿Id,Name\nA,갑\n\n\n";
            string plain = "Id,Name\nA,갑";

            Assert.AreEqual(SheetDiff.Normalize(plain), SheetDiff.Normalize(withBom));
        }

        // ====================================================================================================
        // 첫 열 뽑기
        // ====================================================================================================

        /// <summary>따옴표 없는 첫 열을 뽑습니다.</summary>
        [Test]
        public void 첫_열을_뽑는다()
        {
            Assert.AreEqual("A", SheetDiff.FirstField("A,갑,10"));
            Assert.AreEqual("A", SheetDiff.FirstField(" A ,갑"));
            Assert.AreEqual("A", SheetDiff.FirstField("A"));
            Assert.AreEqual(string.Empty, SheetDiff.FirstField(""));
        }

        /// <summary>따옴표로 감싼 첫 열의 쉼표는 구분자가 아닙니다.</summary>
        [Test]
        public void 따옴표_안의_쉼표는_구분자가_아니다()
        {
            Assert.AreEqual("갑, 을", SheetDiff.FirstField("\"갑, 을\",10"));
        }

        /// <summary>이스케이프된 따옴표는 리터럴 따옴표입니다.</summary>
        [Test]
        public void 이스케이프된_따옴표를_읽는다()
        {
            Assert.AreEqual("가\"나", SheetDiff.FirstField("\"가\"\"나\",10"));
        }

        // ====================================================================================================
        // 색인
        // ====================================================================================================

        /// <summary>헤더는 색인에서 빠지고, 빈 줄도 빠집니다.</summary>
        [Test]
        public void 헤더와_빈줄은_색인에서_빠진다()
        {
            Dictionary<string, string> map = SheetDiff.IndexByFirstField(Table("A,갑,10", "", "B,을,20"));

            Assert.AreEqual(2, map.Count);
            CollectionAssert.AreEquivalent(new[] { "A", "B" }, new List<string>(map.Keys));
        }

        /// <summary>식별자가 겹치면 뒤엣것이 이깁니다. 임포터의 덮어쓰기 순서와 같아야 합니다.</summary>
        [Test]
        public void 식별자가_겹치면_뒤엣것이_이긴다()
        {
            Dictionary<string, string> map = SheetDiff.IndexByFirstField(Table("A,갑,10", "A,을,20"));

            Assert.AreEqual(1, map.Count);
            Assert.AreEqual("A,을,20", map["A"]);
        }

        // ====================================================================================================
        // 차이 설명
        // ====================================================================================================

        /// <summary>같은 내용이면 null입니다. 이것이 "받아도 잃을 것이 없다"의 근거입니다.</summary>
        [Test]
        public void 같으면_null이다()
        {
            Assert.IsNull(SheetDiff.Describe(Table("A,갑,10"), Table("A,갑,10")));
        }

        /// <summary>
        /// 헤더가 다르면 <b>그 사실만</b> 알리고 행 대조로 넘어가지 않습니다.
        /// 열 구성이 바뀌었는지 엉뚱한 탭인지는 사람이 봐야 알 수 있습니다.
        /// </summary>
        [Test]
        public void 헤더가_다르면_그것만_알린다()
        {
            string difference = SheetDiff.Describe(Table("A,갑,10"), "Id,Name,MP\nA,갑,10\n");

            Assert.IsNotNull(difference);
            StringAssert.Contains("헤더가 다릅니다", difference);
            StringAssert.DoesNotContain("로컬에만 있는 행", difference);
        }

        /// <summary>로컬에만 있는 행은 받으면 사라집니다. 그 사실이 목록에 나와야 합니다.</summary>
        [Test]
        public void 로컬에만_있는_행을_알린다()
        {
            string difference = SheetDiff.Describe(Table("A,갑,10", "B,을,20"), Table("A,갑,10"));

            StringAssert.Contains("경고 없이 덮입니다", difference);
            StringAssert.Contains("로컬에만 있는 행 1: B", difference);
        }

        /// <summary>시트에만 있는 행과 값이 바뀐 행을 나누어 셉니다.</summary>
        [Test]
        public void 추가와_변경을_나누어_센다()
        {
            string difference = SheetDiff.Describe(
                Table("A,갑,10", "B,을,20"),
                Table("A,갑,99", "B,을,20", "C,병,30"));

            StringAssert.Contains("시트에만 있는 행 1: C", difference);
            StringAssert.Contains("값이 다른 행 1: A", difference);
            StringAssert.DoesNotContain("로컬에만 있는 행", difference);
        }

        /// <summary>
        /// 첫 열로 대조가 되지 않는 표는 <b>거짓으로 "같다"고 하지 않고</b> 줄 수만 알립니다.
        /// 대조에 실패한 것을 일치로 보고하면 받기가 조용히 덮습니다.
        /// </summary>
        [Test]
        public void 대조가_안_되면_줄_수만_알린다()
        {
            // 첫 열이 모두 같아 한 항목으로 접히고, 마지막 줄이 양쪽에서 같아지는 표입니다.
            string difference = SheetDiff.Describe(
                Table("A,갑,10", "A,을,20"),
                Table("A,병,30", "A,을,20"));

            Assert.IsNotNull(difference);
            StringAssert.Contains("행 대조로는 차이를 찾지 못했습니다", difference);

            // 색인 크기를 쓰면 양쪽 다 1이라 아무것도 알려 주지 못합니다. 실제 줄 수여야 합니다.
            StringAssert.Contains("줄 수 로컬 2 / 시트 2", difference);
        }

        /// <summary>목록이 길면 뒤를 줄여 로그가 터지지 않게 합니다.</summary>
        [Test]
        public void 긴_목록은_줄인다()
        {
            var items = new List<string>();
            for (int i = 0; i < 12; i++) items.Add($"Row{i}");

            string joined = SheetDiff.Join(items);

            StringAssert.Contains("Row0", joined);
            StringAssert.Contains("외 4개", joined);
            StringAssert.DoesNotContain("Row11", joined);
        }

        // ====================================================================================================
        // HTML 판별
        // ====================================================================================================

        /// <summary>
        /// 공개돼 있지 않은 시트는 오류가 아니라 <b>로그인 HTML을 200으로</b> 옵니다.
        /// 이걸 통과시키면 표가 HTML로 덮여 에셋이 망가집니다.
        /// </summary>
        [Test]
        public void 로그인_HTML을_표로_보지_않는다()
        {
            Assert.IsTrue(SheetDiff.LooksLikeHtml("<!DOCTYPE html><html><head>"));
            Assert.IsTrue(SheetDiff.LooksLikeHtml("\n  <html lang=\"ko\">"));
            Assert.IsTrue(SheetDiff.LooksLikeHtml("<meta http-equiv=\"refresh\" content=\"0\">"));
        }

        /// <summary>표는 HTML이 아닙니다. 꺾쇠가 값에 들어 있어도 마찬가지입니다.</summary>
        [Test]
        public void 표를_HTML로_보지_않는다()
        {
            Assert.IsFalse(SheetDiff.LooksLikeHtml(Table("A,갑,10")));
            Assert.IsFalse(SheetDiff.LooksLikeHtml("Id,Desc\nA,\"<b>굵게</b>\"\n"));
            Assert.IsFalse(SheetDiff.LooksLikeHtml(string.Empty));
        }
    }
}
