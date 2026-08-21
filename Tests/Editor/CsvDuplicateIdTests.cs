using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 식별자가 겹쳤을 때 <b>말없이 덮어쓰지 않는지</b> 봅니다.
    /// 겹친 식별자는 두 행이 같은 파일을 가리켜 앞 행의 값이 사라지는데, 건수로는
    /// '생성 1 / 갱신 1'이 되어 정상 임포트와 구분되지 않습니다. 그래서 <b>경고가 나는지</b>를 검사합니다.
    /// </summary>
    public sealed class CsvDuplicateIdTests
    {
        private const string CsvPath = "Assets/Memory/CsvPipelineTests_Widgets.csv";
        private const string OutputFolder = "Assets/Memory/WidgetData";

        private const string Header = "Id,Title,MaxSpeed,Stock,OwnerId,HP\n";

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

        /// <summary>표를 놓고 굽습니다.</summary>
        /// <param name="body">헤더 뒤에 붙일 데이터 행들입니다.</param>
        /// <returns>임포트 리포트입니다.</returns>
        private CsvImportReport Bake(string body)
        {
            _assets.WithTable(CsvPath, Header + body);
            return new CsvSchemaImportDefinition(CsvSchema.For(typeof(WidgetData))).Run(CsvPath);
        }

        /// <summary>표를 놓고 계획만 세웁니다.</summary>
        /// <param name="body">헤더 뒤에 붙일 데이터 행들입니다.</param>
        /// <returns>계획입니다.</returns>
        private CsvImportPlan Plan(string body)
        {
            _assets.WithTable(CsvPath, Header + body);
            return new CsvSchemaImportDefinition(CsvSchema.For(typeof(WidgetData))).Plan(CsvPath);
        }

        /// <summary>구워진 에셋을 읽습니다.</summary>
        /// <param name="id">에셋 이름입니다.</param>
        /// <returns>찾은 에셋이거나 null입니다.</returns>
        private WidgetData Load(string id) => _assets.Get<WidgetData>($"{OutputFolder}/{id}.asset");

        // ====================================================================================================
        // 장부 자체
        // ====================================================================================================

        /// <summary>같은 자리를 두 번 차지하려 하면 두 번째가 거절되고, 앞선 자리를 알려 줍니다.</summary>
        [Test]
        public void 같은_자리는_한_번만_차지한다()
        {
            var claims = new CsvIdClaims();

            Assert.IsTrue(claims.TryClaim("Assets/A.asset", "A", 2, out _));
            Assert.IsFalse(claims.TryClaim("Assets/A.asset", "A", 7, out CsvIdClaim taken));

            Assert.AreEqual(2, taken.Line, "앞서 차지한 줄 번호를 돌려줘야 합니다.");
            Assert.AreEqual(1, claims.Count, "겹친 자리를 새로 세면 안 됩니다.");
        }

        /// <summary>서로 다른 자리는 겹치지 않습니다. 없는 문제를 만들어 내면 경고가 소음이 됩니다.</summary>
        [Test]
        public void 다른_자리는_겹치지_않는다()
        {
            var claims = new CsvIdClaims();

            Assert.IsTrue(claims.TryClaim("Assets/A.asset", "A", 2, out _));
            Assert.IsTrue(claims.TryClaim("Assets/B.asset", "B", 3, out _));

            Assert.AreEqual(2, claims.Count);
        }

        /// <summary>
        /// 대소문자만 다른 식별자도 겹침입니다. 윈도우에서는 같은 파일이 되고 macOS·리눅스에서는
        /// 다른 파일이 되어, 그대로 두면 <b>어느 PC에서 구웠느냐에 따라 결과가 갈립니다.</b>
        /// </summary>
        [Test]
        public void 대소문자만_다른_식별자도_겹침이다()
        {
            var claims = new CsvIdClaims();

            Assert.IsTrue(claims.TryClaim("Assets/Sword.asset", "Sword", 2, out _));
            Assert.IsFalse(claims.TryClaim("Assets/sword.asset", "sword", 3, out CsvIdClaim taken));

            StringAssert.Contains("letter case", CsvIdClaims.Describe("sword", taken),
                                  "덮어쓴다고만 하면 사람이 원인을 찾지 못합니다.");
        }

        /// <summary>가릴 값이 없으면 겹침을 따지지 않습니다.</summary>
        [Test]
        public void 자리를_가릴_수_없으면_따지지_않는다()
        {
            var claims = new CsvIdClaims();

            Assert.IsTrue(claims.TryClaim(null, null, 2, out _));
            Assert.IsTrue(claims.TryClaim(string.Empty, string.Empty, 3, out _));
            Assert.AreEqual(0, claims.Count);
        }

        // ====================================================================================================
        // 굽기
        // ====================================================================================================

        /// <summary>겹친 식별자를 만나면 경고합니다. 값은 뒤 행이 이기고, 앞 행의 값은 사라집니다.</summary>
        [Test]
        public void 겹친_식별자를_경고한다()
        {
            LogAssert.Expect(LogType.Warning, new Regex("was already used"));

            CsvImportReport report = Bake(
                "Widget_A,첫 위젯,30,12,Player,100\n"
                + "Widget_A,덮어쓴 위젯,40,20,Player,120\n");

            Assert.AreEqual(1, report.Created, report.Summary());
            Assert.AreEqual(1, report.Updated, report.Summary());
            Assert.AreEqual("덮어쓴 위젯", Load("Widget_A").title, "뒤 행이 이깁니다.");
        }

        /// <summary>겹치지 않는 표는 경고하지 않습니다.</summary>
        [Test]
        public void 겹치지_않으면_조용하다()
        {
            CsvImportReport report = Bake(
                "Widget_A,첫 위젯,30,12,Player,100\n"
                + "Widget_B,둘째 위젯,7.5,3,,50\n");

            Assert.AreEqual(2, report.Created, report.Summary());
            Assert.AreEqual(0, report.Issues.Count, "멀쩡한 표에 경고가 붙으면 안 됩니다.");
        }

        // ====================================================================================================
        // 미리보기
        // ====================================================================================================

        /// <summary>
        /// 미리보기도 같은 겹침을 알립니다. 굽기만 알면 <b>적용 전에는 알 길이 없어</b>
        /// 미리보기가 있으나 마나가 됩니다.
        /// </summary>
        [Test]
        public void 미리보기가_겹침을_먼저_알린다()
        {
            CsvImportPlan plan = Plan(
                "Widget_A,첫 위젯,30,12,Player,100\n"
                + "Widget_A,덮어쓴 위젯,40,20,Player,120\n");

            Assert.AreEqual(1, plan.Issues.Count, "겹침이 계획에 올라와야 합니다.");
            Assert.AreEqual(CsvIssueSeverity.Warning, plan.Issues[0].Severity);
            Assert.AreEqual(CsvPlanState.Problem, CsvPlanStatus.Of(plan),
                            "겹친 표는 목록 위로 올라와야 합니다.");
        }

        /// <summary>겹치지 않는 표는 계획에도 아무 문제가 없습니다.</summary>
        [Test]
        public void 겹치지_않는_표는_계획도_깨끗하다()
        {
            CsvImportPlan plan = Plan(
                "Widget_A,첫 위젯,30,12,Player,100\n"
                + "Widget_B,둘째 위젯,7.5,3,,50\n");

            Assert.AreEqual(0, plan.Issues.Count, plan.Summary());
            Assert.AreEqual(CsvPlanState.Changed, CsvPlanStatus.Of(plan), plan.Summary());
        }
    }
}
