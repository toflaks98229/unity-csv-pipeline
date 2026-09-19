using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// Adds the pipeline settings page to the Project Settings window.
    /// When there is no settings asset, it shows which defaults the pipeline is running on and lets you create one right there.
    /// </summary>
    internal static class CsvPipelineSettingsProvider
    {
        /// <summary>Default location to create the settings asset at when there is none.</summary>
        private const string DefaultAssetPath = "Assets/CsvPipelineSettings.asset";

        /// <summary>Serialized object that edits the settings asset. It is kept only while the page is open.</summary>
        private static SerializedObject _serialized;

        /// <summary>Registers the "CSV Pipeline" entry in Project Settings.</summary>
        /// <returns>The settings page to register.</returns>
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider("Project/CSV Pipeline", SettingsScope.Project)
            {
                label = "CSV Pipeline",
                keywords = new[] { "csv", "scriptableobject", "google", "sheet", "import" },
                // 화면을 열고 닫을 때만 다시 찾습니다. 그리기 안에서 프로젝트를 뒤지면
                // 마우스를 올려 두는 것만으로 메모리가 계속 늘어납니다.
                activateHandler = (_, __) => Reset(),
                deactivateHandler = Reset,
                guiHandler = _ => DrawGui()
            };
        }

        /// <summary>Drops what is held so the next draw searches again.</summary>
        private static void Reset()
        {
            _serialized = null;
            CsvPipelineSettings.InvalidateCache();
            GoogleServiceAccount.InvalidateToken();
        }

        /// <summary>Draws the body of the settings page.</summary>
        private static void DrawGui()
        {
            EditorGUILayout.Space();

            if (!CsvPipelineSettings.ExistsInProject)
            {
                DrawMissingState();
                return;
            }

            CsvPipelineSettings settings = CsvPipelineSettings.Instance;
            if (_serialized == null || _serialized.targetObject != settings)
            {
                _serialized = new SerializedObject(settings);
            }

            _serialized.Update();

            EditorGUI.indentLevel++;
            SerializedProperty property = _serialized.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "m_Script") continue;
                EditorGUILayout.PropertyField(property, true);
            }
            EditorGUI.indentLevel--;

            if (_serialized.ApplyModifiedProperties()) AssetDatabase.SaveAssets();

            EditorGUILayout.Space();
            DrawResolvedPaths(settings);

            EditorGUILayout.Space();
            if (GUILayout.Button("Select Settings Asset", GUILayout.Width(160)))
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }
        }

        /// <summary>Draws the notice and the create button shown when there is no settings asset.</summary>
        private static void DrawMissingState()
        {
            EditorGUILayout.HelpBox(
                "There is no settings asset, so the pipeline runs on defaults.\n"
                + "If your CSV folder differs from the default, create an asset and point it at the right path.",
                MessageType.Info);

            DrawResolvedPaths(CsvPipelineSettings.Instance);

            EditorGUILayout.Space();
            if (!GUILayout.Button($"Create Settings Asset at {DefaultAssetPath}", GUILayout.Width(320))) return;

            CsvPipelineSettings created = CsvPipelineSettings.CreateAsset(DefaultAssetPath);
            _serialized = null;
            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
            GUIUtility.ExitGUI();   // 이번 프레임의 남은 그리기는 사라진 화면을 향합니다.
        }

        /// <summary>Shows the paths actually in use, read-only. (This makes the fallback for an empty field visible too.)</summary>
        /// <param name="settings">Settings to display.</param>
        private static void DrawResolvedPaths(CsvPipelineSettings settings)
        {
            EditorGUILayout.LabelField("Paths in Effect", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("CSV Root", settings.CsvRootFolder);
                EditorGUILayout.TextField("Sheet Sync Settings", settings.SheetSyncSettingsFolder);
                EditorGUILayout.TextField("Snapshot", settings.SnapshotFolder);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Private Sheet Authentication", EditorStyles.boldLabel);

            if (string.IsNullOrEmpty(settings.ServiceAccountKeyPath))
            {
                EditorGUILayout.HelpBox(
                    "No service account key. Only sheets shared as 'Anyone with the link - Viewer' are pulled.",
                    MessageType.None);
            }
            else if (GoogleServiceAccount.IsConfigured)
            {
                EditorGUILayout.HelpBox(
                    "Service account key found. Share a sheet with that account's email and you can pull it while it stays private.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"No key file at the given path: {settings.ServiceAccountKeyPath}",
                    MessageType.Warning);
            }
        }
    }
}
