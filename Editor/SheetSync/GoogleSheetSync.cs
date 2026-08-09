using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace CsvPipeline
{
    /// <summary>
    /// 구글 스프레드시트에서 CSV를 받아 <c>03.DataAssets/CSV</c>에 덮어쓰는 <b>에디터 전용</b> 동기화 도구입니다.
    /// </summary>
    public static class GoogleSheetSync
    {
        /// <summary>CSV 파일들이 놓이는 프로젝트 폴더입니다.</summary>
        private static string CsvRoot => CsvPipelineSettings.Instance.CsvRootFolder;

        /// <summary>동기화 설정 에셋들이 놓이는 폴더입니다. (Editor 폴더라 빌드에 포함되지 않습니다)</summary>
        private static string SettingsRoot => CsvPipelineSettings.Instance.SheetSyncSettingsFolder;

        /// <summary>
        /// 마지막으로 받아 기록한 내용의 사본을 두는 폴더입니다. (버전 관리 대상이 아닌 Library 아래)
        /// </summary>
        private static string SnapshotRoot => CsvPipelineSettings.Instance.SnapshotFolder;

        /// <summary>로그 접두사입니다.</summary>
        private const string TAG = "[SheetSync]";

        /// <summary>설정 화면으로 안내할 때 쓰는 경로입니다.</summary>
        private const string SETTINGS_HINT = "Project Settings ▸ CSV Pipeline";

        /// <summary>자동 받기의 다음 확인 예정 시각입니다. (에디터 기동 후 경과 초)</summary>
        private static double _nextAutoPullTime;

        /// <summary>동기화가 진행 중인지 여부입니다. (중복 실행 방지)</summary>
        private static bool _running;

        // ====================================================================================================
        // 메뉴
        // ====================================================================================================

        /// <summary>설정이 갖춰진 모든 CSV를 받아옵니다.</summary>
        [MenuItem("Tools/CSV Pipeline/Google Sheet에서 받기")]
        public static void PullAllMenu()
        {
            List<GoogleSheetSyncSettings> all = FindAllSettings();
            if (all.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "동기화 설정이 없습니다",
                    $"설정 에셋이 하나도 없습니다.\n\n{SettingsRoot} 폴더를 확인하거나,\n"
                    + "메뉴 Tools ▸ CSV Pipeline ▸ Google Sheet 설정 에셋 만들기 를 실행하세요.\n\n"
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
        [MenuItem("Tools/CSV Pipeline/Google Sheet와 비교만 (쓰지 않음)")]
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

        /// <summary>설정 에셋 폴더를 프로젝트 창에서 선택합니다.</summary>
        [MenuItem("Tools/CSV Pipeline/Google Sheet 설정 폴더 열기")]
        public static void SelectSettingsFolderMenu()
        {
            var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SettingsRoot);
            if (folder == null)
            {
                EditorUtility.DisplayDialog("폴더가 없습니다",
                    $"{SettingsRoot} 폴더가 없습니다.\n\n"
                    + "메뉴 Tools ▸ CSV Pipeline ▸ Google Sheet 설정 에셋 만들기 를 먼저 실행하세요.", "확인");
                return;
            }

            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }

        /// <summary>
        /// 설정 에셋이 없는 CSV마다 설정 에셋을 하나씩 만듭니다. (이미 있는 것은 건드리지 않습니다)
        /// </summary>
        [MenuItem("Tools/CSV Pipeline/Google Sheet 설정 에셋 만들기")]
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
        // 자동 받기
        // ====================================================================================================

        /// <summary>에디터 기동 시 자동 받기 루프를 겁니다.</summary>
        [InitializeOnLoadMethod]
        private static void InstallAutoPull()
        {
            EditorApplication.update -= AutoPullTick;
            EditorApplication.update += AutoPullTick;
        }

        /// <summary>자동 받기가 켜진 설정들을 간격마다 확인합니다.</summary>
        private static void AutoPullTick()
        {
            if (_running || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.timeSinceStartup < _nextAutoPullTime) return;

            var due = new List<GoogleSheetSyncSettings>();
            float interval = 300f;

            foreach (GoogleSheetSyncSettings s in FindAllSettings())
            {
                if (!s.autoPull || !s.enabled || !s.IsConfigured) continue;

                due.Add(s);
                interval = Mathf.Min(interval, Mathf.Max(10f, s.autoPullIntervalSeconds));
            }

            // 대상이 없으면 다음 확인을 멀찍이 미뤄, 매 프레임 에셋을 뒤지지 않게 합니다.
            _nextAutoPullTime = EditorApplication.timeSinceStartup + (due.Count > 0 ? interval : 30f);
            if (due.Count == 0) return;

            // 자동 경로에서는 대화상자를 띄우지 않습니다. 확인이 필요한 상황은 건너뛰고 로그만 남깁니다.
            _ = PullManyAsync(due, interactive: false);
        }

        // ====================================================================================================
        // 동기화 본체
        // ====================================================================================================

        /// <summary>
        /// 주어진 설정들을 받아 변경된 파일만 기록하고 재임포트합니다.
        /// </summary>
        /// <param name="targets">처리할 설정 목록입니다.</param>
        /// <param name="interactive">사용자에게 확인 대화상자를 띄워도 되는 경로인지 여부입니다.</param>
        private static async Task PullManyAsync(List<GoogleSheetSyncSettings> targets, bool interactive)
        {
            if (_running) return;

            _running = true;
            var changed = new List<string>();
            int skipped = 0, failed = 0;

            try
            {
                Directory.CreateDirectory(SnapshotRoot);

                for (int i = 0; i < targets.Count; i++)
                {
                    GoogleSheetSyncSettings settings = targets[i];

                    if (interactive)
                    {
                        EditorUtility.DisplayProgressBar("Google Sheet 동기화",
                            settings.csvFileName, (float)i / Mathf.Max(1, targets.Count));
                    }

                    PullResult result = await PullOneAsync(settings, interactive);
                    switch (result)
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
        /// 대상들을 시트와 비교해 결과를 콘솔에 보고합니다. 파일은 건드리지 않습니다.
        /// </summary>
        /// <param name="targets">비교할 설정 목록입니다.</param>
        private static async Task CompareAllAsync(List<GoogleSheetSyncSettings> targets)
        {
            if (_running) return;

            _running = true;
            var report = new StringBuilder();
            int same = 0, different = 0, failed = 0;

            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    GoogleSheetSyncSettings settings = targets[i];
                    string csvFileName = settings.csvFileName;

                    EditorUtility.DisplayProgressBar("Google Sheet 비교",
                        csvFileName, (float)i / Mathf.Max(1, targets.Count));

                    string fullPath = Path.GetFullPath($"{CsvRoot}/{csvFileName}");
                    if (!File.Exists(fullPath))
                    {
                        failed++;
                        report.AppendLine($"  [실패] {csvFileName} — 로컬에 파일이 없습니다");
                        continue;
                    }

                    string body;
                    try
                    {
                        body = await DownloadTextAsync(settings.ExportUrl);
                    }
                    catch (Exception e)
                    {
                        failed++;
                        report.AppendLine($"  [실패] {csvFileName} — 받기 실패: {e.Message}");
                        continue;
                    }

                    if (LooksLikeHtml(body))
                    {
                        failed++;
                        report.AppendLine($"  [실패] {csvFileName} — CSV 대신 HTML (공유 설정을 확인하세요)");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(body))
                    {
                        failed++;
                        report.AppendLine($"  [실패] {csvFileName} — 시트가 비어 있습니다");
                        continue;
                    }

                    string local = Normalize(File.ReadAllText(fullPath));
                    string sheet = Normalize(body);
                    string difference = DescribeDifference(local, sheet);

                    if (difference == null)
                    {
                        same++;
                        continue;
                    }

                    different++;
                    report.AppendLine($"  [다름] {csvFileName}");
                    report.Append(difference);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _running = false;
            }

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

        /// <summary>
        /// 두 CSV 내용의 차이를 사람이 읽을 수 있게 설명합니다. 같으면 null을 반환합니다.
        /// </summary>
        /// <param name="local">로컬 CSV 내용입니다.</param>
        /// <param name="sheet">시트에서 받은 내용입니다.</param>
        /// <returns>차이 설명 문자열이거나, 동일하면 null입니다.</returns>
        private static string DescribeDifference(string local, string sheet)
        {
            if (local == sheet) return null;

            var text = new StringBuilder();
            string localHeader = FirstLine(local);
            string sheetHeader = FirstLine(sheet);

            if (localHeader != sheetHeader)
            {
                // 헤더가 다르면 받기가 확인을 묻고 막아 줍니다. 열 구성이 바뀐 것인지,
                // 엉뚱한 탭을 가리키는 것인지는 사람이 봐야 알 수 있습니다.
                text.AppendLine("      헤더가 다릅니다 (받기가 확인을 묻습니다)");
                text.AppendLine($"        로컬: {localHeader}");
                text.AppendLine($"        시트: {sheetHeader}");
                return text.ToString();
            }

            Dictionary<string, string> localRows = IndexByFirstField(local);
            Dictionary<string, string> sheetRows = IndexByFirstField(sheet);

            var onlyLocal = new List<string>();
            var onlySheet = new List<string>();
            var changed = new List<string>();

            foreach (KeyValuePair<string, string> pair in localRows)
            {
                if (!sheetRows.TryGetValue(pair.Key, out string sheetRow)) onlyLocal.Add(pair.Key);
                else if (sheetRow != pair.Value) changed.Add(pair.Key);
            }
            foreach (string key in sheetRows.Keys)
            {
                if (!localRows.ContainsKey(key)) onlySheet.Add(key);
            }

            // 헤더가 같으면 받기가 아무 경고 없이 덮습니다. 그래서 여기서 분명히 알려야 합니다.
            text.AppendLine("      헤더는 같고 내용이 다릅니다 (받으면 경고 없이 덮입니다)");

            if (onlyLocal.Count > 0) text.AppendLine($"        로컬에만 있는 행 {onlyLocal.Count}: {Join(onlyLocal)}");
            if (onlySheet.Count > 0) text.AppendLine($"        시트에만 있는 행 {onlySheet.Count}: {Join(onlySheet)}");
            if (changed.Count > 0) text.AppendLine($"        값이 다른 행 {changed.Count}: {Join(changed)}");

            // 첫 열이 비어 있거나 중복이면 행 대조가 성립하지 않습니다. 그때는 줄 수만 알립니다.
            if (onlyLocal.Count == 0 && onlySheet.Count == 0 && changed.Count == 0)
            {
                text.AppendLine($"        (행 대조로는 차이를 찾지 못했습니다. 줄 수 로컬 {localRows.Count} / 시트 {sheetRows.Count})");
            }

            return text.ToString();
        }

        /// <summary>목록을 보기 좋게 잇습니다. 너무 길면 뒤를 줄입니다.</summary>
        /// <param name="items">이어 붙일 항목들입니다.</param>
        /// <returns>쉼표로 이은 문자열입니다.</returns>
        private static string Join(List<string> items)
        {
            const int maxShown = 8;
            if (items.Count <= maxShown) return string.Join(", ", items);

            return string.Join(", ", items.GetRange(0, maxShown)) + $" 외 {items.Count - maxShown}개";
        }

        /// <summary>헤더를 뺀 각 줄을 첫 열(식별자) 기준으로 색인합니다.</summary>
        /// <param name="text">CSV 전체 내용입니다.</param>
        /// <returns>식별자 → 줄 전체 사전입니다.</returns>
        private static Dictionary<string, string> IndexByFirstField(string text)
        {
            var map = new Dictionary<string, string>();
            string[] lines = text.Split('\n');

            for (int i = 1; i < lines.Length; i++)   // 0번은 헤더
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                string key = FirstField(line);
                if (string.IsNullOrEmpty(key)) key = $"(빈 식별자 {i}행)";

                // 식별자가 겹치면 뒤엣것이 이깁니다. (임포터도 같은 순서로 덮어씁니다)
                map[key] = line;
            }

            return map;
        }

        /// <summary>한 줄에서 첫 열의 값을 뽑습니다. 큰따옴표로 감싼 필드를 인식합니다.</summary>
        /// <param name="line">CSV 한 줄입니다.</param>
        /// <returns>첫 열의 값입니다.</returns>
        private static string FirstField(string line)
        {
            if (line.Length == 0) return string.Empty;

            if (line[0] != '"')
            {
                int comma = line.IndexOf(',');
                return (comma < 0 ? line : line.Substring(0, comma)).Trim();
            }

            var buffer = new StringBuilder();
            for (int i = 1; i < line.Length; i++)
            {
                if (line[i] != '"') { buffer.Append(line[i]); continue; }

                // 이스케이프된 따옴표("")는 리터럴 " 로, 아니면 필드 종료입니다.
                if (i + 1 < line.Length && line[i + 1] == '"') { buffer.Append('"'); i++; continue; }
                break;
            }

            return buffer.ToString().Trim();
        }

        /// <summary>한 파일의 처리 결과입니다.</summary>
        private enum PullResult { Changed, Unchanged, Failed }

        /// <summary>
        /// 설정 하나가 가리키는 탭을 받아 검증하고, 내용이 달라졌을 때만 파일에 기록합니다.
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

            string body;
            try
            {
                body = await DownloadTextAsync(settings.ExportUrl);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{TAG} {csvFileName}: 받기 실패 — {e.Message}", settings);
                return PullResult.Failed;
            }

            // [핵심 검증] 시트가 공개돼 있지 않으면 구글은 오류가 아니라 로그인 HTML을 200으로 돌려줍니다.
            // 이걸 통과시키면 CSV가 HTML로 덮이고 파이프라인이 ScriptableObject를 망가뜨립니다.
            if (LooksLikeHtml(body))
            {
                Debug.LogError(
                    $"{TAG} {csvFileName}: CSV 대신 HTML이 왔습니다. 시트 공유 설정을 확인하세요.\n"
                    + "  (공유 → 링크가 있는 모든 사용자 → 뷰어)", settings);
                return PullResult.Failed;
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                Debug.LogWarning($"{TAG} {csvFileName}: 빈 응답이라 건너뜁니다. (탭이 비어 있는지 확인)", settings);
                return PullResult.Failed;
            }

            string normalized = Normalize(body);
            string existing = Normalize(File.ReadAllText(fullPath));
            if (existing == normalized) return PullResult.Unchanged;

            // 헤더가 달라졌다면 열을 바꿨거나 엉뚱한 탭을 가리키는 것입니다. 후자는 조용히 통과하면 안 됩니다.
            if (settings.confirmOnHeaderChange && FirstLine(existing) != FirstLine(normalized))
            {
                if (!interactive)
                {
                    Debug.LogWarning($"{TAG} {csvFileName}: 헤더가 달라 자동 받기를 건너뜁니다. "
                                   + "메뉴에서 직접 받아 확인하세요.", settings);
                    return PullResult.Failed;
                }

                string message =
                    $"{csvFileName}의 헤더가 다릅니다.\n\n"
                    + $"현재: {FirstLine(existing)}\n\n"
                    + $"시트: {FirstLine(normalized)}\n\n"
                    + $"엉뚱한 탭(gid={settings.Gid})을 가리키고 있을 수 있습니다. 덮어쓸까요?";

                if (!EditorUtility.DisplayDialog("헤더가 다릅니다", message, "덮어쓰기", "건너뛰기"))
                {
                    return PullResult.Failed;
                }
            }

            // 로컬에서 손으로 고친 흔적이 있으면 알립니다. (시트가 저작 원본이라는 규칙을 어긴 상태)
            string snapshotPath = Path.Combine(SnapshotRoot, csvFileName);
            if (File.Exists(snapshotPath) && Normalize(File.ReadAllText(snapshotPath)) != existing)
            {
                Debug.LogWarning(
                    $"{TAG} {csvFileName}: 마지막 동기화 이후 로컬에서 직접 수정된 흔적이 있습니다. "
                    + "시트 내용으로 덮어씁니다. (수정분은 git에서 되찾을 수 있습니다)", settings);
            }

            // BOM 없는 UTF-8로 기록합니다. 파이프라인의 리더가 기대하는 형식입니다.
            File.WriteAllText(fullPath, normalized, new UTF8Encoding(false));
            File.WriteAllText(snapshotPath, normalized, new UTF8Encoding(false));
            return PullResult.Changed;
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
        // 보조
        // ====================================================================================================

        /// <summary>URL의 본문을 문자열로 받아옵니다.</summary>
        /// <param name="url">받아올 주소입니다.</param>
        /// <returns>응답 본문입니다.</returns>
        private static Task<string> DownloadTextAsync(string url)
        {
            var completion = new TaskCompletionSource<string>();
            UnityWebRequest request = UnityWebRequest.Get(url);

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        completion.SetException(new Exception($"{request.responseCode} {request.error}"));
                    }
                    else
                    {
                        completion.SetResult(request.downloadHandler.text);
                    }
                }
                finally
                {
                    request.Dispose();
                }
            };

            return completion.Task;
        }

        /// <summary>응답이 CSV가 아니라 HTML 페이지인지 판별합니다.</summary>
        /// <param name="body">응답 본문입니다.</param>
        /// <returns>HTML로 보이면 true입니다.</returns>
        private static bool LooksLikeHtml(string body)
        {
            if (string.IsNullOrEmpty(body)) return false;

            string trimmed = body.TrimStart();
            string head = trimmed.Substring(0, Math.Min(200, trimmed.Length));

            return head.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
                || head.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
                || head.IndexOf("<meta ", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>줄 끝을 LF로 통일하고 BOM을 제거합니다. (내용 비교와 기록 형식을 한 가지로 맞춤)</summary>
        /// <param name="text">정규화할 문자열입니다.</param>
        /// <returns>정규화된 문자열입니다.</returns>
        private static string Normalize(string text)
        {
            return text.TrimStart('﻿').Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd() + "\n";
        }

        /// <summary>첫 줄(헤더)을 반환합니다.</summary>
        /// <param name="text">대상 문자열입니다.</param>
        /// <returns>첫 줄입니다.</returns>
        private static string FirstLine(string text)
        {
            int index = text.IndexOf('\n');
            return index < 0 ? text : text.Substring(0, index);
        }

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

            foreach (string guid in AssetDatabase.FindAssets("t:GoogleSheetSyncSettings"))
            {
                var settings = AssetDatabase.LoadAssetAtPath<GoogleSheetSyncSettings>(
                    AssetDatabase.GUIDToAssetPath(guid));

                if (settings != null) list.Add(settings);
            }

            list.Sort((a, b) => string.CompareOrdinal(a.csvFileName, b.csvFileName));
            return list;
        }
    }
}
