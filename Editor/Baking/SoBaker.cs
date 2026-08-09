using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// CSV 값을 <see cref="SerializedObject"/> 필드에 반영하는 공용 세터 모음입니다.
    /// (Item/Combat 임포터에 중복되던 <c>SetString/Int/Float/Bool/Enum/Object/Vector3/Color</c>를 통합)
    /// </summary>
    public static class SoBaker
    {
        /// <summary>경고 로그에 붙이는 접두 태그입니다.</summary>
        private const string TAG = "[SoBaker]";

        // --- string ---

        /// <summary>문자열 필드에 값을 기록합니다. 필드가 없으면 무시합니다.</summary>
        /// <param name="so">대상 직렬화 객체입니다.</param>
        /// <param name="field">필드 이름입니다.</param>
        /// <param name="value">기록할 값입니다.</param>
        public static void SetString(SerializedObject so, string field, string value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.stringValue = value;
        }

        /// <summary>문자열 필드에 값을 기록하되, 값이 비어 있으면 기존 값을 보존합니다.</summary>
        /// <param name="so">대상 직렬화 객체입니다.</param>
        /// <param name="field">필드 이름입니다.</param>
        /// <param name="value">기록할 값입니다. 비어 있으면 아무것도 하지 않습니다.</param>
        public static void SetStringIf(SerializedObject so, string field, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            SetString(so, field, value);
        }

        // --- numeric / bool (value overloads) ---

        /// <summary>정수 필드에 값을 기록합니다. 필드가 없으면 무시합니다.</summary>
        /// <param name="so">대상 직렬화 객체입니다.</param>
        /// <param name="field">필드 이름입니다.</param>
        /// <param name="value">기록할 값입니다.</param>
        public static void SetInt(SerializedObject so, string field, int value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.intValue = value;
        }

        /// <summary>실수 필드에 값을 기록합니다. 필드가 없으면 무시합니다.</summary>
        /// <param name="so">대상 직렬화 객체입니다.</param>
        /// <param name="field">필드 이름입니다.</param>
        /// <param name="value">기록할 값입니다.</param>
        public static void SetFloat(SerializedObject so, string field, float value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.floatValue = value;
        }

        /// <summary>불리언 필드에 값을 기록합니다. 필드가 없으면 무시합니다.</summary>
        /// <param name="so">대상 직렬화 객체입니다.</param>
        /// <param name="field">필드 이름입니다.</param>
        /// <param name="value">기록할 값입니다.</param>
        public static void SetBool(SerializedObject so, string field, bool value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.boolValue = value;
        }

        // --- numeric / bool (raw-string, skip-if-empty overloads) ---

        /// <summary>CSV 원문 문자열을 정수로 파싱해 기록합니다. 빈 셀이나 파싱 실패 시 기존 값을 보존합니다.</summary>
        /// <param name="so">대상 직렬화 객체입니다.</param>
        /// <param name="field">필드 이름입니다.</param>
        /// <param name="raw">CSV 셀 원문입니다. 로케일 독립(InvariantCulture)으로 파싱합니다.</param>
        public static void SetIntIf(SerializedObject so, string field, string raw)
        {
            if (string.IsNullOrEmpty(raw)) return;
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) SetInt(so, field, v);
        }

        /// <summary>CSV 원문 문자열을 실수로 파싱해 기록합니다. 빈 셀이나 파싱 실패 시 기존 값을 보존합니다.</summary>
        /// <param name="so">대상 직렬화 객체입니다.</param>
        /// <param name="field">필드 이름입니다.</param>
        /// <param name="raw">CSV 셀 원문입니다. 로케일 독립(InvariantCulture)으로 파싱합니다.</param>
        public static void SetFloatIf(SerializedObject so, string field, string raw)
        {
            if (string.IsNullOrEmpty(raw)) return;
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) SetFloat(so, field, v);
        }

        /// <summary>CSV 원문 문자열을 불리언으로 해석해 기록합니다. 빈 셀이면 기존 값을 보존합니다.</summary>
        /// <param name="so">대상 직렬화 객체입니다.</param>
        /// <param name="field">필드 이름입니다.</param>
        /// <param name="raw">CSV 셀 원문입니다. "TRUE"(대소문자 무관) 또는 "1"만 참으로 봅니다.</param>
        public static void SetBoolIf(SerializedObject so, string field, string raw)
        {
            if (string.IsNullOrEmpty(raw)) return;
            bool v = raw.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || raw == "1";
            SetBool(so, field, v);
        }

        // --- enum ---

        /// <summary>열거형 필드를 이름으로 지정해 기록합니다. 빈 값이면 기존 값을 보존합니다.</summary>
        /// <param name="so">대상 직렬화 객체입니다.</param>
        /// <param name="field">필드 이름입니다.</param>
        /// <param name="name">열거형 상수 이름입니다. (대소문자 무관)</param>
        public static void SetEnumIf(SerializedObject so, string field, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            SetEnumByName(so.FindProperty(field), name);
        }

        /// <summary>열거형 프로퍼티에 이름으로 값을 기록합니다. 이름을 찾지 못하면 경고를 남기고 기존 값을 보존합니다.</summary>
        /// <param name="prop">대상 열거형 프로퍼티입니다.</param>
        /// <param name="name">열거형 상수 이름입니다. enumNames 인덱스 매칭이라 게임플레이 enum 타입을 직접 참조하지 않습니다.</param>
        public static void SetEnumByName(SerializedProperty prop, string name)
        {
            if (prop == null || string.IsNullOrEmpty(name)) return;
            int idx = Array.FindIndex(prop.enumNames, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) prop.enumValueIndex = idx;
            else Debug.LogWarning($"{TAG} enum 값 '{name}'을(를) 찾지 못했습니다. (가능: {string.Join("/", prop.enumNames)})");
        }

        // --- object reference ---

        /// <summary>오브젝트 참조 필드에 에셋을 연결합니다. 필드가 없으면 무시합니다.</summary>
        /// <param name="so">대상 직렬화 객체입니다.</param>
        /// <param name="field">필드 이름입니다.</param>
        /// <param name="value">연결할 에셋입니다. null이면 참조를 비웁니다.</param>
        public static void SetObjectRef(SerializedObject so, string field, UnityEngine.Object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.objectReferenceValue = value;
        }

        // --- vector / color ---

        /// <summary>Vector3 필드의 각 축을 개별 기록합니다. 빈 축은 기존 값을 보존하므로 축 단위 부분 갱신이 가능합니다.</summary>
        /// <param name="so">대상 직렬화 객체입니다.</param>
        /// <param name="field">필드 이름입니다.</param>
        /// <param name="x">X 성분 원문입니다. 비어 있으면 유지합니다.</param>
        /// <param name="y">Y 성분 원문입니다. 비어 있으면 유지합니다.</param>
        /// <param name="z">Z 성분 원문입니다. 비어 있으면 유지합니다.</param>
        public static void SetVector3If(SerializedObject so, string field, string x, string y, string z)
        {
            if (string.IsNullOrEmpty(x) && string.IsNullOrEmpty(y) && string.IsNullOrEmpty(z)) return;
            SerializedProperty p = so.FindProperty(field);
            if (p == null) return;

            Vector3 v = p.vector3Value;
            if (float.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out float fx)) v.x = fx;
            if (float.TryParse(y, NumberStyles.Float, CultureInfo.InvariantCulture, out float fy)) v.y = fy;
            if (float.TryParse(z, NumberStyles.Float, CultureInfo.InvariantCulture, out float fz)) v.z = fz;
            p.vector3Value = v;
        }

        /// <summary>HTML 색상 문자열(#RRGGBB 등)을 파싱해 색상 필드에 기록합니다. 빈 셀이면 보존, 파싱 실패면 경고를 남깁니다.</summary>
        /// <param name="so">대상 직렬화 객체입니다.</param>
        /// <param name="field">필드 이름입니다.</param>
        /// <param name="raw">색상 원문입니다. (예: #FFFFFF)</param>
        public static void SetColorIf(SerializedObject so, string field, string raw)
        {
            if (string.IsNullOrEmpty(raw)) return;
            if (ColorUtility.TryParseHtmlString(raw, out Color c))
            {
                SerializedProperty p = so.FindProperty(field);
                if (p != null) p.colorValue = c;
            }
            else
            {
                Debug.LogWarning($"{TAG} 색상 '{raw}' 파싱 실패 (예: #FFFFFF)");
            }
        }
    }
}
