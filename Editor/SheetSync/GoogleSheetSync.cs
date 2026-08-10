using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace CsvPipeline
{
    /// <summary>
    /// 구글 스프레드시트에서 표를 받아 CSV 폴더에 덮어쓰는 <b>에디터 전용</b> 동기화 도구입니다.
    /// 받기는 <see cref="SheetDownloader"/>, 비교는 <see cref="SheetDiff"/>,
    /// 사본은 <see cref="SheetSnapshot"/>, 시점은 <see cref="SheetSyncScheduler"/>가 맡고,
    /// 여기에는 <b>사람에게 무엇을 보여 주고 무엇을 파일로 쓸지</b>만 남깁니다.
    /// </summary>
    public static class GoogleSheetSync
    {
        /// <summary>CSV 파일들이 놓이는 프로젝트 폴더입니다.</summary>
        private static string CsvRoot => CsvPipelineSettings.Instance.CsvRootFolder;

        /// <summary>동기화 설정 에셋들이 놓이는 폴더입니다. (Editor 폴더라 빌드에 포함되지 않습니다)</summary>
        private static string SettingsRoot => CsvPipelineSettings.Instance.SheetSyncSettingsFolder;

        /// <summary>로그 접두사입니다.</summary>
        private const string TAG = "[SheetSync]";

        /// <summary>설정 화면으로 안내할 때 쓰는 경로입니다.</summary>
        private const string SETTINGS_HINT = "Project Settings ▸ CSV Pipeline";

        /// <summary>동기화가 진행 중인지 여부입니다. (중복 실행 방지)</summary>
        private static bool _running;

        /// <summary>지금 동기화가 돌고 있는지 여부입니다.</summary>
        public static bool IsRunning => _running;

        // ====================================================================================================
        // 메뉴
        // ====================================================================================================

        /// <summary>설정이 갖춰진 모든 CSV를 받아옵니다.</summary>
        [MenuItem("Tools/CSV Pipeline/Google Sheet에서 받기", false, 40)]
        public static void PullAllMenu()
        {
            List<GoogleSheetSyncSettings> all = FindAllSettings();
            if (all.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "동기화 설정이 없습니다",
                    $"설정 에셋이 하나도 없습니다.\n\n{SettingsRoot} 폴더를 확인하거나,\n"
                    + "메뉴 Tools ▸ CSV Pipeline ▸ Google Sheet 설정 만들기 를 실행하세요.\n\n"
                    + $"CSV 폴더 위치는 {SETTINGS_HINT} 에서 바꿉니다.",
                    "확인");
                return;
            }

            var ready = all.FindAll(s => s.enabled && s.IsConfigured);
            if (ready.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "받아올 대상이 없습니다",
                    $"설정 에셋은 {all.Count}개 있지만, 링크가 채워지고 켜져 있는 항목이 없습니다.\n\n"
                    + $"{SettingsRoot} 안의 에셋에 시트 주소를 붙여넣고 Enabled를 켜 주세요.",
                    "확인");
                return;
            }

            _ = PullManyAsync(ready, interactive: true);
        }

        /// <summary>
        /// 시트와 로컬 CSV의 차이를 조사해 보고만 합니다. <b>파일을 쓰지 않습니다.</b>
        /// </summary>
        [MenuItem("Tools/CSV Pipeline/Google Sheet와 비교만", false, 41)]
        public static void CompareAllMenu()
        {
            var ready = FindAllSettings().FindAll(s => s.enabled && s.IsConfigured);
            if (ready.Count == 0)
            {
                EditorUtility.DisplayDialog("비교할 대상이 없습니다",
                    "링크가 채워지고 켜져 있는 설정이 없습니다.", "확인");
                return;
            }

            _ = CompareAllAsync(ready);
        }

        /// <summary>
        /// 설정 하나만 시트와 비교해 결과를 콘솔에 보고합니다. <b>파일을 쓰지 않습니다.</b>
        /// </summary>
        /// <param name="settings">비교할 설정입니다.</param>
        public static void CompareOne(GoogleSheetSyncSettings settings)
        {
            if (settings == null || !settings.IsConfigured) return;

            _ = CompareAllAsync(new List<GoogleSheetSyncSettings> { settings });
        }

        /// <summary>설정 하나만 시트에서 받아옵니다. 내용이 달라졌을 때만 기록합니다.</summary>
        /// <param name="settings">받아올 설정입니다.</param>
        public static void PullOne(GoogleSheetSyncSettings settings)
        {
            if (settings == null || !settings.IsConfigured) return;

            _ = PullManyAsync(new List<GoogleSheetSyncSettings> { settings }, interactive: true);
        }

        /// <summary>
        /// 자동 받기 경로입니다. <b>대화상자를 띄우지 않습니다.</b>
        /// 확인이 필요한 상황은 건너뛰고 로그만 남깁니다.
        /// </summary>
        /// <param name="targets">이번에 받을 설정들입니다.</param>
        public static void PullAutomatically(List<GoogleSheetSyncSettings> targets)
            => _ = PullManyAsync(targets, interactive: false);

        /// <summary>프로젝트의 모든 연동 설정입니다. 파일 이름 순입니다.</summary>
        /// <returns>찾은 설정 에셋 목록입니다.</returns>
        public static List<GoogleSheetSyncSettings> FindAll() => FindAllSettings();

        /// <summary>연동 설정의 존재 여부에 맞춰 자동 받기 루프를 다시 겁니다.</summary>
        internal static void RefreshAutoPullHook() => SheetSyncScheduler.Refresh();

        /// <summary>설정 에셋 폴더를 프로젝트 창에서 선택합니다.</summary>
        public static void SelectSettingsFolderMenu()
        {
            var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SettingsRoot);
            if (folder == null)
            {
                EditorUtility.DisplayDialog("폴더가 없습니다",
                    $"{SettingsRoot} 폴더가 없습니다.\n\n"
                    + "메뉴 Tools ▸ CSV Pipeline ▸ Google Sheet 설정 만들기 를 먼저 실행하세요.", "확인");
                return;
            }

            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }

        /// <summary>
        /// 설정 에셋이 없는 CSV마다 설정 에셋을 하나씩 만듭니다. (이미 있는 것은 건드리지 않습니다)
        /// </summary>
        [MenuItem("Tools/CSV Pipeline/Google Sheet 설정 만들기", false, 42)]
        public static void CreateMissingSettingsMenu()
        {
            string csvRoot = CsvRoot;
            if (!AssetDatabase.IsValidFolder(csvRoot))
            {
                EditorUtility.DisplayDialog("CSV 폴더가 없습니다",
                    $"CSV 루트 폴더를 찾지 못했습니다: {csvRoot}\n\n"
                    + $"{SETTINGS_HINT} 에서 실제 폴더를 지정하십시오.", "확인");
                return;
            }

            string settingsRoot = SettingsRoot;
            CsvAssetPipeline.EnsureFolder(settingsRoot);

            var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (GoogleSheetSyncSettings s in FindAllSettings())
            {
                if (!string.IsNullOrWhiteSpace(s.csvFileName)) covered.Add(s.csvFileName);
            }

            int created = 0;
            foreach (string path in Directory.GetFiles(csvRoot, "*.csv", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(path);
                if (covered.Contains(fileName) || IsTempFile(fileName)) continue;

                var settings = ScriptableObject.CreateInstance<GoogleSheetSyncSettings>();
                settings.csvFileName = fileName;
                // 링크를 채우기 전에는 꺼 둡니다. 켜진 채로 비어 있으면 매번 실패 로그만 쌓입니다.
                settings.enabled = false;

                string assetName = "SheetSync_" + Path.GetFileNameWithoutExtension(fileName) + ".asset";
                AssetDatabase.CreateAsset(settings, $"{settingsRoot}/{assetName}");
                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"{TAG} 설정 에셋 {created}개를 만들었습니다. 각 에셋에 시트 주소를 넣고 Enabled를 켜 주세요.");
            SelectSettingsFolderMenu();
        }

        // ====================================================================================================
        // 받기
        // ====================================================================================================

        /// <summary>한 파일의 처리 결과입니다.</summary>
        private enum PullResult { Changed, Unchanged, Failed }

        /// <summary>
        /// 주어진 설정들을 받아 변경된 파일만 기록하고 재임포트합니다.
        /// </summary>
        /// <param name="targets">처리할 설정 목록입니다.</param>
        /// <param name="interactive">사용자에게 확인 대화상자를 띄워도 되는 경로인지 여부입니다.</param>
        private static async Task PullManyAsync(List<GoogleSheetSyncSettings> targets, bool interactive)
        {
            if (_running || targets == null || targets.Count == 0) return;

            _running = true;
            var changed = new List<string>();
            int skipped = 0, failed = 0;

            try
            {
                SheetSnapshot.EnsureFolder();

                for (int i = 0; i < targets.Count; i++)
                {
                    GoogleSheetSyncSettings settings = targets[i];

                    if (interactive)
                    {
                        EditorUtility.DisplayProgressBar("Google Sheet 동기화",
                            settings.csvFileName, (float)i / Mathf.Max(1, targets.Count));
                    }

                    switch (await PullOneAsync(settings, interactive))
                    {
                        case PullResult.Changed: changed.Add(settings.csvFileName); break;
                        case PullResult.Unchanged: skipped++; break;
                        default: failed++; break;
                    }
                }
            }
            finally
            {
                if (interactive) EditorUtility.ClearProgressBar();
                _running = false;
            }

            if (changed.Count > 0) ReimportAll(changed);

            string summary = $"{TAG} 완료 — 갱신 {changed.Count} / 동일 {skipped} / 실패 {failed}";
            if (failed > 0) Debug.LogWarning(summary);
            else if (changed.Count > 0) Debug.Log($"{summary}\n  {string.Join("\n  ", changed)}");
            else if (interactive) Debug.Log(summary);
        }

        /// <summary>
        /// 설정 하나가 가리키는 탭을 받아, 내용이 달라졌을 때만 파일에 기록합니다.
        /// </summary>
        /// <param name="settings">처리할 설정입니다.</param>
        /// <param name="interactive">확인 대화상자를 띄워도 되는 경로인지 여부입니다.</param>
        /// <returns>처리 결과입니다.</returns>
        private static async Task<PullResult> PullOneAsync(GoogleSheetSyncSettings settings, bool interactive)
        {
            string csvFileName = settings.csvFileName;
            string assetPath = $"{CsvRoot}/{csvFileName}";
            string fullPath = Path.GetFullPath(assetPath);

            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"{TAG} {csvFileName}: 대상 CSV가 프로젝트에 없습니다. ({assetPath})", settings);
                return PullResult.Failed;
            }

            SheetFetch fetch = await SheetDownloader.FetchAsync(settings.ExportUrl);
            if (!fetch.Ok)
            {
                if (fetch.IsAccessDenied)
                {
                    Debug.LogError($"{TAG} {csvFileName}: {fetch.Error}\n{SheetDownloader.AccessDeniedHint()}", settings);
                }
                else
                {
                    Debug.LogWarning($"{TAG} {csvFileName}: {fetch.Error}", settings);
                }
                return PullResult.Failed;
            }

            string incoming = fetch.Text;
            string existing = SheetDiff.Normalize(File.ReadAllText(fullPath));
            if (existing == incoming) return PullResult.Unchanged;

            if (!ConfirmHeaderChange(settings, existing, incoming, interactive)) return PullResult.Failed;

            // 로컬에서 손으로 고친 흔적이 있으면 알립니다. (시트가 저작 원본이라는 규칙을 어긴 상태)
            if (SheetSnapshot.DivergedFromLocal(csvFileName, existing))
            {
                Debug.LogWarning(
                    $"{TAG} {csvFileName}: 마지막 동기화 이후 로컬에서 직접 수정된 흔적이 있습니다. "
                    + "시트 내용으로 덮어씁니다. (수정분은 git에서 되찾을 수 있습니다)", settings);
            }

            // BOM 없는 UTF-8로 기록합니다. 파이프라인의 리더가 기대하는 형식입니다.
            File.WriteAllText(fullPath, incoming, new UTF8Encoding(false));
            SheetSnapshot.Write(csvFileName, incoming);
            return PullResult.Changed;
        }

        /// <summary>
        /// 헤더가 달라졌을 때 덮어써도 되는지 정합니다.
        /// 열을 바꾼 것일 수도, <b>엉뚱한 탭을 가리키는 것</b>일 수도 있어 조용히 통과시키지 않습니다.
        /// </summary>
        /// <param name="settings">처리 중인 설정입니다.</param>
        /// <param name="existing">지금 로컬 내용입니다.</param>
        /// <param name="incoming">시트에서 받은 내용입니다.</param>
        /// <param name="interactive">대화상자를 띄워도 되는 경로인지 여부입니다.</param>
        /// <returns>계속 진행해도 되면 true입니다.</returns>
        private static bool ConfirmHeaderChange(GoogleSheetSyncSettings settings,
                                                string existing, string incoming, bool interactive)
        {
            if (!settings.confirmOnHeaderChange) return true;

            string localHeader = SheetDiff.FirstLine(existing);
            string sheetHeader = SheetDiff.FirstLine(incoming);
            if (localHeader == sheetHeader) return true;

            if (!interactive)
            {
                Debug.LogWarning($"{TAG} {settings.csvFileName}: 헤더가 달라 자동 받기를 건너뜁니다. "
                               + "메뉴에서 직접 받아 확인하세요.", settings);
                return false;
            }

            string message =
                $"{settings.csvFileName}의 헤더가 다릅니다.\n\n"
                + $"현재: {localHeader}\n\n"
                + $"시트: {sheetHeader}\n\n"
                + $"엉뚱한 탭(gid={settings.Gid})을 가리키고 있을 수 있습니다. 덮어쓸까요?";

            return EditorUtility.DisplayDialog("헤더가 다릅니다", message, "덮어쓰기", "건너뛰기");
        }

        /// <summary>
        /// 바뀐 CSV들을 강제 재임포트해 파이프라인을 발화시킵니다.
        /// </summary>
        /// <param name="fileNames">재임포트할 CSV 파일 이름 목록입니다.</param>
        private static void ReimportAll(List<string> fileNames)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string fileName in fileNames)
                {
                    // 임포터는 CSV가 "변경"될 때 발화하므로 CsvRebuildMenu와 같은 강제 재임포트를 씁니다.
                    AssetDatabase.ImportAsset($"{CsvRoot}/{fileName}", ImportAssetOptions.ForceUpdate);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh();
        }

        // ====================================================================================================
        // 비교
        // ====================================================================================================

        /// <summary>
        /// 대상들을 시트와 비교해 결과를 콘솔에 보고합니다. 파일은 건드리지 않습니다.
        /// </summary>
        /// <param name="targets">비교할 설정 목록입니다.</param>
        private static async Task CompareAllAsync(List<GoogleSheetSyncSettings> targets)
        {
            if (_running || targets == null || targets.Count == 0) return;

            _running = true;
            var report = new StringBuilder();
            int same = 0, different = 0, failed = 0;

            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    GoogleSheetSyncSettings settings = targets[i];

                    EditorUtility.DisplayProgressBar("Google Sheet 비교",
                        settings.csvFileName, (float)i / Mathf.Max(1, targets.Count));

                    switch (await CompareOneAsync(settings, report))
                    {
                        case CompareResult.Same: same++; break;
                        case CompareResult.Different: different++; break;
                        default: failed++; break;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _running = false;
            }

            EmitCompareReport(report, same, different, failed);
        }

        /// <summary>한 표의 비교 결과입니다.</summary>
        private enum CompareResult { Same, Different, Failed }

        /// <summary>
        /// 설정 하나를 시트와 비교하고, 알릴 내용을 보고문에 덧붙입니다.
        /// </summary>
        /// <param name="settings">비교할 설정입니다.</param>
        /// <param name="report">결과를 덧붙일 보고문입니다.</param>
        /// <returns>비교 결과입니다.</returns>
        private static async Task<CompareResult> CompareOneAsync(GoogleSheetSyncSettings settings, StringBuilder report)
        {
            string csvFileName = settings.csvFileName;
            string fullPath = Path.GetFullPath($"{CsvRoot}/{csvFileName}");

            if (!File.Exists(fullPath))
            {
                report.AppendLine($"  [실패] {csvFileName} — 로컬에 파일이 없습니다");
                return CompareResult.Failed;
            }

            SheetFetch fetch = await SheetDownloader.FetchAsync(settings.ExportUrl);
            if (!fetch.Ok)
            {
                report.AppendLine($"  [실패] {csvFileName} — {fetch.Error}");
                return CompareResult.Failed;
            }

            string difference = SheetDiff.Describe(SheetDiff.Normalize(File.ReadAllText(fullPath)), fetch.Text);
            if (difference == null) return CompareResult.Same;

            report.AppendLine($"  [다름] {csvFileName}");
            report.Append(difference);
            return CompareResult.Different;
        }

        /// <summary>비교 결과를 콘솔과 대화상자로 알립니다.</summary>
        /// <param name="report">모아 둔 상세 보고문입니다.</param>
        /// <param name="same">동일한 표의 수입니다.</param>
        /// <param name="different">다른 표의 수입니다.</param>
        /// <param name="failed">비교하지 못한 표의 수입니다.</param>
        private static void EmitCompareReport(StringBuilder report, int same, int different, int failed)
        {
            string headline = $"{TAG} 비교 결과 — 동일 {same} / 다름 {different} / 실패 {failed}";

            if (different == 0 && failed == 0)
            {
                Debug.Log($"{headline}\n  모든 표가 시트와 일치합니다. 받아도 잃을 것이 없습니다.");
            }
            else
            {
                Debug.LogWarning($"{headline}\n{report}");
            }

            EditorUtility.DisplayDialog("Google Sheet 비교",
                $"동일 {same} / 다름 {different} / 실패 {failed}\n\n"
                + (different + failed > 0
                    ? "자세한 내용은 콘솔을 보세요.\n\n다름으로 나온 표는 받으면 로컬 내용이 시트 내용으로 바뀝니다."
                    : "모든 표가 시트와 일치합니다."),
                "확인");
        }

        // ====================================================================================================
        // 보조
        // ====================================================================================================

        /// <summary>
        /// 편집기가 만든 임시 파일인지 판별합니다.
        /// </summary>
        /// <param name="fileName">판별할 파일 이름입니다.</param>
        /// <returns>임시 파일이면 true입니다.</returns>
        private static bool IsTempFile(string fileName)
        {
            return fileName.StartsWith("~$", StringComparison.Ordinal)
                || fileName.StartsWith(".", StringComparison.Ordinal);
        }

        /// <summary>프로젝트의 모든 동기화 설정 에셋을 파일 이름 순으로 반환합니다.</summary>
        /// <returns>찾은 설정 에셋 목록입니다.</returns>
        private static List<GoogleSheetSyncSettings> FindAllSettings()
        {
            var list = new List<GoogleSheetSyncSettings>();

            foreach (string path in CsvAssets.Current.FindPaths("t:GoogleSheetSyncSettings"))
            {
                if (CsvAssets.Current.Load(path, typeof(GoogleSheetSyncSettings)) is GoogleSheetSyncSettings settings)
                {
                    list.Add(settings);
                }
            }

            list.Sort((a, b) => string.CompareOrdinal(a.csvFileName, b.csvFileName));
            return list;
        }
    }
}
