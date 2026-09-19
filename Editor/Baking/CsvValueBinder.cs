using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// The assets that a single name matches. <b>The fact that there may be several is itself held as a value</b> —
    /// overwriting the dictionary entry leaves no way to ask how many matched.
    /// </summary>
    internal sealed class CsvNameMatch
    {
        /// <summary>The first asset met. Used only when exactly one matched.</summary>
        public UnityEngine.Object Asset;

        /// <summary>Paths of the assets carrying this name.</summary>
        public readonly List<string> Paths = new List<string>();
    }

    /// <summary>
    /// Writes raw cell text into a <see cref="SerializedProperty"/>. Handles lists, enums, and object references.
    /// It caches object reference lookups, so create one per import and reuse it.
    /// </summary>
    public sealed class CsvValueBinder
    {
        /// <summary>Separators allowed when splitting Vector types. (A comma clashes with CSV, so it only works inside quotes.)</summary>
        private static readonly char[] VectorSeparators = { ';', '|', ' ', ',' };

        /// <summary>Name index per type and folder. Reused within the same import.</summary>
        private readonly Dictionary<string, Dictionary<string, CsvNameMatch>> _references
            = new Dictionary<string, Dictionary<string, CsvNameMatch>>();

        /// <summary>
        /// Applies raw cell text to a property.
        /// </summary>
        /// <param name="property">Target property.</param>
        /// <param name="fieldType">C# type of the target field. Used for list and reference handling.</param>
        /// <param name="raw">Raw cell text.</param>
        /// <param name="binding">Column binding settings.</param>
        /// <param name="error">Receives why the value could not be applied. Null on success.</param>
        /// <returns>True when a value was actually written. (False when skipped to preserve the existing value, and then error is null too.)</returns>
        public bool Apply(SerializedProperty property, Type fieldType, string raw, CsvBinding binding, out string error)
        {
            error = null;
            if (property == null) { error = "Target field not found."; return false; }

            bool empty = string.IsNullOrEmpty(raw);
            if (empty && !binding.OverwriteWhenEmpty) return false;   // 빈 셀 = 기존 값 보존

            if (IsList(property))
            {
                return ApplyList(property, fieldType, raw, binding, out error);
            }

            return ApplyScalar(property, fieldType, raw, binding, out error);
        }

        /// <summary>
        /// Checks whether an integer value fits the target field <b>without being truncated</b>.
        /// <para>
        /// <see cref="SerializedProperty.longValue"/> writes into any integer field, but when the target
        /// is narrower Unity silently truncates. The number written in the table and the number baked into
        /// the asset then differ, with neither an error nor a warning, so we catch it here before baking.
        /// </para>
        /// </summary>
        /// <param name="property">Target property.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="error">Receives the reason when the value does not fit.</param>
        /// <returns>True when the value fits as written.</returns>
        private static bool FitsIntegerField(SerializedProperty property, long value, out string error)
        {
            error = null;

            long min;
            long max;
            switch (property.type)
            {
                case "int":    min = int.MinValue;    max = int.MaxValue;    break;
                case "uint":   min = uint.MinValue;   max = uint.MaxValue;   break;
                case "short":  min = short.MinValue;  max = short.MaxValue;  break;
                case "ushort": min = ushort.MinValue; max = ushort.MaxValue; break;
                case "sbyte":  min = sbyte.MinValue;  max = sbyte.MaxValue;  break;
                case "byte":   min = byte.MinValue;   max = byte.MaxValue;   break;
                case "char":   min = char.MinValue;   max = char.MaxValue;   break;

                // long·ulong 은 long 이 담을 수 있는 값이면 그대로 들어갑니다.
                // (ulong 의 상위 절반은 애초에 long.TryParse 가 받지 못해 '정수가 아닙니다'로 걸립니다)
                default: return true;
            }

            if (value >= min && value <= max) return true;

            error = $"Out of the range {property.type} can hold: {value.ToString(CultureInfo.InvariantCulture)} "
                  + $"({min.ToString(CultureInfo.InvariantCulture)} ~ {max.ToString(CultureInfo.InvariantCulture)})";
            return false;
        }

        /// <summary>Tells whether the property is an array or list. (A string also reports isArray, so it is filtered out by type.)</summary>
        /// <param name="property">Property to inspect.</param>
        /// <returns>True for an array or list.</returns>
        private static bool IsList(SerializedProperty property)
            => property.isArray && property.propertyType != SerializedPropertyType.String;

        /// <summary>
        /// Fills an array property with the tokens split by the separators.
        /// <para>
        /// <b>If even one token fails to convert, the list is left untouched.</b> A scalar keeps its
        /// existing value when the value is wrong, but truncating the list first and then filling it would
        /// wipe out a hand-authored list entirely over one mistyped cell. The same mistake must not end
        /// differently depending on the field type.
        /// </para>
        /// </summary>
        /// <param name="property">Target array property.</param>
        /// <param name="fieldType">C# type of the target field.</param>
        /// <param name="raw">Raw cell text.</param>
        /// <param name="binding">Column binding settings.</param>
        /// <param name="error">Receives why the value could not be applied.</param>
        /// <returns>True when applied. False when even one token failed to convert, and the list stays as it was.</returns>
        private bool ApplyList(SerializedProperty property, Type fieldType, string raw, CsvBinding binding, out string error)
        {
            error = null;
            string[] tokens = CsvRow.SplitList(raw, binding.Separators);
            Type elementType = ElementTypeOf(fieldType);

            // 셀에 뭔가 적혀 있는데 토큰이 하나도 안 나오면, 그것은 "비우라"가 아니라 셀이 잘못된
            // 것입니다. 구분자만 남은 셀(';;')이 대표적인데 — 내보내기가 다룰 줄 모르는 원소를 빈
            // 문자열로 쓰면 그런 셀이 나옵니다 — 그대로 두면 손으로 저작한 목록이 경고 한 줄 없이
            // 통째로 비워집니다. 빈 셀은 위에서 이미 '보존'으로 갈라져 여기 오지 않습니다.
            if (tokens.Length == 0)
            {
                // 비우는 것을 저작으로 삼겠다고 선언했으면 그 뜻대로 비웁니다.
                if (binding.OverwriteWhenEmpty)
                {
                    property.arraySize = 0;
                    return true;
                }

                error = $"Nothing to read as a list: '{raw}' (only separators, or every element is empty) "
                      + "If you mean to clear the list, add [CsvColumn(OverwriteWhenEmpty = true)].";
                return false;
            }

            List<string> failures = FindListFailures(property, elementType, tokens, binding);
            if (failures != null)
            {
                error = string.Join(" / ", failures);
                return false;
            }

            property.arraySize = tokens.Length;
            for (int i = 0; i < tokens.Length; i++)
            {
                ApplyScalar(property.GetArrayElementAtIndex(i), elementType, tokens[i], binding, out _);
            }
            return true;
        }

        /// <summary>
        /// Checks <b>up front</b> whether the tokens convert to the element type.
        /// <para>
        /// It appends one probe slot at the end of the array, bakes only into that, then takes it back off.
        /// The real elements are never touched. The judgement is handed to <see cref="ApplyScalar"/> as is
        /// because writing a second copy of the parsing rules here would make <b>the previewed result and
        /// the actually baked result diverge</b>.
        /// </para>
        /// </summary>
        /// <param name="property">Target array property.</param>
        /// <param name="elementType">C# type of the element.</param>
        /// <param name="tokens">Tokens to check.</param>
        /// <param name="binding">Column binding settings.</param>
        /// <returns>The failure reasons, or null when every token converts.</returns>
        private List<string> FindListFailures(SerializedProperty property, Type elementType,
                                              string[] tokens, CsvBinding binding)
        {
            if (tokens.Length == 0) return null;

            int original = property.arraySize;
            property.arraySize = original + 1;
            SerializedProperty probe = property.GetArrayElementAtIndex(original);

            List<string> failures = null;
            for (int i = 0; i < tokens.Length; i++)
            {
                if (ApplyScalar(probe, elementType, tokens[i], binding, out string itemError)) continue;
                if (itemError == null) continue;

                if (failures == null) failures = new List<string>();
                failures.Add($"[{i}] {itemError}");
            }

            property.arraySize = original;
            return failures;
        }

        /// <summary>Writes one scalar value into a property.</summary>
        /// <param name="property">Target property.</param>
        /// <param name="fieldType">C# type of the target field.</param>
        /// <param name="raw">Raw value text.</param>
        /// <param name="binding">Column binding settings.</param>
        /// <param name="error">Receives why the value could not be applied.</param>
        /// <returns>True when applied.</returns>
        private bool ApplyScalar(SerializedProperty property, Type fieldType, string raw, CsvBinding binding, out string error)
        {
            error = null;
            raw = raw?.Trim() ?? string.Empty;

            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                    property.stringValue = raw;
                    return true;

                case SerializedPropertyType.Integer:
                    if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                    {
                        // 대상이 int 인데 long 값을 넣으면 Unity가 조용히 잘라 넣습니다 —
                        // 2147483648 이 2147483647 로 구워지고 아무 말도 남지 않습니다. 이 패키지의
                        // 규칙은 "해석하지 못하면 기존 값을 남긴다"이고, 범위를 넘은 값은 해석하지
                        // 못한 것입니다. 다른 타입과 같게 다룹니다.
                        if (!FitsIntegerField(property, l, out string overflow))
                        {
                            error = overflow;
                            return false;
                        }
                        property.longValue = l;
                        return true;
                    }
                    error = $"Not an integer: '{raw}'";
                    return false;

                case SerializedPropertyType.Float:
                    if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    {
                        if (property.type == "double") { property.doubleValue = d; return true; }

                        // float 범위를 넘으면 (float)d 가 Infinity 가 됩니다. 표에 적은 수가
                        // 무한대로 구워지는 것은 값을 잃은 것이지 반영한 것이 아닙니다.
                        float f = (float)d;
                        if (float.IsInfinity(f) && !double.IsInfinity(d))
                        {
                            error = $"Out of the range float can hold: '{raw}' "
                                  + $"(up to ±{float.MaxValue.ToString("R", CultureInfo.InvariantCulture)})";
                            return false;
                        }
                        property.floatValue = f;
                        return true;
                    }
                    error = $"Not a floating-point number: '{raw}'";
                    return false;

                case SerializedPropertyType.Boolean:
                    if (TryParseBool(raw, out bool b)) { property.boolValue = b; return true; }
                    error = $"Not a true/false value: '{raw}' (TRUE/FALSE/1/0)";
                    return false;

                case SerializedPropertyType.Enum:
                    int index = Array.FindIndex(property.enumNames,
                        n => string.Equals(n, raw, StringComparison.OrdinalIgnoreCase));
                    if (index >= 0) { property.enumValueIndex = index; return true; }
                    error = $"'{raw}' is not one of the values. (allowed: {string.Join("/", property.enumNames)})";
                    return false;

                case SerializedPropertyType.Color:
                    if (ColorUtility.TryParseHtmlString(raw, out Color color)) { property.colorValue = color; return true; }
                    error = $"Not a color: '{raw}' (e.g. #FFFFFF)";
                    return false;

                case SerializedPropertyType.Vector2:
                    if (TryParseNumbers(raw, 2, out float[] v2)) { property.vector2Value = new Vector2(v2[0], v2[1]); return true; }
                    error = $"2 numbers are required: '{raw}'";
                    return false;

                case SerializedPropertyType.Vector3:
                    if (TryParseNumbers(raw, 3, out float[] v3)) { property.vector3Value = new Vector3(v3[0], v3[1], v3[2]); return true; }
                    error = $"3 numbers are required: '{raw}'";
                    return false;

                case SerializedPropertyType.Vector4:
                    if (TryParseNumbers(raw, 4, out float[] v4)) { property.vector4Value = new Vector4(v4[0], v4[1], v4[2], v4[3]); return true; }
                    error = $"4 numbers are required: '{raw}'";
                    return false;

                case SerializedPropertyType.ObjectReference:
                    if (raw.Length == 0) { property.objectReferenceValue = null; return true; }

                    UnityEngine.Object found = Resolve(fieldType, raw, binding.ReferenceFolder,
                                                       out List<string> ambiguous);
                    if (found != null) { property.objectReferenceValue = found; return true; }

                    // 여럿이면 아무거나 골라 두지 않습니다. 고르면 다음 임포트에서 다른 것이 붙을 수 있는데,
                    // 그 사이 사람은 배선이 맞다고 믿습니다. 못 찾은 것보다 나쁜 종류의 실패입니다.
                    error = ambiguous != null
                        ? DescribeAmbiguity(raw, Describe(fieldType), ambiguous)
                        : $"No {Describe(fieldType)} asset named '{raw}' was found. Leaving the value as it is.";
                    return false;

                default:
                    error = $"{property.propertyType} cannot be authored from a table.";
                    return false;
            }
        }

        /// <summary>
        /// Whether this kind of value can be authored from a table.
        /// <para>
        /// <b>This list must stay the same as the cases in <see cref="ApplyScalar"/>.</b> Growing only one
        /// side makes table creation <b>hand out a column that cannot be read back</b>; shrinking only one
        /// side drops an authorable field from the table. The two places sit next to each other and a test
        /// ties them together.
        /// </para>
        /// </summary>
        /// <param name="type">Property kind to check.</param>
        /// <returns>True when it can be authored from a table.</returns>
        public static bool CanAuthor(SerializedPropertyType type)
        {
            switch (type)
            {
                case SerializedPropertyType.String:
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Float:
                case SerializedPropertyType.Boolean:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.Color:
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Vector4:
                case SerializedPropertyType.ObjectReference:
                    return true;

                default:
                    return false;
            }
        }

        // ====================================================================================================
        // 보조
        // ====================================================================================================

        /// <summary>"TRUE"/"1" is true, "FALSE"/"0" is false.</summary>
        /// <param name="raw">Raw value text.</param>
        /// <param name="value">Receives the parsed value.</param>
        /// <returns>True when the text was parsed.</returns>
        private static bool TryParseBool(string raw, out bool value)
        {
            if (raw.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || raw == "1") { value = true; return true; }
            if (raw.Equals("FALSE", StringComparison.OrdinalIgnoreCase) || raw == "0") { value = false; return true; }
            value = false;
            return false;
        }

        /// <summary>Pulls exactly the given count of numbers out of the raw text.</summary>
        /// <param name="raw">Raw value text.</param>
        /// <param name="count">How many numbers are required.</param>
        /// <param name="values">Receives the pulled values.</param>
        /// <returns>True when the count matches and every part is a number.</returns>
        private static bool TryParseNumbers(string raw, int count, out float[] values)
        {
            values = null;
            string[] parts = raw.Split(VectorSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != count) return false;

            var parsed = new float[count];
            for (int i = 0; i < count; i++)
            {
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed[i]))
                    return false;
            }
            values = parsed;
            return true;
        }

        /// <summary>Takes the element type out of a list or array type. Returns the type itself when it is not a list.</summary>
        /// <param name="fieldType">Type to inspect.</param>
        /// <returns>The element type.</returns>
        private static Type ElementTypeOf(Type fieldType)
        {
            if (fieldType == null) return null;
            if (fieldType.IsArray) return fieldType.GetElementType();
            if (fieldType.IsGenericType && fieldType.GetGenericArguments().Length == 1)
                return fieldType.GetGenericArguments()[0];
            return fieldType;
        }

        /// <summary>
        /// Finds the object asset a cell points at. Caches the index per type and folder.
        /// <para>
        /// When names collide it <b>does not pick one</b> and hands back the colliding paths. Writing the
        /// path itself in the cell settles it — that is the only way the table can say which one it means.
        /// </para>
        /// </summary>
        /// <param name="fieldType">Asset type to look for.</param>
        /// <param name="raw">Raw cell text. Either an asset name (without the extension) or an asset path.</param>
        /// <param name="folder">Folder that narrows the search. Empty means the whole project.</param>
        /// <param name="ambiguous">Receives the colliding paths when the name collides. Null otherwise.</param>
        /// <returns>The asset found, or null.</returns>
        private UnityEngine.Object Resolve(Type fieldType, string raw, string folder, out List<string> ambiguous)
        {
            ambiguous = null;
            Type target = ElementTypeOf(fieldType) ?? typeof(UnityEngine.Object);

            // 경로를 적었으면 색인을 거치지 않습니다. 겹친 이름을 사람이 풀어 주는 자리입니다.
            if (LooksLikePath(raw)) return CsvAssets.Current.Load(raw.Replace('\\', '/'), target);

            string key = target.Name + "|" + (folder ?? string.Empty);

            if (!_references.TryGetValue(key, out Dictionary<string, CsvNameMatch> index))
            {
                index = BuildIndex(target, folder);
                _references[key] = index;
            }

            if (!index.TryGetValue(raw, out CsvNameMatch match)) return null;

            if (match.Paths.Count > 1)
            {
                ambiguous = match.Paths;
                return null;
            }
            return match.Asset;
        }

        /// <summary>Tells whether the cell holds a path rather than a name.</summary>
        /// <param name="raw">Raw cell text.</param>
        /// <returns>True when it looks like a path.</returns>
        private static bool LooksLikePath(string raw)
            => raw.IndexOf('/') >= 0 || raw.IndexOf('\\') >= 0;

        /// <summary>
        /// Indexes assets of the given type by name. <b>It gathers same-named assets instead of overwriting them.</b>
        /// If it overwrote, which one survived would be decided by the search order, and that order is not guaranteed.
        /// </summary>
        /// <param name="target">Asset type to index.</param>
        /// <param name="folder">Folder that bounds the search.</param>
        /// <returns>A dictionary of name to the assets that matched.</returns>
        private static Dictionary<string, CsvNameMatch> BuildIndex(Type target, string folder)
        {
            // 열 이름·필드 이름·열거형 값이 모두 대소문자를 무시하는데 참조 이름만 가리면,
            // 셀에 Sword 라고 적었을 때 sword.asset 을 못 찾고 "없습니다" 라고 답합니다.
            // 대소문자만 다른 에셋이 실제로 둘 있으면 한 자리에 모여 "겹쳤다" 로 걸립니다 —
            // 이 패키지가 이미 고른 답이고, 저장소가 대소문자를 안 가리는 이상 그것이 사실입니다.
            var index = new Dictionary<string, CsvNameMatch>(StringComparer.OrdinalIgnoreCase);
            ICsvAssetGateway assets = CsvAssets.Current;

            foreach (string path in assets.FindPaths($"t:{target.Name}", folder))
            {
                UnityEngine.Object asset = assets.Load(path, target);
                if (asset == null) continue;

                string name = Path.GetFileNameWithoutExtension(path);
                if (!index.TryGetValue(name, out CsvNameMatch match))
                {
                    match = new CsvNameMatch();
                    index[name] = match;
                }

                match.Paths.Add(path);
                if (match.Asset == null) match.Asset = asset;
            }
            return index;
        }

        /// <summary>Maximum number of candidate paths to show. Beyond that, only the remaining count is reported.</summary>
        private const int MaxCandidatesShown = 5;

        /// <summary>
        /// Puts the name collision, together with the candidates, into one paragraph.
        /// <b>It also writes how to resolve it</b> — saying only what went wrong leaves the person stuck at the same spot again.
        /// </summary>
        /// <param name="raw">Name written in the cell.</param>
        /// <param name="typeName">Name of the asset type being looked for.</param>
        /// <param name="paths">Paths of the assets carrying that name.</param>
        /// <returns>The explanatory paragraph.</returns>
        private static string DescribeAmbiguity(string raw, string typeName, List<string> paths)
        {
            // 늘 같은 순서로 말해야 사람이 지난번 메시지와 비교할 수 있습니다.
            var sorted = new List<string>(paths);
            sorted.Sort(StringComparer.Ordinal);

            var text = new StringBuilder();
            text.Append($"There are {sorted.Count} {typeName} assets named '{raw}', so which one is meant cannot be settled. Leaving the value as it is.");

            int shown = Math.Min(sorted.Count, MaxCandidatesShown);
            for (int i = 0; i < shown; i++) text.AppendLine().Append("  ").Append(sorted[i]);
            if (sorted.Count > shown) text.AppendLine().Append($"  … and {sorted.Count - shown} more");

            text.AppendLine().Append("Write the path itself in the cell, or narrow the range with [CsvColumn(ReferenceFolder = \"…\")].");
            return text.ToString();
        }

        /// <summary>Type name to use in error messages.</summary>
        /// <param name="fieldType">Type to describe.</param>
        /// <returns>The type name.</returns>
        private static string Describe(Type fieldType) => (ElementTypeOf(fieldType) ?? typeof(UnityEngine.Object)).Name;
    }
}
