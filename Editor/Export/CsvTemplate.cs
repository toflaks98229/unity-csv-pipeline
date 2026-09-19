using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>One field left out of the columns because a table cannot author it.</summary>
    public sealed class CsvTemplateOmission
    {
        /// <summary>Records one left-out field.</summary>
        /// <param name="field">Field name.</param>
        /// <param name="reason">Reason it was left out.</param>
        public CsvTemplateOmission(string field, string reason)
        {
            Field = field;
            Reason = reason;
        }

        /// <summary>Name of the left-out field.</summary>
        public string Field { get; }

        /// <summary>Reason it was left out.</summary>
        public string Reason { get; }
    }

    /// <summary>
    /// A table draft built from one type.
    /// <b>It writes nothing</b> — turning it into a file is up to the caller.
    /// </summary>
    public sealed class CsvTemplateDraft
    {
        /// <summary>Type the table was built from.</summary>
        public Type AssetType { get; internal set; }

        /// <summary>Table text. Null when it could not be built.</summary>
        public string Text { get; internal set; }

        /// <summary>Reason the draft could not be built. Null when it was.</summary>
        public string Unsupported { get; internal set; }

        /// <summary>Column names. The identifier column comes first.</summary>
        public List<string> Headers { get; } = new List<string>();

        /// <summary>
        /// Fields left out because a table cannot author them.
        /// <b>Not reporting these amounts to handing out columns that can never be read back.</b>
        /// </summary>
        public List<CsvTemplateOmission> Omitted { get; } = new List<CsvTemplateOmission>();

        /// <summary>Number of rows filled in.</summary>
        public int RowCount { get; internal set; }

        /// <summary>Name of the identifier column.</summary>
        public string IdColumn { get; internal set; }

        /// <summary>Whether the type already carried a <c>[CsvAsset]</c> declaration.</summary>
        public bool Declared { get; internal set; }

        /// <summary>
        /// Whether the declaration takes <b>only tagged fields</b>. (<c>AutoMap = false</c>)
        /// </summary>
        public bool OptIn { get; internal set; }

        /// <summary>
        /// Fields a table could author but that fell out for lack of <c>[CsvColumn]</c>. Filled only when <see cref="OptIn"/>.
        /// <para>
        /// This means something different from <see cref="Omitted"/> — those were left out because they
        /// <b>cannot be written</b>, these because they <b>can be written but nobody asked for them</b>.
        /// Wording both the same way reads what you can fix and what you cannot as one lump.
        /// </para>
        /// </summary>
        public List<string> Untagged { get; } = new List<string>();

        /// <summary>
        /// The one <c>[CsvAsset]</c> line to paste onto a type that has no declaration. Null when one is already there.
        /// Build the table and forget the declaration, and that table <b>bakes nothing.</b>
        /// </summary>
        public string DeclarationSnippet { get; internal set; }
    }

    /// <summary>
    /// Looks at one ScriptableObject type and builds <b>the table that authors that type</b>.
    /// <para>
    /// This is for building the table first, putting it on a sheet, and authoring there, so it also takes
    /// types that do not carry <c>[CsvAsset]</c> yet. When one is there, it follows that declaration
    /// (column names and identifier column) exactly.
    /// </para>
    /// <para>
    /// <b>It only builds columns that can be read back.</b> Pull fields <see cref="CsvValueBinder"/> cannot
    /// handle into columns and, the moment someone fills them on the sheet and pulls it back, warnings pour
    /// out row after row. Not handing out the column beats handing it out and failing later.
    /// </para>
    /// </summary>
    public static class CsvTemplate
    {
        /// <summary>Identifier column name used for types with no declaration.</summary>
        public const string DefaultIdColumn = "Id";

        /// <summary>Null when a table can be built from this type, otherwise the reason.</summary>
        /// <param name="type">Type to check.</param>
        /// <returns>The rejection reason, or null when it can be built.</returns>
        public static string Reject(Type type)
        {
            if (type == null) return "Could not find the type.";
            if (!typeof(ScriptableObject).IsAssignableFrom(type)) return $"{type.Name} is not a ScriptableObject.";
            if (type.IsAbstract) return $"{type.Name} is abstract, so it cannot be created.";
            if (type.IsGenericTypeDefinition) return $"{type.Name} is a generic definition, so it cannot be created.";

            return null;
        }

        /// <summary>
        /// Builds a table draft from one type.
        /// </summary>
        /// <param name="type">ScriptableObject type the table authors.</param>
        /// <param name="delimiter">Field delimiter to use. Usually decided by the extension of the file being saved.</param>
        /// <param name="includeExistingAssets">Whether to fill rows from assets that already exist.</param>
        /// <returns>The draft. When it could not be built, <see cref="CsvTemplateDraft.Unsupported"/> carries the reason.</returns>
        public static CsvTemplateDraft Build(Type type, char delimiter = CsvReader.Comma,
                                             bool includeExistingAssets = true)
        {
            var draft = new CsvTemplateDraft { AssetType = type };

            string rejected = Reject(type);
            if (rejected != null)
            {
                draft.Unsupported = rejected;
                return draft;
            }

            CsvSchema declared = CsvSchema.For(type);
            CsvSchema schema = declared ?? CsvSchema.Draft(type, $"{type.Name}.csv", DefaultIdColumn);
            if (schema == null)
            {
                draft.Unsupported = $"The [CsvAsset] declaration on {type.Name} is empty.";
                return draft;
            }

            draft.Declared = declared != null;
            draft.OptIn = schema.OptIn;
            draft.IdColumn = schema.Declaration.IdColumn;

            List<CsvBinding> columns = SelectColumns(schema, draft);
            if (columns == null)
            {
                draft.Unsupported = $"Could not inspect the fields of {type.Name}.";
                return draft;
            }

            if (columns.Count == 0)
            {
                // 왜 비었는지를 갈라 말합니다. 표시를 잊은 것과 애초에 쓸 수 없는 것은 할 일이 다릅니다.
                if (draft.OptIn)
                {
                    draft.Unsupported =
                        $"{type.Name} is declared to take only fields tagged with [CsvColumn] (AutoMap = false), "
                        + "and not one field is tagged. Tag the fields the table should author with [CsvColumn]."
                        + (draft.Untagged.Count > 0
                            ? $"\n\nFields you can tag: {string.Join(", ", draft.Untagged)}"
                            : string.Empty);
                    return draft;
                }

                if (draft.Omitted.Count > 0)
                {
                    draft.Unsupported = "Not one field here can be authored by a table.";
                    return draft;
                }
            }

            BuildHeaders(schema, columns, draft);

            var writer = new CsvWriter(delimiter);
            writer.WriteRow(draft.Headers);

            if (includeExistingAssets) WriteExistingAssets(schema, columns, writer, draft);

            draft.Text = writer.ToString();
            draft.DeclarationSnippet = draft.Declared ? null : Snippet(schema, type);
            return draft;
        }

        // ====================================================================================================
        // 열 고르기
        // ====================================================================================================

        /// <summary>
        /// Picks only the bindings that can be read back, and records what it leaves out, with the reason, in the draft.
        /// <para>
        /// The judgement <b>creates one real instance and looks through Unity's serialization eyes</b>. Reflection
        /// over the type alone goes wrong on nested structs, list elements, and things like
        /// <c>[SerializeReference]</c>, because baking itself goes through <see cref="SerializedProperty"/>.
        /// </para>
        /// </summary>
        /// <param name="schema">Schema holding the column bindings.</param>
        /// <param name="draft">Draft to record the left-out fields in.</param>
        /// <returns>The bindings to use, or null when the instance could not be created.</returns>
        private static List<CsvBinding> SelectColumns(CsvSchema schema, CsvTemplateDraft draft)
        {
            ScriptableObject probe;
            try { probe = ScriptableObject.CreateInstance(schema.AssetType); }
            catch (Exception) { return null; }

            if (probe == null) return null;

            var chosen = new List<CsvBinding>();
            try
            {
                var serialized = new SerializedObject(probe);

                foreach (CsvBinding binding in schema.Bindings)
                {
                    // 식별자 열은 에셋 이름이 채웁니다. 같은 이름의 필드가 있어도 열은 하나입니다.
                    if (string.Equals(binding.Column, schema.Declaration.IdColumn, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    SerializedProperty property = serialized.FindProperty(binding.PropertyPath);
                    if (property == null)
                    {
                        draft.Omitted.Add(new CsvTemplateOmission(binding.PropertyPath, "Field is not serialized."));
                        continue;
                    }

                    string reason = Unauthorable(property);
                    if (reason != null)
                    {
                        draft.Omitted.Add(new CsvTemplateOmission(binding.PropertyPath, reason));
                        continue;
                    }

                    chosen.Add(binding);
                }

                CollectUntagged(schema, serialized, draft);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }

            return chosen;
        }

        /// <summary>
        /// Collects the fields that fell out for lack of a tag but <b>would work with the tag alone added</b>.
        /// <para>
        /// When only the fields you opt in get tagged, a field whose tag was forgotten drops out without a word.
        /// That silence is both why this approach is safe and its one danger, so <b>it does say what dropped out.</b>
        /// What a table could never author in the first place is not worth suggesting, so it does not go in here.
        /// </para>
        /// </summary>
        /// <param name="schema">Target schema.</param>
        /// <param name="serialized">Serialized object of the instance being inspected.</param>
        /// <param name="draft">Draft to record the result in.</param>
        private static void CollectUntagged(CsvSchema schema, SerializedObject serialized, CsvTemplateDraft draft)
        {
            foreach (string field in schema.UntaggedFields())
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property == null) continue;
                if (Unauthorable(property) != null) continue;

                draft.Untagged.Add(field);
            }
        }

        /// <summary>
        /// The reason a table cannot author this property. Null when it can.
        /// </summary>
        /// <param name="property">Property to inspect.</param>
        /// <returns>The rejection reason, or null when it can be authored.</returns>
        internal static string Unauthorable(SerializedProperty property)
        {
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                SerializedPropertyType element = ElementTypeOf(property);
                return CsvValueBinder.CanAuthor(element)
                    ? null
                    : $"A table cannot author a list of {element}.";
            }

            return CsvValueBinder.CanAuthor(property.propertyType)
                ? null
                : $"A table cannot author the {property.propertyType} type.";
        }

        /// <summary>
        /// Element kind of an array property.
        /// When it is empty there is no element to look at, so this grows it by one slot and takes that back.
        /// (The instance being inspected is thrown away anyway, but leaving nothing behind lets other callers use this too.)
        /// </summary>
        /// <param name="property">Array property.</param>
        /// <returns>Kind of the element.</returns>
        private static SerializedPropertyType ElementTypeOf(SerializedProperty property)
        {
            int original = property.arraySize;
            if (original == 0) property.arraySize = 1;

            SerializedPropertyType element = property.GetArrayElementAtIndex(0).propertyType;

            property.arraySize = original;
            return element;
        }

        // ====================================================================================================
        // 표 만들기
        // ====================================================================================================

        /// <summary>
        /// Puts the identifier column first and appends the chosen columns after it.
        /// <para>
        /// When the table <b>already exists, it keeps the spelling written there</b>. Automatic binding takes the
        /// field name as the column name, so without this <c>MaxSpeed</c> turns into <c>maxSpeed</c> on every rebuild.
        /// The contents match, but the headers no longer match the sheet, and sync stops to ask about overwriting.
        /// </para>
        /// </summary>
        /// <param name="schema">Target schema.</param>
        /// <param name="columns">Bindings to use.</param>
        /// <param name="draft">Draft to hold the headers.</param>
        private static void BuildHeaders(CsvSchema schema, List<CsvBinding> columns, CsvTemplateDraft draft)
        {
            // 헤더만 있는 표에서도 표기를 가져와야 합니다 — 지난번에 뽑아 둔 빈 초안을 다시 뽑는
            // 경우가 바로 그것이라, 행이 없다고 null을 돌려주는 ReadTable을 쓰지 않습니다.
            string sourcePath = CsvAssetPipeline.FindCsvPath(schema.Declaration.FileName);
            string sourceText = sourcePath == null ? null : CsvImportUtil.ReadText(sourcePath);

            CsvTable source = string.IsNullOrEmpty(sourceText)
                ? null
                : CsvReader.ReadTable(sourceText, CsvReader.DelimiterForPath(sourcePath));

            draft.Headers.Add(source?.ResolveHeader(schema.Declaration.IdColumn) ?? schema.Declaration.IdColumn);

            foreach (CsvBinding binding in columns)
            {
                draft.Headers.Add(source?.ResolveHeader(binding.Column) ?? binding.Column);
            }
        }

        /// <summary>
        /// Fills rows from assets that already exist. When there are none, only the header remains.
        /// </summary>
        /// <param name="schema">Target schema.</param>
        /// <param name="columns">Bindings to use.</param>
        /// <param name="writer">Writer to write the rows with.</param>
        /// <param name="draft">Draft to record the row count in.</param>
        private static void WriteExistingAssets(CsvSchema schema, List<CsvBinding> columns,
                                                CsvWriter writer, CsvTemplateDraft draft)
        {
            ICsvAssetGateway assets = CsvAssets.Current;

            // 경로순으로 써야 같은 프로젝트에서 늘 같은 파일이 나옵니다.
            var paths = new List<string>(assets.FindPaths($"t:{schema.AssetType.Name}", SearchFolder(schema)));
            paths.Sort(StringComparer.Ordinal);

            var cells = new List<string>(draft.Headers.Count);
            foreach (string path in paths)
            {
                if (!(assets.Load(path, schema.AssetType) is ScriptableObject asset)) continue;

                var serialized = new SerializedObject(asset);
                cells.Clear();
                cells.Add(Path.GetFileNameWithoutExtension(path));   // 식별자 열 = 에셋 이름

                foreach (CsvBinding binding in columns)
                {
                    cells.Add(CsvValueFormatter.Format(serialized.FindProperty(binding.PropertyPath),
                                                       binding.Separators));
                }

                writer.WriteRow(cells);
                draft.RowCount++;
            }
        }

        /// <summary>
        /// Folder to look for existing assets in.
        /// It looks at the output folder the declaration points to first, and when no such folder exists it looks at
        /// the whole project — a type that has no table yet usually has no output folder either.
        /// </summary>
        /// <param name="schema">Target schema.</param>
        /// <returns>The folder to search, or null when searching everything.</returns>
        private static string SearchFolder(CsvSchema schema)
        {
            string folder = schema.ResolveOutputFolder();
            return !string.IsNullOrEmpty(folder) && CsvAssets.Current.FolderExists(folder) ? folder : null;
        }

        /// <summary>The one <c>[CsvAsset]</c> line to paste onto a type that has no declaration.</summary>
        /// <param name="schema">Schema the draft was built from.</param>
        /// <param name="type">Target type.</param>
        /// <returns>The line to paste.</returns>
        private static string Snippet(CsvSchema schema, Type type)
            => $"[CsvAsset(\"{schema.Declaration.FileName}\", \"{schema.Declaration.IdColumn}\", "
             + $"OutputFolder = \"Assets/Data/{type.Name}\")]";
    }
}
