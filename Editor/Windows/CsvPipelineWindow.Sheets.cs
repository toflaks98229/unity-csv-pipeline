using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>The sheet sync tab. It shows each settings asset's current state on one line.</summary>
    public sealed partial class CsvPipelineWindow
    {
        /// <summary>State of a sync settings asset.</summary>
        private enum SheetState { Ready, Auto, Off, NeedsUrl, BadUrl }

        /// <summary>
        /// The sync settings list being held.
        /// <b>It is not looked up again every frame.</b> Drawing keeps running while the mouse moves,
        /// and searching the project and reading every asset each time would cost something from merely
        /// leaving the window open.
        /// </summary>
        private List<GoogleSheetSyncSettings> _sheets;

        private Vector2 _sheetScroll;
        private SearchField _sheetSearchField;

        /// <summary>The sync settings that currently match. It is not rebuilt on every draw.</summary>
        private readonly List<GoogleSheetSyncSettings> _visibleSheets = new List<GoogleSheetSyncSettings>();

        /// <summary>Search term that filters the sync settings.</summary>
        private string SheetSearch
        {
            get => SessionState.GetString(StateKey + "SheetSearch", string.Empty);
            set => SessionState.SetString(StateKey + "SheetSearch", value ?? string.Empty);
        }

        /// <summary>The sync settings list. If there is none yet, it is gathered here.</summary>
        private List<GoogleSheetSyncSettings> Sheets => _sheets ?? (_sheets = GoogleSheetSync.FindAll());

        /// <summary>Summary of the sheet tab for the status bar.</summary>
        /// <returns>Summary string.</returns>
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

            string text = $"Settings {Sheets.Count} · Auto {auto} · Ready {ready}";
            return needsWork > 0 ? $"{text} · Problems {needsWork}" : text;
        }

        /// <summary>Draws the sheet sync tab.</summary>
        private void DrawSheets()
        {
            DrawSheetToolbar();

            if (Sheets.Count == 0)
            {
                CsvEditorUI.EmptyState(
                    "No sync settings",
                    "Authoring from sheets needs one settings asset per table.\n"
                    + "'Create Settings' scans the tables under the CSV root and creates only the missing ones.\n"
                    + "After that, paste the browser URL into each asset and turn Enabled on.\n\n"
                    + "If you do not use sheets, you can leave this tab empty. Nothing here runs every frame.",
                    "Create Settings", () =>
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
                    $"No settings match '{SheetSearch}'",
                    "The search looks at the file name of the target table.",
                    "Clear Search", () => SheetSearch = string.Empty);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>Gathers only the settings that match the search term.</summary>
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

        /// <summary>Checks whether this settings asset matches the search term.</summary>
        /// <param name="settings">Settings to check.</param>
        /// <param name="search">Search term.</param>
        /// <returns>True if it matches.</returns>
        private static bool Matches(GoogleSheetSyncSettings settings, string search)
        {
            string name = settings.csvFileName;
            return !string.IsNullOrEmpty(name)
                && name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Toolbar of the sheet tab.</summary>
        private void DrawSheetToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(
                        CsvEditorUI.IconAnd("d_Refresh", " Rescan", "Gathers the sync settings assets again"),
                        EditorStyles.toolbarButton, GUILayout.Width(86)))
                {
                    _sheets = null;
                }

                CsvEditorUI.ToolbarSeparator();

                // 비교가 먼저입니다. 받기는 로컬 표를 덮어쓰므로, 무엇이 달라지는지 본 뒤가 순서입니다.
                if (GUILayout.Button(
                        new GUIContent("Compare All", "Shows only the differences between the sheets and the local tables. Writes no files"),
                        EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    GoogleSheetSync.CompareAllMenu();
                }

                if (GUILayout.Button(
                        new GUIContent("Pull All", "Pulls every enabled settings asset and overwrites the local tables"),
                        EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    GoogleSheetSync.PullAllMenu();
                }

                GUILayout.Space(CsvEditorUI.GapTight);

                if (_sheetSearchField == null) _sheetSearchField = new SearchField();

                string next = _sheetSearchField.OnToolbarGUI(SheetSearch, GUILayout.MinWidth(90));
                if (next != SheetSearch) SheetSearch = next;

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(CsvEditorUI.IconOr("_Popup", "⋯", "Manage sync"),
                                     EditorStyles.toolbarButton, GUILayout.Width(28)))
                {
                    ShowSheetActionsMenu();
                }
            }
        }

        /// <summary>Actions that operate on the sync settings themselves.</summary>
        private void ShowSheetActionsMenu()
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Create Missing Settings"), false, () =>
            {
                GoogleSheetSync.CreateMissingSettingsMenu();
                _sheets = null;
            });

            menu.AddItem(new GUIContent("Open Settings Folder"), false, GoogleSheetSync.SelectSettingsFolderMenu);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Service Account Key Settings…"), false,
                         () => SettingsService.OpenProjectSettings("Project/CSV Pipeline"));

            menu.ShowAsContext();
        }

        /// <summary>Draws one sync settings row.</summary>
        /// <param name="settings">Settings to draw.</param>
        /// <param name="index">Position as shown on screen.</param>
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
                new GUIContent(named ? settings.csvFileName : "(no target)",
                               named ? "The table this settings asset overwrites" : "This settings asset names no target table"),
                GUILayout.MinWidth(120));

            CsvEditorUI.ColoredLabel(StateWord(state), StateColor(state), EditorStyles.miniLabel,
                                     GUILayout.Width(90));

            GUILayout.FlexibleSpace();

            bool ready = settings.IsConfigured;
            string why = ready ? null : "The sheet URL is missing or in a form that cannot be read. Check Sheet Url on the settings asset";

            using (new EditorGUI.DisabledScope(!ready))
            {
                if (GUILayout.Button(
                        new GUIContent("Compare", why ?? "Shows only the differences between the sheet and this table. Writes no files"),
                        EditorStyles.miniButtonLeft, GUILayout.Width(44)))
                {
                    GoogleSheetSync.CompareOne(settings);
                }

                if (GUILayout.Button(
                        new GUIContent("Pull", why ?? "Overwrites this table with the sheet's contents and rebakes it"),
                        EditorStyles.miniButtonRight, GUILayout.Width(44)))
                {
                    GoogleSheetSync.PullOne(settings);
                }
            }

            GUILayout.Space(CsvEditorUI.GapTight);

            if (GUILayout.Button(CsvEditorUI.IconOr("d_Search Icon", "Select", "Select the settings asset"),
                                 EditorStyles.miniButton, GUILayout.Width(28)))
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }

            GUILayout.Space(CsvEditorUI.Gap);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Works out the state of a sync settings asset.</summary>
        /// <param name="settings">Settings to check.</param>
        /// <returns>The state.</returns>
        private static SheetState StateOf(GoogleSheetSyncSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.sheetUrl)) return SheetState.NeedsUrl;
            if (!settings.IsConfigured) return SheetState.BadUrl;
            if (!settings.enabled) return SheetState.Off;

            return settings.autoPull ? SheetState.Auto : SheetState.Ready;
        }

        /// <summary>Icon for the state.</summary>
        /// <param name="state">State to translate.</param>
        /// <returns>Content to draw.</returns>
        private static GUIContent StateIcon(SheetState state)
        {
            switch (state)
            {
                case SheetState.BadUrl: return CsvEditorUI.IconOr("console.erroricon.sml", "!", "Check the URL");
                case SheetState.NeedsUrl: return CsvEditorUI.IconOr("console.warnicon.sml", "?", "The link is empty");
                case SheetState.Off: return CsvEditorUI.IconOr("d_winbtn_mac_min", "·", "Turned off");
                case SheetState.Auto: return CsvEditorUI.IconOr("d_Refresh", "~", "Pulls automatically");
                default: return CsvEditorUI.IconOr("TestPassed", "·", "Ready to pull");
            }
        }

        /// <summary>One word for the state.</summary>
        /// <param name="state">State to translate.</param>
        /// <returns>Display string.</returns>
        private static string StateWord(SheetState state)
        {
            switch (state)
            {
                case SheetState.BadUrl: return "Check URL";
                case SheetState.NeedsUrl: return "No link";
                case SheetState.Off: return "Off";
                case SheetState.Auto: return "Auto pull";
                default: return "Ready";
            }
        }

        /// <summary>Color for the state.</summary>
        /// <param name="state">State to translate.</param>
        /// <returns>Text color.</returns>
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
