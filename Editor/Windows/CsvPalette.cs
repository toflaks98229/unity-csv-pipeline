using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// 이 패키지의 에디터 화면이 쓰는 <b>색과 그 근거</b>입니다.
    /// <para>
    /// 색을 그리기 안에 흩어 두면 "읽히는가"를 확인할 방법이 없습니다. Unity Editor Design System 은
    /// 글자에 <b>4.5:1</b>, 아이콘·비활성 요소에 <b>3:1</b> 의 명암비를 요구합니다(US-0173·US-0174).
    /// 그래서 색을 여기 모으고, 명암비 계산까지 같이 두어 <b>검사가 그 값을 지키게</b> 했습니다.
    /// </para>
    /// <para>
    /// 색은 <b>다 정해진 값</b>입니다. <c>GUI.contentColor</c> 로 반투명하게 덧칠하지 않습니다 —
    /// 그 방식은 바탕 스타일의 글자색과 곱해져 최종 색이 무엇인지 알 수 없게 되고, 그러면 명암비를
    /// 잴 수도 지킬 수도 없습니다.
    /// </para>
    /// </summary>
    public static class CsvPalette
    {
        // ====================================================================================================
        // 기준
        // ====================================================================================================

        /// <summary>글자에 요구되는 최소 명암비입니다. (US-0173)</summary>
        public const float TextContrast = 4.5f;

        /// <summary>아이콘·비활성 요소에 요구되는 최소 명암비입니다. (US-0173 · US-0174)</summary>
        public const float NonTextContrast = 3f;

        /// <summary>
        /// 어두운 스킨의 창 바탕입니다. Unity 가 API 로 알려 주지 않아 기본 스킨 값을 적어 둡니다.
        /// 스킨을 손댄 프로젝트에서는 실제 값과 다를 수 있습니다.
        /// </summary>
        public static readonly Color DarkBackground = new Color32(56, 56, 56, 255);

        /// <summary>밝은 스킨의 창 바탕입니다.</summary>
        public static readonly Color LightBackground = new Color32(194, 194, 194, 255);

        // ====================================================================================================
        // 색
        // ====================================================================================================

        /// <summary>덜 중요한 글자입니다. 그래도 <b>읽히는 것이 먼저</b>라 기준을 넘겨 둡니다.</summary>
        /// <param name="dark">어두운 스킨인지 여부입니다.</param>
        /// <returns>글자색입니다.</returns>
        public static Color Muted(bool dark)
            => dark ? new Color(0.67f, 0.67f, 0.67f) : new Color(0.29f, 0.29f, 0.29f);

        /// <summary>무엇이 바뀐다는 표시입니다. (파랑)</summary>
        /// <param name="dark">어두운 스킨인지 여부입니다.</param>
        /// <returns>글자색입니다.</returns>
        public static Color Accent(bool dark)
            => dark ? new Color(0.45f, 0.72f, 1f) : new Color(0.09f, 0.28f, 0.58f);

        /// <summary>
        /// 조심할 것이 있다는 표시입니다. (노랑) 되돌릴 수 없는 삭제가 여기 듭니다.
        /// 오류와 색을 나누는 것은 Unity 의 뜻 색 규약입니다 — 빨강은 오류, 노랑은 주의.
        /// </summary>
        /// <param name="dark">어두운 스킨인지 여부입니다.</param>
        /// <returns>글자색입니다.</returns>
        public static Color Warning(bool dark)
            => dark ? new Color(1f, 0.8f, 0.35f) : new Color(0.33f, 0.28f, 0f);

        /// <summary>잘못된 것이 있다는 표시입니다. (빨강)</summary>
        /// <param name="dark">어두운 스킨인지 여부입니다.</param>
        /// <returns>글자색입니다.</returns>
        public static Color Danger(bool dark)
            => dark ? new Color(1f, 0.55f, 0.4f) : new Color(0.58f, 0.04f, 0.09f);

        /// <summary>그 스킨의 창 바탕입니다.</summary>
        /// <param name="dark">어두운 스킨인지 여부입니다.</param>
        /// <returns>바탕색입니다.</returns>
        public static Color Background(bool dark) => dark ? DarkBackground : LightBackground;

        /// <summary>
        /// 목록에서 한 줄 걸러 깔리는 바탕입니다. 밝기만 살짝 달리해 훑기를 돕습니다.
        /// </summary>
        /// <param name="dark">어두운 스킨인지 여부입니다.</param>
        /// <returns>바탕색입니다.</returns>
        public static Color RowAlt(bool dark)
            => dark ? new Color(1f, 1f, 1f, 0.025f) : new Color(0f, 0f, 0f, 0.025f);

        /// <summary>
        /// 줄과 줄을 가르는 선입니다. <b>장식입니다</b> — 줄이 갈린다는 사실은 한 줄 걸러 깔리는
        /// 바탕이 이미 나르고 있어, 이 선이 없어도 읽는 데 지장이 없습니다. 그래서 상태 표시에
        /// 요구되는 3:1 을 적용하지 않습니다. 다만 <b>보이기는 해야</b> 하므로 검사가 그것만 봅니다.
        /// </summary>
        /// <param name="dark">어두운 스킨인지 여부입니다.</param>
        /// <returns>선 색입니다.</returns>
        public static Color Divider(bool dark)
            => dark ? new Color(0f, 0f, 0f, 0.35f) : new Color(0f, 0f, 0f, 0.12f);

        /// <summary>
        /// 한 줄 걸러 바탕이 깔린 자리의 실제 바탕색입니다. 명암비는 <b>불리한 쪽</b>으로 재야 하므로
        /// 검사가 이 값도 함께 봅니다.
        /// </summary>
        /// <param name="dark">어두운 스킨인지 여부입니다.</param>
        /// <returns>겹쳐진 바탕색입니다.</returns>
        public static Color AlternateBackground(bool dark) => Over(RowAlt(dark), Background(dark));

        // ====================================================================================================
        // 명암비
        // ====================================================================================================

        /// <summary>
        /// 두 색의 명암비입니다. 같은 색이면 1, 검정과 흰색이면 21입니다. (WCAG 2.1)
        /// </summary>
        /// <param name="a">한쪽 색입니다.</param>
        /// <param name="b">다른 쪽 색입니다.</param>
        /// <returns>명암비입니다.</returns>
        public static float ContrastRatio(Color a, Color b)
        {
            float la = RelativeLuminance(a);
            float lb = RelativeLuminance(b);

            float hi = Mathf.Max(la, lb);
            float lo = Mathf.Min(la, lb);

            return (hi + 0.05f) / (lo + 0.05f);
        }

        /// <summary>WCAG 상대 휘도입니다. 알파는 보지 않습니다 — 먼저 <see cref="Over"/> 로 겹치십시오.</summary>
        /// <param name="color">잴 색입니다.</param>
        /// <returns>0(검정)~1(흰색)의 휘도입니다.</returns>
        public static float RelativeLuminance(Color color)
            => 0.2126f * Linear(color.r) + 0.7152f * Linear(color.g) + 0.0722f * Linear(color.b);

        /// <summary>반투명한 색을 바탕 위에 겹쳐 불투명한 색으로 만듭니다.</summary>
        /// <param name="over">위에 올릴 색입니다.</param>
        /// <param name="under">아래 바탕색입니다.</param>
        /// <returns>겹쳐진 불투명한 색입니다.</returns>
        public static Color Over(Color over, Color under)
        {
            float a = Mathf.Clamp01(over.a);
            return new Color(
                over.r * a + under.r * (1f - a),
                over.g * a + under.g * (1f - a),
                over.b * a + under.b * (1f - a),
                1f);
        }

        /// <summary>sRGB 한 채널을 선형으로 폅니다.</summary>
        /// <param name="channel">0~1의 채널 값입니다.</param>
        /// <returns>선형 값입니다.</returns>
        private static float Linear(float channel)
        {
            channel = Mathf.Clamp01(channel);
            return channel <= 0.03928f
                ? channel / 12.92f
                : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }
    }
}
