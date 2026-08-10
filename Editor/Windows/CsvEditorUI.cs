using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// 이 패키지의 에디터 화면이 공유하는 <b>치수·아이콘·줄 그리기</b>입니다.
    /// 간격을 자리마다 눈대중으로 정하면 화면이 조금씩 어긋나 보이므로 한 벌로 모읍니다.
    /// 치수는 Unity 자신의 디자인 시스템(App UI)이 쓰는 4px 배수 계단을 따릅니다.
    /// </summary>
    internal static class CsvEditorUI
    {
        // ====================================================================================================
        // 치수
        // ====================================================================================================

        /// <summary>붙어 있는 것들 사이입니다. (아이콘과 글자)</summary>
        public const float GapTight = 4f;

        /// <summary>같은 묶음 안입니다.</summary>
        public const float Gap = 8f;

        /// <summary>다른 묶음 사이입니다.</summary>
        public const float GapWide = 12f;

        /// <summary>문단이 갈리는 자리입니다.</summary>
        public const float GapSection = 16f;

        /// <summary>목록 한 줄의 높이입니다.</summary>
        public const float RowHeight = 20f;

        /// <summary>줄 맨 앞 상태 표시의 너비입니다. 여기가 흔들리면 목록이 훑히지 않습니다.</summary>
        public const float StatusWidth = 18f;

        // ====================================================================================================
        // 색
        // ====================================================================================================

        /// <summary>지금 스킨이 어두운지 여부입니다.</summary>
        private static bool Dark => EditorGUIUtility.isProSkin;

        /// <summary>목록에서 한 줄 걸러 깔리는 바탕입니다. 밝기만 살짝 달리해 훑기를 돕습니다.</summary>
        public static Color RowAlt => Dark ? new Color(1f, 1f, 1f, 0.025f) : new Color(0f, 0f, 0f, 0.025f);

        /// <summary>줄과 줄을 가르는 선입니다.</summary>
        public static Color Divider => Dark ? new Color(0f, 0f, 0f, 0.35f) : new Color(0f, 0f, 0f, 0.12f);

        /// <summary>무엇이 바뀐다는 표시입니다.</summary>
        public static Color Accent => Dark ? new Color(0.45f, 0.72f, 1f) : new Color(0.13f, 0.4f, 0.75f);

        /// <summary>사라지는 것이 있다는 표시입니다.</summary>
        public static Color Danger => Dark ? new Color(1f, 0.55f, 0.4f) : new Color(0.75f, 0.25f, 0.1f);

        /// <summary>덜 중요한 글자입니다.</summary>
        public static Color Muted => Dark ? new Color(1f, 1f, 1f, 0.45f) : new Color(0f, 0f, 0f, 0.45f);

        // ====================================================================================================
        // 아이콘
        // ====================================================================================================

        private static readonly Dictionary<string, GUIContent> IconCache = new Dictionary<string, GUIContent>();

        /// <summary>
        /// 내장 아이콘을 가져옵니다. <b>없으면 null입니다.</b>
        /// 아이콘 이름은 유니티 판마다 달라질 수 있어, 못 찾으면 부르는 쪽이 글자로 물러섭니다.
        /// </summary>
        /// <param name="name">내장 아이콘 이름입니다.</param>
        /// <returns>아이콘이거나, 없으면 null입니다.</returns>
        public static GUIContent Icon(string name)
        {
            if (IconCache.TryGetValue(name, out GUIContent cached)) return cached;

            GUIContent found = null;
            try
            {
                GUIContent content = EditorGUIUtility.IconContent(name);
                if (content != null && content.image != null) found = new GUIContent(content.image);
            }
            catch (System.Exception)
            {
                // 이 판에 없는 아이콘입니다. 글자로 물러섭니다.
            }

            IconCache[name] = found;
            return found;
        }

        /// <summary>아이콘이 있으면 아이콘을, 없으면 글자를 담은 내용입니다.</summary>
        /// <param name="iconName">내장 아이콘 이름입니다.</param>
        /// <param name="text">아이콘을 못 찾았을 때 쓸 글자입니다.</param>
        /// <param name="tooltip">덧붙일 설명입니다.</param>
        /// <returns>그릴 내용입니다.</returns>
        public static GUIContent IconOr(string iconName, string text, string tooltip = null)
        {
            GUIContent icon = Icon(iconName);
            return icon != null
                ? new GUIContent(icon.image, tooltip ?? text)
                : new GUIContent(text, tooltip);
        }

        /// <summary>아이콘과 글자를 함께 담은 내용입니다. 아이콘이 없으면 글자만 남습니다.</summary>
        /// <param name="iconName">내장 아이콘 이름입니다.</param>
        /// <param name="text">함께 보일 글자입니다.</param>
        /// <param name="tooltip">덧붙일 설명입니다.</param>
        /// <returns>그릴 내용입니다.</returns>
        public static GUIContent IconAnd(string iconName, string text, string tooltip = null)
        {
            GUIContent icon = Icon(iconName);
            return icon != null ? new GUIContent(text, icon.image, tooltip) : new GUIContent(text, tooltip);
        }

        // ====================================================================================================
        // 그리기
        // ====================================================================================================

        /// <summary>
        /// 줄 바탕을 깔고 그 자리를 돌려줍니다. <b>내용을 그리기 전에</b> 부르십시오.
        /// </summary>
        /// <param name="rect">줄의 자리입니다.</param>
        /// <param name="index">줄 번호입니다. 홀짝으로 바탕을 번갈아 깝니다.</param>
        /// <param name="selected">고른 줄인지 여부입니다.</param>
        public static void RowBackground(Rect rect, int index, bool selected = false)
        {
            if (Event.current.type != EventType.Repaint) return;

            if (selected) EditorGUI.DrawRect(rect, SelectionColor());
            else if ((index & 1) == 1) EditorGUI.DrawRect(rect, RowAlt);
        }

        /// <summary>고른 줄의 바탕색입니다. 창이 활성인지에 따라 유니티가 쓰는 색을 따릅니다.</summary>
        /// <returns>바탕색입니다.</returns>
        private static Color SelectionColor()
        {
            Color color = GUI.skin.settings.selectionColor;
            color.a = 0.4f;
            return color;
        }

        /// <summary>가로로 얇은 가름선을 긋습니다.</summary>
        /// <param name="space">위아래로 띄울 간격입니다.</param>
        public static void Divider1(float space = GapTight)
        {
            GUILayout.Space(space);
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(rect, Divider);
            GUILayout.Space(space);
        }

        /// <summary>글자를 지정한 색으로 씁니다.</summary>
        /// <param name="text">쓸 글자입니다.</param>
        /// <param name="color">글자색입니다.</param>
        /// <param name="style">쓸 모양입니다. null이면 작은 글씨입니다.</param>
        /// <param name="options">배치 옵션입니다.</param>
        public static void ColoredLabel(string text, Color color, GUIStyle style = null,
                                        params GUILayoutOption[] options)
        {
            Color previous = GUI.contentColor;
            GUI.contentColor = color;
            GUILayout.Label(text, style ?? EditorStyles.miniLabel, options);
            GUI.contentColor = previous;
        }

        /// <summary>
        /// 도구 줄에 묶음을 가르는 세로 선을 넣습니다. 성격이 다른 단추가 붙어 있으면 하나로 읽힙니다.
        /// </summary>
        public static void ToolbarSeparator()
        {
            GUILayout.Space(GapTight);
            Rect rect = GUILayoutUtility.GetRect(1f, 14f, GUILayout.Width(1f));
            rect.y += 2f;
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(rect, Divider);
            GUILayout.Space(GapTight);
        }

        /// <summary>
        /// 무엇이 비어 있을 때의 안내입니다. <b>제목 → 왜 → 할 일 하나</b> 순으로 둡니다.
        /// "없습니다"로 끝내면 사람이 다음에 무엇을 할지 알 수 없습니다.
        /// </summary>
        /// <param name="title">한 줄 제목입니다.</param>
        /// <param name="body">왜 비어 있는지입니다.</param>
        /// <param name="primaryLabel">가장 그럴듯한 다음 행동의 이름입니다. null이면 단추를 두지 않습니다.</param>
        /// <param name="primary">그 행동입니다.</param>
        /// <param name="extras">덜 그럴듯한 행동들입니다. 작은 단추로 나란히 둡니다.</param>
        public static void EmptyState(string title, string body, string primaryLabel = null,
                                      System.Action primary = null,
                                      params (string Label, System.Action Action)[] extras)
        {
            GUILayout.Space(GapSection);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(GapSection);

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label(title, EditorStyles.boldLabel);
                    GUILayout.Space(GapTight);

                    var wrapped = new GUIStyle(EditorStyles.label) { wordWrap = true };
                    Color previous = GUI.contentColor;
                    GUI.contentColor = Muted;
                    GUILayout.Label(body, wrapped);
                    GUI.contentColor = previous;

                    GUILayout.Space(GapWide);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (primaryLabel != null
                            && GUILayout.Button(primaryLabel, GUILayout.Height(24f), GUILayout.MinWidth(140f)))
                        {
                            primary?.Invoke();
                        }

                        foreach ((string label, System.Action action) in extras)
                        {
                            GUILayout.Space(GapTight);
                            if (GUILayout.Button(label, EditorStyles.miniButton,
                                                 GUILayout.Height(24f), GUILayout.MinWidth(90f)))
                            {
                                action?.Invoke();
                            }
                        }

                        GUILayout.FlexibleSpace();
                    }
                }

                GUILayout.Space(GapSection);
            }
        }
    }
}
