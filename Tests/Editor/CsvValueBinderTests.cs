using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 셀 원문이 각 타입의 필드에 제대로 들어가는지 검사합니다.
    /// 이 계층은 컴파일만으로는 아무것도 보증되지 않아 실제로 써 보는 것이 유일한 확인 방법입니다.
    /// </summary>
    public sealed class CsvValueBinderTests
    {
        private BinderTarget _target;
        private SerializedObject _serialized;
        private CsvValueBinder _binder;

        /// <summary>매 검사마다 새 대상을 만듭니다. 에셋으로 저장하지 않습니다.</summary>
        [SetUp]
        public void SetUp()
        {
            _target = ScriptableObject.CreateInstance<BinderTarget>();
            _serialized = new SerializedObject(_target);
            _binder = new CsvValueBinder();
        }

        /// <summary>대상을 정리합니다.</summary>
        [TearDown]
        public void TearDown()
        {
            if (_target != null) UnityEngine.Object.DestroyImmediate(_target);
        }

        /// <summary>기본 설정의 열 연결을 만듭니다.</summary>
        /// <param name="column">열 이름입니다.</param>
        /// <param name="overwriteWhenEmpty">빈 셀로 덮어쓸지 여부입니다.</param>
        /// <param name="separators">리스트 구분자입니다.</param>
        /// <returns>만들어진 연결입니다.</returns>
        private static CsvBinding Binding(string column, bool overwriteWhenEmpty = false, char[] separators = null)
            => new CsvBinding
            {
                Column = column,
                PropertyPath = column,
                Separators = separators ?? CsvRow.ListSeparators,
                OverwriteWhenEmpty = overwriteWhenEmpty
            };

        /// <summary>필드에 값을 쓰고 결과를 반영합니다.</summary>
        /// <param name="field">대상 필드 이름입니다.</param>
        /// <param name="raw">셀 원문입니다.</param>
        /// <param name="error">오류 메시지를 받습니다.</param>
        /// <param name="binding">쓸 연결 설정입니다. null이면 기본값입니다.</param>
        /// <returns>값을 실제로 썼으면 true입니다.</returns>
        private bool Apply(string field, string raw, out string error, CsvBinding binding = null)
        {
            CsvBinding used = binding ?? Binding(field);
            Type fieldType = typeof(BinderTarget).GetField(field)?.FieldType;

            bool wrote = _binder.Apply(_serialized.FindProperty(field), fieldType, raw, used, out error);
            _serialized.ApplyModifiedPropertiesWithoutUndo();
            return wrote;
        }

        // ====================================================================================================
        // 스칼라
        // ====================================================================================================

        /// <summary>문자열을 씁니다.</summary>
        [Test]
        public void 문자열을_쓴다()
        {
            Assert.IsTrue(Apply("text", "새 값", out string error));
            Assert.IsNull(error);
            Assert.AreEqual("새 값", _target.text);
        }

        /// <summary>정수를 씁니다.</summary>
        [Test]
        public void 정수를_쓴다()
        {
            Assert.IsTrue(Apply("count", "42", out _));
            Assert.AreEqual(42, _target.count);
        }

        /// <summary>정수가 아니면 쓰지 않고 이유를 알립니다.</summary>
        [Test]
        public void 정수가_아니면_이유를_알린다()
        {
            Assert.IsFalse(Apply("count", "열둘", out string error));
            Assert.IsNotNull(error);
            Assert.AreEqual(7, _target.count, "실패했으면 기존 값이 남아야 합니다.");
        }

        /// <summary>정수처럼 보이는 값도 실수 필드에 들어갑니다.</summary>
        [Test]
        public void 정수처럼_보여도_실수_필드에_들어간다()
        {
            Assert.IsTrue(Apply("speed", "30", out _));
            Assert.AreEqual(30f, _target.speed, 0.0001f);
        }

        /// <summary>배정밀도 필드도 씁니다.</summary>
        [Test]
        public void 배정밀도_필드를_쓴다()
        {
            Assert.IsTrue(Apply("precise", "0.125", out _));
            Assert.AreEqual(0.125d, _target.precise, 0.000001d);
        }

        /// <summary>참/거짓 표기를 모두 받습니다.</summary>
        [Test]
        public void 참거짓_표기를_모두_받는다()
        {
            Assert.IsTrue(Apply("flag", "TRUE", out _));
            Assert.IsTrue(_target.flag);

            Assert.IsTrue(Apply("flag", "false", out _));
            Assert.IsFalse(_target.flag);

            Assert.IsTrue(Apply("flag", "1", out _));
            Assert.IsTrue(_target.flag);

            Assert.IsFalse(Apply("flag", "예", out string error));
            Assert.IsNotNull(error);
        }

        /// <summary>
        /// 열거형은 이름으로 지정하며 대소문자를 가리지 않습니다.
        /// <see cref="Grade"/>는 값과 인덱스가 어긋나 있어, 인덱스 기반 처리가 맞는지 함께 확인합니다.
        /// </summary>
        [Test]
        public void 열거형을_이름으로_쓴다()
        {
            Assert.IsTrue(Apply("grade", "medium", out _));
            Assert.AreEqual(Grade.Medium, _target.grade);

            Assert.IsTrue(Apply("grade", "Large", out _));
            Assert.AreEqual(Grade.Large, _target.grade);
        }

        /// <summary>없는 열거형 값이면 가능한 값을 알려 줍니다.</summary>
        [Test]
        public void 없는_열거형_값은_후보를_알려_준다()
        {
            Assert.IsFalse(Apply("grade", "Huge", out string error));
            StringAssert.Contains("Small", error);
            Assert.AreEqual(Grade.Small, _target.grade);
        }

        /// <summary>HTML 색상 표기를 받습니다.</summary>
        [Test]
        public void 색상을_쓴다()
        {
            Assert.IsTrue(Apply("tint", "#FF0000", out _));
            Assert.AreEqual(1f, _target.tint.r, 0.001f);
            Assert.AreEqual(0f, _target.tint.g, 0.001f);
        }

        /// <summary>벡터는 공백이나 리스트 구분자로 나눕니다.</summary>
        [Test]
        public void 벡터를_쓴다()
        {
            Assert.IsTrue(Apply("offset", "1 2 3", out _));
            Assert.AreEqual(new Vector3(1, 2, 3), _target.offset);

            Assert.IsTrue(Apply("offset", "4;5;6", out _));
            Assert.AreEqual(new Vector3(4, 5, 6), _target.offset);
        }

        /// <summary>성분 개수가 맞지 않으면 쓰지 않습니다.</summary>
        [Test]
        public void 벡터_성분_개수가_틀리면_쓰지_않는다()
        {
            Assert.IsFalse(Apply("offset", "1 2", out string error));
            Assert.IsNotNull(error);
        }

        // ====================================================================================================
        // 리스트
        // ====================================================================================================

        /// <summary>리스트 셀을 구분자로 나눠 채웁니다.</summary>
        [Test]
        public void 문자열_리스트를_채운다()
        {
            Assert.IsTrue(Apply("tags", "가;나|다", out _));

            Assert.AreEqual(3, _target.tags.Count);
            Assert.AreEqual("가", _target.tags[0]);
            Assert.AreEqual("다", _target.tags[2]);
        }

        /// <summary>지정한 구분자만 씁니다.</summary>
        [Test]
        public void 리스트_구분자를_지정할_수_있다()
        {
            Assert.IsTrue(Apply("tags", "가|나", out _, Binding("tags", separators: new[] { '|' })));

            Assert.AreEqual(2, _target.tags.Count);
        }

        /// <summary>열거형 배열도 채웁니다.</summary>
        [Test]
        public void 열거형_배열을_채운다()
        {
            Assert.IsTrue(Apply("grades", "Small;Large", out _));

            Assert.AreEqual(2, _target.grades.Length);
            Assert.AreEqual(Grade.Small, _target.grades[0]);
            Assert.AreEqual(Grade.Large, _target.grades[1]);
        }

        /// <summary>덮어쓰기로 지정하면 빈 셀이 리스트를 비웁니다.</summary>
        [Test]
        public void 빈_셀로_리스트를_비울_수_있다()
        {
            Apply("tags", "가;나", out _);
            Assert.AreEqual(2, _target.tags.Count);

            Assert.IsTrue(Apply("tags", string.Empty, out _, Binding("tags", overwriteWhenEmpty: true)));
            Assert.AreEqual(0, _target.tags.Count);
        }

        // ====================================================================================================
        // 빈 셀
        // ====================================================================================================

        /// <summary>기본값은 보존입니다. 인스펙터에서 저작한 값이 빈 셀 때문에 날아가지 않습니다.</summary>
        [Test]
        public void 빈_셀은_기본적으로_기존_값을_보존한다()
        {
            Assert.IsFalse(Apply("text", string.Empty, out string error));
            Assert.IsNull(error, "보존은 오류가 아닙니다.");
            Assert.AreEqual("기존값", _target.text);
        }

        /// <summary>덮어쓰기를 켜면 빈 셀이 값을 지웁니다.</summary>
        [Test]
        public void 덮어쓰기를_켜면_빈_셀이_값을_지운다()
        {
            Assert.IsTrue(Apply("text", string.Empty, out _, Binding("text", overwriteWhenEmpty: true)));
            Assert.AreEqual(string.Empty, _target.text);
        }

        // ====================================================================================================
        // 오브젝트 참조
        // ====================================================================================================

        /// <summary>찾지 못한 참조는 이름을 붙여 알립니다.</summary>
        [Test]
        public void 없는_참조는_이름을_붙여_알린다()
        {
            Assert.IsFalse(Apply("icon", "존재하지않는에셋", out string error));
            StringAssert.Contains("존재하지않는에셋", error);
            Assert.IsNull(_target.icon);
        }

        /// <summary>빈 셀을 덮어쓰기로 주면 참조를 비웁니다.</summary>
        [Test]
        public void 참조를_비울_수_있다()
        {
            Assert.IsTrue(Apply("icon", string.Empty, out _, Binding("icon", overwriteWhenEmpty: true)));
            Assert.IsNull(_target.icon);
        }

        /// <summary>대상 필드를 찾지 못하면 이유를 알립니다.</summary>
        [Test]
        public void 없는_필드는_이유를_알린다()
        {
            Assert.IsFalse(_binder.Apply(null, typeof(string), "값", Binding("nope"), out string error));
            Assert.IsNotNull(error);
        }
    }
}
