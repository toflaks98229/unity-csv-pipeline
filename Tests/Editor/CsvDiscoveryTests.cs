using System.Reflection;
using NUnit.Framework;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 발견이 <b>소비 프로젝트의 것만</b> 집어 오는지 봅니다.
    /// <para>
    /// 이 검사가 있는 까닭이 있습니다. 실제 게임 프로젝트에 이 패키지를 끼워 넣고 드리프트 검사를
    /// 돌려 보니, <b>패키지 자신의 검사 픽스처 넷이 남의 목록에 "못 읽음" 으로 올라왔습니다.</b>
    /// 픽스처가 가리키는 표는 검사가 도는 동안에만 메모리에 있으니 당연히 못 찾습니다.
    /// </para>
    /// <para>
    /// 목록이 지저분해지는 것으로 끝나지 않습니다 — 굽기 경로도 같은 목록을 쓰므로,
    /// 남의 프로젝트에서 <b>있지도 않은 표를 구우려 듭니다.</b>
    /// </para>
    /// </summary>
    public sealed class CsvDiscoveryTests
    {
        /// <summary>이 검사 어셈블리는 검사 어셈블리로 판정돼야 합니다.</summary>
        [Test]
        public void 검사_어셈블리를_알아본다()
            => Assert.IsTrue(CsvAssemblies.IsTestAssembly(typeof(CsvDiscoveryTests).Assembly));

        /// <summary>패키지 본체는 검사 어셈블리가 아닙니다. 이것까지 걸러 내면 아무것도 안 남습니다.</summary>
        [Test]
        public void 패키지_본체는_검사_어셈블리가_아니다()
        {
            Assert.IsFalse(CsvAssemblies.IsTestAssembly(typeof(CsvSchema).Assembly));
            Assert.IsFalse(CsvAssemblies.IsTestAssembly(typeof(CsvAssetAttribute).Assembly));
        }

        /// <summary>모르는 것은 <b>보통 코드로 봅니다.</b> 걸러 내는 쪽으로 틀리면 남의 표가 사라집니다.</summary>
        [Test]
        public void 판단할_수_없으면_보통_코드로_본다()
            => Assert.IsFalse(CsvAssemblies.IsTestAssembly(null));

        /// <summary>
        /// <see cref="CsvSchema.All"/> 이 이 검사의 픽스처를 집어 오지 않습니다.
        /// 집어 오면 소비 프로젝트의 창과 드리프트 검사가 남의 표로 더러워집니다.
        /// </summary>
        [Test]
        public void 발견이_검사_픽스처를_집지_않는다()
        {
            foreach (CsvSchema schema in CsvSchema.All())
            {
                Assert.AreNotEqual(typeof(WidgetData), schema.AssetType,
                                   "검사 픽스처가 소비 프로젝트의 목록에 올라옵니다.");
                Assert.AreNotEqual(typeof(ByPathData), schema.AssetType,
                                   "검사 픽스처가 소비 프로젝트의 목록에 올라옵니다.");
            }
        }

        /// <summary>같은 규칙이 직접 작성한 임포터 쪽에도 걸립니다.</summary>
        [Test]
        public void 발견이_검사_임포터도_집지_않는다()
        {
            Assembly tests = typeof(CsvDiscoveryTests).Assembly;

            foreach (CsvImportDefinition definition in CsvImportDefinition.DiscoverAll())
            {
                Assert.AreNotEqual(tests, definition.GetType().Assembly,
                                   $"{definition.GetType().Name} 이(가) 소비 프로젝트의 목록에 올라옵니다.");
            }
        }

        /// <summary>
        /// 걸러 내도 <see cref="CsvSchema.For"/> 는 그대로 됩니다.
        /// 발견에서 뺀 것이지 쓸 수 없게 만든 것이 아닙니다 — 이 저장소의 다른 검사들이 그 길로 돕니다.
        /// </summary>
        [Test]
        public void 이름을_대면_여전히_만들_수_있다()
            => Assert.IsNotNull(CsvSchema.For(typeof(WidgetData)));
    }
}
