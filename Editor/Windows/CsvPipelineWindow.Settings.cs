using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>설정 갈래입니다. <b>지금 실제로 적용되는 값</b>을 보여 주고, 고치는 곳으로 안내합니다.</summary>
    public sealed partial class CsvPipelineWindow
    {
        private Vector2 _settingsScroll;

        /// <summary>설정 갈래를 그립니다. 값은 Project Settings 에서 고칩니다.</summary>
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

        /// <summary>지금 적용 중인 경로들입니다.</summary>
        /// <param name="settings">읽을 설정입니다.</param>
        private void DrawPaths(CsvPipelineSettings settings)
        {
            GUILayout.Label("실제 적용되는 경로", EditorStyles.boldLabel);
            CsvEditorUI.Divider1();

            DrawPathRow("CSV 루트", settings.CsvRootFolder, "표 파일을 찾는 곳입니다.");
            DrawPathRow("시트 연동 설정", settings.SheetSyncSettingsFolder, "연동 설정 에셋이 놓이는 곳입니다.");
            DrawPathRow("스냅숏", settings.SnapshotFolder,
                        "마지막으로 받은 내용의 사본입니다. 로컬을 직접 고쳤는지 가리는 데 씁니다.");
            DrawPathRow("서비스 계정 키",
                        string.IsNullOrEmpty(settings.ServiceAccountKeyPath)
                            ? "(비움 — 공개 시트만 읽습니다)"
                            : settings.ServiceAccountKeyPath,
                        "비공개 시트를 읽으려면 지정합니다.");

            GUILayout.Space(CsvEditorUI.GapWide);

            if (!CsvPipelineSettings.ExistsInProject)
            {
                EditorGUILayout.HelpBox(
                    "설정 에셋이 없어 기본값으로 동작합니다. 표가 다른 폴더에 있으면 지정하십시오.",
                    MessageType.Info);
                GUILayout.Space(CsvEditorUI.Gap);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(CsvEditorUI.IconAnd("d_SettingsIcon", " Project Settings 에서 고치기"),
                                     GUILayout.Height(24f), GUILayout.MinWidth(220f)))
                {
                    SettingsService.OpenProjectSettings("Project/CSV Pipeline");
                }

                GUILayout.FlexibleSpace();
            }
        }

        /// <summary>
        /// 경로 한 줄입니다. 값은 고를 수 있게 두되 고쳐지지는 않습니다.
        /// 여기서 고치게 하면 Project Settings 와 두 벌이 되어 어느 쪽이 진짜인지 흐려집니다.
        /// </summary>
        /// <param name="label">항목 이름입니다.</param>
        /// <param name="value">지금 값입니다.</param>
        /// <param name="hint">이 경로가 무엇에 쓰이는지입니다.</param>
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
        /// 되돌리기가 안 된다는 사실입니다. 이 도구에서 가장 먼저 알아야 할 것이라 접지 않고 늘 펴 둡니다.
        /// </summary>
        private static void DrawUndoWarning()
        {
            GUILayout.Label("알아 둘 것", EditorStyles.boldLabel);
            CsvEditorUI.Divider1();

            EditorGUILayout.HelpBox(
                "임포트는 Ctrl+Z 로 되돌릴 수 없습니다.\n\n"
                + "한 번의 임포트가 필드 수정과 에셋 생성·삭제를 함께 하는데, Unity 의 Undo 는 필드 수정만\n"
                + "되돌립니다. 절반만 되돌아가면 되돌아간 줄 알고 넘어가게 되어 더 나쁩니다.\n\n"
                + "대신 '표' 갈래에서 무엇이 달라지는지 먼저 보고, 산출물은 git 으로 되돌리십시오.",
                MessageType.Warning);
        }
    }
}
