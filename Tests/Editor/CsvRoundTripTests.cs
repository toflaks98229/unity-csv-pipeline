using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
        private const string CsvFileName = "CsvPipelineTests_Widgets.csv";

        private static readonly string Sample =
            "Id,Title,MaxSpeed,Stock,OwnerId,HP\n"
            + "Widget_A,첫 위젯,30,12,Player,100\n"
            + "Widget_B,\"둘째, 쉼표 포함\",7.5,3,,50\n";

        /// <summary>이번 검사가 쓰는 임시 폴더입니다.</summary>
        private string _temp;

        private string CsvAssetPath => $"{_temp}/{CsvFileName}";
        private string OutputFolder => $"{_temp}/WidgetData";

        /// <summary>
        /// 표 하나를 <b>이 검사만의</b> 임시 폴더에 놓습니다.
        /// 검사끼리 같은 경로를 지웠다 만들기를 반복하면 AssetDatabase가 낡은 객체를 돌려주어
        /// 결과가 검사 순서에 따라 달라집니다. 폴더를 매번 새로 주어 그 얽힘을 끊습니다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _temp = CsvTestFolder.Create();
            WriteCsv(Sample);
        }

        /// <summary>만든 것을 모두 지웁니다.</summary>
        [TearDown]
        public void TearDown() => CsvTestFolder.Delete(_temp);

        /// <summary>
        /// 표 내용을 임시 폴더에 놓습니다. <b>굽지는 않습니다.</b>
        /// 굽기는 각 검사가 <see cref="Bake"/>로 한 번만 하며, 그래야 로그와 건수가 검사와 일대일로 대응합니다.
        /// </summary>
        /// <param name="text">기록할 표 원문입니다.</param>
        private void WriteCsv(string text)
        {
            using (CsvImport.Suppress())
            {
                File.WriteAllText(Path.GetFullPath(CsvAssetPath), text, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(CsvAssetPath, ImportAssetOptions.ForceUpdate);
            }
        }

        /// <summary>
        /// 산출물을 지워 생성 경로부터 다시 확인할 수 있게 합니다.
        /// 지운 뒤 <see cref="AssetDatabase.Refresh"/>로 반영을 끝내야 합니다. 그러지 않고 같은 경로에
        /// 곧바로 에셋을 만들면 AssetDatabase가 지워진 항목을 아직 붙들고 있어 쓴 값이 남지 않습니다.
        /// </summary>
        private void ClearOutput()
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder)) return;

            using (CsvImport.Suppress())
            {
                AssetDatabase.DeleteAsset(OutputFolder);
                AssetDatabase.Refresh();
            }
        }

        /// <summary>이 표의 스키마를 만듭니다.</summary>
        /// <returns>스키마입니다.</returns>
        private static CsvSchema Schema() => CsvSchema.For(typeof(WidgetData));

        /// <summary>표를 굽고 결과 리포트를 돌려줍니다.</summary>
        /// <returns>임포트 리포트입니다.</returns>
        private CsvImportReport Bake()
            => new CsvSchemaImportDefinition(Schema()).Run(CsvAssetPath);

        /// <summary>구워진 에셋을 읽습니다.</summary>
        /// <param name="id">에셋 이름입니다.</param>
        /// <returns>찾은 에셋이거나 null입니다.</returns>
        private WidgetData Load(string id)
            => AssetDatabase.LoadAssetAtPath<WidgetData>($"{OutputFolder}/{id}.asset");

        /// <summary>
        /// 에셋의 실제 값을 풀어 씁니다. 검사가 실패했을 때 무엇이 들어갔고 무엇이 안 들어갔는지
        /// 바로 보이지 않으면 원인을 찾는 데 시간이 걸립니다.
        /// </summary>
        /// <param name="id">에셋 이름입니다.</param>
        /// <returns>진단 문자열입니다.</returns>
        private string Dump(string id)
        {
            WidgetData asset = Load(id);
            if (asset == null) return $"{id}: 에셋이 없습니다.";

            var text = new StringBuilder($"{id}: ");
            var serialized = new SerializedObject(asset);

            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "m_Script") continue;
                text.Append($"{property.propertyPath}='{CsvValueFormatter.Format(property)}' ");
            }
            return text.ToString();
        }

        // ====================================================================================================

        /// <summary>행마다 에셋이 생기고 값이 들어갑니다.</summary>
        [Test]
        public void 행마다_에셋이_생긴다()
        {
            CsvImportReport report = Bake();

            Assert.IsNotNull(report);
            Assert.AreEqual(2, report.Created, report.Summary());
            Assert.IsFalse(report.HasErrors);

            WidgetData a = Load("Widget_A");
            Assert.IsNotNull(a, "산출물 폴더가 표 옆에 만들어져야 합니다.");
            Assert.AreEqual("첫 위젯", a.title, Dump("Widget_A"));
            Assert.AreEqual(12, a.stock, Dump("Widget_A"));
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
            Bake();

            Assert.AreEqual(30f, Load("Widget_A").maxSpeed, 0.0001f, Dump("Widget_A"));
            Assert.AreEqual(7.5f, Load("Widget_B").maxSpeed, 0.0001f, Dump("Widget_B"));
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

        /// <summary>표에서 사라진 행의 에셋은 아무도 참조하지 않으면 정리됩니다.</summary>
        [Test]
        public void 표에서_사라진_행의_에셋을_정리한다()
        {
            Bake();
            Assert.IsNotNull(Load("Widget_B"));

            WriteCsv("Id,Title,MaxSpeed,Stock,OwnerId,HP\nWidget_A,첫 위젯,30,12,Player,100\n");
            CsvImportReport report = Bake();

            Assert.AreEqual(1, report.Deleted, report.Summary());
            Assert.IsNull(Load("Widget_B"));
            Assert.IsNotNull(Load("Widget_A"));
        }

        /// <summary>산출물을 지운 뒤 다시 구우면 새로 만듭니다.</summary>
        [Test]
        public void 산출물을_지우면_다시_만든다()
        {
            CsvImportReport first = Bake();
            ClearOutput();
            Assert.IsNull(Load("Widget_A"), "지운 뒤에는 없어야 합니다.");

            CsvImportReport second = Bake();

            WidgetData a = Load("Widget_A");
            string diag = $"1차={first.Summary()} 2차={second.Summary()} "
                        + $"폴더={OutputFolder} 유효={AssetDatabase.IsValidFolder(OutputFolder)} "
                        + $"실제경로={(a == null ? "(null)" : AssetDatabase.GetAssetPath(a))} "
                        + $"개수={AssetDatabase.FindAssets("t:WidgetData").Length} {Dump("Widget_A")}";

            Assert.AreEqual(2, second.Created, diag);
            Assert.AreEqual(30f, a.maxSpeed, 0.0001f, diag);
        }

        /// <summary>필수 열이 없으면 아무것도 반영하지 않습니다. 빈 셀로 굽는 것보다 멈추는 편이 낫습니다.</summary>
        [Test]
        public void 필수_열이_없으면_아무것도_굽지_않는다()
        {
            WriteCsv("Id,Title,MaxSpeed,Stock,OwnerId\nWidget_A,첫 위젯,30,12,Player\n");

            // 오류가 나는 것 자체가 이 검사의 기대값입니다. 무시하지 않고 명시해 확인합니다.
            LogAssert.Expect(LogType.Error, new Regex("열 'HP'"));

            CsvImportReport report = Bake();

            Assert.IsTrue(report.HasErrors, "빠진 HP 열이 오류로 보고돼야 합니다.");
            Assert.AreEqual(0, report.Created);
            Assert.IsNull(Load("Widget_A"));
        }

        /// <summary>식별자가 빈 행은 건너뛰고 이유를 남깁니다.</summary>
        [Test]
        public void 식별자가_빈_행은_건너뛴다()
        {
            WriteCsv("Id,Title,MaxSpeed,Stock,OwnerId,HP\n"
                   + "Widget_A,첫 위젯,30,12,Player,100\n"
                   + " ,이름 없음,1,1,,1\n");

            LogAssert.Expect(LogType.Warning, new Regex("비어 있어 건너뜁니다"));

            CsvImportReport report = Bake();

            Assert.AreEqual(1, report.Created, report.Summary());
            Assert.AreEqual(1, report.Skipped, report.Summary());
        }

        /// <summary>내보내면 원본의 헤더 표기를 그대로 쓰고, 다시 읽어도 같은 값이 나옵니다.</summary>
        [Test]
        public void 내보낸_표가_원본과_왕복한다()
        {
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

    /// <summary>
    /// 검사마다 서로 다른 임시 폴더를 내어 줍니다.
    /// 같은 경로를 한 세션 안에서 지웠다 만들기를 반복하면 AssetDatabase가 낡은 객체를 돌려주어,
    /// 검사 결과가 실행 순서에 따라 달라집니다.
    /// </summary>
    internal static class CsvTestFolder
    {
        /// <summary>비어 있는 임시 폴더를 만듭니다.</summary>
        /// <returns>만들어진 폴더의 에셋 경로입니다.</returns>
        public static string Create()
        {
            string path = "Assets/CsvPipelineTests_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Directory.CreateDirectory(Path.GetFullPath(path));

            using (CsvImport.Suppress()) AssetDatabase.Refresh();
            return path;
        }

        /// <summary>임시 폴더를 지웁니다.</summary>
        /// <param name="path">지울 폴더의 에셋 경로입니다.</param>
        public static void Delete(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            using (CsvImport.Suppress())
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.Refresh();
            }
        }
    }
}
