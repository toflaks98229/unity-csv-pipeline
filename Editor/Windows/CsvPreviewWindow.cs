using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// 표를 굽기 전에 <b>무엇이 달라지는지</b> 보여 주는 창입니다. 아무것도 쓰지 않습니다.
    /// 이 패키지가 지키는 규칙(참조가 남은 에셋은 지우지 않는다, 빈 셀은 보존한다)은
    /// 결과를 봐야만 확인되는데, 그 결과를 적용 전에 보여 주는 것이 이 창의 목적입니다.
    /// </summary>
    public sealed class CsvPreviewWindow : EditorWindow
    {
        /// <summary>지금 화면에 올린 계획들입니다.</summary>
        private readonly List<CsvImportPlan> _plans = new List<CsvImportPlan>();

        /// <summary>펼쳐 둔 계획의 표 이름들입니다.</summary>
        private readonly HashSet<string> _expanded = new HashSet<string>();

        private Vector2 _scroll;
        private bool _onlyChanged = true;
        private bool _scanned;

        /// <summary>창을 엽니다.</summary>
        [MenuItem("Tools/CSV Pipeline/미리보기 (굽지 않고 확인)")]
        public static void Open()
        {
            var window = GetWindow<CsvPreviewWindow>();
            window.titleContent = new GUIContent("CSV 미리보기");
            window.minSize = new Vector2(520, 320);
            window.Rescan();
        }

        /// <summary>창이 열릴 때 한 번 훑습니다.</summary>
        private void OnEnable() => _scanned = false;

        /// <summary>등록된 모든 표의 계획을 다시 계산합니다.</summary>
        private void Rescan()
        {
            _plans.Clear();
            _scanned = true;

            try
            {
                EditorUtility.DisplayProgressBar("CSV 미리보기", "표를 훑는 중…", 0f);

                var definitions = new List<CsvImportDefinition>(CsvImportDefinition.DiscoverAll());
                foreach (CsvSchema schema in CsvSchema.All())
                {
                    definitions.Add(new CsvSchemaImportDefinition(schema));
                }

                for (int i = 0; i < definitions.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("CSV 미리보기", definitions[i].ToString(),
                                                     (float)i / Mathf.Max(1, definitions.Count));
                    _plans.Add(definitions[i].Plan());
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            _plans.Sort((a, b) => string.CompareOrdinal(a.FileName, b.FileName));
        }

        /// <summary>창을 그립니다.</summary>
        private void OnGUI()
        {
            if (!_scanned) Rescan();

            DrawToolbar();

            if (_plans.Count == 0)
            {
                EditorGUILayout.HelpBox("등록된 표가 없습니다.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            int shown = 0;
            foreach (CsvImportPlan plan in _plans)
            {
                if (_onlyChanged && plan.IsSupported && plan.IsNoOp && plan.Issues.Count == 0) continue;

                DrawPlan(plan);
                shown++;
            }

            if (shown == 0)
            {
                EditorGUILayout.HelpBox("바뀌는 표가 없습니다. 모든 산출물이 표와 일치합니다.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>상단 도구 줄을 그립니다.</summary>
        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("다시 훑기", EditorStyles.toolbarButton, GUILayout.Width(80))) Rescan();

                _onlyChanged = GUILayout.Toggle(_onlyChanged, "바뀌는 것만",
                                                EditorStyles.toolbarButton, GUILayout.Width(90));

                GUILayout.FlexibleSpace();
                GUILayout.Label("이 창은 아무것도 쓰지 않습니다", EditorStyles.miniLabel);
            }
        }

        /// <summary>계획 하나를 그립니다.</summary>
        /// <param name="plan">그릴 계획입니다.</param>
        private void DrawPlan(CsvImportPlan plan)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool open = _expanded.Contains(plan.FileName);
                string title = $"{plan.Label}  ·  {plan.FileName}";

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool next = EditorGUILayout.Foldout(open, title, true);
                    if (next != open)
                    {
                        if (next) _expanded.Add(plan.FileName);
                        else _expanded.Remove(plan.FileName);
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.Label(plan.Summary(), EditorStyles.miniLabel);
                }

                if (!plan.IsSupported)
                {
                    EditorGUILayout.HelpBox(plan.Unsupported, MessageType.None);
                }

                foreach (CsvIssue issue in plan.Issues)
                {
                    string where = issue.Where;
                    EditorGUILayout.HelpBox(
                        string.IsNullOrEmpty(where) ? issue.Message : $"{where} — {issue.Message}",
                        issue.Severity == CsvIssueSeverity.Error ? MessageType.Error : MessageType.Warning);
                }

                if (!_expanded.Contains(plan.FileName)) return;

                EditorGUI.indentLevel++;
                foreach (CsvPlannedChange change in plan.Changes) DrawChange(change);
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>변경 한 줄을 그립니다.</summary>
        /// <param name="change">그릴 변경입니다.</param>
        private static void DrawChange(CsvPlannedChange change)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(Mark(change.Kind), GUILayout.Width(44));
                GUILayout.Label(change.DisplayName, GUILayout.Width(180));

                if (change.Line > 0) GUILayout.Label($"{change.Line}행", EditorStyles.miniLabel, GUILayout.Width(48));
                if (!string.IsNullOrEmpty(change.Note)) GUILayout.Label(change.Note, EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();

                if (!string.IsNullOrEmpty(change.AssetPath) && GUILayout.Button("찾기", GUILayout.Width(44)))
                {
                    Object asset = AssetDatabase.LoadAssetAtPath<Object>(change.AssetPath);
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
                EditorGUILayout.LabelField($"{field.Column}", $"{Shorten(field.From)}  →  {Shorten(field.To)}");
            }
            EditorGUI.indentLevel--;
        }

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
