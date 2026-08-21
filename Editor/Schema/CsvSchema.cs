using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>표의 열 하나와 에셋 필드 하나의 연결입니다.</summary>
    public sealed class CsvBinding
    {
        /// <summary>표의 열 이름입니다.</summary>
        public string Column;

        /// <summary>대상 필드의 직렬화 경로입니다. (최상위 필드 이름)</summary>
        public string PropertyPath;

        /// <summary>대상 필드의 타입입니다. 오브젝트 참조·리스트 처리에 씁니다.</summary>
        public Type FieldType;

        /// <summary>이 열이 표에 반드시 있어야 하는지 여부입니다.</summary>
        public bool Required;

        /// <summary>셀이 비었을 때 기존 값을 덮어쓸지 여부입니다.</summary>
        public bool OverwriteWhenEmpty;

        /// <summary>리스트 셀을 나눌 구분자들입니다.</summary>
        public char[] Separators;

        /// <summary>오브젝트 참조 검색을 한정할 폴더입니다. null이면 프로젝트 전체입니다.</summary>
        public string ReferenceFolder;
    }

    /// <summary>
    /// <see cref="CsvAssetAttribute"/>가 붙은 타입 하나에서 읽어 낸 표 ↔ 에셋 연결 계획입니다.
    /// </summary>
    public sealed class CsvSchema
    {
        /// <summary>구울 에셋 타입입니다.</summary>
        public Type AssetType { get; private set; }

        /// <summary>타입에 붙어 있던 선언입니다.</summary>
        public CsvAssetAttribute Declaration { get; private set; }

        /// <summary>열 ↔ 필드 연결 목록입니다. 표에 적힌 순서가 아니라 필드 선언 순서입니다.</summary>
        public List<CsvBinding> Bindings { get; } = new List<CsvBinding>();

        /// <summary>
        /// 산출물이 놓일 폴더를 정합니다.
        /// 선언에 적혀 있으면 그것을 쓰고, 비어 있으면 <b>원본 표 옆의 타입 이름 폴더</b>입니다.
        /// </summary>
        /// <param name="csvPath">원본 표의 에셋 경로입니다. null이면 파일 이름으로 찾습니다.</param>
        /// <returns>산출물 폴더 경로이거나, 표를 찾지 못했으면 null입니다.</returns>
        public string ResolveOutputFolder(string csvPath = null)
        {
            string declared = Declaration.OutputFolder;
            if (!string.IsNullOrWhiteSpace(declared)) return declared.TrimEnd('/');

            string path = csvPath ?? CsvAssetPipeline.FindCsvPath(Declaration.FileName);
            if (string.IsNullOrEmpty(path)) return null;

            string folder = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            return string.IsNullOrEmpty(folder) ? null : $"{folder}/{AssetType.Name}";
        }

        /// <summary>반드시 있어야 하는 열들입니다. 식별자 열은 항상 포함합니다.</summary>
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

        // ====================================================================================================
        // 수집
        // ====================================================================================================

        /// <summary>찾아 둔 스키마입니다. 타입 정보는 도메인 리로드로만 바뀌므로 캐시해도 안전합니다.</summary>
        private static List<CsvSchema> _cached;

        /// <summary>
        /// 로드된 어셈블리 전체에서 <see cref="CsvAssetAttribute"/>가 붙은 타입을 찾아 스키마를 만듭니다.
        /// </summary>
        /// <returns>찾은 스키마들입니다. 파일 이름 순입니다.</returns>
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

        /// <summary>다음 조회에서 스키마를 다시 찾도록 캐시를 버립니다.</summary>
        public static void InvalidateCache() => _cached = null;

        /// <summary>
        /// 타입 하나의 스키마를 만듭니다. <see cref="CsvAssetAttribute"/>가 없거나 선언이 불완전하면 null입니다.
        /// </summary>
        /// <param name="type">대상 ScriptableObject 타입입니다.</param>
        /// <returns>만들어진 스키마이거나 null입니다.</returns>
        public static CsvSchema For(Type type)
        {
            if (type == null) return null;

            var declaration = (CsvAssetAttribute)Attribute.GetCustomAttribute(type, typeof(CsvAssetAttribute));
            return declaration == null ? null : Build(type, declaration);
        }

        /// <summary>
        /// 타입과 선언에서 스키마를 만듭니다. 선언이 불완전하면 경고 후 null입니다.
        /// </summary>
        /// <param name="type">대상 ScriptableObject 타입입니다.</param>
        /// <param name="declaration">타입에 붙은 선언입니다.</param>
        /// <returns>만들어진 스키마이거나 null입니다.</returns>
        private static CsvSchema Build(Type type, CsvAssetAttribute declaration)
        {
            if (string.IsNullOrEmpty(declaration.FileName) || string.IsNullOrEmpty(declaration.IdColumn))
            {
                Debug.LogWarning(
                    $"[CsvPipeline] {type.Name}의 [CsvAsset] 선언이 비어 있습니다. "
                    + "fileName과 idColumn을 모두 지정하십시오.");
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
        /// Unity가 직렬화하는 인스턴스 필드를 상속 계층까지 훑어 돌려줍니다.
        /// (public 이거나 <c>[SerializeField]</c>가 붙은 것, <c>[NonSerialized]</c>·static·const 제외)
        /// </summary>
        /// <param name="type">훑을 타입입니다.</param>
        /// <returns>직렬화 대상 필드들입니다. 기반 타입이 먼저입니다.</returns>
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

        /// <summary>구분자 문자열을 배열로 바꿉니다. 비어 있으면 기본 구분자입니다.</summary>
        /// <param name="separators">구분자 문자들을 이어 붙인 문자열입니다.</param>
        /// <returns>쓸 구분자 배열입니다.</returns>
        private static char[] ParseSeparators(string separators)
            => string.IsNullOrEmpty(separators) ? CsvRow.ListSeparators : separators.ToCharArray();
    }
}
