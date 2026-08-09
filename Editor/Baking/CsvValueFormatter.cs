using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// 필드 값을 셀 문자열로 되돌립니다. <see cref="CsvValueBinder"/>의 반대 방향이며,
    /// 내보내기와 미리보기가 같은 표기를 쓰도록 한 곳에 둡니다.
    /// </summary>
    public static class CsvValueFormatter
    {
        /// <summary>리스트를 이을 때 쓰는 기본 구분자입니다.</summary>
        public const char DefaultSeparator = ';';

        /// <summary>프로퍼티 값을 셀 문자열로 되돌립니다.</summary>
        /// <param name="property">읽을 프로퍼티입니다. null이면 빈 문자열입니다.</param>
        /// <param name="separators">리스트를 이을 때 쓸 구분자들입니다. 비우면 기본값입니다.</param>
        /// <returns>셀 문자열입니다.</returns>
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

        /// <summary>스칼라 프로퍼티 하나를 셀 문자열로 되돌립니다.</summary>
        /// <param name="property">읽을 프로퍼티입니다.</param>
        /// <returns>셀 문자열입니다.</returns>
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

        /// <summary>숫자들을 공백으로 이어 붙입니다. (읽는 쪽이 공백 구분을 받습니다)</summary>
        /// <param name="values">이어 붙일 값들입니다.</param>
        /// <returns>셀 문자열입니다.</returns>
        private static string Join(params float[] values)
        {
            var parts = new string[values.Length];
            for (int i = 0; i < values.Length; i++) parts[i] = values[i].ToString("R", CultureInfo.InvariantCulture);
            return string.Join(" ", parts);
        }
    }
}
