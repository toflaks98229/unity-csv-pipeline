using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>Binding between one table column and one asset field.</summary>
    public sealed class CsvBinding
    {
        /// <summary>Column name in the table.</summary>
        public string Column;

        /// <summary>Serialization path of the target field. (top-level field name)</summary>
        public string PropertyPath;

        /// <summary>Type of the target field. Used for object references and lists.</summary>
        public Type FieldType;

        /// <summary>Whether this column must be present in the table.</summary>
        public bool Required;

        /// <summary>Whether an empty cell overwrites the existing value.</summary>
        public bool OverwriteWhenEmpty;

        /// <summary>Separators that split a list cell.</summary>
        public char[] Separators;

        /// <summary>Folder that narrows the object reference search. Null means the whole project.</summary>
        public string ReferenceFolder;
    }

    /// <summary>
    /// Table-to-asset binding plan read from one type carrying <see cref="CsvAssetAttribute"/>.
    /// </summary>
    public sealed class CsvSchema
    {
        /// <summary>Asset type to bake.</summary>
        public Type AssetType { get; private set; }

        /// <summary>Declaration that was attached to the type.</summary>
        public CsvAssetAttribute Declaration { get; private set; }

        /// <summary>Column-to-field bindings. They follow field declaration order, not table order.</summary>
        public List<CsvBinding> Bindings { get; } = new List<CsvBinding>();

        /// <summary>
        /// Settles the folder the output assets go into.
        /// The declaration wins when it names one; otherwise it is <b>a folder named after the type, next to the source table</b>.
        /// </summary>
        /// <param name="csvPath">Asset path of the source table. Null looks it up by file name.</param>
        /// <returns>Output folder path, or null when the table is not found.</returns>
        public string ResolveOutputFolder(string csvPath = null)
        {
            string declared = Declaration.OutputFolder;
            if (!string.IsNullOrWhiteSpace(declared)) return declared.TrimEnd('/');

            string path = csvPath ?? CsvAssetPipeline.FindCsvPath(Declaration.FileName);
            if (string.IsNullOrEmpty(path)) return null;

            string folder = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            return string.IsNullOrEmpty(folder) ? null : $"{folder}/{AssetType.Name}";
        }

        /// <summary>Columns that must be present. The Id column is always included.</summary>
        public IEnumerable<string> RequiredColumns
        {
            get
            {
                yield return Declaration.IdColumn;
                foreach (CsvBinding binding in Bindings)
                {
                    if (binding.Required) yield return binding.Column;
                }
            }
        }

        /// <summary>
        /// Whether this declaration takes <b>only the fields that are tagged</b>.
        /// (<c>AutoMap = false</c> — a new field does not follow into the table on its own)
        /// </summary>
        public bool OptIn => !Declaration.AutoMap;

        /// <summary>
        /// Serialized fields left unbound because they carry no tag. Empty unless <see cref="OptIn"/>.
        /// <para>
        /// When you tag <b>what to include</b> instead of tagging what to leave out one by one, a field
        /// you forgot to tag drops out without a word. That silence is the only value of this mode,
        /// so we leave a way to ask what dropped out.
        /// </para>
        /// </summary>
        /// <returns>Names of the fields left out for want of a tag. Declaration order is kept.</returns>
        public List<string> UntaggedFields()
        {
            var untagged = new List<string>();
            if (!OptIn) return untagged;

            foreach (FieldInfo field in SerializableFields(AssetType))
            {
                // 빼라고 적어 둔 것은 잊은 것이 아닙니다.
                if (Attribute.IsDefined(field, typeof(CsvIgnoreAttribute))) continue;
                if (Attribute.IsDefined(field, typeof(CsvColumnAttribute))) continue;

                untagged.Add(field.Name);
            }
            return untagged;
        }

        // ====================================================================================================
        // 수집
        // ====================================================================================================

        /// <summary>Schemas found so far. Type information changes only on a domain reload, so caching is safe.</summary>
        private static List<CsvSchema> _cached;

        /// <summary>
        /// Scans every loaded assembly for types carrying <see cref="CsvAssetAttribute"/> and builds their schemas.
        /// </summary>
        /// <returns>The schemas found, ordered by file name.</returns>
        public static IReadOnlyList<CsvSchema> All()
        {
            if (_cached != null) return _cached;

            var found = new List<CsvSchema>();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // 검사 픽스처는 소비 프로젝트의 데이터가 아닙니다. 끼워 넣은 패키지의 것까지 딸려 오면
                // 남의 창에 깨진 표로 올라오고, 굽기까지 시도하게 됩니다.
                if (CsvAssemblies.IsTestAssembly(assembly)) continue;

                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }   // 일부만 로드된 어셈블리도 훑습니다.

                foreach (Type type in types)
                {
                    if (type == null || type.IsAbstract) continue;
                    if (!typeof(ScriptableObject).IsAssignableFrom(type)) continue;

                    CsvSchema schema = For(type);
                    if (schema != null) found.Add(schema);
                }
            }

            found.Sort((a, b) => string.CompareOrdinal(a.Declaration.FileName, b.Declaration.FileName));
            _cached = found;
            return _cached;
        }

        /// <summary>Drops the cache so the next lookup finds the schemas again.</summary>
        public static void InvalidateCache() => _cached = null;

        /// <summary>
        /// Builds the schema for one type. Null when <see cref="CsvAssetAttribute"/> is absent or the declaration is incomplete.
        /// </summary>
        /// <param name="type">Target ScriptableObject type.</param>
        /// <returns>The schema that was built, or null.</returns>
        public static CsvSchema For(Type type)
        {
            if (type == null) return null;

            var declaration = (CsvAssetAttribute)Attribute.GetCustomAttribute(type, typeof(CsvAssetAttribute));
            return declaration == null ? null : Build(type, declaration);
        }

        /// <summary>
        /// Builds a schema even for a type with no declaration. Use it for a type <b>before its table exists</b>.
        /// <para>
        /// When you want to export the table first, put it on a sheet, and author it there, that type usually
        /// does not carry <see cref="CsvAssetAttribute"/> yet. When a declaration is present, <b>it is used as is</b> —
        /// if the values made up here diverged from the real bake, the table a person sees and the table that bakes
        /// would differ.
        /// </para>
        /// <para>
        /// <b>Do not bake with this.</b> Baking never consults a made-up declaration,
        /// so baking with this schema produces a result different from the real import.
        /// </para>
        /// </summary>
        /// <param name="type">Target ScriptableObject type.</param>
        /// <param name="fileName">Table file name to use when there is no declaration.</param>
        /// <param name="idColumn">Id column name to use when there is no declaration.</param>
        /// <returns>The schema that was built, or null when it cannot be built.</returns>
        public static CsvSchema Draft(Type type, string fileName, string idColumn)
        {
            if (type == null) return null;

            CsvSchema declared = For(type);
            if (declared != null) return declared;

            return Build(type, new CsvAssetAttribute(fileName, idColumn));
        }

        /// <summary>
        /// Builds a schema from a type and its declaration. Warns and returns null when the declaration is incomplete.
        /// </summary>
        /// <param name="type">Target ScriptableObject type.</param>
        /// <param name="declaration">Declaration attached to the type.</param>
        /// <returns>The schema that was built, or null.</returns>
        private static CsvSchema Build(Type type, CsvAssetAttribute declaration)
        {
            if (string.IsNullOrEmpty(declaration.FileName) || string.IsNullOrEmpty(declaration.IdColumn))
            {
                Debug.LogWarning(
                    $"[CsvPipeline] The [CsvAsset] declaration on {type.Name} is empty. "
                    + "Specify both fileName and idColumn.");
                return null;
            }

            var schema = new CsvSchema { AssetType = type, Declaration = declaration };

            foreach (FieldInfo field in SerializableFields(type))
            {
                if (Attribute.IsDefined(field, typeof(CsvIgnoreAttribute))) continue;

                var column = (CsvColumnAttribute)Attribute.GetCustomAttribute(field, typeof(CsvColumnAttribute));
                if (column == null && !declaration.AutoMap) continue;

                schema.Bindings.Add(new CsvBinding
                {
                    Column = !string.IsNullOrEmpty(column?.Name) ? column.Name : field.Name,
                    PropertyPath = field.Name,
                    FieldType = field.FieldType,
                    Required = column?.Required ?? false,
                    OverwriteWhenEmpty = column?.OverwriteWhenEmpty ?? false,
                    Separators = ParseSeparators(column?.Separators),
                    ReferenceFolder = column?.ReferenceFolder
                });
            }

            return schema;
        }

        /// <summary>
        /// Walks the inheritance chain and returns the instance fields Unity serializes.
        /// (public, or carrying <c>[SerializeField]</c>; <c>[NonSerialized]</c>, static and const are excluded)
        /// </summary>
        /// <param name="type">Type to walk.</param>
        /// <returns>The serialized fields. Base type fields come first.</returns>
        private static IEnumerable<FieldInfo> SerializableFields(Type type)
        {
            var chain = new List<Type>();
            for (Type t = type; t != null && t != typeof(ScriptableObject) && t != typeof(object); t = t.BaseType)
            {
                chain.Add(t);
            }
            chain.Reverse();   // 기반 타입의 필드가 먼저 오도록

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public
                                     | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            foreach (Type t in chain)
            {
                foreach (FieldInfo field in t.GetFields(flags))
                {
                    if (field.IsStatic || field.IsLiteral || field.IsInitOnly) continue;
                    if (Attribute.IsDefined(field, typeof(NonSerializedAttribute))) continue;
                    if (!field.IsPublic && !Attribute.IsDefined(field, typeof(SerializeField))) continue;

                    yield return field;
                }
            }
        }

        /// <summary>Turns a separator string into an array. Empty means the default separators.</summary>
        /// <param name="separators">String concatenating the separator characters.</param>
        /// <returns>The separator array to use.</returns>
        private static char[] ParseSeparators(string separators)
            => string.IsNullOrEmpty(separators) ? CsvRow.ListSeparators : separators.ToCharArray();
    }
}
