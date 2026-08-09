using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// Project Settings 창에 파이프라인 설정 화면을 붙입니다.
    /// 설정 에셋이 없으면 어떤 기본값으로 돌고 있는지 보여 주고, 그 자리에서 만들 수 있게 합니다.
    /// </summary>
    internal static class CsvPipelineSettingsProvider
    {
        /// <summary>설정 에셋이 없을 때 만들 기본 위치입니다.</summary>
        private const string DefaultAssetPath = "Assets/CsvPipelineSettings.asset";

        /// <summary>설정 에셋을 편집하는 직렬화 객체입니다. 화면이 열려 있는 동안만 유지합니다.</summary>
        private static SerializedObject _serialized;

        /// <summary>Project Settings에 "CSV Pipeline" 항목을 등록합니다.</summary>
        /// <returns>등록할 설정 화면입니다.</returns>
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider("Project/CSV Pipeline", SettingsScope.Project)
            {
                label = "CSV Pipeline",
                keywords = new[] { "csv", "scriptableobject", "google", "sheet", "import" },
                activateHandler = (_, __) => _serialized = null,
                deactivateHandler = () => _serialized = null,
                guiHandler = _ => DrawGui()
            };
        }

        /// <summary>설정 화면 본문을 그립니다.</summary>
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
            if (GUILayout.Button("설정 에셋 선택", GUILayout.Width(160)))
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }
        }

        /// <summary>설정 에셋이 없을 때의 안내와 생성 버튼을 그립니다.</summary>
        private static void DrawMissingState()
        {
            EditorGUILayout.HelpBox(
                "설정 에셋이 없어 기본값으로 동작합니다.\n"
                + "CSV 폴더가 기본값과 다르면 에셋을 만들어 경로를 지정하십시오.",
                MessageType.Info);

            DrawResolvedPaths(CsvPipelineSettings.Instance);

            EditorGUILayout.Space();
            if (!GUILayout.Button($"{DefaultAssetPath} 에 설정 에셋 만들기", GUILayout.Width(320))) return;

            CsvPipelineSettings created = CsvPipelineSettings.CreateAsset(DefaultAssetPath);
            _serialized = null;
            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
        }

        /// <summary>실제로 쓰이는 경로들을 읽기 전용으로 보여 줍니다. (빈 칸의 폴백까지 확인 가능)</summary>
        /// <param name="settings">표시할 설정입니다.</param>
        private static void DrawResolvedPaths(CsvPipelineSettings settings)
        {
            EditorGUILayout.LabelField("실제 적용되는 경로", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("CSV 루트", settings.CsvRootFolder);
                EditorGUILayout.TextField("시트 연동 설정", settings.SheetSyncSettingsFolder);
                EditorGUILayout.TextField("스냅숏", settings.SnapshotFolder);
            }
        }
    }
}
