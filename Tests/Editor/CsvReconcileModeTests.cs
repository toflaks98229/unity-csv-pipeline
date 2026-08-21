using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// <c>[CsvAsset(ReconcileByPath = true)]</c> 가 정리 대조를 <b>경로로</b> 바꾸는지 봅니다.
    /// <para>
    /// 이 선택지는 SCPPJ 의 임포터를 속성으로 옮겨 보다가 필요해졌습니다. 코드로 쓴 임포터에는
    /// <c>ReconcileByPath</c> 가 있는데 속성 쪽에는 없어, <b>그 표 하나 때문에 코드를 남겨야</b> 했습니다.
    /// </para>
    /// </summary>
    public sealed class CsvReconcileModeTests
    {
        private const string CsvPath = "Assets/Memory/CsvPipelineTests_ByPath.csv";
        private const string OutputFolder = "Assets/Memory/ByPath";

        private const string Header = "Id,Title\n";

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
            return new CsvSchemaImportDefinition(CsvSchema.For(typeof(ByPathData))).Run(CsvPath);
        }

        // ====================================================================================================

        /// <summary>선언이 경로 대조를 켜면 계획도 굽기도 그것을 따릅니다.</summary>
        [Test]
        public void 선언이_경로_대조를_켠다()
        {
            var declaration = (CsvAssetAttribute)System.Attribute.GetCustomAttribute(
                typeof(ByPathData), typeof(CsvAssetAttribute));

            Assert.IsTrue(declaration.ReconcileByPath);
        }

        /// <summary>이름 대조와 마찬가지로, 표에서 사라진 행의 산출물은 지웁니다.</summary>
        [Test]
        public void 표에서_사라진_행은_그대로_지운다()
        {
            Bake("A,첫\nB,둘\n");

            CsvImportReport report = Bake("A,첫\n");

            Assert.AreEqual(1, report.Deleted, report.Summary());
            Assert.IsNull(_assets.Get<ByPathData>($"{OutputFolder}/B.asset"));
        }

        /// <summary>
        /// 표가 만들지 않은 에셋도 이 폴더에 있으면 정리 대상입니다.
        /// <b>경로 대조든 이름 대조든 그 점은 같습니다.</b> 다른 것은 무엇을 같다고 보느냐뿐입니다.
        /// </summary>
        [Test]
        public void 표가_만들지_않은_에셋은_참조가_남으면_보존된다()
        {
            Bake("A,첫\n");

            string stray = $"{OutputFolder}/Handmade.asset";
            _assets.Add<ByPathData>(stray);
            _assets.Referenced.Add(stray);

            LogAssert.Expect(LogType.Warning, new Regex("참조 중이라 보존"));

            CsvImportReport report = Bake("A,첫\n");

            Assert.AreEqual(0, report.Deleted, report.Summary());
            Assert.AreEqual(1, report.Preserved, report.Summary());
            Assert.IsNotNull(_assets.Get<ByPathData>(stray));
        }

        /// <summary>미리보기도 같은 대조를 씁니다. 다르면 본 것과 일어나는 것이 갈립니다.</summary>
        [Test]
        public void 미리보기가_같은_대조를_쓴다()
        {
            Bake("A,첫\nB,둘\n");

            _assets.WithTable(CsvPath, Header + "A,첫\n");
            CsvImportPlan plan = new CsvSchemaImportDefinition(CsvSchema.For(typeof(ByPathData))).Plan(CsvPath);

            Assert.AreEqual(1, plan.Count(CsvChangeKind.Delete), plan.Summary());
            Assert.AreEqual($"{OutputFolder}/B.asset",
                            FirstOfKind(plan, CsvChangeKind.Delete), plan.Summary());
        }

        /// <summary>계획에서 그 종류의 첫 경로를 꺼냅니다.</summary>
        /// <param name="plan">볼 계획입니다.</param>
        /// <param name="kind">찾을 종류입니다.</param>
        /// <returns>찾은 경로이거나 null입니다.</returns>
        private static string FirstOfKind(CsvImportPlan plan, CsvChangeKind kind)
        {
            foreach (CsvPlannedChange change in plan.Changes)
            {
                if (change.Kind == kind) return change.AssetPath;
            }
            return null;
        }
    }
}
