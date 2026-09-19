using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 셀에 적힌 이름을 에셋으로 배선하는 규칙을 검사합니다.
    /// <para>
    /// 가장 중요한 것은 <b>이름이 겹칠 때 고르지 않는 것</b>입니다. 고르면 검색 순서가 어느 것을
    /// 붙일지 정하는데 그 순서는 보장되지 않습니다. 사람 쪽에서는 배선이 된 것처럼 보이므로,
    /// 못 찾은 것보다 나쁜 종류의 실패입니다.
    /// </para>
    /// </summary>
    public sealed class CsvReferenceResolveTests
    {
        private const string InA = "Assets/Memory/A/Sword.asset";
        private const string InB = "Assets/Memory/B/Sword.asset";

        private MemoryAssetGateway _assets;
        private IDisposable _scope;
        private BinderTarget _target;
        private SerializedObject _serialized;
        private CsvValueBinder _binder;

        /// <summary>메모리 게이트웨이를 끼우고 배선 대상을 만듭니다.</summary>
        [SetUp]
        public void SetUp()
        {
            _assets = new MemoryAssetGateway();
            _scope = CsvAssets.Use(_assets);

            _target = ScriptableObject.CreateInstance<BinderTarget>();
            _serialized = new SerializedObject(_target);
            _binder = new CsvValueBinder();
        }

        /// <summary>게이트웨이를 걷어내고 만든 객체를 정리합니다.</summary>
        [TearDown]
        public void TearDown()
        {
            if (_target != null) UnityEngine.Object.DestroyImmediate(_target);
            _scope.Dispose();
            _assets.Dispose();
        }

        /// <summary>참조 필드에 값을 씁니다.</summary>
        /// <param name="raw">셀 원문입니다.</param>
        /// <param name="error">오류 메시지를 받습니다.</param>
        /// <returns>실제로 썼으면 true입니다.</returns>
        private bool ApplyLink(string raw, out string error)
        {
            var binding = new CsvBinding
            {
                Column = "linked",
                PropertyPath = "linked",
                FieldType = typeof(WidgetData),
                Separators = CsvRow.ListSeparators
            };

            bool wrote = _binder.Apply(_serialized.FindProperty("linked"), typeof(WidgetData), raw, binding, out error);
            _serialized.ApplyModifiedPropertiesWithoutUndo();
            return wrote;
        }

        // ====================================================================================================
        // 하나뿐일 때
        // ====================================================================================================

        /// <summary>이름이 하나뿐이면 그대로 붙습니다.</summary>
        [Test]
        public void 이름이_하나뿐이면_붙는다()
        {
            WidgetData only = _assets.Add<WidgetData>(InA);

            Assert.IsTrue(ApplyLink("Sword", out string error), error);
            Assert.IsNull(error);
            Assert.AreSame(only, _target.linked);
        }

        /// <summary>찾지 못하면 이름을 붙여 알리고, 기존 값을 그대로 둡니다.</summary>
        [Test]
        public void 못_찾으면_이름을_붙여_알린다()
        {
            Assert.IsFalse(ApplyLink("없는이름", out string error));

            StringAssert.Contains("없는이름", error);
            StringAssert.Contains("was found", error);
            Assert.IsNull(_target.linked, "찾지 못했으면 쓰지 않아야 합니다.");
        }

        // ====================================================================================================
        // 겹칠 때
        // ====================================================================================================

        /// <summary>
        /// 같은 이름이 여럿이면 <b>고르지 않습니다.</b> 이 검사가 이 기능의 전부입니다.
        /// </summary>
        [Test]
        public void 이름이_겹치면_고르지_않는다()
        {
            _assets.Add<WidgetData>(InA);
            _assets.Add<WidgetData>(InB);

            Assert.IsFalse(ApplyLink("Sword", out string error));
            Assert.IsNull(_target.linked, "어느 것인지 모르면 아무것도 붙이지 않아야 합니다.");
            Assert.IsNotNull(error);
        }

        /// <summary>겹친 후보의 경로를 전부 알려 줍니다. 어디에 있는지 모르면 사람이 고를 수 없습니다.</summary>
        [Test]
        public void 겹친_후보의_경로를_알려_준다()
        {
            _assets.Add<WidgetData>(InA);
            _assets.Add<WidgetData>(InB);

            ApplyLink("Sword", out string error);

            StringAssert.Contains(InA, error);
            StringAssert.Contains(InB, error);
            StringAssert.Contains("There are 2", error);
        }

        /// <summary>어떻게 푸는지까지 적습니다. 무엇이 잘못됐는지만 말하면 같은 자리에서 다시 막힙니다.</summary>
        [Test]
        public void 푸는_방법을_함께_알려_준다()
        {
            _assets.Add<WidgetData>(InA);
            _assets.Add<WidgetData>(InB);

            ApplyLink("Sword", out string error);

            StringAssert.Contains("Write the path", error);
            StringAssert.Contains("ReferenceFolder", error);
        }

        /// <summary>이미 배선돼 있던 값은 겹침 때문에 지워지지 않습니다.</summary>
        [Test]
        public void 겹쳐도_기존_배선을_지우지_않는다()
        {
            WidgetData existing = _assets.Add<WidgetData>("Assets/Memory/Kept.asset");
            _serialized.FindProperty("linked").objectReferenceValue = existing;
            _serialized.ApplyModifiedPropertiesWithoutUndo();

            _assets.Add<WidgetData>(InA);
            _assets.Add<WidgetData>(InB);

            Assert.IsFalse(ApplyLink("Sword", out _));
            Assert.AreSame(existing, _target.linked, "정하지 못했다고 손으로 한 배선을 날리면 안 됩니다.");
        }

        // ====================================================================================================
        // 푸는 길
        // ====================================================================================================

        /// <summary>셀에 경로를 적으면 겹친 이름도 정해집니다. 표가 어느 것인지 말할 수 있는 길입니다.</summary>
        [Test]
        public void 경로를_적으면_겹쳐도_정해진다()
        {
            _assets.Add<WidgetData>(InA);
            WidgetData wanted = _assets.Add<WidgetData>(InB);

            Assert.IsTrue(ApplyLink(InB, out string error), error);
            Assert.AreSame(wanted, _target.linked);
        }

        /// <summary>폴더로 범위를 좁히면 겹침이 풀립니다.</summary>
        [Test]
        public void 폴더로_좁히면_겹침이_풀린다()
        {
            _assets.Add<WidgetData>(InA);
            WidgetData wanted = _assets.Add<WidgetData>(InB);
            _assets.EnsureFolder("Assets/Memory/B");

            var binding = new CsvBinding
            {
                Column = "linked",
                PropertyPath = "linked",
                FieldType = typeof(WidgetData),
                Separators = CsvRow.ListSeparators,
                ReferenceFolder = "Assets/Memory/B"
            };

            bool wrote = _binder.Apply(_serialized.FindProperty("linked"), typeof(WidgetData),
                                       "Sword", binding, out string error);
            _serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsTrue(wrote, error);
            Assert.AreSame(wanted, _target.linked);
        }

        /// <summary>없는 경로를 적으면 못 찾은 것으로 다룹니다. 조용히 이름으로 되돌아가지 않습니다.</summary>
        [Test]
        public void 없는_경로는_못_찾은_것으로_다룬다()
        {
            _assets.Add<WidgetData>(InA);

            Assert.IsFalse(ApplyLink("Assets/Memory/없는곳/Sword.asset", out string error));
            Assert.IsNull(_target.linked);
            Assert.IsNotNull(error);
        }
    }
}
