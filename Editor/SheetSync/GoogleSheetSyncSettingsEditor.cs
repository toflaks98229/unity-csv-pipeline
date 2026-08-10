using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// 연동 설정을 붙여넣은 그대로 두지 않고 <b>지금 어떤 상태인지</b> 보여 줍니다.
    /// 주소에서 뽑아 낸 시트 ID와 탭 번호를 드러내는 것이 핵심입니다.
    /// gid를 잘못 가리키면 엉뚱한 탭의 내용이 조용히 들어오는데, 값을 보여 주지 않으면 알 방법이 없습니다.
    /// </summary>
    [CustomEditor(typeof(GoogleSheetSyncSettings))]
    [CanEditMultipleObjects]
    public sealed class GoogleSheetSyncSettingsEditor : Editor
    {
        /// <summary>인스펙터를 그립니다.</summary>
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (targets.Length > 1) return;   // 여럿 선택했을 때는 상태를 단정할 수 없습니다.

            var settings = (GoogleSheetSyncSettings)target;

            EditorGUILayout.Space();
            DrawStatus(settings);

            EditorGUILayout.Space();
            DrawResolved(settings);

            EditorGUILayout.Space();
            DrawActions(settings);
        }

        /// <summary>지금 이 설정이 동작할 수 있는 상태인지 한 줄로 알립니다.</summary>
        /// <param name="settings">대상 설정입니다.</param>
        private static void DrawStatus(GoogleSheetSyncSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.sheetUrl))
            {
                EditorGUILayout.HelpBox(
                    "시트 주소가 비어 있습니다. 브라우저 주소창의 링크를 그대로 붙여넣으세요.\n"
                    + "열어 둔 탭이 대상이 됩니다.", MessageType.Info);
                return;
            }

            if (!settings.IsConfigured)
            {
                EditorGUILayout.HelpBox(
                    "주소에서 시트 ID를 찾지 못했습니다. docs.google.com/spreadsheets/d/... 형태여야 합니다.",
                    MessageType.Warning);
                return;
            }

            if (!settings.enabled)
            {
                EditorGUILayout.HelpBox("링크는 갖췄지만 꺼져 있어 받아오지 않습니다.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                settings.autoPull
                    ? $"받을 준비가 됐습니다. {settings.autoPullIntervalSeconds:0}초마다 자동으로 확인합니다."
                    : "받을 준비가 됐습니다. 메뉴에서 직접 받아옵니다.",
                MessageType.None);
        }

        /// <summary>주소에서 뽑아 낸 값과 대상 파일의 상태를 보여 줍니다.</summary>
        /// <param name="settings">대상 설정입니다.</param>
        private static void DrawResolved(GoogleSheetSyncSettings settings)
        {
            EditorGUILayout.LabelField("주소에서 읽어 낸 것", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("시트 ID", settings.SpreadsheetId);
                EditorGUILayout.TextField("탭 번호 (gid)", settings.Gid);
            }

            EditorGUILayout.LabelField("대상 표", EditorStyles.boldLabel);

            string csvPath = CsvAssetPipeline.FindCsvPath(settings.csvFileName);
            if (string.IsNullOrEmpty(settings.csvFileName))
            {
                EditorGUILayout.HelpBox("대상 파일 이름이 비어 있습니다.", MessageType.Warning);
            }
            else if (csvPath == null)
            {
                EditorGUILayout.HelpBox(
                    $"'{settings.csvFileName}'을(를) 프로젝트에서 찾지 못했습니다. 받아도 쓸 곳이 없습니다.",
                    MessageType.Warning);
            }
            else
            {
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.TextField("경로", csvPath);
            }

            EditorGUILayout.LabelField("마지막 동기화", LastSyncLabel(settings));
        }

        /// <summary>이 설정으로 할 수 있는 동작들입니다.</summary>
        /// <param name="settings">대상 설정입니다.</param>
        private static void DrawActions(GoogleSheetSyncSettings settings)
        {
            using (new EditorGUI.DisabledScope(!settings.IsConfigured))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("이 표만 비교 (쓰지 않음)")) GoogleSheetSync.CompareOne(settings);

                // 브라우저를 여는 것은 사람이 이 버튼을 눌렀을 때뿐입니다.
                if (GUILayout.Button("브라우저에서 시트 열기", GUILayout.Width(180)))
                {
                    Application.OpenURL(
                        $"https://docs.google.com/spreadsheets/d/{settings.SpreadsheetId}/edit#gid={settings.Gid}");
                }
            }
        }

        /// <summary>
        /// 마지막으로 받아 기록한 시각입니다. 스냅숏 파일의 수정 시각을 그대로 씁니다.
        /// 따로 상태를 저장하지 않으므로 어긋날 일이 없습니다.
        /// </summary>
        /// <param name="settings">대상 설정입니다.</param>
        /// <returns>표시할 문자열입니다.</returns>
        private static string LastSyncLabel(GoogleSheetSyncSettings settings)
        {
            if (string.IsNullOrEmpty(settings.csvFileName)) return "—";

            string snapshot = Path.Combine(CsvPipelineSettings.Instance.SnapshotFolder, settings.csvFileName);
            if (!File.Exists(snapshot)) return "받은 적 없음";

            DateTime when = File.GetLastWriteTime(snapshot);
            return $"{when:yyyy-MM-dd HH:mm} ({(DateTime.Now - when).TotalHours:0.#}시간 전)";
        }
    }
}
