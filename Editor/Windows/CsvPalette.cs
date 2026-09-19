using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// The <b>colors and the reasoning behind them</b> that this package's editor screens use.
    /// <para>
    /// Scattering colors through the drawing code leaves no way to check whether they are readable.
    /// The Unity Editor Design System requires a contrast ratio of <b>4.5:1</b> for text and
    /// <b>3:1</b> for icons and disabled elements (US-0173, US-0174). So the colors live here,
    /// together with the contrast math, so that <b>tests can hold them to those numbers</b>.
    /// </para>
    /// <para>
    /// Every color is a <b>fixed value</b>. Nothing is tinted on top with a translucent
    /// <c>GUI.contentColor</c> — that multiplies with the base style's text color, so the final
    /// color becomes unknowable, and a contrast ratio you cannot measure is one you cannot keep.
    /// </para>
    /// </summary>
    public static class CsvPalette
    {
        // ====================================================================================================
        // 기준
        // ====================================================================================================

        /// <summary>Minimum contrast ratio required for text. (US-0173)</summary>
        public const float TextContrast = 4.5f;

        /// <summary>Minimum contrast ratio required for icons and disabled elements. (US-0173, US-0174)</summary>
        public const float NonTextContrast = 3f;

        /// <summary>
        /// Window background of the dark skin. Unity does not expose it through an API, so the
        /// default skin's value is written down here. In a project with a modified skin the real
        /// value may differ.
        /// </summary>
        public static readonly Color DarkBackground = new Color32(56, 56, 56, 255);

        /// <summary>Window background of the light skin.</summary>
        public static readonly Color LightBackground = new Color32(194, 194, 194, 255);

        // ====================================================================================================
        // 색
        // ====================================================================================================

        /// <summary>Less important text. Even so, <b>readability comes first</b>, so it clears the bar.</summary>
        /// <param name="dark">Whether the skin is dark.</param>
        /// <returns>Text color.</returns>
        public static Color Muted(bool dark)
            => dark ? new Color(0.67f, 0.67f, 0.67f) : new Color(0.29f, 0.29f, 0.29f);

        /// <summary>Marks that something changes. (blue)</summary>
        /// <param name="dark">Whether the skin is dark.</param>
        /// <returns>Text color.</returns>
        public static Color Accent(bool dark)
            => dark ? new Color(0.45f, 0.72f, 1f) : new Color(0.09f, 0.28f, 0.58f);

        /// <summary>
        /// Marks that something needs care. (yellow) Irreversible deletion belongs here.
        /// Keeping this color apart from the error color follows Unity's semantic color
        /// convention — red is an error, yellow is a caution.
        /// </summary>
        /// <param name="dark">Whether the skin is dark.</param>
        /// <returns>Text color.</returns>
        public static Color Warning(bool dark)
            => dark ? new Color(1f, 0.8f, 0.35f) : new Color(0.33f, 0.28f, 0f);

        /// <summary>Marks that something is wrong. (red)</summary>
        /// <param name="dark">Whether the skin is dark.</param>
        /// <returns>Text color.</returns>
        public static Color Danger(bool dark)
            => dark ? new Color(1f, 0.55f, 0.4f) : new Color(0.58f, 0.04f, 0.09f);

        /// <summary>Window background of that skin.</summary>
        /// <param name="dark">Whether the skin is dark.</param>
        /// <returns>Background color.</returns>
        public static Color Background(bool dark) => dark ? DarkBackground : LightBackground;

        /// <summary>
        /// Background laid under every other row of a list. Only the brightness shifts slightly, which helps the eye scan.
        /// </summary>
        /// <param name="dark">Whether the skin is dark.</param>
        /// <returns>Background color.</returns>
        public static Color RowAlt(bool dark)
            => dark ? new Color(1f, 1f, 1f, 0.025f) : new Color(0f, 0f, 0f, 0.025f);

        /// <summary>
        /// The line that separates one row from the next. It is <b>decoration</b> — the alternating
        /// row background already carries the fact that rows are separate, so nothing becomes harder
        /// to read without this line. That is why the 3:1 required of status indicators does not
        /// apply here. It does have to be <b>visible</b>, though, and that is all the tests check.
        /// </summary>
        /// <param name="dark">Whether the skin is dark.</param>
        /// <returns>Line color.</returns>
        public static Color Divider(bool dark)
            => dark ? new Color(0f, 0f, 0f, 0.35f) : new Color(0f, 0f, 0f, 0.12f);

        /// <summary>
        /// The actual background color where the alternating row background is laid down. Contrast
        /// must be measured against the <b>worse case</b>, so the tests check this value too.
        /// </summary>
        /// <param name="dark">Whether the skin is dark.</param>
        /// <returns>Composited background color.</returns>
        public static Color AlternateBackground(bool dark) => Over(RowAlt(dark), Background(dark));

        // ====================================================================================================
        // 명암비
        // ====================================================================================================

        /// <summary>
        /// Contrast ratio between two colors. Identical colors give 1; black against white gives 21. (WCAG 2.1)
        /// </summary>
        /// <param name="a">One color.</param>
        /// <param name="b">The other color.</param>
        /// <returns>Contrast ratio.</returns>
        public static float ContrastRatio(Color a, Color b)
        {
            float la = RelativeLuminance(a);
            float lb = RelativeLuminance(b);

            float hi = Mathf.Max(la, lb);
            float lo = Mathf.Min(la, lb);

            return (hi + 0.05f) / (lo + 0.05f);
        }

        /// <summary>WCAG relative luminance. Alpha is ignored — composite with <see cref="Over"/> first.</summary>
        /// <param name="color">Color to measure.</param>
        /// <returns>Luminance from 0 (black) to 1 (white).</returns>
        public static float RelativeLuminance(Color color)
            => 0.2126f * Linear(color.r) + 0.7152f * Linear(color.g) + 0.0722f * Linear(color.b);

        /// <summary>Composites a translucent color over a background into an opaque color.</summary>
        /// <param name="over">Color to lay on top.</param>
        /// <param name="under">Background color underneath.</param>
        /// <returns>Composited opaque color.</returns>
        public static Color Over(Color over, Color under)
        {
            float a = Mathf.Clamp01(over.a);
            return new Color(
                over.r * a + under.r * (1f - a),
                over.g * a + under.g * (1f - a),
                over.b * a + under.b * (1f - a),
                1f);
        }

        /// <summary>Expands one sRGB channel into linear space.</summary>
        /// <param name="channel">Channel value from 0 to 1.</param>
        /// <returns>Linear value.</returns>
        private static float Linear(float channel)
        {
            channel = Mathf.Clamp01(channel);
            return channel <= 0.03928f
                ? channel / 12.92f
                : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }
    }
}
