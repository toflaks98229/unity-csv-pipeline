using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>표 갈래입니다. 표마다 지금 구우면 무엇이 달라지는지를 보여 줍니다.</summary>
    public sealed partial class CsvPipelineWindow
    {
        /// <summary>표 하나에 대해 창이 들고 있는 것입니다.</summary>
        private sealed class Entry
        {
            /// <summary>이 표를 굽는 임포터입니다.</summary>
            public CsvImportDefinition Definition;

            /// <summary>지금 구우면 무엇이 달라지는지입니다.</summary>
            public CsvImportPlan Plan;

            /// <summary>원본 표의 경로입니다. 찾지 못했으면 null입니다.</summary>
            public string CsvPath;
        }

        /// <summary>진행 막대를 띄우기 시작하는 표 개수입니다. 몇 장뿐이면 막대가 깜빡이기만 합니다.</summary>
        private const int ProgressThreshold = 12;

        private readonly List<Entry> _entries = new List<Entry>();

        /// <summary>
        /// 지금 화면에 걸리는 항목들입니다. <b>매 그리기마다 새로 만들지 않습니다</b> —
        /// 그리기는 마우스가 움직이는 동안에도 계속 돌기 때문입니다.
        /// </summary>
        private readonly List<Entry> _visible = new List<Entry>();

        private bool _scanned;
        private Vector2 _tableScroll;
        private SearchField _searchField;

        /// <summary>마우스가 올라가 있는 줄입니다. 없으면 -1입니다.</summary>
        private int _hoverIndex = -1;

        /// <summary>고른 줄을 화면 안으로 끌어와야 하는지 여부입니다.</summary>
        private bool _scrollToSelection;

        /// <summary>마지막으로 훑은 시각입니다. 지금 보는 것이 언제 것인지 알리는 데 씁니다.</summary>
        private string ScannedAt
        {
            get => SessionState.GetString(StateKey + "ScannedAt", string.Empty);
            set => SessionState.SetString(StateKey + "ScannedAt", value ?? string.Empty);
        }

        /// <summary>검색어입니다. 도메인이 다시 실려도 남습니다.</summary>
        private string Search
        {
            get => SessionState.GetString(StateKey + "Search", string.Empty);
            set => SessionState.SetString(StateKey + "Search", value ?? string.Empty);
        }

        /// <summary>지금 고른 보기입니다.</summary>
        private CsvTableView View
        {
            get => (CsvTableView)SessionState.GetInt(StateKey + "View", (int)CsvTableView.Changed);
            set => SessionState.SetInt(StateKey + "View", (int)value);
        }

        /// <summary>
        /// 키보드로 고른 표의 파일 이름입니다.
        /// <b>자리(번호)가 아니라 이름으로 들고 있습니다.</b> 거르기나 훑기로 목록이 바뀌어도
        /// 골라 둔 것이 엉뚱한 표로 옮겨 가지 않게 하려는 것입니다.
        /// </summary>
        private string SelectedFile
        {
            get => SessionState.GetString(StateKey + "Selected", string.Empty);
            set => SessionState.SetString(StateKey + "Selected", value ?? string.Empty);
        }

        /// <summary>표 하나가 펼쳐져 있는지 여부입니다.</summary>
        /// <param name="fileName">표 파일 이름입니다.</param>
        /// <returns>펼쳐져 있으면 true입니다.</returns>
        private static bool IsExpanded(string fileName)
            => SessionState.GetBool(StateKey + "Open." + fileName, false);

        /// <summary>표 하나의 펼침을 정합니다.</summary>
        /// <param name="fileName">표 파일 이름입니다.</param>
        /// <param name="open">펼칠지 여부입니다.</param>
        private static void SetExpanded(string fileName, bool open)
            => SessionState.SetBool(StateKey + "Open." + fileName, open);

        // ====================================================================================================
        // 훑기
        // ====================================================================================================

        /// <summary>
        /// 등록된 모든 표의 계획을 다시 계산합니다.
        /// </summary>
        /// <param name="interactive">
        /// 사람이 눌러서 부른 것인지 여부입니다. 그릴 때 저절로 부르는 첫 훑기에서는 <b>진행 막대를 띄우지
        /// 않습니다.</b> OnGUI 한가운데에서 막대를 띄우면 Layout 과 Repaint 가 서로 다른 것을 그리게 됩니다.
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
                        EditorUtility.DisplayProgressBar("CSV 파이프라인", "표를 훑는 중…",
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
        /// 손볼 것이 있는 표를 위로 올립니다. 이름순으로만 늘어놓으면 정작 볼 것이 아래에 묻힙니다.
        /// </summary>
        /// <param name="a">비교할 항목입니다.</param>
        /// <param name="b">비교할 항목입니다.</param>
        /// <returns>정렬 순서입니다.</returns>
        private static int CompareEntries(Entry a, Entry b)
        {
            int byState = CsvPlanStatus.Of(a.Plan).CompareTo(CsvPlanStatus.Of(b.Plan));
            if (byState != 0) return -byState;   // 값이 큰 상태(문제·삭제)가 위로

            return string.CompareOrdinal(a.Plan.FileName, b.Plan.FileName);
        }

        /// <summary>상태를 아이콘과 설명으로 옮깁니다.</summary>
        /// <param name="state">옮길 상태입니다.</param>
        /// <returns>줄 앞에 그릴 내용입니다.</returns>
        private static GUIContent StateIcon(CsvPlanState state)
        {
            switch (state)
            {
                case CsvPlanState.Problem: return CsvEditorUI.IconOr("console.erroricon.sml", "!", "문제가 있습니다");
                case CsvPlanState.Blocked: return CsvEditorUI.IconOr("console.warnicon.sml", "?", "계획을 세우지 못했습니다");
                case CsvPlanState.Removing: return CsvEditorUI.IconOr("console.warnicon.sml", "−", "사라지는 산출물이 있습니다");
                case CsvPlanState.Changed: return CsvEditorUI.IconOr("d_Refresh", "~", "바뀌는 것이 있습니다");
                default: return CsvEditorUI.IconOr("TestPassed", "·", "표와 산출물이 같습니다");
            }
        }

        /// <summary>상태 바에 쓸 표 갈래 요약입니다.</summary>
        /// <returns>요약 문자열입니다.</returns>
        private string TablesStatus()
        {
            if (!_scanned) return "훑는 중…";

            int changed = 0, problems = 0;
            foreach (Entry entry in _entries)
            {
                CsvPlanState state = CsvPlanStatus.Of(entry.Plan);
                if (CsvPlanStatus.NeedsAttention(state)) problems++;
                else if (state != CsvPlanState.Ok) changed++;
            }

            string text = problems > 0
                ? $"표 {_entries.Count} · 바뀜 {changed} · 문제 {problems}"
                : $"표 {_entries.Count} · 바뀜 {changed}";

            // 지금 보는 것이 언제 것인지 모르면, 밖에서 표를 고친 뒤에도 낡은 화면을 믿게 됩니다.
            string at = ScannedAt;
            return string.IsNullOrEmpty(at) ? text : $"{text}   ·   {at} 기준";
        }

        // ====================================================================================================
        // 그리기
        // ====================================================================================================

        /// <summary>표 갈래를 그립니다.</summary>
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

        /// <summary>지금 걸리는 항목만 모읍니다.</summary>
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

        /// <summary>표 갈래의 도구 줄입니다. 왼쪽은 보기, 오른쪽은 전체에 미치는 행동입니다.</summary>
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
                        CsvEditorUI.IconAnd("d_Refresh", " 다시 훑기", "표를 모두 다시 읽어 계획을 새로 세웁니다"),
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
                if (GUILayout.Button(CsvEditorUI.IconOr("_Popup", "⋯", "펼치기·전체 다시 굽기·내보내기"),
                                     EditorStyles.toolbarButton, GUILayout.Width(28)))
                {
                    ShowTableActionsMenu();
                }
            }
        }

        /// <summary>무엇을 보여 줄지 고르는 내림 단추입니다.</summary>
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

        /// <summary>전체에 미치는 행동들입니다. 되돌릴 수 없는 것은 한 번 더 묻습니다.</summary>
        private void ShowTableActionsMenu()
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("모두 펼치기"), false, () => SetAllExpanded(true));
            menu.AddItem(new GUIContent("모두 접기"), false, () => SetAllExpanded(false));
            menu.AddSeparator(string.Empty);

            menu.AddItem(new GUIContent("전체 다시 굽기"), false, () =>
            {
                if (!EditorUtility.DisplayDialog(
                        "전체 다시 굽기",
                        $"등록된 표 {_entries.Count}장을 모두 다시 굽습니다.\n\n"
                        + "표에서 사라진 산출물은 참조가 없으면 삭제됩니다.\n"
                        + "이 작업은 Ctrl+Z 로 되돌릴 수 없습니다.",
                        "굽기", "취소"))
                {
                    return;
                }

                CsvRebuildMenu.RebuildAllMenu();
                Rescan();
            });

            menu.AddItem(new GUIContent("에셋을 표로 내보내기"), false, () =>
            {
                if (!EditorUtility.DisplayDialog(
                        "에셋을 표로 내보내기",
                        "산출물 에셋의 지금 값으로 원본 표 파일을 덮어씁니다.\n\n"
                        + "표 쪽에만 있던 수정은 사라집니다. 시트 연동을 켠 표라면\n"
                        + "시트와 어긋나게 되므로 먼저 '전부 비교만'으로 확인하십시오.",
                        "내보내기", "취소"))
                {
                    return;
                }

                CsvExporter.ExportAllMenu();
                Rescan();
            });

            menu.ShowAsContext();
        }

        /// <summary>지금 보이는 표를 모두 펼치거나 접습니다.</summary>
        /// <param name="open">펼칠지 여부입니다.</param>
        private void SetAllExpanded(bool open)
        {
            foreach (Entry entry in _visible) SetExpanded(entry.Plan.FileName, open);
            Repaint();
        }

        // ====================================================================================================
        // 키보드
        // ====================================================================================================

        /// <summary>
        /// 키 하나를 받아 목록을 움직입니다.
        /// <para>
        /// Unity Editor Design System 은 모든 화면이 <b>마우스 없이 키보드만으로</b> 닿을 수 있어야
        /// 한다고 요구합니다(US-0180). 어느 키가 무슨 뜻인지는 <see cref="CsvListKeys"/>가 정하고,
        /// 여기서는 그 뜻을 창에 적용하기만 합니다.
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

        /// <summary>고른 표가 지금 목록의 몇 번째인지입니다. 없으면 -1입니다.</summary>
        /// <returns>자리 번호입니다.</returns>
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

        /// <summary>그 자리의 표를 고릅니다.</summary>
        /// <param name="index">고를 자리입니다.</param>
        private void Select(int index)
        {
            if (index < 0 || index >= _visible.Count) return;

            SelectedFile = _visible[index].Plan.FileName;
            _scrollToSelection = true;
        }

        /// <summary>표 하나를 굽고 목록을 다시 훑습니다.</summary>
        /// <param name="entry">구울 항목입니다.</param>
        private void BakeOne(Entry entry)
        {
            if (entry.CsvPath == null || entry.Plan.IsNoOp) return;

            entry.Definition.Run(entry.CsvPath);
            Rescan();
            GUIUtility.ExitGUI();   // 목록이 바뀌었으므로 이번 프레임 그리기를 멈춥니다.
        }

        // ====================================================================================================
        // 거르기
        // ====================================================================================================

        /// <summary>걸리는 표가 없을 때의 안내입니다. 어느 조건이 걸렀는지까지 말합니다.</summary>
        private void DrawNothingMatched()
        {
            bool searching = !string.IsNullOrWhiteSpace(Search);
            CsvTableView view = View;
            bool hiding = view != CsvTableView.All;

            if (searching)
            {
                // 걸러 낸 조건이 둘일 수 있습니다. 어느 쪽이 범인인지 모르면 사람이 검색어만 의심합니다.
                (string, Action)[] extras = hiding
                    ? new (string, Action)[] { ("전부 보기", () => View = CsvTableView.All) }
                    : Array.Empty<(string, Action)>();

                CsvEditorUI.EmptyState(
                    $"'{Search}' 에 걸리는 표가 없습니다",
                    hiding
                        ? $"'{CsvTableFilter.Label(view)}' 로 보고 있어 {CsvTableFilter.Describe(view)}"
                        : "표 파일 이름·산출물 타입 이름·산출물 폴더로 찾습니다.",
                    "검색어 지우기", () => Search = string.Empty,
                    extras);
                return;
            }

            CsvEditorUI.EmptyState(
                view == CsvTableView.Problems ? "손볼 표가 없습니다" : "바뀌는 표가 없습니다",
                view == CsvTableView.Problems
                    ? "문제가 있거나 계획을 세우지 못한 표가 없습니다."
                    : "모든 산출물이 표와 일치합니다. 지금 구워도 달라지는 것이 없습니다.",
                "전부 보기", () => View = CsvTableView.All);
        }

        // ====================================================================================================
        // 표 한 장
        // ====================================================================================================

        /// <summary>표 한 장을 그립니다.</summary>
        /// <param name="entry">그릴 항목입니다.</param>
        /// <param name="index">화면에 보이는 순번입니다. 바탕을 번갈아 까는 데 씁니다.</param>
        /// <returns>마우스가 이 줄 위에 있으면 true입니다.</returns>
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
        /// 표 한 장의 머리 줄입니다. <b>줄 전체가 펼침 단추</b>라 삼각형을 정확히 겨눌 필요가 없습니다.
        /// </summary>
        /// <param name="entry">그릴 항목입니다.</param>
        /// <param name="index">화면에 보이는 순번입니다.</param>
        /// <param name="open">펼쳐져 있는지 여부입니다.</param>
        /// <param name="clicked">눌렸으면 true를 받습니다.</param>
        /// <returns>마우스가 이 줄 위에 있으면 true입니다.</returns>
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

        /// <summary>머리 줄 위의 마우스를 다룹니다.</summary>
        /// <param name="entry">이 줄의 항목입니다.</param>
        /// <param name="row">줄의 자리입니다.</param>
        /// <returns>펼침을 뒤집어야 하면 true입니다.</returns>
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

        /// <summary>줄에서 오른쪽 단추를 눌렀을 때의 차림표입니다.</summary>
        /// <param name="entry">대상 항목입니다.</param>
        private void ShowEntryMenu(Entry entry)
        {
            var menu = new GenericMenu();

            AddMenuItem(menu, "지금 굽기", entry.CsvPath != null && !entry.Plan.IsNoOp, () => BakeOne(entry));
            menu.AddSeparator(string.Empty);
            AddMenuItem(menu, "표 열기", entry.CsvPath != null, () => Ping(entry.CsvPath));
            AddMenuItem(menu, "산출물 폴더 열기", !string.IsNullOrEmpty(entry.Plan.OutputFolder),
                        () => Ping(entry.Plan.OutputFolder));
            menu.AddSeparator(string.Empty);
            AddMenuItem(menu, "표 경로 복사", entry.CsvPath != null,
                        () => EditorGUIUtility.systemCopyBuffer = entry.CsvPath);

            menu.ShowAsContext();
        }

        /// <summary>
        /// 할 수 없는 항목은 <b>감추지 않고 흐리게</b> 둡니다. 감추면 차림표의 모양이 줄마다 달라져,
        /// 손이 기억한 자리가 매번 어긋납니다.
        /// </summary>
        /// <param name="menu">채울 차림표입니다.</param>
        /// <param name="label">항목 이름입니다.</param>
        /// <param name="enabled">고를 수 있는지 여부입니다.</param>
        /// <param name="action">고르면 할 일입니다.</param>
        private static void AddMenuItem(GenericMenu menu, string label, bool enabled, GenericMenu.MenuFunction action)
        {
            if (enabled) menu.AddItem(new GUIContent(label), false, action);
            else menu.AddDisabledItem(new GUIContent(label));
        }

        /// <summary>상태의 색입니다. 색만으로 뜻을 나르지 않도록 아이콘·낱말과 함께 씁니다.</summary>
        /// <param name="state">줄의 상태입니다.</param>
        /// <returns>글자색입니다.</returns>
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

        /// <summary>펼친 표에서 할 수 있는 동작들입니다.</summary>
        /// <param name="entry">대상 항목입니다.</param>
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
                            CsvEditorUI.IconAnd("d_TextAsset Icon", " 표 열기",
                                                hasCsv ? "원본 표를 프로젝트 창에서 찾습니다"
                                                       : "원본 표 파일을 찾지 못했습니다. CSV 루트 설정을 확인하십시오"),
                            EditorStyles.miniButton, GUILayout.Width(88)))
                    {
                        Ping(entry.CsvPath);
                    }
                }

                bool hasFolder = !string.IsNullOrEmpty(entry.Plan.OutputFolder);
                using (new EditorGUI.DisabledScope(!hasFolder))
                {
                    if (GUILayout.Button(
                            CsvEditorUI.IconAnd("Folder Icon", " 산출물 폴더",
                                                hasFolder ? "구워진 에셋이 놓이는 폴더를 찾습니다"
                                                          : "이 임포터는 산출물 폴더를 알려 주지 않습니다"),
                            EditorStyles.miniButton, GUILayout.Width(100)))
                    {
                        Ping(entry.Plan.OutputFolder);
                    }
                }

                GUILayout.FlexibleSpace();

                bool canBake = hasCsv && !entry.Plan.IsNoOp;
                using (new EditorGUI.DisabledScope(!canBake))
                {
                    string why = !hasCsv ? "원본 표 파일을 찾지 못했습니다"
                               : entry.Plan.IsNoOp ? "지금 구워도 달라지는 것이 없습니다"
                               : "이 표만 지금 굽습니다. Ctrl+Z 로 되돌릴 수 없습니다";

                    if (GUILayout.Button(new GUIContent("지금 굽기", why), GUILayout.Width(84)))
                    {
                        BakeOne(entry);
                    }
                }

                GUILayout.Space(CsvEditorUI.Gap);
            }
        }

        /// <summary>변경 한 줄을 그립니다.</summary>
        /// <param name="change">그릴 변경입니다.</param>
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
                    CsvEditorUI.ColoredLabel(new GUIContent($"{change.Line}행", "원본 표에서의 줄 번호입니다"),
                                             CsvEditorUI.Muted, EditorStyles.miniLabel, GUILayout.Width(44));
                }

                if (!string.IsNullOrEmpty(change.Note))
                {
                    CsvEditorUI.ColoredLabel(change.Note, CsvEditorUI.Muted);
                }

                GUILayout.FlexibleSpace();

                if (!string.IsNullOrEmpty(change.AssetPath)
                    && GUILayout.Button(new GUIContent("찾기", "이 에셋을 프로젝트 창에서 찾습니다"),
                                        EditorStyles.miniButton, GUILayout.Width(44)))
                {
                    Ping(change.AssetPath);
                }

                GUILayout.Space(CsvEditorUI.Gap);
            }

            DrawFieldChanges(change);
        }

        /// <summary>
        /// 필드 하나하나가 어떻게 바뀌는지입니다.
        /// 열 이름·이전·다음을 <b>같은 가로 위치</b>에 세워, 여러 줄을 위아래로 훑을 수 있게 합니다.
        /// 칸 너비는 창 너비를 따릅니다 — 고정 픽셀로 두면 창을 좁혔을 때 가로로 잘립니다.
        /// </summary>
        /// <param name="change">그릴 변경입니다.</param>
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

                    CsvEditorUI.ColoredLabel(new GUIContent(field.Column, $"필드 {field.Field}"),
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
        /// 표가 하나도 없을 때의 안내입니다. 설치 직후 이 창을 열면 여기부터 보게 되므로,
        /// "없습니다"로 끝내지 않고 무엇을 해야 하는지까지 적습니다.
        /// </summary>
        private void DrawGettingStarted()
        {
            CsvPipelineSettings settings = CsvPipelineSettings.Instance;

            CsvEditorUI.EmptyState(
                "아직 굽는 표가 없습니다",
                $"지금 보고 있는 CSV 루트는 {settings.CsvRootFolder} 입니다.\n\n"
                + "표를 붙이는 길은 둘입니다.\n"
                + "① 코드 없이 — ScriptableObject 에 [CsvAsset(\"표이름.csv\", \"Id열\")] 을 붙이면\n"
                + "     필드 이름과 열 이름을 대소문자 무시로 맞춰 굽습니다.\n"
                + "② 코드로 — 값의 뜻이 다른 열에 따라 달라지는 표는 CsvRowImporter 같은 베이스를\n"
                + "     상속해 행→에셋 매핑을 직접 적습니다.\n\n"
                + "Package Manager 의 Samples 에서 Quick Start 를 가져오면 동작하는 예제를 볼 수 있습니다.",
                "Quick Start 샘플 가져오기",
                () => UnityEditor.PackageManager.UI.Window.Open("com.toflaks.csv-pipeline"),
                ("CSV 루트 확인", () => CurrentTab = Tab.Settings),
                ("다시 훑기", () => Rescan()));
        }

        // ====================================================================================================
        // 표기
        // ====================================================================================================

        /// <summary>변경 종류의 아이콘입니다.</summary>
        /// <param name="kind">표기할 종류입니다.</param>
        /// <returns>그릴 내용입니다.</returns>
        private static GUIContent ChangeIcon(CsvChangeKind kind)
        {
            switch (kind)
            {
                case CsvChangeKind.Create: return CsvEditorUI.IconOr("d_Toolbar Plus", "+", "새로 만듭니다");
                case CsvChangeKind.Delete: return CsvEditorUI.IconOr("d_Toolbar Minus", "−", "지웁니다");
                case CsvChangeKind.Preserve: return CsvEditorUI.IconOr("d_AssetLock", "=", "지우지 않고 남깁니다");
                case CsvChangeKind.Skip: return CsvEditorUI.IconOr("d_winbtn_mac_min", "/", "건너뜁니다");
                default: return CsvEditorUI.IconOr("d_Refresh", "~", "값을 바꿉니다");
            }
        }

        /// <summary>변경 종류의 한 마디입니다.</summary>
        /// <param name="kind">표기할 종류입니다.</param>
        /// <returns>표기 문자열입니다.</returns>
        private static string ChangeWord(CsvChangeKind kind)
        {
            switch (kind)
            {
                case CsvChangeKind.Create: return "생성";
                case CsvChangeKind.Delete: return "삭제";
                case CsvChangeKind.Preserve: return "보존";
                case CsvChangeKind.Skip: return "건너뜀";
                default: return "갱신";
            }
        }

        /// <summary>
        /// 변경 종류의 색입니다. 삭제는 빨강, <b>보존과 건너뜀은 노랑</b>입니다 —
        /// 둘 다 "표에 적힌 대로 되지 않았다"는 뜻이라 눈에 걸려야 합니다.
        /// </summary>
        /// <param name="kind">표기할 종류입니다.</param>
        /// <returns>글자색입니다.</returns>
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

        /// <summary>에셋을 골라 프로젝트 창에서 반짝입니다.</summary>
        /// <param name="path">찾아갈 에셋 경로입니다.</param>
        private static void Ping(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset == null) return;

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        /// <summary>
        /// 칸에 들어갈 값입니다. 길면 줄이되 <b>전체 값을 툴팁에 담습니다</b> —
        /// 줄여 놓고 볼 길이 없으면 미리보기가 답을 반만 주는 셈입니다.
        /// </summary>
        /// <param name="value">표시할 값입니다.</param>
        /// <param name="width">칸의 너비입니다.</param>
        /// <returns>그릴 내용입니다.</returns>
        private static GUIContent Value(string value, float width)
        {
            if (string.IsNullOrEmpty(value)) return new GUIContent("(빈 값)", "이 셀은 비어 있습니다");

            string flat = value.Replace('\n', ' ');
            int max = Mathf.Max(6, Mathf.FloorToInt(width / 7f));

            return flat.Length <= max
                ? new GUIContent(flat, value)
                : new GUIContent(flat.Substring(0, max - 1) + "…", value);
        }
    }
}
