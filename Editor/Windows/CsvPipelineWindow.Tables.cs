using System;
using System.Collections.Generic;
using UnityEditor;
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
        private bool _scanned;
        private Vector2 _tableScroll;

        /// <summary>검색어입니다. 도메인이 다시 실려도 남습니다.</summary>
        private string Search
        {
            get => SessionState.GetString(StateKey + "Search", string.Empty);
            set => SessionState.SetString(StateKey + "Search", value ?? string.Empty);
        }

        /// <summary>바뀌는 표만 보일지 여부입니다.</summary>
        private bool OnlyChanged
        {
            get => SessionState.GetBool(StateKey + "OnlyChanged", true);
            set => SessionState.SetBool(StateKey + "OnlyChanged", value);
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
                if (state == CsvPlanState.Problem || state == CsvPlanState.Blocked) problems++;
                else if (state != CsvPlanState.Ok) changed++;
            }

            return problems > 0
                ? $"표 {_entries.Count} · 바뀜 {changed} · 문제 {problems}"
                : $"표 {_entries.Count} · 바뀜 {changed}";
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

            _tableScroll = EditorGUILayout.BeginScrollView(_tableScroll);

            int shown = 0;
            foreach (Entry entry in _entries)
            {
                if (!Matches(entry)) continue;

                DrawEntry(entry, shown);
                shown++;
            }

            if (shown == 0) DrawNothingMatched();

            EditorGUILayout.EndScrollView();
        }

        /// <summary>표 갈래의 도구 줄입니다. 왼쪽은 보기, 오른쪽은 전체에 미치는 행동입니다.</summary>
        private void DrawTableToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(CsvEditorUI.IconAnd("d_Refresh", " 다시 훑기"),
                                     EditorStyles.toolbarButton, GUILayout.Width(86)))
                {
                    Rescan();
                }

                CsvEditorUI.ToolbarSeparator();

                OnlyChanged = GUILayout.Toggle(OnlyChanged, "바뀌는 것만",
                                               EditorStyles.toolbarButton, GUILayout.Width(80));

                GUILayout.Space(CsvEditorUI.GapTight);

                string next = EditorGUILayout.TextField(Search, EditorStyles.toolbarSearchField,
                                                        GUILayout.MinWidth(120));
                if (next != Search) Search = next;

                GUILayout.FlexibleSpace();

                // 표를 통째로 다시 굽거나 내보내는 것은 되돌릴 수 없어, 자주 쓰는 단추 옆에 두지 않습니다.
                if (GUILayout.Button(CsvEditorUI.IconOr("_Popup", "⋯", "전체 작업"),
                                     EditorStyles.toolbarButton, GUILayout.Width(28)))
                {
                    ShowTableActionsMenu();
                }
            }
        }

        /// <summary>전체에 미치는 행동들입니다. 되돌릴 수 없어 한 번 더 묻습니다.</summary>
        private void ShowTableActionsMenu()
        {
            var menu = new GenericMenu();

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

        /// <summary>검색어와 필터에 걸리는지 봅니다.</summary>
        /// <param name="entry">검사할 항목입니다.</param>
        /// <returns>보여 줘야 하면 true입니다.</returns>
        private bool Matches(Entry entry)
        {
            if (OnlyChanged && CsvPlanStatus.Of(entry.Plan) == CsvPlanState.Ok) return false;

            string search = Search;
            if (string.IsNullOrWhiteSpace(search)) return true;

            return entry.Plan.FileName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || entry.Plan.Label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>걸리는 표가 없을 때의 안내입니다. 어느 조건이 걸렀는지까지 말합니다.</summary>
        private void DrawNothingMatched()
        {
            bool searching = !string.IsNullOrWhiteSpace(Search);

            if (searching)
            {
                // 걸러 낸 조건이 둘일 수 있습니다. 어느 쪽이 범인인지 모르면 사람이 검색어만 의심합니다.
                (string, Action)[] extras = OnlyChanged
                    ? new (string, Action)[] { ("필터 끄기", () => OnlyChanged = false) }
                    : Array.Empty<(string, Action)>();

                CsvEditorUI.EmptyState(
                    $"'{Search}' 에 걸리는 표가 없습니다",
                    OnlyChanged
                        ? "'바뀌는 것만' 이 켜져 있어, 이미 표와 같은 산출물은 애초에 빠져 있습니다."
                        : "표 파일 이름이나 산출물 타입 이름으로 찾습니다.",
                    "검색어 지우기", () => Search = string.Empty,
                    extras);
                return;
            }

            CsvEditorUI.EmptyState(
                "바뀌는 표가 없습니다",
                "모든 산출물이 표와 일치합니다. 지금 구워도 달라지는 것이 없습니다.",
                "전부 보기", () => OnlyChanged = false);
        }

        /// <summary>표 한 장을 그립니다.</summary>
        /// <param name="entry">그릴 항목입니다.</param>
        /// <param name="index">화면에 보이는 순번입니다. 바탕을 번갈아 까는 데 씁니다.</param>
        private void DrawEntry(Entry entry, int index)
        {
            CsvImportPlan plan = entry.Plan;
            bool open = IsExpanded(plan.FileName);

            if (DrawEntryHeader(plan, index, open)) SetExpanded(plan.FileName, !open);

            if (!open) return;

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
        }

        /// <summary>
        /// 표 한 장의 머리 줄입니다. <b>줄 전체가 펼침 단추</b>라 삼각형을 정확히 겨눌 필요가 없습니다.
        /// </summary>
        /// <param name="plan">그릴 계획입니다.</param>
        /// <param name="index">화면에 보이는 순번입니다.</param>
        /// <param name="open">펼쳐져 있는지 여부입니다.</param>
        /// <returns>눌렸으면 true입니다.</returns>
        private static bool DrawEntryHeader(CsvImportPlan plan, int index, bool open)
        {
            Rect row = EditorGUILayout.BeginHorizontal(GUILayout.Height(CsvEditorUI.RowHeight));
            CsvEditorUI.RowBackground(row, index);

            CsvPlanState state = CsvPlanStatus.Of(plan);

            GUILayout.Space(CsvEditorUI.GapTight);

            // 삼각형만 정확히 그립니다. Foldout 컨트롤을 쓰면 폭이 줄에 따라 흔들려 아래 칸이 어긋납니다.
            Rect arrow = GUILayoutUtility.GetRect(14f, CsvEditorUI.RowHeight, GUILayout.Width(14f));
            if (Event.current.type == EventType.Repaint)
            {
                EditorStyles.foldout.Draw(arrow, false, false, open, false);
            }

            GUILayout.Label(StateIcon(state), GUILayout.Width(CsvEditorUI.StatusWidth),
                            GUILayout.Height(CsvEditorUI.RowHeight));

            GUILayout.Label(plan.Label, EditorStyles.boldLabel, GUILayout.MinWidth(80));
            CsvEditorUI.ColoredLabel(plan.FileName, CsvEditorUI.Muted, EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();
            CsvEditorUI.ColoredLabel(plan.Summary(), SummaryColor(state), EditorStyles.miniLabel);
            GUILayout.Space(CsvEditorUI.Gap);

            EditorGUILayout.EndHorizontal();

            return Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && row.Contains(Event.current.mousePosition)
                && ConsumeClick();
        }

        /// <summary>줄을 눌렀다는 사실을 소비해 아래로 새지 않게 합니다.</summary>
        /// <returns>언제나 true입니다.</returns>
        private static bool ConsumeClick()
        {
            Event.current.Use();
            return true;
        }

        /// <summary>요약 글자의 색입니다. 색만으로 뜻을 나르지 않도록 아이콘과 함께 씁니다.</summary>
        /// <param name="state">줄의 상태입니다.</param>
        /// <returns>글자색입니다.</returns>
        private static Color SummaryColor(CsvPlanState state)
        {
            switch (state)
            {
                case CsvPlanState.Problem:
                case CsvPlanState.Blocked:
                case CsvPlanState.Removing: return CsvEditorUI.Danger;
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

                using (new EditorGUI.DisabledScope(entry.CsvPath == null))
                {
                    if (GUILayout.Button(CsvEditorUI.IconAnd("d_TextAsset Icon", " 표 열기"),
                                         EditorStyles.miniButton, GUILayout.Width(88)))
                    {
                        Ping(entry.CsvPath);
                    }
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(entry.Plan.OutputFolder)))
                {
                    if (GUILayout.Button(CsvEditorUI.IconAnd("Folder Icon", " 산출물 폴더"),
                                         EditorStyles.miniButton, GUILayout.Width(100)))
                    {
                        Ping(entry.Plan.OutputFolder);
                    }
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(entry.CsvPath == null || entry.Plan.IsNoOp))
                {
                    if (GUILayout.Button("지금 굽기", GUILayout.Width(84)))
                    {
                        entry.Definition.Run(entry.CsvPath);
                        Rescan();
                        GUIUtility.ExitGUI();   // 목록이 바뀌었으므로 이번 프레임 그리기를 멈춥니다.
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

                GUILayout.Label(change.DisplayName, GUILayout.MinWidth(120));

                if (change.Line > 0)
                {
                    CsvEditorUI.ColoredLabel($"{change.Line}행", CsvEditorUI.Muted,
                                             EditorStyles.miniLabel, GUILayout.Width(44));
                }

                if (!string.IsNullOrEmpty(change.Note))
                {
                    CsvEditorUI.ColoredLabel(change.Note, CsvEditorUI.Muted);
                }

                GUILayout.FlexibleSpace();

                if (!string.IsNullOrEmpty(change.AssetPath)
                    && GUILayout.Button("찾기", EditorStyles.miniButton, GUILayout.Width(44)))
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
        /// </summary>
        /// <param name="change">그릴 변경입니다.</param>
        private static void DrawFieldChanges(CsvPlannedChange change)
        {
            if (change.Fields.Count == 0) return;

            foreach (CsvFieldChange field in change.Fields)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(CsvEditorUI.GapSection * 2f + CsvEditorUI.StatusWidth);

                    CsvEditorUI.ColoredLabel(field.Column, CsvEditorUI.Muted, EditorStyles.miniLabel,
                                             GUILayout.Width(110));

                    GUILayout.Label(Shorten(field.From), EditorStyles.miniLabel, GUILayout.Width(150));
                    CsvEditorUI.ColoredLabel("→", CsvEditorUI.Muted, EditorStyles.miniLabel,
                                             GUILayout.Width(16));
                    CsvEditorUI.ColoredLabel(Shorten(field.To), CsvEditorUI.Accent, EditorStyles.miniLabel);

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
                case CsvChangeKind.Preserve: return CsvEditorUI.IconOr("d_AssetLock", "=", "참조가 남아 보존합니다");
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

        /// <summary>변경 종류의 색입니다.</summary>
        /// <param name="kind">표기할 종류입니다.</param>
        /// <returns>글자색입니다.</returns>
        private static Color ChangeColor(CsvChangeKind kind)
        {
            switch (kind)
            {
                case CsvChangeKind.Delete: return CsvEditorUI.Danger;
                case CsvChangeKind.Create:
                case CsvChangeKind.Update: return CsvEditorUI.Accent;
                default: return CsvEditorUI.Muted;
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

        /// <summary>값이 길면 줄여 보여 줍니다. 빈 값은 눈에 보이게 표기합니다.</summary>
        /// <param name="value">표시할 값입니다.</param>
        /// <returns>표시 문자열입니다.</returns>
        private static string Shorten(string value)
        {
            if (string.IsNullOrEmpty(value)) return "(빈 값)";

            string flat = value.Replace('\n', ' ');
            return flat.Length <= 28 ? flat : flat.Substring(0, 27) + "…";
        }
    }
}
