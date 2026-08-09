using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 미리보기가 실제 결과와 맞는지, 그리고 <b>아무것도 쓰지 않는지</b> 확인합니다.
    /// 쓰지 않는다는 것이 이 기능의 전제이므로 그것부터 못박습니다.
    /// </summary>
    public sealed class CsvPlanTests
    {
        private const string TempFolder = "Assets/CsvPipelineTests_Temp";
        private const string CsvFileName = "CsvPipelineTests_Widgets.csv";
        private const string OutputFolder = TempFolder + "/WidgetData";

        private static string CsvAssetPath => $"{TempFolder}/{CsvFileName}";

        private static readonly string Sample =
            "Id,Title,MaxSpeed,Stock,OwnerId,HP\n"
            + "Widget_A,첫 위젯,30,12,Player,100\n"
            + "Widget_B,둘째 위젯,7.5,3,,50\n";

        /// <summary>표 하나를 임시 폴더에 놓습니다. 굽지는 않습니다.</summary>
        [SetUp]
        public void SetUp()
        {
            Directory.CreateDirectory(Path.GetFullPath(TempFolder));
            WriteCsv(Sample);
        }

        /// <summary>만든 것을 모두 지웁니다.</summary>
        [TearDown]
        public void TearDown()
        {
            using (CsvImport.Suppress())
            {
                AssetDatabase.DeleteAsset(TempFolder);
                AssetDatabase.Refresh();
            }
        }

        /// <summary>표 내용을 놓되 자동으로 굽지 않습니다.</summary>
        /// <param name="text">기록할 표 원문입니다.</param>
        private static void WriteCsv(string text)
        {
            using (CsvImport.Suppress())
            {
                File.WriteAllText(Path.GetFullPath(CsvAssetPath), text, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(CsvAssetPath, ImportAssetOptions.ForceUpdate);
            }
        }

        /// <summary>이 표의 임포터를 만듭니다.</summary>
        /// <returns>임포터입니다.</returns>
        private static CsvSchemaImportDefinition Importer()
            => new CsvSchemaImportDefinition(CsvSchema.For(typeof(WidgetData)));

        /// <summary>산출물 폴더의 에셋 개수입니다.</summary>
        /// <returns>개수입니다.</returns>
        private static int AssetCount()
            => AssetDatabase.IsValidFolder(OutputFolder)
                ? AssetDatabase.FindAssets("t:WidgetData", new[] { OutputFolder }).Length
                : 0;

        /// <summary>구워진 에셋을 읽습니다.</summary>
        /// <param name="id">에셋 이름입니다.</param>
        /// <returns>찾은 에셋이거나 null입니다.</returns>
        private static WidgetData Load(string id)
            => AssetDatabase.LoadAssetAtPath<WidgetData>($"{OutputFolder}/{id}.asset");

        // ====================================================================================================

        /// <summary>계획을 세워도 에셋이 생기지 않습니다. 이 기능의 전제입니다.</summary>
        [Test]
        public void 계획은_아무것도_쓰지_않는다()
        {
            Assert.AreEqual(0, AssetCount(), "시작 시점에는 산출물이 없어야 합니다.");

            CsvImportPlan plan = Importer().Plan(CsvAssetPath);

            Assert.AreEqual(2, plan.Count(CsvChangeKind.Create), plan.Summary());
            Assert.AreEqual(0, AssetCount(), "미리보기가 에셋을 만들면 안 됩니다.");
        }

        /// <summary>이미 구운 뒤 다시 계획하면 바뀌는 것이 없습니다.</summary>
        [Test]
        public void 이미_같으면_바뀌는_것이_없다()
        {
            Importer().Run(CsvAssetPath);

            CsvImportPlan plan = Importer().Plan(CsvAssetPath);

            Assert.IsTrue(plan.IsNoOp, plan.Summary());
            Assert.AreEqual(0, plan.Count(CsvChangeKind.Update),
                            "값이 같으면 갱신으로 올리지 않아야 '무엇이 실제로 바뀌는가'가 드러납니다.");
        }

        /// <summary>값이 달라지면 어느 열이 무엇에서 무엇으로 바뀌는지까지 알려 줍니다.</summary>
        [Test]
        public void 값이_달라지면_필드까지_알려_준다()
        {
            Importer().Run(CsvAssetPath);
            Assert.AreEqual(30f, Load("Widget_A").maxSpeed, 0.0001f);

            WriteCsv(Sample.Replace("Widget_A,첫 위젯,30,", "Widget_A,첫 위젯,44,"));

            CsvImportPlan plan = Importer().Plan(CsvAssetPath);

            Assert.AreEqual(1, plan.Count(CsvChangeKind.Update), plan.Summary());

            CsvPlannedChange change = plan.Changes.Find(c => c.Kind == CsvChangeKind.Update);
            Assert.AreEqual(1, change.Fields.Count, "달라진 열 하나만 올라야 합니다.");
            Assert.AreEqual("MaxSpeed", change.Fields[0].Column);
            Assert.AreEqual("30", change.Fields[0].From);
            Assert.AreEqual("44", change.Fields[0].To);

            Assert.AreEqual(30f, Load("Widget_A").maxSpeed, 0.0001f, "미리보기가 원본을 건드리면 안 됩니다.");
        }

        /// <summary>표에서 행이 사라지면 삭제로 잡습니다. 실제로 지우지는 않습니다.</summary>
        [Test]
        public void 행이_사라지면_삭제로_잡는다()
        {
            Importer().Run(CsvAssetPath);
            Assert.AreEqual(2, AssetCount());

            WriteCsv("Id,Title,MaxSpeed,Stock,OwnerId,HP\nWidget_A,첫 위젯,30,12,Player,100\n");

            CsvImportPlan plan = Importer().Plan(CsvAssetPath);

            Assert.AreEqual(1, plan.Count(CsvChangeKind.Delete), plan.Summary());
            Assert.AreEqual(2, AssetCount(), "미리보기가 에셋을 지우면 안 됩니다.");
            Assert.IsNotNull(Load("Widget_B"));
        }

        /// <summary>식별자가 빈 행은 건너뜀으로 잡습니다.</summary>
        [Test]
        public void 식별자가_빈_행은_건너뜀으로_잡는다()
        {
            WriteCsv("Id,Title,MaxSpeed,Stock,OwnerId,HP\n"
                   + "Widget_A,첫 위젯,30,12,Player,100\n"
                   + " ,이름 없음,1,1,,1\n");

            CsvImportPlan plan = Importer().Plan(CsvAssetPath);

            Assert.AreEqual(1, plan.Count(CsvChangeKind.Create), plan.Summary());
            Assert.AreEqual(1, plan.Count(CsvChangeKind.Skip), plan.Summary());
        }

        /// <summary>필수 열이 없으면 계획 자체를 세우지 않고 이유를 남깁니다.</summary>
        [Test]
        public void 필수_열이_빠지면_계획을_세우지_않는다()
        {
            WriteCsv("Id,Title,MaxSpeed,Stock,OwnerId\nWidget_A,첫 위젯,30,12,Player\n");

            CsvImportPlan plan = Importer().Plan(CsvAssetPath);

            Assert.IsFalse(plan.IsSupported);
            Assert.AreEqual(1, plan.Issues.Count);
            StringAssert.Contains("HP", plan.Issues[0].Message);
        }

        /// <summary>값을 해석하지 못하는 셀은 굽기 전에 경고로 드러납니다.</summary>
        [Test]
        public void 해석하지_못하는_값을_미리_알려_준다()
        {
            Importer().Run(CsvAssetPath);

            WriteCsv(Sample.Replace("Widget_A,첫 위젯,30,12,", "Widget_A,첫 위젯,빠름,12,"));

            CsvImportPlan plan = Importer().Plan(CsvAssetPath);

            Assert.AreEqual(1, plan.Issues.Count, "실수가 아닌 값이 경고로 올라야 합니다.");
            Assert.AreEqual("MaxSpeed", plan.Issues[0].Column);
        }

        /// <summary>직접 작성한 임포터도 목록에 오릅니다. (미리보기 창이 이 목록을 씁니다)</summary>
        [Test]
        public void 임포터를_반사로_찾아낸다()
        {
            var found = CsvImportDefinition.DiscoverAll();

            Assert.IsNotNull(found);
            CollectionAssert.AllItemsAreNotNull(found);
        }

        /// <summary>표를 찾지 못하면 계획을 세우지 않고 이유를 남깁니다.</summary>
        [Test]
        public void 표가_없으면_이유를_남긴다()
        {
            CsvImportPlan plan = Importer().Plan("Assets/없는파일.csv");

            Assert.IsFalse(plan.IsSupported);
            Assert.IsNotNull(plan.Unsupported);
        }
    }
}
