using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// Turns field values back into cell text. This is the opposite direction of <see cref="CsvValueBinder"/>,
    /// and it lives in one place so that export and preview use the same notation.
    /// </summary>
    public static class CsvValueFormatter
    {
        /// <summary>Default separator used when joining a list.</summary>
        public const char DefaultSeparator = ';';

        /// <summary>Turns a property value back into cell text.</summary>
        /// <param name="property">Property to read. Null yields an empty string.</param>
        /// <param name="separators">Separators to join a list with. Empty means the default.</param>
        /// <returns>The cell text.</returns>
        public static string Format(SerializedProperty property, char[] separators = null)
        {
            if (property == null) return string.Empty;

            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                char separator = separators != null && separators.Length > 0 ? separators[0] : DefaultSeparator;

                var parts = new List<string>(property.arraySize);
                for (int i = 0; i < property.arraySize; i++)
                {
                    parts.Add(FormatScalar(property.GetArrayElementAtIndex(i)));
                }
                return string.Join(separator.ToString(), parts);
            }

            return FormatScalar(property);
        }

        /// <summary>Turns one scalar property back into cell text.</summary>
        /// <param name="property">Property to read.</param>
        /// <returns>The cell text.</returns>
        public static string FormatScalar(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                    return property.stringValue ?? string.Empty;

                case SerializedPropertyType.Integer:
                    return property.longValue.ToString(CultureInfo.InvariantCulture);

                case SerializedPropertyType.Float:
                    return property.type == "double"
                        ? property.doubleValue.ToString("R", CultureInfo.InvariantCulture)
                        : property.floatValue.ToString("R", CultureInfo.InvariantCulture);

                case SerializedPropertyType.Boolean:
                    return property.boolValue ? "TRUE" : "FALSE";

                case SerializedPropertyType.Enum:
                {
                    int index = property.enumValueIndex;
                    string[] names = property.enumNames;
                    return index >= 0 && index < names.Length ? names[index] : string.Empty;
                }

                case SerializedPropertyType.Color:
                    return "#" + ColorUtility.ToHtmlStringRGBA(property.colorValue);

                case SerializedPropertyType.Vector2:
                    return Join(property.vector2Value.x, property.vector2Value.y);

                case SerializedPropertyType.Vector3:
                    return Join(property.vector3Value.x, property.vector3Value.y, property.vector3Value.z);

                case SerializedPropertyType.Vector4:
                    return Join(property.vector4Value.x, property.vector4Value.y,
                                property.vector4Value.z, property.vector4Value.w);

                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue == null ? string.Empty : property.objectReferenceValue.name;

                default:
                    return string.Empty;
            }
        }

        /// <summary>Joins numbers with a space. (The reading side accepts space separation.)</summary>
        /// <param name="values">Values to join.</param>
        /// <returns>The cell text.</returns>
        private static string Join(params float[] values)
        {
            var parts = new string[values.Length];
            for (int i = 0; i < values.Length; i++) parts[i] = values[i].ToString("R", CultureInfo.InvariantCulture);
            return string.Join(" ", parts);
        }
    }
}
