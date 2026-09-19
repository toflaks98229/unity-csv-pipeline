using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CsvPipeline.Tests
{
    /// <summary>
    /// 없는 내장 아이콘을 물어봐도 <b>조용히</b> 글자로 물러서는지 봅니다.
    /// <para>
    /// 아이콘 이름은 유니티 판·플랫폼마다 갈리므로 없는 이름을 만나는 것은 정상입니다.
    /// 문제는 그때 <c>EditorGUIUtility.IconContent</c> 가 예외가 아니라 오류 로그를 남긴다는 것입니다 —
    /// 화면은 설계대로 글자로 멀쩡히 물러서는데 검사 틀만 그 로그를 잡아 깨집니다.
    /// </para>
    /// <para>
    /// <b>저작된 표의 상태에 기대지 않고 여기서 직접 물어봅니다.</b> 창을 그려서 이 갈래에 닿는 길은
    /// 표가 하나도 그 상태가 아니게 되는 순간 조용히 사라지고, 그러면 결함이 남아 있어도 초록이 뜹니다.
    /// </para>
    /// </summary>
    public sealed class CsvEditorIconTests
    {
        /// <summary>
        /// <c>CsvEditorUI</c> 는 이 어셈블리에서 <c>internal</c> 이라 되비쳐 부릅니다.
        /// 형제(<see cref="CsvPalette"/> 등)처럼 <c>public</c> 으로 열면 곧바로 부를 수 있습니다.
        /// </summary>
        /// <param name="iconName">내장 아이콘 이름입니다.</param>
        /// <param name="text">아이콘을 못 찾았을 때 쓸 글자입니다.</param>
        /// <param name="tooltip">덧붙일 설명입니다.</param>
        /// <returns>그릴 내용입니다.</returns>
        private static GUIContent IconOr(string iconName, string text, string tooltip)
        {
            Type type = typeof(CsvPalette).Assembly.GetType("CsvPipeline.CsvEditorUI");
            Assert.That(type, Is.Not.Null, "CsvEditorUI 를 찾지 못했습니다.");

            MethodInfo method = type.GetMethod(
                "IconOr", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "CsvEditorUI.IconOr 를 찾지 못했습니다.");

            return (GUIContent)method.Invoke(null, new object[] { iconName, text, tooltip });
        }

        /// <summary>이 실행에 분명히 없는 이름입니다. 캐시가 가려 주지 않도록 매번 새로 짓습니다.</summary>
        /// <returns>없는 아이콘 이름입니다.</returns>
        private static string MissingIconName()
            => "csvpipeline_없는_아이콘_" + Guid.NewGuid().ToString("N");

        // ====================================================================================================

        /// <summary>
        /// 없는 아이콘을 물어보면 글자로 물러서고, <b>오류 로그를 남기지 않습니다.</b>
        /// 로그가 새어 나가면 검사 틀이 "Unhandled log message" 로 이 검사를 깹니다.
        /// </summary>
        [Test]
        public void 없는_아이콘은_조용히_글자로_물러선다()
        {
            GUIContent content = IconOr(MissingIconName(), "·", "꺼져 있습니다");

            Assert.That(content.image, Is.Null, "없는 아이콘인데 그림이 돌아왔습니다.");
            Assert.That(content.text, Is.EqualTo("·"), "글자로 물러서지 않았습니다.");
        }

        /// <summary>
        /// 여러 번 물어봐도 마찬가지입니다. 한 번 물어본 이름은 캐시에 남으므로,
        /// <b>처음 묻는 이름</b>마다 조용한지가 실제로 걸리는 조건입니다.
        /// </summary>
        [Test]
        public void 처음_묻는_이름마다_조용하다()
        {
            for (int i = 0; i < 3; i++)
            {
                GUIContent content = IconOr(MissingIconName(), "/", "건너뜁니다");
                Assert.That(content.image, Is.Null);
            }
        }

        /// <summary>
        /// 있는 아이콘은 그대로 그림으로 돌아옵니다. 로그를 끄는 것이 <b>찾기 자체를 망가뜨리지</b>
        /// 않았는지 못 박습니다. 이것이 없으면 "전부 글자로 물러서기"도 위 검사를 통과해 버립니다.
        /// </summary>
        [Test]
        public void 있는_아이콘은_그림으로_돌아온다()
        {
            GUIContent content = IconOr("console.erroricon.sml", "!", "문제가 있습니다");

            Assert.That(content.image, Is.Not.Null, "있는 아이콘인데 글자로 물러섰습니다.");
        }
    }
}
