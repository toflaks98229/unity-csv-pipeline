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
    /// <b>Editor-only</b> sync tool that pulls tables from Google Sheets and overwrites the CSV folder.
    /// <see cref="SheetDownloader"/> does the pulling, <see cref="SheetDiff"/> the comparing,
    /// <see cref="SheetSnapshot"/> the snapshots, and <see cref="SheetSyncScheduler"/> the timing,
    /// so only <b>what to show the user and what to write to disk</b> stays here.
    /// </summary>
    public static class GoogleSheetSync
    {
        /// <summary>Project folder that holds the CSV files.</summary>
        private static string CsvRoot => CsvPipelineSettings.Instance.CsvRootFolder;

        /// <summary>Folder that holds the sync settings assets. (An Editor folder, so it stays out of builds.)</summary>
        private static string SettingsRoot => CsvPipelineSettings.Instance.SheetSyncSettingsFolder;

        /// <summary>Log prefix.</summary>
        private const string TAG = "[SheetSync]";

        /// <summary>Path used when pointing the user at the settings screen.</summary>
        private const string SETTINGS_HINT = "Project Settings ▸ CSV Pipeline";

        /// <summary>Whether a sync is in progress. (Guards against a second run.)</summary>
        private static bool _running;

        /// <summary>Whether a sync is running right now.</summary>
        public static bool IsRunning => _running;

        // ====================================================================================================
        // 메뉴
        // ====================================================================================================

        /// <summary>Pulls every CSV that has its settings filled in.</summary>
        [MenuItem("Tools/CSV Pipeline/Pull from Google Sheets", false, 40)]
        public static void PullAllMenu()
        {
            List<GoogleSheetSyncSettings> all = FindAllSettings();
            if (all.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "No sync settings",
                    $"There is not a single settings asset.\n\nCheck the {SettingsRoot} folder,\n"
                    + "or run the menu Tools ▸ CSV Pipeline ▸ Create Google Sheet Settings.\n\n"
                    + $"The CSV folder location is changed in {SETTINGS_HINT}.",
                    "OK");
                return;
            }

            var ready = all.FindAll(s => s.enabled && s.IsConfigured);
            if (ready.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Nothing to pull",
                    $"There are {all.Count} settings assets, but none of them has a link filled in and is enabled.\n\n"
                    + $"Paste the sheet address into the assets under {SettingsRoot} and turn Enabled on.",
                    "OK");
                return;
            }

            CsvAsync.Forget(PullManyAsync(ready, interactive: true), TAG, "sheet pull");
        }

        /// <summary>
        /// Only inspects and reports the difference between the sheet and the local CSV. <b>Writes no files.</b>
        /// </summary>
        [MenuItem("Tools/CSV Pipeline/Compare with Google Sheets", false, 41)]
        public static void CompareAllMenu()
        {
            var ready = FindAllSettings().FindAll(s => s.enabled && s.IsConfigured);
            if (ready.Count == 0)
            {
                EditorUtility.DisplayDialog("Nothing to compare",
                    "No settings have a link filled in and are enabled.", "OK");
                return;
            }

            CsvAsync.Forget(CompareAllAsync(ready), TAG, "sheet compare");
        }

        /// <summary>
        /// Compares one settings asset against the sheet and reports the result to the console. <b>Writes no files.</b>
        /// </summary>
        /// <param name="settings">Settings to compare.</param>
        public static void CompareOne(GoogleSheetSyncSettings settings)
        {
            if (settings == null || !settings.IsConfigured) return;

            CsvAsync.Forget(CompareAllAsync(new List<GoogleSheetSyncSettings> { settings }), TAG, "sheet compare");
        }

        /// <summary>Pulls one settings asset from the sheet. Writes only when the content changed.</summary>
        /// <param name="settings">Settings to pull.</param>
        public static void PullOne(GoogleSheetSyncSettings settings)
        {
            if (settings == null || !settings.IsConfigured) return;

            CsvAsync.Forget(PullManyAsync(new List<GoogleSheetSyncSettings> { settings }, interactive: true),
                            TAG, "sheet pull");
        }

        /// <summary>
        /// The automatic pull path. <b>It shows no dialogs.</b>
        /// Anything that would need confirmation is skipped and only logged.
        /// </summary>
        /// <param name="targets">Settings to pull this time.</param>
        public static void PullAutomatically(List<GoogleSheetSyncSettings> targets)
            => CsvAsync.Forget(PullManyAsync(targets, interactive: false), TAG, "automatic sheet pull");

        /// <summary>Every sync settings asset in the project, ordered by file name.</summary>
        /// <returns>List of settings assets found.</returns>
        public static List<GoogleSheetSyncSettings> FindAll() => FindAllSettings();

        /// <summary>Re-arms the automatic pull loop according to whether any sync settings exist.</summary>
        internal static void RefreshAutoPullHook() => SheetSyncScheduler.Refresh();

        /// <summary>Selects the settings asset folder in the Project window.</summary>
        public static void SelectSettingsFolderMenu()
        {
            var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SettingsRoot);
            if (folder == null)
            {
                EditorUtility.DisplayDialog("Folder not found",
                    $"There is no {SettingsRoot} folder.\n\n"
                    + "Run the menu Tools ▸ CSV Pipeline ▸ Create Google Sheet Settings first.", "OK");
                return;
            }

            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }

        /// <summary>
        /// Creates one settings asset for every CSV that has none. (Existing ones are left alone.)
        /// </summary>
        [MenuItem("Tools/CSV Pipeline/Create Google Sheet Settings", false, 42)]
        public static void CreateMissingSettingsMenu()
        {
            string csvRoot = CsvRoot;
            if (!AssetDatabase.IsValidFolder(csvRoot))
            {
                EditorUtility.DisplayDialog("CSV folder not found",
                    $"Could not find the CSV root folder: {csvRoot}\n\n"
                    + $"Point {SETTINGS_HINT} at the folder you actually use.", "OK");
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

            Debug.Log($"{TAG} Created {created} settings assets. Put the sheet address into each one and turn Enabled on.");
            SelectSettingsFolderMenu();
        }

        // ====================================================================================================
        // 받기
        // ====================================================================================================

        /// <summary>Result of processing one file.</summary>
        private enum PullResult { Changed, Unchanged, Failed }

        /// <summary>
        /// Pulls the given settings, then writes and reimports only the files that changed.
        /// </summary>
        /// <param name="targets">Settings to process.</param>
        /// <param name="interactive">Whether this path may show confirmation dialogs to the user.</param>
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
                        EditorUtility.DisplayProgressBar("Google Sheet Sync",
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

            string summary = $"{TAG} Done — updated {changed.Count} / unchanged {skipped} / failed {failed}";
            if (failed > 0) Debug.LogWarning(summary);
            else if (changed.Count > 0) Debug.Log($"{summary}\n  {string.Join("\n  ", changed)}");
            else if (interactive) Debug.Log(summary);
        }

        /// <summary>
        /// Pulls the tab one settings asset points at, and writes the file only when the content changed.
        /// </summary>
        /// <param name="settings">Settings to process.</param>
        /// <param name="interactive">Whether this path may show confirmation dialogs.</param>
        /// <returns>Result of the pull.</returns>
        private static async Task<PullResult> PullOneAsync(GoogleSheetSyncSettings settings, bool interactive)
        {
            string csvFileName = settings.csvFileName;
            string assetPath = $"{CsvRoot}/{csvFileName}";
            string fullPath = Path.GetFullPath(assetPath);

            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"{TAG} {csvFileName}: the target CSV is not in the project. ({assetPath})", settings);
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
                    $"{TAG} {csvFileName}: there are signs of a direct local edit since the last sync. "
                    + "Overwriting with the sheet content. (You can recover the edit from git.)", settings);
            }

            // 인코딩은 설정을 따릅니다. 기본은 BOM을 붙이는 쪽입니다 — 받아 온 표를 Excel로 열어 보는
            // 것이 흔한데, BOM이 없으면 그때 한글이 깨져 보입니다. 읽는 쪽은 어느 쪽이든 같습니다.
            try
            {
                File.WriteAllText(fullPath, incoming, CsvPipelineSettings.TableEncoding);
                SheetSnapshot.Write(csvFileName, incoming);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // 표를 Excel로 열어 둔 채 받기를 누르면 여기서 막힙니다. 가장 흔한 실패이면서
                // 가장 조용한 실패였습니다 — 예외가 아무 데도 닿지 않아 '아무 일도 안 일어남'으로 보였습니다.
                Debug.LogError(
                    $"{TAG} {csvFileName}: could not write the table — {e.Message}\n"
                    + "  Check whether Excel or another program is holding this file open. "
                    + "The pulled content was not written, and the existing table is untouched.", settings);
                return PullResult.Failed;
            }

            return PullResult.Changed;
        }

        /// <summary>
        /// Decides whether it is safe to overwrite when the header changed.
        /// The columns may have been reworked, or the settings may <b>point at the wrong tab</b>,
        /// so this never passes silently.
        /// </summary>
        /// <param name="settings">Settings being processed.</param>
        /// <param name="existing">Current local content.</param>
        /// <param name="incoming">Content pulled from the sheet.</param>
        /// <param name="interactive">Whether this path may show dialogs.</param>
        /// <returns>True when it is safe to continue.</returns>
        private static bool ConfirmHeaderChange(GoogleSheetSyncSettings settings,
                                                string existing, string incoming, bool interactive)
        {
            if (!settings.confirmOnHeaderChange) return true;

            string localHeader = SheetDiff.FirstLine(existing);
            string sheetHeader = SheetDiff.FirstLine(incoming);
            if (localHeader == sheetHeader) return true;

            if (!interactive)
            {
                Debug.LogWarning($"{TAG} {settings.csvFileName}: the headers differ, so the automatic pull is skipped. "
                               + "Pull it from the menu yourself and check it.", settings);
                return false;
            }

            string message =
                $"The header of {settings.csvFileName} differs.\n\n"
                + $"Local: {localHeader}\n\n"
                + $"Sheet: {sheetHeader}\n\n"
                + $"The settings may point at the wrong tab (gid={settings.Gid}). Overwrite?";

            return EditorUtility.DisplayDialog("Headers differ", message, "Overwrite", "Skip");
        }

        /// <summary>
        /// Force-reimports the changed CSVs so the pipeline fires.
        /// </summary>
        /// <param name="fileNames">CSV file names to reimport.</param>
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
        /// Compares the targets against the sheet and reports the result to the console. It touches no files.
        /// </summary>
        /// <param name="targets">Settings to compare.</param>
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

                    EditorUtility.DisplayProgressBar("Google Sheet Compare",
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

        /// <summary>Comparison result for one table.</summary>
        private enum CompareResult { Same, Different, Failed }

        /// <summary>
        /// Compares one settings asset against the sheet and appends what to tell the user to the report.
        /// </summary>
        /// <param name="settings">Settings to compare.</param>
        /// <param name="report">Report to append the result to.</param>
        /// <returns>Comparison result.</returns>
        private static async Task<CompareResult> CompareOneAsync(GoogleSheetSyncSettings settings, StringBuilder report)
        {
            string csvFileName = settings.csvFileName;
            string fullPath = Path.GetFullPath($"{CsvRoot}/{csvFileName}");

            if (!File.Exists(fullPath))
            {
                report.AppendLine($"  [failed] {csvFileName} — the file does not exist locally");
                return CompareResult.Failed;
            }

            SheetFetch fetch = await SheetDownloader.FetchAsync(settings.ExportUrl);
            if (!fetch.Ok)
            {
                report.AppendLine($"  [failed] {csvFileName} — {fetch.Error}");
                return CompareResult.Failed;
            }

            string difference = SheetDiff.Describe(SheetDiff.Normalize(File.ReadAllText(fullPath)), fetch.Text);
            if (difference == null) return CompareResult.Same;

            report.AppendLine($"  [different] {csvFileName}");
            report.Append(difference);
            return CompareResult.Different;
        }

        /// <summary>Reports the comparison result to the console and a dialog.</summary>
        /// <param name="report">Detailed report collected so far.</param>
        /// <param name="same">Number of tables that match.</param>
        /// <param name="different">Number of tables that differ.</param>
        /// <param name="failed">Number of tables that could not be compared.</param>
        private static void EmitCompareReport(StringBuilder report, int same, int different, int failed)
        {
            string headline = $"{TAG} Compare result — same {same} / different {different} / failed {failed}";

            if (different == 0 && failed == 0)
            {
                Debug.Log($"{headline}\n  Every table matches its sheet. A pull would lose nothing.");
            }
            else
            {
                Debug.LogWarning($"{headline}\n{report}");
            }

            EditorUtility.DisplayDialog("Google Sheet Compare",
                $"Same {same} / different {different} / failed {failed}\n\n"
                + (different + failed > 0
                    ? "See the console for the details.\n\nPulling a table listed as different replaces the local content with the sheet content."
                    : "Every table matches its sheet."),
                "OK");
        }

        // ====================================================================================================
        // 보조
        // ====================================================================================================

        /// <summary>
        /// Tells whether the file is a temporary file left by an editor program.
        /// </summary>
        /// <param name="fileName">File name to test.</param>
        /// <returns>True when it is a temporary file.</returns>
        private static bool IsTempFile(string fileName)
        {
            return fileName.StartsWith("~$", StringComparison.Ordinal)
                || fileName.StartsWith(".", StringComparison.Ordinal);
        }

        /// <summary>Returns every sync settings asset in the project, ordered by file name.</summary>
        /// <returns>List of settings assets found.</returns>
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
