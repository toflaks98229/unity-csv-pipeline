using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>시트 연동 갈래입니다. 설정마다 지금 어떤 상태인지를 한 줄로 보여 줍니다.</summary>
    public sealed partial class CsvPipelineWindow
    {
        /// <summary>연동 설정의 상태입니다.</summary>
        private enum SheetState { Ready, Auto, Off, NeedsUrl, BadUrl }

        /// <summary>
        /// 들고 있는 연동 설정 목록입니다.
        /// <b>매 프레임 다시 찾지 않습니다.</b> 그리기는 마우스가 움직이는 동안에도 계속 도는데,
        /// 그때마다 프로젝트를 뒤져 에셋을 전부 읽으면 창을 열어 둔 것만으로 비용이 듭니다.
        /// </summary>
        private List<GoogleSheetSyncSettings> _sheets;

        private Vector2 _sheetScroll;

        /// <summary>연동 설정 목록입니다. 없으면 이때 모읍니다.</summary>
        private List<GoogleSheetSyncSettings> Sheets => _sheets ?? (_sheets = GoogleSheetSync.FindAll());

        /// <summary>상태 바에 쓸 시트 갈래 요약입니다.</summary>
        /// <returns>요약 문자열입니다.</returns>
        private string SheetsStatus()
        {
            int ready = 0, auto = 0, needsWork = 0;

            foreach (GoogleSheetSyncSettings settings in Sheets)
            {
                switch (StateOf(settings))
                {
                    case SheetState.Auto: auto++; break;
                    case SheetState.Ready: ready++; break;
                    case SheetState.Off: break;
                    default: needsWork++; break;
                }
            }

            string text = $"설정 {Sheets.Count} · 자동 {auto} · 준비됨 {ready}";
            return needsWork > 0 ? $"{text} · 손볼 것 {needsWork}" : text;
        }

        /// <summary>시트 연동 갈래를 그립니다.</summary>
        private void DrawSheets()
        {
            DrawSheetToolbar();

            if (Sheets.Count == 0)
            {
                CsvEditorUI.EmptyState(
                    "연동 설정이 없습니다",
                    "시트에서 저작하려면 표마다 설정 에셋이 하나씩 필요합니다.\n"
                    + "'설정 만들기' 는 CSV 루트의 표를 훑어 빠진 것만 만들어 둡니다.\n"
                    + "만든 뒤 각 에셋에 브라우저 주소를 붙여넣고 Enabled 를 켜십시오.\n\n"
                    + "시트를 쓰지 않는다면 이 갈래는 비워 두어도 됩니다. 매 프레임 도는 것도 없습니다.",
                    "설정 만들기", () =>
                    {
                        GoogleSheetSync.CreateMissingSettingsMenu();
                        _sheets = null;
                    });
                return;
            }

            _sheetScroll = EditorGUILayout.BeginScrollView(_sheetScroll);

            for (int i = 0; i < Sheets.Count; i++) DrawSheetRow(Sheets[i], i);

            EditorGUILayout.EndScrollView();
        }

        /// <summary>시트 갈래의 도구 줄입니다.</summary>
        private void DrawSheetToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(CsvEditorUI.IconAnd("d_Refresh", " 다시 훑기"),
                                     EditorStyles.toolbarButton, GUILayout.Width(86)))
                {
                    _sheets = null;
                }

                CsvEditorUI.ToolbarSeparator();

                // 비교가 먼저입니다. 받기는 로컬 표를 덮어쓰므로, 무엇이 달라지는지 본 뒤가 순서입니다.
                if (GUILayout.Button("전부 비교만", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    GoogleSheetSync.CompareAllMenu();
                }

                if (GUILayout.Button("전부 받기", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    GoogleSheetSync.PullAllMenu();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(CsvEditorUI.IconOr("_Popup", "⋯", "연동 관리"),
                                     EditorStyles.toolbarButton, GUILayout.Width(28)))
                {
                    ShowSheetActionsMenu();
                }
            }
        }

        /// <summary>연동 설정 자체를 다루는 행동들입니다.</summary>
        private void ShowSheetActionsMenu()
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("빠진 설정 만들기"), false, () =>
            {
                GoogleSheetSync.CreateMissingSettingsMenu();
                _sheets = null;
            });

            menu.AddItem(new GUIContent("설정 폴더 열기"), false, GoogleSheetSync.SelectSettingsFolderMenu);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("서비스 계정 키 설정…"), false,
                         () => SettingsService.OpenProjectSettings("Project/CSV Pipeline"));

            menu.ShowAsContext();
        }

        /// <summary>연동 설정 한 줄을 그립니다.</summary>
        /// <param name="settings">그릴 설정입니다.</param>
        /// <param name="index">화면에 보이는 순번입니다.</param>
        private static void DrawSheetRow(GoogleSheetSyncSettings settings, int index)
        {
            Rect row = EditorGUILayout.BeginHorizontal(GUILayout.Height(CsvEditorUI.RowHeight));
            CsvEditorUI.RowBackground(row, index);

            SheetState state = StateOf(settings);

            GUILayout.Space(CsvEditorUI.Gap);
            GUILayout.Label(StateIcon(state), GUILayout.Width(CsvEditorUI.StatusWidth),
                            GUILayout.Height(CsvEditorUI.RowHeight));

            string name = string.IsNullOrEmpty(settings.csvFileName) ? "(대상 없음)" : settings.csvFileName;
            GUILayout.Label(name, GUILayout.MinWidth(140));

            CsvEditorUI.ColoredLabel(StateWord(state), StateColor(state), EditorStyles.miniLabel,
                                     GUILayout.Width(90));

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(!settings.IsConfigured))
            {
                if (GUILayout.Button("비교", EditorStyles.miniButtonLeft, GUILayout.Width(44)))
                {
                    GoogleSheetSync.CompareOne(settings);
                }

                if (GUILayout.Button("받기", EditorStyles.miniButtonRight, GUILayout.Width(44)))
                {
                    GoogleSheetSync.PullOne(settings);
                }
            }

            GUILayout.Space(CsvEditorUI.GapTight);

            if (GUILayout.Button(CsvEditorUI.IconOr("d_Search Icon", "선택", "설정 에셋 고르기"),
                                 EditorStyles.miniButton, GUILayout.Width(28)))
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }

            GUILayout.Space(CsvEditorUI.Gap);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>연동 설정의 상태를 가릅니다.</summary>
        /// <param name="settings">볼 설정입니다.</param>
        /// <returns>상태입니다.</returns>
        private static SheetState StateOf(GoogleSheetSyncSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.sheetUrl)) return SheetState.NeedsUrl;
            if (!settings.IsConfigured) return SheetState.BadUrl;
            if (!settings.enabled) return SheetState.Off;

            return settings.autoPull ? SheetState.Auto : SheetState.Ready;
        }

        /// <summary>상태의 아이콘입니다.</summary>
        /// <param name="state">옮길 상태입니다.</param>
        /// <returns>그릴 내용입니다.</returns>
        private static GUIContent StateIcon(SheetState state)
        {
            switch (state)
            {
                case SheetState.BadUrl: return CsvEditorUI.IconOr("console.erroricon.sml", "!", "주소를 확인하십시오");
                case SheetState.NeedsUrl: return CsvEditorUI.IconOr("console.warnicon.sml", "?", "링크가 비어 있습니다");
                case SheetState.Off: return CsvEditorUI.IconOr("d_winbtn_mac_min", "·", "꺼져 있습니다");
                case SheetState.Auto: return CsvEditorUI.IconOr("d_Refresh", "~", "자동으로 받습니다");
                default: return CsvEditorUI.IconOr("TestPassed", "·", "받을 준비가 됐습니다");
            }
        }

        /// <summary>상태의 한 마디입니다.</summary>
        /// <param name="state">옮길 상태입니다.</param>
        /// <returns>표기 문자열입니다.</returns>
        private static string StateWord(SheetState state)
        {
            switch (state)
            {
                case SheetState.BadUrl: return "주소 확인 필요";
                case SheetState.NeedsUrl: return "링크 없음";
                case SheetState.Off: return "꺼짐";
                case SheetState.Auto: return "자동 받기";
                default: return "준비됨";
            }
        }

        /// <summary>상태의 색입니다.</summary>
        /// <param name="state">옮길 상태입니다.</param>
        /// <returns>글자색입니다.</returns>
        private static Color StateColor(SheetState state)
        {
            switch (state)
            {
                case SheetState.BadUrl:
                case SheetState.NeedsUrl: return CsvEditorUI.Danger;
                case SheetState.Auto: return CsvEditorUI.Accent;
                default: return CsvEditorUI.Muted;
            }
        }
    }
}
