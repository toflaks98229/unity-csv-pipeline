using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// Instead of leaving the sync settings as pasted, this shows <b>what state they are in right now</b>.
    /// The point is to surface the sheet ID and tab number pulled out of the address.
    /// A wrong gid quietly brings in the contents of the wrong tab, and without those values on screen there is no way to tell.
    /// </summary>
    [CustomEditor(typeof(GoogleSheetSyncSettings))]
    [CanEditMultipleObjects]
    public sealed class GoogleSheetSyncSettingsEditor : Editor
    {
        /// <summary>Draws the inspector.</summary>
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (targets.Length > 1) return;   // 여럿 선택했을 때는 상태를 단정할 수 없습니다.

            var settings = (GoogleSheetSyncSettings)target;

            EditorGUILayout.Space();
            DrawStatus(settings);

            EditorGUILayout.Space();
            DrawResolved(settings);

            EditorGUILayout.Space();
            DrawActions(settings);
        }

        /// <summary>Says in one line whether these settings can work right now.</summary>
        /// <param name="settings">The settings in question.</param>
        private static void DrawStatus(GoogleSheetSyncSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.sheetUrl))
            {
                EditorGUILayout.HelpBox(
                    "The sheet address is empty. Paste the link from the browser address bar as is.\n"
                    + "The tab you have open becomes the target.", MessageType.Info);
                return;
            }

            if (!settings.IsConfigured)
            {
                EditorGUILayout.HelpBox(
                    "No sheet ID was found in the address. It has to look like docs.google.com/spreadsheets/d/...",
                    MessageType.Warning);
                return;
            }

            if (!settings.enabled)
            {
                EditorGUILayout.HelpBox("The link is in place, but this is off so nothing is pulled.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                settings.autoPull
                    ? $"Ready to pull. Checks automatically every {settings.autoPullIntervalSeconds:0} seconds."
                    : "Ready to pull. Pull it yourself from the menu.",
                MessageType.None);
        }

        /// <summary>Shows the values pulled out of the address and the state of the target file.</summary>
        /// <param name="settings">The settings in question.</param>
        private static void DrawResolved(GoogleSheetSyncSettings settings)
        {
            EditorGUILayout.LabelField("Read from the address", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Sheet ID", settings.SpreadsheetId);
                EditorGUILayout.TextField("Tab number (gid)", settings.Gid);
            }

            EditorGUILayout.LabelField("Target table", EditorStyles.boldLabel);

            string csvPath = CsvAssetPipeline.FindCsvPath(settings.csvFileName);
            if (string.IsNullOrEmpty(settings.csvFileName))
            {
                EditorGUILayout.HelpBox("The target file name is empty.", MessageType.Warning);
            }
            else if (csvPath == null)
            {
                EditorGUILayout.HelpBox(
                    $"'{settings.csvFileName}' was not found in the project. A pull would have nowhere to go.",
                    MessageType.Warning);
            }
            else
            {
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.TextField("Path", csvPath);
            }

            EditorGUILayout.LabelField("Last sync", LastSyncLabel(settings));
        }

        /// <summary>The actions these settings make possible.</summary>
        /// <param name="settings">The settings in question.</param>
        private static void DrawActions(GoogleSheetSyncSettings settings)
        {
            using (new EditorGUI.DisabledScope(!settings.IsConfigured))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Compare This Table Only (no write)")) GoogleSheetSync.CompareOne(settings);

                // 브라우저를 여는 것은 사람이 이 버튼을 눌렀을 때뿐입니다.
                if (GUILayout.Button("Open Sheet in Browser", GUILayout.Width(180)))
                {
                    Application.OpenURL(
                        $"https://docs.google.com/spreadsheets/d/{settings.SpreadsheetId}/edit#gid={settings.Gid}");
                }
            }
        }

        /// <summary>
        /// The time of the last recorded pull. It uses the snapshot file's modification time directly.
        /// No separate state is stored, so nothing can drift.
        /// </summary>
        /// <param name="settings">The settings in question.</param>
        /// <returns>The string to display.</returns>
        private static string LastSyncLabel(GoogleSheetSyncSettings settings)
        {
            if (string.IsNullOrEmpty(settings.csvFileName)) return "—";

            string snapshot = Path.Combine(CsvPipelineSettings.Instance.SnapshotFolder, settings.csvFileName);
            if (!File.Exists(snapshot)) return "Never pulled";

            DateTime when = File.GetLastWriteTime(snapshot);
            return $"{when:yyyy-MM-dd HH:mm} ({(DateTime.Now - when).TotalHours:0.#} hours ago)";
        }
    }
}
