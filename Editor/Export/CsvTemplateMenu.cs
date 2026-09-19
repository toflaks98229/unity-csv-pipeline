using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// Looks at the selected ScriptableObject (asset or script) and writes <b>the table that authors that type</b> to a file.
    /// This is where you start authoring by putting the table on a sheet, so it can be saved anywhere, inside the project or out.
    /// </summary>
    public static class CsvTemplateMenu
    {
        /// <summary>Log prefix tag.</summary>
        private const string TAG = "[CsvTemplate]";

        /// <summary>
        /// Title of the save dialog. It says here that <b>the extension is where you choose the format</b>.
        /// This package decides the delimiter from the extension, so a separate format picker would put the two places at odds.
        /// </summary>
        private const string SaveTitle = "Save as Table — the extension decides the format (.csv comma, .tsv tab)";

        // ====================================================================================================
        // 진입점
        // ====================================================================================================

        /// <summary>Creates a table from the selection.</summary>
        [MenuItem("Assets/CSV Pipeline/Create Table for This Type", false, 30)]
        private static void CreateFromProject() => Create(TypeOf(Selection.activeObject));

        /// <summary>Enables the menu item only when the selection is something a table can be built from.</summary>
        /// <returns>True when it may be enabled.</returns>
        [MenuItem("Assets/CSV Pipeline/Create Table for This Type", true)]
        private static bool CanCreateFromProject() => CsvTemplate.Reject(TypeOf(Selection.activeObject)) == null;

        /// <summary>Creates a table from the selection. When nothing is selected, it says what to select.</summary>
        [MenuItem("Tools/CSV Pipeline/Create Table from ScriptableObject", false, 23)]
        private static void CreateFromMenu()
        {
            Type type = TypeOf(Selection.activeObject);
            string rejected = CsvTemplate.Reject(type);

            if (rejected != null)
            {
                EditorUtility.DisplayDialog(
                    "Nothing selected",
                    $"{rejected}\n\nSelect a ScriptableObject asset or its script (.cs) in the Project window and run this again.",
                    "OK");
                return;
            }

            Create(type);
        }

        /// <summary>
        /// The type the selected object points at. For an asset, its type; for a script, the class inside it.
        /// </summary>
        /// <param name="selected">Selected object.</param>
        /// <returns>The type found, or null.</returns>
        private static Type TypeOf(UnityEngine.Object selected)
        {
            switch (selected)
            {
                case null: return null;
                case MonoScript script: return script.GetClass();
                case ScriptableObject asset: return asset.GetType();
                default: return null;
            }
        }

        // ====================================================================================================
        // 만들기
        // ====================================================================================================

        /// <summary>Builds a table from one type and saves it.</summary>
        /// <param name="type">Type the table authors.</param>
        private static void Create(Type type)
        {
            string rejected = CsvTemplate.Reject(type);
            if (rejected != null)
            {
                EditorUtility.DisplayDialog("Cannot build a table", rejected, "OK");
                return;
            }

            string path = EditorUtility.SaveFilePanel(SaveTitle, DefaultFolder(), DefaultName(type), "csv");
            if (string.IsNullOrEmpty(path)) return;

            // 구분자는 사람이 고른 확장자가 정합니다. 파이프라인이 표를 읽을 때와 같은 규칙입니다.
            CsvTemplateDraft draft = CsvTemplate.Build(type, CsvReader.DelimiterForPath(path));

            if (draft.Unsupported != null)
            {
                EditorUtility.DisplayDialog("Cannot build a table",
                                            $"{type.Name}\n\n{draft.Unsupported}", "OK");
                return;
            }

            try
            {
                // 파이프라인의 리더가 기대하는 형식입니다. (BOM 없는 UTF-8, 줄 끝은 LF)
                File.WriteAllText(path, draft.Text, CsvPipelineSettings.TableEncoding);
            }
            catch (Exception e)
            {
                Debug.LogError($"{TAG} Could not write the table: {path}\n{e.GetType().Name} — {e.Message}");
                EditorUtility.DisplayDialog("Write failed",
                                            $"{path}\n\n{e.Message}", "OK");
                return;
            }

            RefreshIfInsideProject(path);
            Report(type, path, draft);
        }

        /// <summary>
        /// Imports the file so it shows up, when it was saved inside the project.
        /// <b>Automatic baking stops meanwhile</b> — the table was just built from those assets, so a rebake gives the same result.
        /// </summary>
        /// <param name="fullPath">Absolute path it was saved to.</param>
        private static void RefreshIfInsideProject(string fullPath)
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            if (root == null) return;

            string normalized = fullPath.Replace('\\', '/');
            if (!normalized.StartsWith(root.Replace('\\', '/') + "/", StringComparison.OrdinalIgnoreCase)) return;

            using (CsvImport.Suppress())
            {
                AssetDatabase.Refresh();
            }
        }

        /// <summary>Folder the save dialog opens at first. When a CSV root exists, that is the place.</summary>
        /// <returns>Absolute path of the folder.</returns>
        private static string DefaultFolder()
        {
            string root = CsvPipelineSettings.Instance.CsvRootFolder;
            return AssetDatabase.IsValidFolder(root) ? Path.GetFullPath(root) : Path.GetFullPath(".");
        }

        /// <summary>
        /// Name the save dialog prefills.
        /// When a declaration is there, it has to be <b>the name that declaration looks for</b>. Build it under
        /// another name and baking never even looks at that table.
        /// </summary>
        /// <param name="type">Target type.</param>
        /// <returns>File name.</returns>
        private static string DefaultName(Type type)
        {
            CsvSchema declared = CsvSchema.For(type);
            return declared != null ? declared.Declaration.FileName : $"{type.Name}.csv";
        }

        // ====================================================================================================
        // 알리기
        // ====================================================================================================

        /// <summary>Reports the result through the console and a dialog.</summary>
        /// <param name="type">Target type.</param>
        /// <param name="path">Path it was saved to.</param>
        /// <param name="draft">Draft that was built.</param>
        private static void Report(Type type, string path, CsvTemplateDraft draft)
        {
            string summary = Describe(type, path, draft);

            // 대화상자는 닫히면 사라집니다. 뺀 필드와 붙여 넣을 줄은 나중에 다시 봐야 하므로 콘솔에도 남깁니다.
            Debug.Log($"{TAG} {summary}"
                    + (draft.DeclarationSnippet != null ? $"\n\n{draft.DeclarationSnippet}" : string.Empty));

            if (draft.DeclarationSnippet == null)
            {
                if (!EditorUtility.DisplayDialog("Table created", summary, "OK", "Open Folder"))
                {
                    EditorUtility.RevealInFinder(path);
                }
                return;
            }

            int choice = EditorUtility.DisplayDialogComplex(
                "Table created",
                $"{summary}\n\n"
                + $"{type.Name} has no [CsvAsset] declaration yet. Paste this line onto the class to connect it to baking.\n\n"
                + draft.DeclarationSnippet,
                "OK", "Open Folder", "Copy [CsvAsset] Line");

            if (choice == 1) EditorUtility.RevealInFinder(path);
            else if (choice == 2) EditorGUIUtility.systemCopyBuffer = draft.DeclarationSnippet;
        }

        /// <summary>Result summary for a person to read.</summary>
        /// <param name="type">Target type.</param>
        /// <param name="path">Path it was saved to.</param>
        /// <param name="draft">Draft that was built.</param>
        /// <returns>The summary string.</returns>
        private static string Describe(Type type, string path, CsvTemplateDraft draft)
        {
            var text = new StringBuilder();

            text.Append(type.Name).Append(" → ").Append(Path.GetFileName(path)).AppendLine();
            text.Append($"{draft.Headers.Count} columns · ");
            text.Append(draft.RowCount > 0
                ? $"Filled rows from {draft.RowCount} existing assets."
                : "No assets, so only the header was built.");

            // 뺀 것을 말하지 않으면, 사람은 그 필드가 표에 있는 줄 알고 시트에 열을 만들어 채웁니다.
            if (draft.Omitted.Count > 0)
            {
                text.AppendLine().AppendLine();
                text.Append($"{draft.Omitted.Count} fields left out because a table cannot author them:");
                foreach (CsvTemplateOmission omission in draft.Omitted)
                {
                    text.AppendLine().Append("  ").Append(omission.Field).Append(" — ").Append(omission.Reason);
                }
            }

            // 위와 뜻이 다릅니다 — 이쪽은 표시만 붙이면 되는, 고칠 수 있는 목록입니다.
            if (draft.Untagged.Count > 0)
            {
                text.AppendLine().AppendLine();
                text.Append($"{draft.Untagged.Count} fields dropped for lack of [CsvColumn] (tag them and they become columns):");
                text.AppendLine().Append("  ").Append(string.Join(", ", draft.Untagged));
            }

            return text.ToString();
        }
    }
}
