using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// 표·시트 연동·설정을 한 화면에서 다루는 창입니다.
    /// 메뉴 항목으로 흩어 놓으면 지금 무엇이 어떤 상태인지 한눈에 볼 자리가 없습니다.
    /// <b>이 창은 사람이 버튼을 누를 때만 씁니다.</b> 열어 두는 것만으로는 아무것도 바뀌지 않습니다.
    /// </summary>
    public sealed partial class CsvPipelineWindow : EditorWindow
    {
        /// <summary>화면을 가르는 갈래입니다.</summary>
        private enum Tab
        {
            /// <summary>표와 그 산출물입니다.</summary>
            Tables,

            /// <summary>구글 시트 연동입니다.</summary>
            Sheets,

            /// <summary>폴더 위치 등 설정입니다.</summary>
            Settings
        }

        private static readonly string[] TabLabels = { "표", "시트 연동", "설정" };

        /// <summary>
        /// 창 상태를 담아 두는 열쇠의 앞자리입니다.
        /// 스크립트를 고칠 때마다 도메인이 다시 실리는데, 보통 필드로 두면 그때 갈래도 검색어도
        /// 펼친 것도 전부 초기값으로 돌아갑니다. 유니티 자신의 도구들이 쓰는 <see cref="SessionState"/>에
        /// 실어 그 사이를 넘깁니다.
        /// </summary>
        private const string StateKey = "CsvPipeline.Window.";

        /// <summary>지금 보고 있는 갈래입니다.</summary>
        private Tab CurrentTab
        {
            get => (Tab)SessionState.GetInt(StateKey + "Tab", 0);
            set => SessionState.SetInt(StateKey + "Tab", (int)value);
        }

        /// <summary>창을 엽니다.</summary>
        [MenuItem("Tools/CSV Pipeline/CSV 파이프라인 창", false, 0)]
        public static void Open()
        {
            var window = GetWindow<CsvPipelineWindow>();
            window.titleContent = CsvEditorUI.IconAnd("d_ScriptableObject Icon", "CSV Pipeline");
            window.minSize = new Vector2(560, 360);
            window.Invalidate();
        }

        /// <summary>창이 열릴 때 다시 훑도록 표시합니다.</summary>
        private void OnEnable() => Invalidate();

        /// <summary>들고 있던 목록을 버려 다음 그리기에서 다시 모으게 합니다.</summary>
        private void Invalidate()
        {
            _scanned = false;
            _sheets = null;
        }

        /// <summary>창을 그립니다.</summary>
        private void OnGUI()
        {
            DrawTabs();

            switch (CurrentTab)
            {
                case Tab.Tables: DrawTables(); break;
                case Tab.Sheets: DrawSheets(); break;
                default: DrawSettings(); break;
            }

            DrawStatusBar();
        }

        /// <summary>갈래를 고르는 줄입니다.</summary>
        private void DrawTabs()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                Tab current = CurrentTab;

                for (int i = 0; i < TabLabels.Length; i++)
                {
                    bool on = (int)current == i;
                    if (GUILayout.Toggle(on, TabLabels[i], EditorStyles.toolbarButton, GUILayout.Width(84)) && !on)
                    {
                        CurrentTab = (Tab)i;
                        GUI.FocusControl(null);
                    }
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(CsvEditorUI.IconOr("_Help", "?", "문서 열기"),
                                     EditorStyles.toolbarButton, GUILayout.Width(28)))
                {
                    Application.OpenURL("https://github.com/toflaks98229/unity-csv-pipeline#readme");
                }
            }
        }

        /// <summary>
        /// 창 맨 아래 한 줄입니다. 지금 상태와 이 창의 성격을 늘 같은 자리에서 알립니다.
        /// 도구 줄에 두면 단추와 섞여 읽히지 않고, 갈래마다 흩어 놓으면 볼 때마다 자리가 달라집니다.
        /// </summary>
        private void DrawStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(StatusText(), EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                CsvEditorUI.ColoredLabel("누르기 전까지 아무것도 쓰지 않습니다", CsvEditorUI.Muted);
            }
        }

        /// <summary>지금 갈래에 맞는 상태 한 줄입니다.</summary>
        /// <returns>상태 문자열입니다.</returns>
        private string StatusText()
        {
            switch (CurrentTab)
            {
                case Tab.Tables: return TablesStatus();
                case Tab.Sheets: return SheetsStatus();
                default: return CsvPipelineSettings.ExistsInProject ? "설정 에셋 사용 중" : "설정 에셋 없음 — 기본값";
            }
        }
    }
}
