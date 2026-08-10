using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 굽기 골격이 지키는 계약을 확인합니다.
    /// 특히 <b>"멈추면 정리하지 않는다"</b>는, 어기면 취소 버튼이 삭제 버튼이 되는 규칙입니다.
    /// 파생 클래스마다 다시 적던 시절에는 새 임포터 하나가 빠뜨리기만 해도 무너졌습니다.
    /// </summary>
    public sealed class CsvBakeSkeletonTests
    {
        private const string CsvPath = "Assets/Memory/CsvPipelineTests_Skeleton.csv";
        private const string Folder = "Assets/Memory/Skeleton";

        private const string Sample = "Id,Title\nA,갑\nB,을\n";

        private MemoryAssetGateway _assets;
        private System.IDisposable _scope;

        /// <summary>메모리 게이트웨이를 끼우고 표와 남아 있을 산출물을 놓습니다.</summary>
        [SetUp]
        public void SetUp()
        {
            _assets = new MemoryAssetGateway().WithTable(CsvPath, Sample);
            _assets.EnsureFolder(Folder);
            _scope = CsvAssets.Use(_assets);
        }

        /// <summary>게이트웨이를 걷어내고 만든 객체를 정리합니다.</summary>
        [TearDown]
        public void TearDown()
        {
            _scope.Dispose();
            _assets.Dispose();
        }

        /// <summary>표에 없는 낡은 산출물을 하나 놓습니다.</summary>
        /// <returns>놓은 에셋의 경로입니다.</returns>
        private string PlaceObsolete()
        {
            const string path = Folder + "/Z.asset";
            _assets.Add<WidgetData>(path);
            return path;
        }

        // ====================================================================================================

        /// <summary>끝까지 구우면 표에 없는 산출물이 정리됩니다. 아래 검사들의 대조군입니다.</summary>
        [Test]
        public void 끝까지_구우면_정리한다()
        {
            string obsolete = PlaceObsolete();

            CsvImportReport report = new SkeletonImporter().Run(CsvPath);

            Assert.AreEqual(2, report.Created, report.Summary());
            Assert.AreEqual(1, report.Deleted, report.Summary());
            Assert.IsNull(_assets.Get<WidgetData>(obsolete));
        }

        /// <summary>
        /// 굽는 도중에 멈추면 <b>정리하지 않습니다.</b>
        /// 아직 읽지 않은 행의 산출물을 "표에서 사라진 것"으로 오해해 지우게 되기 때문입니다.
        /// </summary>
        [Test]
        public void 멈추면_정리하지_않는다()
        {
            string obsolete = PlaceObsolete();

            CsvImportReport report = new SkeletonImporter { CancelAt = 0 }.Run(CsvPath);

            Assert.AreEqual(0, report.Deleted, report.Summary());
            Assert.IsNotNull(_assets.Get<WidgetData>(obsolete),
                             "멈춘 굽기가 산출물을 지우면 사람이 되돌릴 수 없습니다.");
        }

        /// <summary>멈춘 뒤의 단위는 굽지 않습니다.</summary>
        [Test]
        public void 멈춘_뒤의_단위는_굽지_않는다()
        {
            var importer = new SkeletonImporter { CancelAt = 0 };

            CsvImportReport report = importer.Run(CsvPath);

            Assert.AreEqual(1, importer.BakedCount, "멈춘 행 다음은 부르지 않아야 합니다.");
            Assert.AreEqual(1, report.Created, report.Summary());
        }

        /// <summary>건너뛴 단위는 확정 목록에 들어가지 않아, 그 산출물도 정리 대상이 됩니다.</summary>
        [Test]
        public void 건너뛴_단위는_확정되지_않는다()
        {
            _assets.WithTable(CsvPath, "Id,Title\nA,갑\n ,이름 없음\n");

            CsvImportReport report = new SkeletonImporter().Run(CsvPath);

            Assert.AreEqual(1, report.Created, report.Summary());
            Assert.AreEqual(1, report.Skipped, report.Summary());
        }

        /// <summary>정리를 쓰지 않는 임포터는 표에 없는 산출물을 건드리지 않습니다.</summary>
        [Test]
        public void 정리를_쓰지_않으면_지우지_않는다()
        {
            string obsolete = PlaceObsolete();

            CsvImportReport report = new SkeletonImporter { Reconciles = false }.Run(CsvPath);

            Assert.AreEqual(2, report.Created, report.Summary());
            Assert.AreEqual(0, report.Deleted, report.Summary());
            Assert.IsNotNull(_assets.Get<WidgetData>(obsolete));
        }
    }

    /// <summary>
    /// 골격만 쓰는 최소 임포터입니다. 언제 멈출지와 정리 여부를 검사가 정합니다.
    /// </summary>
    internal sealed class SkeletonImporter : CsvImportDefinition
    {
        /// <summary>이 번째 단위를 구운 뒤 멈춥니다. 음수면 멈추지 않습니다.</summary>
        public int CancelAt { get; set; } = -1;

        /// <summary>표에서 사라진 산출물을 정리할지 여부입니다.</summary>
        public bool Reconciles { get; set; } = true;

        /// <summary>실제로 구운 단위 수입니다.</summary>
        public int BakedCount { get; private set; }

        protected override string FileName => "CsvPipelineTests_Skeleton.csv";
        protected override string OutputFolder => "Assets/Memory/Skeleton";
        protected override IEnumerable<string> RequiredColumns => new[] { "Id" };

        protected override CsvReconcileMode ReconcileMode
            => Reconciles ? CsvReconcileMode.ByName : CsvReconcileMode.None;

        protected override string ReconcileTypeFilter => "t:WidgetData";

        /// <summary>행마다 에셋 하나를 굽습니다.</summary>
        /// <param name="table">파싱된 표입니다.</param>
        /// <param name="report">건수와 문제를 기록할 리포트입니다.</param>
        protected override void Process(CsvTable table, CsvImportReport report)
        {
            CsvAssetPipeline.EnsureFolder(OutputFolder);

            BakeEach(table.Rows, report, BakeOne);
        }

        /// <summary>행 하나를 굽고, 지정된 지점이면 멈춥니다.</summary>
        /// <param name="row">읽을 행입니다.</param>
        /// <param name="report">문제를 기록할 리포트입니다.</param>
        /// <returns>구운 결과입니다.</returns>
        private CsvBakeOutcome BakeOne(CsvRow row, CsvImportReport report)
        {
            string id = row.GetString("Id");
            if (string.IsNullOrEmpty(id))
            {
                report.Warn("식별자가 비어 있어 건너뜁니다.", row.LineNumber);
                return CsvBakeOutcome.Skipped();
            }

            var asset = CsvAssetPipeline.CreateOrLoad<WidgetData>($"{OutputFolder}/{id}.asset", out bool created);

            var serialized = new SerializedObject(asset);
            serialized.FindProperty("title").stringValue = row.GetString("Title");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            CsvAssets.Current.MarkDirty(asset);

            BakedCount++;
            if (BakedCount - 1 == CancelAt) CancelCurrentRun();

            return CsvBakeOutcome.Baked(created, id);
        }
    }
}
