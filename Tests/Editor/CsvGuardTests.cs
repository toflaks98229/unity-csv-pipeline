using System;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// <b>조용히 데이터를 잃던 자리</b>들을 고정합니다.
    /// <para>
    /// 여기 있는 것은 전부 "오류도 경고도 없이 값이 바뀌거나 사라지던" 동작이었습니다. 그런 결함은
    /// 건수로도 리포트로도 드러나지 않아, 검사가 없으면 다음 판에서 조용히 되돌아옵니다.
    /// 그래서 <b>고친 자리마다 검사를 하나씩</b> 둡니다.
    /// </para>
    /// </summary>
    public sealed class CsvGuardTests
    {
        private const string CsvPath = "Assets/Memory/CsvPipelineTests_Guard.csv";
        private const string OutputFolder = "Assets/Memory/Guard";

        private MemoryAssetGateway _assets;
        private IDisposable _scope;

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
        /// <param name="text">표 원문 전체입니다.</param>
        /// <returns>임포트 리포트입니다. 읽을 것이 없으면 null입니다.</returns>
        private CsvImportReport Bake(string text)
        {
            _assets.WithTable(CsvPath, text);
            return new CsvSchemaImportDefinition(CsvSchema.For(typeof(GuardData))).Run(CsvPath);
        }

        // ====================================================================================================
        // 파서 — 닫히지 않은 따옴표
        // ====================================================================================================

        /// <summary>
        /// 따옴표 하나가 뒤의 행을 통째로 삼키면 <b>굽지 않습니다.</b>
        /// 삼켜진 행은 '건너뜀'으로도 세어지지 않아, 그대로 두면 정리가 그 산출물을 지웁니다.
        /// </summary>
        [Test]
        public void 닫히지_않은_따옴표는_표를_못_읽은_것으로_다룬다()
        {
            CsvTable table = CsvReader.ReadTable("Id,Note\nA,he said \"hi\nB,ok\nC,fine\n");

            Assert.IsNotNull(table.Defect, "닫히지 않은 따옴표를 알리지 않았습니다.");
            StringAssert.Contains("2", table.Defect, "따옴표가 시작된 줄 번호를 알려야 합니다.");
        }

        /// <summary>온전한 표에는 결함이 없습니다. 없는 문제를 만들어 내면 멀쩡한 표가 안 구워집니다.</summary>
        [Test]
        public void 온전한_표에는_결함이_없다()
        {
            Assert.IsNull(CsvReader.ReadTable("Id,Note\nA,\"he said \"\"hi\"\"\"\nB,ok\n").Defect);
            Assert.IsNull(CsvReader.ReadTable("Id,Note\nA,\"두\n줄\"\nB,ok\n").Defect);
        }

        /// <summary>그 표로 구우면 아무것도 지우지 않고 오류로 멈춥니다.</summary>
        [Test]
        public void 닫히지_않은_따옴표가_있으면_굽지도_지우지도_않는다()
        {
            Bake("Id,Title\nA,first\nB,second\nC,third\n");
            Assert.AreEqual(3, _assets.FindPaths("t:GuardData", OutputFolder).Count, "준비가 어긋났습니다.");

            LogAssert.Expect(LogType.Error, new Regex("double quote"));
            Bake("Id,Title\nA,\"oops\nB,second\nC,third\n");

            Assert.AreEqual(3, _assets.FindPaths("t:GuardData", OutputFolder).Count,
                            "표를 읽지 못했는데 산출물이 지워졌습니다.");
        }

        // ====================================================================================================
        // 파서 — 같은 이름의 열
        // ====================================================================================================

        /// <summary>같은 이름의 열은 뒤 열이 앞 열을 덮으므로, 무엇을 잃고 있는지 알립니다.</summary>
        [Test]
        public void 같은_이름의_열을_알린다()
        {
            CollectionAssert.AreEqual(new[] { "Title" }, CsvReader.ReadTable("Id,Title,Title\nA,1,2\n").DuplicateHeaders);
        }

        /// <summary>대소문자만 다른 열도 같은 열입니다. 헤더 대조가 대소문자를 무시하기 때문입니다.</summary>
        [Test]
        public void 대소문자만_다른_열도_겹침으로_본다()
        {
            CsvTable table = CsvReader.ReadTable("Id,Title,TITLE\nA,first,second\n");

            Assert.AreEqual(1, table.DuplicateHeaders.Count);
            Assert.AreEqual("second", table.Rows[0].GetString("Title"), "뒤 열이 이깁니다.");
        }

        /// <summary>겹치지 않으면 아무 말도 하지 않습니다.</summary>
        [Test]
        public void 겹치지_않는_열은_알리지_않는다()
        {
            Assert.AreEqual(0, CsvReader.ReadTable("Id,Title,Note\nA,1,2\n").DuplicateHeaders.Count);
        }

        /// <summary>굽기가 그 사실을 리포트에 남깁니다. 다만 <b>멈추지는 않습니다</b> — 뒤 열은 정상입니다.</summary>
        [Test]
        public void 겹친_열은_경고로_남고_굽기는_이어진다()
        {
            LogAssert.Expect(LogType.Warning, new Regex("two or more"));
            CsvImportReport report = Bake("Id,Title,Title\nA,first,second\n");

            Assert.AreEqual(1, report.Created, "겹친 열 때문에 멀쩡한 행까지 멈추면 안 됩니다.");
        }

        // ====================================================================================================
        // 값 변환 — 범위를 넘는 수
        // ====================================================================================================

        /// <summary>범위를 넘는 정수를 잘라 넣지 않고, 이 패키지의 규칙대로 기존 값을 남깁니다.</summary>
        [Test]
        [TestCase("2147483648")]
        [TestCase("-2147483649")]
        [TestCase("9999999999")]
        public void 정수_범위를_넘으면_기존_값을_남긴다(string raw)
        {
            var target = ScriptableObject.CreateInstance<BinderTarget>();
            try
            {
                var serialized = new SerializedObject(target);
                bool wrote = new CsvValueBinder().Apply(
                    serialized.FindProperty("count"), typeof(int), raw,
                    new CsvBinding { Column = "count", PropertyPath = "count" }, out string error);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsFalse(wrote, "범위를 넘은 값을 썼습니다.");
                Assert.IsNotNull(error, "조용히 넘어갔습니다.");
                Assert.AreEqual(7, target.count, "기존 값이 바뀌었습니다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(target); }
        }

        /// <summary>범위 안의 값은 그대로 들어가야 합니다. 막는 쪽으로만 치우치면 도구가 못 쓰게 됩니다.</summary>
        [Test]
        [TestCase("2147483647", 2147483647)]
        [TestCase("-2147483648", -2147483648)]
        [TestCase("0", 0)]
        public void 정수_범위_안의_값은_그대로_들어간다(string raw, int expected)
        {
            var target = ScriptableObject.CreateInstance<BinderTarget>();
            try
            {
                var serialized = new SerializedObject(target);
                bool wrote = new CsvValueBinder().Apply(
                    serialized.FindProperty("count"), typeof(int), raw,
                    new CsvBinding { Column = "count", PropertyPath = "count" }, out string error);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsTrue(wrote, error);
                Assert.AreEqual(expected, target.count);
            }
            finally { UnityEngine.Object.DestroyImmediate(target); }
        }

        /// <summary>float 범위를 넘는 수가 무한대로 구워지지 않습니다.</summary>
        [Test]
        public void 실수_범위를_넘으면_기존_값을_남긴다()
        {
            var target = ScriptableObject.CreateInstance<BinderTarget>();
            try
            {
                var serialized = new SerializedObject(target);
                bool wrote = new CsvValueBinder().Apply(
                    serialized.FindProperty("speed"), typeof(float), "1e40",
                    new CsvBinding { Column = "speed", PropertyPath = "speed" }, out string error);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsFalse(wrote);
                Assert.IsNotNull(error);
                Assert.AreEqual(1.5f, target.speed, "기존 값이 Infinity 로 덮였습니다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(target); }
        }

        /// <summary>표에 일부러 적은 무한대는 그대로 받습니다. 사람이 뜻을 밝힌 값입니다.</summary>
        [Test]
        public void 무한대는_적은_대로_받는다()
        {
            var target = ScriptableObject.CreateInstance<BinderTarget>();
            try
            {
                var serialized = new SerializedObject(target);
                bool wrote = new CsvValueBinder().Apply(
                    serialized.FindProperty("speed"), typeof(float), "Infinity",
                    new CsvBinding { Column = "speed", PropertyPath = "speed" }, out string error);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsTrue(wrote, error);
                Assert.IsTrue(float.IsPositiveInfinity(target.speed));
            }
            finally { UnityEngine.Object.DestroyImmediate(target); }
        }

        // ====================================================================================================
        // 값 변환 — 목록을 비우는 셀
        // ====================================================================================================

        /// <summary>
        /// 구분자만 남은 셀이 손으로 저작한 목록을 비우지 않습니다.
        /// 내보내기가 다룰 줄 모르는 원소를 빈 문자열로 쓰면 <c>;;</c> 같은 셀이 나오던 자리입니다.
        /// </summary>
        [Test]
        [TestCase(";;")]
        [TestCase(";")]
        [TestCase("  ")]
        public void 구분자만_있는_셀은_목록을_비우지_않는다(string raw)
        {
            var target = ScriptableObject.CreateInstance<BinderTarget>();
            try
            {
                target.tags.Add("keep");
                target.tags.Add("me");

                var serialized = new SerializedObject(target);
                bool wrote = new CsvValueBinder().Apply(
                    serialized.FindProperty("tags"), typeof(System.Collections.Generic.List<string>), raw,
                    new CsvBinding { Column = "tags", PropertyPath = "tags", Separators = CsvRow.ListSeparators },
                    out string error);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsFalse(wrote);
                Assert.IsNotNull(error, "목록이 비워지는데 아무 말이 없었습니다.");
                Assert.AreEqual(2, target.tags.Count, "저작한 목록이 사라졌습니다.");
            }
            finally { UnityEngine.Object.DestroyImmediate(target); }
        }

        /// <summary>비우는 것을 저작으로 삼겠다고 밝혔으면 그 뜻대로 비웁니다.</summary>
        [Test]
        public void 비우겠다고_밝히면_목록을_비운다()
        {
            var target = ScriptableObject.CreateInstance<BinderTarget>();
            try
            {
                target.tags.Add("gone");

                var serialized = new SerializedObject(target);
                bool wrote = new CsvValueBinder().Apply(
                    serialized.FindProperty("tags"), typeof(System.Collections.Generic.List<string>), ";",
                    new CsvBinding
                    {
                        Column = "tags",
                        PropertyPath = "tags",
                        Separators = CsvRow.ListSeparators,
                        OverwriteWhenEmpty = true
                    },
                    out string error);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsTrue(wrote, error);
                Assert.AreEqual(0, target.tags.Count);
            }
            finally { UnityEngine.Object.DestroyImmediate(target); }
        }

        // ====================================================================================================
        // 정리 — 식별자의 대소문자
        // ====================================================================================================

        /// <summary>
        /// 식별자의 대소문자만 바꿔도 그 행의 에셋이 지워지던 자리입니다.
        /// 저장소가 대소문자를 가리지 않아 <b>방금 갱신한 그 에셋</b>이 '사라진 행'으로 잡혔습니다.
        /// </summary>
        [Test]
        public void 식별자_대소문자만_바뀌면_지우지_않는다()
        {
            Bake("Id,Title\nSword,A\n");
            Assert.AreEqual(1, _assets.FindPaths("t:GuardData", OutputFolder).Count, "준비가 어긋났습니다.");

            CsvImportReport report = Bake("Id,Title\nsword,B\n");

            Assert.AreEqual(0, report.Deleted, "대소문자만 바뀌었는데 지웠습니다.");
            Assert.AreEqual(1, _assets.FindPaths("t:GuardData", OutputFolder).Count,
                            "산출물이 사라졌습니다.");
        }

        /// <summary>정말로 사라진 행은 그대로 정리합니다. 막는 쪽으로만 치우치면 정리가 죽습니다.</summary>
        [Test]
        public void 정말_사라진_행은_그대로_지운다()
        {
            Bake("Id,Title\nSword,A\nShield,B\n");
            Assert.AreEqual(2, _assets.FindPaths("t:GuardData", OutputFolder).Count, "준비가 어긋났습니다.");

            CsvImportReport report = Bake("Id,Title\nSword,A\n");

            Assert.AreEqual(1, report.Deleted);
            Assert.AreEqual(1, _assets.FindPaths("t:GuardData", OutputFolder).Count);
        }

        // ====================================================================================================
        // 굽기 비용 — 되임포트를 미루는 범위
        // ====================================================================================================

        /// <summary>
        /// 굽기 루프가 <b>배치 범위 안에서</b> 돕니다. 이것이 없으면 행마다 에셋 파이프라인이
        /// 한 번씩 돌아, 3,000행 표에서 한 칸만 고쳐도 저장할 때마다 20초가 넘습니다.
        /// </summary>
        [Test]
        public void 굽기는_되임포트를_미루는_범위_안에서_돈다()
        {
            Assert.AreEqual(0, _assets.BatchCount, "준비가 어긋났습니다.");

            Bake("Id,Title\nA,1\nB,2\nC,3\n");

            Assert.GreaterOrEqual(_assets.BatchCount, 1, "굽기가 범위를 열지 않았습니다.");
            Assert.AreEqual(0, _assets.BatchDepth, "연 범위를 닫지 않았습니다.");
        }

        /// <summary>
        /// 값이 달라진 행만 더럽힙니다.
        /// <para>
        /// 더럽힌 에셋은 굽기 끝의 저장이 전부 다시 씁니다. 값이 그대로인 행까지 더럽히면
        /// 3,000행 표에서 <b>한 칸만 고쳐도 3,000개를 다시 쓰게</b> 됩니다 — 실측으로 25초였고,
        /// 이 자리를 고친 뒤 1.2초가 됐습니다. 결과가 같아 검사로 드러나지 않던 낭비입니다.
        /// </para>
        /// </summary>
        [Test]
        public void 값이_그대로면_에셋을_더럽히지_않는다()
        {
            Bake("Id,Title\nA,1\nB,2\nC,3\n");
            Assert.AreEqual(3, _assets.DirtyCount, "처음 만들 때는 세 개 다 더럽혀야 합니다.");

            Bake("Id,Title\nA,1\nB,2\nC,3\n");
            Assert.AreEqual(3, _assets.DirtyCount, "값이 그대로인데 다시 더럽혔습니다.");

            Bake("Id,Title\nA,1\nB,바뀜\nC,3\n");
            Assert.AreEqual(4, _assets.DirtyCount, "바뀐 행 하나만 더럽혀야 합니다.");
        }

        // ====================================================================================================
        // 삭제 안전장치 — 제외 확장자 목록
        // ====================================================================================================

        /// <summary>
        /// 참조 조사가 건너뛰는 확장자 목록에 <b>참조를 담을 수 있는 것이 섞이면</b>
        /// 그 파일이 붙잡고 있던 산출물이 조용히 지워집니다. 그 사고를 검사로 막습니다.
        /// </summary>
        [Test]
        [TestCase(".unity")]
        [TestCase(".prefab")]
        [TestCase(".asset")]
        [TestCase(".playable")]
        [TestCase(".preset")]
        [TestCase(".controller")]
        [TestCase(".overrideController")]
        [TestCase(".mask")]
        [TestCase(".mixer")]
        [TestCase(".spriteatlas")]
        [TestCase(".terrainlayer")]
        [TestCase(".inputactions")]
        [TestCase(".mat")]
        [TestCase(".physicMaterial")]
        [TestCase(".guiskin")]
        [TestCase(".lighting")]
        [TestCase(".renderTexture")]
        [TestCase(".signal")]
        public void 참조를_담을_수_있는_확장자는_건너뛰지_않는다(string extension)
        {
            CollectionAssert.DoesNotContain(
                UnityAssetGateway.ReferencelessExtensions, extension,
                $"'{extension}' 이(가) 참조 조사에서 빠지면 그 파일이 쓰고 있는 산출물이 지워집니다.");
        }

        /// <summary>목록은 소문자·점 포함으로만 적어야 합니다. 대조가 소문자로 정규화해 비교하기 때문입니다.</summary>
        [Test]
        public void 제외_확장자_목록의_표기가_고르다()
        {
            foreach (string extension in UnityAssetGateway.ReferencelessExtensions)
            {
                Assert.IsTrue(extension.StartsWith(".", StringComparison.Ordinal), $"점이 없습니다: {extension}");
                Assert.AreEqual(extension.ToLowerInvariant(), extension, $"대문자가 섞였습니다: {extension}");
            }
        }

        // ====================================================================================================
        // 인코딩 — BOM 이 붙은 파일
        // ====================================================================================================

        /// <summary>
        /// BOM 이 UTF-8 이라고 선언해도 본문이 그렇지 않으면 거절합니다.
        /// 치환 폴백으로 읽으면 깨진 바이트가 <c>U+FFFD</c> 로 조용히 바뀌어 통과하던 자리입니다.
        /// </summary>
        [Test]
        public void BOM이_붙어도_본문이_UTF8이_아니면_거절한다()
        {
            byte[] bytes = Concat(
                new byte[] { 0xEF, 0xBB, 0xBF },
                Encoding.ASCII.GetBytes("Id,V\nA,"),
                new byte[] { 0xB0, 0xA1, 0x0A });   // CP949 '가'

            Assert.IsFalse(CsvText.TryDecode(bytes, out string text, out string problem));
            Assert.IsNull(text);
            Assert.IsNotNull(problem);
        }

        /// <summary>BOM + 정상 UTF-8 은 그대로 읽고 BOM 을 값에 남기지 않습니다.</summary>
        [Test]
        public void BOM이_붙은_정상_UTF8은_그대로_읽는다()
        {
            byte[] bytes = Concat(
                new byte[] { 0xEF, 0xBB, 0xBF },
                Encoding.UTF8.GetBytes("Id,V\nA,가\n"),
                Array.Empty<byte>());

            Assert.IsTrue(CsvText.TryDecode(bytes, out string text, out string problem), problem);
            Assert.AreEqual("Id,V\nA,가\n", text);
        }

        /// <summary>바이트열 셋을 이어 붙입니다.</summary>
        /// <param name="a">첫 조각입니다.</param>
        /// <param name="b">둘째 조각입니다.</param>
        /// <param name="c">셋째 조각입니다.</param>
        /// <returns>이어 붙인 바이트열입니다.</returns>
        private static byte[] Concat(byte[] a, byte[] b, byte[] c)
        {
            var result = new byte[a.Length + b.Length + c.Length];
            Buffer.BlockCopy(a, 0, result, 0, a.Length);
            Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
            Buffer.BlockCopy(c, 0, result, a.Length + b.Length, c.Length);
            return result;
        }
    }
}
