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
    /// <b>실제 AssetDatabase 위에서만</b> 드러나는 것들을 봅니다.
    /// 굽기 규칙 자체는 <see cref="CsvBakeUnitTests"/>가 메모리에서 훨씬 빠르게 확인하므로,
    /// 여기 남은 것은 재임포트·디스크 기록·GUID 참조 훑기처럼 Unity가 직접 관여하는 자리뿐입니다.
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

        /// <summary>
        /// 구운 값이 <b>디스크에 남습니다.</b> 메모리 게이트웨이로는 볼 수 없는 자리입니다.
        /// 스크립트 링크가 끊긴 에셋이 만들어지면 값이 하나도 남지 않으므로 여기서 걸립니다.
        /// <para>
        /// 파일을 <b>글자로 읽어</b> 확인하므로, Asset Serialization 이 <c>Force Text</c> 가 아닌
        /// 프로젝트에서는 스스로 건너뜁니다. 이진으로 저장된 파일에 글자가 없는 것은
        /// 이 패키지의 결함이 아닙니다.
        /// </para>
        /// </summary>
        [Test]
        public void 구운_값이_파일에_남는다()
        {
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                Assert.Ignore("Asset Serialization 이 Force Text 가 아니라 파일을 글자로 읽을 수 없습니다.");
            }

            CsvImportReport report = Bake();

            Assert.AreEqual(2, report.Created, report.Summary());

            string path = $"{OutputFolder}/Widget_A.asset";
            Assert.IsTrue(File.Exists(Path.GetFullPath(path)), $"에셋 파일이 있어야 합니다: {path}");

            string yaml = File.ReadAllText(Path.GetFullPath(path));
            string diag = $"길이={yaml.Length} 스크립트링크={yaml.Contains("m_Script: {fileID: 11500000")} "
                        + $"stock={yaml.Contains("stock: 12")} health={yaml.Contains("health: 100")} "
                        + $"title={yaml.Contains("첫 위젯")} {Dump("Widget_A")}";

            // 표식은 ASCII 필드로 봅니다. Unity가 YAML에 한글을 어떤 표기로 쓰는지는 이 검사의 관심사가
            // 아니고, 그것에 기대면 "값이 남았는가"와 무관한 이유로 검사가 깨집니다.
            Assert.IsTrue(yaml.Contains("stock: 12"), $"쓴 값이 파일에 남아야 합니다. {diag}");
            Assert.AreEqual(100, Load("Widget_A").health, diag);
        }

        /// <summary>
        /// 산출물을 지운 뒤 다시 구우면 새로 만들고 <b>값도 남습니다.</b>
        /// 지운 경로에 다시 만들면 재임포트가 끼어들어 메모리에만 있던 수정이 버려지던 자리입니다.
        /// </summary>
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

        /// <summary>
        /// 표에서 사라진 행의 에셋을 정리합니다.
        /// 무엇이 참조 중인지는 프로젝트 파일을 GUID로 훑어 판정하므로 실제 AssetDatabase가 필요합니다.
        /// </summary>
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

        /// <summary>
        /// <b>다른 에셋이 붙잡고 있으면 지우지 않습니다.</b> 이 패키지의 1번 안전장치입니다.
        /// <para>
        /// 메모리 게이트웨이로는 확인할 수 없습니다 — 거기서는 "참조가 남았다" 를 검사가 손으로
        /// 알려 주기 때문입니다. 진짜로 물어봐야 하는 자리라 실제 AssetDatabase 위에 둡니다.
        /// </para>
        /// </summary>
        [Test]
        public void 다른_에셋이_붙잡고_있으면_지우지_않는다()
        {
            Bake();

            WidgetData held = Load("Widget_B");
            Assert.IsNotNull(held);

            string holderPath = $"{_temp}/Holder.asset";
            using (CsvImport.Suppress())
            {
                var holder = ScriptableObject.CreateInstance<WidgetHolder>();
                holder.widget = held;
                AssetDatabase.CreateAsset(holder, holderPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            WriteCsv("Id,Title,MaxSpeed,Stock,OwnerId,HP\nWidget_A,첫 위젯,30,12,Player,100\n");

            LogAssert.Expect(LogType.Warning, new Regex("참조 중이라 보존"));
            CsvImportReport report = Bake();

            Assert.AreEqual(0, report.Deleted, report.Summary());
            Assert.AreEqual(1, report.Preserved, report.Summary());
            Assert.IsNotNull(Load("Widget_B"), "붙잡고 있는 에셋이 있으면 남아야 합니다.");

            // 정말로 붙잡고 있었는지 — 배선이 끊기지 않았는지까지 봅니다.
            var reloaded = AssetDatabase.LoadAssetAtPath<WidgetHolder>(holderPath);
            Assert.IsNotNull(reloaded.widget, "참조가 살아 있어야 보존한 뜻이 있습니다.");
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
