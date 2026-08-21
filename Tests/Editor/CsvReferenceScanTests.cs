using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 참조를 <b>조사할 수 없을 때</b> 정리가 멈추는지 봅니다.
    /// <para>
    /// 이 패키지의 참조 조사는 씬·프리팹·에셋을 글자로 읽어 GUID를 찾습니다. 프로젝트의
    /// Asset Serialization이 Force Text가 아니면 GUID가 글자로 없어 <b>무엇을 물어도 "참조 없음"</b>이
    /// 나옵니다. 그 답을 그대로 믿으면 씬이 쓰고 있는 에셋이 경고 없이 사라집니다.
    /// </para>
    /// <para>
    /// 이 검사가 필요한 이유가 하나 더 있습니다 — <b>개발에 쓰는 프로젝트가 Force Text라면
    /// 아무리 써 봐도 이 사고는 드러나지 않습니다.</b> 사람의 손으로는 확인할 수 없는 자리입니다.
    /// </para>
    /// </summary>
    public sealed class CsvReferenceScanTests
    {
        private const string CsvPath = "Assets/Memory/CsvPipelineTests_Widgets.csv";
        private const string OutputFolder = "Assets/Memory/WidgetData";

        private const string Header = "Id,Title,MaxSpeed,Stock,OwnerId,HP\n";
        private const string TwoRows = "Widget_A,첫 위젯,30,12,Player,100\nWidget_B,둘째 위젯,7.5,3,,50\n";
        private const string OneRow = "Widget_A,첫 위젯,30,12,Player,100\n";

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

        /// <summary>평소에는 그대로 정리합니다. 아래 검사들이 정리 자체를 막아 버린 것이 아님을 못 박습니다.</summary>
        [Test]
        public void 조사할_수_있으면_그대로_지운다()
        {
            Bake(TwoRows);

            CsvImportReport report = Bake(OneRow);

            Assert.AreEqual(1, report.Deleted, report.Summary());
            Assert.AreEqual(0, report.Preserved, report.Summary());
            Assert.IsNull(Load("Widget_B"), "표에서 사라진 산출물은 지워져야 합니다.");
        }

        /// <summary>
        /// 조사할 수 없으면 <b>하나도 지우지 않습니다.</b> "참조가 없다"는 틀린 답을 받는 것보다
        /// 지울 수 있는 것이 없다고 답하는 편이 낫습니다.
        /// </summary>
        [Test]
        public void 조사할_수_없으면_지우지_않는다()
        {
            Bake(TwoRows);
            _assets.WithReferenceScanBlocked("검사용: 참조를 조사할 수 없습니다.");

            LogAssert.Expect(LogType.Warning, new Regex("지우지 않았습니다"));

            CsvImportReport report = Bake(OneRow);

            Assert.AreEqual(0, report.Deleted, report.Summary());
            Assert.AreEqual(1, report.Preserved, report.Summary());
            Assert.IsNotNull(Load("Widget_B"), "조사할 수 없으면 남겨야 합니다.");
        }

        /// <summary>
        /// 미리보기도 삭제를 올리지 않고, <b>왜 못 지우는지</b>를 함께 알립니다.
        /// 그냥 보존으로만 적으면 안전장치가 꺼져 있다는 사실이 "잘 지켜지고 있다"로 읽힙니다.
        /// </summary>
        [Test]
        public void 조사할_수_없으면_계획도_삭제를_올리지_않는다()
        {
            Bake(TwoRows);
            _assets.WithReferenceScanBlocked("검사용: 참조를 조사할 수 없습니다.");

            CsvImportPlan plan = Plan(OneRow);

            Assert.AreEqual(0, plan.Count(CsvChangeKind.Delete), plan.Summary());
            Assert.AreEqual(1, plan.Count(CsvChangeKind.Preserve), plan.Summary());
            Assert.AreEqual(1, plan.Issues.Count, "조사할 수 없다는 사실이 계획에 올라와야 합니다.");
            Assert.AreEqual(CsvPlanState.Problem, CsvPlanStatus.Of(plan),
                            "안전장치가 꺼진 표는 목록 위로 올라와야 합니다.");
        }

        /// <summary>
        /// 조사할 수 있을 때의 보존은 <b>다른 뜻</b>이라, 계획에 문제로 올라오지 않습니다.
        /// </summary>
        [Test]
        public void 참조가_남아_보존한_것은_문제가_아니다()
        {
            Bake(TwoRows);
            _assets.Referenced.Add($"{OutputFolder}/Widget_B.asset");

            CsvImportPlan plan = Plan(OneRow);

            Assert.AreEqual(1, plan.Count(CsvChangeKind.Preserve), plan.Summary());
            Assert.AreEqual(0, plan.Issues.Count, "참조가 남은 보존은 정상 동작입니다.");
        }
    }
}
