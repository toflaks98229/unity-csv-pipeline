using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// <b>표시가 붙은 필드만</b> 표와 연결하는 방식을 검사합니다. (<c>AutoMap = false</c>)
    /// <para>
    /// 빼야 할 것을 하나씩 적는 방식(<c>[CsvIgnore]</c>)은 <b>잊으면 새 필드가 표에 딸려 들어갑니다.</b>
    /// 넣을 것만 적는 방식은 잊으면 빠질 뿐이라, 틀리는 방향이 안전한 쪽입니다.
    /// 대신 <b>말없이 빠진다</b>는 값을 치르므로, 무엇이 빠졌는지 물어볼 길이 함께 있어야 합니다.
    /// </para>
    /// </summary>
    public sealed class CsvOptInTests
    {
        private const string CsvPath = "Assets/Memory/CsvPipelineTests_OptIn.csv";
        private const string OutputFolder = "Assets/Memory/OptInTarget";

        private MemoryAssetGateway _assets;
        private IDisposable _scope;

        /// <summary>메모리 게이트웨이를 끼웁니다.</summary>
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

        /// <summary>표를 놓고 굽습니다.</summary>
        /// <param name="text">쓸 표 원문입니다.</param>
        /// <returns>임포트 리포트입니다.</returns>
        private CsvImportReport Bake(string text)
        {
            _assets.WithTable(CsvPath, text);
            return new CsvSchemaImportDefinition(CsvSchema.For(typeof(OptInTarget))).Run(CsvPath);
        }

        /// <summary>구워진 에셋을 읽습니다.</summary>
        /// <param name="id">에셋 이름입니다.</param>
        /// <returns>찾은 에셋이거나 null입니다.</returns>
        private OptInTarget Load(string id) => _assets.Get<OptInTarget>($"{OutputFolder}/{id}.asset");

        /// <summary>리포트에 이 조각을 담은 문제가 있는지 봅니다.</summary>
        /// <param name="report">살펴볼 리포트입니다.</param>
        /// <param name="fragment">찾을 조각입니다.</param>
        /// <returns>있으면 true입니다.</returns>
        private static bool Mentions(CsvImportReport report, string fragment)
        {
            foreach (CsvIssue issue in report.Issues)
            {
                if (issue.Message != null && issue.Message.Contains(fragment)) return true;
            }
            return false;
        }

        // ====================================================================================================
        // 연결
        // ====================================================================================================

        /// <summary>표시가 붙은 필드만 열과 연결됩니다.</summary>
        [Test]
        public void 표시된_필드만_연결된다()
        {
            CsvSchema schema = CsvSchema.For(typeof(OptInTarget));

            var columns = new List<string>();
            foreach (CsvBinding binding in schema.Bindings) columns.Add(binding.Column);

            Assert.IsTrue(schema.OptIn);
            CollectionAssert.AreEquivalent(new[] { "title", "HP" }, columns);
        }

        /// <summary>
        /// <b>표에 같은 이름의 열이 있어도</b> 표시가 없으면 쓰지 않습니다.
        /// 이 방식의 값 전부가 여기 걸려 있습니다 — 열이 생겼다고 값이 덮이면 아무것도 지켜지지 않습니다.
        /// </summary>
        [Test]
        public void 표시가_없으면_열이_있어도_쓰지_않는다()
        {
            CsvImportReport report = Bake("Id,title,HP,note\nA,갑,10,표에서 온 값\n");

            Assert.AreEqual(1, report.Created, report.Summary());

            OptInTarget baked = Load("A");
            Assert.AreEqual("갑", baked.title, "표시된 필드는 들어가야 합니다.");
            Assert.AreEqual(10, baked.health);
            Assert.AreEqual("손으로 적은 값", baked.note, "표시가 없는 필드는 열이 있어도 그대로여야 합니다.");
        }

        /// <summary>
        /// 표시를 붙였는데 표에 그 열이 없으면 알립니다.
        /// 자동 연결에서는 흔한 일이지만, 손으로 붙였다면 그 열을 <b>달라고 적어 둔 것</b>입니다.
        /// </summary>
        [Test]
        public void 표시했는데_열이_없으면_알린다()
        {
            CsvImportReport report = Bake("Id,title\nA,갑\n");

            Assert.IsTrue(Mentions(report, "HP"),
                          "붙여 둔 열이 표에 없다는 사실을 말해야 합니다: " + report.Summary());
        }

        /// <summary>열이 다 맞으면 쓸데없는 말을 하지 않습니다.</summary>
        [Test]
        public void 열이_다_맞으면_조용하다()
        {
            CsvImportReport report = Bake("Id,title,HP\nA,갑,10\n");

            Assert.AreEqual(0, report.Issues.Count, report.Summary());
        }

        // ====================================================================================================
        // 무엇이 빠졌는지 묻기
        // ====================================================================================================

        /// <summary>표시를 잊은 필드를 물어볼 수 있습니다. 말없이 빠지는 것이 이 방식의 유일한 위험입니다.</summary>
        [Test]
        public void 표시를_잊은_필드를_물어볼_수_있다()
        {
            List<string> untagged = CsvSchema.For(typeof(OptInTarget)).UntaggedFields();

            CollectionAssert.Contains(untagged, "note");
            CollectionAssert.Contains(untagged, "icon");
            CollectionAssert.DoesNotContain(untagged, "title", "표시가 붙은 것은 빠진 것이 아닙니다.");
            CollectionAssert.DoesNotContain(untagged, "health");
        }

        /// <summary>빼라고 적어 둔 것은 잊은 것이 아니라 정한 것입니다.</summary>
        [Test]
        public void 빼라고_적은_것은_잊은_것이_아니다()
        {
            CollectionAssert.DoesNotContain(CsvSchema.For(typeof(OptInTarget)).UntaggedFields(), "secret");
        }

        /// <summary>자동 연결에서는 "빠진 필드"라는 개념이 없습니다.</summary>
        [Test]
        public void 자동_연결에서는_빠진_필드가_없다()
        {
            CsvSchema schema = CsvSchema.For(typeof(WidgetData));

            Assert.IsFalse(schema.OptIn);
            CollectionAssert.IsEmpty(schema.UntaggedFields());
        }

        // ====================================================================================================
        // 표 만들기
        // ====================================================================================================

        /// <summary>표를 뽑을 때도 표시된 열만 나옵니다.</summary>
        [Test]
        public void 표를_뽑아도_표시된_열만_나온다()
        {
            CsvTemplateDraft draft = CsvTemplate.Build(typeof(OptInTarget));

            Assert.IsNull(draft.Unsupported, draft.Unsupported);
            Assert.IsTrue(draft.OptIn);
            CollectionAssert.AreEqual(new[] { "Id", "title", "HP" }, draft.Headers);
        }

        /// <summary>
        /// 표시가 없어 빠진 필드를 <b>따로</b> 알려 줍니다.
        /// 못 써서 뺀 것과 한 덩어리로 적으면, 고칠 수 있는 것과 없는 것이 섞여 읽힙니다.
        /// </summary>
        [Test]
        public void 표시가_없어_빠진_필드를_알려_준다()
        {
            CsvTemplateDraft draft = CsvTemplate.Build(typeof(OptInTarget));

            CollectionAssert.Contains(draft.Untagged, "note");
            CollectionAssert.Contains(draft.Untagged, "icon");
            CollectionAssert.DoesNotContain(draft.Untagged, "area",
                                            "애초에 표로 못 쓰는 것을 붙이라고 권하면 안 됩니다.");

            var omitted = new List<string>();
            foreach (CsvTemplateOmission omission in draft.Omitted) omitted.Add(omission.Field);
            CollectionAssert.DoesNotContain(omitted, "note", "두 목록은 뜻이 달라 섞이면 안 됩니다.");
        }

        /// <summary>표시를 하나도 붙이지 않았으면, 무엇을 해야 하는지 말해 줍니다.</summary>
        [Test]
        public void 표시가_하나도_없으면_그렇게_말한다()
        {
            CsvTemplateDraft draft = CsvTemplate.Build(typeof(OptInEmptyTarget));

            Assert.IsNotNull(draft.Unsupported);
            StringAssert.Contains("[CsvColumn]", draft.Unsupported);
            StringAssert.Contains("title", draft.Unsupported, "붙일 수 있는 필드를 함께 알려야 합니다.");
        }
    }
}
