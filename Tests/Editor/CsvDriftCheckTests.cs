using System.Collections.Generic;
using NUnit.Framework;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 표를 고치고 <b>굽기를 잊은 채 커밋한 것</b>을 잡아내는지 봅니다.
    /// <para>
    /// 이 사고는 사람 눈으로 잡기 어렵습니다 — 표 파일이 바뀐 것은 diff 에 보이지만,
    /// 산출물이 안 바뀐 것은 <b>diff 에 보이지 않습니다.</b> 없는 것은 눈에 띄지 않으니까요.
    /// </para>
    /// </summary>
    public sealed class CsvDriftCheckTests
    {
        private const string CsvPath = "Assets/Memory/CsvPipelineTests_Widgets.csv";

        private const string Header = "Id,Title,MaxSpeed,Stock,OwnerId,HP\n";
        private const string OneRow = "Widget_A,첫 위젯,30,12,Player,100\n";
        private const string TwoRows = OneRow + "Widget_B,둘째 위젯,7.5,3,,50\n";

        private MemoryAssetGateway _assets;
        private System.IDisposable _scope;

        /// <summary>메모리 게이트웨이를 끼웁니다.</summary>
        [SetUp]
        public void SetUp()
        {
            _assets = new MemoryAssetGateway();
            _scope = CsvAssets.Use(_assets);
        }

        /// <summary>게이트웨이를 걷어냅니다.</summary>
        [TearDown]
        public void TearDown()
        {
            _scope.Dispose();
            _assets.Dispose();
        }

        /// <summary>이 검사가 볼 임포터입니다. 프로젝트 전체를 찾으면 다른 픽스처까지 딸려 옵니다.</summary>
        /// <returns>임포터 하나가 든 목록입니다.</returns>
        private static IEnumerable<CsvImportDefinition> Only()
            => new List<CsvImportDefinition> { new CsvSchemaImportDefinition(CsvSchema.For(typeof(WidgetData))) };

        /// <summary>표를 놓습니다.</summary>
        /// <param name="body">헤더 뒤에 붙일 데이터 행들입니다.</param>
        private void Table(string body) => _assets.WithTable(CsvPath, Header + body);

        /// <summary>표를 굽습니다.</summary>
        private void Bake() => new CsvSchemaImportDefinition(CsvSchema.For(typeof(WidgetData))).Run(CsvPath);

        // ====================================================================================================

        /// <summary>표와 산출물이 같으면 어긋난 것이 없습니다.</summary>
        [Test]
        public void 구워_둔_표는_깨끗하다()
        {
            Table(TwoRows);
            Bake();

            CsvDriftReport report = CsvDriftCheck.Inspect(Only());

            Assert.IsTrue(report.IsClean, report.Describe());
            Assert.AreEqual(0, report.ExitCode);
            Assert.AreEqual(1, report.Checked);
        }

        /// <summary>표를 고치고 굽지 않았으면 어긋난 것으로 잡습니다.</summary>
        [Test]
        public void 고치고_굽지_않으면_잡힌다()
        {
            Table(OneRow);
            Bake();

            Table(TwoRows);   // 표만 고치고 굽지 않았습니다.

            CsvDriftReport report = CsvDriftCheck.Inspect(Only());

            Assert.IsFalse(report.IsClean);
            Assert.AreEqual(1, report.ExitCode, "배치 실행이 종료 코드로 답할 수 있어야 합니다.");
            Assert.AreEqual(CsvDriftKind.Changed, report.Drifted[0].Kind);
        }

        /// <summary>한 번도 굽지 않은 표도 어긋난 것입니다.</summary>
        [Test]
        public void 한_번도_굽지_않은_표도_잡힌다()
        {
            Table(TwoRows);

            CsvDriftReport report = CsvDriftCheck.Inspect(Only());

            Assert.IsFalse(report.IsClean);
            Assert.AreEqual(CsvDriftKind.Changed, report.Drifted[0].Kind);
        }

        /// <summary>표 자체에 손볼 것이 있으면 '문제'로 갈라 봅니다. 굽는다고 풀리지 않기 때문입니다.</summary>
        [Test]
        public void 표에_문제가_있으면_문제로_잡는다()
        {
            Table("Widget_A,첫 위젯,30,12,Player,100\nWidget_A,겹친 식별자,40,20,Player,120\n");

            CsvDriftReport report = CsvDriftCheck.Inspect(Only());

            Assert.AreEqual(CsvDriftKind.Problem, report.Drifted[0].Kind);
            StringAssert.Contains("이미 쓰였습니다", report.Drifted[0].Reason());
        }

        /// <summary>표 파일 자체가 없으면 어긋났는지조차 알 수 없습니다. 그것도 답입니다.</summary>
        [Test]
        public void 표를_찾지_못하면_못_읽음으로_잡는다()
        {
            CsvDriftReport report = CsvDriftCheck.Inspect(Only());

            Assert.AreEqual(CsvDriftKind.Unreadable, report.Drifted[0].Kind);
            Assert.AreEqual(1, report.ExitCode);
        }

        /// <summary>
        /// 판정이 창의 것과 같습니다. 화면에서 '바뀌는 것 없음'인 표가 CI 에서 실패하면
        /// <b>둘 중 하나는 거짓말</b>이고, 사람은 어느 쪽을 믿을지 알 수 없습니다.
        /// </summary>
        [Test]
        public void 판정이_창과_같다()
        {
            Table(TwoRows);
            Bake();

            CsvImportPlan plan = new CsvSchemaImportDefinition(CsvSchema.For(typeof(WidgetData))).Plan(CsvPath);

            bool driftSaysClean = CsvDriftCheck.KindOf(plan) == CsvDriftKind.None;
            bool windowSaysClean = !CsvTableFilter.MatchesView(plan, CsvTableView.Changed);

            Assert.AreEqual(windowSaysClean, driftSaysClean);
        }

        /// <summary>결과는 사람이 읽을 수 있어야 합니다. CI 로그에 그대로 남는 것이 이것뿐입니다.</summary>
        [Test]
        public void 결과가_사람이_읽을_수_있다()
        {
            Table(OneRow);
            Bake();
            Table(TwoRows);

            string text = CsvDriftCheck.Inspect(Only()).Describe();

            StringAssert.Contains("CsvPipelineTests_Widgets.csv", text, "어느 표가 어긋났는지 나와야 합니다.");
            StringAssert.Contains("WidgetData", text, "무엇을 굽는 표인지 나와야 합니다.");
        }

        /// <summary>깨끗할 때도 무엇을 봤는지 말합니다. 아무 말도 없으면 돌긴 돈 것인지 알 수 없습니다.</summary>
        [Test]
        public void 깨끗해도_무엇을_봤는지_말한다()
        {
            Table(TwoRows);
            Bake();

            StringAssert.Contains("1", CsvDriftCheck.Inspect(Only()).Describe());
        }
    }
}
