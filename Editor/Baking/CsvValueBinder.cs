using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// 셀 원문을 <see cref="SerializedProperty"/>에 써 넣습니다. 리스트·열거형·오브젝트 참조까지 다룹니다.
    /// 오브젝트 참조 조회 결과를 캐시하므로 임포트 한 번당 하나를 만들어 쓰십시오.
    /// </summary>
    public sealed class CsvValueBinder
    {
        /// <summary>Vector 계열을 나눌 때 허용하는 구분자들입니다. (쉼표는 CSV와 충돌하므로 따옴표 안에서만 유효)</summary>
        private static readonly char[] VectorSeparators = { ';', '|', ' ', ',' };

        /// <summary>타입+폴더별 이름 색인입니다. 같은 임포트 안에서 재사용합니다.</summary>
        private readonly Dictionary<string, Dictionary<string, UnityEngine.Object>> _references
            = new Dictionary<string, Dictionary<string, UnityEngine.Object>>();

        /// <summary>
        /// 셀 원문을 프로퍼티에 반영합니다.
        /// </summary>
        /// <param name="property">대상 프로퍼티입니다.</param>
        /// <param name="fieldType">대상 필드의 C# 타입입니다. 리스트·참조 처리에 씁니다.</param>
        /// <param name="raw">셀 원문입니다.</param>
        /// <param name="binding">열 연결 설정입니다.</param>
        /// <param name="error">반영하지 못한 이유를 받습니다. 성공하면 null입니다.</param>
        /// <returns>실제로 값을 썼으면 true입니다. (보존해서 건너뛴 경우 false, 이때 error도 null)</returns>
        public bool Apply(SerializedProperty property, Type fieldType, string raw, CsvBinding binding, out string error)
        {
            error = null;
            if (property == null) { error = "대상 필드를 찾지 못했습니다."; return false; }

            bool empty = string.IsNullOrEmpty(raw);
            if (empty && !binding.OverwriteWhenEmpty) return false;   // 빈 셀 = 기존 값 보존

            if (IsList(property))
            {
                return ApplyList(property, fieldType, raw, binding, out error);
            }

            return ApplyScalar(property, fieldType, raw, binding, out error);
        }

        /// <summary>배열/리스트 프로퍼티인지 판정합니다. (문자열도 isArray가 참이라 타입으로 걸러 냄)</summary>
        /// <param name="property">검사할 프로퍼티입니다.</param>
        /// <returns>배열/리스트면 true입니다.</returns>
        private static bool IsList(SerializedProperty property)
            => property.isArray && property.propertyType != SerializedPropertyType.String;

        /// <summary>구분자로 나눈 토큰들을 배열 프로퍼티에 채웁니다.</summary>
        /// <param name="property">대상 배열 프로퍼티입니다.</param>
        /// <param name="fieldType">대상 필드의 C# 타입입니다.</param>
        /// <param name="raw">셀 원문입니다.</param>
        /// <param name="binding">열 연결 설정입니다.</param>
        /// <param name="error">반영하지 못한 이유를 받습니다.</param>
        /// <returns>반영했으면 true입니다.</returns>
        private bool ApplyList(SerializedProperty property, Type fieldType, string raw, CsvBinding binding, out string error)
        {
            error = null;
            string[] tokens = CsvRow.SplitList(raw, binding.Separators);
            Type elementType = ElementTypeOf(fieldType);

            property.arraySize = tokens.Length;
            var failures = new List<string>();

            for (int i = 0; i < tokens.Length; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                if (!ApplyScalar(element, elementType, tokens[i], binding, out string itemError) && itemError != null)
                {
                    failures.Add($"[{i}] {itemError}");
                }
            }

            if (failures.Count > 0) error = string.Join(" / ", failures);
            return true;
        }

        /// <summary>스칼라 값 하나를 프로퍼티에 씁니다.</summary>
        /// <param name="property">대상 프로퍼티입니다.</param>
        /// <param name="fieldType">대상 필드의 C# 타입입니다.</param>
        /// <param name="raw">값 원문입니다.</param>
        /// <param name="binding">열 연결 설정입니다.</param>
        /// <param name="error">반영하지 못한 이유를 받습니다.</param>
        /// <returns>반영했으면 true입니다.</returns>
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
                        property.longValue = l;
                        return true;
                    }
                    error = $"정수가 아닙니다: '{raw}'";
                    return false;

                case SerializedPropertyType.Float:
                    if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    {
                        if (property.type == "double") property.doubleValue = d;
                        else property.floatValue = (float)d;
                        return true;
                    }
                    error = $"실수가 아닙니다: '{raw}'";
                    return false;

                case SerializedPropertyType.Boolean:
                    if (TryParseBool(raw, out bool b)) { property.boolValue = b; return true; }
                    error = $"참/거짓이 아닙니다: '{raw}' (TRUE/FALSE/1/0)";
                    return false;

                case SerializedPropertyType.Enum:
                    int index = Array.FindIndex(property.enumNames,
                        n => string.Equals(n, raw, StringComparison.OrdinalIgnoreCase));
                    if (index >= 0) { property.enumValueIndex = index; return true; }
                    error = $"'{raw}'는 없는 값입니다. (가능: {string.Join("/", property.enumNames)})";
                    return false;

                case SerializedPropertyType.Color:
                    if (ColorUtility.TryParseHtmlString(raw, out Color color)) { property.colorValue = color; return true; }
                    error = $"색상이 아닙니다: '{raw}' (예: #FFFFFF)";
                    return false;

                case SerializedPropertyType.Vector2:
                    if (TryParseNumbers(raw, 2, out float[] v2)) { property.vector2Value = new Vector2(v2[0], v2[1]); return true; }
                    error = $"숫자 2개가 필요합니다: '{raw}'";
                    return false;

                case SerializedPropertyType.Vector3:
                    if (TryParseNumbers(raw, 3, out float[] v3)) { property.vector3Value = new Vector3(v3[0], v3[1], v3[2]); return true; }
                    error = $"숫자 3개가 필요합니다: '{raw}'";
                    return false;

                case SerializedPropertyType.Vector4:
                    if (TryParseNumbers(raw, 4, out float[] v4)) { property.vector4Value = new Vector4(v4[0], v4[1], v4[2], v4[3]); return true; }
                    error = $"숫자 4개가 필요합니다: '{raw}'";
                    return false;

                case SerializedPropertyType.ObjectReference:
                    if (raw.Length == 0) { property.objectReferenceValue = null; return true; }

                    UnityEngine.Object found = Resolve(fieldType, raw, binding.ReferenceFolder);
                    if (found != null) { property.objectReferenceValue = found; return true; }
                    error = $"'{raw}'라는 이름의 {Describe(fieldType)} 에셋을 찾지 못했습니다.";
                    return false;

                default:
                    error = $"{property.propertyType} 타입은 표로 저작할 수 없습니다.";
                    return false;
            }
        }

        // ====================================================================================================
        // 보조
        // ====================================================================================================

        /// <summary>"TRUE"/"1"은 참, "FALSE"/"0"은 거짓입니다.</summary>
        /// <param name="raw">값 원문입니다.</param>
        /// <param name="value">해석된 값을 받습니다.</param>
        /// <returns>해석했으면 true입니다.</returns>
        private static bool TryParseBool(string raw, out bool value)
        {
            if (raw.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || raw == "1") { value = true; return true; }
            if (raw.Equals("FALSE", StringComparison.OrdinalIgnoreCase) || raw == "0") { value = false; return true; }
            value = false;
            return false;
        }

        /// <summary>원문에서 숫자를 정확히 지정 개수만큼 뽑습니다.</summary>
        /// <param name="raw">값 원문입니다.</param>
        /// <param name="count">필요한 숫자 개수입니다.</param>
        /// <param name="values">뽑아 낸 값들을 받습니다.</param>
        /// <returns>개수가 맞고 전부 숫자면 true입니다.</returns>
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

        /// <summary>리스트/배열 타입에서 원소 타입을 꺼냅니다. 리스트가 아니면 그대로 돌려줍니다.</summary>
        /// <param name="fieldType">검사할 타입입니다.</param>
        /// <returns>원소 타입입니다.</returns>
        private static Type ElementTypeOf(Type fieldType)
        {
            if (fieldType == null) return null;
            if (fieldType.IsArray) return fieldType.GetElementType();
            if (fieldType.IsGenericType && fieldType.GetGenericArguments().Length == 1)
                return fieldType.GetGenericArguments()[0];
            return fieldType;
        }

        /// <summary>이름으로 오브젝트 에셋을 찾습니다. 타입+폴더 단위로 색인을 캐시합니다.</summary>
        /// <param name="fieldType">찾을 에셋 타입입니다.</param>
        /// <param name="name">에셋 이름입니다. (확장자 제외)</param>
        /// <param name="folder">검색을 한정할 폴더입니다. 비우면 프로젝트 전체입니다.</param>
        /// <returns>찾은 에셋이거나 null입니다.</returns>
        private UnityEngine.Object Resolve(Type fieldType, string name, string folder)
        {
            Type target = ElementTypeOf(fieldType) ?? typeof(UnityEngine.Object);
            string key = target.Name + "|" + (folder ?? string.Empty);

            if (!_references.TryGetValue(key, out Dictionary<string, UnityEngine.Object> index))
            {
                index = BuildIndex(target, folder);
                _references[key] = index;
            }

            return index.TryGetValue(name, out UnityEngine.Object asset) ? asset : null;
        }

        /// <summary>지정 타입의 에셋을 이름으로 색인합니다.</summary>
        /// <param name="target">색인할 에셋 타입입니다.</param>
        /// <param name="folder">검색 범위 폴더입니다.</param>
        /// <returns>이름 → 에셋 사전입니다.</returns>
        private static Dictionary<string, UnityEngine.Object> BuildIndex(Type target, string folder)
        {
            var index = new Dictionary<string, UnityEngine.Object>();
            ICsvAssetGateway assets = CsvAssets.Current;

            foreach (string path in assets.FindPaths($"t:{target.Name}", folder))
            {
                UnityEngine.Object asset = assets.Load(path, target);
                if (asset != null) index[Path.GetFileNameWithoutExtension(path)] = asset;
            }
            return index;
        }

        /// <summary>오류 메시지에 쓸 타입 이름입니다.</summary>
        /// <param name="fieldType">설명할 타입입니다.</param>
        /// <returns>타입 이름입니다.</returns>
        private static string Describe(Type fieldType) => (ElementTypeOf(fieldType) ?? typeof(UnityEngine.Object)).Name;
    }
}
