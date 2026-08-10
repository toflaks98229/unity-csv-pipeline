using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 굽기 규칙을 <b>메모리 게이트웨이</b> 위에서 확인합니다.
    /// 임시 폴더도 재임포트도 없어 검사끼리 얽힐 여지가 없고, 실행이 즉시 끝납니다.
    /// AssetDatabase 자체의 거동(지운 자리에 다시 만들기·직렬화 왕복)은 통합 검사가 봅니다.
    /// </summary>
    public sealed class CsvBakeUnitTests
    {
        private const string CsvPath = "Assets/Memory/CsvPipelineTests_Widgets.csv";
        private const string OutputFolder = "Assets/Memory/WidgetData";

        private const string Sample =
            "Id,Title,MaxSpeed,Stock,OwnerId,HP\n"
            + "Widget_A,첫 위젯,30,12,Player,100\n"
            + "Widget_B,\"둘째, 쉼표 포함\",7.5,3,,50\n";

        private MemoryAssetGateway _assets;
        private System.IDisposable _scope;

        /// <summary>메모리 게이트웨이를 끼우고 표 하나를 놓습니다.</summary>
        [SetUp]
        public void SetUp()
        {
            _assets = new MemoryAssetGateway().WithTable(CsvPath, Sample);
            _scope = CsvAssets.Use(_assets);
        }

        /// <summary>게이트웨이를 걷어내고 만든 객체를 정리합니다.</summary>
        [TearDown]
        public void TearDown()
        {
            _scope.Dispose();
            _assets.Dispose();
        }

        /// <summary>표를 굽습니다.</summary>
        /// <param name="text">바꿔 넣을 표 원문입니다. 비우면 그대로 씁니다.</param>
        /// <returns>임포트 리포트입니다.</returns>
        private CsvImportReport Bake(string text = null)
        {
            if (text != null) _assets.WithTable(CsvPath, text);
            return new CsvSchemaImportDefinition(CsvSchema.For(typeof(WidgetData))).Run(CsvPath);
        }

        /// <summary>구워진 에셋을 읽습니다.</summary>
        /// <param name="id">에셋 이름입니다.</param>
        /// <returns>찾은 에셋이거나 null입니다.</returns>
        private WidgetData Load(string id) => _assets.Get<WidgetData>($"{OutputFolder}/{id}.asset");

        // ====================================================================================================

        /// <summary>행마다 에셋이 생기고 값이 들어갑니다.</summary>
        [Test]
        public void 행마다_에셋이_생긴다()
        {
            CsvImportReport report = Bake();

            Assert.AreEqual(2, report.Created, report.Summary());
            Assert.IsFalse(report.HasErrors, report.Summary());

            WidgetData a = Load("Widget_A");
            Assert.IsNotNull(a, "산출물이 표 옆 폴더에 만들어져야 합니다.");
            Assert.AreEqual("첫 위젯", a.title);
            Assert.AreEqual(12, a.stock);
            Assert.AreEqual(100, a.health, "이름을 바꿔 지정한 HP 열이 붙어야 합니다.");
            Assert.AreEqual("Player", a.OwnerId, "직렬화되는 private 필드도 붙어야 합니다.");
        }

        /// <summary>값이 정수처럼 보여도 필드가 실수면 실수로 들어갑니다.</summary>
        [Test]
        public void 필드_타입이_값_모양을_이긴다()
        {
            Bake();

            Assert.AreEqual(30f, Load("Widget_A").maxSpeed, 0.0001f);
            Assert.AreEqual(7.5f, Load("Widget_B").maxSpeed, 0.0001f);
        }

        /// <summary>따옴표로 감싼 셀의 쉼표가 살아 있습니다.</summary>
        [Test]
        public void 따옴표로_감싼_셀이_보존된다()
        {
            Bake();

            Assert.AreEqual("둘째, 쉼표 포함", Load("Widget_B").title);
        }

        /// <summary>두 번 구워도 새로 만들지 않고 갱신합니다.</summary>
        [Test]
        public void 두_번_구워도_갱신만_한다()
        {
            Bake();

            CsvImportReport second = Bake();

            Assert.AreEqual(0, second.Created, second.Summary());
            Assert.AreEqual(2, second.Updated, second.Summary());
        }

        /// <summary>빈 셀은 기존 값을 지우지 않습니다. 인스펙터에서 저작한 값이 표 때문에 날아가면 안 됩니다.</summary>
        [Test]
        public void 빈_셀은_기존_값을_보존한다()
        {
            Bake();
            Assert.AreEqual("Player", Load("Widget_A").OwnerId);

            Bake("Id,Title,MaxSpeed,Stock,OwnerId,HP\nWidget_A,첫 위젯,30,12,,100\n");

            Assert.AreEqual("Player", Load("Widget_A").OwnerId, "빈 셀이 기존 값을 지우면 안 됩니다.");
        }

        /// <summary>표에서 사라진 행의 에셋은 아무도 참조하지 않으면 정리됩니다.</summary>
        [Test]
        public void 표에서_사라진_행의_에셋을_정리한다()
        {
            Bake();
            Assert.IsNotNull(Load("Widget_B"));

            CsvImportReport report = Bake("Id,Title,MaxSpeed,Stock,OwnerId,HP\nWidget_A,첫 위젯,30,12,Player,100\n");

            Assert.AreEqual(1, report.Deleted, report.Summary());
            Assert.IsNull(Load("Widget_B"));
            Assert.IsNotNull(Load("Widget_A"));
        }

        /// <summary>
        /// 참조가 남은 에셋은 표에서 사라져도 지우지 않습니다.
        /// 지우면 GUID가 사라져 git으로 파일을 되돌려도 배선이 돌아오지 않습니다.
        /// </summary>
        [Test]
        public void 참조가_남은_에셋은_지우지_않는다()
        {
            Bake();
            _assets.Referenced.Add($"{OutputFolder}/Widget_B.asset");

            LogAssert.Expect(LogType.Warning, new Regex("아직 참조 중이라 보존"));

            CsvImportReport report = Bake("Id,Title,MaxSpeed,Stock,OwnerId,HP\nWidget_A,첫 위젯,30,12,Player,100\n");

            Assert.AreEqual(0, report.Deleted, report.Summary());
            Assert.AreEqual(1, report.Preserved, report.Summary());
            Assert.IsNotNull(Load("Widget_B"), "참조가 남은 에셋은 남아 있어야 합니다.");
        }

        /// <summary>필수 열이 없으면 아무것도 반영하지 않습니다. 빈 셀로 굽는 것보다 멈추는 편이 낫습니다.</summary>
        [Test]
        public void 필수_열이_없으면_아무것도_굽지_않는다()
        {
            LogAssert.Expect(LogType.Error, new Regex("열 'HP'"));

            CsvImportReport report = Bake("Id,Title,MaxSpeed,Stock,OwnerId\nWidget_A,첫 위젯,30,12,Player\n");

            Assert.IsTrue(report.HasErrors, "빠진 HP 열이 오류로 보고돼야 합니다.");
            Assert.AreEqual(0, report.Created);
            Assert.IsNull(Load("Widget_A"));
        }

        /// <summary>식별자가 빈 행은 건너뛰고 이유를 남깁니다.</summary>
        [Test]
        public void 식별자가_빈_행은_건너뛴다()
        {
            LogAssert.Expect(LogType.Warning, new Regex("비어 있"));

            CsvImportReport report = Bake(
                "Id,Title,MaxSpeed,Stock,OwnerId,HP\n"
                + "Widget_A,첫 위젯,30,12,Player,100\n"
                + " ,이름 없음,1,1,,1\n");

            Assert.AreEqual(1, report.Created, report.Summary());
            Assert.AreEqual(1, report.Skipped, report.Summary());
        }

        /// <summary>미리보기는 아무것도 쓰지 않습니다. 이 기능의 전제입니다.</summary>
        [Test]
        public void 미리보기는_아무것도_쓰지_않는다()
        {
            CsvImportPlan plan = new CsvSchemaImportDefinition(CsvSchema.For(typeof(WidgetData))).Plan(CsvPath);

            Assert.AreEqual(2, plan.Count(CsvChangeKind.Create), plan.Summary());
            Assert.IsNull(Load("Widget_A"), "계획만 세웠는데 에셋이 생기면 안 됩니다.");
            Assert.AreEqual(0, _assets.SaveCount, "계획만 세웠는데 저장이 일어나면 안 됩니다.");
        }

        /// <summary>내보내면 원본의 헤더 표기를 그대로 쓰고, 다시 읽어도 같은 값이 나옵니다.</summary>
        [Test]
        public void 내보낸_표가_원본과_왕복한다()
        {
            Bake();

            string exported = CsvExporter.Build(CsvSchema.For(typeof(WidgetData)), out int rowCount);

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

        /// <summary>구운 뒤에는 저장이 한 번 일어납니다. 값이 메모리에만 남으면 안 됩니다.</summary>
        [Test]
        public void 구운_뒤에_저장한다()
        {
            Bake();

            Assert.AreEqual(1, _assets.SaveCount);
        }
    }
}
