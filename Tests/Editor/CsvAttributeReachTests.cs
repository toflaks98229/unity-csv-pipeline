using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 코드 없는 길의 속성들이 <b>게임 코드에서 닿을 수 있는 자리</b>에 있는지 봅니다.
    /// <para>
    /// 이 검사가 있는 까닭이 있습니다. 속성들이 처음에는 에디터 전용 어셈블리에 있었습니다.
    /// 그 자리에서는 <b>에디터에서는 컴파일되고 플레이어 빌드에서만 깨집니다.</b> 실제로 확인해 보니
    /// README 의 첫 예제가 <c>CS0246: The type or namespace name 'CsvPipeline' could not be found</c>
    /// 로 빌드에서 떨어졌습니다. 만드는 동안에는 멀쩡하다가 <b>출시할 때 터지는</b> 종류의 결함입니다.
    /// </para>
    /// <para>
    /// 되돌아오기 쉬운 실수라 — 파일 하나를 옮기기만 하면 됩니다 — 검사로 못 박아 둡니다.
    /// </para>
    /// </summary>
    public sealed class CsvAttributeReachTests
    {
        /// <summary>속성이 들어 있어야 하는 런타임 어셈블리의 이름입니다.</summary>
        private const string RuntimeAssembly = "CsvPipeline";

        /// <summary>게임 코드가 닿을 수 없는 에디터 전용 어셈블리의 이름입니다.</summary>
        private const string EditorAssembly = "CsvPipeline.Editor";

        /// <summary>
        /// 선언용 속성 셋은 런타임 어셈블리에 있어야 합니다.
        /// 게임의 ScriptableObject 는 런타임 타입이고, 런타임 어셈블리는 에디터 어셈블리를 볼 수 없습니다.
        /// </summary>
        [TestCase(typeof(CsvAssetAttribute))]
        [TestCase(typeof(CsvColumnAttribute))]
        [TestCase(typeof(CsvIgnoreAttribute))]
        public void 선언용_속성은_런타임_어셈블리에_있다(Type attribute)
        {
            string actual = attribute.Assembly.GetName().Name;

            Assert.AreEqual(RuntimeAssembly, actual,
                $"{attribute.Name} 이(가) '{actual}' 에 있습니다. 게임 데이터 타입이 이 속성을 붙이면 "
                + "에디터에서는 컴파일되고 플레이어 빌드에서 깨집니다.");
        }

        /// <summary>굽는 코드는 반대로 런타임에 섞이면 안 됩니다. 빌드에 들어갈 이유가 없습니다.</summary>
        [TestCase(typeof(CsvSchema))]
        [TestCase(typeof(CsvImportDefinition))]
        [TestCase(typeof(CsvAssetPipeline))]
        [TestCase(typeof(CsvPipelineWindow))]
        public void 굽는_코드는_에디터_어셈블리에_있다(Type type)
            => Assert.AreEqual(EditorAssembly, type.Assembly.GetName().Name,
                               $"{type.Name} 은(는) 빌드에 들어갈 이유가 없습니다.");

        /// <summary>
        /// 런타임 어셈블리에는 <b>선언만</b> 들어갑니다. 무엇이 더 들어가면 빌드에 코드가 실립니다.
        /// </summary>
        [Test]
        public void 런타임_어셈블리에는_선언만_있다()
        {
            Assembly runtime = typeof(CsvAssetAttribute).Assembly;

            foreach (Type type in runtime.GetTypes())
            {
                // Unity 가 어셈블리마다 끼워 넣는 생성 타입은 우리 것이 아닙니다.
                if (type.Namespace != "CsvPipeline") continue;

                Assert.IsTrue(typeof(Attribute).IsAssignableFrom(type),
                              $"런타임 어셈블리에 속성이 아닌 {type.FullName} 이(가) 있습니다.");
            }
        }

        /// <summary>
        /// 런타임 어셈블리가 <b>UnityEngine 에도 매여 있지 않은지</b> 봅니다.
        /// 선언뿐이라 그럴 이유가 없고, 매이지 않으면 어디서든 쓸 수 있습니다.
        /// </summary>
        [Test]
        public void 런타임_어셈블리는_엔진에도_매이지_않는다()
        {
            foreach (AssemblyName reference in typeof(CsvAssetAttribute).Assembly.GetReferencedAssemblies())
            {
                Assert.IsFalse(reference.Name.StartsWith("UnityEngine", StringComparison.Ordinal),
                               $"런타임 어셈블리가 {reference.Name} 을(를) 참조합니다.");
            }
        }

        /// <summary>
        /// 게임 데이터 타입에 속성을 붙이는 것이 실제로 되는지, <b>런타임 타입 하나로</b> 확인합니다.
        /// </summary>
        [Test]
        public void 런타임_타입에_속성을_붙일_수_있다()
        {
            var declaration = (CsvAssetAttribute)Attribute.GetCustomAttribute(
                typeof(RuntimeShapedData), typeof(CsvAssetAttribute));

            Assert.IsNotNull(declaration);
            Assert.AreEqual("CsvPipelineTests_RuntimeShaped.csv", declaration.FileName);
        }
    }

    /// <summary>
    /// 게임의 데이터 타입과 <b>같은 모양</b>인 타입입니다 — UnityEngine 말고는 아무것도 참조하지 않습니다.
    /// 속성이 런타임에서 닿는 자리에 있는지 확인하는 데 씁니다.
    /// </summary>
    [CsvAsset("CsvPipelineTests_RuntimeShaped.csv", "Id")]
    public sealed class RuntimeShapedData : ScriptableObject
    {
        /// <summary>표에서 오는 값입니다.</summary>
        public string title;
    }
}
