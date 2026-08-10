using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// 표·시트 연동·설정을 한 화면에서 다루는 창입니다.
    /// 메뉴 항목으로 흩어 놓으면 지금 무엇이 어떤 상태인지 한눈에 볼 자리가 없습니다.
    /// <b>이 창은 사람이 버튼을 누를 때만 씁니다.</b> 열어 두는 것만으로는 아무것도 바뀌지 않습니다.
    /// </summary>
    public sealed class CsvPipelineWindow : EditorWindow
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

        private static readonly string[] TabLabels = { "표", "시트 연동", "설정" };

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly HashSet<string> _expanded = new HashSet<string>();

        private Tab _tab;
        private Vector2 _scroll;
        private string _search = string.Empty;
        private bool _onlyChanged = true;
        private bool _scanned;

        /// <summary>창을 엽니다.</summary>
        [MenuItem("Tools/CSV Pipeline/CSV 파이프라인 창", false, 0)]
        public static void Open()
        {
            var window = GetWindow<CsvPipelineWindow>();
            window.titleContent = new GUIContent("CSV Pipeline");
            window.minSize = new Vector2(560, 360);
            window._scanned = false;
        }

        /// <summary>창이 열릴 때 다시 훑도록 표시합니다.</summary>
        private void OnEnable() => _scanned = false;

        /// <summary>창을 그립니다.</summary>
        private void OnGUI()
        {
            DrawTabs();

            switch (_tab)
            {
                case Tab.Tables: DrawTables(); break;
                case Tab.Sheets: DrawSheets(); break;
                default: DrawSettings(); break;
            }
        }

        /// <summary>갈래를 고르는 줄입니다.</summary>
        private void DrawTabs()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                for (int i = 0; i < TabLabels.Length; i++)
                {
                    bool on = (int)_tab == i;
                    if (GUILayout.Toggle(on, TabLabels[i], EditorStyles.toolbarButton, GUILayout.Width(80)) && !on)
                    {
                        _tab = (Tab)i;
                        GUI.FocusControl(null);
                    }
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label("이 창은 누르기 전까지 아무것도 쓰지 않습니다", EditorStyles.miniLabel);
            }
        }

        // ====================================================================================================
        // 표
        // ====================================================================================================

        /// <summary>등록된 모든 표의 계획을 다시 계산합니다.</summary>
        private void Rescan()
        {
            _entries.Clear();
            _scanned = true;

            var definitions = new List<CsvImportDefinition>(CsvImportDefinition.DiscoverAll());
            foreach (CsvSchema schema in CsvSchema.All()) definitions.Add(new CsvSchemaImportDefinition(schema));

            try
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("CSV 파이프라인", "표를 훑는 중…",
                                                     (float)i / Mathf.Max(1, definitions.Count));

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
                EditorUtility.ClearProgressBar();
            }

            _entries.Sort((a, b) => string.CompareOrdinal(a.Plan.FileName, b.Plan.FileName));
        }

        /// <summary>표 갈래를 그립니다.</summary>
        private void DrawTables()
        {
            if (!_scanned) Rescan();

            if (_entries.Count == 0)
            {
                DrawGettingStarted();
                return;
            }

            DrawTableToolbar();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            int shown = 0;
            foreach (Entry entry in _entries)
            {
                if (!Matches(entry)) continue;

                DrawEntry(entry);
                shown++;
            }

            if (shown == 0)
            {
                EditorGUILayout.HelpBox(
                    _onlyChanged
                        ? "바뀌는 표가 없습니다. 모든 산출물이 표와 일치합니다."
                        : "조건에 맞는 표가 없습니다.",
                    MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>표 갈래의 도구 줄과 요약입니다.</summary>
        private void DrawTableToolbar()
        {
            int changed = 0, problems = 0;
            foreach (Entry entry in _entries)
            {
                if (!entry.Plan.IsSupported || !entry.Plan.IsNoOp) changed++;
                if (entry.Plan.Issues.Count > 0) problems++;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("다시 훑기", EditorStyles.toolbarButton, GUILayout.Width(70))) Rescan();

                _onlyChanged = GUILayout.Toggle(_onlyChanged, "바뀌는 것만",
                                                EditorStyles.toolbarButton, GUILayout.Width(80));

                GUILayout.Space(6);
                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120));

                GUILayout.FlexibleSpace();
                GUILayout.Label($"표 {_entries.Count} · 바뀜 {changed} · 문제 {problems}", EditorStyles.miniLabel);
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("전체 다시 굽기", EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    CsvRebuildMenu.RebuildAllMenu();
                    Rescan();
                }

                if (GUILayout.Button("에셋을 표로 내보내기", EditorStyles.toolbarButton, GUILayout.Width(130)))
                {
                    CsvExporter.ExportAllMenu();
                    Rescan();
                }
            }
        }

        /// <summary>검색어와 필터에 걸리는지 봅니다.</summary>
        /// <param name="entry">검사할 항목입니다.</param>
        /// <returns>보여 줘야 하면 true입니다.</returns>
        private bool Matches(Entry entry)
        {
            if (_onlyChanged && entry.Plan.IsSupported && entry.Plan.IsNoOp && entry.Plan.Issues.Count == 0)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(_search)) return true;

            return entry.Plan.FileName.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0
                || entry.Plan.Label.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>표 한 줄을 그립니다.</summary>
        /// <param name="entry">그릴 항목입니다.</param>
        private void DrawEntry(Entry entry)
        {
            CsvImportPlan plan = entry.Plan;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool open = _expanded.Contains(plan.FileName);

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool next = EditorGUILayout.Foldout(open, $"{plan.Label}    {plan.FileName}", true);
                    if (next != open)
                    {
                        if (next) _expanded.Add(plan.FileName);
                        else _expanded.Remove(plan.FileName);
                    }

                    GUILayout.FlexibleSpace();
                    DrawSummaryLabel(plan);
                }

                foreach (CsvIssue issue in plan.Issues)
                {
                    string where = issue.Where;
                    EditorGUILayout.HelpBox(
                        string.IsNullOrEmpty(where) ? issue.Message : $"{where} — {issue.Message}",
                        issue.Severity == CsvIssueSeverity.Error ? MessageType.Error : MessageType.Warning);
                }

                if (!_expanded.Contains(plan.FileName)) return;

                if (!plan.IsSupported) EditorGUILayout.HelpBox(plan.Unsupported, MessageType.None);

                EditorGUI.indentLevel++;
                foreach (CsvPlannedChange change in plan.Changes) DrawChange(change);
                EditorGUI.indentLevel--;

                DrawEntryActions(entry);
            }
        }

        /// <summary>요약을 상태에 맞는 색으로 씁니다.</summary>
        /// <param name="plan">그릴 계획입니다.</param>
        private static void DrawSummaryLabel(CsvImportPlan plan)
        {
            Color previous = GUI.contentColor;

            if (!plan.IsSupported || plan.Count(CsvChangeKind.Delete) > 0) GUI.contentColor = new Color(1f, 0.6f, 0.4f);
            else if (!plan.IsNoOp) GUI.contentColor = new Color(0.6f, 0.85f, 1f);

            GUILayout.Label(plan.Summary(), EditorStyles.miniLabel);
            GUI.contentColor = previous;
        }

        /// <summary>펼친 표에서 할 수 있는 동작들입니다.</summary>
        /// <param name="entry">대상 항목입니다.</param>
        private void DrawEntryActions(Entry entry)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(entry.CsvPath == null))
                {
                    if (GUILayout.Button("표 선택", GUILayout.Width(70)))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<Object>(entry.CsvPath);
                        if (asset != null)
                        {
                            Selection.activeObject = asset;
                            EditorGUIUtility.PingObject(asset);
                        }
                    }

                    if (GUILayout.Button("지금 굽기", GUILayout.Width(80)))
                    {
                        entry.Definition.Run(entry.CsvPath);
                        Rescan();
                        GUIUtility.ExitGUI();   // 목록이 바뀌었으므로 이번 프레임 그리기를 멈춥니다.
                    }
                }
            }
        }

        /// <summary>변경 한 줄을 그립니다.</summary>
        /// <param name="change">그릴 변경입니다.</param>
        private static void DrawChange(CsvPlannedChange change)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(Mark(change.Kind), GUILayout.Width(60));
                GUILayout.Label(change.DisplayName, GUILayout.Width(180));

                if (change.Line > 0) GUILayout.Label($"{change.Line}행", EditorStyles.miniLabel, GUILayout.Width(48));
                if (!string.IsNullOrEmpty(change.Note)) GUILayout.Label(change.Note, EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();

                if (!string.IsNullOrEmpty(change.AssetPath) && GUILayout.Button("찾기", GUILayout.Width(44)))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(change.AssetPath);
                    if (asset != null)
                    {
                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                    }
                }
            }

            if (change.Fields.Count == 0) return;

            EditorGUI.indentLevel++;
            foreach (CsvFieldChange field in change.Fields)
            {
                EditorGUILayout.LabelField(field.Column, $"{Shorten(field.From)}   →   {Shorten(field.To)}");
            }
            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// 표가 하나도 없을 때의 안내입니다. 설치 직후 이 창을 열면 여기부터 보게 되므로,
        /// "없습니다"로 끝내지 않고 무엇을 해야 하는지까지 적습니다.
        /// </summary>
        private void DrawGettingStarted()
        {
            CsvPipelineSettings settings = CsvPipelineSettings.Instance;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("아직 굽는 표가 없습니다", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"지금 보고 있는 CSV 루트: {settings.CsvRootFolder}", EditorStyles.miniLabel);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "표를 붙이는 길은 둘입니다.\n\n"
                + "① 코드 없이 — ScriptableObject에 [CsvAsset(\"표이름.csv\", \"Id열\")] 을 붙입니다.\n"
                + "   필드 이름과 열 이름을 대소문자 무시로 맞춥니다.\n\n"
                + "② 코드로 — 값의 의미가 다른 열에 따라 달라지는 표는 CsvRowImporter 같은\n"
                + "   베이스를 상속해 행→에셋 매핑을 직접 적습니다.",
                MessageType.None);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("설정 갈래로", GUILayout.Width(110))) _tab = Tab.Settings;

                if (GUILayout.Button("Package Manager 열기 (샘플)", GUILayout.Width(200)))
                {
                    UnityEditor.PackageManager.UI.Window.Open("com.toflaks.csv-pipeline");
                }

                if (GUILayout.Button("다시 훑기", GUILayout.Width(80))) Rescan();
            }

            EditorGUILayout.LabelField(
                "Package Manager의 Samples에서 Quick Start를 가져오면 동작하는 예제를 볼 수 있습니다.",
                EditorStyles.miniLabel);
        }

        // ====================================================================================================
        // 시트 연동
        // ====================================================================================================

        /// <summary>시트 연동 갈래를 그립니다.</summary>
        private void DrawSheets()
        {
            List<GoogleSheetSyncSettings> all = GoogleSheetSync.FindAll();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("전부 받기", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    GoogleSheetSync.PullAllMenu();
                }

                if (GUILayout.Button("전부 비교만", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    GoogleSheetSync.CompareAllMenu();
                }

                if (GUILayout.Button("설정 만들기", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    GoogleSheetSync.CreateMissingSettingsMenu();
                }

                if (GUILayout.Button("설정 폴더", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    GoogleSheetSync.SelectSettingsFolderMenu();
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label($"설정 {all.Count}개", EditorStyles.miniLabel);
            }

            if (all.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "연동 설정이 없습니다. 시트에서 저작하려면 위의 '설정 만들기'로 표마다 설정을 만든 뒤,\n"
                    + "각 에셋에 브라우저 주소를 붙여넣고 Enabled를 켜십시오.\n\n"
                    + "시트를 쓰지 않는다면 이 갈래는 비워 두어도 됩니다. 매 프레임 도는 것도 없습니다.",
                    MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (GoogleSheetSyncSettings settings in all) DrawSheetRow(settings);

            EditorGUILayout.EndScrollView();
        }

        /// <summary>연동 설정 한 줄을 그립니다.</summary>
        /// <param name="settings">그릴 설정입니다.</param>
        private static void DrawSheetRow(GoogleSheetSyncSettings settings)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(string.IsNullOrEmpty(settings.csvFileName) ? "(대상 없음)" : settings.csvFileName,
                                GUILayout.Width(200));

                Color previous = GUI.contentColor;
                if (!settings.IsConfigured) GUI.contentColor = new Color(1f, 0.6f, 0.4f);
                else if (!settings.enabled) GUI.contentColor = Color.gray;

                GUILayout.Label(SheetStatus(settings), EditorStyles.miniLabel, GUILayout.Width(120));
                GUI.contentColor = previous;

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!settings.IsConfigured))
                {
                    if (GUILayout.Button("비교", GUILayout.Width(50))) GoogleSheetSync.CompareOne(settings);
                    if (GUILayout.Button("받기", GUILayout.Width(50))) GoogleSheetSync.PullOne(settings);
                }

                if (GUILayout.Button("선택", GUILayout.Width(50)))
                {
                    Selection.activeObject = settings;
                    EditorGUIUtility.PingObject(settings);
                }
            }
        }

        /// <summary>연동 설정의 상태를 한 마디로 씁니다.</summary>
        /// <param name="settings">대상 설정입니다.</param>
        /// <returns>상태 문자열입니다.</returns>
        private static string SheetStatus(GoogleSheetSyncSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.sheetUrl)) return "링크 없음";
            if (!settings.IsConfigured) return "주소 확인 필요";
            if (!settings.enabled) return "꺼짐";
            return settings.autoPull ? "자동 받기" : "준비됨";
        }

        // ====================================================================================================
        // 설정
        // ====================================================================================================

        /// <summary>설정 갈래를 그립니다. 값은 Project Settings에서 고칩니다.</summary>
        private void DrawSettings()
        {
            CsvPipelineSettings settings = CsvPipelineSettings.Instance;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("실제 적용되는 경로", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("CSV 루트", settings.CsvRootFolder);
                EditorGUILayout.TextField("시트 연동 설정", settings.SheetSyncSettingsFolder);
                EditorGUILayout.TextField("스냅숏", settings.SnapshotFolder);
                EditorGUILayout.TextField("서비스 계정 키",
                    string.IsNullOrEmpty(settings.ServiceAccountKeyPath) ? "(비움 — 공개 시트만)" : settings.ServiceAccountKeyPath);
            }

            EditorGUILayout.Space();

            if (!CsvPipelineSettings.ExistsInProject)
            {
                EditorGUILayout.HelpBox(
                    "설정 에셋이 없어 기본값으로 동작합니다. 표가 다른 폴더에 있으면 지정하십시오.",
                    MessageType.Info);
            }

            if (GUILayout.Button("Project Settings 에서 고치기", GUILayout.Width(220)))
            {
                SettingsService.OpenProjectSettings("Project/CSV Pipeline");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("알아 둘 것", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "임포트는 Ctrl+Z 로 되돌릴 수 없습니다. 한 번의 임포트가 필드 수정과 에셋 생성·삭제를\n"
                + "함께 하는데 Unity의 Undo는 필드 수정만 되돌리기 때문입니다. 절반만 되돌아가면\n"
                + "되돌아간 줄 알고 넘어가게 되어 더 나쁩니다.\n\n"
                + "대신 '표' 갈래에서 무엇이 달라지는지 먼저 확인하고, 산출물은 git으로 되돌리십시오.",
                MessageType.None);
        }

        // ====================================================================================================
        // 표기
        // ====================================================================================================

        /// <summary>변경 종류의 짧은 표기입니다.</summary>
        /// <param name="kind">표기할 종류입니다.</param>
        /// <returns>표기 문자열입니다.</returns>
        private static string Mark(CsvChangeKind kind)
        {
            switch (kind)
            {
                case CsvChangeKind.Create: return "＋ 생성";
                case CsvChangeKind.Update: return "· 갱신";
                case CsvChangeKind.Delete: return "－ 삭제";
                case CsvChangeKind.Preserve: return "◦ 보존";
                default: return "／ 건너뜀";
            }
        }

        /// <summary>값이 길면 줄여 보여 줍니다. 빈 값은 눈에 보이게 표기합니다.</summary>
        /// <param name="value">표시할 값입니다.</param>
        /// <returns>표시 문자열입니다.</returns>
        private static string Shorten(string value)
        {
            if (string.IsNullOrEmpty(value)) return "(빈 값)";

            string flat = value.Replace('\n', ' ');
            return flat.Length <= 48 ? flat : flat.Substring(0, 47) + "…";
        }
    }
}
