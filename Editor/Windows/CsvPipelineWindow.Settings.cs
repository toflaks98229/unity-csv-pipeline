using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>The Settings tab. It shows <b>the values actually in effect right now</b> and points to where they are edited.</summary>
    public sealed partial class CsvPipelineWindow
    {
        private Vector2 _settingsScroll;

        /// <summary>Draws the Settings tab. The values themselves are edited in Project Settings.</summary>
        private void DrawSettings()
        {
            CsvPipelineSettings settings = CsvPipelineSettings.Instance;

            _settingsScroll = EditorGUILayout.BeginScrollView(_settingsScroll);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(CsvEditorUI.GapWide);

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(CsvEditorUI.GapWide);

                    DrawPaths(settings);
                    GUILayout.Space(CsvEditorUI.GapSection);

                    DrawUndoWarning();
                    GUILayout.Space(CsvEditorUI.GapWide);
                }

                GUILayout.Space(CsvEditorUI.GapWide);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>The paths currently in effect.</summary>
        /// <param name="settings">Settings to read.</param>
        private void DrawPaths(CsvPipelineSettings settings)
        {
            GUILayout.Label("Paths in effect", EditorStyles.boldLabel);
            CsvEditorUI.Divider1();

            DrawPathRow("CSV root", settings.CsvRootFolder, "Where table files are looked up.");
            DrawPathRow("Sheet sync settings", settings.SheetSyncSettingsFolder, "Where sync settings assets are placed.");
            DrawPathRow("Snapshots", settings.SnapshotFolder,
                        "A copy of what was pulled last. Used to tell whether the local file was edited by hand.");
            DrawPathRow("Service account key",
                        string.IsNullOrEmpty(settings.ServiceAccountKeyPath)
                            ? "(empty — reads public sheets only)"
                            : settings.ServiceAccountKeyPath,
                        "Set this to read private sheets.");

            GUILayout.Space(CsvEditorUI.GapWide);

            if (!CsvPipelineSettings.ExistsInProject)
            {
                EditorGUILayout.HelpBox(
                    "No settings asset, so defaults apply. Point these at your folders if your tables live elsewhere.",
                    MessageType.Info);
                GUILayout.Space(CsvEditorUI.Gap);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(CsvEditorUI.IconAnd("d_SettingsIcon", " Edit in Project Settings"),
                                     GUILayout.Height(24f), GUILayout.MinWidth(220f)))
                {
                    SettingsService.OpenProjectSettings("Project/CSV Pipeline");
                }

                GUILayout.FlexibleSpace();
            }
        }

        /// <summary>
        /// One path row. The value stays selectable but cannot be edited.
        /// Editing it here would make a second copy alongside Project Settings, and then it is unclear
        /// which one is real.
        /// </summary>
        /// <param name="label">Item name.</param>
        /// <param name="value">Current value.</param>
        /// <param name="hint">What this path is used for.</param>
        private static void DrawPathRow(string label, string value, string hint)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent(label, hint), GUILayout.Width(110));

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField(value);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(114f);
                CsvEditorUI.ColoredLabel(hint, CsvEditorUI.Muted);
            }

            GUILayout.Space(CsvEditorUI.GapTight);
        }

        /// <summary>
        /// The fact that imports cannot be undone. It is the first thing to know about this tool, so it
        /// stays open instead of being foldable.
        /// </summary>
        private static void DrawUndoWarning()
        {
            GUILayout.Label("Good to know", EditorStyles.boldLabel);
            CsvEditorUI.Divider1();

            EditorGUILayout.HelpBox(
                "Imports cannot be undone with Ctrl+Z.\n\n"
                + "A single import both edits fields and creates or deletes assets, and Unity's Undo only\n"
                + "reverts the field edits. Reverting halfway is worse: you walk away believing it was undone.\n\n"
                + "Instead, look at what changes in the 'Tables' tab first, and revert output assets with git.",
                MessageType.Warning);
        }
    }
}
