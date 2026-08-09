using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CsvPipeline.Tests
{
    /// <summary>선언에서 열 ↔ 필드 연결이 제대로 읽히는지 검사합니다.</summary>
    public sealed class CsvSchemaTests
    {
        private static CsvSchema Schema() => CsvSchema.For(typeof(WidgetData));

        private static CsvBinding Find(CsvSchema schema, string propertyPath)
            => schema.Bindings.FirstOrDefault(b => b.PropertyPath == propertyPath);

        /// <summary>특성이 없는 타입에서는 스키마가 나오지 않습니다.</summary>
        [Test]
        public void 특성이_없으면_스키마가_없다()
        {
            Assert.IsNull(CsvSchema.For(typeof(BinderTarget)));
            Assert.IsNull(CsvSchema.For(null));
        }

        /// <summary>선언한 파일과 식별자 열을 읽습니다.</summary>
        [Test]
        public void 선언을_읽는다()
        {
            CsvSchema schema = Schema();

            Assert.IsNotNull(schema);
            Assert.AreEqual("CsvPipelineTests_Widgets.csv", schema.Declaration.FileName);
            Assert.AreEqual("Id", schema.Declaration.IdColumn);
        }

        /// <summary>
        /// public 필드는 이름 그대로 열에 연결됩니다.
        /// 열 이름은 필드 이름(camelCase)으로 잡히고, 실제 조회에서 대소문자를 흡수합니다.
        /// </summary>
        [Test]
        public void public_필드를_자동으로_연결한다()
        {
            CsvSchema schema = Schema();

            Assert.IsNotNull(Find(schema, "title"));
            Assert.AreEqual("maxSpeed", Find(schema, "maxSpeed").Column);
            Assert.IsNotNull(Find(schema, "stock"));
        }

        /// <summary>private 이어도 직렬화되면 연결됩니다.</summary>
        [Test]
        public void 직렬화되는_private_필드도_연결한다()
        {
            Assert.IsNotNull(Find(Schema(), "ownerId"));
        }

        /// <summary>제외 표시한 필드는 연결하지 않습니다.</summary>
        [Test]
        public void 제외한_필드는_연결하지_않는다()
        {
            Assert.IsNull(Find(Schema(), "artwork"));
        }

        /// <summary>열 이름을 바꿔 지정할 수 있습니다.</summary>
        [Test]
        public void 열_이름을_바꿔_지정한다()
        {
            CsvBinding binding = Find(Schema(), "health");

            Assert.IsNotNull(binding);
            Assert.AreEqual("HP", binding.Column);
            Assert.IsTrue(binding.Required);
        }

        /// <summary>필수 열 목록에는 식별자 열과 필수로 표시한 열이 들어갑니다.</summary>
        [Test]
        public void 필수_열을_모은다()
        {
            List<string> required = Schema().RequiredColumns.ToList();

            CollectionAssert.Contains(required, "Id");
            CollectionAssert.Contains(required, "HP");
            CollectionAssert.DoesNotContain(required, "title");
        }

        /// <summary>빈 셀 보존이 기본값입니다.</summary>
        [Test]
        public void 빈_셀_보존이_기본값이다()
        {
            Assert.IsFalse(Find(Schema(), "title").OverwriteWhenEmpty);
        }

        /// <summary>산출물 폴더를 선언하지 않으면 원본 표 옆으로 정합니다.</summary>
        [Test]
        public void 폴더를_선언하지_않으면_표_옆으로_정한다()
        {
            CsvSchema schema = Schema();

            Assert.IsNull(schema.Declaration.OutputFolder);
            Assert.AreEqual("Assets/Any/Where/WidgetData",
                schema.ResolveOutputFolder("Assets/Any/Where/CsvPipelineTests_Widgets.csv"));
        }
    }
}
