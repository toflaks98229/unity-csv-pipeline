using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// The <b>metrics, icons, and row drawing</b> shared by this package's editor screens.
    /// Eyeballing spacing at each site leaves the screen subtly out of alignment, so it is gathered into one set.
    /// The metrics follow the 4px-multiple scale that Unity's own design system (App UI) uses.
    /// The colors live in <see cref="CsvPalette"/>, where tests hold them to their contrast ratios.
    /// </summary>
    internal static class CsvEditorUI
    {
        // ====================================================================================================
        // 치수
        // ====================================================================================================

        /// <summary>Between things that belong together. (an icon and its text)</summary>
        public const float GapTight = 4f;

        /// <summary>Within one group.</summary>
        public const float Gap = 8f;

        /// <summary>Between different groups.</summary>
        public const float GapWide = 12f;

        /// <summary>Where one section ends and the next begins.</summary>
        public const float GapSection = 16f;

        /// <summary>Height of one list row.</summary>
        public const float RowHeight = 20f;

        /// <summary>Width of the status indicator at the head of a row. If this wobbles, the list stops being scannable.</summary>
        public const float StatusWidth = 18f;

        /// <summary>Width of the status bar raised at the far left of a row.</summary>
        public const float StatusBarWidth = 3f;

        // ====================================================================================================
        // 색
        // ====================================================================================================

        /// <summary>Whether the current skin is dark.</summary>
        private static bool Dark => EditorGUIUtility.isProSkin;

        /// <summary>Background laid under every other row of a list.</summary>
        public static Color RowAlt => CsvPalette.RowAlt(Dark);

        /// <summary>The line that separates one row from the next.</summary>
        public static Color Divider => CsvPalette.Divider(Dark);

        /// <summary>Marks that something changes.</summary>
        public static Color Accent => CsvPalette.Accent(Dark);

        /// <summary>Marks that something needs care.</summary>
        public static Color Warning => CsvPalette.Warning(Dark);

        /// <summary>Marks that something disappears or that something is wrong.</summary>
        public static Color Danger => CsvPalette.Danger(Dark);

        /// <summary>Less important text.</summary>
        public static Color Muted => CsvPalette.Muted(Dark);

        /// <summary>Background laid under the row the mouse is over.</summary>
        public static Color RowHover => Dark ? new Color(1f, 1f, 1f, 0.06f) : new Color(0f, 0f, 0f, 0.05f);

        // ====================================================================================================
        // 아이콘
        // ====================================================================================================

        private static readonly Dictionary<string, GUIContent> IconCache = new Dictionary<string, GUIContent>();

        /// <summary>
        /// Fetches a built-in icon. <b>Returns null if there is none.</b>
        /// Icon names can change between Unity versions, so when one is not found the caller falls back to text.
        /// </summary>
        /// <param name="name">Built-in icon name.</param>
        /// <returns>The icon, or null if there is none.</returns>
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

        /// <summary>Content holding the icon if there is one, and the text otherwise.</summary>
        /// <param name="iconName">Built-in icon name.</param>
        /// <param name="text">Text to use when the icon is not found.</param>
        /// <param name="tooltip">Explanation to attach.</param>
        /// <returns>Content to draw.</returns>
        public static GUIContent IconOr(string iconName, string text, string tooltip = null)
        {
            GUIContent icon = Icon(iconName);
            return icon != null
                ? new GUIContent(icon.image, tooltip ?? text)
                : new GUIContent(text, tooltip);
        }

        /// <summary>Content holding both the icon and the text. If there is no icon, only the text remains.</summary>
        /// <param name="iconName">Built-in icon name.</param>
        /// <param name="text">Text to show alongside.</param>
        /// <param name="tooltip">Explanation to attach.</param>
        /// <returns>Content to draw.</returns>
        public static GUIContent IconAnd(string iconName, string text, string tooltip = null)
        {
            GUIContent icon = Icon(iconName);
            return icon != null ? new GUIContent(text, icon.image, tooltip) : new GUIContent(text, tooltip);
        }

        // ====================================================================================================
        // 스타일
        // ====================================================================================================

        /// <summary>A style built for one base style and one color.</summary>
        private struct TintedStyle
        {
            public GUIStyle Basis;
            public Color Color;
            public GUIStyle Result;
        }

        private static readonly List<TintedStyle> TintCache = new List<TintedStyle>();
        private static bool _tintCacheDark;

        /// <summary>
        /// A style with only the text color changed. There is a <b>reason this does not use
        /// <c>GUI.contentColor</c></b> — that <b>multiplies</b> with the base style's text color, so the
        /// final color becomes unknowable, and then the contrast ratios <see cref="CsvPalette"/> keeps
        /// are not kept on screen.
        /// <para>
        /// The styles it builds are held on to. Drawing keeps running while the mouse moves, and
        /// building a new <c>GUIStyle</c> every time would pile up garbage from merely leaving the window open.
        /// </para>
        /// </summary>
        /// <param name="basis">Style to build on.</param>
        /// <param name="color">Text color to use.</param>
        /// <returns>A style that writes text in that color.</returns>
        public static GUIStyle Tinted(GUIStyle basis, Color color)
        {
            if (basis == null) basis = EditorStyles.label;

            // 스킨이 바뀌면 색이 통째로 달라집니다. 들고 있던 것을 버려야 합니다.
            if (_tintCacheDark != Dark)
            {
                _tintCacheDark = Dark;
                TintCache.Clear();
            }

            for (int i = 0; i < TintCache.Count; i++)
            {
                TintedStyle entry = TintCache[i];
                if (ReferenceEquals(entry.Basis, basis) && entry.Color == color) return entry.Result;
            }

            var style = new GUIStyle(basis);
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.focused.textColor = color;
            style.active.textColor = color;

            // 스타일이 무한히 쌓일 자리는 아니지만, 혹시 늘어나면 통째로 버립니다.
            if (TintCache.Count > 32) TintCache.Clear();
            TintCache.Add(new TintedStyle { Basis = basis, Color = color, Result = style });

            return style;
        }

        // ====================================================================================================
        // 그리기
        // ====================================================================================================

        /// <summary>
        /// Lays down the row background. Call this <b>before drawing the content</b>.
        /// </summary>
        /// <param name="rect">Rect of the row.</param>
        /// <param name="index">Row number. Odd and even rows alternate the background.</param>
        /// <param name="selected">Whether this is the selected row.</param>
        /// <param name="hovered">Whether the mouse is over this row.</param>
        public static void RowBackground(Rect rect, int index, bool selected = false, bool hovered = false)
        {
            if (Event.current.type != EventType.Repaint) return;

            if (selected) EditorGUI.DrawRect(rect, SelectionColor());
            else if ((index & 1) == 1) EditorGUI.DrawRect(rect, RowAlt);

            if (hovered && !selected) EditorGUI.DrawRect(rect, RowHover);
        }

        /// <summary>
        /// Raises a status-colored bar at the far left of a row.
        /// It adds <b>position</b> on top of icon, word, and color, so that scanning the list vertically only asks the eye to follow one line.
        /// </summary>
        /// <param name="rect">Rect of the row.</param>
        /// <param name="color">Color of the status.</param>
        public static void StatusBar(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;

            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 1f, StatusBarWidth, rect.height - 2f), color);
        }

        /// <summary>Background color of the selected row. It follows the color Unity uses for whether the window is focused.</summary>
        /// <returns>Background color.</returns>
        private static Color SelectionColor()
        {
            Color color = GUI.skin.settings.selectionColor;
            color.a = 0.4f;
            return color;
        }

        /// <summary>Draws a thin horizontal separator line.</summary>
        /// <param name="space">Space to leave above and below.</param>
        public static void Divider1(float space = GapTight)
        {
            GUILayout.Space(space);
            Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(rect, Divider);
            GUILayout.Space(space);
        }

        /// <summary>Writes text in the given color.</summary>
        /// <param name="text">Text to write.</param>
        /// <param name="color">Text color.</param>
        /// <param name="style">Style to write it in. Null means the mini label style.</param>
        /// <param name="options">Layout options.</param>
        public static void ColoredLabel(string text, Color color, GUIStyle style = null,
                                        params GUILayoutOption[] options)
            => GUILayout.Label(text, Tinted(style ?? EditorStyles.miniLabel, color), options);

        /// <summary>Writes text in the given color. A tooltip can be attached.</summary>
        /// <param name="content">Content to write.</param>
        /// <param name="color">Text color.</param>
        /// <param name="style">Style to write it in. Null means the mini label style.</param>
        /// <param name="options">Layout options.</param>
        public static void ColoredLabel(GUIContent content, Color color, GUIStyle style = null,
                                        params GUILayoutOption[] options)
            => GUILayout.Label(content, Tinted(style ?? EditorStyles.miniLabel, color), options);

        /// <summary>
        /// Puts a vertical line in the toolbar to separate groups. Buttons of different kinds sitting together read as one group.
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
        /// The guidance shown when something is empty. It goes <b>title, then why, then one thing to do</b>.
        /// Ending at "there is nothing here" leaves the person with no idea what to do next.
        /// </summary>
        /// <param name="title">One-line title.</param>
        /// <param name="body">Why it is empty.</param>
        /// <param name="primaryLabel">Name of the most likely next action. Null draws no button.</param>
        /// <param name="primary">That action.</param>
        /// <param name="extras">Less likely actions. They sit side by side as mini buttons.</param>
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

                    GUILayout.Label(body, Wrapped);

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

        private static GUIStyle _wrapped;

        /// <summary>Less important text that wraps onto several lines.</summary>
        private static GUIStyle Wrapped
        {
            get
            {
                GUIStyle tinted = Tinted(EditorStyles.label, Muted);
                if (_wrapped == null || !_wrapped.wordWrap || _wrapped.normal.textColor != Muted)
                {
                    _wrapped = new GUIStyle(tinted) { wordWrap = true };
                }
                return _wrapped;
            }
        }
    }
}
