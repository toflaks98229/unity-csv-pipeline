using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>The Tables tab. For each table it shows what baking right now would change.</summary>
    public sealed partial class CsvPipelineWindow
    {
        /// <summary>What the window holds for a single table.</summary>
        private sealed class Entry
        {
            /// <summary>The importer that bakes this table.</summary>
            public CsvImportDefinition Definition;

            /// <summary>What baking right now would change.</summary>
            public CsvImportPlan Plan;

            /// <summary>Path to the source table. Null when it was not found.</summary>
            public string CsvPath;
        }

        /// <summary>Table count at which the progress bar starts showing. With only a few, the bar just flickers.</summary>
        private const int ProgressThreshold = 12;

        private readonly List<Entry> _entries = new List<Entry>();

        /// <summary>
        /// The entries currently matching on screen. <b>This is not rebuilt on every draw</b> —
        /// drawing keeps running while the mouse moves.
        /// </summary>
        private readonly List<Entry> _visible = new List<Entry>();

        private bool _scanned;
        private Vector2 _tableScroll;
        private SearchField _searchField;

        /// <summary>The row the mouse is over. -1 when there is none.</summary>
        private int _hoverIndex = -1;

        /// <summary>Whether the selected row has to be pulled into view.</summary>
        private bool _scrollToSelection;

        /// <summary>When the last scan ran. Used to tell how old what you are looking at is.</summary>
        private string ScannedAt
        {
            get => SessionState.GetString(StateKey + "ScannedAt", string.Empty);
            set => SessionState.SetString(StateKey + "ScannedAt", value ?? string.Empty);
        }

        /// <summary>The search text. It survives a domain reload.</summary>
        private string Search
        {
            get => SessionState.GetString(StateKey + "Search", string.Empty);
            set => SessionState.SetString(StateKey + "Search", value ?? string.Empty);
        }

        /// <summary>The view currently selected.</summary>
        private CsvTableView View
        {
            get => (CsvTableView)SessionState.GetInt(StateKey + "View", (int)CsvTableView.Changed);
            set => SessionState.SetInt(StateKey + "View", (int)value);
        }

        /// <summary>
        /// File name of the table selected with the keyboard.
        /// <b>It is held by name, not by position.</b> That way, when filtering or a rescan reorders the
        /// list, the selection does not jump to some other table.
        /// </summary>
        private string SelectedFile
        {
            get => SessionState.GetString(StateKey + "Selected", string.Empty);
            set => SessionState.SetString(StateKey + "Selected", value ?? string.Empty);
        }

        /// <summary>Whether one table is expanded.</summary>
        /// <param name="fileName">Table file name.</param>
        /// <returns>True when it is expanded.</returns>
        private static bool IsExpanded(string fileName)
            => SessionState.GetBool(StateKey + "Open." + fileName, false);

        /// <summary>Sets whether one table is expanded.</summary>
        /// <param name="fileName">Table file name.</param>
        /// <param name="open">Whether to expand it.</param>
        private static void SetExpanded(string fileName, bool open)
            => SessionState.SetBool(StateKey + "Open." + fileName, open);

        // ====================================================================================================
        // 훑기
        // ====================================================================================================

        /// <summary>
        /// Recomputes the plan for every registered table.
        /// </summary>
        /// <param name="interactive">
        /// Whether a person triggered this. The first scan, which the draw pass calls on its own,
        /// <b>shows no progress bar.</b> Raising a bar in the middle of OnGUI makes Layout and Repaint
        /// draw different things.
        /// </param>
        private void Rescan(bool interactive = true)
        {
            _entries.Clear();
            _scanned = true;

            var definitions = new List<CsvImportDefinition>(CsvImportDefinition.DiscoverAll());
            foreach (CsvSchema schema in CsvSchema.All()) definitions.Add(new CsvSchemaImportDefinition(schema));

            bool showProgress = interactive && definitions.Count >= ProgressThreshold;

            try
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    if (showProgress)
                    {
                        EditorUtility.DisplayProgressBar("CSV Pipeline", "Scanning tables…",
                                                         (float)i / Mathf.Max(1, definitions.Count));
                    }

                    CsvImportPlan plan = definitions[i].Plan();
                    _entries.Add(new Entry
                    {
                        Definition = definitions[i],
                        Plan = plan,
                        CsvPath = CsvAssetPipeline.FindCsvPath(plan.FileName)
                    });
                }
            }
            finally
            {
                if (showProgress) EditorUtility.ClearProgressBar();
            }

            _entries.Sort(CompareEntries);
            ScannedAt = DateTime.Now.ToString("HH:mm:ss");
        }

        /// <summary>
        /// Lifts tables that need attention to the top. Sorted by name alone, the ones worth looking at
        /// end up buried at the bottom.
        /// </summary>
        /// <param name="a">Entry to compare.</param>
        /// <param name="b">Entry to compare.</param>
        /// <returns>Sort order.</returns>
        private static int CompareEntries(Entry a, Entry b)
        {
            int byState = CsvPlanStatus.Of(a.Plan).CompareTo(CsvPlanStatus.Of(b.Plan));
            if (byState != 0) return -byState;   // 값이 큰 상태(문제·삭제)가 위로

            return string.CompareOrdinal(a.Plan.FileName, b.Plan.FileName);
        }

        /// <summary>Turns a state into an icon and a description.</summary>
        /// <param name="state">State to turn.</param>
        /// <returns>What to draw at the start of the row.</returns>
        private static GUIContent StateIcon(CsvPlanState state)
        {
            switch (state)
            {
                case CsvPlanState.Problem: return CsvEditorUI.IconOr("console.erroricon.sml", "!", "Has problems");
                case CsvPlanState.Blocked: return CsvEditorUI.IconOr("console.warnicon.sml", "?", "Could not build a plan");
                case CsvPlanState.Removing: return CsvEditorUI.IconOr("console.warnicon.sml", "−", "Some output assets go away");
                case CsvPlanState.Changed: return CsvEditorUI.IconOr("d_Refresh", "~", "Something changes");
                default: return CsvEditorUI.IconOr("TestPassed", "·", "Table and output assets match");
            }
        }

        /// <summary>The Tables tab summary for the status bar.</summary>
        /// <returns>Summary text.</returns>
        private string TablesStatus()
        {
            if (!_scanned) return "Scanning…";

            int changed = 0, problems = 0;
            foreach (Entry entry in _entries)
            {
                CsvPlanState state = CsvPlanStatus.Of(entry.Plan);
                if (CsvPlanStatus.NeedsAttention(state)) problems++;
                else if (state != CsvPlanState.Ok) changed++;
            }

            string text = problems > 0
                ? $"{_entries.Count} tables · {changed} changed · {problems} problems"
                : $"{_entries.Count} tables · {changed} changed";

            // 지금 보는 것이 언제 것인지 모르면, 밖에서 표를 고친 뒤에도 낡은 화면을 믿게 됩니다.
            string at = ScannedAt;
            return string.IsNullOrEmpty(at) ? text : $"{text}   ·   as of {at}";
        }

        // ====================================================================================================
        // 그리기
        // ====================================================================================================

        /// <summary>Draws the Tables tab.</summary>
        private void DrawTables()
        {
            if (!_scanned) Rescan(interactive: false);

            if (_entries.Count == 0)
            {
                DrawGettingStarted();
                return;
            }

            DrawTableToolbar();
            CollectVisible();
            HandleListKeys();

            bool tracking = Event.current.type == EventType.MouseMove;
            int hovered = -1;

            _tableScroll = EditorGUILayout.BeginScrollView(_tableScroll);

            for (int i = 0; i < _visible.Count; i++)
            {
                if (DrawEntry(_visible[i], i) && tracking) hovered = i;
            }

            if (_visible.Count == 0) DrawNothingMatched();

            EditorGUILayout.EndScrollView();

            // 마우스를 따라 다시 그리는 것은 값이 듭니다. 올라간 줄이 <b>바뀔 때만</b> 그립니다.
            if (tracking && hovered != _hoverIndex)
            {
                _hoverIndex = hovered;
                Repaint();
            }
        }

        /// <summary>Collects only the entries that match right now.</summary>
        private void CollectVisible()
        {
            _visible.Clear();

            CsvTableView view = View;
            string search = Search;

            foreach (Entry entry in _entries)
            {
                if (CsvTableFilter.Matches(entry.Plan, view, search)) _visible.Add(entry);
            }
        }

        /// <summary>The Tables tab toolbar. Views on the left, actions that affect everything on the right.</summary>
        private void DrawTableToolbar()
        {
            if (_searchField == null)
            {
                _searchField = new SearchField();

                // 검색 칸에서 위·아래를 누르면 목록으로 넘어갑니다. 글자를 치다 바로 고를 수 있습니다.
                _searchField.downOrUpArrowKeyPressed += () => GUIUtility.keyboardControl = 0;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(
                        CsvEditorUI.IconAnd("d_Refresh", " Rescan", "Reads every table again and rebuilds the plans"),
                        EditorStyles.toolbarButton, GUILayout.Width(86)))
                {
                    Rescan();
                }

                CsvEditorUI.ToolbarSeparator();

                DrawViewDropdown();

                GUILayout.Space(CsvEditorUI.GapTight);

                string next = _searchField.OnToolbarGUI(Search, GUILayout.MinWidth(110));
                if (next != Search)
                {
                    Search = next;
                    _scrollToSelection = true;
                }

                GUILayout.FlexibleSpace();

                // 표를 통째로 다시 굽거나 내보내는 것은 되돌릴 수 없어, 자주 쓰는 단추 옆에 두지 않습니다.
                if (GUILayout.Button(CsvEditorUI.IconOr("_Popup", "⋯", "Expand · Rebuild all · Export"),
                                     EditorStyles.toolbarButton, GUILayout.Width(28)))
                {
                    ShowTableActionsMenu();
                }
            }
        }

        /// <summary>The dropdown that selects what to show.</summary>
        private void DrawViewDropdown()
        {
            CsvTableView current = View;
            var content = new GUIContent(CsvTableFilter.Label(current), CsvTableFilter.Describe(current));

            Rect rect = GUILayoutUtility.GetRect(content, EditorStyles.toolbarDropDown, GUILayout.Width(96));
            if (!EditorGUI.DropdownButton(rect, content, FocusType.Passive, EditorStyles.toolbarDropDown)) return;

            var menu = new GenericMenu();
            foreach (CsvTableView view in new[] { CsvTableView.Changed, CsvTableView.Problems, CsvTableView.All })
            {
                CsvTableView captured = view;
                menu.AddItem(new GUIContent(CsvTableFilter.Label(view)), view == current, () =>
                {
                    View = captured;
                    _scrollToSelection = true;
                });
            }
            menu.DropDown(rect);
        }

        /// <summary>Actions that affect everything. The ones that cannot be undone ask once more.</summary>
        private void ShowTableActionsMenu()
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Expand All"), false, () => SetAllExpanded(true));
            menu.AddItem(new GUIContent("Collapse All"), false, () => SetAllExpanded(false));
            menu.AddSeparator(string.Empty);

            menu.AddItem(new GUIContent("Rebuild All Tables"), false, () =>
            {
                if (!EditorUtility.DisplayDialog(
                        "Rebuild All Tables",
                        $"Rebakes all {_entries.Count} registered tables.\n\n"
                        + "Output assets whose rows are gone from the table are deleted when nothing still references them.\n"
                        + "This cannot be undone with Ctrl+Z.",
                        "Bake", "Cancel"))
                {
                    return;
                }

                CsvRebuildMenu.RebuildAllMenu();
                Rescan();
            });

            menu.AddItem(new GUIContent("Export Assets to Tables"), false, () =>
            {
                if (!EditorUtility.DisplayDialog(
                        "Export Assets to Tables",
                        "Overwrites the source table files with the current values of the output assets.\n\n"
                        + "Edits made only on the table side are lost. For a table with sheet sync on,\n"
                        + "this puts it out of step with the sheet, so check with 'Compare All' first.",
                        "Export", "Cancel"))
                {
                    return;
                }

                CsvExporter.ExportAllMenu();
                Rescan();
            });

            menu.ShowAsContext();
        }

        /// <summary>Expands or collapses every table currently visible.</summary>
        /// <param name="open">Whether to expand them.</param>
        private void SetAllExpanded(bool open)
        {
            foreach (Entry entry in _visible) SetExpanded(entry.Plan.FileName, open);
            Repaint();
        }

        // ====================================================================================================
        // 키보드
        // ====================================================================================================

        /// <summary>
        /// Takes one key and moves the list.
        /// <para>
        /// The Unity Editor Design System requires every screen to be reachable <b>with the keyboard
        /// alone, without a mouse</b> (US-0180). <see cref="CsvListKeys"/> decides what each key means,
        /// and this method only applies that meaning to the window.
        /// </para>
        /// </summary>
        private void HandleListKeys()
        {
            Event e = Event.current;
            CsvListCommand command = CsvListKeys.Read(e.type, e.keyCode, e.modifiers);
            if (command == CsvListCommand.None) return;

            // 찾기와 지우기는 글자를 치고 있는 중에도 들어야 합니다.
            if (command == CsvListCommand.Find)
            {
                _searchField.SetFocus();
                e.Use();
                return;
            }

            if (command == CsvListCommand.ClearSearch)
            {
                if (string.IsNullOrEmpty(Search)) return;   // 지울 것이 없으면 남이 쓰게 둡니다

                Search = string.Empty;
                GUIUtility.keyboardControl = 0;
                e.Use();
                Repaint();
                return;
            }

            // 나머지는 글자 칸에 초점이 없을 때만입니다. 그러지 않으면 검색어를 못 고칩니다.
            if (GUIUtility.keyboardControl != 0) return;
            if (_visible.Count == 0) return;

            int index = IndexOfSelected();

            switch (command)
            {
                case CsvListCommand.MoveUp:
                case CsvListCommand.MoveDown:
                case CsvListCommand.MoveFirst:
                case CsvListCommand.MoveLast:
                    Select(CsvListKeys.Move(command, index, _visible.Count));
                    break;

                case CsvListCommand.Expand:
                    if (index < 0) Select(0);
                    else SetExpanded(_visible[index].Plan.FileName, true);
                    break;

                case CsvListCommand.Collapse:
                    if (index >= 0) SetExpanded(_visible[index].Plan.FileName, false);
                    break;

                case CsvListCommand.Toggle:
                    if (index < 0) Select(0);
                    else SetExpanded(_visible[index].Plan.FileName, !IsExpanded(_visible[index].Plan.FileName));
                    break;

                case CsvListCommand.Activate:
                    e.Use();
                    if (index >= 0) BakeOne(_visible[index]);
                    return;
            }

            e.Use();
            Repaint();
        }

        /// <summary>Where the selected table sits in the current list. -1 when there is none.</summary>
        /// <returns>Index of the row.</returns>
        private int IndexOfSelected()
        {
            string selected = SelectedFile;
            if (string.IsNullOrEmpty(selected)) return -1;

            for (int i = 0; i < _visible.Count; i++)
            {
                if (_visible[i].Plan.FileName == selected) return i;
            }
            return -1;
        }

        /// <summary>Selects the table at that index.</summary>
        /// <param name="index">Index to select.</param>
        private void Select(int index)
        {
            if (index < 0 || index >= _visible.Count) return;

            SelectedFile = _visible[index].Plan.FileName;
            _scrollToSelection = true;
        }

        /// <summary>Bakes one table and rescans the list.</summary>
        /// <param name="entry">Entry to bake.</param>
        private void BakeOne(Entry entry)
        {
            if (entry.CsvPath == null || entry.Plan.IsNoOp) return;

            CsvImportReport report = entry.Definition.Run(entry.CsvPath);
            Rescan();

            // 사람이 눌러서 구운 자리입니다. 여기서만 알립니다 — 자동 임포트나 미리보기에서
            // 같은 알림이 뜨면, 목록을 보는 것만으로 대화상자가 뜹니다.
            CsvBakeAlert.ShowIfNeeded(report);

            GUIUtility.ExitGUI();   // 목록이 바뀌었으므로 이번 프레임 그리기를 멈춥니다.
        }

        // ====================================================================================================
        // 거르기
        // ====================================================================================================

        /// <summary>The message shown when no table matches. It also names which condition filtered them out.</summary>
        private void DrawNothingMatched()
        {
            bool searching = !string.IsNullOrWhiteSpace(Search);
            CsvTableView view = View;
            bool hiding = view != CsvTableView.All;

            if (searching)
            {
                // 걸러 낸 조건이 둘일 수 있습니다. 어느 쪽이 범인인지 모르면 사람이 검색어만 의심합니다.
                (string, Action)[] extras = hiding
                    ? new (string, Action)[] { ("Show Everything", () => View = CsvTableView.All) }
                    : Array.Empty<(string, Action)>();

                CsvEditorUI.EmptyState(
                    $"No table matches '{Search}'",
                    hiding
                        ? $"You are viewing '{CsvTableFilter.Label(view)}'. {CsvTableFilter.Describe(view)}"
                        : "The search matches table file names, output type names, and output folders.",
                    "Clear Search", () => Search = string.Empty,
                    extras);
                return;
            }

            CsvEditorUI.EmptyState(
                view == CsvTableView.Problems ? "No table needs attention" : "No table changes",
                view == CsvTableView.Problems
                    ? "No table has a problem, and none failed to produce a plan."
                    : "Every output asset matches its table. Baking now would change nothing.",
                "Show Everything", () => View = CsvTableView.All);
        }

        // ====================================================================================================
        // 표 한 장
        // ====================================================================================================

        /// <summary>Draws one table.</summary>
        /// <param name="entry">Entry to draw.</param>
        /// <param name="index">Position on screen. Used to alternate the row background.</param>
        /// <returns>True when the mouse is over this row.</returns>
        private bool DrawEntry(Entry entry, int index)
        {
            CsvImportPlan plan = entry.Plan;
            bool open = IsExpanded(plan.FileName);

            bool hovered = DrawEntryHeader(entry, index, open, out bool clicked);
            if (clicked)
            {
                SelectedFile = plan.FileName;
                SetExpanded(plan.FileName, !open);
                open = !open;
            }

            if (!open) return hovered;

            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.Space(CsvEditorUI.GapTight);

                foreach (CsvIssue issue in plan.Issues)
                {
                    string where = issue.Where;
                    EditorGUILayout.HelpBox(
                        string.IsNullOrEmpty(where) ? issue.Message : $"{where} — {issue.Message}",
                        issue.Severity == CsvIssueSeverity.Error ? MessageType.Error : MessageType.Warning);
                }

                if (!plan.IsSupported) EditorGUILayout.HelpBox(plan.Unsupported, MessageType.Info);

                foreach (CsvPlannedChange change in plan.Changes) DrawChange(change);

                DrawEntryActions(entry);
                GUILayout.Space(CsvEditorUI.Gap);
            }

            return hovered;
        }

        /// <summary>
        /// The header row of one table. <b>The whole row is the foldout button</b>, so there is no need to
        /// aim precisely at the triangle.
        /// </summary>
        /// <param name="entry">Entry to draw.</param>
        /// <param name="index">Position on screen.</param>
        /// <param name="open">Whether it is expanded.</param>
        /// <param name="clicked">Receives true when the row was clicked.</param>
        /// <returns>True when the mouse is over this row.</returns>
        private bool DrawEntryHeader(Entry entry, int index, bool open, out bool clicked)
        {
            CsvImportPlan plan = entry.Plan;
            CsvPlanState state = CsvPlanStatus.Of(plan);
            bool selected = plan.FileName == SelectedFile;

            Rect row = EditorGUILayout.BeginHorizontal(GUILayout.Height(CsvEditorUI.RowHeight));

            bool hovered = row.Contains(Event.current.mousePosition);
            CsvEditorUI.RowBackground(row, index, selected, hovered);
            CsvEditorUI.StatusBar(row, StateColor(state));

            GUILayout.Space(CsvEditorUI.Gap);

            // 삼각형만 정확히 그립니다. Foldout 컨트롤을 쓰면 폭이 줄에 따라 흔들려 아래 칸이 어긋납니다.
            Rect arrow = GUILayoutUtility.GetRect(14f, CsvEditorUI.RowHeight, GUILayout.Width(14f));
            if (Event.current.type == EventType.Repaint)
            {
                EditorStyles.foldout.Draw(arrow, false, false, open, false);
            }

            GUILayout.Label(StateIcon(state), GUILayout.Width(CsvEditorUI.StatusWidth),
                            GUILayout.Height(CsvEditorUI.RowHeight));

            GUILayout.Label(plan.Label, EditorStyles.boldLabel, GUILayout.MinWidth(60));
            CsvEditorUI.ColoredLabel(plan.FileName, CsvEditorUI.Muted, EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();
            CsvEditorUI.ColoredLabel(plan.Summary(), StateColor(state), EditorStyles.miniLabel);
            GUILayout.Space(CsvEditorUI.Gap);

            EditorGUILayout.EndHorizontal();

            // 고른 줄이 화면 밖에 있으면 끌어옵니다. 키로 옮겨 놓고 안 보이면 옮긴 뜻이 없습니다.
            if (selected && _scrollToSelection && Event.current.type == EventType.Repaint)
            {
                _scrollToSelection = false;
                GUI.ScrollTo(row);
            }

            clicked = HandleRowMouse(entry, row);
            return hovered;
        }

        /// <summary>Handles the mouse over a header row.</summary>
        /// <param name="entry">Entry for this row.</param>
        /// <param name="row">Rect of the row.</param>
        /// <returns>True when the expansion has to be toggled.</returns>
        private bool HandleRowMouse(Entry entry, Rect row)
        {
            Event e = Event.current;
            if (!row.Contains(e.mousePosition)) return false;

            if (e.type == EventType.ContextClick)
            {
                SelectedFile = entry.Plan.FileName;
                ShowEntryMenu(entry);
                e.Use();
                return false;
            }

            if (e.type != EventType.MouseDown || e.button != 0) return false;

            GUIUtility.keyboardControl = 0;   // 검색 칸에 남아 있던 초점을 걷어 키가 목록으로 옵니다.

            // 두 번 누르면 원본 표로 갑니다. 펼치기는 이미 한 번 누르기가 맡고 있습니다.
            if (e.clickCount == 2 && entry.CsvPath != null)
            {
                SelectedFile = entry.Plan.FileName;
                Ping(entry.CsvPath);
                e.Use();
                return false;
            }

            e.Use();
            return true;
        }

        /// <summary>The menu shown when a row is right-clicked.</summary>
        /// <param name="entry">Target entry.</param>
        private void ShowEntryMenu(Entry entry)
        {
            var menu = new GenericMenu();

            AddMenuItem(menu, "Bake Now", entry.CsvPath != null && !entry.Plan.IsNoOp, () => BakeOne(entry));
            menu.AddSeparator(string.Empty);
            AddMenuItem(menu, "Open Table", entry.CsvPath != null, () => Ping(entry.CsvPath));
            AddMenuItem(menu, "Open Output Folder", !string.IsNullOrEmpty(entry.Plan.OutputFolder),
                        () => Ping(entry.Plan.OutputFolder));
            menu.AddSeparator(string.Empty);
            AddMenuItem(menu, "Copy Table Path", entry.CsvPath != null,
                        () => EditorGUIUtility.systemCopyBuffer = entry.CsvPath);

            menu.ShowAsContext();
        }

        /// <summary>
        /// Items that cannot be used are <b>greyed out, not hidden</b>. Hiding them changes the shape of
        /// the menu from row to row, so the spot your hand remembers is somewhere else every time.
        /// </summary>
        /// <param name="menu">Menu to fill.</param>
        /// <param name="label">Item name.</param>
        /// <param name="enabled">Whether the item can be chosen.</param>
        /// <param name="action">What to do when it is chosen.</param>
        private static void AddMenuItem(GenericMenu menu, string label, bool enabled, GenericMenu.MenuFunction action)
        {
            if (enabled) menu.AddItem(new GUIContent(label), false, action);
            else menu.AddDisabledItem(new GUIContent(label));
        }

        /// <summary>The color for a state. It always comes with an icon and a word, so color alone never carries the meaning.</summary>
        /// <param name="state">State of the row.</param>
        /// <returns>Text color.</returns>
        private static Color StateColor(CsvPlanState state)
        {
            switch (state)
            {
                case CsvPlanState.Problem:
                case CsvPlanState.Blocked: return CsvEditorUI.Danger;
                case CsvPlanState.Removing: return CsvEditorUI.Warning;
                case CsvPlanState.Changed: return CsvEditorUI.Accent;
                default: return CsvEditorUI.Muted;
            }
        }

        /// <summary>The actions available on an expanded table.</summary>
        /// <param name="entry">Target entry.</param>
        private void DrawEntryActions(Entry entry)
        {
            GUILayout.Space(CsvEditorUI.GapTight);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(CsvEditorUI.GapSection);

                bool hasCsv = entry.CsvPath != null;
                using (new EditorGUI.DisabledScope(!hasCsv))
                {
                    if (GUILayout.Button(
                            CsvEditorUI.IconAnd("d_TextAsset Icon", " Open Table",
                                                // 표 조회는 프로젝트 전체를 이름으로 찾습니다 — CSV 루트 설정과
                                                // 무관합니다. 그 설정을 보라고 하면 사람은 멀쩡한 값을 들여다봅니다.
                                                hasCsv ? "Pings the source table in the Project window"
                                                       : "No table file with this name exists in the project. Check that the declared file name matches the actual one"),
                            EditorStyles.miniButton, GUILayout.Width(88)))
                    {
                        Ping(entry.CsvPath);
                    }
                }

                bool hasFolder = !string.IsNullOrEmpty(entry.Plan.OutputFolder);
                using (new EditorGUI.DisabledScope(!hasFolder))
                {
                    if (GUILayout.Button(
                            CsvEditorUI.IconAnd("Folder Icon", " Output Folder",
                                                hasFolder ? "Pings the folder the baked assets go into"
                                                          : "This importer does not report an output folder"),
                            EditorStyles.miniButton, GUILayout.Width(100)))
                    {
                        Ping(entry.Plan.OutputFolder);
                    }
                }

                GUILayout.FlexibleSpace();

                bool canBake = hasCsv && !entry.Plan.IsNoOp;
                using (new EditorGUI.DisabledScope(!canBake))
                {
                    string why = !hasCsv ? "The source table file was not found"
                               : entry.Plan.IsNoOp ? "Baking now would change nothing"
                               : "Bakes this table alone, right now. This cannot be undone with Ctrl+Z";

                    if (GUILayout.Button(new GUIContent("Bake Now", why), GUILayout.Width(84)))
                    {
                        BakeOne(entry);
                    }
                }

                GUILayout.Space(CsvEditorUI.Gap);
            }
        }

        /// <summary>Draws one change row.</summary>
        /// <param name="change">Change to draw.</param>
        private static void DrawChange(CsvPlannedChange change)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(CsvEditorUI.GapSection);

                GUILayout.Label(ChangeIcon(change.Kind), GUILayout.Width(CsvEditorUI.StatusWidth));
                CsvEditorUI.ColoredLabel(ChangeWord(change.Kind), ChangeColor(change.Kind),
                                         EditorStyles.miniLabel, GUILayout.Width(44));

                GUILayout.Label(new GUIContent(change.DisplayName, change.AssetPath), GUILayout.MinWidth(100));

                if (change.Line > 0)
                {
                    CsvEditorUI.ColoredLabel(new GUIContent($"#{change.Line}", "Row number in the source table"),
                                             CsvEditorUI.Muted, EditorStyles.miniLabel, GUILayout.Width(44));
                }

                if (!string.IsNullOrEmpty(change.Note))
                {
                    CsvEditorUI.ColoredLabel(change.Note, CsvEditorUI.Muted);
                }

                GUILayout.FlexibleSpace();

                if (!string.IsNullOrEmpty(change.AssetPath)
                    && GUILayout.Button(new GUIContent("Ping", "Pings this asset in the Project window"),
                                        EditorStyles.miniButton, GUILayout.Width(44)))
                {
                    Ping(change.AssetPath);
                }

                GUILayout.Space(CsvEditorUI.Gap);
            }

            DrawFieldChanges(change);
        }

        /// <summary>
        /// How each individual field changes.
        /// Column name, before, and after all sit at <b>the same horizontal position</b>, so several rows
        /// can be scanned down the page. Column widths follow the window width — fixed pixels would cut
        /// the row off sideways once the window is narrowed.
        /// </summary>
        /// <param name="change">Change to draw.</param>
        private static void DrawFieldChanges(CsvPlannedChange change)
        {
            if (change.Fields.Count == 0) return;

            float indent = CsvEditorUI.GapSection * 2f + CsvEditorUI.StatusWidth;
            float available = Mathf.Max(180f, EditorGUIUtility.currentViewWidth - indent - CsvEditorUI.GapSection * 3f);

            float columnWidth = Mathf.Clamp(available * 0.24f, 64f, 150f);
            float valueWidth = Mathf.Clamp(available * 0.30f, 72f, 240f);

            foreach (CsvFieldChange field in change.Fields)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(indent);

                    CsvEditorUI.ColoredLabel(new GUIContent(field.Column, $"Field {field.Field}"),
                                             CsvEditorUI.Muted, EditorStyles.miniLabel,
                                             GUILayout.Width(columnWidth));

                    GUILayout.Label(Value(field.From, valueWidth), EditorStyles.miniLabel,
                                    GUILayout.Width(valueWidth));
                    CsvEditorUI.ColoredLabel("→", CsvEditorUI.Muted, EditorStyles.miniLabel,
                                             GUILayout.Width(16));
                    CsvEditorUI.ColoredLabel(Value(field.To, valueWidth), CsvEditorUI.Accent,
                                             EditorStyles.miniLabel);

                    GUILayout.FlexibleSpace();
                }
            }
        }

        /// <summary>
        /// The message shown when there is not a single table. Opening this window right after installing
        /// starts here, so it does not stop at "there is nothing" — it says what to do next.
        /// </summary>
        private void DrawGettingStarted()
        {
            CsvPipelineSettings settings = CsvPipelineSettings.Instance;

            CsvEditorUI.EmptyState(
                "No tables are baked yet",
                $"The CSV root currently in effect is {settings.CsvRootFolder}.\n\n"
                + "There are two ways to hook a table up.\n"
                + "① Without code — put [CsvAsset(\"table.csv\", \"IdColumn\")] on a ScriptableObject and it\n"
                + "     bakes by matching field names to column names, ignoring case.\n"
                + "② With code — for a table where a value means different things depending on another\n"
                + "     column, inherit a base such as CsvRowImporter and write the row→asset mapping yourself.\n\n"
                + "Import Quick Start from Samples in the Package Manager to see a working example.",
                "Import the Quick Start sample",
                () => UnityEditor.PackageManager.UI.Window.Open("com.toflaks.csv-pipeline"),
                ("Check the CSV root", () => CurrentTab = Tab.Settings),
                ("Rescan", () => Rescan()));
        }

        // ====================================================================================================
        // 표기
        // ====================================================================================================

        /// <summary>The icon for a change kind.</summary>
        /// <param name="kind">Kind to show.</param>
        /// <returns>What to draw.</returns>
        private static GUIContent ChangeIcon(CsvChangeKind kind)
        {
            switch (kind)
            {
                case CsvChangeKind.Create: return CsvEditorUI.IconOr("d_Toolbar Plus", "+", "Creates a new asset");
                case CsvChangeKind.Delete: return CsvEditorUI.IconOr("d_Toolbar Minus", "−", "Deletes the asset");
                case CsvChangeKind.Preserve: return CsvEditorUI.IconOr("d_AssetLock", "=", "Keeps it instead of deleting it");
                case CsvChangeKind.Skip: return CsvEditorUI.IconOr("d_winbtn_mac_min", "/", "Skips it");
                default: return CsvEditorUI.IconOr("d_Refresh", "~", "Changes values");
            }
        }

        /// <summary>The one word for a change kind.</summary>
        /// <param name="kind">Kind to show.</param>
        /// <returns>Text to show.</returns>
        private static string ChangeWord(CsvChangeKind kind)
        {
            switch (kind)
            {
                case CsvChangeKind.Create: return "Create";
                case CsvChangeKind.Delete: return "Delete";
                case CsvChangeKind.Preserve: return "Preserve";
                case CsvChangeKind.Skip: return "Skip";
                default: return "Update";
            }
        }

        /// <summary>
        /// The color for a change kind. Delete is red, and <b>preserve and skip are yellow</b> —
        /// both mean "it did not turn out the way the table says", so they have to catch the eye.
        /// </summary>
        /// <param name="kind">Kind to show.</param>
        /// <returns>Text color.</returns>
        private static Color ChangeColor(CsvChangeKind kind)
        {
            switch (kind)
            {
                case CsvChangeKind.Delete: return CsvEditorUI.Danger;
                case CsvChangeKind.Preserve:
                case CsvChangeKind.Skip: return CsvEditorUI.Warning;
                default: return CsvEditorUI.Accent;
            }
        }

        /// <summary>Selects the asset and flashes it in the Project window.</summary>
        /// <param name="path">Path of the asset to ping.</param>
        private static void Ping(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset == null) return;

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        /// <summary>
        /// The value that goes in a column. Long values are shortened, but <b>the full value goes into the
        /// tooltip</b> — shorten it with no way to see the rest and the preview answers only half the question.
        /// </summary>
        /// <param name="value">Value to show.</param>
        /// <param name="width">Width of the column.</param>
        /// <returns>What to draw.</returns>
        private static GUIContent Value(string value, float width)
        {
            if (string.IsNullOrEmpty(value)) return new GUIContent("(empty)", "This cell is empty");

            string flat = value.Replace('\n', ' ');
            int max = Mathf.Max(6, Mathf.FloorToInt(width / 7f));

            return flat.Length <= max
                ? new GUIContent(flat, value)
                : new GUIContent(flat.Substring(0, max - 1) + "…", value);
        }
    }
}
