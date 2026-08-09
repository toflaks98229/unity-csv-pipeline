using NUnit.Framework;

namespace CsvPipeline.Tests
{
    /// <summary>파서가 표를 규칙대로 해석하는지 검사합니다.</summary>
    public sealed class CsvReaderTests
    {
        /// <summary>따옴표로 감싼 필드 안의 구분자는 내용으로 남습니다.</summary>
        [Test]
        public void 따옴표_안의_쉼표는_필드를_나누지_않는다()
        {
            CsvTable table = CsvReader.ReadTable("Id,Body\nA,\"쉼표, 포함\"\n");

            Assert.AreEqual(1, table.Count);
            Assert.AreEqual("쉼표, 포함", table.Rows[0].GetString("Body"));
        }

        /// <summary>이스케이프된 따옴표는 리터럴 따옴표가 됩니다.</summary>
        [Test]
        public void 이스케이프된_따옴표는_리터럴이_된다()
        {
            CsvTable table = CsvReader.ReadTable("Id,Body\nA,\"그는 \"\"안녕\"\" 이라 했다\"\n");

            Assert.AreEqual("그는 \"안녕\" 이라 했다", table.Rows[0].GetString("Body"));
        }

        /// <summary>따옴표 안의 개행은 필드 내용으로 보존됩니다.</summary>
        [Test]
        public void 따옴표_안의_개행은_보존된다()
        {
            CsvTable table = CsvReader.ReadTable("Id,Body\nA,\"첫 줄\n둘째 줄\"\nB,짧음\n");

            Assert.AreEqual(2, table.Count);
            Assert.AreEqual("첫 줄\n둘째 줄", table.Rows[0].GetString("Body"));
            Assert.AreEqual("짧음", table.Rows[1].GetString("Body"));
        }

        /// <summary>BOM과 CRLF가 섞여 있어도 헤더가 깨지지 않습니다.</summary>
        [Test]
        public void BOM과_CRLF를_처리한다()
        {
            CsvTable table = CsvReader.ReadTable("﻿Id,Name\r\nA,가\r\n");

            Assert.AreEqual("Id", table.Headers[0]);
            Assert.AreEqual("가", table.Rows[0].GetString("Name"));
        }

        /// <summary>줄 번호는 헤더를 1행으로 세며, 여러 줄에 걸친 필드 뒤에도 어긋나지 않습니다.</summary>
        [Test]
        public void 줄_번호는_헤더를_1행으로_센다()
        {
            CsvTable table = CsvReader.ReadTable("Id,Body\nA,\"두\n줄\"\nB,한줄\n");

            Assert.AreEqual(2, table.Rows[0].LineNumber);
            Assert.AreEqual(4, table.Rows[1].LineNumber, "여러 줄 필드가 끝난 다음 줄이어야 합니다.");
        }

        /// <summary>첫 열이 빈 줄은 데이터 행으로 세지 않습니다.</summary>
        [Test]
        public void 첫_열이_빈_줄은_건너뛴다()
        {
            CsvTable table = CsvReader.ReadTable("Id,Name\nA,가\n,\nB,나\n");

            Assert.AreEqual(2, table.Count);
        }

        /// <summary>탭 구분 표를 읽습니다.</summary>
        [Test]
        public void 탭_구분자를_읽는다()
        {
            CsvTable table = CsvReader.ReadTable("Id\tName\nA\t가\n", CsvReader.Tab);

            Assert.AreEqual("가", table.Rows[0].GetString("Name"));
        }

        /// <summary>헤더 줄만 보고 구분자를 고릅니다.</summary>
        [Test]
        public void 구분자를_내용으로_판별한다()
        {
            Assert.AreEqual(CsvReader.Tab, CsvReader.DetectDelimiter("a\tb\tc\nx,y\n"));
            Assert.AreEqual(CsvReader.Comma, CsvReader.DetectDelimiter("a,b,c\n"));
        }

        /// <summary>확장자가 구분자를 정합니다.</summary>
        [Test]
        public void 확장자로_구분자를_정한다()
        {
            Assert.AreEqual(CsvReader.Tab, CsvReader.DelimiterForPath("Assets/Data/X.tsv"));
            Assert.AreEqual(CsvReader.Tab, CsvReader.DelimiterForPath("Assets/Data/X.TAB"));
            Assert.AreEqual(CsvReader.Comma, CsvReader.DelimiterForPath("Assets/Data/X.csv"));
        }

        /// <summary>숫자는 로케일과 무관하게 같은 값으로 읽힙니다.</summary>
        [Test]
        public void 숫자는_로케일_독립적으로_읽힌다()
        {
            CsvTable table = CsvReader.ReadTable("Id,Value\nA,1.5\n");

            Assert.AreEqual(1.5f, table.Rows[0].GetFloat("Value"), 0.0001f);
        }

        /// <summary>
        /// 예전부터 쓰던 <see cref="CsvReader.Read(string)"/>는 boxed 숫자와 대소문자 구분을 그대로 둡니다.
        /// 호출부의 동작이 조용히 바뀌지 않게 하는 것이 이 경로의 목적입니다.
        /// </summary>
        [Test]
        public void 레거시_경로는_동작을_유지한다()
        {
            var rows = CsvReader.Read("Id,MaxSpeed\nA,30\n");

            Assert.AreEqual(1, rows.Count);
            Assert.IsInstanceOf<int>(rows[0]["MaxSpeed"], "boxed int가 보존돼야 합니다.");
            Assert.IsFalse(rows[0].ContainsKey("maxSpeed"), "레거시 경로는 대소문자를 구분합니다.");
        }

        /// <summary>내용이 없으면 빈 표입니다.</summary>
        [Test]
        public void 헤더만_있거나_비면_행이_없다()
        {
            Assert.AreEqual(0, CsvReader.ReadTable(string.Empty).Count);
            Assert.AreEqual(0, CsvReader.ReadTable("Id,Name\n").Count);
        }
    }
}
