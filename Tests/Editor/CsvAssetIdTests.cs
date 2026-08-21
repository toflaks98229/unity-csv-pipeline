using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 식별자를 <b>파일 이름으로 쓸 수 있는지</b> 가리는 규칙을 봅니다.
    /// 이 규칙이 없으면 쓸 수 없는 이름의 행은 경고도 건너뜀도 없이, 정상 경로를 타다가
    /// 저장소 쪽에서 조용히 실패합니다. 사람이 알아챌 방법이 없는 자리입니다.
    /// </summary>
    public sealed class CsvAssetIdTests
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

        // ====================================================================================================
        // 규칙 자체
        // ====================================================================================================

        /// <summary>평범한 식별자는 그대로 통과합니다.</summary>
        [TestCase("Widget_A")]
        [TestCase("quest.night-watch")]
        [TestCase("아이템_검")]
        [TestCase("A")]
        public void 쓸_수_있는_이름은_통과한다(string id)
            => Assert.IsNull(CsvAssetId.Reject(id), $"'{id}'는 파일 이름으로 쓸 수 있습니다.");

        /// <summary>
        /// 경로 구분자와 금지 문자는 거절합니다. 목록을 운영체제에서 가져오지 않고 고정해 둔 것은,
        /// 그러지 않으면 <b>윈도우에서 거절된 표가 macOS에서는 통과해</b> 사람에 따라 결과가 갈리기 때문입니다.
        /// </summary>
        [TestCase("Item/Sword")]
        [TestCase("Item\\Sword")]
        [TestCase("Q:01")]
        [TestCase("A*")]
        [TestCase("B?")]
        [TestCase("<C>")]
        [TestCase("D|E")]
        [TestCase("F\"G")]
        public void 파일_이름에_쓸_수_없는_문자는_거절한다(string id)
            => Assert.IsNotNull(CsvAssetId.Reject(id), $"'{id}'는 거절돼야 합니다.");

        /// <summary>
        /// 앞뒤 공백은 거절합니다. 눈에 보이지 않아 표에서는 같아 보이는데 파일로는 갈립니다.
        /// </summary>
        [TestCase(" Widget_A")]
        [TestCase("Widget_A ")]
        public void 앞뒤_공백은_거절한다(string id)
            => StringAssert.Contains("whitespace", CsvAssetId.Reject(id));

        /// <summary>마침표로 끝나면 거절합니다. 윈도우가 그 마침표를 떼어 다른 이름으로 만듭니다.</summary>
        [Test]
        public void 마침표로_끝나면_거절한다()
            => Assert.IsNotNull(CsvAssetId.Reject("Widget_A."));

        /// <summary>윈도우가 장치 이름으로 잡아 둔 낱말은 확장자를 붙여도 파일이 되지 않습니다.</summary>
        [TestCase("CON")]
        [TestCase("nul")]
        [TestCase("COM1")]
        [TestCase("LPT9.data")]
        public void 예약된_장치_이름은_거절한다(string id)
            => Assert.IsNotNull(CsvAssetId.Reject(id), $"'{id}'는 거절돼야 합니다.");

        /// <summary>이름이 지나치게 길면 경로가 넘쳐 에셋을 만들 수 없습니다.</summary>
        [Test]
        public void 너무_긴_이름은_거절한다()
            => Assert.IsNotNull(CsvAssetId.Reject(new string('A', 200)));

        /// <summary>보이지 않는 제어 문자도 거절합니다.</summary>
        [Test]
        public void 제어_문자는_거절한다()
            => Assert.IsNotNull(CsvAssetId.Reject("Widget\tA"));

        /// <summary>
        /// 비어 있는 식별자는 여기서 가리지 않습니다. 표를 잘못 적은 것이 아니라 행이 빈 것이라,
        /// 호출부가 저마다의 안내로 먼저 거릅니다.
        /// </summary>
        [Test]
        public void 빈_식별자는_여기서_가리지_않는다()
        {
            Assert.IsNull(CsvAssetId.Reject(null));
            Assert.IsNull(CsvAssetId.Reject(string.Empty));
        }

        // ====================================================================================================
        // 굽기와 미리보기
        // ====================================================================================================

        /// <summary>쓸 수 없는 이름의 행은 건너뛰고, 왜 건너뛰는지 남깁니다.</summary>
        [Test]
        public void 쓸_수_없는_이름의_행은_건너뛴다()
        {
            LogAssert.Expect(LogType.Warning, new Regex("cannot be used as one"));

            CsvImportReport report = Bake(
                "Widget_A,첫 위젯,30,12,Player,100\n"
                + "Widget/B,쓸 수 없는 이름,7.5,3,,50\n");

            Assert.AreEqual(1, report.Created, report.Summary());
            Assert.AreEqual(1, report.Skipped, report.Summary());
            Assert.IsNotNull(_assets.Get<WidgetData>($"{OutputFolder}/Widget_A.asset"));
        }

        /// <summary>미리보기도 같은 행을 건너뛰고, 목록 위로 올라오게 표시합니다.</summary>
        [Test]
        public void 미리보기도_같은_행을_건너뛴다()
        {
            CsvImportPlan plan = Plan(
                "Widget_A,첫 위젯,30,12,Player,100\n"
                + "Widget/B,쓸 수 없는 이름,7.5,3,,50\n");

            Assert.AreEqual(1, plan.Count(CsvChangeKind.Create), plan.Summary());
            Assert.AreEqual(1, plan.Count(CsvChangeKind.Skip), plan.Summary());
            Assert.AreEqual(1, plan.Issues.Count, "왜 건너뛰는지가 계획에 올라와야 합니다.");
            Assert.AreEqual(CsvPlanState.Problem, CsvPlanStatus.Of(plan));
        }
    }
}
