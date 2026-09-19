using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 타입 하나에서 표를 뽑아내는 생성기를 검사합니다.
    /// <para>
    /// 가장 중요한 것은 <b>되읽을 수 없는 열을 만들어 주지 않는 것</b>입니다. 만들어 주면 사람은
    /// 시트에 그 열을 채우고, 받아오는 순간 행마다 경고가 쏟아집니다. 애초에 만들지 않는 편이
    /// 만들어 주고 나중에 실패하는 것보다 낫습니다.
    /// </para>
    /// </summary>
    public sealed class CsvTemplateTests
    {
        private MemoryAssetGateway _assets;
        private IDisposable _scope;

        /// <summary>메모리 게이트웨이를 끼웁니다. 프로젝트의 실제 에셋이 결과에 섞이지 않게 합니다.</summary>
        [SetUp]
        public void SetUp()
        {
            _assets = new MemoryAssetGateway();
            _scope = CsvAssets.Use(_assets);
        }

        /// <summary>게이트웨이를 걷어내고 만든 객체를 정리합니다.</summary>
        [TearDown]
        public void TearDown()
        {
            _scope.Dispose();
            _assets.Dispose();
        }

        /// <summary>줄 끝을 걷어내고 줄 단위로 나눕니다.</summary>
        /// <param name="text">나눌 표 원문입니다.</param>
        /// <returns>줄들입니다.</returns>
        private static string[] Lines(string text) => text.TrimEnd('\n').Split('\n');

        /// <summary>뺀 필드들의 이름입니다.</summary>
        /// <param name="draft">살펴볼 초안입니다.</param>
        /// <returns>이름 목록입니다.</returns>
        private static List<string> OmittedFields(CsvTemplateDraft draft)
        {
            var names = new List<string>();
            foreach (CsvTemplateOmission omission in draft.Omitted) names.Add(omission.Field);
            return names;
        }

        // ====================================================================================================
        // 열 고르기
        // ====================================================================================================

        /// <summary>선언이 아직 없는 타입도 필드를 보고 열을 뽑습니다. 표를 만들기 전이 바로 그 상태입니다.</summary>
        [Test]
        public void 선언이_없는_타입도_열을_뽑는다()
        {
            CsvTemplateDraft draft = CsvTemplate.Build(typeof(TemplateTarget));

            Assert.IsNull(draft.Unsupported, draft.Unsupported);
            Assert.AreEqual(CsvTemplate.DefaultIdColumn, draft.Headers[0], "식별자 열이 맨 앞이어야 합니다.");
            CollectionAssert.Contains(draft.Headers, "title");
            CollectionAssert.Contains(draft.Headers, "cost");
            CollectionAssert.Contains(draft.Headers, "tags");
        }

        /// <summary>열 이름 지정과 제외 표시를 굽기와 똑같이 따릅니다.</summary>
        [Test]
        public void 열_이름_지정과_제외를_따른다()
        {
            CsvTemplateDraft draft = CsvTemplate.Build(typeof(TemplateTarget));

            CollectionAssert.Contains(draft.Headers, "HP");
            CollectionAssert.DoesNotContain(draft.Headers, "health");
            CollectionAssert.DoesNotContain(draft.Headers, "note");
        }

        /// <summary>
        /// 표로 저작할 수 없는 필드는 열에서 빼고, 뺐다는 사실을 알립니다.
        /// 원소를 저작할 수 없는 <b>목록</b>도 마찬가지입니다.
        /// </summary>
        [Test]
        public void 표로_못_쓰는_필드는_빼고_알린다()
        {
            CsvTemplateDraft draft = CsvTemplate.Build(typeof(TemplateTarget));

            CollectionAssert.DoesNotContain(draft.Headers, "area");
            CollectionAssert.DoesNotContain(draft.Headers, "curve");
            CollectionAssert.DoesNotContain(draft.Headers, "areas");

            List<string> omitted = OmittedFields(draft);
            CollectionAssert.Contains(omitted, "area");
            CollectionAssert.Contains(omitted, "curve");
            CollectionAssert.Contains(omitted, "areas", "원소를 못 쓰는 목록도 빼야 합니다.");
        }

        /// <summary>왜 뺐는지 말하지 않으면, 사람은 그 필드가 표에 있는 줄 알고 시트에 열을 만듭니다.</summary>
        [Test]
        public void 뺀_이유를_함께_적는다()
        {
            CsvTemplateDraft draft = CsvTemplate.Build(typeof(TemplateTarget));

            Assert.IsNotEmpty(draft.Omitted);
            foreach (CsvTemplateOmission omission in draft.Omitted)
            {
                Assert.IsNotEmpty(omission.Reason, $"{omission.Field}를 왜 뺐는지 말해야 합니다.");
            }
        }

        /// <summary>
        /// 저작할 수 있다고 답한 것은 <b>실제로 써지고</b>, 없다고 답한 것은 변환기도 거절합니다.
        /// 두 목록이 갈라지면 되읽지 못할 열이 생기거나 쓸 수 있는 필드가 표에서 빠집니다.
        /// </summary>
        [Test]
        public void 저작_가능_판정이_변환기와_어긋나지_않는다()
        {
            CsvTemplateDraft supported = CsvTemplate.Build(typeof(BinderTarget));
            CsvTemplateDraft mixed = CsvTemplate.Build(typeof(TemplateTarget));

            // BinderTarget 은 변환기가 다루는 타입을 한 벌 모아 둔 것이라, 하나도 빠지지 않아야 합니다.
            Assert.IsEmpty(OmittedFields(supported),
                           "변환기가 쓸 수 있는 필드가 표에서 빠졌습니다: " + string.Join(", ", OmittedFields(supported)));

            // 반대로 변환기가 거절하는 타입은 반드시 빠져 있어야 합니다.
            CollectionAssert.Contains(OmittedFields(mixed), "curve");
        }

        // ====================================================================================================
        // 선언
        // ====================================================================================================

        /// <summary>선언이 붙어 있으면 지어내지 않고 그 선언을 그대로 씁니다.</summary>
        [Test]
        public void 선언이_있으면_그_선언을_따른다()
        {
            CsvTemplateDraft draft = CsvTemplate.Build(typeof(WidgetData));

            Assert.IsTrue(draft.Declared);
            Assert.AreEqual("Id", draft.IdColumn);
            Assert.IsNull(draft.DeclarationSnippet, "이미 붙어 있으면 붙여 넣을 줄이 필요 없습니다.");
            CollectionAssert.Contains(draft.Headers, "HP");
            CollectionAssert.DoesNotContain(draft.Headers, "artwork");
        }

        /// <summary>선언이 없으면 붙여 넣을 줄을 함께 줍니다. 이 줄이 없으면 표는 아무것도 굽지 않습니다.</summary>
        [Test]
        public void 선언이_없으면_붙여_넣을_줄을_준다()
        {
            CsvTemplateDraft draft = CsvTemplate.Build(typeof(TemplateTarget));

            Assert.IsFalse(draft.Declared);
            StringAssert.Contains("[CsvAsset(", draft.DeclarationSnippet);
            StringAssert.Contains("TemplateTarget.csv", draft.DeclarationSnippet);
        }

        // ====================================================================================================
        // 행
        // ====================================================================================================

        /// <summary>에셋이 하나도 없으면 헤더만 남습니다. 시트를 백지에서 시작하는 경우입니다.</summary>
        [Test]
        public void 에셋이_없으면_헤더만_만든다()
        {
            CsvTemplateDraft draft = CsvTemplate.Build(typeof(TemplateTarget));

            Assert.AreEqual(0, draft.RowCount);
            Assert.AreEqual(1, Lines(draft.Text).Length, "헤더 한 줄이어야 합니다.");
        }

        /// <summary>이미 만들어 둔 에셋이 있으면 행으로 채웁니다. 경로순이라 늘 같은 파일이 나옵니다.</summary>
        [Test]
        public void 에셋이_있으면_행으로_채운다()
        {
            _assets.Add<WidgetData>("Assets/Memory/Widgets/B.asset").title = "을";
            _assets.Add<WidgetData>("Assets/Memory/Widgets/A.asset").title = "갑";

            CsvTemplateDraft draft = CsvTemplate.Build(typeof(WidgetData));

            Assert.AreEqual(2, draft.RowCount);

            string[] lines = Lines(draft.Text);
            StringAssert.StartsWith("A,", lines[1], "경로순이어야 늘 같은 파일이 나옵니다.");
            StringAssert.StartsWith("B,", lines[2]);
            StringAssert.Contains("갑", lines[1]);
        }

        /// <summary>선언이 산출물 폴더를 가리키면 그 안만 봅니다. 밖의 에셋은 이 표의 것이 아닙니다.</summary>
        [Test]
        public void 산출물_폴더가_있으면_그_안만_본다()
        {
            _assets.EnsureFolder("Assets/Memory/ByPath");
            _assets.Add<ByPathData>("Assets/Memory/ByPath/In.asset");
            _assets.Add<ByPathData>("Assets/Memory/Elsewhere/Out.asset");

            CsvTemplateDraft draft = CsvTemplate.Build(typeof(ByPathData));

            Assert.AreEqual(1, draft.RowCount);
            StringAssert.Contains("In", draft.Text);
            StringAssert.DoesNotContain("Out", draft.Text);
        }

        // ====================================================================================================
        // 형식
        // ====================================================================================================

        /// <summary>구분자를 골라 뽑습니다. 부르는 쪽은 저장할 파일의 확장자로 정합니다.</summary>
        [Test]
        public void 구분자를_고를_수_있다()
        {
            CsvTemplateDraft comma = CsvTemplate.Build(typeof(TemplateTarget), CsvReader.Comma);
            CsvTemplateDraft tab = CsvTemplate.Build(typeof(TemplateTarget), CsvReader.Tab);

            StringAssert.Contains(",", comma.Text);
            StringAssert.Contains("\t", tab.Text);
            Assert.IsFalse(tab.Text.Contains(","), "탭 구분 표에 쉼표 구분자가 남으면 안 됩니다.");
        }

        /// <summary>뽑아낸 표는 이 패키지의 파서가 그대로 다시 읽습니다. 왕복이 닫혀 있어야 합니다.</summary>
        [Test]
        public void 만든_표는_그대로_다시_읽힌다()
        {
            _assets.Add<WidgetData>("Assets/Memory/Widgets/A.asset").title = "갑, 쉼표 포함";

            CsvTemplateDraft draft = CsvTemplate.Build(typeof(WidgetData));
            CsvTable table = CsvReader.ReadTable(draft.Text);

            Assert.AreEqual(1, table.Count);
            Assert.AreEqual("A", table.Rows[0].GetString("Id"));
            Assert.AreEqual("갑, 쉼표 포함", table.Rows[0].GetString("title"),
                            "쉼표가 든 셀이 감싸여 있어야 합니다.");
        }

        /// <summary>
        /// 이미 표가 있으면 거기 적힌 열 표기를 그대로 씁니다.
        /// 다시 뽑을 때마다 표기가 바뀌면 시트와 헤더가 어긋나 동기화가 멈춥니다.
        /// </summary>
        [Test]
        public void 이미_있는_표의_열_표기를_지킨다()
        {
            _assets.WithTable("Assets/CSV/CsvPipelineTests_Widgets.csv",
                              "Id,Title,MaxSpeed,Stock,OwnerId,HP\n");

            CsvTemplateDraft draft = CsvTemplate.Build(typeof(WidgetData));

            CollectionAssert.Contains(draft.Headers, "Title");
            CollectionAssert.DoesNotContain(draft.Headers, "title");
        }

        // ====================================================================================================
        // 거절
        // ====================================================================================================

        /// <summary>표를 뽑을 수 없는 것은 이유를 붙여 거절합니다.</summary>
        [Test]
        public void 만들_수_없는_타입은_이유를_붙여_거절한다()
        {
            Assert.IsNotNull(CsvTemplate.Reject(null));
            Assert.IsNotNull(CsvTemplate.Reject(typeof(string)));
            Assert.IsNull(CsvTemplate.Reject(typeof(TemplateTarget)));
        }

        /// <summary>거절한 타입은 초안에도 이유가 남고, 표 원문은 만들지 않습니다.</summary>
        [Test]
        public void 거절한_타입은_표를_만들지_않는다()
        {
            CsvTemplateDraft draft = CsvTemplate.Build(typeof(string));

            Assert.IsNotNull(draft.Unsupported);
            Assert.IsNull(draft.Text);
        }
    }
}
