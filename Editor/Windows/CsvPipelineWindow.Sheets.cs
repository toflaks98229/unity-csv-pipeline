using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
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
        private SearchField _sheetSearchField;

        /// <summary>지금 걸리는 연동 설정들입니다. 매 그리기마다 새로 만들지 않습니다.</summary>
        private readonly List<GoogleSheetSyncSettings> _visibleSheets = new List<GoogleSheetSyncSettings>();

        /// <summary>연동 설정을 거르는 검색어입니다.</summary>
        private string SheetSearch
        {
            get => SessionState.GetString(StateKey + "SheetSearch", string.Empty);
            set => SessionState.SetString(StateKey + "SheetSearch", value ?? string.Empty);
        }

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

            CollectVisibleSheets();

            _sheetScroll = EditorGUILayout.BeginScrollView(_sheetScroll);

            for (int i = 0; i < _visibleSheets.Count; i++) DrawSheetRow(_visibleSheets[i], i);

            if (_visibleSheets.Count == 0)
            {
                CsvEditorUI.EmptyState(
                    $"'{SheetSearch}' 에 걸리는 설정이 없습니다",
                    "대상 표의 파일 이름으로 찾습니다.",
                    "검색어 지우기", () => SheetSearch = string.Empty);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>검색어에 걸리는 설정만 모읍니다.</summary>
        private void CollectVisibleSheets()
        {
            _visibleSheets.Clear();

            string search = SheetSearch;
            bool all = string.IsNullOrWhiteSpace(search);

            foreach (GoogleSheetSyncSettings settings in Sheets)
            {
                if (all || Matches(settings, search.Trim())) _visibleSheets.Add(settings);
            }
        }

        /// <summary>이 설정이 검색어에 걸리는지 봅니다.</summary>
        /// <param name="settings">볼 설정입니다.</param>
        /// <param name="search">검색어입니다.</param>
        /// <returns>걸리면 true입니다.</returns>
        private static bool Matches(GoogleSheetSyncSettings settings, string search)
        {
            string name = settings.csvFileName;
            return !string.IsNullOrEmpty(name)
                && name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>시트 갈래의 도구 줄입니다.</summary>
        private void DrawSheetToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(
                        CsvEditorUI.IconAnd("d_Refresh", " 다시 훑기", "연동 설정 에셋을 다시 모읍니다"),
                        EditorStyles.toolbarButton, GUILayout.Width(86)))
                {
                    _sheets = null;
                }

                CsvEditorUI.ToolbarSeparator();

                // 비교가 먼저입니다. 받기는 로컬 표를 덮어쓰므로, 무엇이 달라지는지 본 뒤가 순서입니다.
                if (GUILayout.Button(
                        new GUIContent("전부 비교만", "시트와 로컬 표의 차이만 봅니다. 파일은 쓰지 않습니다"),
                        EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    GoogleSheetSync.CompareAllMenu();
                }

                if (GUILayout.Button(
                        new GUIContent("전부 받기", "켜진 설정을 모두 받아 로컬 표를 덮어씁니다"),
                        EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    GoogleSheetSync.PullAllMenu();
                }

                GUILayout.Space(CsvEditorUI.GapTight);

                if (_sheetSearchField == null) _sheetSearchField = new SearchField();

                string next = _sheetSearchField.OnToolbarGUI(SheetSearch, GUILayout.MinWidth(90));
                if (next != SheetSearch) SheetSearch = next;

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

            // 표 갈래와 같은 자리에 같은 뜻의 막대를 세웁니다. 줄에 마우스를 올려도 밝아지지 않는 것은
            // 여기서는 줄 자체가 단추가 아니기 때문입니다 — 밝아지면 누를 수 있다고 잘못 말하게 됩니다.
            CsvEditorUI.StatusBar(row, StateColor(state));

            GUILayout.Space(CsvEditorUI.Gap);
            GUILayout.Label(StateIcon(state), GUILayout.Width(CsvEditorUI.StatusWidth),
                            GUILayout.Height(CsvEditorUI.RowHeight));

            bool named = !string.IsNullOrEmpty(settings.csvFileName);
            GUILayout.Label(
                new GUIContent(named ? settings.csvFileName : "(대상 없음)",
                               named ? "이 설정이 덮어쓰는 표입니다" : "이 설정에 대상 표가 적혀 있지 않습니다"),
                GUILayout.MinWidth(120));

            CsvEditorUI.ColoredLabel(StateWord(state), StateColor(state), EditorStyles.miniLabel,
                                     GUILayout.Width(90));

            GUILayout.FlexibleSpace();

            bool ready = settings.IsConfigured;
            string why = ready ? null : "시트 주소가 없거나 읽을 수 없는 형식입니다. 설정 에셋의 Sheet Url 을 확인하십시오";

            using (new EditorGUI.DisabledScope(!ready))
            {
                if (GUILayout.Button(
                        new GUIContent("비교", why ?? "시트와 이 표의 차이만 봅니다. 파일은 쓰지 않습니다"),
                        EditorStyles.miniButtonLeft, GUILayout.Width(44)))
                {
                    GoogleSheetSync.CompareOne(settings);
                }

                if (GUILayout.Button(
                        new GUIContent("받기", why ?? "시트 내용으로 이 표를 덮어쓰고 다시 굽습니다"),
                        EditorStyles.miniButtonRight, GUILayout.Width(44)))
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
                // 주소가 틀린 것은 잘못된 상태이고, 아직 안 적은 것은 손볼 것이 남은 상태입니다.
                // 같은 빨강으로 두면 "고쳐야 할 것"과 "아직 안 한 것"이 구분되지 않습니다.
                case SheetState.BadUrl: return CsvEditorUI.Danger;
                case SheetState.NeedsUrl: return CsvEditorUI.Warning;
                case SheetState.Auto: return CsvEditorUI.Accent;
                default: return CsvEditorUI.Muted;
            }
        }
    }
}
