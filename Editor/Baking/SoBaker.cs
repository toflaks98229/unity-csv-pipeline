using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// A shared set of setters that apply CSV values to <see cref="SerializedObject"/> fields.
    /// (Unifies the <c>SetString/Int/Float/Bool/Enum/Object/Vector3/Color</c> that were duplicated in the Item and Combat importers.)
    /// </summary>
    public static class SoBaker
    {
        /// <summary>Prefix tag attached to warning logs.</summary>
        private const string TAG = "[SoBaker]";

        // --- string ---

        /// <summary>Writes a value into a string field. Ignored when the field does not exist.</summary>
        /// <param name="so">Target serialized object.</param>
        /// <param name="field">Field name.</param>
        /// <param name="value">Value to write.</param>
        public static void SetString(SerializedObject so, string field, string value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.stringValue = value;
        }

        /// <summary>Writes a value into a string field, but preserves the existing value when the value is empty.</summary>
        /// <param name="so">Target serialized object.</param>
        /// <param name="field">Field name.</param>
        /// <param name="value">Value to write. Nothing happens when it is empty.</param>
        public static void SetStringIf(SerializedObject so, string field, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            SetString(so, field, value);
        }

        // --- numeric / bool (value overloads) ---

        /// <summary>Writes a value into an integer field. Ignored when the field does not exist.</summary>
        /// <param name="so">Target serialized object.</param>
        /// <param name="field">Field name.</param>
        /// <param name="value">Value to write.</param>
        public static void SetInt(SerializedObject so, string field, int value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.intValue = value;
        }

        /// <summary>Writes a value into a float field. Ignored when the field does not exist.</summary>
        /// <param name="so">Target serialized object.</param>
        /// <param name="field">Field name.</param>
        /// <param name="value">Value to write.</param>
        public static void SetFloat(SerializedObject so, string field, float value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.floatValue = value;
        }

        /// <summary>Writes a value into a boolean field. Ignored when the field does not exist.</summary>
        /// <param name="so">Target serialized object.</param>
        /// <param name="field">Field name.</param>
        /// <param name="value">Value to write.</param>
        public static void SetBool(SerializedObject so, string field, bool value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.boolValue = value;
        }

        // --- numeric / bool (raw-string, skip-if-empty overloads) ---

        /// <summary>Parses raw CSV text as an integer and writes it. Preserves the existing value on an empty cell or a parse failure.</summary>
        /// <param name="so">Target serialized object.</param>
        /// <param name="field">Field name.</param>
        /// <param name="raw">Raw CSV cell text. Parsed locale-independently (InvariantCulture).</param>
        public static void SetIntIf(SerializedObject so, string field, string raw)
        {
            if (string.IsNullOrEmpty(raw)) return;
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) SetInt(so, field, v);
        }

        /// <summary>Parses raw CSV text as a float and writes it. Preserves the existing value on an empty cell or a parse failure.</summary>
        /// <param name="so">Target serialized object.</param>
        /// <param name="field">Field name.</param>
        /// <param name="raw">Raw CSV cell text. Parsed locale-independently (InvariantCulture).</param>
        public static void SetFloatIf(SerializedObject so, string field, string raw)
        {
            if (string.IsNullOrEmpty(raw)) return;
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) SetFloat(so, field, v);
        }

        /// <summary>Reads raw CSV text as a boolean and writes it. Preserves the existing value on an empty cell.</summary>
        /// <param name="so">Target serialized object.</param>
        /// <param name="field">Field name.</param>
        /// <param name="raw">Raw CSV cell text. Only "TRUE" (case-insensitive) or "1" count as true.</param>
        public static void SetBoolIf(SerializedObject so, string field, string raw)
        {
            if (string.IsNullOrEmpty(raw)) return;
            bool v = raw.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || raw == "1";
            SetBool(so, field, v);
        }

        // --- enum ---

        /// <summary>Sets an enum field by name. Preserves the existing value when the value is empty.</summary>
        /// <param name="so">Target serialized object.</param>
        /// <param name="field">Field name.</param>
        /// <param name="name">Enum constant name. (Case-insensitive.)</param>
        public static void SetEnumIf(SerializedObject so, string field, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            SetEnumByName(so.FindProperty(field), name);
        }

        /// <summary>Writes a value into an enum property by name. Leaves a warning and preserves the existing value when the name is not found.</summary>
        /// <param name="prop">Target enum property.</param>
        /// <param name="name">Enum constant name. It matches against enumNames by index, so no gameplay enum type is referenced directly.</param>
        public static void SetEnumByName(SerializedProperty prop, string name)
        {
            if (prop == null || string.IsNullOrEmpty(name)) return;
            int idx = Array.FindIndex(prop.enumNames, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) prop.enumValueIndex = idx;
            else Debug.LogWarning($"{TAG} enum value '{name}' was not found. (allowed: {string.Join("/", prop.enumNames)})");
        }

        // --- object reference ---

        /// <summary>Binds an asset to an object reference field. Ignored when the field does not exist.</summary>
        /// <param name="so">Target serialized object.</param>
        /// <param name="field">Field name.</param>
        /// <param name="value">Asset to bind. Null clears the reference.</param>
        public static void SetObjectRef(SerializedObject so, string field, UnityEngine.Object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.objectReferenceValue = value;
        }

        // --- vector / color ---

        /// <summary>Writes each axis of a Vector3 field separately. An empty axis preserves its existing value, so partial per-axis updates are possible.</summary>
        /// <param name="so">Target serialized object.</param>
        /// <param name="field">Field name.</param>
        /// <param name="x">Raw text of the X component. Kept as is when empty.</param>
        /// <param name="y">Raw text of the Y component. Kept as is when empty.</param>
        /// <param name="z">Raw text of the Z component. Kept as is when empty.</param>
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

        /// <summary>Parses an HTML color string (#RRGGBB and the like) and writes it into a color field. An empty cell preserves the value; a parse failure leaves a warning.</summary>
        /// <param name="so">Target serialized object.</param>
        /// <param name="field">Field name.</param>
        /// <param name="raw">Raw color text. (e.g. #FFFFFF)</param>
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
                Debug.LogWarning($"{TAG} failed to parse color '{raw}' (e.g. #FFFFFF)");
            }
        }
    }
}
