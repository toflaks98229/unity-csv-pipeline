using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 표 → 에셋 → 표의 전 구간을 실제로 돌립니다.
    /// 리플렉션 연결·에셋 생성·정리·내보내기는 컴파일로 보증되지 않는 부분이라 여기서만 확인됩니다.
    /// </summary>
    public sealed class CsvRoundTripTests
    {
        private const string TempFolder = "Assets/CsvPipelineTests_Temp";
        private const string CsvFileName = "CsvPipelineTests_Widgets.csv";
        private const string OutputFolder = TempFolder + "/WidgetData";

        private static string CsvAssetPath => $"{TempFolder}/{CsvFileName}";

        private static readonly string Sample =
            "Id,Title,MaxSpeed,Stock,OwnerId,HP\n"
            + "Widget_A,첫 위젯,30,12,Player,100\n"
            + "Widget_B,\"둘째, 쉼표 포함\",7.5,3,,50\n";

        /// <summary>표 하나를 임시 폴더에 놓습니다.</summary>
        [SetUp]
        public void SetUp()
        {
            // 실패 경로를 검사할 때 리포트가 LogError를 내므로, 그것 때문에 테스트가 깨지지 않게 합니다.
            LogAssert.ignoreFailingMessages = true;

            Directory.CreateDirectory(Path.GetFullPath(TempFolder));
            WriteCsv(Sample);
        }

        /// <summary>만든 것을 모두 지웁니다.</summary>
        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;

            AssetDatabase.DeleteAsset(TempFolder);
            AssetDatabase.Refresh();
        }

        /// <summary>표 내용을 임시 폴더에 기록하고 임포트합니다.</summary>
        /// <param name="text">기록할 표 원문입니다.</param>
        private static void WriteCsv(string text)
        {
            File.WriteAllText(Path.GetFullPath(CsvAssetPath), text, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(CsvAssetPath, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>산출물을 지워 생성 경로부터 다시 확인할 수 있게 합니다.</summary>
        private static void ClearOutput()
        {
            if (AssetDatabase.IsValidFolder(OutputFolder)) AssetDatabase.DeleteAsset(OutputFolder);
        }

        /// <summary>이 표의 스키마를 만듭니다.</summary>
        /// <returns>스키마입니다.</returns>
        private static CsvSchema Schema() => CsvSchema.For(typeof(WidgetData));

        /// <summary>표를 굽고 결과 리포트를 돌려줍니다.</summary>
        /// <returns>임포트 리포트입니다.</returns>
        private static CsvImportReport Bake()
            => new CsvSchemaImportDefinition(Schema()).Run(CsvAssetPath);

        /// <summary>구워진 에셋을 읽습니다.</summary>
        /// <param name="id">에셋 이름입니다.</param>
        /// <returns>찾은 에셋이거나 null입니다.</returns>
        private static WidgetData Load(string id)
            => AssetDatabase.LoadAssetAtPath<WidgetData>($"{OutputFolder}/{id}.asset");

        // ====================================================================================================

        /// <summary>행마다 에셋이 생기고 값이 들어갑니다.</summary>
        [Test]
        public void 행마다_에셋이_생긴다()
        {
            ClearOutput();
            CsvImportReport report = Bake();

            Assert.IsNotNull(report);
            Assert.AreEqual(2, report.Created, report.Summary());
            Assert.IsFalse(report.HasErrors);

            WidgetData a = Load("Widget_A");
            Assert.IsNotNull(a, "산출물 폴더가 표 옆에 만들어져야 합니다.");
            Assert.AreEqual("첫 위젯", a.title);
            Assert.AreEqual(12, a.stock);
            Assert.AreEqual(100, a.health, "이름을 바꿔 지정한 HP 열이 붙어야 합니다.");
            Assert.AreEqual("Player", a.OwnerId, "직렬화되는 private 필드도 붙어야 합니다.");
        }

        /// <summary>
        /// 값이 정수처럼 보여도 필드가 실수면 실수로 들어갑니다.
        /// 값만 보고 타입을 추론했다면 틀렸을 자리입니다.
        /// </summary>
        [Test]
        public void 필드_타입이_값_모양을_이긴다()
        {
            ClearOutput();
            Bake();

            Assert.AreEqual(30f, Load("Widget_A").maxSpeed, 0.0001f);
            Assert.AreEqual(7.5f, Load("Widget_B").maxSpeed, 0.0001f);
        }

        /// <summary>따옴표로 감싼 셀의 쉼표가 살아 있습니다.</summary>
        [Test]
        public void 따옴표로_감싼_셀이_보존된다()
        {
            ClearOutput();
            Bake();

            Assert.AreEqual("둘째, 쉼표 포함", Load("Widget_B").title);
        }

        /// <summary>두 번 구워도 새로 만들지 않고 갱신합니다.</summary>
        [Test]
        public void 두_번_구워도_갱신만_한다()
        {
            ClearOutput();
            Bake();

            CsvImportReport second = Bake();

            Assert.AreEqual(0, second.Created, second.Summary());
            Assert.AreEqual(2, second.Updated, second.Summary());
        }

        /// <summary>표에서 사라진 행의 에셋은 아무도 참조하지 않으면 정리됩니다.</summary>
        [Test]
        public void 표에서_사라진_행의_에셋을_정리한다()
        {
            ClearOutput();
            Bake();
            Assert.IsNotNull(Load("Widget_B"));

            WriteCsv("Id,Title,MaxSpeed,Stock,OwnerId,HP\nWidget_A,첫 위젯,30,12,Player,100\n");
            CsvImportReport report = Bake();

            Assert.AreEqual(1, report.Deleted, report.Summary());
            Assert.IsNull(Load("Widget_B"));
            Assert.IsNotNull(Load("Widget_A"));
        }

        /// <summary>필수 열이 없으면 아무것도 반영하지 않습니다. 빈 셀로 굽는 것보다 멈추는 편이 낫습니다.</summary>
        [Test]
        public void 필수_열이_없으면_아무것도_굽지_않는다()
        {
            ClearOutput();
            WriteCsv("Id,Title,MaxSpeed,Stock,OwnerId\nWidget_A,첫 위젯,30,12,Player\n");

            CsvImportReport report = Bake();

            Assert.IsTrue(report.HasErrors, "빠진 HP 열이 오류로 보고돼야 합니다.");
            Assert.AreEqual(0, report.Created);
            Assert.IsNull(Load("Widget_A"));
        }

        /// <summary>식별자가 빈 행은 건너뛰고 이유를 남깁니다.</summary>
        [Test]
        public void 식별자가_빈_행은_건너뛴다()
        {
            ClearOutput();
            WriteCsv("Id,Title,MaxSpeed,Stock,OwnerId,HP\n"
                   + "Widget_A,첫 위젯,30,12,Player,100\n"
                   + " ,이름 없음,1,1,,1\n");

            CsvImportReport report = Bake();

            Assert.AreEqual(1, report.Created, report.Summary());
            Assert.AreEqual(1, report.Skipped, report.Summary());
        }

        /// <summary>내보내면 원본의 헤더 표기를 그대로 쓰고, 다시 읽어도 같은 값이 나옵니다.</summary>
        [Test]
        public void 내보낸_표가_원본과_왕복한다()
        {
            ClearOutput();
            Bake();

            string exported = CsvExporter.Build(Schema(), out int rowCount);

            Assert.IsNotNull(exported);
            Assert.AreEqual(2, rowCount);

            CsvTable table = CsvReader.ReadTable(exported);
            CollectionAssert.AreEqual(
                new[] { "Id", "Title", "MaxSpeed", "Stock", "OwnerId", "HP" }, table.Headers,
                "필드 이름이 아니라 원본 표기를 써야 시트와 헤더가 어긋나지 않습니다.");

            CsvRow first = table.Rows[0];
            Assert.AreEqual("Widget_A", first.GetString("Id"));
            Assert.AreEqual("첫 위젯", first.GetString("Title"));
            Assert.AreEqual(100, first.GetInt("HP"));
            Assert.AreEqual("둘째, 쉼표 포함", table.Rows[1].GetString("Title"));
        }
    }
}
