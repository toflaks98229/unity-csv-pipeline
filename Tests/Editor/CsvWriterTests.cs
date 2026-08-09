using NUnit.Framework;

namespace CsvPipeline.Tests
{
    /// <summary>내보낸 표를 다시 읽었을 때 값이 그대로인지 검사합니다.</summary>
    public sealed class CsvWriterTests
    {
        /// <summary>감쌀 필요가 없는 값은 감싸지 않습니다.</summary>
        [Test]
        public void 평범한_값은_감싸지_않는다()
        {
            var writer = new CsvWriter();
            writer.WriteRow(new[] { "A", "가나다", "30" });

            Assert.AreEqual("A,가나다,30\n", writer.ToString());
        }

        /// <summary>구분자가 든 값은 큰따옴표로 감쌉니다.</summary>
        [Test]
        public void 구분자가_든_값을_감싼다()
        {
            var writer = new CsvWriter();
            writer.WriteRow(new[] { "A", "쉼표, 포함" });

            Assert.AreEqual("A,\"쉼표, 포함\"\n", writer.ToString());
        }

        /// <summary>따옴표는 두 번 적어 이스케이프합니다.</summary>
        [Test]
        public void 따옴표를_이스케이프한다()
        {
            var writer = new CsvWriter();
            writer.WriteRow(new[] { "그는 \"안녕\"" });

            Assert.AreEqual("\"그는 \"\"안녕\"\"\"\n", writer.ToString());
        }

        /// <summary>탭 구분으로도 씁니다.</summary>
        [Test]
        public void 탭_구분으로_쓴다()
        {
            var writer = new CsvWriter(CsvReader.Tab);
            writer.WriteRow(new[] { "A", "가" });

            Assert.AreEqual("A\t가\n", writer.ToString());
        }

        /// <summary>쓴 것을 다시 읽으면 원래 값이 나옵니다. 왕복의 최소 조건입니다.</summary>
        [Test]
        public void 쓰고_다시_읽으면_같다()
        {
            var writer = new CsvWriter();
            writer.WriteRow(new[] { "Id", "Body" });
            writer.WriteRow(new[] { "A", "쉼표, 그리고 \"따옴표\"\n와 개행" });

            CsvTable table = CsvReader.ReadTable(writer.ToString());

            Assert.AreEqual(1, table.Count);
            Assert.AreEqual("쉼표, 그리고 \"따옴표\"\n와 개행", table.Rows[0].GetString("Body"));
        }
    }
}
