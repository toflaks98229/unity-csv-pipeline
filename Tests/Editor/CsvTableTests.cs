using NUnit.Framework;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 열 조회가 대소문자를 흡수하는지 검사합니다.
    /// 표는 PascalCase, 필드는 camelCase로 적히므로 이걸 가리면 자동 연결이 하나도 붙지 않습니다.
    /// </summary>
    public sealed class CsvTableTests
    {
        private static CsvTable Table() => CsvReader.ReadTable("Id,MaxSpeed,TrunkCapacity\nA,30,12\n");

        /// <summary>정확히 같은 이름으로 찾습니다.</summary>
        [Test]
        public void 열을_정확한_이름으로_찾는다()
        {
            Assert.IsTrue(Table().HasColumn("MaxSpeed"));
        }

        /// <summary>필드 이름(camelCase)으로도 표의 열(PascalCase)을 찾습니다.</summary>
        [Test]
        public void 열을_대소문자_무시로_찾는다()
        {
            CsvTable table = Table();

            Assert.IsTrue(table.HasColumn("maxSpeed"), "camelCase 필드 이름으로 찾을 수 있어야 합니다.");
            Assert.IsTrue(table.HasColumn("MAXSPEED"));
        }

        /// <summary>없는 열은 없다고 답합니다.</summary>
        [Test]
        public void 없는_열은_찾지_못한다()
        {
            Assert.IsFalse(Table().HasColumn("Nope"));
            Assert.IsFalse(Table().HasColumn(null));
        }

        /// <summary>셀 조회도 같은 규칙을 따릅니다.</summary>
        [Test]
        public void 셀도_대소문자_무시로_읽는다()
        {
            CsvRow row = Table().Rows[0];

            Assert.AreEqual("30", row.GetString("maxSpeed"));
            Assert.AreEqual("30", row.GetString("MaxSpeed"));
            Assert.IsTrue(row.HasColumn("trunkcapacity"));
        }

        /// <summary>빠진 열만 골라 돌려줍니다.</summary>
        [Test]
        public void 빠진_열을_보고한다()
        {
            var missing = Table().FindMissingColumns(new[] { "Id", "Missing", "Also" });

            Assert.AreEqual(2, missing.Count);
            CollectionAssert.Contains(missing, "Missing");
            CollectionAssert.DoesNotContain(missing, "Id");
        }

        /// <summary>대소문자만 다른 것은 빠진 열이 아닙니다.</summary>
        [Test]
        public void 대소문자만_다른_열은_빠진_것이_아니다()
        {
            Assert.AreEqual(0, Table().FindMissingColumns(new[] { "id", "maxspeed" }).Count);
        }

        /// <summary>공백·밑줄·하이픈 차이는 오타로 보고 원래 이름을 알려 줍니다.</summary>
        [Test]
        public void 구분기호만_다른_열을_오타로_알려_준다()
        {
            CsvTable table = Table();

            Assert.AreEqual("MaxSpeed", table.FindSimilarColumn("Max_Speed"));
            Assert.AreEqual("MaxSpeed", table.FindSimilarColumn("max speed"));
            Assert.AreEqual("MaxSpeed", table.FindSimilarColumn("MAX-SPEED"));
            Assert.IsNull(table.FindSimilarColumn("Torque"));
        }

        /// <summary>뒤쪽 셀이 잘린 행에서도 열 자체는 존재합니다.</summary>
        [Test]
        public void 셀이_잘린_행과_열_존재는_다르다()
        {
            CsvTable table = CsvReader.ReadTable("Id,Name,Note\nA,가\n");
            CsvRow row = table.Rows[0];

            Assert.IsTrue(row.HasColumn("Note"), "표에는 열이 있습니다.");
            Assert.IsFalse(row.Has("Note"), "이 행에는 셀이 없습니다.");
            Assert.AreEqual(string.Empty, row.GetString("Note"));
        }
    }
}
